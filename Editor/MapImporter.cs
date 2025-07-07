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
    /// custom Unity importer that detects MAP, RMF, VMF, or JMF files in /Assets/
    /// and automatically imports them like any other 3D mesh
    /// </summary>
    [ScriptedImporter(1, new string[] {"map", "rmf", "vmf", "jmf"}, 6900)]
    public class MapImporter : ScriptedImporter
    {
        public ScopaMapConfigAsset externalConfig;
        public ScopaMapConfig config;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var currentConfig = externalConfig != null ? externalConfig.config : config;

            if ( currentConfig == null ) {
                currentConfig = new ScopaMapConfig();
            }

            var filepath = Application.dataPath + ctx.assetPath.Substring("Assets".Length);

            var gameObject = ScopaCore.ImportMap(filepath, currentConfig, out var meshList);
            ctx.AddObjectToAsset(gameObject.name, gameObject);

            // we have to serialize every mesh as a subasset, or else it won't get saved
            foreach ( var meshResult in meshList ) {
                if (meshResult == null)
                    continue;
                    
                var mesh = meshResult.GetMesh();
                if (mesh != null) {
                    ctx.AddObjectToAsset(mesh.name, mesh);
                    EditorUtility.SetDirty(mesh);
                //    PrefabUtility.RecordPrefabInstancePropertyModifications(mesh);
                }
            }
            ctx.SetMainObject(gameObject);

            EditorUtility.SetDirty(gameObject);
        }
    }

}