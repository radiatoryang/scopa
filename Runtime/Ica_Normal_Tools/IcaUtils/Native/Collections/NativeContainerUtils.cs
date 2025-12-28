using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Assertions;

namespace Ica.Utils
{
    /// <summary>
    /// Some of methods to create one dimensional container from multi-Dimensional container.
    /// </summary>
    [BurstCompile]
    public static class NativeContainerUtils
    {
        [BurstCompile]
        public static void UnrollListsToList<T>([NoAlias] in UnsafeList<NativeList<T>> nestedData, [NoAlias] ref NativeList<T> outUnrolledData, [NoAlias] ref NativeList<int> startIndices)
            where T : unmanaged
        {
            GetTotalSizeOfNestedContainer(nestedData, out var size);
            outUnrolledData.SetCapacity(size);
            startIndices.SetCapacity(nestedData.Length + 1);
            outUnrolledData.Clear();
            startIndices.Clear();
            var startIndex = 0;
            for (int i = 0; i < nestedData.Length; i++)
            {
                startIndices.Add(startIndex);
                outUnrolledData.AddRange(nestedData[i].AsArray());
                startIndex += nestedData[i].Length;
            }

            startIndices.Add(startIndex);
        }

        [BurstCompile]
        public static unsafe void UnrollUnsafeListsToList<T>([NoAlias] in UnsafeList<UnsafeList<T>> nestedData, [NoAlias] ref NativeList<T> outUnrolledData, [NoAlias] ref NativeList<int> startIndices)
            where T : unmanaged
        {
            GetTotalSizeOfNestedContainer(nestedData, out var size);
            outUnrolledData.SetCapacity(size);
            startIndices.SetCapacity(nestedData.Length + 1);
            outUnrolledData.Clear();
            startIndices.Clear();
            var startIndex = 0;
            for (int i = 0; i < nestedData.Length; i++)
            {
                startIndices.Add(startIndex);
                outUnrolledData.AddRange(nestedData[i].Ptr, nestedData[i].Length);
                startIndex += nestedData[i].Length;
            }

            startIndices.Add(startIndex);
        }


        [BurstCompile]
        public static void UnrollArraysToList<T>([NoAlias] in UnsafeList<NativeArray<T>> nestedData, [NoAlias] ref NativeList<T> outUnrolledData, [NoAlias] ref NativeList<int> startIndices)
            where T : unmanaged
        {
            outUnrolledData.Clear();
            startIndices.Clear();
            var startIndex = 0;

            for (int i = 0; i < nestedData.Length; i++)
            {
                startIndices.Add(startIndex);
                outUnrolledData.AddRange(nestedData[i]);
                startIndex += nestedData[i].Length;
            }

            startIndices.Add(startIndex);
        }

        [BurstCompile]
        public static void UnrollArraysToArray<T>([NoAlias] in UnsafeList<NativeArray<T>> nestedData, [NoAlias] ref NativeArray<T> outUnrolledData, [NoAlias] ref NativeList<int> startIndices)
            where T : unmanaged
        {
            startIndices.Clear();
            var startIndex = 0;

            for (int i = 0; i < nestedData.Length; i++)
            {
                startIndices.Add(startIndex);
                var size = nestedData[i].Length;
                NativeArray<T>.Copy(nestedData[i], 0, outUnrolledData, startIndex, size);
                startIndex += size;
            }

            startIndices.Add(startIndex);
        }


        [BurstCompile]
        public static void GetTotalSizeOfNestedContainer<T>([NoAlias] in UnsafeList<NativeList<T>> nestedContainer, [NoAlias] out int size) where T : unmanaged
        {
            size = 0;
            for (int i = 0; i < nestedContainer.Length; i++)
            {
                checked
                {
                    size += nestedContainer[i].Length;
                }
            }
        }

        [BurstCompile]
        public static void GetTotalSizeOfNestedContainer<T>([NoAlias] in UnsafeList<NativeArray<T>> nestedContainer, [NoAlias] out int size) where T : unmanaged
        {
            size = 0;
            for (int i = 0; i < nestedContainer.Length; i++)
            {
                size += nestedContainer[i].Length;
            }
        }

        [BurstCompile]
        public static void GetTotalSizeOfNestedContainer<T>([NoAlias] in UnsafeList<UnsafeList<T>> nestedContainer, [NoAlias] out int size) where T : unmanaged
        {
            size = 0;
            for (int i = 0; i < nestedContainer.Length; i++)
            {
                size += nestedContainer[i].Length;
            }
        }


        // [BurstCompile]
        // public static unsafe void AddRangeUnsafeList<T>([NoAlias]this ref NativeList<T> list,[NoAlias] in UnsafeList<T> unsafeList) where T : unmanaged
        // {
        //     list.AddRange(unsafeList.Ptr,unsafeList.Length);
        //
        // }
    }
}