using System.Numerics;

namespace Sledge.Formats.Map.Objects
{
    public class Surface
    {
        public string TextureName { get; set; }

        public Vector3 UAxis { get; set; }
        public Vector3 VAxis { get; set; }
        public float XScale { get; set; }
        public float YScale { get; set; }
        public float XShift { get; set; }
        public float YShift { get; set; }
        public float Rotation { get; set; }

        public int ContentFlags { get; set; }
        public int SurfaceFlags { get; set; }
        public float Value { get; set; }

        public float LightmapScale { get; set; }
        public string SmoothingGroups { get; set; }
    }
}