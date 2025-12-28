using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace Ica.Normal.JobStructs
{
    //Originally from
    //https://web.archive.org/web/20220624174317/http://schemingdeveloper.com/2017/03/26/better-method-recalculate-normals-unity-part-2/
    public struct VertexEntry
    {
        public readonly int VertexIndex;
        public readonly int TriangleIndex;

        public VertexEntry(int vertexIndex, int triangleIndex)
        {
            VertexIndex = vertexIndex;
            TriangleIndex = triangleIndex;
        }
    }

    [BurstCompile]
    public struct UncachedAngleVertexNormalJob : IJob
    {
        [ReadOnly] public NativeArray<float3> TriNormals;
        [ReadOnly] public NativeArray<float3> Vertices;
        [ReadOnly] public NativeArray<int> Indices;
        public NativeArray<float3> OutNormals;
        [ReadOnly] public float CosineThreshold;

        public ProfilerMarker PGetVertexPosHashMap;
        public ProfilerMarker PCalculate;

        public void Execute()
        {
            PGetVertexPosHashMap.Begin();

            var posMap = new UnsafeHashMap<float3, NativeList<VertexEntry>>(Vertices.Length, Allocator.Temp);

            for (int i = 0; i < Indices.Length; i += 3)
            {
                int triIndex = i / 3;

                for (int j = 0; j < 3; j++)
                {
                    int subVertexIndex = Indices[i + j];

                    if (posMap.TryGetValue(Vertices[subVertexIndex], out var vEntry))
                    {
                        vEntry.Add(new VertexEntry(subVertexIndex, triIndex));
                    }
                    else
                    {
                        vEntry = new NativeList<VertexEntry>(3, Allocator.Temp) { new VertexEntry(subVertexIndex, triIndex) };
                        posMap.Add(Vertices[subVertexIndex], vEntry);
                    }
                }
            }

            PGetVertexPosHashMap.End();

            PCalculate.Begin();
            foreach (var kvp in posMap)
            {
                for (int i = 0; i < kvp.Value.Length; ++i)
                {
                    var sum = new float3();
                    VertexEntry lhsEntry = kvp.Value.ElementAt(i);

                    for (int j = 0; j < kvp.Value.Length; ++j)
                    {
                        VertexEntry rhsEntry = kvp.Value.ElementAt(j);

                        if (lhsEntry.VertexIndex == rhsEntry.VertexIndex)
                        {
                            sum += TriNormals[rhsEntry.TriangleIndex];
                        }
                        else
                        {
                            // The dot product is the cosine of the angle between the two triangles.
                            // A larger cosine means a smaller angle.
                            float dotProduct = math.dot(TriNormals[lhsEntry.TriangleIndex], TriNormals[rhsEntry.TriangleIndex]);

                            if (dotProduct >= CosineThreshold)
                            {
                                sum += TriNormals[rhsEntry.TriangleIndex];
                            }
                        }
                    }

                    var normalized = math.normalize(sum);
                    OutNormals[lhsEntry.VertexIndex] = normalized;
                }
            }

            PCalculate.End();
        }
    }


    //
    [BurstCompile]
    public struct UncachedSmoothVertexNormalJob : IJob
    {
        [ReadOnly] public NativeArray<float3> TriNormals;
        [ReadOnly] public NativeArray<float3> Vertices;
        [ReadOnly] public NativeArray<int> Indices;
        public NativeArray<float3> OutNormals;

        public ProfilerMarker pmGetVertexPosHashMap;
        public ProfilerMarker pmCalculate;


        public void Execute()
        {
            pmGetVertexPosHashMap.Begin();

            var posMap = new UnsafeHashMap<float3, NativeList<VertexEntry>>(Vertices.Length, Allocator.Temp);

            for (int i = 0; i < Indices.Length; i += 3)
            {
                int triIndex = i / 3;

                for (int j = 0; j < 3; j++)
                {
                    int subVertexIndex = Indices[i + j];

                    if (posMap.TryGetValue(Vertices[subVertexIndex], out var vEntry))
                    {
                        vEntry.Add(new VertexEntry(subVertexIndex, triIndex));
                    }
                    else
                    {
                        vEntry = new NativeList<VertexEntry>(3, Allocator.Temp) { new VertexEntry(subVertexIndex, triIndex) };
                        posMap.Add(Vertices[subVertexIndex], vEntry);
                    }
                }
            }

            pmGetVertexPosHashMap.End();


            pmCalculate.Begin();
            for (int i = 0; i < Indices.Length; i += 3)
            {
                int triIndex = i / 3;

                for (int j = 0; j < 3; j++)
                {
                    int subVertexIndex = Indices[i + j];

                    var map = posMap[Vertices[subVertexIndex]];

                    for (int k = 0; k < map.Length; k++)
                    {
                        OutNormals[map[k].VertexIndex] += TriNormals[triIndex];
                    }
                }
            }

            for (int i = 0; i < OutNormals.Length; i++)
            {
                OutNormals[i] = math.normalize(OutNormals[i]);
            }


            pmCalculate.End();
        }
    }
}