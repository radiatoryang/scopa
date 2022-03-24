using System.IO;

namespace Scopa.Formats.Texture.Wad.Lumps
{
    public class PaletteLump : ILump
    {
        public LumpType Type => LumpType.Palette;
        public byte[] PaletteData { get; set; }
        const int Length = 256 * 3;

        public PaletteLump(BinaryReader br)
        {
            PaletteData = br.ReadBytes(Length);
        }

        public int Write(BinaryWriter bw)
        {
            bw.Write(PaletteData, 0, Length);
            return Length;
        }
    }
}