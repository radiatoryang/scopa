using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Ica.Utils
{
    public static class MeshDataExtensions
    {
        [BurstCompile]
        public static void AllocAndGetVerticesDataAsArray(this in Mesh.MeshData data, out NativeArray<float3> outVertices, Allocator allocator)
        {
            outVertices = new NativeArray<float3>(data.vertexCount, allocator, NativeArrayOptions.UninitializedMemory);
            data.GetVertices(outVertices.Reinterpret<Vector3>());
        }

        [BurstCompile]
        public static void AllocAndGetNormalsDataAsArray(this in Mesh.MeshData data, out NativeArray<float3> outNormals, Allocator allocator)
        {
            outNormals = new NativeArray<float3>(data.vertexCount, allocator, NativeArrayOptions.UninitializedMemory);
            data.GetNormals(outNormals.Reinterpret<Vector3>());
        }

        [BurstCompile]
        public static void GetVerticesDataAsList(this in Mesh.MeshData data, ref NativeList<float3> outVertices)
        {
            outVertices.Resize(data.vertexCount, NativeArrayOptions.UninitializedMemory);
            data.GetVertices(outVertices.AsArray().Reinterpret<Vector3>());
        }

        [BurstCompile]
        public static void GetNormalsDataAsList(this in Mesh.MeshData data, ref NativeList<float3> outNormals)
        {
            outNormals.Resize(data.vertexCount, NativeArrayOptions.UninitializedMemory);
            data.GetNormals(outNormals.AsArray().Reinterpret<Vector3>());
        }

        [BurstCompile]
        public static void GetUVsDataAsList(this in Mesh.MeshData data, int channel, ref NativeList<float2> outUVs)
        {
            outUVs.Resize(data.vertexCount, NativeArrayOptions.UninitializedMemory);
            data.GetUVs(channel, outUVs.AsArray().Reinterpret<Vector2>());
        }

        /// <summary>
        /// Creates a new native list with specified allocator then fill it with indices of given mesh's all submesh-es. similar to Mesh.triangles method.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="outIndices"></param>
        /// <param name="allocator"></param>
        [BurstCompile]
        public static void GetAllIndicesDataAsList(this in Mesh.MeshData data, ref NativeList<int> outIndices)
        {
            //GetCountOfAllIndices(data,out var indexCount);
            outIndices.Clear();
            //outIndices.Resize(indexCount,NativeArrayOptions.UninitializedMemory);

            for (int subMeshIndex = 0; subMeshIndex < data.subMeshCount; subMeshIndex++)
            {
                var tempSubmeshIndices = new NativeArray<int>(data.GetSubMesh(subMeshIndex).indexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                data.GetIndices(tempSubmeshIndices, subMeshIndex);

                outIndices.AddRange(tempSubmeshIndices);
            }
        }

        /// <summary>
        /// counts and return given mesh's all indices.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="indexCount"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetCountOfAllIndices(this in Mesh.MeshData data, out int indexCount)
        {
            var submeshCount = data.subMeshCount;
            indexCount = 0;
            for (int i = 0; i < submeshCount; i++)
            {
                indexCount += data.GetSubMesh(i).indexCount;
            }
        }
    }
}