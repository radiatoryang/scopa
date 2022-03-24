namespace Scopa.Formats.Texture.Wad
{
    public enum LumpType : byte
    {
        Palette = 0x40, // Not used by any code
        ColorMap = 0x41, // Not used by any code
        Image = 0x42, // // Not used by any code. Simple image with any size
        MipTexture = 0x43, // Used by HL1 only. Power-of-16-sized world textures with 4 mipmaps
        RawTexture = 0x44, // Used by Quake 1 only. Same as Texture but without the palette
        ColorMap2 = 0x45, // Not used by any code
        Font = 0x46, // Used by HL1 only....maybe? Fixed-height font. Contains an image and font data (row, X offset and width of a character) for 256 ASCII characters. 
    }
}