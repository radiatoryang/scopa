using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Scopa {
    /// <summary> ScriptableObject to use for configuring how Scopa imports WADs, even for runtime imports too.</summary>
    [CreateAssetMenu(fileName = "New ScopaWadConfig", menuName = "Scopa/WAD Config", order = 1)]
    public class ScopaWadConfigAsset : ScriptableObject {
        public ScopaWadConfig config = new ScopaWadConfig();
    }

    [System.Serializable]
    public class ScopaWadConfig {
        [Header("Textures")]
        [Tooltip("(default: false) WAD textures are more 'correct' in default Gamma sRGB color space, but you may prefer the washed-out look of importing them as Linear textures.")]
        public bool useLinearColorSpace = false;

        [Tooltip("(default: Point) For a retro pixel art Quake look, use Point. For a modern smoother look, use Bilinear or Trilinear.")]
        public FilterMode textureFilterMode = FilterMode.Point;

        [Tooltip("(default: true) Compression saves memory but adds noise. Good for most textures, bad for subtle gradients.")]
        public bool compressTextures = true;

        [Range(0, 16)]
        [Tooltip("(default: 1) Anisotropic filtering spends more GPU time to make textures slightly less blurry at grazing angles. Often used for ground textures. NOTE: Setting this to 0 means the texture will never use aniso, even if you Force Aniso on in the Quality Settings.")]
        public int anisoLevel = 1;

        [Header("Materials")]
        [Tooltip("(default: true) If enabled, Scopa will generate a Material for each texture.")]
        public bool generateMaterials = true;

        [Tooltip("(optional) override the templates that Scopa uses to generate Materials; make sure the shader has a _MainTex property (or [MainTexture] attribute) for Scopa to use!\n   - opaque: used for most brushes\n   - alpha: fences, grates, etc. with transparency")]
        public Material templateMaterialOpaque, templateMaterialAlpha;

        [Header("Misc")]
        [Tooltip("(default: Small 1024x1024) maximum size of the preview atlas texture for the entire WAD")]
        public AtlasSize atlasSize = AtlasSize.Tiny_512;

        public int GetAtlasSize() {
            switch(atlasSize) {
                case AtlasSize.Mini_256: return 256;
                case AtlasSize.Tiny_512: return 512;
                case AtlasSize.Small_1024: return 1024;
                case AtlasSize.Medium_2048: return 2048;
                case AtlasSize.Large_4096: return 4096;
                default: return 512;
            }
        }
    }

    public enum AtlasSize {
        Mini_256,
        Tiny_512,
        Small_1024,
        Medium_2048,
        Large_4096
    }
}

