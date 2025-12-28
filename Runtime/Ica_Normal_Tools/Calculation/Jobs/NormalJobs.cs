using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Ica.Normal.JobStructs
{
    [BurstCompile]
    public struct TriangleNormalJob : IJobFor
    {
        [ReadOnly] public NativeArray<int> Indices;
        [ReadOnly] public NativeArray<float3> Vertices;
        [WriteOnly] public NativeArray<float3> TriNormals;

        public void Execute(int index)
        {
            float3 vertexA = Vertices[Indices[index * 3]];
            float3 vertexB = Vertices[Indices[index * 3 + 1]];
            float3 vertexC = Vertices[Indices[index * 3 + 2]];

            // Calculate the normal of the triangle
            float3 crossProduct = math.cross(vertexB - vertexA, vertexC - vertexA);

            TriNormals[index] = crossProduct;
        }
    }


    [BurstCompile]
    public struct AngleBasedVertexNormalJob : IJobFor
    {
        [ReadOnly] public NativeArray<int> AdjacencyList;
        [ReadOnly] public NativeArray<int> AdjacencyMapper;
        [ReadOnly] public NativeArray<int> ConnectedMapper;
        [ReadOnly] public NativeArray<float3> TriNormals;
        [ReadOnly] public float CosineThreshold;
        [WriteOnly] public NativeArray<float3> Normals;

        public void Execute(int vertexIndex)
        {
            int subArrayStart = AdjacencyMapper[vertexIndex];
            int subArrayCount = AdjacencyMapper[vertexIndex + 1] - AdjacencyMapper[vertexIndex];
            int connectedCount = ConnectedMapper[vertexIndex];
            float3 sum = 0;

            //for every connected triangle, include it to final normal output no matter what
            for (int i = 0; i < connectedCount; ++i)
            {
                int triID = AdjacencyList[subArrayStart + i];
                sum += TriNormals[triID];
            }

            float3 normalFromConnectedTriangles = math.normalize(sum);

            //for every non connected (but adjacent) triangle, include it to final vertex normal if angle smooth enough
            for (int i = 0; i < subArrayCount - connectedCount; i++)
            {
                int triID = AdjacencyList[subArrayStart + connectedCount + i];
                var normalizedCurrentTri = math.normalize(TriNormals[triID]);
                double dotProd = math.dot(normalFromConnectedTriangles, normalizedCurrentTri);

                if (dotProd >= CosineThreshold)
                {
                    sum += TriNormals[triID];
                }
            }

            Normals[vertexIndex] = math.normalize(sum);
        }
    }

    [BurstCompile]
    public struct SmoothVertexNormalJob : IJobFor
    {
        [ReadOnly] public NativeArray<int> AdjacencyList;
        [ReadOnly] public NativeArray<int> AdjacencyMapper;
        [ReadOnly] public NativeArray<float3> TriNormals;
        [WriteOnly] public NativeArray<float3> Normals;

        public void Execute(int vertexIndex)
        {
            int subArrayStart = AdjacencyMapper[vertexIndex];
            int subArrayCount = AdjacencyMapper[vertexIndex + 1] - AdjacencyMapper[vertexIndex];
            float3 dotProdSum = 0;

            //for every adjacent triangle
            for (int i = 0; i < subArrayCount; ++i)
            {
                int triID = AdjacencyList[subArrayStart + i];
                dotProdSum += TriNormals[triID];
            }

            var normalized = math.normalize(dotProdSum);

            Normals[vertexIndex] = normalized;
        }
    }
}