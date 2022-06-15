using UnityEngine;
using UnityEditor;
using System.Diagnostics;

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
        public ScopaMapConfigAsset externalConfig;
        public ScopaMapConfig config;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var filepath = Application.dataPath + ctx.assetPath.Substring("Assets".Length);

            var currentConfig = externalConfig != null ? externalConfig.config : config;

            if ( currentConfig == null ) {
                currentConfig = new ScopaMapConfig();
            }

            var parseTimer = new Stopwatch();
            parseTimer.Start();
            var mapFile = ScopaCore.ParseMap(filepath, currentConfig);
            parseTimer.Stop();
 
            // try to find the default gridded blockout material... but if we can't find it, fallback to plain Unity gray material
            var defaultMaterial = currentConfig.defaultMaterial != null ? currentConfig.defaultMaterial : AssetDatabase.LoadAssetAtPath<Material>( "Packages/com.radiatoryang.scopa/Runtime/Textures/BlockoutDark.mat" );
            if ( defaultMaterial == null ) {
                defaultMaterial = AssetDatabase.LoadAssetAtPath<Material>( AssetDatabase.FindAssets("BlockoutDark.mat")[0] );
                if ( defaultMaterial == null ) {
                    defaultMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");
                }
            }

            // this is where the magic happens
            var buildTimer = new Stopwatch();
            buildTimer.Start();
            var gameObject = ScopaCore.BuildMapIntoGameObject(mapFile, defaultMaterial, currentConfig, out var meshList);
            buildTimer.Stop();

            ctx.AddObjectToAsset(gameObject.name, gameObject);

            // we have to serialize every mesh as a subasset, or else it won't get saved
            foreach ( var mesh in meshList ) {
                ctx.AddObjectToAsset(mesh.name, mesh);
                EditorUtility.SetDirty(mesh);
            //    PrefabUtility.RecordPrefabInstancePropertyModifications(mesh);
            }
            ctx.SetMainObject(gameObject);

            EditorUtility.SetDirty(gameObject);

            UnityEngine.Debug.Log($"imported {filepath}\n Parsed in {parseTimer.ElapsedMilliseconds} ms, Built in {buildTimer.ElapsedMilliseconds} ms");
            //PrefabUtility.RecordPrefabInstancePropertyModifications(gameObject);
        }
    }

}