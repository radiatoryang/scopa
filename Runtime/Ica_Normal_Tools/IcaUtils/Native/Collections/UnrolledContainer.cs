using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;


namespace Ica.Utils
{
    /// <summary>
    /// Unrolled 2D native list. Add operations involve a MemMove, similar to List Insert. So adding to last lists cheaper than adding to first lists.
    /// </summary>
    /// <typeparam name="T">Type of data that list holds</typeparam>
    public struct UnrolledList<T> : IDisposable where T : unmanaged
    {
        public NativeList<T> _data;

        /// <summary>
        /// Start indices of sub array on _data. Last element is total count of data;
        /// </summary>
        public NativeList<int> _startIndices;
        public int SubContainerCount => _startIndices.Length - 1;
        

        public void Add(int subArrayIndex, T item)
        {
            _data.Insert(_startIndices[subArrayIndex] + GetSubArrayLength(subArrayIndex), item);
            for (int i = subArrayIndex + 1; i < _startIndices.Length; i++)
            {
                _startIndices[i]++;
            }
        }

        public UnrolledList(int subArrayCount, Allocator allocator)
        {
            _data = new NativeList<T>(0, allocator);
            _startIndices = new NativeList<int>(subArrayCount + 1, allocator);
            _startIndices.Resize(subArrayCount + 1,NativeArrayOptions.ClearMemory);
        }

        public UnrolledList(in UnsafeList<NativeList<T>> nestedData, Allocator allocator)
        {
            Assert.IsTrue(nestedData.Length > 0, "nested list count should be more than zero");
            NativeContainerUtils.GetTotalSizeOfNestedContainer(nestedData, out var totalSize);
            _data = new NativeList<T>(totalSize, allocator);
            _startIndices = new NativeList<int>(nestedData.Length + 1, allocator);
            NativeContainerUtils.UnrollListsToList(nestedData, ref _data, ref _startIndices);
        }

        public NativeArray<T> GetSubArray(int index)
        {
            return _data.AsArray().GetSubArray(_startIndices[index], GetSubArrayLength(index));
        }

        public int GetSubArrayLength(int subArrayIndex)
        {
            return _startIndices[subArrayIndex + 1] - _startIndices[subArrayIndex];
        }

        public void Dispose()
        {
            _data.Dispose();
            _startIndices.Dispose();
        }
    }
}