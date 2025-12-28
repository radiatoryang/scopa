using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ica.Utils.Tests
{
    public class MeshUtilsTest
    {
        [Test]
        public void GetVerticesDataAsList()
        {
            var paths = AssetDatabase.FindAssets("testMesh_teapot");
            var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(paths[0]));
            var obj = (GameObject)Object.Instantiate(asset);
            var mesh = obj.GetComponent<MeshFilter>().sharedMesh;

            var trueData = mesh.vertices;
            
            var mda = Mesh.AcquireReadOnlyMeshData(mesh);
            var vertices = new NativeList<float3>(1,Allocator.Temp);
            vertices.Add(new float3(1f,3f,4f));
            mda[0].GetVerticesDataAsList(ref vertices);

            Assert.AreEqual(trueData,vertices.AsArray().Reinterpret<Vector3>());
            
            mda.Dispose();
        }


        [Test]
        public void GetAllIndicesAsList()
        {
            var paths = AssetDatabase.FindAssets("testMesh_teapot");
            var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(paths[0]));
            var obj = (GameObject)Object.Instantiate(asset);
            var mesh = obj.GetComponent<MeshFilter>().sharedMesh;

            var trueData = mesh.triangles;
            
            var mda = Mesh.AcquireReadOnlyMeshData(mesh);
            var indices = new NativeList<int>(1,Allocator.Temp);
            indices.Add(1);
            mda[0].GetAllIndicesDataAsList(ref indices);

            Assert.AreEqual(trueData,indices.AsArray());
            
            mda.Dispose();
        }
    }
    
    
    
    
}