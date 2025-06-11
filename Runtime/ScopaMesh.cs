using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Sledge.Formats.Map.Formats;
using Sledge.Formats.Map.Objects;
using Sledge.Formats.Precision;
using Scopa.Formats.Texture.Wad;
using Scopa.Formats.Id;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Rendering;
using Mesh = UnityEngine.Mesh;
using Vector3 = UnityEngine.Vector3;

#if SCOPA_USE_BURST
using Unity.Burst;
using Unity.Mathematics;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Scopa {
    /// <summary>main class for Scopa mesh generation / geo functions</summary>
    public static class ScopaMesh {
        // to avoid GC, we use big static lists so we just allocate once
        // TODO: will have to reorganize this for multithreading later on
        static List<Face> allFaces = new List<Face>(8192);
        static HashSet<Face> discardedFaces = new HashSet<Face>(8192);

        static List<Vector3> verts = new List<Vector3>(4096);
        static List<Vector3> faceVerts = new List<Vector3>(64);
        static List<int> tris = new List<int>(8192);
        static List<int> faceTris = new List<int>(32);
        static List<Vector2> uvs = new List<Vector2>(4096);
        static List<Vector2> faceUVs = new List<Vector2>(64);

        const float EPSILON = 0.01f;


        public static void AddFaceForCulling(Face brushFace) {
            allFaces.Add(brushFace);
        }

        public static void ClearFaceCullingList() {
            allFaces.Clear();
            discardedFaces.Clear();
        }

        public static void DiscardFace(Face brushFace) {
            discardedFaces.Add(brushFace);
        }

        public static bool IsFaceCulledDiscard(Face brushFace) {
            return discardedFaces.Contains(brushFace);
        }
        
        public static FaceCullingJobGroup StartFaceCullingJobs() {
            return new FaceCullingJobGroup();
        }

        public class FaceCullingJobGroup {
            NativeArray<int> cullingOffsets;
            NativeArray<Vector4> cullingPlanes;
            NativeArray<Vector3> cullingVerts;
            NativeArray<bool> cullingResults;
            JobHandle jobHandle;

            public FaceCullingJobGroup() {
                var vertCount = 0;
                cullingOffsets = new NativeArray<int>(allFaces.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                cullingPlanes = new NativeArray<Vector4>(allFaces.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                for(int i=0; i<allFaces.Count; i++) {
                    cullingOffsets[i] = vertCount;
                    vertCount += allFaces[i].Vertices.Count;
                    cullingPlanes[i] = new Vector4(allFaces[i].Plane.Normal.X, allFaces[i].Plane.Normal.Y, allFaces[i].Plane.Normal.Z, allFaces[i].Plane.D);
                }

                cullingVerts = new NativeArray<Vector3>(vertCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                for(int i=0; i<allFaces.Count; i++) {
                    for(int v=cullingOffsets[i]; v < (i<cullingOffsets.Length-1 ? cullingOffsets[i+1] : vertCount); v++) {
                        cullingVerts[v] = allFaces[i].Vertices[v-cullingOffsets[i]].ToUnity();
                    }
                }
                
                cullingResults = new NativeArray<bool>(allFaces.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                for(int i=0; i<allFaces.Count; i++) {
                    cullingResults[i] = IsFaceCulledDiscard(allFaces[i]);
                }
                
                var jobData = new FaceCullingJob();

                #if SCOPA_USE_BURST
                jobData.faceVertices = cullingVerts.Reinterpret<float3>();
                jobData.facePlanes = cullingPlanes.Reinterpret<float4>();
                #else
                jobData.faceVertices = cullingVerts;
                jobData.facePlanes = cullingPlanes;
                #endif

                jobData.faceVertexOffsets = cullingOffsets;
                jobData.cullFaceResults = cullingResults;
                jobHandle = jobData.Schedule(cullingResults.Length, 32);
            }

            public void Complete() {
                jobHandle.Complete();

                // int culledFaces = 0;
                for(int i=0; i<cullingResults.Length; i++) {
                    // if (!allFaces[i].discardWhenBuildingMesh && cullingResults[i])
                    //     culledFaces++;
                    if(cullingResults[i])
                        discardedFaces.Add(allFaces[i]);
                }
                // Debug.Log($"Culled {culledFaces} faces!");

                cullingOffsets.Dispose();
                cullingVerts.Dispose();
                cullingPlanes.Dispose();
                cullingResults.Dispose();
            }

        }

        #if SCOPA_USE_BURST
        [BurstCompile]
        #endif
        public struct FaceCullingJob : IJobParallelFor
        {
            [ReadOnlyAttribute]
            #if SCOPA_USE_BURST
            public NativeArray<float3> faceVertices;
            #else
            public NativeArray<Vector3> faceVertices;
            #endif   

            [ReadOnlyAttribute]
            #if SCOPA_USE_BURST
            public NativeArray<float4> facePlanes;
            #else
            public NativeArray<Vector4> facePlanes;
            #endif

            [ReadOnlyAttribute]
            public NativeArray<int> faceVertexOffsets;
            
            public NativeArray<bool> cullFaceResults;

            public void Execute(int i)
            {
                if (cullFaceResults[i])
                    return;

                // test against all other faces
                for(int n=0; n<faceVertexOffsets.Length; n++) {
                    // first, test (1) share similar plane distance and (2) face opposite directions
                    // we are testing the NEGATIVE case for early out
                    #if SCOPA_USE_BURST
                    if ( math.abs(facePlanes[i].w + facePlanes[n].w) > 0.5f || math.dot(facePlanes[i].xyz, facePlanes[n].xyz) > -0.999f )
                        continue;
                    #else
                    if ( Mathf.Abs(facePlanes[i].w + facePlanes[n].w) > 0.5f || Vector3.Dot(facePlanes[i], facePlanes[n]) > -0.999f )
                        continue;
                    #endif
                    
                    // then, test whether this face's vertices are completely inside the other
                    var offsetStart = faceVertexOffsets[i];
                    var offsetEnd = i<faceVertexOffsets.Length-1 ? faceVertexOffsets[i+1] : faceVertices.Length;

                    var Center = faceVertices[offsetStart];
                    for( int b=offsetStart+1; b<offsetEnd; b++) {
                        Center += faceVertices[b];
                    }
                    Center /= offsetEnd-offsetStart;

                    // 2D math is easier, so let's ignore the least important axis
                    var ignoreAxis = GetMainAxisToNormal(facePlanes[i]);

                    var otherOffsetStart = faceVertexOffsets[n];
                    var otherOffsetEnd = n<faceVertexOffsets.Length-1 ? faceVertexOffsets[n+1] : faceVertices.Length;

                    #if SCOPA_USE_BURST
                    var polygon = new NativeArray<float3>(otherOffsetEnd-otherOffsetStart, Allocator.Temp);
                    NativeArray<float3>.Copy(faceVertices, otherOffsetStart, polygon, 0, polygon.Length);
                    #else
                    var polygon = new Vector3[otherOffsetEnd-otherOffsetStart];
                    NativeArray<Vector3>.Copy(faceVertices, otherOffsetStart, polygon, 0, polygon.Length);
                    #endif

                    var vertNotInOtherFace = false;
                    for( int x=offsetStart; x<offsetEnd; x++ ) {
                        #if SCOPA_USE_BURST
                        var p = faceVertices[x] + math.normalize(Center - faceVertices[x]) * 0.2f;
                        #else
                        var p = faceVertices[x] + Vector3.Normalize(Center - faceVertices[x]) * 0.2f;
                        #endif                  
                        switch (ignoreAxis) {
                            case Axis.X: if (!IsInPolygonYZ(p, polygon)) vertNotInOtherFace = true; break;
                            case Axis.Y: if (!IsInPolygonXZ(p, polygon)) vertNotInOtherFace = true; break;
                            case Axis.Z: if (!IsInPolygonXY(p, polygon)) vertNotInOtherFace = true; break;
                        }

                        if (vertNotInOtherFace)
                            break;
                    }

                    #if SCOPA_USE_BURST
                    polygon.Dispose();
                    #endif

                    if (vertNotInOtherFace)
                        continue;

                    // if we got this far, then this face should be culled
                    var tempResult = true;
                    cullFaceResults[i] = tempResult;
                    return;
                }
               
            }
        }

        public enum Axis { X, Y, Z}

        #if SCOPA_USE_BURST
        public static Axis GetMainAxisToNormal(float4 vec) {
            // VHE prioritises the axes in order of X, Y, Z.
            // so in Unity land, that's X, Z, and Y
            var norm = new float3(
                math.abs(vec.x), 
                math.abs(vec.y),
                math.abs(vec.z)
            );

            if (norm.x >= norm.y && norm.x >= norm.z) return Axis.X;
            if (norm.z >= norm.y) return Axis.Z;
            return Axis.Y;
        }

        public static bool IsInPolygonXY(float3 p, NativeArray<float3> polygon ) {
            // https://wrf.ecse.rpi.edu/Research/Short_Notes/pnpoly.html
            bool inside = false;
            for ( int i = 0, j = polygon.Length - 1 ; i < polygon.Length ; j = i++ ) {
                if ( ( polygon[i].y > p.y ) != ( polygon[j].y > p.y ) &&
                    p.x < ( polygon[j].x - polygon[i].x ) * ( p.y - polygon[i].y ) / ( polygon[j].y - polygon[i].y ) + polygon[i].x )
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        public static bool IsInPolygonYZ(float3 p, NativeArray<float3> polygon ) {
            // https://wrf.ecse.rpi.edu/Research/Short_Notes/pnpoly.html
            bool inside = false;
            for ( int i = 0, j = polygon.Length - 1 ; i < polygon.Length ; j = i++ ) {
                if ( ( polygon[i].y > p.y ) != ( polygon[j].y > p.y ) &&
                    p.z < ( polygon[j].z - polygon[i].z ) * ( p.y - polygon[i].y ) / ( polygon[j].y - polygon[i].y ) + polygon[i].z )
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        public static bool IsInPolygonXZ(float3 p, NativeArray<float3> polygon ) {
            // https://wrf.ecse.rpi.edu/Research/Short_Notes/pnpoly.html
            bool inside = false;
            for ( int i = 0, j = polygon.Length - 1 ; i < polygon.Length ; j = i++ ) {
                if ( ( polygon[i].x > p.x ) != ( polygon[j].x > p.x ) &&
                    p.z < ( polygon[j].z - polygon[i].z ) * ( p.x - polygon[i].x ) / ( polygon[j].x - polygon[i].x ) + polygon[i].z )
                {
                    inside = !inside;
                }
            }

            return inside;
        }
        #endif

        public static Axis GetMainAxisToNormal(Vector3 norm) {
            // VHE prioritises the axes in order of X, Y, Z.
            // so in Unity land, that's X, Z, and Y
            norm = norm.Absolute();

            if (norm.x >= norm.y && norm.x >= norm.z) return Axis.X;
            if (norm.z >= norm.y) return Axis.Z;
            return Axis.Y;
        }

        public static bool IsInPolygonXY( Vector3 p, Vector3[] polygon ) {
            // https://wrf.ecse.rpi.edu/Research/Short_Notes/pnpoly.html
            bool inside = false;
            for ( int i = 0, j = polygon.Length - 1 ; i < polygon.Length ; j = i++ ) {
                if ( ( polygon[i].y > p.y ) != ( polygon[j].y > p.y ) &&
                    p.x < ( polygon[j].x - polygon[i].x ) * ( p.y - polygon[i].y ) / ( polygon[j].y - polygon[i].y ) + polygon[i].x )
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        public static bool IsInPolygonYZ( Vector3 p, Vector3[] polygon ) {
            // https://wrf.ecse.rpi.edu/Research/Short_Notes/pnpoly.html
            bool inside = false;
            for ( int i = 0, j = polygon.Length - 1 ; i < polygon.Length ; j = i++ ) {
                if ( ( polygon[i].y > p.y ) != ( polygon[j].y > p.y ) &&
                    p.z < ( polygon[j].z - polygon[i].z ) * ( p.y - polygon[i].y ) / ( polygon[j].y - polygon[i].y ) + polygon[i].z )
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        public static bool IsInPolygonXZ( Vector3 p, Vector3[] polygon ) {
            // https://wrf.ecse.rpi.edu/Research/Short_Notes/pnpoly.html
            bool inside = false;
            for ( int i = 0, j = polygon.Length - 1 ; i < polygon.Length ; j = i++ ) {
                if ( ( polygon[i].x > p.x ) != ( polygon[j].x > p.x ) &&
                    p.z < ( polygon[j].z - polygon[i].z ) * ( p.x - polygon[i].x ) / ( polygon[j].x - polygon[i].x ) + polygon[i].z )
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        public class MeshBuildingJobGroup {

            NativeArray<int> faceVertexOffsets, faceTriIndexCounts; // index = i
            NativeArray<Vector3> faceVertices;
            NativeArray<Vector4> faceU, faceV; // index = i, .w = scale
            NativeArray<Vector2> faceShift, faceUVoverride; // index = i
            int vertCount, triIndexCount;

            public Mesh.MeshDataArray outputMesh;
            Mesh newMesh;
            JobHandle jobHandle;
            ScopaMapConfig config;

            public MeshBuildingJobGroup(string meshName, Vector3 meshOrigin, IEnumerable<Solid> solids, ScopaMapConfig config, ScopaMapConfig.MaterialOverride materialOverride = null, bool includeDiscardedFaces = false) {       
                this.config = config;
                var faceList = new List<Face>();
                foreach( var solid in solids) {
                    foreach(var face in solid.Faces) {
                        // if ( face.Vertices == null || face.Vertices.Count == 0) // this shouldn't happen though
                        //     continue;

                        if ( !includeDiscardedFaces && IsFaceCulledDiscard(face) )
                            continue;

                        // face.TextureName doesn't need to be ToLowerInvariant'd anymore, ScopaCore already did it earlier
                        if ( materialOverride != null && materialOverride.GetTextureName().GetHashCode() != face.TextureName.GetHashCode() )
                            continue;

                        faceList.Add(face);
                    }
                }

                faceVertexOffsets = new NativeArray<int>(faceList.Count+1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                faceTriIndexCounts = new NativeArray<int>(faceList.Count+1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                for(int i=0; i<faceList.Count; i++) {
                    faceVertexOffsets[i] = vertCount;
                    vertCount += faceList[i].Vertices.Count;
                    faceTriIndexCounts[i] = triIndexCount;
                    triIndexCount += (faceList[i].Vertices.Count-2)*3;
                }
                faceVertexOffsets[faceList.Count] = vertCount;
                faceTriIndexCounts[faceList.Count] = triIndexCount;

                faceVertices = new NativeArray<Vector3>(vertCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                faceU = new NativeArray<Vector4>(faceList.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                faceV = new NativeArray<Vector4>(faceList.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                faceShift = new NativeArray<Vector2>(faceList.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                faceUVoverride = new NativeArray<Vector2>(vertCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                for(int i=0; i<faceList.Count; i++) {
                    if (materialOverride != null 
                        && materialOverride.materialConfig != null
                        && materialOverride.materialConfig.useOnBuildBrushFace 
                        && materialOverride.materialConfig.OnBuildBrushFace(faceList[i], config, out var overrideUVs)
                    ) {
                        for(int u=0; u<overrideUVs.Length; u++) {
                            faceUVoverride[faceVertexOffsets[i]+u] = overrideUVs[u];
                        }
                    } else {
                        for(int u=faceVertexOffsets[i]; u<faceVertexOffsets[i+1]; u++) { // fill with dummy values to ignore in the Job later
                            faceUVoverride[u] = new Vector2(MeshBuildingJob.IGNORE_UV, MeshBuildingJob.IGNORE_UV);
                        }
                    }

                    for(int v=faceVertexOffsets[i]; v < faceVertexOffsets[i+1]; v++) {
                        faceVertices[v] = faceList[i].Vertices[v-faceVertexOffsets[i]].ToUnity();
                    }
                    faceU[i] = new Vector4(faceList[i].UAxis.X, faceList[i].UAxis.Y, faceList[i].UAxis.Z, faceList[i].XScale);
                    faceV[i] = new Vector4(faceList[i].VAxis.X, faceList[i].VAxis.Y, faceList[i].VAxis.Z, faceList[i].YScale);
                    faceShift[i] = new Vector2(faceList[i].XShift, faceList[i].YShift);
                }

                outputMesh = Mesh.AllocateWritableMeshData(1);
                var meshData = outputMesh[0];
                meshData.SetVertexBufferParams(vertCount,
                    new VertexAttributeDescriptor(VertexAttribute.Position),
                    new VertexAttributeDescriptor(VertexAttribute.Normal, stream:1),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord0, dimension:2, stream:2)
                );
                meshData.SetIndexBufferParams(triIndexCount, IndexFormat.UInt32);
                 
                var jobData = new MeshBuildingJob();
                jobData.faceVertexOffsets = faceVertexOffsets;
                jobData.faceTriIndexCounts = faceTriIndexCounts;

                #if SCOPA_USE_BURST
                jobData.faceVertices = faceVertices.Reinterpret<float3>();
                jobData.faceU = faceU.Reinterpret<float4>();
                jobData.faceV = faceV.Reinterpret<float4>();
                jobData.faceShift = faceShift.Reinterpret<float2>();
                jobData.uvOverride = faceUVoverride.Reinterpret<float2>();
                #else
                jobData.faceVertices = faceVertices;
                jobData.faceU = faceU;
                jobData.faceV = faceV;
                jobData.faceShift = faceShift;
                jobData.uvOverride = faceUVoverride;
                #endif

                jobData.meshData = outputMesh[0];
                jobData.scalingFactor = config.scalingFactor;
                jobData.globalTexelScale = config.globalTexelScale;
                jobData.textureWidth = materialOverride?.material?.mainTexture != null ? materialOverride.material.mainTexture.width : config.defaultTexSize;
                jobData.textureHeight = materialOverride?.material?.mainTexture != null ? materialOverride.material.mainTexture.height : config.defaultTexSize;
                jobData.meshOrigin = meshOrigin;
                jobHandle = jobData.Schedule(faceList.Count, 128);

                newMesh = new Mesh();
                newMesh.name = meshName;
            }

            public Mesh Complete() {
                jobHandle.Complete();

                var meshData = outputMesh[0];
                meshData.subMeshCount = 1;
                meshData.SetSubMesh(0, new SubMeshDescriptor(0, triIndexCount), MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);

                Mesh.ApplyAndDisposeWritableMeshData(outputMesh, newMesh);
                newMesh.RecalculateNormals();
                newMesh.RecalculateBounds();

                if (config.addTangents) {
                    newMesh.RecalculateTangents();
                }

                #if UNITY_EDITOR
                if ( config.addLightmapUV2 ) {
                    UnwrapParam.SetDefaults( out var unwrap);
                    unwrap.packMargin *= 2;
                    Unwrapping.GenerateSecondaryUVSet( newMesh, unwrap );
                }

                if ( config.meshCompression != ScopaMapConfig.ModelImporterMeshCompression.Off)
                    UnityEditor.MeshUtility.SetMeshCompression(newMesh, (ModelImporterMeshCompression)config.meshCompression);
                #endif

                faceVertexOffsets.Dispose();
                faceTriIndexCounts.Dispose();
                faceVertices.Dispose();
                faceUVoverride.Dispose();
                faceU.Dispose();
                faceV.Dispose();
                faceShift.Dispose();

                // If optimize everything, just combine the two optimizations into one call
                if ((config.optimizeMesh & ScopaMapConfig.ModelImporterMeshOptimization.OptimizeIndexBuffers) != 0 &&
                    (config.optimizeMesh & ScopaMapConfig.ModelImporterMeshOptimization.OptimizeVertexBuffers) != 0)
                {
                    newMesh.Optimize();
                }
                else
                {
                    // Optimize index buffers
                    if ((config.optimizeMesh & ScopaMapConfig.ModelImporterMeshOptimization.OptimizeIndexBuffers) != 0)
                    {
                        newMesh.OptimizeIndexBuffers();
                    }
                    
                    // Optimize vertex buffers
                    if ((config.optimizeMesh & ScopaMapConfig.ModelImporterMeshOptimization.OptimizeVertexBuffers) != 0)
                    {
                        newMesh.OptimizeReorderVertexBuffer();
                    }
                }

                return newMesh;
            }

        }

        #if SCOPA_USE_BURST
        [BurstCompile]
        #endif
        public struct MeshBuildingJob : IJobParallelFor
        {
            [ReadOnlyAttribute] public NativeArray<int> faceVertexOffsets, faceTriIndexCounts; // index = i

            #if SCOPA_USE_BURST
            [ReadOnlyAttribute] public NativeArray<float3> faceVertices;
            [ReadOnlyAttribute] public NativeArray<float4> faceU, faceV; // index = i, .w = scale
            [ReadOnlyAttribute] public NativeArray<float2> faceShift, uvOverride; // index = i
            [ReadOnlyAttribute] public float3 meshOrigin;
            #else            
            [ReadOnlyAttribute] public NativeArray<Vector3> faceVertices;
            [ReadOnlyAttribute] public NativeArray<Vector4> faceU, faceV; // index = i, .w = scale
            [ReadOnlyAttribute] public NativeArray<Vector2> faceShift, uvOverride; // index = i
            [ReadOnlyAttribute] public Vector3 meshOrigin;
            #endif
            
            [NativeDisableParallelForRestriction]
            public Mesh.MeshData meshData;

            [ReadOnlyAttribute] public float scalingFactor, globalTexelScale, textureWidth, textureHeight;
            public const float IGNORE_UV = -99999f;

            #if SCOPA_USE_BURST
            // will need this to support non-Valve formats
            public static float2 Rotate(float2 v, float deltaRadians) {
                return new float2(
                    v.x * math.cos(deltaRadians) - v.y * math.sin(deltaRadians),
                    v.x * math.sin(deltaRadians) + v.y * math.cos(deltaRadians)
            );
            #else
            public static Vector2 Rotate(Vector2 v, float deltaRadians) {
                return new Vector2(
                    v.x * Mathf.Cos(deltaRadians) - v.y * Mathf.Sin(deltaRadians),
                    v.x * Mathf.Sin(deltaRadians) + v.y * Mathf.Cos(deltaRadians)
            );
            #endif
}

            public void Execute(int i)
            {
                var offsetStart = faceVertexOffsets[i];
                var offsetEnd = faceVertexOffsets[i+1];

                #if SCOPA_USE_BURST
                var outputVerts = meshData.GetVertexData<float3>();
                var outputUVs = meshData.GetVertexData<float2>(2);
                #else
                var outputVerts = meshData.GetVertexData<Vector3>();
                var outputUVs = meshData.GetVertexData<Vector2>(2);
                #endif

                var outputTris = meshData.GetIndexData<int>();

                // add all verts, normals, and UVs
                for( int n=offsetStart; n<offsetEnd; n++ ) {
                    outputVerts[n] = faceVertices[n] * scalingFactor - meshOrigin;

                    if (uvOverride[n].x > IGNORE_UV) {
                        outputUVs[n] = uvOverride[n];
                    } else {
                        #if SCOPA_USE_BURST
                        outputUVs[n] = new float2(
                            (math.dot(faceVertices[n], faceU[i].xyz / faceU[i].w) + faceShift[i].x) / textureWidth,
                            (math.dot(faceVertices[n], faceV[i].xyz / faceV[i].w) - faceShift[i].y) / textureHeight
                        ) * globalTexelScale;
                        #else
                        outputUVs[n] = new Vector2(
                            (Vector3.Dot(faceVertices[n], faceU[i] / faceU[i].w) + faceShift[i].x) / textureWidth,
                            (Vector3.Dot(faceVertices[n], faceV[i] / faceV[i].w) - faceShift[i].y) / textureHeight
                        ) * globalTexelScale;
                        #endif
                    }
                }

                // verts are already in correct order, add as basic fan pattern (since we know it's a convex face)
                for(int t=2; t<offsetEnd-offsetStart; t++) {
                    outputTris[faceTriIndexCounts[i]+(t-2)*3] = offsetStart;
                    outputTris[faceTriIndexCounts[i]+(t-2)*3+1] = offsetStart + t-1;
                    outputTris[faceTriIndexCounts[i]+(t-2)*3+2] = offsetStart + t;
                }
            }
        }

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
                foreach( var solid in solids) {
                    if (mergedEntityData.ContainsKey(solid) && (config.IsEntityNonsolid(mergedEntityData[solid].ClassName) || config.IsEntityTrigger(mergedEntityData[solid].ClassName)) )
                        continue;

                    foreach(var face in solid.Faces) {
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
                solidFaceOffsets.CopyFrom( solidFaceOffsetsManaged.ToArray() );
                canBeBoxCollider = new NativeArray<bool>(solidCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                faceVertexOffsets = new NativeArray<int>(faceList.Count+1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                faceTriIndexCounts = new NativeArray<int>(faceList.Count+1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                for(int i=0; i<faceList.Count; i++) {
                    faceVertexOffsets[i] = vertCount;
                    vertCount += faceList[i].Vertices.Count;
                    faceTriIndexCounts[i] = triIndexCount;
                    triIndexCount += (faceList[i].Vertices.Count-2)*3;
                }
                faceVertexOffsets[faceVertexOffsets.Length-1] = vertCount;
                faceTriIndexCounts[faceTriIndexCounts.Length-1] = triIndexCount;

                faceVertices = new NativeArray<Vector3>(vertCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                facePlaneNormals = new NativeArray<Vector3>(faceList.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                for(int i=0; i<faceList.Count; i++) {
                    for(int v=faceVertexOffsets[i]; v < faceVertexOffsets[i+1]; v++) {
                        faceVertices[v] = faceList[i].Vertices[v-faceVertexOffsets[i]].ToUnity();
                    }
                    facePlaneNormals[i] = faceList[i].Plane.Normal.ToUnity();
                }

                outputMesh = Mesh.AllocateWritableMeshData(solidCount);
                meshes = new Mesh[solidCount];
                for(int i=0; i<solidCount; i++) {
                    meshes[i] = new Mesh();
                    meshes[i].name = string.Format(colliderNameFormat, i.ToString("D5", System.Globalization.CultureInfo.InvariantCulture));

                    var solidOffsetStart = solidFaceOffsets[i];
                    var solidOffsetEnd = solidFaceOffsets[i+1];
                    var finalVertCount = faceVertexOffsets[solidOffsetEnd] - faceVertexOffsets[solidOffsetStart];
                    var finalTriCount = faceTriIndexCounts[solidOffsetEnd] - faceTriIndexCounts[solidOffsetStart];

                    var meshData = outputMesh[i];
                    meshData.SetVertexBufferParams(finalVertCount,
                        new VertexAttributeDescriptor(VertexAttribute.Position)
                    );
                    meshData.SetIndexBufferParams(finalTriCount, IndexFormat.UInt32);
                }
                
                var jobData = new ColliderJob();
                jobData.faceVertexOffsets = faceVertexOffsets;
                jobData.faceTriIndexCounts = faceTriIndexCounts;
                jobData.solidFaceOffsets = solidFaceOffsets;

                #if SCOPA_USE_BURST
                jobData.faceVertices = faceVertices.Reinterpret<float3>();
                jobData.facePlaneNormals = facePlaneNormals.Reinterpret<float3>();
                #else
                jobData.faceVertices = faceVertices;
                jobData.facePlaneNormals = facePlaneNormals;
                #endif

                jobData.meshDataArray = outputMesh;
                jobData.canBeBoxColliderResults = canBeBoxCollider;
                jobData.colliderMode = config.colliderMode;
                jobData.scalingFactor = config.scalingFactor;
                jobData.meshOrigin = gameObject.transform.position;
                jobHandle = jobData.Schedule(solidCount, 64);
            }

            public Mesh[] Complete() {
                jobHandle.Complete();

                Mesh.ApplyAndDisposeWritableMeshData(outputMesh, meshes);
                for (int i = 0; i < meshes.Length; i++) {
                    Mesh newMesh = meshes[i];
                    var newGO = new GameObject(newMesh.name);
                    newGO.transform.SetParent( gameObject.transform );
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
            public struct ColliderJob : IJobParallelFor
            {
                [ReadOnlyAttribute] public NativeArray<int> faceVertexOffsets, faceTriIndexCounts, solidFaceOffsets; // index = i

                #if SCOPA_USE_BURST
                [ReadOnlyAttribute] public NativeArray<float3> faceVertices, facePlaneNormals;
                [ReadOnlyAttribute] public float3 meshOrigin;
                #else            
                [ReadOnlyAttribute] public NativeArray<Vector3> faceVertices, facePlaneNormals;
                [ReadOnlyAttribute] public Vector3 meshOrigin;
                #endif
                
                public Mesh.MeshDataArray meshDataArray;
                [WriteOnly] public NativeArray<bool> canBeBoxColliderResults;

                [ReadOnlyAttribute] public float scalingFactor;
                [ReadOnlyAttribute] public ScopaMapConfig.ColliderImportMode colliderMode;

                public void Execute(int i)
                {
                    var solidOffsetStart = solidFaceOffsets[i];
                    var solidOffsetEnd = solidFaceOffsets[i+1];

                    var solidVertStart = faceVertexOffsets[solidOffsetStart];
                    var finalTriIndexCount = faceTriIndexCounts[solidOffsetEnd] - faceTriIndexCounts[solidOffsetStart];

                    var meshData = meshDataArray[i];

                    #if SCOPA_USE_BURST
                    var outputVerts = meshData.GetVertexData<float3>();
                    #else
                    var outputVerts = meshData.GetVertexData<Vector3>();
                    #endif

                    var outputTris = meshData.GetIndexData<int>();

                    // for each solid, gather faces...
                    var canBeBoxCollider = colliderMode == ScopaMapConfig.ColliderImportMode.BoxAndConvex || colliderMode == ScopaMapConfig.ColliderImportMode.BoxColliderOnly;
                    for(int face=solidOffsetStart; face<solidOffsetEnd; face++) {
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
                        var vertOffsetEnd = faceVertexOffsets[face+1];
                        for( int n=vertOffsetStart; n<vertOffsetEnd; n++ ) {
                            outputVerts[n-solidVertStart] = faceVertices[n] * scalingFactor - meshOrigin;
                        }

                        // verts are already in correct order, add as basic fan pattern (since we know it's a convex face)
                        var triIndexStart = faceTriIndexCounts[face] - faceTriIndexCounts[solidOffsetStart];
                        var faceVertStart = vertOffsetStart-solidVertStart;
                        for(int t=2; t<vertOffsetEnd-vertOffsetStart; t++) {
                            outputTris[triIndexStart+(t-2)*3] = faceVertStart;
                            outputTris[triIndexStart+(t-2)*3+1] = faceVertStart + t-1;
                            outputTris[triIndexStart+(t-2)*3+2] = faceVertStart + t;
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

        public static void WeldVertices(this Mesh aMesh, float aMaxDelta = 0.1f, float maxAngle = 180f)
        {
            var verts = aMesh.vertices;
            var normals = aMesh.normals;
            var uvs = aMesh.uv;
            List<int> newVerts = new List<int>();
            int[] map = new int[verts.Length];
            // create mapping and filter duplicates.
            for (int i = 0; i < verts.Length; i++)
            {
                var p = verts[i];
                var n = normals[i];
                var uv = uvs[i];
                bool duplicate = false;
                for (int i2 = 0; i2 < newVerts.Count; i2++)
                {
                    int a = newVerts[i2];
                    if (
                        (verts[a] - p).sqrMagnitude <= aMaxDelta // compare position
                        && Vector3.Angle(normals[a], n) <= maxAngle // compare normal
                        // && (uvs[a] - uv).sqrMagnitude <= aMaxDelta // compare first uv coordinate
                        )
                    {
                        map[i] = i2;
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate)
                {
                    map[i] = newVerts.Count;
                    newVerts.Add(i);
                }
            }
            // create new vertices
            var verts2 = new Vector3[newVerts.Count];
            var normals2 = new Vector3[newVerts.Count];
            var uvs2 = new Vector2[newVerts.Count];
            for (int i = 0; i < newVerts.Count; i++)
            {
                int a = newVerts[i];
                verts2[i] = verts[a];
                normals2[i] = normals[a];
                uvs2[i] = uvs[a];
            }
            // map the triangle to the new vertices
            var tris = aMesh.triangles;
            for (int i = 0; i < tris.Length; i++)
            {
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

            #if SCOPA_USE_BURST
            jobData.verts = verts.Reinterpret<float3>();
            jobData.normals = normals.Reinterpret<float3>();
            jobData.results = smoothNormalsResults.Reinterpret<float3>();
            #else
            jobData.verts = verts;
            jobData.normals = normals;
            jobData.results = smoothNormalsResults;
            #endif

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
        public struct SmoothJob : IJobParallelFor
        {
            #if SCOPA_USE_BURST
            [ReadOnlyAttribute] public NativeArray<float3> verts, normals;
            public NativeArray<float3> results;
            #else
            [ReadOnlyAttribute] public NativeArray<Vector3> verts, normals;
            public NativeArray<Vector3> results;
            #endif

            public float cos, maxDelta;

            public void Execute(int i)
            {
                var tempResult = normals[i];
                var resultCount = 1;
                
                for(int i2 = 0; i2 < verts.Length; i2++) {
                    #if SCOPA_USE_BURST
                    if ( math.lengthsq(verts[i2] - verts[i] ) <= maxDelta && math.dot(normals[i2], normals[i] ) >= cos ) 
                    #else
                    if ( (verts[i2] - verts[i] ).sqrMagnitude <= maxDelta && Vector3.Dot(normals[i2], normals[i] ) >= cos ) 
                    #endif
                    {
                        tempResult += normals[i2];
                        resultCount++;
                    }
                }

                if (resultCount > 1)
                #if SCOPA_USE_BURST
                    tempResult = math.normalize(tempResult / resultCount);
                #else
                    tempResult = (tempResult / resultCount).normalized;
                #endif
                results[i] = tempResult;
            }
        }

        public static void SnapBrushVertices(Solid sledgeSolid, float snappingDistance = 4f) {
            // snap nearby vertices together within in each solid -- but always snap to the FURTHEST vertex from the center
            var origin = new System.Numerics.Vector3();
            var vertexCount = 0;
            foreach(var face in sledgeSolid.Faces) {
                for(int i=0; i<face.Vertices.Count; i++) {
                    origin += face.Vertices[i];
                }
                vertexCount += face.Vertices.Count;
            }
            origin /= vertexCount;

            foreach(var face1 in sledgeSolid.Faces) {
                foreach (var face2 in sledgeSolid.Faces) {
                    if ( face1 == face2 )
                        continue;

                    for(int a=0; a<face1.Vertices.Count; a++) {
                        for(int b=0; b<face2.Vertices.Count; b++ ) {
                            if ( (face1.Vertices[a] - face2.Vertices[b]).LengthSquared() < snappingDistance * snappingDistance ) {
                                if ( (face1.Vertices[a] - origin).LengthSquared() > (face2.Vertices[b] - origin).LengthSquared() )
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