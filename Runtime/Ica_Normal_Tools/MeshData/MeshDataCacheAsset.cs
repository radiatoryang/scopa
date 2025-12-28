using Ica.Utils;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;


namespace Ica.Normal
{
  //TODO
    /// <summary>
    /// A mesh data cache asset to eliminate data calculation time on start
    /// </summary>
   // [PreferBinarySerialization]
   // [CreateAssetMenu(menuName = "Plugins/IcaNormalRecalculation/MeshDataCacheAsset", fileName = "IcaMeshDataCacheAsset")]
    public class MeshDataCacheAsset : ScriptableObject
    {
        public Mesh TargetMesh;
        [SerializeField, HideInInspector] public int[] SerializedIndices;
        [SerializeField, HideInInspector] public int[] SerializedAdjacencyList;
        [SerializeField, HideInInspector] public int[] SerializedAdjacencyMapper;
        public string LastCacheDate = "Never";


        [ContextMenu("CacheData")]
        public void CacheData()
        {
            Profiler.BeginSample("GetMDA");
            var mda = Mesh.AcquireReadOnlyMeshData(TargetMesh);
            var data = mda[0];
            Profiler.EndSample();

            Profiler.BeginSample("GetVertices");
            var vertices = new NativeList<float3>(data.vertexCount, Allocator.Temp);
            data.GetVerticesDataAsList(ref vertices);
            Profiler.EndSample();

            Profiler.BeginSample("GetIndices");
            data.GetCountOfAllIndices(out int indexCount);
            var indices = new NativeList<int>(indexCount, Allocator.Temp);
            data.GetAllIndicesDataAsList(ref indices);
            Profiler.EndSample();

            Profiler.BeginSample("GetPosGraph");
            VertexPositionMapper.GetVertexPosHashMap(vertices.AsArray(), out var posMap, Allocator.Temp);
            Profiler.EndSample();

            Profiler.BeginSample("GetDuplicatesGraph");
            //DuplicateVerticesMapper.GetDuplicateVerticesMap(in posGraph, out var nativeVertMap, Allocator.Temp);
            Profiler.EndSample();


            Profiler.BeginSample("DuplicatesToManaged");
            //SerializedDuplicatesData = NativeToManagedUtils.GetManagedDuplicateVerticesMap(nativeVertMap);
            Profiler.EndSample();


            Profiler.BeginSample("Adjacency");
            Profiler.BeginSample("Calculate");
            AdjacencyMapper.CalculateAdjacencyData(vertices.AsArray(), indices.AsArray(), posMap, out var adjacencyList, out var adjacencyMapper, out var connectedMap, Allocator.Temp);
            Profiler.EndSample();

            SerializedAdjacencyList = new int[adjacencyList.Length];
            SerializedAdjacencyMapper = new int[adjacencyMapper.Length];
            SerializedIndices = new int[indices.Length];
            adjacencyList.AsArray().CopyTo(SerializedAdjacencyList);
            adjacencyMapper.AsArray().CopyTo(SerializedAdjacencyMapper);
            indices.AsArray().CopyTo(SerializedIndices);
            Profiler.EndSample();

            mda.Dispose();

#if UNITY_EDITOR
            LastCacheDate = System.DateTime.Now.ToShortDateString() + " " + System.DateTime.Now.ToShortTimeString();
#endif
        }
    }
}