using UnityEngine;
using UnityEditor;

#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#elif UNITY_2017_1_OR_NEWER
using UnityEditor.Experimental.AssetImporters;
#endif

using System.IO;
using Scopa;

namespace Scopa.Editor {

    /// <summary>
    /// custom Unity importer that detects .BSP files in /Assets/
    /// and automatically imports them like any other 3D mesh
    /// </summary>
    [ScriptedImporter(1, "map")]
    public class MapImporter : ScriptedImporter
    {

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var filepath = Application.dataPath + ctx.assetPath.Substring("Assets".Length);
            var mapFile = Scopa.Import(filepath);

            var mesh = Scopa.BuildMesh(mapFile.Worldspawn);
            // var position = JsonUtility.FromJson<Vector3>(File.ReadAllText(ctx.assetPath));

            // cube.transform.position = position;
            // cube.transform.localScale = new Vector3(m_Scale, m_Scale, m_Scale);

            // 'cube' is a GameObject and will be automatically converted into a prefab
            // (Only the 'Main Asset' is eligible to become a Prefab.)
            ctx.AddObjectToAsset("Worldspawn", mesh);
            ctx.SetMainObject(mesh);

            var material = new Material(Shader.Find("Standard"));
            material.color = Color.gray;

            // Assets must be assigned a unique identifier string consistent across imports
            ctx.AddObjectToAsset("my Material", material);

            // Assets that are not passed into the context as import outputs must be destroyed
            // var tempMesh = new Mesh();
            // DestroyImmediate(tempMesh);
        }
    }

}