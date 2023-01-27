using System;
using System.IO;

namespace Scopa.Formats.Texture.Wad.Lumps
{
    public class ColorMap2Lump : ILump
    {
        public LumpType Type => LumpType.ColorMap2;

        public ColorMap2Lump(BinaryReader br)
        {
            // Who knows
        }

        public int Write(BinaryWriter bw)
        {
            throw new NotSupportedException("ColorMap2 is not a supported lump type.");
        }
    }
}