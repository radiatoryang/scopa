namespace Scopa.Formats.Texture.Wad
{
    public enum Version : uint
    {
        Wad2 = (byte)'W' | (byte)'A' << 8 | (byte)'D' << 16 | (byte)'2' << 24,
        Wad3 = (byte)'W' | (byte)'A' << 8 | (byte)'D' << 16 | (byte)'3' << 24
    }
}