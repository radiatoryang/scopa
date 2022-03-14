using UnityEngine;

namespace Scopa.Formats.Map.Objects
{
    public class Camera
    {
        public Vector3 EyePosition { get; set; }
        public Vector3 LookPosition { get; set; }
        public bool IsActive { get; set; }
    }
}