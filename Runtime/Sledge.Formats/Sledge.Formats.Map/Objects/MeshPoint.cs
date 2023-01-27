using System.Numerics;

namespace Sledge.Formats.Map.Objects
{
    public class MeshPoint
    {
        public int X { get; set; }
        public int Y { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Normal { get; set; }
        public Vector3 Texture { get; set; }
    }
}