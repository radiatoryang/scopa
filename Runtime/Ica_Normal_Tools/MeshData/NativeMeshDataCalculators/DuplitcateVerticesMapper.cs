using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Ica.Normal
{
    [BurstCompile]
    public static class DuplicateVerticesMapper
    {
        /// <summary>
        /// Takes vertex Pos hashMap and return only duplicate ones.
        /// </summary>
        /// <param name="vertexPosHashMap"></param>
        /// <param name="outDuplicateVerticesMap"></param>
        /// <param name="allocator"></param>
        [BurstCompile]
        public static void GetDuplicateVerticesMap
        (
            in UnsafeHashMap<float3, NativeList<int>> vertexPosHashMap,
            out UnsafeList<NativeArray<int>> outDuplicateVerticesMap,
            Allocator allocator
        )
        {
            outDuplicateVerticesMap = new UnsafeList<NativeArray<int>>(16, allocator);

            foreach (var kvp in vertexPosHashMap)
            {
                //If there is more than one vertex on that position that means vertices are duplicate
                if (kvp.Value.Length > 1)
                {
                    outDuplicateVerticesMap.Add(new NativeArray<int>(kvp.Value.AsArray(), allocator));
                }
            }
        }


        /// <summary>
        /// Convert native duplicate vertex map to managed one,which can be serialize.
        /// </summary>
        /// <param name="from"></param>
        /// <returns></returns>
        public static List<DuplicateVerticesList> GetManagedDuplicateVerticesMap(UnsafeList<NativeArray<int>> from)
        {
            var list = new List<DuplicateVerticesList>(from.Length);
            foreach (var fromArray in from)
            {
                var managed = new DuplicateVerticesList { Value = fromArray.ToArray() };
                list.Add(managed);
            }

            return list;
        }
    }
}