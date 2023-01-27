using System.Drawing;
using System.Numerics;

namespace Sledge.Formats.Map.Objects
{
    public class Camera
    {
        public Vector3 EyePosition { get; set; }
        public Vector3 LookPosition { get; set; }
        public bool IsActive { get; set; }
        public Color Color { get; set; }
    }
}