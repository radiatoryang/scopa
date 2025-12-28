using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Ica.Utils.Tests
{
    public class MergedMeshDataTests
    {
        [Test]
        public void GetMergedVertices()
        {
            var paths1 = AssetDatabase.FindAssets("testMesh_Teapot");
            var asset1 = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(paths1[0]));
            var obj1 = (GameObject)Object.Instantiate(asset1);
            var mesh1 = obj1.GetComponent<MeshFilter>().sharedMesh;
            
            var paths2 = AssetDatabase.FindAssets("testMesh_Icosphere");
            var asset2 = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(paths2[0]));
            var obj2 = (GameObject)Object.Instantiate(asset2);
            var mesh2 = obj2.GetComponent<MeshFilter>().sharedMesh;

            var trueData = new List<Vector3>();
            var temp = new List<Vector3>();
            
            
            mesh1.GetVertices(trueData);
            mesh2.GetVertices(temp);
            
            trueData.AddRange(temp);
            
            
            
            var mda = Mesh.AcquireReadOnlyMeshData(new List<Mesh>(){mesh1,mesh2});
            var vertices = new NativeList<float3>(1,Allocator.Temp);
            var mapper = new NativeList<int>(1,Allocator.Temp);
            vertices.Add(new float3(1f,3f,4f));
            mda.GetMergedVertices(ref vertices,ref mapper);

            Assert.AreEqual(trueData,vertices.AsArray().Reinterpret<Vector3>());
            
            mda.Dispose();
        }
        
        [Test]
        public void GetMergedIndices()
        {
            var paths1 = AssetDatabase.FindAssets("testMesh_Teapot");
            var asset1 = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(paths1[0]));
            var obj1 = (GameObject)Object.Instantiate(asset1);
            var mesh1 = obj1.GetComponent<MeshFilter>().sharedMesh;
            
            var paths2 = AssetDatabase.FindAssets("testMesh_Icosphere");
            var asset2 = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(paths2[0]));
            var obj2 = (GameObject)Object.Instantiate(asset2);
            var mesh2 = obj2.GetComponent<MeshFilter>().sharedMesh;

            var trueData = new List<int>();
            
            var arr1 = mesh1.triangles;
            var arr2 = mesh2.triangles;
            var mesh1VertexCount = mesh1.vertexCount;

            for (int i = 0; i < arr2.Length; i++)
            {
                arr2[i] += mesh1VertexCount;
            }
            
            trueData.AddRange(arr1);
            trueData.AddRange(arr2);
            
            
            var mda = Mesh.AcquireReadOnlyMeshData(new List<Mesh>(){mesh1,mesh2});
            var indices = new NativeList<int>(1,Allocator.Temp);
            var mapper = new NativeList<int>(1,Allocator.Temp);
            indices.Add(5);
            mda.GetMergedIndices(ref indices,ref mapper);
            
            Assert.IsTrue(trueData.Count == indices.AsArray().Length);
            Assert.AreEqual(trueData,indices.AsArray());
            
            mda.Dispose();
        }
    }
}