using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Ica.Normal
{
    public class TangentJobs
    {
        // Recalculates mesh tangents
        // For some reason the built-in RecalculateTangents function produces artifacts on dense geometries.
        // This implementation id derived from:
        // Lengyel, Eric. Computing Tangent Space Basis Vectors for an Arbitrary Mesh.
        // Terathon Software 3D Graphics Library, 2001.
        // http://www.terathon.com/code/tangent.html
        
        [BurstCompile]
        public struct TriangleTangentJob : IJobFor
        {
            [ReadOnly] public NativeArray<int> Indices;
            [ReadOnly] public NativeArray<float3> Vertices;
            [ReadOnly] public NativeArray<float2> UV;
            [WriteOnly] public NativeArray<float3> Tan1;
            [WriteOnly] public NativeArray<float3> Tan2;

            public void Execute(int triIndex)
            {
                int i1 = Indices[triIndex * 3];
                int i2 = Indices[triIndex * 3 + 1];
                int i3 = Indices[triIndex * 3 + 2];

                float3 v1 = Vertices[i1];
                float3 v2 = Vertices[i2];
                float3 v3 = Vertices[i3];

                float2 w1 = UV[i1];
                float2 w2 = UV[i2];
                float2 w3 = UV[i3];

                float x1 = v2.x - v1.x;
                float x2 = v3.x - v1.x;
                float y1 = v2.y - v1.y;
                float y2 = v3.y - v1.y;
                float z1 = v2.z - v1.z;
                float z2 = v3.z - v1.z;

                float s1 = w2.x - w1.x;
                float s2 = w3.x - w1.x;
                float t1 = w2.y - w1.y;
                float t2 = w3.y - w1.y;

                float div = s1 * t2 - s2 * t1;
                float r = div == 0.0f ? 0.0f : 1.0f / div;

                var sDir = new float3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
                var tDir = new float3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);

                Tan1[triIndex] = sDir;
                Tan2[triIndex] = tDir;
            }
        }

        [BurstCompile]
        public struct VertexTangentJob : IJobFor
        {
            [ReadOnly] public NativeArray<int> AdjacencyList;
            [ReadOnly] public NativeArray<int> AdjacencyListMapper;
            [ReadOnly] public NativeArray<float3> Normals;
            [ReadOnly] public NativeArray<float3> Tan1;
            [ReadOnly] public NativeArray<float3> Tan2;
            [WriteOnly] public NativeArray<float4> Tangents;

            public void Execute(int vertexIndex)
            {
                int subArrayStart = AdjacencyListMapper[vertexIndex];
                int subArrayCount = AdjacencyListMapper[vertexIndex + 1] - AdjacencyListMapper[vertexIndex];
                float3 t1Sum = new float3();
                float3 t2Sum = new float3();

                for (int i = 0; i < subArrayCount; ++i)
                {
                    int triID = AdjacencyList[subArrayStart + i];
                    t1Sum += Tan1[triID];
                    t2Sum += Tan2[triID];
                }

                Vector3 nTemp = Normals[vertexIndex];
                Vector3 tTemp = t1Sum;

                //TODO: Use math library and float3 here, and remove temp values
                Vector3.OrthoNormalize(ref nTemp, ref tTemp);

                float3 n = nTemp;
                float3 t = tTemp;
                
                float w;
                
                if (math.dot(math.cross(n, t), t2Sum) < 0.0f)
                    w = -1.0f;
                else
                    w = 1.0f;
                Tangents[vertexIndex] = new float4(t.x, t.y, t.z, w);
            }
        }
    }
}