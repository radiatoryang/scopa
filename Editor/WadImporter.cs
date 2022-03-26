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
    /// custom Unity importer that detects .WAD files in /Assets/ and automatically imports textures (and generates materials)
    /// </summary>
    [ScriptedImporter(1, "wad")]
    public class WadImporter : ScriptedImporter
    {
        [Tooltip("(optional) override default WAD import settings with your own (texture filter mode, compression, material generation, etc.)... create via Project tab > Create > Scopa > Scopa Wad Config")]
        public ScopaWadConfig config;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var filepath = Application.dataPath + ctx.assetPath.Substring("Assets".Length);

            // if no config, then generate one for now
            var tempConfig = config != null ? config : ScriptableObject.CreateInstance<ScopaWadConfig>();

            var wad = ScopaCore.ParseWad(filepath);
            var textures = ScopaCore.BuildWadTextures(wad, tempConfig);

            foreach (var tex in textures) {
                ctx.AddObjectToAsset(tex.name, tex);

                if (tempConfig.generateMaterials) {
                    var newMaterial = ScopaCore.BuildMaterialForTexture(tex, tempConfig);
                    ctx.AddObjectToAsset(tex.name, newMaterial);
                }
            }
            
            // generate atlas sample thumbnail, set as main asset
            int atlasSize = tempConfig.GetAtlasSize();
            var atlas = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, false, tempConfig.useLinearColorSpace  );
            atlas.name = wad.Name;
            atlas.PackTextures(textures.ToArray(), 0, atlasSize);
            atlas.Compress(tempConfig.compressTextures);
            ctx.AddObjectToAsset(atlas.name, atlas);
            ctx.SetMainObject(atlas);
        }

 
    }

}