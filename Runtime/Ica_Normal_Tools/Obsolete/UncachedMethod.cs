using Ica.Normal.JobStructs;
using Ica.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Assertions;

namespace Ica.Normal
{
    [BurstCompile]
    public static class UncachedMethod
    {
        [BurstCompile]
        //This is Buggy
        public static void UncachedNormalRecalculate
        (
            in Mesh.MeshData meshData,
            out NativeList<float3> outNormals,
            Allocator allocator,
            float angle = 180f
        )
        {
            Assert.IsFalse(allocator == Allocator.Temp);

            angle = math.clamp(angle, 0, 180);

            var vertices = new NativeList<float3>(meshData.vertexCount, Allocator.Temp);
            meshData.GetVerticesDataAsList(ref vertices);

            var indices = new NativeList<int>(meshData.vertexCount, Allocator.Temp);
            meshData.GetAllIndicesDataAsList(ref indices);

            outNormals = new NativeList<float3>(meshData.vertexCount, allocator);
            outNormals.Resize(meshData.vertexCount, NativeArrayOptions.ClearMemory);

            var triNormals = new NativeArray<float3>(indices.Length / 3, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var triNormalJob = new TriangleNormalJob
            {
                TriNormals = triNormals,
                Vertices = vertices.AsArray(),
                Indices = indices.AsArray(),
            };

            var triNormalJobHandle = triNormalJob.ScheduleParallel(indices.Length / 3, JobUtils.GetBatchCountThatMakesSense(indices.Length / 3), default);

            //smooth version runs faster
            if (angle == 180f)
            {
                var vertexNormalJob = new UncachedSmoothVertexNormalJob()
                {
                    OutNormals = outNormals.AsArray(),
                    Vertices = vertices.AsArray(),
                    Indices = indices.AsArray(),
                    TriNormals = triNormals,
                    pmGetVertexPosHashMap = new ProfilerMarker("pPosMap"),
                    pmCalculate = new ProfilerMarker("pCalculate"),
                };
                var handle = vertexNormalJob.Schedule(triNormalJobHandle);
                handle.Complete();
            }
            else
            {
                var vertexNormalJob = new UncachedAngleVertexNormalJob()
                {
                    OutNormals = outNormals.AsArray(),
                    Vertices = vertices.AsArray(),
                    Indices = indices.AsArray(),
                    TriNormals = triNormals,
                    CosineThreshold = Mathf.Cos(angle * Mathf.Deg2Rad),
                    PGetVertexPosHashMap = new ProfilerMarker("pPosMap"),
                    PCalculate = new ProfilerMarker("pCalculate"),
                };
                var handle = vertexNormalJob.Schedule(triNormalJobHandle);
                handle.Complete();
            }

            triNormals.Dispose();
        }


        [BurstCompile]
        public static void UncachedNormalAndTangentRecalculate
        (
            in Mesh.MeshData meshData,
            out NativeList<float3> outNormals,
            out NativeList<float4> outTangents,
            Allocator allocator,
            float angle = 180f
        )
        {
            Assert.IsFalse(allocator == Allocator.Temp);

            angle = math.clamp(angle, 0, 180);

            var vertices = new NativeList<float3>(meshData.vertexCount, Allocator.Temp);
            meshData.GetVerticesDataAsList(ref vertices);

            var indices = new NativeList<int>(meshData.vertexCount, Allocator.Temp);
            meshData.GetAllIndicesDataAsList(ref indices);

            outNormals = new NativeList<float3>(meshData.vertexCount, allocator);
            outNormals.Resize(meshData.vertexCount, NativeArrayOptions.ClearMemory);

            var triNormals = new NativeArray<float3>(indices.Length / 3, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var triNormalJob = new TriangleNormalJob
            {
                TriNormals = triNormals,
                Vertices = vertices.AsArray(),
                Indices = indices.AsArray(),
            };

            var triNormalJobHandle = triNormalJob.ScheduleParallel(indices.Length / 3, JobUtils.GetBatchCountThatMakesSense(indices.Length / 3), default);

            JobHandle normalHandle;
            if (angle == 180f)
            {
                var vertexNormalJob = new UncachedSmoothVertexNormalJob()
                {
                    OutNormals = outNormals.AsArray(),
                    Vertices = vertices.AsArray(),
                    Indices = indices.AsArray(),
                    TriNormals = triNormals,
                    pmGetVertexPosHashMap = new ProfilerMarker("pPosMap"),
                    pmCalculate = new ProfilerMarker("pCalculate"),
                };
                normalHandle = vertexNormalJob.Schedule(triNormalJobHandle);
            }
            else
            {
                var vertexNormalJob = new UncachedAngleVertexNormalJob()
                {
                    OutNormals = outNormals.AsArray(),
                    Vertices = vertices.AsArray(),
                    Indices = indices.AsArray(),
                    TriNormals = triNormals,
                    CosineThreshold = Mathf.Cos(angle * Mathf.Deg2Rad),
                    PGetVertexPosHashMap = new ProfilerMarker("pPosMap"),
                    PCalculate = new ProfilerMarker("pCalculate"),
                };
                normalHandle = vertexNormalJob.Schedule(triNormalJobHandle);
            }

            normalHandle.Complete();

            var uvs = new NativeList<float2>(vertices.Length, Allocator.Temp);
            meshData.GetUVsDataAsList(0, ref uvs);
            RecalculateTangentsUncached(vertices, outNormals, uvs, indices, out outTangents, allocator);

            triNormals.Dispose();
        }


        [BurstCompile]
        public static void RecalculateTangentsUncached
        (
            in NativeList<float3> vertices,
            in NativeList<float3> normals,
            in NativeList<float2> uvs,
            in NativeList<int> indices,
            out NativeList<float4> tangents,
            Allocator allocator
        )
        {
            // Recalculates mesh tangents
            // For some reason the built-in RecalculateTangents function produces artifacts on dense geometries.
            // This implementation id derived from:
            // Lengyel, Eric. Computing Tangent Space Basis Vectors for an Arbitrary Mesh.
            // Terathon Software 3D Graphics Library, 2001.
            // http://www.terathon.com/code/tangent.html

            tangents = new NativeList<float4>(vertices.Length, allocator);
            tangents.ResizeUninitialized(vertices.Length);


            var tan1 = new NativeArray<float3>(vertices.Length, Allocator.Temp, NativeArrayOptions.ClearMemory);
            var tan2 = new NativeArray<float3>(vertices.Length, Allocator.Temp, NativeArrayOptions.ClearMemory);


            for (int i = 0; i < indices.Length; i += 3)
            {
                int i1 = indices[i];
                int i2 = indices[i + 1];
                int i3 = indices[i + 2];

                float3 v1 = vertices[i1];
                float3 v2 = vertices[i2];
                float3 v3 = vertices[i3];

                float2 w1 = uvs[i1];
                float2 w2 = uvs[i2];
                float2 w3 = uvs[i3];

                float x1 = v2.x - v1.x;
                float x2 = v3.x - v1.x;
                float y1 = v2.y - v1.y;
                float y2 = v3.y - v1.y;
                float z1 = v2.z - v1.z;
                float z2 = v3.z - v1.z;

                float s1 = w2.x - w1.x;
                float s2 = w3.x - w1.x;
                float t1 = w2.y - w1.y;
                float t2 = w3.y - w1.y;

                float div = s1 * t2 - s2 * t1;
                float r = div == 0.0f ? 0.0f : 1.0f / div;

                var sDir = new float3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
                var tDir = new float3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);

                tan1[i1] += sDir;
                tan1[i2] += sDir;
                tan1[i3] += sDir;

                tan2[i1] += tDir;
                tan2[i2] += tDir;
                tan2[i3] += tDir;
            }


            for (int a = 0; a < vertices.Length; ++a)
            {
                Vector3 nTemp = normals[a];
                Vector3 tTemp = tan1[a];

                Vector3.OrthoNormalize(ref nTemp, ref tTemp);

                float3 n = nTemp;
                float3 t = tTemp;

                var dot = math.dot(math.cross(n, t), tan2[a]);
                //w of tangent is always 1 or -1
                var w = dot < 0.0f ? -1.0f : 1.0f;
                tangents[a] = new float4(t.x, t.y, t.z, w);
            }
        }
    }
}