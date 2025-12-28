using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Ica.Utils
{
    public static unsafe class NativeCollectionExtensions
    {
        public static void InsertAtBeginning<T>(this ref NativeList<T> list, T element) where T : unmanaged
        {
            list.Add(new T());
            T* destination = list.GetUnsafeList()->Ptr + 1;
            T* source = list.GetUnsafeList()->Ptr;
            long size = sizeof(T) * (list.Length - 1);
            UnsafeUtility.MemMove(destination, source, size);
            list[0] = element;
        }

        public static void InsertAtBeginning<T>(this ref UnsafeList<T> list, T element) where T : unmanaged
        {
            list.Add(new T());
            UnsafeUtility.MemMove(list.Ptr + 1, list.Ptr, sizeof(T) * (list.Length - 1));
            list[0] = element;
        }

        public static void Insert<T>(this ref NativeList<T> list, int index, T item) where T : unmanaged
        {
            list.Add(item);

            var destination = list.GetUnsafeList()->Ptr + index + 1;
            var source = list.GetUnsafeList()->Ptr + index;
            long size = sizeof(T) * (list.Length - 1 - index);

            UnsafeUtility.MemMove(destination, source, size);

            list[index] = item;
        }
    }
}

namespace Unity.Collections.LowLevel.Unsafe
{
    public static unsafe class UnsafeHashMapAsRefExtensions
    {
        [BurstCompile]
        public static bool TryGetValueAsRef<TKey, TValue>(ref UnsafeHashMap<TKey, TValue> hashMap, in TKey key, ref TValue outRef)
            where TValue : unmanaged where TKey : unmanaged, IEquatable<TKey>
        {
            ref HashMapHelper<TKey> data = ref hashMap.m_Data;
            int idx = data.Find(key);

            if (-1 != idx)
            {
                outRef =  UnsafeUtility.ArrayElementAsRef<TValue>(data.Ptr, idx);
                return  true;
            }

            outRef = outRef;
            return false;


        }
    }
}