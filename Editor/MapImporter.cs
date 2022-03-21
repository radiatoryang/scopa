using UnityEngine;
using UnityEditor;

#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#elif UNITY_2017_1_OR_NEWER
using UnityEditor.Experimental.AssetImporters;
#endif

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

            var mapFile = Scopa.Parse(filepath);
            var defaultMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");
            var gameObject = Scopa.BuildMapIntoGameObject(mapFile, defaultMaterial, out var meshList);

            // (Only the 'Main Asset' is eligible to become a Prefab.)
            ctx.AddObjectToAsset(gameObject.name, gameObject);
            foreach ( var mesh in meshList ) {
                ctx.AddObjectToAsset(mesh.name, mesh);
            }
            ctx.SetMainObject(gameObject);

            // Assets must be assigned a unique identifier string consistent across imports
            // ctx.AddObjectToAsset("my Material", material);

            // Assets that are not passed into the context as import outputs must be destroyed
            // var tempMesh = new Mesh();
            // DestroyImmediate(tempMesh);
        }
    }

}