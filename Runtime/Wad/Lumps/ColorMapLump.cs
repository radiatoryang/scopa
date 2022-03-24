using System;
using System.IO;

namespace Scopa.Formats.Texture.Wad.Lumps
{
    public class ColorMapLump : ILump
    {
        public LumpType Type => LumpType.ColorMap;

        public ColorMapLump(BinaryReader br)
        {
            // Who knows
        }

        public int Write(BinaryWriter bw)
        {
            throw new NotSupportedException("ColorMap is not a supported lump type.");
        }
    }
}