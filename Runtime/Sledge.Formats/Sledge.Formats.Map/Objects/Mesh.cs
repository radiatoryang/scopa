using System.Collections.Generic;

namespace Sledge.Formats.Map.Objects
{
    public class Mesh : Surface
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public List<MeshPoint> Points { get; set; }

        public Mesh()
        {
            Points = new List<MeshPoint>();
        }
    }
}