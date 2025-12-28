using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;


namespace Ica.Normal
{
    
    /// <summary>
    /// EXPERIMENTAL DO NOT USE!!!
    /// </summary>
    //[BurstCompile]
    [Obsolete]
    public static class CachedLiteMethod
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        public static void NormalizeDuplicateVertices(in UnsafeList<NativeArray<int>> duplicatesData, ref NativeArray<float3> normals, ref NativeArray<float4> tangents)
        {
            var job = new NormalizeDuplicateVerticesJob
            {
                DuplicatesData = duplicatesData,
                Normals = normals,
                Tangents = tangents
            };
            job.Run();
        }
        
        [BurstCompile]
        private struct NormalizeDuplicateVerticesJob : IJob
        {
            [ReadOnly] public UnsafeList<NativeArray<int>> DuplicatesData;
            public NativeArray<float3> Normals;
            public NativeArray<float4> Tangents;

            public void Execute()
            {
                for (int duplicatePos = 0; duplicatePos < DuplicatesData.Length; duplicatePos++)
                {
                    var normalSum = float3.zero;
                    var tangentSum = float4.zero;

                    var length = DuplicatesData[duplicatePos].Length;

                    for (int v = 0; v < length; v++)
                    {
                        normalSum += Normals[DuplicatesData[duplicatePos][v]];
                        tangentSum += Tangents[DuplicatesData[duplicatePos][v]];
                    }

                    normalSum = math.normalize(normalSum);

                    var tangXYZ = new float3(tangentSum.x, tangentSum.y, tangentSum.z);
                    tangXYZ = math.normalize(tangXYZ);
                    tangentSum = new Vector4(tangXYZ.x, tangXYZ.y, tangXYZ.z, math.clamp(tangentSum.w, -1f, 1f));

                    for (int i = 0; i < length; i++)
                    {
                        Normals[DuplicatesData[duplicatePos][i]] = normalSum;
                        Tangents[DuplicatesData[duplicatePos][i]] = tangentSum;
                    }
                }
            }
        }
    }
}