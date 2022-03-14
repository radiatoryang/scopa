using UnityEngine;

namespace Scopa {
    public static class PlaneExtensions {
        /// <summary>
        /// Gets the axis closest to the normal of this plane
        /// </summary>
        /// <returns>Vector3.UnitX, Vector3.UnitY, or Vector3.UnitZ depending on the plane's normal</returns>
        public static Vector3 GetClosestAxisToNormal(this Plane plane)
        {
            // VHE prioritises the axes in order of X, Y, Z.
            var norm = plane.normal.Abs();

            if (norm.x >= norm.y && norm.x >= norm.z) return Vector3.right;
            if (norm.y >= norm.z) return Vector3.up;
            return Vector3.forward;
        }

        public static Vector3 PointOnPlane(this Plane plane) {
            return plane.normal * plane.distance;
        }

    }

}