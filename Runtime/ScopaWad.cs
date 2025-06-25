using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Scopa.Formats.Texture.Wad;
using Scopa.Formats.Texture.Wad.Lumps;
using Scopa.Formats;
using Scopa.Formats.Id;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;



#if UNITY_EDITOR
using System.Diagnostics;
using UnityEditor;
using Debug = UnityEngine.Debug;
#endif

namespace Scopa {
    /// <summary> main class for WAD import and export </summary>
    public static class ScopaWad {

        // static buffers for all reading and writing operations, to try to reduce GC
        static Color32[] palette = new Color32[256];
        public static Texture2D resizedTexture { get; private set; }
        static WuColorQuantizer quantizer;

        #region WAD Reading

        public static WadFile ParseWad(string fileName)
        {
            using (var fStream = System.IO.File.OpenRead(fileName))
            {
                var newWad = new WadFile(fStream);
                newWad.Name = System.IO.Path.GetFileNameWithoutExtension(fileName);
                return newWad;
            }
        }

        public static List<Texture2D> BuildWadTextures(WadFile wad, ScopaWadConfig config) {
            if ( wad == null || wad.Entries == null || wad.Entries.Count == 0) {
                Debug.LogError("Couldn't parse WAD file " + wad.Name);
            }

            var textureList = new List<Texture2D>();

            foreach ( var entry in wad.Entries ) {
                if ( entry.Type != LumpType.RawTexture && entry.Type != LumpType.MipTexture )
                    continue;

                var texData = (wad.GetLump(entry) as MipTexture);
                // Debug.Log(entry.Name);
                // Debug.Log( "BITMAP: " + string.Join(", ", texData.MipData[0].Select( b => b.ToString() )) );
                // Debug.Log( "PALETTE: " + string.Join(", ", texData.Palette.Select( b => b.ToString() )) );

                // Half-Life GoldSrc textures use individualized 256 color palettes; Quake textures will have a reference to the hard-coded Quake palette
                var width = System.Convert.ToInt32(texData.Width);
                var height = System.Convert.ToInt32(texData.Height);

                for (int i=0; i<256; i++) {
                    palette[i] = new Color32( texData.Palette[i*3], texData.Palette[i*3+1], texData.Palette[i*3+2], 0xff );
                }

                // the last color is reserved for transparency
                var paletteHasTransparency = false;
                if ( (palette[255].r == QuakePalette.Data[255*3] && palette[255].g == QuakePalette.Data[255*3+1] && palette[255].b == QuakePalette.Data[255*3+2])
                    || (palette[255].r == 0x00 && palette[255].g == 0x00 && palette[255].b == 0xff) ) {
                    paletteHasTransparency = true;
                    palette[255] = new Color32(0x00, 0x00, 0x00, 0x00);
                }
                
                var mipSize = texData.MipData[0].Length;
                var pixels = new Color32[mipSize];
                var usesTransparency = false;

                // for some reason, WAD texture bytes are flipped? have to unflip them for Unity
                for( int y=0; y < height; y++) {
                    for (int x=0; x < width; x++) {
                        int paletteIndex = texData.MipData[0][(height-1-y)*width + x];
                        pixels[y*width+x] = palette[paletteIndex];
                        if ( !usesTransparency && paletteHasTransparency && paletteIndex == 255) {
                            usesTransparency = true;
                        }
                    }
                }

                // we have all pixel color data now, so we can build the Texture2D
                var newTexture = new Texture2D( width, height, usesTransparency ? TextureFormat.RGBA32 : TextureFormat.RGB24, true, config.linearColorspace);
                newTexture.name = texData.Name.ToLowerInvariant();
                newTexture.SetPixels32(pixels);
                #if UNITY_EDITOR
                newTexture.alphaIsTransparency = usesTransparency;
                #endif
                newTexture.filterMode = config.filterMode;
                newTexture.anisoLevel = config.anisoLevel;
                newTexture.Apply();
                if ( config.compressTextures ) {
                    newTexture.Compress(false);
                }
                textureList.Add( newTexture );
                
            }

            return textureList;
        }

        public static Material BuildMaterialForTexture( Texture2D texture, ScopaWadConfig config ) {
            var material = GraphicsFormatUtility.HasAlphaChannel(texture.graphicsFormat) ? GenerateMaterialAlpha(config) : GenerateMaterialOpaque(config);
            material.name = texture.name;
            material.mainTexture = texture;

            return material;
        }

        public static Material GenerateMaterialOpaque( ScopaWadConfig config ) {
            Material material;

            if ( config.opaqueTemplate != null ) {
                material = new Material(config.opaqueTemplate);
            }
            else {
                if (GraphicsSettings.currentRenderPipeline != null) {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    material.SetFloat("_Smoothness", 0.1f);
                } else {
                    material = new Material(Shader.Find("Standard"));
                    material.SetFloat("_Glossiness", 0.1f);
                }
            }

            return material;
        }

        public static Material GenerateMaterialAlpha( ScopaWadConfig config ) {
			Material material;

			if ( config.alphaTemplate != null ) {
				material = new Material(config.alphaTemplate);
			}
			else {
                if (GraphicsSettings.currentRenderPipeline != null) {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    material.SetFloat("_Smoothness", 0.1f);
                    material.SetFloat("_AlphaClip", 1);
                    material.SetFloat("_AlphaToMask", 1);
                } else {
                    material = new Material(Shader.Find("Standard"));
                    material.SetFloat("_Glossiness", 0.1f);
                    material.SetFloat("_Mode", 1);
                    material.EnableKeyword("_ALPHATEST_ON");
                    material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.renderQueue = 2450;
                }
			}

            return material;
        }

        #endregion
        #region WAD Writing

        const int MAX_WAD_NAME_LENGTH = 16;
        const string ALPHABET = "abcdefghijklmnopqrstuvwxyz";

        public static string SanitizeWadTextureName(string input) {
            var newInput = input.Replace(" ", "").ToLowerInvariant();
            newInput = newInput.Substring(0, Mathf.Min(newInput.Length, MAX_WAD_NAME_LENGTH) );
            if (newInput != input)
                Debug.Log($"ScopaWad: texture was renamed from {input} >> {newInput}. WAD filenames have a limit of 16 characters + are all lowercase.");
            return newInput;
        }

        public static void SaveWad3File(string filepath, ScopaWadCreator wadConfig) {
            var wadData = GenerateWad3Data( Path.GetFileNameWithoutExtension(filepath), wadConfig);
            if (wadData == null)
                return;

            using (var fStream = System.IO.File.OpenWrite(filepath))
            {
                wadData.Write(fStream);
            }
            Debug.Log("Scopa saved WAD3 file to " + filepath);
        }

        static WadFile GenerateWad3Data(string wadName, ScopaWadCreator wadConfig) {
            #if UNITY_EDITOR
            var wadTimer = new Stopwatch();
            wadTimer.Start();
            #endif

            var duplicateNames = new List<string>();
            var texNames = new List<string>();
            var newWad = new WadFile(Formats.Texture.Wad.Version.Wad3);
            newWad.Name = wadName;

            var whiteTexture = new Texture2D(32, 32);
            Color[] pixels = Enumerable.Repeat(Color.white, 32*32).ToArray();
            whiteTexture.SetPixels(pixels);
            whiteTexture.Apply();

            for (int i1 = 0; i1 < wadConfig.materials.Length; i1++) {
                Material mat = wadConfig.materials[i1];
                if (mat == null) {
                    Debug.LogWarning($"{wadName}: materials slot {i1} is empty.");
                    continue;
                }

                var hasDuplicateName = false;
                var texName = SanitizeWadTextureName(mat.name);

                var texture = mat.mainTexture;
                if (texture == null) {
                    texture = whiteTexture;
                    Debug.LogWarning($"{wadName}: {mat.name} doesn't have a mainTexture! Make sure the shader has a _MainTex or [MainTexture] property, and assign a Texture in the material!... Defaulting to a 32x32 white texture.");
                }

                // duplicate texture name handling
                for( int c=0; texNames.Contains(texName); c++ ) {
                    hasDuplicateName = true;
                    texName = texName.Substring(0, texName.Length-1);
                    if (c <= 9) {
                        texName += c.ToString(); // increment by number
                    } else if (c <= 9+ALPHABET.Length) { 
                        texName += ALPHABET[c-10]; // try incrementing by letter
                    } else {
                        texName = "";
                        for(int r=0; r<=MAX_WAD_NAME_LENGTH; r++) { // give up and generate random string of letters
                            texName += ALPHABET[ UnityEngine.Random.Range(0, ALPHABET.Length) ];
                        }
                        break;
                    }
                }
                // Debug.Log("started working on " + texName);

                var mipTex = new MipTextureLump();
                mipTex.Name = texName;
                mipTex.Width = System.Convert.ToUInt32(texture.width / (int)wadConfig.resolution);
                mipTex.Height = System.Convert.ToUInt32(texture.height / (int)wadConfig.resolution);
                mipTex.NumMips = 4; // all wad3 textures always have 3 mips
                // Debug.Log($"{mipTex.Name} is {mipTex.Width} x {mipTex.Height}");

                bool isTransparent = GraphicsFormatUtility.HasAlphaChannel(mat.mainTexture.graphicsFormat);

                mipTex.MipData = QuantizeToMipmap( texture, mat.color, isTransparent, (int)wadConfig.resolution, out var palette );

                mipTex.Palette = new byte[palette.Length * 3];
                for( int i=0; i<palette.Length; i++) {
                    mipTex.Palette[i*3] = palette[i].r;
                    mipTex.Palette[i*3+1] = palette[i].g;
                    mipTex.Palette[i*3+2] = palette[i].b;
                }

                newWad.AddLump( texName, mipTex );
                texNames.Add(texName);
                if (hasDuplicateName)
                    duplicateNames.Add(texName);
            }

            if (texNames.Count == 0) {
                Debug.LogError($"Scopa couldn't generate WAD file {wadName}: you must add at least one valid material to the config.");
                return null;
            }

            #if UNITY_EDITOR
            wadTimer.Stop();
            Debug.Log($"ScopaWad finished generating {wadName} in {wadTimer.ElapsedMilliseconds} ms" 
                + (duplicateNames.Count > 0 ? $"... however, some textures were renamed to avoid duplicates: {string.Join(", ", duplicateNames)}" : "")
            );
            #endif
            return newWad;
        }

        public static byte[][] QuantizeToMipmap(Texture mainTexture, Color colorTint, bool isTransparent, int resizeFactor, out Color32[] fixedPalette, bool generatePalette=true)
        {
            // we have to do this in two passes, with two render textures (to bypass texture read/write limits + for fast processing of pixels)

            // pass 1: read the texture, and while we're at it, tint it too
            ResizeCopyToBuffer(
                (Texture2D)mainTexture, 
                new Color(colorTint.r, colorTint.g, colorTint.b, 1f), // ignore colorTint alpha when tinting the texture
                Mathf.Max(Mathf.RoundToInt(mainTexture.width / resizeFactor), 4), 
                Mathf.Max(Mathf.RoundToInt(mainTexture.height / resizeFactor), 4)
            ); 
            var width = resizedTexture.width;
            var height = resizedTexture.height;

            if ( generatePalette ) {
                
                var bytes = resizedTexture.GetRawTextureData();

                if (quantizer == null)
                    quantizer = new WuColorQuantizer();

                var result = quantizer.Quantize(bytes, 255); // only generate 255 colors (last palette color is for transparency)

                // convert quantizer byte array (ARGB) to Color32
                fixedPalette = new Color32[256];
                for(int i = 0; i < result.Palette.Length; i+=4) {
                    var color = new Color32(result.Palette[i], result.Palette[i+1], result.Palette[i+2], 0xFF);
                    fixedPalette[i/4] = color;
                }
                fixedPalette[255] = new Color32(0, 0, 0xFF, 0xFF); // last palette entry is always blue 255 for transparency

                var mipmap = new byte[4][];

                // mip 0: read directly from the quantizer
                mipmap[0] = new byte[width*height];
                for( int y=0; y<height; y++) {
                    for( int x=0; x<width; x++) {
                        var index = Mathf.RoundToInt(y*width+x);
                        if (isTransparent && (int)bytes[index*4+3] < 128) // if original has a transparent pixel, use reserved palette index 0xFF
                            mipmap[0][(height-1-y)*width + x] = 0xFF;
                        else
                            mipmap[0][(height-1-y)*width + x] = result.Bytes[index];
                    }
                }

                // mips 1-3: resize and read the alpha
                for( int mip=1; mip<4; mip++) {
                    int factor = Mathf.RoundToInt( Mathf.Pow(2, mip) );
                    mipmap[mip] = new byte[ (width/factor) * (height/factor) ];

                    ResizeCopyToBuffer(resizedTexture, Color.white, width/factor, height/factor, fixedPalette, isTransparent, mip == 1 ? 0.454545f : 1f);
                    var indexEncodedAsPixels = resizedTexture.GetPixels();

                    for( int y=0; y<height/factor; y++) {
                        for( int x=0; x<width/factor; x++) {
                            // recover palette index from alpha
                            var index = Mathf.RoundToInt(indexEncodedAsPixels[y*width/factor+x].a * 255f);
                            // textures are vertically flipped, so have to unflip them
                            mipmap[mip][(height/factor-1-y)*width/factor + x] = System.Convert.ToByte(index);
                        }
                    }
                }
                return mipmap;

            } else {
                fixedPalette = new Color32[256];
                for (int i=0; i<256; i++) {
                    fixedPalette[i] = new Color32( QuakePalette.Data[i*3], QuakePalette.Data[i*3+1], QuakePalette.Data[i*3+2], 0xff );
                }
                
                // pass 2: now that we have a color palette, use render texture to palettize and generate mipmaps all at once
                ResizeCopyToBuffer((Texture2D)mainTexture, colorTint, width, height, fixedPalette, isTransparent);
                var mipmap = new byte[4][];
                for( int mip=0; mip<4; mip++) {
                    int factor = Mathf.RoundToInt( Mathf.Pow(2, mip) );
                    mipmap[mip] = new byte[ (width/factor) * (height/factor) ];
                    
                    var indexEncodedAsPixels = resizedTexture.GetPixels(mip);
                    for( int y=0; y<height/factor; y++) {
                        for( int x=0; x<width/factor; x++) {
                            // textures are vertically flipped, so have to unflip them
                            // also, ResizeCopyToBuffer saves the palette index in the alpha channel
                            var index = Mathf.RoundToInt(indexEncodedAsPixels[y*width/factor+x].a * 255f);
                            // if ( x==0 && y==0 && mip==0) {
                            //     Debug.Log($"pixel 0 for {material.name} is {index} {Mathf.CeilToInt(index.a * 255f)} = {fixedPalette[Mathf.CeilToInt(index.a * 255f)]}");
                            // }
                            mipmap[mip][(height/factor-1-y)*width/factor + x] = System.Convert.ToByte(index);
                        }
                    }
                }
                return mipmap;
            }
        }


        // code from https://github.com/ababilinski/unity-gpu-texture-resize
        // and https://support.unity.com/hc/en-us/articles/206486626-How-can-I-get-pixels-from-unreadable-textures-
        public static void ResizeCopyToBuffer(Texture2D source, Color tint, int targetX, int targetY, Color32[] palette = null, bool isTransparent = false, float gamma = 1f) {
            RenderTexture tmp = RenderTexture.GetTemporary( 
                targetX,
                targetY,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.sRGB, 
                1
            );

            // Blit the pixels on texture to the RenderTexture
            if ( palette != null ) {
                var mat = new Material( Shader.Find("Hidden/PalettizeBlit") );
                mat.SetColor("_Color", tint);
                mat.SetColorArray( "_Colors", palette.Select( c => new Color(c.r / 255f, c.g / 255f, c.b / 255f) ).ToArray() );
                mat.SetInteger("_AlphaIsTransparency", isTransparent ? 1 : 0);
                mat.SetFloat("_Gamma", gamma);
                Graphics.Blit(source, tmp, mat);
            } else {
                var mat = new Material( Shader.Find("Hidden/BlitTint") );
                mat.SetColor("_Color", tint);
                Graphics.Blit(source, tmp, mat);
            }

            // Backup the currently set RenderTexture
            RenderTexture previous = RenderTexture.active;

            // Set the current RenderTexture to the temporary one we created
            RenderTexture.active = tmp;

            // Create a new readable Texture2D to copy the pixels to it
            resizedTexture = new Texture2D(targetX, targetY, TextureFormat.RGBA32, false, false);
            resizedTexture.filterMode = FilterMode.Bilinear;

            // Copy the pixels from the RenderTexture to the new Texture
            resizedTexture.ReadPixels(new Rect(0, 0, targetX, targetY), 0, 0, false);
            resizedTexture.Apply();

            // Reset the active RenderTexture
            RenderTexture.active = previous;

            // Release the temporary RenderTexture
            RenderTexture.ReleaseTemporary(tmp);
        }

#endregion
    }

}
