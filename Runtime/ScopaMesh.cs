// uncomment to enable debug messages
// #define SCOPA_MESH_DEBUG
// #define SCOPA_MESH_VERBOSE

#if SCOPA_MESH_DEBUG
using System.Diagnostics;
using Debug = UnityEngine.Debug;
#endif

using System;
using System.Linq;
using System.Collections.Generic;
using Sledge.Formats.Map.Objects;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;
using Mesh = UnityEngine.Mesh;
using Vector3 = UnityEngine.Vector3;

#if SCOPA_USE_BURST
using Unity.Burst;
using Unity.Burst.CompilerServices;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Scopa {
    /// <summary>main class for Scopa mesh generation / geo functions</summary>
    public static class ScopaMesh {
        /// <summary>
        /// <para>Main data class that schedules jobs to process all of one (1) entity's Quake brushes into Unity mesh data and colliders, all at once.</para>
        /// <para>API: see <c>ScopaMaterialConfig.OnJobs()</c> and <c>ScopaMaterialConfig.OnMeshes()</c> to add your own code to modify meshes, per material.</para>
        /// <list> Jobs overview:
        /// <item> 1) VertJob fills vertStream with shared vertex data </item>
        /// <item> 2) VertCountJob counts and finalizes shared vertex data </item>
        /// <item> 3) (optional) OcclusionJob culls hidden faces </item>
        /// <item> 4) MeshCountJob counts vertices and tri indices per mesh </item>
        /// <item> 5) MeshJob uses MeshCountJob results to fill MeshDataArray with verts, normals, and triangles, etc</item>
        /// </list></summary>
        public class ScopaMeshJobGroup {
            public ScopaMapConfig config;
            public ScopaEntityData entity;
            public NativeArray<int> planeOffsets;
            public NativeArray<double4> planes;

            /// <summary> Temporary container for all of the entity's vertices, populated by VertJob. 
            /// Each buffer corresponds to a plane / face. 
            /// Don't use this after CountJob is done. </summary>
            public NativeStream vertStream;
            /// <summary> Contains all of the entity Populated by CountJob, based on vertStream data. </summary>
            public NativeArray<float3> faceVerts;

            public NativeArray<ScopaFaceData> faceData;
            public NativeArray<ScopaFaceMeshData> faceMeshData;
            public NativeArray<ScopaFaceUVData> faceUVData;
            public Mesh.MeshDataArray meshDataArray;

            /// <summary> Burst-compatible list of various metadata / counters, corresponds to textureNames list </summary>
            public NativeArray<ScopaMeshCounts> meshCounts;

            /// <summary> Managed list of all texture names, verbatim </summary>
            public List<string> textureNames = new();
            /// <summary> Burst-compatible list of various metadata / counters, corresponds to textureNames list </summary>
            public NativeList<ScopaTextureData> textureData;
            /// <summary> Lookup to convert a textureName (verbatim) to a Scopa MaterialOverride.
            /// Note that the order may not match textureNames list, since C# dictionaries are not indexed / ordered. </summary>
            public Dictionary<string, ScopaMapConfig.MaterialOverride> textureToMaterial = new();

            /// <summary> The finalized Unity mesh list. Will be NULL until Complete() is called. </summary>
            public List<ScopaMeshData> results;

            JobHandle finalJobHandle;

            #if SCOPA_MESH_DEBUG
            Dictionary<string, Stopwatch> timers = new();
            #endif

            void StartTimer(string timerLabel) {
                #if SCOPA_MESH_DEBUG
                var timer = new Stopwatch();
                timers.Add(timerLabel, timer);
                timer.Start();
                #endif
            }

            void StopTimer(string timerLabel) {
                #if SCOPA_MESH_DEBUG
                if (!timers.ContainsKey(timerLabel))
                    Debug.LogError($"no timer called {timerLabel}");
                timers[timerLabel].Stop();
                #endif
            }

            public ScopaMeshJobGroup(ScopaMapConfig config, ScopaEntityData entity, Solid[] solids) {
                this.config = config;
                this.entity = entity;

                StartTimer("Total");
                StartTimer("Planes");
                // this can never be Bursted because Solids are managed
                var planeCount = 0;
                planeOffsets = new NativeArray<int>(solids.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < solids.Length; i++) {
                    planeOffsets[i] = planeCount;
                    planeCount += solids[i].Faces.Count;
                }

                planes = new(planeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < solids.Length; i++) {
                    // TODO: error checking, make sure solid has at least 4 faces?
                    for (int p = 0; p < solids[i].Faces.Count; p++) {
                        var quakeFace = solids[i].Faces[p];
                        planes[planeOffsets[i] + p] = new double4(
                            quakeFace.Plane.Normal.X,
                            quakeFace.Plane.Normal.Y,
                            quakeFace.Plane.Normal.Z,
                            -quakeFace.Plane.D
                        );
                    }
                }
                vertStream = new(planeCount, Allocator.TempJob);
                StopTimer("Planes");

                // VERT JOB - intersect the planes to generate vertices
                StartTimer("Verts");
                var vertJob = new VertJob {
                    planeOffsets = planeOffsets,
                    facePlanes = planes,
                    vertStream = vertStream.AsWriter()
                };
                vertJob.Schedule(solids.Length, 64).Complete();
                StopTimer("Verts");

                // VERT COUNT JOB - count vertices and make the buffer
                StartTimer("VertCount");
                faceData = new(planeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                faceVerts = new(vertStream.Count(), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                var vertCountJob = new VertCountJob {
                    faceData = faceData,
                    planes = planes,
                    faceVerts = faceVerts,
                    vertStream = vertStream,
                };
                var vertCountJobHandle = vertCountJob.Schedule();

                // while vert count happens, start making mesh groups
                faceMeshData = new(planeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                faceUVData = new(planeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                textureData = new(128, Allocator.TempJob);
                for (int i = 0; i < solids.Length; i++) {
                    for (int p = 0; p < solids[i].Faces.Count; p++) {
                        // detect materials and allocate
                        var quakeFace = solids[i].Faces[p];
                        var textureName = quakeFace.TextureName;
                        var pIndex = planeOffsets[i] + p;

                        if (textureNames.Contains(textureName)) {
                            var index = textureNames.IndexOf(textureName);
                            faceMeshData[pIndex] = new(index, textureData[index].textureIndex < 0);
                        } else {
                            // TODO: detect if materials are actually the same
                            var matOverride = config.GetMaterialOverrideFor(textureName);
                            if (matOverride == null)
                                matOverride = new(textureName, config.GetDefaultMaterial());
                            textureToMaterial.Add(textureName, matOverride);

                            bool isCulled = config.IsTextureNameCulled(textureName.ToLowerInvariant());
                            var index = textureNames.Count;
                            faceMeshData[pIndex] = new(index, isCulled);

                            textureNames.Add(textureName);
                            textureData.Add(matOverride.material != null && matOverride.material.mainTexture != null ?
                                new(matOverride.material.mainTexture.width, matOverride.material.mainTexture.height, index, isCulled) :
                                new(config.defaultTexSize, config.defaultTexSize, index, isCulled)
                            );
                        }

                        faceUVData[pIndex] = new(
                            new float4((float3)quakeFace.UAxis.ToUnity() / quakeFace.XScale, quakeFace.XShift),
                            new float4((float3)quakeFace.VAxis.ToUnity() / quakeFace.YScale, quakeFace.YShift)
                        );
                    }
                }

                vertCountJobHandle.Complete();
                StopTimer("VertCount");
                
                if (config.removeHiddenFaces) {
                    StartTimer("Occlusion");
                    var planeLookup = new NativeParallelMultiHashMap<int4, int>(planeCount, Allocator.TempJob);
                    var planeLookupJob = new PlaneLookupJob {
                        planes = planes,
                        faceMeshData = faceMeshData,
                        planeLookup = planeLookup
                    };
                    planeLookupJob.Run(planeCount);
                    
                    var occlusionJob = new OcclusionJob {
                        faceVerts = faceVerts,
                        planes = planes,
                        faceData = faceData,
                        faceMeshData = faceMeshData,
                        planeLookup = planeLookup
                    };
                    occlusionJob.Schedule(planeCount, 128).Complete();
                    planeLookup.Dispose();
                    StopTimer("Occlusion");
                }

                StartTimer("MeshCount");
                var faceMeshDataReadOnly = new NativeArray<ScopaFaceMeshData>(faceMeshData.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                faceMeshData.CopyTo(faceMeshDataReadOnly);
                meshCounts = new(textureData.Length, Allocator.TempJob);
                var meshCountJob = new MeshCountJob {
                    faceData = faceData,
                    faceMeshDataReadOnly = faceMeshDataReadOnly,
                    faceMeshData = faceMeshData,
                    meshCounts = meshCounts,
                    textureData = textureData.AsArray()
                };
                meshCountJob.Schedule(textureNames.Count, 4).Complete();
                faceMeshDataReadOnly.Dispose();
                StopTimer("MeshCount");

                StartTimer("MeshBuild");
                meshDataArray = Mesh.AllocateWritableMeshData(textureNames.Count);
                for (int i = 0; i < textureNames.Count; i++) {
                    var meshData = meshDataArray[i];
                    meshData.SetVertexBufferParams(meshCounts[i].vertCount,
                        new VertexAttributeDescriptor(VertexAttribute.Position),
                        new VertexAttributeDescriptor(VertexAttribute.Normal, stream: 1),
                        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, dimension: 2, stream: 2)
                    );
                    meshData.SetIndexBufferParams(meshCounts[i].triIndexCount, IndexFormat.UInt32);
                }

                var meshJob = new MeshJob {
                    planes = planes,
                    faceData = faceData,
                    faceUVData = faceUVData,
                    faceMeshData = faceMeshData,
                    faceVertices = faceVerts,
                    meshOrigin = float3.zero, // TODO: get entity origin
                    meshCounts = meshCounts,
                    meshDataArray = meshDataArray,
                    textureData = textureData.AsArray(),
                    scalingConfig = new float2(config.globalTexelScale, config.scalingFactor)
                };
                finalJobHandle = meshJob.Schedule(planeCount, 128);

                // TODO: smooth normals job
            }

            /// <summary> Per-brush job that intersects planes to generate vertices.
            /// The vertices go into a big per-face shared NativeStream buffer. </summary>
#if SCOPA_USE_BURST
            [BurstCompile(FloatMode = FloatMode.Fast)]
#endif
            public struct VertJob : IJobParallelFor {
                [ReadOnlyAttribute] public NativeArray<double4> facePlanes;
                [NativeDisableParallelForRestriction] public NativeStream.Writer vertStream;
                [ReadOnlyAttribute] public NativeArray<int> planeOffsets;

                public void Execute(int i) { // i = solid index
                    int planeCount = (i + 1 < planeOffsets.Length ? planeOffsets[i + 1] : facePlanes.Length) - planeOffsets[i];

                    for (var p = 0; p < planeCount; p++) {
                        // Generate a giant polygon for each plane, in double precision
                        var plane = facePlanes[planeOffsets[i] + p];
                        var polygon = new NativeArray<double3>(4, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                        var direction = GetClosestAxisToNormal(plane.xyz);
                        var tempV = direction.z >= 1 ? new double3(0, -1, 0) : new double3(0, 0, -1);
                        var up = math.normalize(math.cross(tempV, plane.xyz));
                        var right = math.normalize(math.cross(plane.xyz, up));

                        var planePoint = plane.xyz * -plane.w;
                        var radius = 1000000d;
                        polygon[0] = planePoint + (right + up) * radius; // Top right
                        polygon[1] = planePoint + (-right + up) * radius; // Top left
                        polygon[2] = planePoint + (-right - up) * radius; // Bottom left
                        polygon[3] = planePoint + (right - up) * radius; // Bottom right

                        // Split each giant polygon by all the other planes
                        for (var p2 = 0; p2 < planeCount; p2++) {
                            if (p != p2 && TrySplit(facePlanes[planeOffsets[i] + p2], polygon, out var newPolygon)) {
                                polygon.Dispose();
                                polygon = newPolygon;
                            }
                        }

                        vertStream.BeginForEachIndex(planeOffsets[i] + p);
                        for (int v = 0; v < polygon.Length; v++) {
                            vertStream.Write<float3>((float3)polygon[v]); // we write vertices back to single precision
                        }
                        vertStream.EndForEachIndex();
                        polygon.Dispose();
                    }
                    #if SCOPA_MESH_VERBOSE
                    Debug.Log($"VertJob finished job {i} with {planeCount} planes!");
                    #endif
                }

                static double3 GetClosestAxisToNormal(double3 normal) {
                    // VHE prioritises the axes in order of X, Y, Z.
                    var norm = math.abs(normal);
                    if (norm.x >= norm.y && norm.x >= norm.z) return new double3(1, 0, 0);
                    if (norm.y >= norm.z) return new double3(0, 1, 0);
                    return new double3(0, 0, 1);
                }

                /// <summary>Splits a polygon by a clipping plane, maybe modifying the original input polygon.</summary>
                static bool TrySplit(double4 clip, NativeArray<double3> polygon, out NativeArray<double3> newPolygon) {
                    // evaluate each vertex's distance from the clipping plane
                    var distances = new NativeArray<double>(polygon.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                    for (int i = 0; i < distances.Length; i++) {
                        distances[i] = clip.x * polygon[i].x + clip.y * polygon[i].y + clip.z * polygon[i].z + clip.w;
                    }

                    const double epsilon = 0.1d;
                    int cb = 0, cf = 0;
                    for (var i = 0; i < distances.Length; i++) {
                        if (distances[i] < -epsilon) cb++;
                        else if (distances[i] > epsilon) cf++;
                        else distances[i] = 0;
                    }

                    if (cb == 0 || cf == 0) {
                        distances.Dispose();
                        newPolygon = polygon;
                        return false;
                    }

                    // Get the new vertices from the "back"
                    var newPoly = new NativeList<double3>(polygon.Length * 2, Allocator.Temp);

                    for (var i = 0; i < polygon.Length; i++) {
                        var sd = distances[i];
                        if (sd <= 0)
                            newPoly.Add(polygon[i]);

                        var j = (i + 1) % polygon.Length;
                        var e = polygon[j];
                        var ed = distances[j];
                        if ((sd < 0 && ed > 0) || (ed < 0 && sd > 0)) {
                            var t = sd / (sd - ed);
                            var intersect = polygon[i] * (1 - t) + e * t;
                            newPoly.Add(intersect);
                        }
                    }

                    distances.Dispose();
                    newPolygon = newPoly.AsArray();
                    return true;
                }
            }

            /// <summary> Single threaded job that counts vertices per face.
            /// Cannot be multithreaded, we need the cumulative vertex offsets. </summary>
#if SCOPA_USE_BURST
            [BurstCompile(FloatMode = FloatMode.Fast)]
#endif
            public struct VertCountJob : IJob {
                public NativeStream vertStream;
                [ReadOnlyAttribute] public NativeArray<double4> planes;
                [WriteOnly] public NativeArray<ScopaFaceData> faceData;
                [WriteOnly] public NativeArray<float3> faceVerts;

                public void Execute() {
                    var vRead = vertStream.AsReader();
                    var vertCounter = 0;
                    for (int i = 0; i < planes.Length; i++) { 
                        var count = vRead.BeginForEachIndex(i); // each ForEachBuffer is a face
                        for (int v = 0; v < count; v++) {
                            faceVerts[vertCounter + v] = vRead.Read<float3>();
                        }
                        vRead.EndForEachIndex();

                        faceData[i] = new(vertCounter, count);
                        vertCounter += count;
                    }
                }
            }

            const double LOOKUP_NORMAL_TOLERANCE = 0.01d;
            static int4 GetPlaneLookupKey(double4 plane) {
                var roundedNormal = math.round(plane.xyz);
                if (math.lengthsq(roundedNormal-plane.xyz) > LOOKUP_NORMAL_TOLERANCE * LOOKUP_NORMAL_TOLERANCE)
                    return ((int4)plane)*100;
                else
                    return new int4((int)roundedNormal.x, (int)roundedNormal.y, (int)roundedNormal.z, (int)plane.w)*100;
            }

            /// <summary> Generates a plane lookup to accelerate OcclusionJob. </summary>
#if SCOPA_USE_BURST
            [BurstCompile(FloatMode = FloatMode.Fast)]
#endif
            public struct PlaneLookupJob : IJobFor {
                [ReadOnlyAttribute] public NativeArray<double4> planes;
                [ReadOnlyAttribute] public NativeArray<ScopaFaceMeshData> faceMeshData;
                [WriteOnly] public NativeParallelMultiHashMap<int4, int> planeLookup;

                public void Execute(int i) {
                    if (faceMeshData[i].materialIndex >= 0)
                        planeLookup.Add(GetPlaneLookupKey(planes[i]), i);
                }
            }

            /// <summary> Per-face job to cull each face covered by another face (in same entity) </summary>
            #if SCOPA_USE_BURST
            [BurstCompile(FloatMode = FloatMode.Fast)]
            #endif
            public struct OcclusionJob : IJobParallelFor {
                [ReadOnlyAttribute] public NativeArray<float3> faceVerts;
                [ReadOnlyAttribute] public NativeArray<double4> planes;
                [ReadOnlyAttribute] public NativeArray<ScopaFaceData> faceData;
                [NativeDisableParallelForRestriction] public NativeArray<ScopaFaceMeshData> faceMeshData;
                [ReadOnlyAttribute] public NativeParallelMultiHashMap<int4, int> planeLookup;

                public void Execute(int i) { // i = face index
                    if (faceMeshData[i].materialIndex < 0)
                        return;

                    var planeLookupKey = GetPlaneLookupKey(planes[i])*-1;
                    if (!planeLookup.ContainsKey(planeLookupKey))
                        return;

                    var face = faceData[i];
                    var offsetStart = face.vertIndexStart;
                    var offsetEnd = offsetStart + face.vertCount;
                    var faceCenter = faceVerts[offsetStart];
                    for( int b=offsetStart+1; b<offsetEnd; b++) {
                        faceCenter += faceVerts[b];
                    }
                    faceCenter /= face.vertCount;
                    var ignoreAxis = GetIgnoreAxis(math.abs(planes[i]));

                    var planeLookupEnumerator = planeLookup.GetValuesForKey(planeLookupKey);
                    
                    foreach(var n in planeLookupEnumerator) {
                    // for(int n=0; n<planes.Length; n++) { // test face (i) against all other faces (n) 
                    //     // early out test: if any of these are true, then occlusion is impossible...
                    //     // (1) if other face is culled OR (2) if they're far apart OR (3) not facing exactly opposite directions
                    //     if ( faceMeshData[n].materialIndex < 0 || math.abs(planes[i].w + planes[n].w) > 0.1d || math.dot(planes[i].xyz, planes[n].xyz) > -0.99d )
                    //         continue;

                        // get occluding face ("polygon")
                        var otherPolygon = new NativeArray<float3>(faceData[n].vertCount, Allocator.Temp);
                        NativeArray<float3>.Copy(faceVerts, faceData[n].vertIndexStart, otherPolygon, 0, faceData[n].vertCount);
                        
                        var foundOutsideVert = false;
                        for( int x=offsetStart; x<offsetEnd && !foundOutsideVert; x++ ) {
                            var point = faceVerts[x] + math.normalize(faceCenter - faceVerts[x]) * 0.2f; // shrink face point slightly
                            switch (ignoreAxis) { // 2D math is easier, so let's ignore the least important axis
                                case 0: if (!IsInPolygonYZ(point, otherPolygon)) foundOutsideVert = true; break;
                                case 1: if (!IsInPolygonXZ(point, otherPolygon)) foundOutsideVert = true; break;
                                case 2: if (!IsInPolygonXY(point, otherPolygon)) foundOutsideVert = true; break;
                            }
                        }
                        otherPolygon.Dispose();

                        // if no verts outside polygon, then it's occluded... cull it!
                        if (!foundOutsideVert) {
                            faceMeshData[i] = new int3(0, 0, -1);
                            return;
                        }
                    }
                }

                [return: AssumeRange(0, 2)]
                static int GetIgnoreAxis(double4 norm) {
                    // VHE prioritises the axes in order of X, Y, Z.
                    if (norm.x >= norm.y && norm.x >= norm.z) return 0;
                    if (norm.y >= norm.z) return 1;
                    return 2;
                }

                // https://wrf.ecse.rpi.edu/Research/Short_Notes/pnpoly.html
                static bool IsInPolygonXY(float3 p, NativeArray<float3> polygon) {
                    bool inside = false;
                    for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++) {
                        if ((polygon[i].y > p.y) != (polygon[j].y > p.y) &&
                            p.x < (polygon[j].x - polygon[i].x) * (p.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x) {
                            inside = !inside;
                        }
                    }
                    return inside;
                }

                static bool IsInPolygonYZ(float3 p, NativeArray<float3> polygon) {
                    bool inside = false;
                    for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++) {
                        if ((polygon[i].y > p.y) != (polygon[j].y > p.y) &&
                            p.z < (polygon[j].z - polygon[i].z) * (p.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].z) {
                            inside = !inside;
                        }
                    }
                    return inside;
                }

                static bool IsInPolygonXZ(float3 p, NativeArray<float3> polygon) {
                    bool inside = false;
                    for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++) {
                        if ((polygon[i].x > p.x) != (polygon[j].x > p.x) &&
                            p.z < (polygon[j].z - polygon[i].z) * (p.x - polygon[i].x) / (polygon[j].x - polygon[i].x) + polygon[i].z) {
                            inside = !inside;
                        }
                    }
                    return inside;
                }
            }


#if SCOPA_USE_BURST
            [BurstCompile(FloatMode = FloatMode.Fast)]
#endif
            public struct MeshCountJob : IJobParallelFor {
                [ReadOnlyAttribute] public NativeArray<ScopaFaceData> faceData;
                [NativeDisableParallelForRestriction, ReadOnlyAttribute] public NativeArray<ScopaFaceMeshData> faceMeshDataReadOnly;
                [NativeDisableParallelForRestriction, WriteOnly] public NativeArray<ScopaFaceMeshData> faceMeshData;
                [WriteOnly] public NativeArray<ScopaMeshCounts> meshCounts;
                [ReadOnlyAttribute] public NativeArray<ScopaTextureData> textureData;

                public void Execute(int i) { // i = per mesh
                    if (textureData[i].textureIndex < 0) // skip culled meshes
                        return;

                    int vertCount = 0, triCount = 0;
                    for(int face=0; face<faceData.Length; face++) {
                        // ignore already culled faces, ignore any face not in this mesh
                        if (faceMeshDataReadOnly[face].materialIndex != i) 
                            continue;

                        faceMeshData[face] = new(vertCount, triCount, i);
                        vertCount += faceData[face].vertCount;
                        triCount += (faceData[face].vertCount - 2) * 3;
                    }
                    meshCounts[i] = new(vertCount, triCount);
                    // Debug.Log($"MeshCount {i} counted {vertCount}/{triCount}");
                }
            }

#if SCOPA_USE_BURST
            [BurstCompile(FloatMode = FloatMode.Fast)]
#endif
            public struct MeshJob : IJobParallelFor {
                [ReadOnlyAttribute] public NativeArray<double4> planes;
                [ReadOnlyAttribute] public NativeArray<ScopaFaceData> faceData;
                [ReadOnlyAttribute] public NativeArray<ScopaFaceUVData> faceUVData;
                [ReadOnlyAttribute] public NativeArray<ScopaFaceMeshData> faceMeshData;
                [ReadOnlyAttribute] public NativeArray<float3> faceVertices;

                [ReadOnlyAttribute] public float3 meshOrigin;
                [ReadOnlyAttribute] public NativeArray<ScopaMeshCounts> meshCounts;
                [ReadOnlyAttribute] public NativeArray<ScopaTextureData> textureData;

                [NativeDisableParallelForRestriction] public Mesh.MeshDataArray meshDataArray;

                /// <summary> x = globalTextureScale, y = scalingFactor</summary>
                [ReadOnlyAttribute] public float2 scalingConfig;

                // TODO: burstify the rotation needed for Quake Standard format
                public static float2 Rotate(float2 v, float deltaRadians) {
                    return new float2(
                        v.x * math.cos(deltaRadians) - v.y * math.sin(deltaRadians),
                        v.x * math.sin(deltaRadians) + v.y * math.cos(deltaRadians)
                    );
                }

                public void Execute(int i) { // i = face index
                    if (faceMeshData[i].materialIndex < 0) 
                        return;

                    var face = faceData[i];
                    var faceMesh = faceMeshData[i];

                    var meshData = meshDataArray[faceMesh.materialIndex];
                    var outputVerts = meshData.GetVertexData<float3>();
                    var outputNorms = meshData.GetVertexData<float3>(1);
                    var outputUVs = meshData.GetVertexData<float2>(2);
                    var outputTris = meshData.GetIndexData<int>();

                    // add all verts, normals, and UVs
                    // Debug.Log($"MeshJob {i} - vertStart {face.vertIndexStart} vertCount {face.vertCount}");
                    for (int x = 0; x < face.vertCount; x++) {
                        var mdi = faceMesh.meshVertStart + x; // mesh data index
                        var n = face.vertIndexStart + x; // global vert buffer index

                        // all Face Datas are still in Quake space; need to convert to Unity space and axes
                        outputVerts[mdi] = faceVertices[n].xzy * scalingConfig.y - meshOrigin;
                        outputNorms[mdi] = (float3)planes[i].xzy;

                        var uv = faceUVData[i];
                        outputUVs[mdi] = new float2( // NOTE: UV axes are already scaled
                            (math.dot(faceVertices[n].xzy, uv.faceU.xzy) + uv.faceU.w) / textureData[faceMesh.materialIndex].textureWidth,
                            (math.dot(faceVertices[n].xzy, -uv.faceV.xzy) - uv.faceV.w) / textureData[faceMesh.materialIndex].textureHeight
                        ) * scalingConfig.x;
                    }

                    // TODO: snapping pass / normals / welding on vertices

                    // verts are already in correct order, add as basic fan pattern (since we know it's a convex face)
                    for (int t = 2; t < face.vertCount; t++) {
                        outputTris[faceMesh.meshTriStart + (t - 2) * 3] = faceMesh.meshVertStart;
                        outputTris[faceMesh.meshTriStart + (t - 2) * 3 + 1] = faceMesh.meshVertStart + t - 1;
                        outputTris[faceMesh.meshTriStart + (t - 2) * 3 + 2] = faceMesh.meshVertStart + t;
                    }
                }
            }

            public List<ScopaMeshData> CompleteJobsAndGetMeshes() {
                var meshes = new Mesh[textureNames.Count];
                for (int i = 0; i < meshes.Length; i++) {
                    var newMesh = new Mesh {
                        name = $"{textureNames[i]}"
                        // name = string.Format(colliderNameFormat, i.ToString("D5", System.Globalization.CultureInfo.InvariantCulture))
                    };
                    meshes[i] = newMesh;
                }
                finalJobHandle.Complete();
                StopTimer("MeshBuild");

                // TODO: call JobsDone()
                StartTimer("MeshWrite");
                for (int i = 0; i < meshes.Length; i++) {
                    var meshData = meshDataArray[i];
                    meshData.subMeshCount = 1;
                    meshData.SetSubMesh(0, new SubMeshDescriptor(0, meshCounts[i].triIndexCount), MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
                }

                // finalizing mesh!
                Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, meshes);
                results = new List<ScopaMeshData>(meshes.Length);
                for (int i = 0; i < meshes.Length; i++) {
                    var newMesh = meshes[i];
                    if (config.addTangents)
                        newMesh.RecalculateTangents();
                    newMesh.RecalculateBounds();

                    // If optimize everything, just combine the two optimizations into one call
                    if ((config.optimizeMesh & ScopaMapConfig.ModelImporterMeshOptimization.OptimizeIndexBuffers) != 0 &&
                        (config.optimizeMesh & ScopaMapConfig.ModelImporterMeshOptimization.OptimizeVertexBuffers) != 0) {
                        newMesh.Optimize();
                    } else {
                        if ((config.optimizeMesh & ScopaMapConfig.ModelImporterMeshOptimization.OptimizeIndexBuffers) != 0)
                            newMesh.OptimizeIndexBuffers();
                        if ((config.optimizeMesh & ScopaMapConfig.ModelImporterMeshOptimization.OptimizeVertexBuffers) != 0)
                            newMesh.OptimizeReorderVertexBuffer();
                    }
                    // Debug.Log($"writing {newMesh.name} with {newMesh.vertexCount}");
                    results.Add(new(newMesh, null, textureToMaterial[textureNames[i]], null));
                }
                StopTimer("MeshWrite");
                StopTimer("Total");

                // TODO: call OnMeshesDone()

                #if SCOPA_MESH_DEBUG
                string timerLog = $"ScopaMeshJobGroup finished {meshes.Length} meshes for {entity.ClassName}";
                foreach(var timerKVP in timers) {
                    timerLog += $"\n{timerKVP.Key} {timerKVP.Value.ElapsedMilliseconds} ms";
                }
                Debug.Log(timerLog);
                #endif

                // TODO: don't dispose these, pass them into collider jobs?
                Dispose();
                return results;
            }

            public void Dispose() {
                planeOffsets.Dispose();
                planes.Dispose();
                vertStream.Dispose();
                faceVerts.Dispose();
                faceData.Dispose();
                faceMeshData.Dispose();
                faceUVData.Dispose();
                textureData.Dispose();
                meshCounts.Dispose();
            }
        }

               /// <summary> Burst-compatible data struct for a face's vertex buffer / tri index offsets </summary>
        public struct ScopaFaceData {
            /// <summary> will be 0 until VertCountJob fills it</summary>
            public int vertIndexStart, vertCount;

            public ScopaFaceData(int globalVertStart, int vertCount) : this() {
                this.vertIndexStart = globalVertStart;
                this.vertCount = vertCount;
            }

            public static implicit operator int2(ScopaFaceData d) => new(d.vertIndexStart, d.vertCount);
            public static implicit operator ScopaFaceData(int2 i) => new(i.x, i.y);

            public override string ToString() {
                return $"VertStart {vertIndexStart}, VertCount {vertCount}";
            }
        }

        public struct ScopaFaceMeshData {
            /// <summary> will be 0 until VertCountJob fills it</summary>
            public int meshVertStart, meshTriStart;

            /// <summary> Corresponds to textureName list index / meshDataArray index. 
            /// If negative, this face has been discarded / should be culled. 
            /// Don't delete the data because it'll be needed for collision meshes! 
            /// Use math.abs() to recover original textureName / meshDataArray index / etc </summary>
            public int materialIndex;

            public ScopaFaceMeshData(int materialIndex, bool isCulled = false) : this() {
                this.meshVertStart = 0;
                this.meshTriStart = 0;
                this.materialIndex = (isCulled ? -1 : 1) * materialIndex;
            }

            public ScopaFaceMeshData(int meshVertStart, int meshTriStart, int materialIndex) : this() {
                this.meshVertStart = meshVertStart;
                this.meshTriStart = meshTriStart;
                this.materialIndex = materialIndex;
            }

            public static implicit operator int3(ScopaFaceMeshData d) => new(d.meshVertStart, d.meshTriStart, d.materialIndex);
            public static implicit operator ScopaFaceMeshData(int3 i) => new(i.x, i.y, i.z);

            public override string ToString() {
                return $"MVertStart {meshVertStart}, MTriStart {meshTriStart}, material {materialIndex}";
            }
        }

        /// <summary> Burst-compatible data struct for generating UVs on a face </summary>
        public struct ScopaFaceUVData {
            /// <summary> .xyz = scaled texture axis in Quake space, .w = shift in Quake space </summary>
            public float4 faceU, faceV;

            public ScopaFaceUVData(float4 u, float4 v) : this() {
                this.faceU = u;
                this.faceV = v;
            }

            public static implicit operator float4x2(ScopaFaceUVData d) => new(d.faceU, d.faceV);
            public static implicit operator ScopaFaceUVData(float4x2 i) => new(i.c0, i.c1);
        }

        /// <summary> Burst-compatible data struct for a texture / material group / mesh </summary>
        public struct ScopaTextureData {
            public int textureWidth, textureHeight, textureIndex;

            public ScopaTextureData(int textureWidth, int textureHeight, int textureIndex, bool isCulled = false) {
                this.textureWidth = textureWidth;
                this.textureHeight = textureHeight;
                this.textureIndex = (isCulled ? -1 : 1) * textureIndex;
            }

            public static implicit operator int3(ScopaTextureData d) => new(d.textureWidth, d.textureHeight, d.textureIndex);
            public static implicit operator ScopaTextureData(int3 i) => new(i.x, i.y, i.z);

            public override string ToString() {
                return $"{textureWidth}x{textureHeight} material {textureIndex}";
            }
        }

        /// <summary> Burst-compatible data struct for a mesh </summary>
        public struct ScopaMeshCounts {
            public int vertCount, triIndexCount;
            public ScopaMeshCounts(int vertCount, int triIndexCount) : this() {
                this.vertCount = vertCount;
                this.triIndexCount = triIndexCount;
            }

            public static implicit operator int2(ScopaMeshCounts d) => new(d.vertCount, d.triIndexCount);
            public static implicit operator ScopaMeshCounts(int2 i) => new(i.x, i.y);
            public override string ToString() {
                return $"{vertCount} verts, {triIndexCount} triIndices";
            }
        }


        // // to avoid GC, we use big static lists so we just allocate once
        // // TODO: will have to reorganize this for multithreading later on
        // static List<Face> allFaces = new List<Face>(8192);
        // static HashSet<Face> discardedFaces = new HashSet<Face>(8192);

        // public static void AddFaceForCulling(Face brushFace) {
        //     allFaces.Add(brushFace);
        // }

        // public static void ClearFaceCullingList() {
        //     allFaces.Clear();
        //     discardedFaces.Clear();
        // }

        // public static void DiscardFace(Face brushFace) {
        //     discardedFaces.Add(brushFace);
        // }

        // public static bool IsFaceCulledDiscard(Face brushFace) {
        //     return discardedFaces.Contains(brushFace);
        // }

        // public static FaceCullingJobGroup StartFaceCullingJobs() {
        //     return new FaceCullingJobGroup();
        // }

        // public class FaceCullingJobGroup {
        //     NativeArray<int> cullingOffsets;
        //     NativeArray<Vector4> cullingPlanes;
        //     NativeArray<Vector3> cullingVerts;
        //     NativeArray<bool> cullingResults;
        //     JobHandle jobHandle;

        //     public FaceCullingJobGroup() {
        //         var vertCount = 0;
        //         cullingOffsets = new NativeArray<int>(allFaces.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        //         cullingPlanes = new NativeArray<Vector4>(allFaces.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        //         for(int i=0; i<allFaces.Count; i++) {
        //             cullingOffsets[i] = vertCount;
        //             vertCount += allFaces[i].Vertices.Count;
        //             cullingPlanes[i] = new Vector4(allFaces[i].Plane.Normal.X, allFaces[i].Plane.Normal.Y, allFaces[i].Plane.Normal.Z, allFaces[i].Plane.D);
        //         }

        //         cullingVerts = new NativeArray<Vector3>(vertCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        //         for(int i=0; i<allFaces.Count; i++) {
        //             for(int v=cullingOffsets[i]; v < (i<cullingOffsets.Length-1 ? cullingOffsets[i+1] : vertCount); v++) {
        //                 cullingVerts[v] = allFaces[i].Vertices[v-cullingOffsets[i]].ToUnity();
        //             }
        //         }

        //         cullingResults = new NativeArray<bool>(allFaces.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        //         for(int i=0; i<allFaces.Count; i++) {
        //             cullingResults[i] = IsFaceCulledDiscard(allFaces[i]);
        //         }

        //         var jobData = new FaceCullingJob {
        //             faceVertices = cullingVerts.Reinterpret<float3>(),
        //             facePlanes = cullingPlanes.Reinterpret<float4>(),
        //             faceVertexOffsets = cullingOffsets,
        //             cullFaceResults = cullingResults
        //         };
        //         jobHandle = jobData.Schedule(cullingResults.Length, 64);
        //     }

        //     public void Complete() {
        //         jobHandle.Complete();

        //         for(int i=0; i<cullingResults.Length; i++) {
        //             if(cullingResults[i])
        //                 discardedFaces.Add(allFaces[i]);
        //         }

        //         cullingOffsets.Dispose();
        //         cullingVerts.Dispose();
        //         cullingPlanes.Dispose();
        //         cullingResults.Dispose();
        //     }

        // }

        // #if SCOPA_USE_BURST
        // [BurstCompile]
        // #endif
        // public struct FaceCullingJob : IJobParallelFor
        // {

        //     #if SCOPA_USE_BURST
        //     [ReadOnlyAttribute] public NativeArray<float3> faceVertices;
        //     [ReadOnlyAttribute] public NativeArray<float4> facePlanes;
        //     #else
        //     [ReadOnlyAttribute] public NativeArray<Vector3> faceVertices;
        //     [ReadOnlyAttribute] public NativeArray<Vector4> facePlanes;
        //     #endif

        //     [ReadOnlyAttribute] public NativeArray<int> faceVertexOffsets;
        //     [NativeDisableParallelForRestriction] public NativeArray<bool> cullFaceResults;

        //     public void Execute(int i)
        //     {
        //         if (cullFaceResults[i])
        //             return;

        //         // test against all other faces
        //         for(int n=0; n<faceVertexOffsets.Length; n++) {
        //             // first, test (1) share similar plane distance and (2) face opposite directions
        //             // we are testing the NEGATIVE case for early out
        //             if ( math.abs(facePlanes[i].w + facePlanes[n].w) > 0.5f || math.dot(facePlanes[i].xyz, facePlanes[n].xyz) > -0.999f )
        //                 continue;

        //             // then, test whether this face's vertices are completely inside the other
        //             var offsetStart = faceVertexOffsets[i];
        //             var offsetEnd = i<faceVertexOffsets.Length-1 ? faceVertexOffsets[i+1] : faceVertices.Length;

        //             var Center = faceVertices[offsetStart];
        //             for( int b=offsetStart+1; b<offsetEnd; b++) {
        //                 Center += faceVertices[b];
        //             }
        //             Center /= offsetEnd-offsetStart;

        //             // 2D math is easier, so let's ignore the least important axis
        //             var ignoreAxis = GetMainAxisToNormal(facePlanes[i]);

        //             var otherOffsetStart = faceVertexOffsets[n];
        //             var otherOffsetEnd = n<faceVertexOffsets.Length-1 ? faceVertexOffsets[n+1] : faceVertices.Length;

        //             var polygon = new NativeArray<float3>(otherOffsetEnd-otherOffsetStart, Allocator.Temp);
        //             NativeArray<float3>.Copy(faceVertices, otherOffsetStart, polygon, 0, polygon.Length);

        //             var vertNotInOtherFace = false;
        //             for( int x=offsetStart; x<offsetEnd; x++ ) {
        //                 var p = faceVertices[x] + math.normalize(Center - faceVertices[x]) * 0.2f;
        //                 switch (ignoreAxis) {
        //                     case Axis.X: if (!IsInPolygonYZ(p, polygon)) vertNotInOtherFace = true; break;
        //                     case Axis.Y: if (!IsInPolygonXZ(p, polygon)) vertNotInOtherFace = true; break;
        //                     case Axis.Z: if (!IsInPolygonXY(p, polygon)) vertNotInOtherFace = true; break;
        //                 }

        //                 if (vertNotInOtherFace)
        //                     break;
        //             }

        //             polygon.Dispose();

        //             if (vertNotInOtherFace)
        //                 continue;

        //             // if we got this far, then this face should be culled
        //             var tempResult = true;
        //             cullFaceResults[i] = tempResult;
        //             return;
        //         }

        //     }
        // }

        // public enum Axis { X, Y, Z}

        // public static Axis GetMainAxisToNormal(float4 vec) {
        //     // VHE prioritises the axes in order of X, Y, Z.
        //     // so in Unity land, that's X, Z, and Y
        //     var norm = new float3(
        //         math.abs(vec.x), 
        //         math.abs(vec.y),
        //         math.abs(vec.z)
        //     );

        //     if (norm.x >= norm.y && norm.x >= norm.z) return Axis.X;
        //     if (norm.z >= norm.y) return Axis.Z;
        //     return Axis.Y;
        // }

        // public static bool IsInPolygonXY(float3 p, NativeArray<float3> polygon ) {
        //     // https://wrf.ecse.rpi.edu/Research/Short_Notes/pnpoly.html
        //     bool inside = false;
        //     for ( int i = 0, j = polygon.Length - 1 ; i < polygon.Length ; j = i++ ) {
        //         if ( ( polygon[i].y > p.y ) != ( polygon[j].y > p.y ) &&
        //             p.x < ( polygon[j].x - polygon[i].x ) * ( p.y - polygon[i].y ) / ( polygon[j].y - polygon[i].y ) + polygon[i].x )
        //         {
        //             inside = !inside;
        //         }
        //     }

        //     return inside;
        // }

        // public static bool IsInPolygonYZ(float3 p, NativeArray<float3> polygon ) {
        //     // https://wrf.ecse.rpi.edu/Research/Short_Notes/pnpoly.html
        //     bool inside = false;
        //     for ( int i = 0, j = polygon.Length - 1 ; i < polygon.Length ; j = i++ ) {
        //         if ( ( polygon[i].y > p.y ) != ( polygon[j].y > p.y ) &&
        //             p.z < ( polygon[j].z - polygon[i].z ) * ( p.y - polygon[i].y ) / ( polygon[j].y - polygon[i].y ) + polygon[i].z )
        //         {
        //             inside = !inside;
        //         }
        //     }

        //     return inside;
        // }

        // public static bool IsInPolygonXZ(float3 p, NativeArray<float3> polygon ) {
        //     // https://wrf.ecse.rpi.edu/Research/Short_Notes/pnpoly.html
        //     bool inside = false;
        //     for ( int i = 0, j = polygon.Length - 1 ; i < polygon.Length ; j = i++ ) {
        //         if ( ( polygon[i].x > p.x ) != ( polygon[j].x > p.x ) &&
        //             p.z < ( polygon[j].z - polygon[i].z ) * ( p.x - polygon[i].x ) / ( polygon[j].x - polygon[i].x ) + polygon[i].z )
        //         {
        //             inside = !inside;
        //         }
        //     }

        //     return inside;
        // }

        // public class MeshBuildingJobGroup {

        //     NativeArray<int> faceVertexOffsets, faceTriIndexCounts; // index = i
        //     NativeArray<Vector3> faceVertices;
        //     NativeArray<Vector4> faceU, faceV; // index = i, .w = scale
        //     NativeArray<Vector2> faceShift, faceUVoverride; // index = i
        //     int vertCount, triIndexCount;

        //     public Mesh.MeshDataArray outputMesh;
        //     Mesh newMesh;
        //     JobHandle jobHandle;
        //     ScopaMapConfig config;

        //     public MeshBuildingJobGroup(string meshName, Vector3 meshOrigin, IEnumerable<Solid> solids, ScopaMapConfig config, ScopaMapConfig.MaterialOverride materialOverride = null, bool includeDiscardedFaces = false) {       
        //         this.config = config;
        //         var faceList = new List<Face>();
        //         foreach( var solid in solids) {
        //             foreach(var face in solid.Faces) {
        //                 if ( !includeDiscardedFaces && IsFaceCulledDiscard(face) )
        //                     continue;

        //                 // face.TextureName doesn't need to be ToLowerInvariant'd anymore, ScopaCore already did it earlier
        //                 if ( materialOverride != null && materialOverride.GetTextureName().GetHashCode() != face.TextureName.GetHashCode() )
        //                     continue;

        //                 faceList.Add(face);
        //             }
        //         }

        //         faceVertexOffsets = new NativeArray<int>(faceList.Count+1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        //         faceTriIndexCounts = new NativeArray<int>(faceList.Count+1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        //         for(int i=0; i<faceList.Count; i++) {
        //             faceVertexOffsets[i] = vertCount;
        //             vertCount += faceList[i].Vertices.Count;
        //             faceTriIndexCounts[i] = triIndexCount;
        //             triIndexCount += (faceList[i].Vertices.Count-2)*3;
        //         }
        //         faceVertexOffsets[faceList.Count] = vertCount;
        //         faceTriIndexCounts[faceList.Count] = triIndexCount;

        //         faceVertices = new NativeArray<Vector3>(vertCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        //         faceU = new NativeArray<Vector4>(faceList.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        //         faceV = new NativeArray<Vector4>(faceList.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        //         faceShift = new NativeArray<Vector2>(faceList.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        //         faceUVoverride = new NativeArray<Vector2>(vertCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        //         for(int i=0; i<faceList.Count; i++) {
        //             if (materialOverride != null 
        //                 && materialOverride.materialConfig != null
        //                 && materialOverride.materialConfig.useOnBuildBrushFace 
        //                 && materialOverride.materialConfig.OnBuildBrushFace(faceList[i], config, out var overrideUVs)
        //             ) {
        //                 for(int u=0; u<overrideUVs.Length; u++) {
        //                     faceUVoverride[faceVertexOffsets[i]+u] = overrideUVs[u];
        //                 }
        //             } else {
        //                 for(int u=faceVertexOffsets[i]; u<faceVertexOffsets[i+1]; u++) { // fill with dummy values to ignore in the Job later
        //                     faceUVoverride[u] = new Vector2(MeshBuildingJob.IGNORE_UV, MeshBuildingJob.IGNORE_UV);
        //                 }
        //             }

        //             for(int v=faceVertexOffsets[i]; v < faceVertexOffsets[i+1]; v++) {
        //                 faceVertices[v] = faceList[i].Vertices[v-faceVertexOffsets[i]].ToUnity();
        //             }
        //             faceU[i] = new Vector4(faceList[i].UAxis.X, faceList[i].UAxis.Y, faceList[i].UAxis.Z, faceList[i].XScale);
        //             faceV[i] = new Vector4(faceList[i].VAxis.X, faceList[i].VAxis.Y, faceList[i].VAxis.Z, faceList[i].YScale);
        //             faceShift[i] = new Vector2(faceList[i].XShift, faceList[i].YShift);
        //         }

        //         outputMesh = Mesh.AllocateWritableMeshData(1);
        //         var meshData = outputMesh[0];
        //         meshData.SetVertexBufferParams(vertCount,
        //             new VertexAttributeDescriptor(VertexAttribute.Position),
        //             new VertexAttributeDescriptor(VertexAttribute.Normal, stream:1),
        //             new VertexAttributeDescriptor(VertexAttribute.TexCoord0, dimension:2, stream:2)
        //         );
        //         meshData.SetIndexBufferParams(triIndexCount, IndexFormat.UInt32);

        //         var jobData = new MeshBuildingJob {
        //             faceVertexOffsets = faceVertexOffsets,
        //             faceTriIndexCounts = faceTriIndexCounts,

        //             faceVertices = faceVertices.Reinterpret<float3>(),
        //             faceU = faceU.Reinterpret<float4>(),
        //             faceV = faceV.Reinterpret<float4>(),
        //             faceShift = faceShift.Reinterpret<float2>(),
        //             uvOverride = faceUVoverride.Reinterpret<float2>(),

        //             meshData = outputMesh[0],
        //             scalingFactor = config.scalingFactor,
        //             globalTexelScale = config.globalTexelScale,
        //             textureWidth = materialOverride?.material?.mainTexture != null ? materialOverride.material.mainTexture.width : config.defaultTexSize,
        //             textureHeight = materialOverride?.material?.mainTexture != null ? materialOverride.material.mainTexture.height : config.defaultTexSize,
        //             meshOrigin = meshOrigin
        //         };
        //         jobHandle = jobData.Schedule(faceList.Count, 128);

        //         newMesh = new Mesh {
        //             name = meshName
        //         };
        //     }

        //     public Mesh Complete() {
        //         jobHandle.Complete();

        //         var meshData = outputMesh[0];
        //         meshData.subMeshCount = 1;
        //         meshData.SetSubMesh(0, new SubMeshDescriptor(0, triIndexCount), MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);

        //         Mesh.ApplyAndDisposeWritableMeshData(outputMesh, newMesh);
        //         newMesh.RecalculateNormals();
        //         newMesh.RecalculateBounds();

        //         if (config.addTangents) {
        //             newMesh.RecalculateTangents();
        //         }

        //         #if UNITY_EDITOR
        //         if ( config.addLightmapUV2 ) {
        //             UnwrapParam.SetDefaults( out var unwrap);
        //             unwrap.packMargin *= 2;
        //             Unwrapping.GenerateSecondaryUVSet( newMesh, unwrap );
        //         }

        //         if ( config.meshCompression != ScopaMapConfig.ModelImporterMeshCompression.Off)
        //             UnityEditor.MeshUtility.SetMeshCompression(newMesh, (ModelImporterMeshCompression)config.meshCompression);
        //         #endif

        //         faceVertexOffsets.Dispose();
        //         faceTriIndexCounts.Dispose();
        //         faceVertices.Dispose();
        //         faceUVoverride.Dispose();
        //         faceU.Dispose();
        //         faceV.Dispose();
        //         faceShift.Dispose();

        //         // If optimize everything, just combine the two optimizations into one call
        //         if ((config.optimizeMesh & ScopaMapConfig.ModelImporterMeshOptimization.OptimizeIndexBuffers) != 0 &&
        //             (config.optimizeMesh & ScopaMapConfig.ModelImporterMeshOptimization.OptimizeVertexBuffers) != 0)
        //         {
        //             newMesh.Optimize();
        //         }
        //         else
        //         {
        //             // Optimize index buffers
        //             if ((config.optimizeMesh & ScopaMapConfig.ModelImporterMeshOptimization.OptimizeIndexBuffers) != 0)
        //             {
        //                 newMesh.OptimizeIndexBuffers();
        //             }

        //             // Optimize vertex buffers
        //             if ((config.optimizeMesh & ScopaMapConfig.ModelImporterMeshOptimization.OptimizeVertexBuffers) != 0)
        //             {
        //                 newMesh.OptimizeReorderVertexBuffer();
        //             }
        //         }

        //         return newMesh;
        //     }

        // }

        // #if SCOPA_USE_BURST
        // [BurstCompile]
        // #endif
        // public struct MeshBuildingJob : IJobParallelFor
        // {
        //     [ReadOnlyAttribute] public NativeArray<int> faceVertexOffsets, faceTriIndexCounts; // index = i

        //     [ReadOnlyAttribute] public NativeArray<float3> faceVertices;
        //     [ReadOnlyAttribute] public NativeArray<float4> faceU, faceV; // index = i, .w = scale
        //     [ReadOnlyAttribute] public NativeArray<float2> faceShift, uvOverride; // index = i
        //     [ReadOnlyAttribute] public float3 meshOrigin;

        //     [NativeDisableParallelForRestriction]
        //     public Mesh.MeshData meshData;

        //     [ReadOnlyAttribute] public float scalingFactor, globalTexelScale, textureWidth, textureHeight;
        //     public const float IGNORE_UV = -99999f;

        //     // will need this to support non-Valve formats
        //     public static float2 Rotate(float2 v, float deltaRadians) {
        //         return new float2(
        //             v.x * math.cos(deltaRadians) - v.y * math.sin(deltaRadians),
        //             v.x * math.sin(deltaRadians) + v.y * math.cos(deltaRadians)
        //         );
        //     }

        //     public void Execute(int i)
        //     {
        //         var offsetStart = faceVertexOffsets[i];
        //         var offsetEnd = faceVertexOffsets[i+1];

        //         var outputVerts = meshData.GetVertexData<float3>();
        //         var outputUVs = meshData.GetVertexData<float2>(2);

        //         var outputTris = meshData.GetIndexData<int>();

        //         // add all verts, normals, and UVs
        //         for( int n=offsetStart; n<offsetEnd; n++ ) {
        //             outputVerts[n] = faceVertices[n] * scalingFactor - meshOrigin;

        //             if (uvOverride[n].x > IGNORE_UV) {
        //                 outputUVs[n] = uvOverride[n];
        //             } else {
        //                 outputUVs[n] = new float2(
        //                     (math.dot(faceVertices[n], faceU[i].xyz / faceU[i].w) + faceShift[i].x) / textureWidth,
        //                     (math.dot(faceVertices[n], faceV[i].xyz / faceV[i].w) - faceShift[i].y) / textureHeight
        //                 ) * globalTexelScale;
        //             }
        //         }

        //         // verts are already in correct order, add as basic fan pattern (since we know it's a convex face)
        //         for(int t=2; t<offsetEnd-offsetStart; t++) {
        //             outputTris[faceTriIndexCounts[i]+(t-2)*3] = offsetStart;
        //             outputTris[faceTriIndexCounts[i]+(t-2)*3+1] = offsetStart + t-1;
        //             outputTris[faceTriIndexCounts[i]+(t-2)*3+2] = offsetStart + t;
        //         }
        //     }
        // }

        public class ColliderJobGroup {

            NativeArray<int> faceVertexOffsets, faceTriIndexCounts, solidFaceOffsets; // index = i
            NativeArray<Vector3> faceVertices, facePlaneNormals;
            NativeArray<bool> canBeBoxCollider;
            int vertCount, triIndexCount, faceCount;

            public GameObject gameObject;
            public Mesh.MeshDataArray outputMesh;
            JobHandle jobHandle;
            Mesh[] meshes;
            bool isTrigger, isConvex;

            public ColliderJobGroup(GameObject gameObject, bool isTrigger, bool forceConvex, string colliderNameFormat, IEnumerable<Solid> solids, ScopaMapConfig config, Dictionary<Solid, Entity> mergedEntityData) {
                this.gameObject = gameObject;

                var faceList = new List<Face>();
                var solidFaceOffsetsManaged = new List<int>();
                var solidCount = 0;
                this.isTrigger = isTrigger;
                this.isConvex = forceConvex || config.colliderMode != ScopaMapConfig.ColliderImportMode.MergeAllToOneConcaveMeshCollider;
                foreach (var solid in solids) {
                    if (mergedEntityData.ContainsKey(solid) && (config.IsEntityNonsolid(mergedEntityData[solid].ClassName) || config.IsEntityTrigger(mergedEntityData[solid].ClassName)))
                        continue;

                    foreach (var face in solid.Faces) {
                        faceList.Add(face);
                    }

                    // if forceConvex or MergeAllToOneConcaveMeshCollider, then pretend it's all just one giant brush
                    // unless it's a trigger, then it MUST be convex
                    if (isTrigger || (!forceConvex && config.colliderMode != ScopaMapConfig.ColliderImportMode.MergeAllToOneConcaveMeshCollider) || solidCount == 0) {
                        solidFaceOffsetsManaged.Add(faceCount);
                        solidCount++;
                    }

                    faceCount += solid.Faces.Count;
                }
                solidFaceOffsetsManaged.Add(faceCount);

                solidFaceOffsets = new NativeArray<int>(solidFaceOffsetsManaged.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                solidFaceOffsets.CopyFrom(solidFaceOffsetsManaged.ToArray());
                canBeBoxCollider = new NativeArray<bool>(solidCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                faceVertexOffsets = new NativeArray<int>(faceList.Count + 1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                faceTriIndexCounts = new NativeArray<int>(faceList.Count + 1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < faceList.Count; i++) {
                    faceVertexOffsets[i] = vertCount;
                    vertCount += faceList[i].Vertices.Count;
                    faceTriIndexCounts[i] = triIndexCount;
                    triIndexCount += (faceList[i].Vertices.Count - 2) * 3;
                }
                faceVertexOffsets[faceVertexOffsets.Length - 1] = vertCount;
                faceTriIndexCounts[faceTriIndexCounts.Length - 1] = triIndexCount;

                faceVertices = new NativeArray<Vector3>(vertCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                facePlaneNormals = new NativeArray<Vector3>(faceList.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < faceList.Count; i++) {
                    for (int v = faceVertexOffsets[i]; v < faceVertexOffsets[i + 1]; v++) {
                        faceVertices[v] = faceList[i].Vertices[v - faceVertexOffsets[i]].ToUnity();
                    }
                    facePlaneNormals[i] = faceList[i].Plane.Normal.ToUnity();
                }

                outputMesh = Mesh.AllocateWritableMeshData(solidCount);
                meshes = new Mesh[solidCount];
                for (int i = 0; i < solidCount; i++) {
                    meshes[i] = new Mesh {
                        name = string.Format(colliderNameFormat, i.ToString("D5", System.Globalization.CultureInfo.InvariantCulture))
                    };

                    var solidOffsetStart = solidFaceOffsets[i];
                    var solidOffsetEnd = solidFaceOffsets[i + 1];
                    var finalVertCount = faceVertexOffsets[solidOffsetEnd] - faceVertexOffsets[solidOffsetStart];
                    var finalTriCount = faceTriIndexCounts[solidOffsetEnd] - faceTriIndexCounts[solidOffsetStart];

                    var meshData = outputMesh[i];
                    meshData.SetVertexBufferParams(finalVertCount,
                        new VertexAttributeDescriptor(VertexAttribute.Position)
                    );
                    meshData.SetIndexBufferParams(finalTriCount, IndexFormat.UInt32);
                }

                var jobData = new ColliderJob {
                    faceVertexOffsets = faceVertexOffsets,
                    faceTriIndexCounts = faceTriIndexCounts,
                    solidFaceOffsets = solidFaceOffsets,

                    faceVertices = faceVertices.Reinterpret<float3>(),
                    facePlaneNormals = facePlaneNormals.Reinterpret<float3>(),

                    meshDataArray = outputMesh,
                    canBeBoxColliderResults = canBeBoxCollider,
                    colliderMode = config.colliderMode,
                    scalingFactor = config.scalingFactor,
                    meshOrigin = gameObject.transform.position
                };
                jobHandle = jobData.Schedule(solidCount, 64);
            }

            public Mesh[] Complete() {
                jobHandle.Complete();

                Mesh.ApplyAndDisposeWritableMeshData(outputMesh, meshes);
                for (int i = 0; i < meshes.Length; i++) {
                    Mesh newMesh = meshes[i];
                    var newGO = new GameObject(newMesh.name);
                    newGO.transform.SetParent(gameObject.transform);
                    newGO.transform.localPosition = Vector3.zero;
                    newGO.transform.localRotation = Quaternion.identity;
                    newGO.transform.localScale = Vector3.one;

                    newMesh.RecalculateBounds();
                    if (canBeBoxCollider[i]) { // if box collider, we'll just use the mesh bounds to config a collider
                        var bounds = newMesh.bounds;
                        var boxCol = newGO.AddComponent<BoxCollider>();
                        boxCol.center = bounds.center;
                        boxCol.size = bounds.size;
                        boxCol.isTrigger = isTrigger;
                        meshes[i] = null; // and don't save box collider meshes
                    } else { // but usually this is a convex mesh collider
                        var newMeshCollider = newGO.AddComponent<MeshCollider>();
                        newMeshCollider.convex = isTrigger ? true : isConvex;
                        newMeshCollider.isTrigger = isTrigger;
                        newMeshCollider.sharedMesh = newMesh;
                    }
                }

                faceVertexOffsets.Dispose();
                faceTriIndexCounts.Dispose();
                solidFaceOffsets.Dispose();

                faceVertices.Dispose();
                facePlaneNormals.Dispose();
                canBeBoxCollider.Dispose();

                return meshes;
            }

#if SCOPA_USE_BURST
            [BurstCompile]
#endif
            public struct ColliderJob : IJobParallelFor {
                [ReadOnlyAttribute] public NativeArray<int> faceVertexOffsets, faceTriIndexCounts, solidFaceOffsets; // index = i

                [ReadOnlyAttribute] public NativeArray<float3> faceVertices, facePlaneNormals;
                [ReadOnlyAttribute] public float3 meshOrigin;

                public Mesh.MeshDataArray meshDataArray;
                [WriteOnly] public NativeArray<bool> canBeBoxColliderResults;

                [ReadOnlyAttribute] public float scalingFactor;
                [ReadOnlyAttribute] public ScopaMapConfig.ColliderImportMode colliderMode;

                public void Execute(int i) {
                    var solidOffsetStart = solidFaceOffsets[i];
                    var solidOffsetEnd = solidFaceOffsets[i + 1];

                    var solidVertStart = faceVertexOffsets[solidOffsetStart];
                    var finalTriIndexCount = faceTriIndexCounts[solidOffsetEnd] - faceTriIndexCounts[solidOffsetStart];

                    var meshData = meshDataArray[i];
                    var outputVerts = meshData.GetVertexData<float3>();
                    var outputTris = meshData.GetIndexData<int>();

                    // for each solid, gather faces...
                    var canBeBoxCollider = colliderMode == ScopaMapConfig.ColliderImportMode.BoxAndConvex || colliderMode == ScopaMapConfig.ColliderImportMode.BoxColliderOnly;
                    for (int face = solidOffsetStart; face < solidOffsetEnd; face++) {
                        // don't bother doing BoxCollider test if we're forcing BoxColliderOnly
                        if (canBeBoxCollider && colliderMode != ScopaMapConfig.ColliderImportMode.BoxColliderOnly) {
                            // but otherwise, test if all face normals are axis aligned... if so, it can be a box collider
#if SCOPA_USE_BURST
                            var absNormal = math.abs(facePlaneNormals[face]);
#else
                            var absNormal = faceNormal.Absolute();
#endif

                            canBeBoxCollider = !((absNormal.x > 0.01f && absNormal.x < 0.99f)
                                            || (absNormal.z > 0.01f && absNormal.z < 0.99f)
                                            || (absNormal.y > 0.01f && absNormal.y < 0.99f));
                        }

                        var vertOffsetStart = faceVertexOffsets[face];
                        var vertOffsetEnd = faceVertexOffsets[face + 1];
                        for (int n = vertOffsetStart; n < vertOffsetEnd; n++) {
                            outputVerts[n - solidVertStart] = faceVertices[n] * scalingFactor - meshOrigin;
                        }

                        // verts are already in correct order, add as basic fan pattern (since we know it's a convex face)
                        var triIndexStart = faceTriIndexCounts[face] - faceTriIndexCounts[solidOffsetStart];
                        var faceVertStart = vertOffsetStart - solidVertStart;
                        for (int t = 2; t < vertOffsetEnd - vertOffsetStart; t++) {
                            outputTris[triIndexStart + (t - 2) * 3] = faceVertStart;
                            outputTris[triIndexStart + (t - 2) * 3 + 1] = faceVertStart + t - 1;
                            outputTris[triIndexStart + (t - 2) * 3 + 2] = faceVertStart + t;
                        }
                    }

                    canBeBoxColliderResults[i] = canBeBoxCollider;
                    meshData.subMeshCount = 1;
                    meshData.SetSubMesh(
                        0,
                        new SubMeshDescriptor(0, finalTriIndexCount)
                    );
                }
            }
        }

        public static void WeldVertices(this Mesh aMesh, float aMaxDelta = 0.1f, float maxAngle = 180f) {
            var verts = aMesh.vertices;
            var normals = aMesh.normals;
            var uvs = aMesh.uv;
            List<int> newVerts = new List<int>();
            int[] map = new int[verts.Length];
            // create mapping and filter duplicates.
            for (int i = 0; i < verts.Length; i++) {
                var p = verts[i];
                var n = normals[i];
                var uv = uvs[i];
                bool duplicate = false;
                for (int i2 = 0; i2 < newVerts.Count; i2++) {
                    int a = newVerts[i2];
                    if (
                        (verts[a] - p).sqrMagnitude <= aMaxDelta // compare position
                        && Vector3.Angle(normals[a], n) <= maxAngle // compare normal
                                                                    // && (uvs[a] - uv).sqrMagnitude <= aMaxDelta // compare first uv coordinate
                        ) {
                        map[i] = i2;
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate) {
                    map[i] = newVerts.Count;
                    newVerts.Add(i);
                }
            }
            // create new vertices
            var verts2 = new Vector3[newVerts.Count];
            var normals2 = new Vector3[newVerts.Count];
            var uvs2 = new Vector2[newVerts.Count];
            for (int i = 0; i < newVerts.Count; i++) {
                int a = newVerts[i];
                verts2[i] = verts[a];
                normals2[i] = normals[a];
                uvs2[i] = uvs[a];
            }
            // map the triangle to the new vertices
            var tris = aMesh.triangles;
            for (int i = 0; i < tris.Length; i++) {
                tris[i] = map[tris[i]];
            }
            aMesh.Clear();
            aMesh.vertices = verts2;
            aMesh.normals = normals2;
            aMesh.triangles = tris;
            aMesh.uv = uvs2;
        }

        public static void SmoothNormalsJobs(this Mesh aMesh, float weldingAngle = 80, float maxDelta = 0.1f) {
            var meshData = Mesh.AcquireReadOnlyMeshData(aMesh);
            var verts = new NativeArray<Vector3>(meshData[0].vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            meshData[0].GetVertices(verts);
            var normals = new NativeArray<Vector3>(meshData[0].vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            meshData[0].GetNormals(normals);
            var smoothNormalsResults = new NativeArray<Vector3>(meshData[0].vertexCount, Allocator.TempJob);

            var jobData = new SmoothJob();
            jobData.cos = Mathf.Cos(weldingAngle * Mathf.Deg2Rad);
            jobData.maxDelta = maxDelta;

            jobData.verts = verts.Reinterpret<float3>();
            jobData.normals = normals.Reinterpret<float3>();
            jobData.results = smoothNormalsResults.Reinterpret<float3>();

            var handle = jobData.Schedule(smoothNormalsResults.Length, 8);
            handle.Complete();

            meshData.Dispose(); // must dispose this early, before modifying mesh

            aMesh.SetNormals(smoothNormalsResults);

            verts.Dispose();
            normals.Dispose();
            smoothNormalsResults.Dispose();
        }

#if SCOPA_USE_BURST
        [BurstCompile]
#endif
        public struct SmoothJob : IJobParallelFor {
            [ReadOnlyAttribute] public NativeArray<float3> verts, normals;
            public NativeArray<float3> results;

            public float cos, maxDelta;

            public void Execute(int i) {
                var tempResult = normals[i];
                var resultCount = 1;

                for (int i2 = 0; i2 < verts.Length; i2++) {
                    if (math.lengthsq(verts[i2] - verts[i]) <= maxDelta && math.dot(normals[i2], normals[i]) >= cos) {
                        tempResult += normals[i2];
                        resultCount++;
                    }
                }

                if (resultCount > 1)
                    tempResult = math.normalize(tempResult / resultCount);
                results[i] = tempResult;
            }
        }

        public static void SnapBrushVertices(Solid sledgeSolid, float snappingDistance = 4f) {
            // snap nearby vertices together within in each solid -- but always snap to the FURTHEST vertex from the center
            var origin = new System.Numerics.Vector3();
            var vertexCount = 0;
            foreach (var face in sledgeSolid.Faces) {
                for (int i = 0; i < face.Vertices.Count; i++) {
                    origin += face.Vertices[i];
                }
                vertexCount += face.Vertices.Count;
            }
            origin /= vertexCount;

            foreach (var face1 in sledgeSolid.Faces) {
                foreach (var face2 in sledgeSolid.Faces) {
                    if (face1 == face2)
                        continue;

                    for (int a = 0; a < face1.Vertices.Count; a++) {
                        for (int b = 0; b < face2.Vertices.Count; b++) {
                            if ((face1.Vertices[a] - face2.Vertices[b]).LengthSquared() < snappingDistance * snappingDistance) {
                                if ((face1.Vertices[a] - origin).LengthSquared() > (face2.Vertices[b] - origin).LengthSquared())
                                    face2.Vertices[b] = face1.Vertices[a];
                                else
                                    face1.Vertices[a] = face2.Vertices[b];
                            }
                        }
                    }
                }
            }
        }

    }
}