using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Scopa.Formats.Texture.Wad;
using Scopa.Formats.Texture.Wad.Lumps;
using Scopa.Formats.Id;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Scopa {
    /// <summary> main class for WAD import and export </summary>
    public static class ScopaWad {

        // static buffers for all reading and writing operations, to try to reduce GC
        static Color32[] palette = new Color32[256];
        static List<ColorBucket> buckets = new List<ColorBucket>(128*128);
        static List<ColorBucket> newBuckets = new List<ColorBucket>(256);
        static Texture2D resizedTexture;

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
                newTexture.alphaIsTransparency = usesTransparency;
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
            var material = texture.alphaIsTransparency ? 
                (config.alphaTemplate != null ? config.alphaTemplate : GenerateDefaultMaterialAlpha())
                : (config.opaqueTemplate != null ? config.opaqueTemplate : GenerateDefaultMaterialOpaque());
            material.name = texture.name;
            material.mainTexture = texture;

            return material;
        }

        public static Material GenerateDefaultMaterialOpaque() {
            // TODO: URP, HDRP
            var material = new Material( Shader.Find("Standard") );
            material.SetFloat("_Glossiness", 0.1f);
            return material;
        }

        public static Material GenerateDefaultMaterialAlpha() {
            // TODO: URP, HDRP
            var material = new Material( Shader.Find("Standard") );
            material.SetFloat("_Glossiness", 0.1f);
            material.SetFloat("_Mode", 1);
            material.EnableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 2450;
            return material;
        }

        #endregion
        #region WAD Writing

        public static void SaveWad3File(string filepath, Material[] mats) {
            var wadData = GenerateWad3Data( Path.GetFileNameWithoutExtension(filepath), mats);
            using (var fStream = System.IO.File.OpenWrite(filepath))
            {
                wadData.Write(fStream);
            }
            Debug.Log("saved WAD3 file to " + filepath);
        }

        static WadFile GenerateWad3Data(string wadName, Material[] mats, int resizeFactor = 4) {
            var newWad = new WadFile(Formats.Texture.Wad.Version.Wad3);
            newWad.Name = wadName;
            foreach ( var mat in mats ) {
                var texName = mat.name.ToLowerInvariant();
                texName = texName.Substring(0, Mathf.Min(texName.Length, 16) ); // texture names are limited to 16 characters

                var mipTex = new MipTextureLump();
                mipTex.Name = texName;
                mipTex.Width = System.Convert.ToUInt32(mat.mainTexture.width / resizeFactor);
                mipTex.Height = System.Convert.ToUInt32(mat.mainTexture.height / resizeFactor);
                mipTex.NumMips = 4; // all wad3 textures always have 3 mips

                mipTex.MipData = QuantizeToMipmap( (Texture2D)mat.mainTexture, resizeFactor, out var palette );

                mipTex.Palette = new byte[palette.Length * 3];
                for( int i=0; i<palette.Length; i++) {
                    mipTex.Palette[i*3] = palette[i].r;
                    mipTex.Palette[i*3+1] = palette[i].g;
                    mipTex.Palette[i*3+2] = palette[i].b;
                }

                newWad.AddLump( texName, mipTex );
            }
            return newWad;
        }

        // median cut color palette quanitization code
        // adapted from https://github.com/bacowan/cSharpColourQuantization/blob/master/ColourQuantization/MedianCut.cs
        // used under Unlicense License
        /// <summary> actual palette color count will be -=1, to insert blue 255 at the end for transparency </summary>
        public static byte[][] QuantizeToMipmap(Texture2D original, int resizeFactor, out Color32[] fixedPalette, int paletteColorCount = 256)
        {
            // var colours = Enumerable.Range(0, bitmap.height)
            //     .SelectMany(y => Enumerable.Range(0, bitmap.width)
            //         .Select(x => bitmap.GetPixel(x, y)));
            ResizeCopyToBuffer(original, original.width / resizeFactor, original.height / resizeFactor); 
            // we use Color32 because it's faster + avoids floating point comparison issues with HasColor() + we need to write out bytes anyway
            var colors = resizedTexture.GetPixels32();
            buckets.Clear();
            paletteColorCount -= 1; // reserve space for blue 255

            // build color buckets / palette groups
            while (buckets.Count < paletteColorCount)
            {
                newBuckets.Clear();
                for (var i = 0; i < buckets.Count; i++)
                {
                    if (newBuckets.Count + (buckets.Count - i) < paletteColorCount)
                    {
                        var split = buckets[i].Split();
                        newBuckets.Add(split.Item1);
                        newBuckets.Add(split.Item2);
                    }
                    else
                    {
                        newBuckets.AddRange(buckets.GetRange(i, buckets.Count - i));
                        break;
                    }
                }
                buckets = newBuckets;
            }
            fixedPalette = buckets.Select( bucket => bucket.Color ).Append(new Color32(0x00, 0x00, 0xff, 0xff)).ToArray();
            
            // use bucket lookup to convert colors to palette indices
            var width = resizedTexture.width;
            var height = resizedTexture.height;
            var mipmap = new byte[4][];
            for( int mip=0; mip<4; mip++) {
                int factor = Mathf.RoundToInt( Mathf.Pow(2, mip) );
                mipmap[mip] = new byte[ width/factor * height/factor ];
                for( int y=0; y<height; y+=factor ) {
                    for (int x=0; x<width; x+=factor ) {
                        mipmap[mip][y*width+x] = System.Convert.ToByte( 
                            buckets.IndexOf(
                                buckets.First(b => b.HasColor(colors[y*width+x]))
                            ) 
                        );
                    }
                }
            }
            return mipmap;
        }

        // code from https://github.com/ababilinski/unity-gpu-texture-resize
        // very fast in most cases?
        public static void ResizeCopyToBuffer(Texture2D source, int targetX, int targetY, bool mipmap = false, FilterMode filter = FilterMode.Bilinear) {
            //create a temporary RenderTexture with the target size
            var rt = RenderTexture.GetTemporary(targetX, targetY, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);

            //set the active RenderTexture to the temporary texture so we can read from it
            RenderTexture.active = rt;
            
            //Copy the texture data on the GPU - this is where the magic happens [(;]
            Graphics.Blit(source, rt);

            // initialize the texture obj to the target values (this sets the pixel data as undefined)
            if ( resizedTexture == null) {
                resizedTexture = new Texture2D(targetX, targetY, source.format, mipmap);
            } else {
                resizedTexture.Reinitialize(targetX, targetY, source.format, mipmap);
            }
            resizedTexture.filterMode = filter;

            try {
                // reads the pixel values from the temporary RenderTexture onto the resized texture
                resizedTexture.ReadPixels(new Rect(0, 0, targetX, targetY), 0, 0);
                // actually upload the changed pixels to the graphics card
                resizedTexture.Apply();
            } catch {
                Debug.LogError("Read/Write is not enabled on texture "+ source.name);
            }

            RenderTexture.ReleaseTemporary(rt);
        }

        public class ColorBucket
        {
            private readonly IDictionary<Color32, int> colors;

            public Color32 Color { get; }

            public ColorBucket(IEnumerable<Color32> colors)
            {
                this.colors = colors.ToLookup(c => c)
                    .ToDictionary(c => c.Key, c => c.Count());
                this.Color = Average(this.colors);
            }

            public ColorBucket(IEnumerable<KeyValuePair<Color32, int>> enumerable)
            {
                this.colors = enumerable.ToDictionary(c => c.Key, c => c.Value);
                this.Color = Average(this.colors);
            }

            private static Color32 Average(IEnumerable<KeyValuePair<Color32, int>> colors)
            {
                var totals = colors.Sum(c => c.Value);
                return new Color32(
                    r: System.Convert.ToByte(colors.Sum(c => c.Key.r * c.Value) / totals),
                    g: System.Convert.ToByte(colors.Sum(c => c.Key.g * c.Value) / totals),
                    b: System.Convert.ToByte(colors.Sum(c => c.Key.b * c.Value) / totals),
                    a: 0xff
                );
            }

            public bool HasColor(Color32 color)
            {
                return colors.ContainsKey(color);
            }

            public Tuple<ColorBucket, ColorBucket> Split()
            {
                var redRange = colors.Keys.Max(c => c.r) - colors.Keys.Min(c => c.r);
                var greenRange = colors.Keys.Max(c => c.g) - colors.Keys.Min(c => c.g);
                var blueRange = colors.Keys.Max(c => c.b) - colors.Keys.Min(c => c.b);

                Func<Color32, int> sorter;
                if (redRange > greenRange)
                {
                    if (redRange > blueRange)
                    {
                        sorter = c => System.Convert.ToInt32(c.r);
                    }
                    else
                    {
                        sorter = c => System.Convert.ToInt32(c.b);
                    }
                }
                else
                {
                    if (greenRange > blueRange)
                    {
                        sorter = c => System.Convert.ToInt32(c.g);
                    }
                    else
                    {
                        sorter = c => System.Convert.ToInt32(c.b);
                    }
                }

                var sorted = colors.OrderBy(c => sorter(c.Key));

                var firstBucketCount = sorted.Count() / 2;

                var bucket1 = new ColorBucket(sorted.Take(firstBucketCount));
                var bucket2 = new ColorBucket(sorted.Skip(firstBucketCount));
                return new Tuple<ColorBucket, ColorBucket>(bucket1, bucket2);
            }
        }

        #endregion
    }
}