using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Ica.Utils
{
    [BurstCompile]
    public static class MergedMeshDataUtils
    {
        [BurstCompile]
        public static void GetMergedVertices([NoAlias]this in Mesh.MeshDataArray mda, [NoAlias] ref NativeList<float3> outMergedVertices, [NoAlias] ref NativeList<int> startIndexMapper)
        {
            var vertexList = new UnsafeList<NativeArray<float3>>(mda.Length, Allocator.Temp);
            for (int i = 0; i < mda.Length; i++)
            {
                var v = new NativeArray<float3>(mda[i].vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                mda[i].GetVertices(v.Reinterpret<Vector3>());
                vertexList.Add(v);
            }

            NativeContainerUtils.UnrollArraysToList(vertexList, ref outMergedVertices, ref startIndexMapper);
        }

        [BurstCompile]
        public static void GetTotalVertexCountFomMDA([NoAlias]this in Mesh.MeshDataArray mda, out int count)
        {
            count = 0;
            for (int i = 0; i < mda.Length; i++)
            {
                count += mda[i].vertexCount;
            }

        }

        [BurstCompile]
        public static void GetMergedUVs([NoAlias]this in Mesh.MeshDataArray mda, [NoAlias] ref NativeList<float2> outMergedUVs, [NoAlias] ref NativeList<int> startIndexMapper)
        {
            var nestedData = new UnsafeList<NativeArray<float2>>(mda.Length, Allocator.Temp);
            for (int i = 0; i < mda.Length; i++)
            {
                var singleMeshData = new NativeArray<float2>(mda[i].vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                mda[i].GetUVs(0, singleMeshData.Reinterpret<Vector2>());
                nestedData.Add(singleMeshData);
            }

            NativeContainerUtils.UnrollArraysToList(nestedData, ref outMergedUVs, ref startIndexMapper);
        }


        [BurstCompile]
        public static void GetMergedNormals([NoAlias]this in Mesh.MeshDataArray mda, [NoAlias] ref NativeList<float3> outMergedNormals, [NoAlias] ref NativeList<int> startIndexMapper)
        {
            var nestedData = new UnsafeList<NativeArray<float3>>(mda.Length, Allocator.Temp);
            for (int i = 0; i < mda.Length; i++)
            {
                var singleMeshData = new NativeArray<float3>(mda[i].vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                mda[i].GetNormals(singleMeshData.Reinterpret<Vector3>());
                nestedData.Add(singleMeshData);
            }

            NativeContainerUtils.UnrollArraysToList(nestedData, ref outMergedNormals, ref startIndexMapper);
        }

        [BurstCompile]
        public static void GetMergedTangents([NoAlias]this in Mesh.MeshDataArray mda, [NoAlias] ref NativeList<float4> outMergedNormals, [NoAlias] ref NativeList<int> startIndexMapper)
        {
            var nestedData = new UnsafeList<NativeArray<float4>>(mda.Length, Allocator.Temp);
            for (int i = 0; i < mda.Length; i++)
            {
                var singleMeshData = new NativeArray<float4>(mda[i].vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                mda[i].GetTangents(singleMeshData.Reinterpret<Vector4>());
                nestedData.Add(singleMeshData);
            }

            NativeContainerUtils.UnrollArraysToList(nestedData,  ref outMergedNormals, ref startIndexMapper);
        }


        [BurstCompile]
        public static void CreateAndGetMergedIndices(this in Mesh.MeshDataArray mda, out NativeList<int> outMergedIndices, out NativeList<int> startIndexMapper, Allocator allocator)
        {
            GetTotalIndexCountOfMDA(mda, out int totalIndexCount);
            outMergedIndices = new NativeList<int>(totalIndexCount, allocator);
            startIndexMapper = new NativeList<int>(totalIndexCount + 1, allocator);

            GetMergedIndices(mda, ref outMergedIndices, ref startIndexMapper);
        }

        [BurstCompile]
        public static void GetMergedIndices([NoAlias]this in Mesh.MeshDataArray mda, [NoAlias] ref NativeList<int> mergedIndices, [NoAlias] ref NativeList<int> mergedIndicesMap)
        {
            var listOfIndexData = new UnsafeList<NativeList<int>>(1, Allocator.Temp);
            var prevMeshesTotalVertexCount = 0;
            for (int i = 0; i < mda.Length; i++)
            {
                mda[i].GetCountOfAllIndices(out int indexCount);
                var indices = new NativeList<int>(indexCount, Allocator.Temp);
                mda[i].GetAllIndicesDataAsList(ref indices);
                for (int j = 0; j < indices.Length; j++)
                {
                    indices[j] += prevMeshesTotalVertexCount;
                }

                listOfIndexData.Add(indices);
                prevMeshesTotalVertexCount += mda[i].vertexCount;
            }

            NativeContainerUtils.UnrollListsToList(listOfIndexData, ref mergedIndices, ref mergedIndicesMap);
        }

        [BurstCompile]
        public static void GetTotalIndexCountOfMDA([NoAlias]this in Mesh.MeshDataArray mda, [NoAlias] out int count)
        {
            count = 0;
            for (int i = 0; i < mda.Length; i++)
            {
                mda[i].GetCountOfAllIndices(out var meshIndexCount);
                count += meshIndexCount;
            }
        }
    }
}