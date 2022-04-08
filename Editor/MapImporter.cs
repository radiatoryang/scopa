using UnityEngine;
using UnityEditor;

#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#else
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
        public ScopaMapConfig config;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var filepath = Application.dataPath + ctx.assetPath.Substring("Assets".Length);

            if ( config == null ) {
                config = new ScopaMapConfig();
            }

            var mapFile = ScopaCore.ParseMap(filepath, config);
 
            // try to find the default gridded blockout material... but if we can't find it, fallback to plain Unity gray material
            var defaultMaterial = config.defaultMaterial != null ? config.defaultMaterial : AssetDatabase.LoadAssetAtPath<Material>( "Packages/com.radiatoryang.scopa/Runtime/Textures/BlockoutDark.mat" );
            if ( defaultMaterial == null ) {
                defaultMaterial = AssetDatabase.LoadAssetAtPath<Material>( AssetDatabase.FindAssets("BlockoutDark.mat")[0] );
                if ( defaultMaterial == null ) {
                    defaultMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");
                }
            }

            // this is where the magic happens
            var gameObject = ScopaCore.BuildMapIntoGameObject(mapFile, defaultMaterial, config, out var meshList);
            ctx.AddObjectToAsset(gameObject.name, gameObject);

            // we have to serialize every mesh as a subasset, or else it won't get saved
            foreach ( var mesh in meshList ) {
                ctx.AddObjectToAsset(mesh.name, mesh);
            }
            ctx.SetMainObject(gameObject);
        }
    }

}