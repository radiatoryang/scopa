using Ica.Normal.JobStructs;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using Ica.Utils;

namespace Ica.Normal
{
    [BurstCompile]
    public static class CachedNormalMethod
    {
        /// <summary>
        /// Scheduling the normal recalculating and returns to job handle.You can use that handle as dependency for tangent job. Do not forget to Complete() job handle!!!
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="indices"></param>
        /// <param name="outNormals"></param>
        /// <param name="adjacencyList"></param>
        /// <param name="adjacencyStartIndicesMap"></param>
        /// <param name="connectedCountMap"></param>
        /// <param name="handle"></param>
        /// <param name="angle">180 is default value which result  smooth angles</param>
        [BurstCompile]
        public static void RecalculateNormalsAndGetHandle
        (
            in NativeList<float3> vertices,
            in NativeList<int> indices,
            ref NativeList<float3> outNormals,
            in NativeList<int> adjacencyList,
            in NativeList<int> adjacencyStartIndicesMap,
            in NativeList<int> connectedCountMap,
            out JobHandle handle,
            float angle = 180f
        )
        {
            var pSchedule = new ProfilerMarker("pSchedule");
            pSchedule.Begin();

            angle = math.clamp(angle, 0, 180);
            var triangleCount = indices.Length / 3;

            var triNormals = new NativeArray<float3>(indices.Length / 3, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var triNormalJob = new TriangleNormalJob
            {
                Indices = indices.AsArray(),
                TriNormals = triNormals,
                Vertices = vertices.AsArray()
            };

            var triNormalJobHandle = triNormalJob.ScheduleParallel(triangleCount, JobUtils.GetBatchCountThatMakesSense(triangleCount), default);

            if (angle == 180f)
            {
                var vertexNormalJob = new SmoothVertexNormalJob()
                {
                    AdjacencyList = adjacencyList.AsArray(),
                    AdjacencyMapper = adjacencyStartIndicesMap.AsArray(),
                    TriNormals = triNormals,
                    Normals = outNormals.AsArray(),
                };
                handle = vertexNormalJob.ScheduleParallel(vertices.Length, JobUtils.GetBatchCountThatMakesSense(vertices.Length), triNormalJobHandle);
                handle = triNormals.Dispose(handle);
            }
            else
            {
                var vertexNormalJob = new AngleBasedVertexNormalJob
                {
                    AdjacencyList = adjacencyList.AsArray(),
                    AdjacencyMapper = adjacencyStartIndicesMap.AsArray(),
                    TriNormals = triNormals,
                    Normals = outNormals.AsArray(),
                    ConnectedMapper = connectedCountMap.AsArray(),
                    CosineThreshold = Mathf.Cos(angle * Mathf.Deg2Rad),
                };
                
                handle = vertexNormalJob.ScheduleParallel(vertices.Length, JobUtils.GetBatchCountThatMakesSense(vertices.Length), triNormalJobHandle);
                handle = triNormals.Dispose(handle);
            }
            
            pSchedule.End();
        }
    }
}