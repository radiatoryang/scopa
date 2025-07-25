// uncomment to enable debug messages
#define SCOPA_MESH_DEBUG
// #define SCOPA_MESH_VERBOSE

#if SCOPA_MESH_DEBUG
using System.Diagnostics;
using Debug = UnityEngine.Debug;
#endif

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
using System.Globalization;

#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Scopa {
    /// <summary>main class for Scopa mesh generation / geo functions </summary>
    public static class ScopaMesh {
        /// <summary>
        /// <para>Main class that schedules multi-threaded jobs to process all of one (1) entity's Quake brushes into Unity mesh data and colliders, all at once.</para>
        /// <para>API: see <c>ScopaMaterialConfig.OnBuildMeshObject()</c> to add your own code to modify meshes, per material.
        /// <br />You can even use the raw jobs data / raw Quake-space map data too, before it gets disposed.</para>
        /// <list> Jobs pipeline overview for meshes in rendererResults:
        /// <item> 1. VertJob clips Quake planes to fill vertex stream with shared vertex data </item>
        /// <item> 2. VertCountJob counts and finalizes shared vertex data, per face </item>
        /// <item> 3. (optional) OcclusionJob culls hidden faces </item>
        /// <item> 4. MeshCountJob counts non-culled vertices and tri indices per mesh </item>
        /// <item> 5. MeshJob uses MeshCounts to fill MeshDataArray with verts, tris, etc</item>
        /// <item> 6. Complete() turns MeshDataArray into actual Meshes</item>
        /// </list></summary>
        public class ScopaMeshJobGroup {
            public ScopaMapConfig config;
            public string namePrefix;
            public float3 entityOrigin;
            public ScopaEntityData entityData;
            public Dictionary<Solid, Entity> mergedEntityData;

            // SHARED BUFFERS
            /// <summary> Raw Quake-space brush data from the Sledge MAP parser. </summary>
            public Solid[] solids;
            /// <summary> Shared buffer for Quake-space planes (xyz = normal, w = distance) straight from Sledge MAP parser. </summary>
            public NativeArray<double4> allPlanes;
            /// <summary> Per-brush offsets in the planes array. </summary>
            public NativeArray<int> allPlaneOffsets;
            /// <summary> Contains Quake-space verts for all meshes. Populated by CountJob, based on NativeStream data. </summary>
            public NativeArray<float3> allVerts;

            // PER-FACE DATA
            /// <summary> Per-face vertStart offsets and vertCount, used with allVerts. </summary>
            public NativeArray<ScopaFaceData> faceData;
            /// <summary> Per-mesh-face vertStart offsets and vertCount, populated by a MeshCountsJob, used with allVerts to build a mesh. </summary>
            public NativeArray<ScopaFaceMeshData> faceMeshData, colliderFaceMeshData;
            /// <summary> Per-face UV axis data. </summary>
            public NativeArray<ScopaFaceUVData> faceUVData;

            // PER-MESH DATA
            /// <summary> Various metadata / counters, each one = a mesh </summary>
            public NativeArray<ScopaMeshCounts> meshCounts, colliderMeshCounts;
            /// <summary> Unity's special data structure for the actual final mesh data. Populated by MeshJob. </summary>
            public Mesh.MeshDataArray meshDataArray, colliderMeshDataArray;

            // COLLISION DATA
            /// <summary> Data to generate Box Colliders </summary>
            public NativeArray<ScopaBoxColliderData> boxColliderData;
            public NativeArray<ColliderSolidity> colliderSolidity;

            // TEXTURES AND MATERIALS
            /// <summary> Managed list of all texture names, verbatim </summary>
            public List<string> textureNames = new();
            /// <summary> Burst-compatible list of various metadata / counters, corresponds to textureNames list </summary>
            public NativeList<ScopaTextureData> textureData;
            /// <summary> Lookup to convert a textureName (verbatim) to a Scopa MaterialOverride.
            /// Order may not match textureNames list, since C# dictionaries are not indexed / ordered. </summary>
            public Dictionary<string, ScopaMapConfig.MaterialOverride> textureToMaterial = new();

            // RESULTS
            /// <summary> Finalized data to generate renderers. Will be NULL until Complete() is called, but will persist even after disposal.</summary>
            public List<ScopaRendererMeshResult> rendererResults;
            /// <summary> Finalized data to generate colliders. Will be NULL until Complete() is called, but will persist even after disposal.</summary>
            public List<ScopaColliderResult> colliderResults;

            JobHandle finalJobHandle, colliderJobHandle;
            bool hasColliderJobs;

            public ScopaMeshJobGroup(ScopaMapConfig config, string namePrefix, Vector3 entityOrigin, ScopaEntityData entity, Solid[] solids, Dictionary<Solid, Entity> mergedEntityData, Dictionary<string, Material> materialSearch = null) {
                this.config = config;
                this.namePrefix = namePrefix;
                this.entityOrigin = entityOrigin;
                this.entityData = entity;
                this.mergedEntityData = mergedEntityData;
                this.solids = solids;

                StartTimer("Total");
                StartTimer("Planes");

                // Planes Prep can never be Bursted because Solids are managed
                var planeCount = 0;
                allPlaneOffsets = new NativeArray<int>(solids.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < solids.Length; i++) {
                    allPlaneOffsets[i] = planeCount;
                    planeCount += solids[i].Faces.Count;
                }

                allPlanes = new(planeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < solids.Length; i++) {
                    if (solids[i].Faces.Count < 4) {
                        Debug.LogWarning($"{entity.ClassName}'s brush #{i} has less than 4 faces, which is impossible. It will be ignored. (Fix it in your level editor.)");
                        continue;
                    }
                    for (int p = 0; p < solids[i].Faces.Count; p++) {
                        var quakeFace = solids[i].Faces[p];
                        allPlanes[allPlaneOffsets[i] + p] = new double4(
                            quakeFace.Plane.Normal.X,
                            quakeFace.Plane.Normal.Y,
                            quakeFace.Plane.Normal.Z,
                            -quakeFace.Plane.D
                        );
                    }
                }
                var allVertStream = new NativeStream(planeCount, Allocator.TempJob);
                StopTimer("Planes");

                // VERT JOB - intersect the planes to generate vertices
                StartTimer("Verts");
                var vertJob = new VertJob {
                    planeOffsets = allPlaneOffsets,
                    facePlanes = allPlanes,
                    allVertStream = allVertStream.AsWriter()
                };
                vertJob.Schedule(solids.Length, 64).Complete();
                StopTimer("Verts");

                // VERT COUNT JOB - count vertices and make the shared buffer
                StartTimer("VertCount");
                faceData = new(planeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                allVerts = new(allVertStream.Count(), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                var vertCountJob = new VertCountJob {
                    faceData = faceData,
                    planes = allPlanes,
                    allVerts = allVerts,
                    allVertStream = allVertStream,
                };
                var vertCountJobHandle = vertCountJob.Schedule();

                // While vert count happens, read the rest of the Face data / allocate mesh data memory
                faceMeshData = new(planeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                faceUVData = new(planeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                textureData = new(128, Allocator.TempJob);
                for (int i = 0; i < solids.Length; i++) {
                    for (int p = 0; p < solids[i].Faces.Count; p++) {
                        // detect materials and allocate
                        var quakeFace = solids[i].Faces[p];
                        var textureName = quakeFace.TextureName;
                        var pIndex = allPlaneOffsets[i] + p;

                        if (textureNames.Contains(textureName)) {
                            var index = textureNames.IndexOf(textureName);
                            faceMeshData[pIndex] = new(index, textureData[index].textureIndex < 0);
                        } else {
                            var matOverride = config.GetMaterialOverrideFor(textureName);

                            if (matOverride == null) {
                                var search = textureName.ToLowerInvariant();
                                var material = materialSearch != null && materialSearch.ContainsKey(search) ? materialSearch[search] : config.GetDefaultMaterial();
                                matOverride = new(textureName, material);
                            }
                            textureToMaterial.Add(textureName, matOverride);

                            bool isCulled = config.IsTextureNameCulled(textureName);
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
                allVertStream.Dispose();
                StopTimer("VertCount");

                if (config.removeHiddenFaces) {
                    StartTimer("Occlusion");
                    var planeLookup = new NativeParallelMultiHashMap<int4, int>(planeCount, Allocator.TempJob);
                    var planeLookupJob = new PlaneLookupJob {
                        planes = allPlanes,
                        faceMeshData = faceMeshData,
                        planeLookup = planeLookup
                    };
                    planeLookupJob.Run(planeCount);

                    var occlusionJob = new OcclusionJob {
                        allVerts = allVerts,
                        planes = allPlanes,
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
                    planes = allPlanes,
                    faceData = faceData,
                    faceUVData = faceUVData,
                    faceMeshData = faceMeshData,
                    allVerts = allVerts,
                    meshOrigin = entityOrigin,
                    meshDataArray = meshDataArray,
                    textureData = textureData.AsArray(),
                    scalingConfig = new float2(config.globalTexelScale, config.scalingFactor)
                };
                finalJobHandle = meshJob.Schedule(planeCount, 128);

                // TODO: vertex snap + smooth normals job?

                // StartTimer("VertSnap");
                // var snappingJob = new VertSnapJob {
                //     facePlanes = planes,
                //     faceVerts = faceVerts,
                //     planeOffsets = planeOffsets,
                //     faceData = faceData,
                //     snappingDistance = config.snappingThreshold
                // };
                // snappingJob.Schedule(planeOffsets.Length, 128).Complete();
                // StopTimer("VertSnap");

                hasColliderJobs = config.colliderMode != ScopaMapConfig.ColliderImportMode.None && !config.IsEntityNonsolid(entityData.ClassName);
                if (hasColliderJobs)
                    StartColliderJobs();
            }

#region Renderer Mesh Jobs
            /// <summary> Per-brush job that intersects planes to generate vertices.
            /// The vertices go into a big per-face shared NativeStream buffer. </summary>
#if SCOPA_USE_BURST
            [BurstCompile(FloatMode = FloatMode.Fast)]
#endif
            public struct VertJob : IJobParallelFor {
                [ReadOnlyAttribute] public NativeArray<double4> facePlanes;
                [NativeDisableParallelForRestriction] public NativeStream.Writer allVertStream;
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

                        allVertStream.BeginForEachIndex(planeOffsets[i] + p);
                        for (int v = 0; v < polygon.Length; v++) {
                            allVertStream.Write((float3)polygon[v]); // we write vertices back to single precision
                        }
                        allVertStream.EndForEachIndex();
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
                public NativeStream allVertStream;
                [ReadOnlyAttribute] public NativeArray<double4> planes;
                [WriteOnly] public NativeArray<ScopaFaceData> faceData;
                [WriteOnly] public NativeArray<float3> allVerts;

                public void Execute() {
                    var vRead = allVertStream.AsReader();
                    var vertCounter = 0;
                    for (int i = 0; i < planes.Length; i++) {
                        var center = float3.zero;
                        var count = vRead.BeginForEachIndex(i); // each ForEachBuffer is a face
                        for (int v = 0; v < count; v++) {
                            var vert = vRead.Read<float3>();
                            allVerts[vertCounter + v] = vert;
                            center += vert;
                        }
                        vRead.EndForEachIndex();

                        faceData[i] = new(vertCounter, count, center/count);
                        vertCounter += count;
                    }
                }
            }

            //             /// <summary> Per-brush job that snaps verts together, outward, to minimize seams. </summary>
            // #if SCOPA_USE_BURST
            //             [BurstCompile(FloatMode = FloatMode.Fast)]
            // #endif
            //             public struct VertSnapJob : IJobParallelFor {
            //                 [ReadOnlyAttribute] public NativeArray<double4> facePlanes;
            //                 [ReadOnlyAttribute] public NativeArray<int> planeOffsets;
            //                 [NativeDisableParallelForRestriction] public NativeArray<float3> faceVerts;
            //                 [ReadOnlyAttribute] public NativeArray<ScopaFaceData> faceData;
            //                 [ReadOnlyAttribute] public float snappingDistance;

            //                 public void Execute(int i) { // i = solid index
            //                     int planeCount = (i + 1 < planeOffsets.Length ? planeOffsets[i + 1] : facePlanes.Length) - planeOffsets[i];

            //                     // snap nearby vertices together within each solid -- but always snap to the FURTHEST vertex from the center
            //                     var vertexCount = 0;
            //                     var origin = float3.zero;
            //                     for (var p=0; p<planeCount; p++) {
            //                         for (int v = 0; v<faceData[p].vertCount; i++) {
            //                             origin += faceVerts[faceData[p].vertIndexStart+v];
            //                         }
            //                         vertexCount += faceData[p].vertCount;
            //                     }
            //                     origin /= vertexCount;

            //                     for(var f1=0; f1<planeCount; f1++) {
            //                         for(var f2=0; f2<planeCount; f2++) {
            //                             if (f1 == f2)
            //                                 continue;

            //                             for (int a=0; a < faceData[f1].vertCount; a++) {
            //                                 var vA = faceData[f1].vertIndexStart+a;
            //                                 for (int b=0; b < faceData[f2].vertCount; b++) {
            //                                     var vB = faceData[f2].vertIndexStart+b;
            //                                     if (math.lengthsq(faceVerts[vA] - faceVerts[vB]) < snappingDistance * snappingDistance) {
            //                                         if (math.lengthsq(faceVerts[vA] - origin) > math.lengthsq(faceVerts[vB] - origin))
            //                                             faceVerts[vB] = faceVerts[vA];
            //                                         else
            //                                             faceVerts[vA] = faceVerts[vB];
            //                                     }
            //                                 }
            //                             }
            //                         }
            //                     }
            //                 }
            //             }

            /// <summary> Generates a plane lookup to accelerate OcclusionJob. </summary>
#if SCOPA_USE_BURST
            [BurstCompile(FloatMode = FloatMode.Fast)]
#endif
            public struct PlaneLookupJob : IJobFor {
                [ReadOnlyAttribute] public NativeArray<double4> planes;
                [ReadOnlyAttribute] public NativeArray<ScopaFaceMeshData> faceMeshData;
                [WriteOnly] public NativeParallelMultiHashMap<int4, int> planeLookup;

                public void Execute(int i) {
                    if (faceMeshData[i].meshIndex >= 0)
                        planeLookup.Add(GetPlaneLookupKey(planes[i]), i);
                }
            }

            const int OCCLUSION_PRECISION = 100;
            const double OCCLUSION_TOLERANCE = 0.01d;
            static int4 GetPlaneLookupKey(double4 plane) {
                var roundedNormal = math.round(plane.xyz);
                if (math.lengthsq(roundedNormal - plane.xyz) > OCCLUSION_TOLERANCE * OCCLUSION_TOLERANCE) {
                    return (int4)(plane * OCCLUSION_PRECISION);
                } else {
                    return new int4(
                        ((int)roundedNormal.x) * OCCLUSION_PRECISION,
                        ((int)roundedNormal.y) * OCCLUSION_PRECISION,
                        ((int)roundedNormal.z) * OCCLUSION_PRECISION,
                        (int)(plane.w * OCCLUSION_PRECISION)
                    );
                }
            }

            /// <summary> Per-face job to cull each face covered by another face (in same entity) </summary>
#if SCOPA_USE_BURST
            [BurstCompile(FloatMode = FloatMode.Fast)]
#endif
            public struct OcclusionJob : IJobParallelFor {
                [ReadOnlyAttribute] public NativeArray<float3> allVerts;
                [ReadOnlyAttribute] public NativeArray<double4> planes;
                [ReadOnlyAttribute] public NativeArray<ScopaFaceData> faceData;
                [NativeDisableParallelForRestriction] public NativeArray<ScopaFaceMeshData> faceMeshData;
                [ReadOnlyAttribute] public NativeParallelMultiHashMap<int4, int> planeLookup;

                public void Execute(int i) { // i = face index
                    if (faceMeshData[i].meshIndex < 0)
                        return;

                    var planeLookupKey = GetPlaneLookupKey(planes[i]) * -1;
                    if (!planeLookup.ContainsKey(planeLookupKey))
                        return;

                    var face = faceData[i];
                    var offsetStart = face.vertIndexStart;
                    var offsetEnd = offsetStart + face.vertCount;
                    var ignoreAxis = GetIgnoreAxis(math.abs(planes[i]));

                    var planeLookupEnumerator = planeLookup.GetValuesForKey(planeLookupKey);
                    foreach (var n in planeLookupEnumerator) {
                        var otherPolygon = new NativeArray<float3>(faceData[n].vertCount, Allocator.Temp);
                        NativeArray<float3>.Copy(allVerts, faceData[n].vertIndexStart, otherPolygon, 0, faceData[n].vertCount);

                        var foundOutsideVert = false;
                        for (int x = offsetStart; x < offsetEnd && !foundOutsideVert; x++) {
                            var point = allVerts[x] + math.normalize(face.faceCenter - allVerts[x]) * 0.2f; // shrink face point slightly
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
                        if (faceMeshDataReadOnly[face].meshIndex != i) 
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
                [ReadOnlyAttribute] public NativeArray<float3> allVerts;

                [ReadOnlyAttribute] public float3 meshOrigin;
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
                    if (faceMeshData[i].meshIndex < 0)
                        return;

                    var face = faceData[i];
                    var faceMesh = faceMeshData[i];

                    var meshData = meshDataArray[faceMesh.meshIndex];
                    var outputVerts = meshData.GetVertexData<float3>();
                    var outputNorms = meshData.GetVertexData<float3>(1);
                    var outputUVs = meshData.GetVertexData<float2>(2);
                    var outputTris = meshData.GetIndexData<uint>();

                    // add all verts, normals, and UVs
                    // Debug.Log($"MeshJob {i} - vertStart {face.vertIndexStart} vertCount {face.vertCount}");
                    for (int x = 0; x < face.vertCount; x++) {
                        var mdi = faceMesh.meshVertStart + x; // mesh data index
                        var n = face.vertIndexStart + x; // global vert buffer index

                        // all Face Datas are still in Quake space; need to convert to Unity space and axes
                        outputVerts[mdi] = allVerts[n].xzy * scalingConfig.y - meshOrigin;
                        outputNorms[mdi] = (float3)planes[i].xzy;

                        var uv = faceUVData[i];
                        outputUVs[mdi] = new float2( // NOTE: UV axes are already scaled
                            (math.dot(allVerts[n].xzy, uv.faceU.xzy) + uv.faceU.w) / textureData[faceMesh.meshIndex].textureWidth,
                            (math.dot(allVerts[n].xzy, -uv.faceV.xzy) - uv.faceV.w) / textureData[faceMesh.meshIndex].textureHeight
                        ) * scalingConfig.x;
                    }

                    // TODO: snapping pass / normals / welding on vertices

                    // verts are already in correct order, add as basic fan pattern (since we know it's a convex face)
                    for (int t = 2; t < face.vertCount; t++) {
                        outputTris[faceMesh.meshTriStart + (t - 2) * 3] = (uint)faceMesh.meshVertStart;
                        outputTris[faceMesh.meshTriStart + (t - 2) * 3 + 1] = (uint)(faceMesh.meshVertStart + t - 1);
                        outputTris[faceMesh.meshTriStart + (t - 2) * 3 + 2] = (uint)(faceMesh.meshVertStart + t);
                    }
                }
            }



#endregion
#region Collider Jobs

            void StartColliderJobs() {
                StartTimer("PrepColliderJobs");
                var mainColliderMode = GetColliderSolidity(entityData.ClassName);
                var isMegaMeshCollider = config.colliderMode == ScopaMapConfig.ColliderImportMode.MergeAllToOneConcaveMeshCollider
                    && mainColliderMode != ColliderSolidity.Trigger;
                colliderFaceMeshData = new(faceMeshData.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                // for each solid, determine whether it's Solid, Trigger, or No Collider
                colliderSolidity = new NativeArray<ColliderSolidity>(solids.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                for(int i=0; i<solids.Length; i++) {
                    // for merged entities, check the entity's original classname
                    if (mergedEntityData.ContainsKey(solids[i])) {
                        colliderSolidity[i] = GetColliderSolidity(mergedEntityData[solids[i]].ClassName);
                    } else {
                        colliderSolidity[i] = mainColliderMode;
                    }

                    if (isMegaMeshCollider && colliderSolidity[i] == ColliderSolidity.SolidConvex)
                        colliderSolidity[i] = ColliderSolidity.SolidConcaveMegaCollider;
                    
                }
                StopTimer("PrepColliderJobs");

                StartTimer("BrushColliderData");
                var fakeTextureData = new NativeArray<ScopaTextureData>(solids.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                var allowBoxColliders = config.colliderMode != ScopaMapConfig.ColliderImportMode.ConvexMeshColliderOnly && !isMegaMeshCollider;
                boxColliderData = new(solids.Length, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                var brushColliderJob = new BrushColliderJob {
                    planes = allPlanes,
                    planeOffsets = allPlaneOffsets,
                    colliderSolidity = colliderSolidity,
                    faceData = faceData,
                    colliderFaceMeshData = colliderFaceMeshData,
                    allowBoxColliders = allowBoxColliders,
                    boxColliderData = boxColliderData,
                    fakeTextureData = fakeTextureData
                };
                brushColliderJob.Schedule(solids.Length, 64).Complete();
                StopTimer("BrushColliderData");

                StartTimer("ColliderMeshCount");
                // TODO: generate fake textureDatas from BrushColliderData?
                var colliderFaceMeshDataReadOnly = new NativeArray<ScopaFaceMeshData>(colliderFaceMeshData.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                colliderFaceMeshData.CopyTo(colliderFaceMeshDataReadOnly);

                colliderMeshCounts = new(solids.Length, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                var meshCountJob = new MeshCountJob {
                    faceData = faceData,
                    faceMeshDataReadOnly = colliderFaceMeshDataReadOnly,
                    faceMeshData = colliderFaceMeshData,
                    meshCounts = colliderMeshCounts,
                    textureData = fakeTextureData
                };
                meshCountJob.Schedule(solids.Length, 64).Complete();
                colliderFaceMeshDataReadOnly.Dispose();
                fakeTextureData.Dispose();
                StopTimer("ColliderMeshCount");

                StartTimer("ColliderMeshBuild");
                colliderMeshDataArray = Mesh.AllocateWritableMeshData(solids.Length);
                for (int i = 0; i < solids.Length; i++) {
                    var meshData = colliderMeshDataArray[i];
                    meshData.SetVertexBufferParams(colliderMeshCounts[i].vertCount,
                        new VertexAttributeDescriptor(VertexAttribute.Position)
                    );
                    meshData.SetIndexBufferParams(colliderMeshCounts[i].triIndexCount, IndexFormat.UInt32);
                }

                var meshJob = new MeshColliderJob {
                    faceData = faceData,
                    colliderFaceMeshData = colliderFaceMeshData,
                    allVerts = allVerts,
                    meshOrigin = entityOrigin,
                    colliderMeshDataArray = colliderMeshDataArray,
                    scalingFactor = config.scalingFactor
                };
                colliderJobHandle = meshJob.Schedule(allPlanes.Length, 128);
            }

            ColliderSolidity GetColliderSolidity(string className) {
                if (config.IsEntityNonsolid(className)) {
                    return ColliderSolidity.NoCollider;
                } else if (config.IsEntityTrigger(className)) {
                    return ColliderSolidity.Trigger;
                } else {
                    return ColliderSolidity.SolidConvex;
                }
            }

#if SCOPA_USE_BURST
            [BurstCompile(FloatMode = FloatMode.Fast)]
#endif
            public struct BrushColliderJob : IJobParallelFor {
                [ReadOnlyAttribute] public NativeArray<double4> planes;
                [ReadOnlyAttribute] public NativeArray<int> planeOffsets;
                [ReadOnlyAttribute] public NativeArray<ColliderSolidity> colliderSolidity;
                [ReadOnlyAttribute] public NativeArray<ScopaFaceData> faceData;
                [NativeDisableParallelForRestriction, WriteOnly] public NativeArray<ScopaFaceMeshData> colliderFaceMeshData;
                const double BOX_ORTHOGONAL_TOLERANCE = 0.01d;
                [ReadOnlyAttribute] public bool allowBoxColliders;
                [WriteOnly] public NativeArray<ScopaBoxColliderData> boxColliderData;
                [WriteOnly] public NativeArray<ScopaTextureData> fakeTextureData;

                public void Execute(int i) { // i = solid index
                    int planeCount = (i + 1 < planeOffsets.Length ? planeOffsets[i + 1] : planes.Length) - planeOffsets[i];
                    var planeStart = planeOffsets[i];
                    var planeEnd = planeStart+planeCount;

                    int meshIndex = allowBoxColliders ? -1 : i;
                    if (colliderSolidity[i] == ColliderSolidity.NoCollider)
                        meshIndex = -2;
                    else if (colliderSolidity[i] == ColliderSolidity.SolidConcaveMegaCollider)
                        meshIndex = 0;
                    else if (allowBoxColliders && planeCount != 6) // a box must have 6 sides
                        meshIndex = i;

                    // for a box, every dot product must be a whole number
                    for (int p = planeStart + 1; meshIndex == -1 && p < planeEnd; p++) {
                        var dot = math.dot(planes[planeStart].xyz, planes[p].xyz);
                        if (math.abs(math.round(dot)-dot) > BOX_ORTHOGONAL_TOLERANCE)
                            meshIndex = i;
                    }

                    // must always write face data, no matter what, so MeshCountsJob doesn't count a wrong face
                    for(int p=planeStart; p<planeEnd; p++) {
                        colliderFaceMeshData[p] = new(0, 0, meshIndex);
                    };

                    fakeTextureData[i] = new(0, 0, meshIndex == i ? i : -1);
                    if (meshIndex == -2) { // skip this solid
                        boxColliderData[i] = float3x3.zero;
                    } else if (meshIndex == -1) { // generate a box collider
                        // position / centroid: average of all face centers
                        var position = float3.zero;
                        var faceCenters = new NativeList<float3>(6, Allocator.Temp);
                        for(int p=0; p<planeCount; p++) {
                            position += faceData[planeStart+p].faceCenter;
                            faceCenters.Add(faceData[planeStart+p].faceCenter);
                        }
                        position /= 6;

                        // size: the vector between each pair of parallel face centers
                        var up = MeasureDistantPairs( 2, faceCenters );
                        var forward = MeasureDistantPairs( 1, faceCenters );
                        var right = MeasureDistantPairs( 0, faceCenters );
                        var size = new float3( math.length(right), math.length(forward), math.length(up));

                        // rotation
                        var rotation = quaternion.LookRotation(math.normalize(up), math.normalize(forward));
                        var euler = math.Euler(rotation);
                        boxColliderData[i] = new(position, euler, size);

                        faceCenters.Dispose();
                    }
                }
            }

            static float3 MeasureDistantPairs(int axis, NativeList<float3> list) {
                if (axis == 0)
                    list.Sort( new SortByX() );
                else if (axis == 1)
                    list.Sort( new SortByY() );
                else
                    list.Sort( new SortByZ() );
                
                var vector = list[list.Length-1] - list[0];
                list.RemoveAt(list.Length-1);
                list.RemoveAt(0);
                return vector;
            }

            struct SortByX : IComparer<float3> {
                public int Compare(float3 a, float3 b) {
                    return a.x.CompareTo(b.x);
                }
            }

            struct SortByY : IComparer<float3> {
                public int Compare(float3 a, float3 b) {
                    return a.y.CompareTo(b.y);
                }
            }

            struct SortByZ : IComparer<float3> {
                public int Compare(float3 a, float3 b) {
                    return a.z.CompareTo(b.z);
                }
            }

            /// <summary> Basically like MeshJob, but just vertices and tris (for collision meshes) </summary>
            public struct MeshColliderJob : IJobParallelFor {
                [ReadOnlyAttribute] public NativeArray<ScopaFaceData> faceData;
                [ReadOnlyAttribute] public NativeArray<ScopaFaceMeshData> colliderFaceMeshData;
                [ReadOnlyAttribute] public NativeArray<float3> allVerts;
                [ReadOnlyAttribute] public float3 meshOrigin;
                [NativeDisableParallelForRestriction] public Mesh.MeshDataArray colliderMeshDataArray;
                [ReadOnlyAttribute] public float scalingFactor;

                public void Execute(int i) { // i = face index
                    if (colliderFaceMeshData[i].meshIndex < 0)
                        return;

                    var face = faceData[i];
                    var faceMesh = colliderFaceMeshData[i];

                    var meshData = colliderMeshDataArray[faceMesh.meshIndex];
                    var outputVerts = meshData.GetVertexData<float3>();
                    var outputTris = meshData.GetIndexData<uint>();

                    // add all verts... but mesh colliders don't need normals or UVs
                    for (int x = 0; x < face.vertCount; x++) {
                        var mdi = faceMesh.meshVertStart + x; // mesh data index
                        var n = face.vertIndexStart + x; // global vert buffer index

                        // all Face Datas are still in Quake space; need to convert to Unity space and axes
                        outputVerts[mdi] = allVerts[n].xzy * scalingFactor - meshOrigin;
                    }

                    // verts are already in correct order, add as basic fan pattern (since we know it's a convex face)
                    for (int t = 2; t < face.vertCount; t++) {
                        outputTris[faceMesh.meshTriStart + (t - 2) * 3] = (uint)faceMesh.meshVertStart;
                        outputTris[faceMesh.meshTriStart + (t - 2) * 3 + 1] = (uint)(faceMesh.meshVertStart + t - 1);
                        outputTris[faceMesh.meshTriStart + (t - 2) * 3 + 2] = (uint)(faceMesh.meshVertStart + t);
                    }
                }
            }

#endregion
#region Jobs Completion

            public void CompleteJobsAndGetResults() {
                FinishRendererMeshes();
                if (hasColliderJobs)
                    FinishCollisionMeshes();
                StopTimer("Total");

                // TODO: call OnMeshesDone()
#if SCOPA_MESH_DEBUG
                string timerLog = $"ScopaMeshJobGroup {entityData.ClassName}: {rendererResults.Count} renderers / {colliderResults.Count} colliders";
                foreach (var timerKVP in timers) {
                    timerLog += $"\n{timerKVP.Key} {timerKVP.Value.ElapsedMilliseconds} ms";
                }
                Debug.Log(timerLog);
#endif
            }

            public void FinishRendererMeshes() {
                var meshes = new Mesh[textureNames.Count];
                for (int i = 0; i < meshes.Length; i++) {
                    var newMesh = new Mesh {
                        name = $"{namePrefix}_{textureNames[i]}"
                    };
                    meshes[i] = newMesh;
                }
                finalJobHandle.Complete();
                StopTimer("MeshBuild");

                StartTimer("MeshWrite");
                for (int i = 0; i < meshes.Length; i++) {
                    var meshData = meshDataArray[i];
                    meshData.subMeshCount = 1;
                    meshData.SetSubMesh(0, new SubMeshDescriptor(0, meshCounts[i].triIndexCount), MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
                }
                // finalizing mesh!
                Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, meshes);
                rendererResults = new List<ScopaRendererMeshResult>(meshes.Length);
                for (int i = 0; i < meshes.Length; i++) {
                    var newMesh = meshes[i];

                    if (newMesh == null || newMesh.vertexCount == 0)
                        continue;

                    if (config.addTangents)
                        newMesh.RecalculateTangents();
                    newMesh.RecalculateBounds();

#if UNITY_EDITOR
                    if (config.addLightmapUV2) {
                        UnwrapParam.SetDefaults(out var unwrap);
                        unwrap.packMargin *= 2;
                        Unwrapping.GenerateSecondaryUVSet(newMesh, unwrap);
                    }

                    if (config.meshCompression != ScopaMapConfig.ModelImporterMeshCompression.Off)
                        MeshUtility.SetMeshCompression(newMesh, (ModelImporterMeshCompression)config.meshCompression);
#endif

                    // Debug.Log($"writing {newMesh.name} with {newMesh.vertexCount}");
                    rendererResults.Add(new(newMesh, entityData, textureToMaterial[textureNames[i]]));
                }
                StopTimer("MeshWrite");
            }

            public void FinishCollisionMeshes() {
                var colliderMeshes = new Mesh[solids.Length];
                for (int i = 0; i < colliderMeshes.Length; i++) {
                    colliderMeshes[i] = new Mesh();
                }

                colliderJobHandle.Complete();
                StopTimer("ColliderMeshBuild");

                StartTimer("ColliderMeshWrite");
                for (int i = 0; i < colliderMeshes.Length; i++) {
                    var meshData = colliderMeshDataArray[i];
                    meshData.subMeshCount = 1;
                    meshData.SetSubMesh(0, new SubMeshDescriptor(0, colliderMeshCounts[i].triIndexCount), MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
                }
                colliderResults = new List<ScopaColliderResult>(solids.Length);
                Mesh.ApplyAndDisposeWritableMeshData(colliderMeshDataArray, colliderMeshes);

                for (int i = 0; i < colliderMeshes.Length; i++) { // TODO: colliderNameFormat string
                    Mesh newMesh = colliderMeshes[i];
                    
                    if (newMesh != null && newMesh.vertexCount > 0) {
                        newMesh.name = $"{namePrefix}_Collider{i.ToString("D5", CultureInfo.InvariantCulture)}";
                        newMesh.RecalculateBounds();
#if UNITY_EDITOR
                        if (config.meshCompression != ScopaMapConfig.ModelImporterMeshCompression.Off)
                            MeshUtility.SetMeshCompression(newMesh, (ModelImporterMeshCompression)config.meshCompression);
#endif
                        colliderResults.Add(new(
                            newMesh,
                            entityData,
                            colliderSolidity[i]
                        ));
                    } else if (colliderSolidity[i] != ColliderSolidity.SolidConcaveMegaCollider && colliderSolidity[i] != ColliderSolidity.NoCollider) { // if box collider, we'll just use the mesh bounds to config a collider 
                        colliderResults.Add(new(
                            boxColliderData[i],
                            entityData,
                            colliderSolidity[i]
                        ));
                    }
                }
                StopTimer("ColliderMeshWrite");
            }

            public void DisposeJobsData() {
                allPlaneOffsets.Dispose();
                allPlanes.Dispose();
                allVerts.Dispose();
                faceData.Dispose();
                faceMeshData.Dispose();
                faceUVData.Dispose();
                textureData.Dispose();
                meshCounts.Dispose();

                colliderFaceMeshData.Dispose();
                colliderMeshCounts.Dispose();
                boxColliderData.Dispose();
                colliderSolidity.Dispose();
            }

#endregion
#region Scopa Mesh Debug

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
            
#endregion

        } // end of ScopaMesh class

#region Burst Data Structs

        /// <summary> Burst-compatible data struct for a face's vertex buffer / tri index offsets </summary>
        public struct ScopaFaceData {
            /// <summary> will be 0 until VertCountJob fills it</summary>
            public int vertIndexStart, vertCount;
            public float3 faceCenter;

            public ScopaFaceData(int vertIndexStart, int vertCount, float3 faceCenter) : this() {
                this.vertIndexStart = vertIndexStart;
                this.vertCount = vertCount;
                this.faceCenter = faceCenter;
            }

            public override string ToString() {
                return $"VertStart {vertIndexStart}, VertCount {vertCount}";
            }
        }

        public struct ScopaFaceMeshData {
            /// <summary> will be 0 until MeshCountJob fills it</summary>
            public int meshVertStart, meshTriStart;

            /// <summary> Corresponds to textureName list index / meshDataArray index. 
            /// If negative, this face has been discarded / should be culled. 
            /// Use math.abs() to recover original textureName / meshDataArray index / etc </summary>
            public int meshIndex;

            public ScopaFaceMeshData(int materialIndex, bool isCulled = false) : this() {
                this.meshVertStart = 0;
                this.meshTriStart = 0;
                this.meshIndex = (isCulled ? -1 : 1) * materialIndex;
            }

            public ScopaFaceMeshData(int meshVertStart, int meshTriStart, int materialIndex) : this() {
                this.meshVertStart = meshVertStart;
                this.meshTriStart = meshTriStart;
                this.meshIndex = materialIndex;
            }

            public static implicit operator int3(ScopaFaceMeshData d) => new(d.meshVertStart, d.meshTriStart, d.meshIndex);
            public static implicit operator ScopaFaceMeshData(int3 i) => new(i.x, i.y, i.z);

            public override string ToString() {
                return $"MVertStart {meshVertStart}, MTriStart {meshTriStart}, material {meshIndex}";
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
            // TODO: smoothingAngle

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

        public enum ColliderSolidity { SolidConvex, Trigger, NoCollider, SolidConcaveMegaCollider };

        /// <summary> Burst-compatible data struct for generating a Box Collider </summary>
        public struct ScopaBoxColliderData {
            public float3 position, eulerAngles, size;

            public ScopaBoxColliderData(float3 position, float3 eulerAngles, float3 size) : this() {
                this.position = position;
                this.eulerAngles = eulerAngles;
                this.size = size;
            }

            public static implicit operator float3x3(ScopaBoxColliderData d) => new(d.position, d.eulerAngles, d.size);
            public static implicit operator ScopaBoxColliderData(float3x3 i) => new(i.c0, i.c1, i.c2);
        }

#endregion


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