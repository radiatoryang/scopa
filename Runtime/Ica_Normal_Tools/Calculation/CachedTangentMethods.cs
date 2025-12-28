using Ica.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace Ica.Normal
{
    public static class CachedTangentMethods
    {
        /// <summary>
        /// Scheduling the tangent recalculating and returns to job handle. Do not forget to Complete job handle!!!
        /// If not dependent on normal handle pass default.
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="normals"></param>
        /// <param name="indices"></param>
        /// <param name="uv"></param>
        /// <param name="adjacencyList"></param>
        /// <param name="adjacencyMap"></param>
        /// <param name="tan1"></param>
        /// <param name="tan2"></param>
        /// <param name="outTangents"></param>
        /// <param name="normalHandle"></param>
        /// <param name="tangentHandle"></param>
        [BurstCompile]
        public static void ScheduleAndGetTangentJobHandle
        (
            in NativeList<float3> vertices,
            in NativeList<float3> normals,
            in NativeList<int> indices,
            in NativeList<float2> uv,
            in NativeList<int> adjacencyList,
            in NativeList<int> adjacencyMap,
            in NativeList<float3> tan1,
            in NativeList<float3> tan2,
            ref NativeList<float4> outTangents,
            ref JobHandle normalHandle,
            out JobHandle tangentHandle
        )
        {
            var pCachedParallelTangent = new ProfilerMarker("pCachedParallelTangent");
            pCachedParallelTangent.Begin();

            var triTangentJob = new TangentJobs.TriangleTangentJob
            {
                Indices = indices.AsArray(),
                Vertices = vertices.AsArray(),
                UV = uv.AsArray(),
                Tan1 = tan1.AsArray(),
                Tan2 = tan2.AsArray()
            };

            var vertexTangentJob = new TangentJobs.VertexTangentJob
            {
                AdjacencyList = adjacencyList.AsArray(),
                AdjacencyListMapper = adjacencyMap.AsArray(),
                Normals = normals.AsArray(),
                Tan1 = tan1.AsArray(),
                Tan2 = tan2.AsArray(),
                Tangents = outTangents.AsArray()
            };

            var triHandle = triTangentJob.ScheduleParallel
                (indices.Length / 3, JobUtils.GetBatchCountThatMakesSense(indices.Length / 3), normalHandle);

            tangentHandle = vertexTangentJob.ScheduleParallel
                (vertices.Length, JobUtils.GetBatchCountThatMakesSense(vertices.Length), triHandle);

            pCachedParallelTangent.End();
        }
    }
}