using UnityEngine;
using UnityEditor;

#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif

using Scopa;
using System.IO;

namespace Scopa.Editor {

    /// <summary>
    /// custom Unity importer that detects .BSP files in /Assets/
    /// and automatically imports them like any other 3D mesh
    /// </summary>
    [ScriptedImporter(1, "wad")]
    public class WadImporter : ScriptedImporter
    {

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var filepath = Application.dataPath + ctx.assetPath.Substring("Assets".Length);

            var wad = Scopa.ParseWad(filepath);
            var textures = Scopa.BuildWadTextures(wad);

            foreach (var tex in textures) {
                ctx.AddObjectToAsset(tex.name, tex);
                var newMaterial = new Material( Shader.Find("Standard") );
                newMaterial.name = tex.name;
                newMaterial.mainTexture = tex;
                // TODO: is it a transparent texture? if so, use cutout mode
                ctx.AddObjectToAsset(tex.name, newMaterial);
            }
            
            // generate atlas sample thumbnail, set as main asset
            var atlas = new Texture2D(4096, 4096);
            atlas.name = wad.Name;
            atlas.PackTextures(textures.ToArray(), 0, 4096);
            atlas.Compress(false);
            ctx.AddObjectToAsset(atlas.name, atlas);
            ctx.SetMainObject(atlas);


            // var mapFile = Scopa.Parse(filepath);
 
            // var defaultMaterial = AssetDatabase.LoadAssetAtPath<Material>( "Packages/com.radiatoryang.scopa/Runtime/Textures/BlockoutDark.mat" );
            // if ( defaultMaterial == null ) {
            //     defaultMaterial = AssetDatabase.LoadAssetAtPath<Material>( AssetDatabase.FindAssets("BlockoutDark.mat")[0] );
            //     if ( defaultMaterial == null ) {
            //         defaultMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");
            //     }
            // }
            // var gameObject = Scopa.BuildMapIntoGameObject(mapFile, defaultMaterial, out var meshList);

            // // (Only the 'Main Asset' is eligible to become a Prefab.)
            // ctx.AddObjectToAsset(gameObject.name, gameObject);
            // foreach ( var mesh in meshList ) {
            //     ctx.AddObjectToAsset(mesh.name, mesh);
            // }
            // ctx.SetMainObject(gameObject);

            // Assets must be assigned a unique identifier string consistent across imports
            // ctx.AddObjectToAsset("my Material", material);

            // Assets that are not passed into the context as import outputs must be destroyed
            // var tempMesh = new Mesh();
            // DestroyImmediate(tempMesh);
        }

 
    }

}