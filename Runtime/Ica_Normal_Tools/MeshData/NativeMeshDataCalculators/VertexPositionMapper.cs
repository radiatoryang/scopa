using System;
using Ica.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace Ica.Normal
{
    [BurstCompile]
    public static unsafe class VertexPositionMapper
    {
        /// <summary>
        /// Get a HashMap where keys are position and values are a list of index of vertices that locate on that position.
        /// If Value List only have one member that means that vertex have not a duplicate. 
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="posVertexIndicesPair"></param>
        /// <param name="allocator"></param>
        [BurstCompile]
        public static void GetVertexPosHashMap(
            [NoAlias] in NativeArray<float3> vertices,
            [NoAlias] out UnsafeHashMap<float3, NativeList<int>> posVertexIndicesPair,
            [NoAlias] Allocator allocator)
        {
            var pGetVertexPosHashMap = new ProfilerMarker("pGetVertexPosHashMap");
            var pAddToList = new ProfilerMarker("pAddToList");
            var pAddNewPair = new ProfilerMarker("pAddNewPair");
            var pCreateList = new ProfilerMarker("pCreateList");
            pGetVertexPosHashMap.Begin();

            posVertexIndicesPair = new UnsafeHashMap<float3, NativeList<int>>(vertices.Length, allocator);

            //NativeList<int> outRef = new NativeList<int>(1, Allocator.Temp);
            
            for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
            {
                 

                //Get by ref test
                // //if position already occurs before, add current vertex index to list. This means this vertex duplicate.
                // if (UnsafeHashMapAsRefExtensions.TryGetValueAsRef(ref posVertexIndicesPair, key: vertices[vertexIndex], outRef: ref outRef ))
                // {
                //     pAddToList.Begin();
                //     outRef.Add(vertexIndex);
                //     pAddToList.End();
                // }
                // else
                // {
                //     pCreateList.Begin();
                //     var vertexIndexList = new NativeList<int>(1, allocator) { vertexIndex };
                //     pCreateList.End();
                //
                //     pAddNewPair.Begin();
                //     posVertexIndicesPair.Add(vertices[vertexIndex], vertexIndexList);
                //     pAddNewPair.End();
                // }


                //if position already occurs before, add current vertex index to list. This means this vertex duplicate.
                if (posVertexIndicesPair.TryGetValue(vertices[vertexIndex], out var vertexIndexList))
                {
                    pAddToList.Begin();
                    vertexIndexList.Add(vertexIndex);
                    pAddToList.End();
                }
                else
                {
                    pCreateList.Begin();
                    vertexIndexList = new NativeList<int>(1, allocator) { vertexIndex };
                    pCreateList.End();
                
                    pAddNewPair.Begin();
                    posVertexIndicesPair.Add(vertices[vertexIndex], vertexIndexList);
                    pAddNewPair.End();
                }
            }


            pGetVertexPosHashMap.End();
        }
    }
}