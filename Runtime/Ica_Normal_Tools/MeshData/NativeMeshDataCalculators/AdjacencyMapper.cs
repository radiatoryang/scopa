using Ica.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;

namespace Ica.Normal
{
    [BurstCompile]
    public static class AdjacencyMapper
    {
        /// <summary>
        /// Calculate adjacency data to triangle of every vertex
        /// </summary>
        [BurstCompile]
        public static void CalculateAdjacencyData
        (
            [NoAlias] in NativeArray<float3> vertices,
            [NoAlias] in NativeArray<int> indices,
            [NoAlias] in UnsafeHashMap<float3, NativeList<int>> vertexPosHashMap,
            [NoAlias] out NativeList<int> outUnrolledAdjacencyList,
            [NoAlias] out NativeList<int> outStartIndicesMap,
            [NoAlias] out NativeList<int> outRealConnectedCount,
            [NoAlias] Allocator allocator
        )
        {
            var pAdjacencyMapper = new ProfilerMarker("pAdjacencyMapper");
            var pUnroll = new ProfilerMarker("pUnroll");
            var pAllocateForPerVertex = new ProfilerMarker("pAllocateForPerVertex");
            var pCalculate = new ProfilerMarker("pCalculate");
            var pInsertToList = new ProfilerMarker("pInsertToList");

            pAdjacencyMapper.Begin();

            var tempAdjData = new UnsafeList<UnsafeList<int>>(vertices.Length, Allocator.Temp);

            //allocate a list for every vertex position.
            pAllocateForPerVertex.Begin();
            for (int i = 0; i < vertices.Length; i++)
            {
                tempAdjData.Add(new UnsafeList<int>(8, Allocator.Temp));
            }

            pAllocateForPerVertex.End();


            outRealConnectedCount = new NativeList<int>(vertices.Length, allocator);
            outRealConnectedCount.Resize(vertices.Length, NativeArrayOptions.ClearMemory);

            pCalculate.Begin();

            //for every index
            for (int i = 0; i < indices.Length; i++)
            {
                int triIndex = i / 3;
                int vertexIndex = indices[i];
                float3 pos = vertices[vertexIndex];
                NativeList<int> listOfVerticesOnThatPosition = vertexPosHashMap[pos];

                // to every vertices on that position, add current triangle index
                for (int j = 0; j < listOfVerticesOnThatPosition.Length; j++)
                {
                    int vertexOnThatPos = listOfVerticesOnThatPosition.ElementAt(j);

                    //add physically connected triangles to beginning of the list, otherwise end of the list
                    //so we can use this info when using angled method
                    //physically connected
                    if (vertexIndex == vertexOnThatPos)
                    {
                        pInsertToList.Begin();
                        tempAdjData.ElementAt(vertexOnThatPos).InsertAtBeginning(triIndex);
                        outRealConnectedCount[vertexIndex]++;
                        pInsertToList.End();
                    }
                    //not physically connected
                    else
                    {
                        tempAdjData.ElementAt(vertexOnThatPos).Add(triIndex);
                    }
                }
            }

            pCalculate.End();

            pUnroll.Begin();
            //Unroll nested list to make calculation run faster on runtime.
            outUnrolledAdjacencyList = new NativeList<int>(allocator);
            outStartIndicesMap = new NativeList<int>(allocator);
            NativeContainerUtils.UnrollUnsafeListsToList(tempAdjData, ref outUnrolledAdjacencyList, ref outStartIndicesMap);
            pUnroll.End();
            pAdjacencyMapper.End();
        }
    }
}