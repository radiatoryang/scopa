using System;
using System.Globalization;
using System.Numerics;

namespace Scopa {
    public static class PlaneExtensions {
        public static bool IsOrthogonal(this Plane _plane) {
            if ( Math.Abs(_plane.Normal.X) > 0.01f && Math.Abs(_plane.Normal.X) < 0.99f ) {
                return false;
            } else if ( Math.Abs(_plane.Normal.Y) > 0.01f && Math.Abs(_plane.Normal.Y) < 0.99f ) {
                return false;
            } else if ( Math.Abs(_plane.Normal.Z) > 0.01f && Math.Abs(_plane.Normal.Z) < 0.99f ) {
                return false;
            }
            return true;
        }

        // public static ScopaMesh.Axis GetMainAxisToNormal(this UnityEngine.Plane _plane) {
        //     // VHE prioritises the axes in order of X, Y, Z.
        //     // so in Unity land, that's X, Z, and Y
        //     var norm = _plane.normal.Absolute();

        //     if (norm.x >= norm.y && norm.x >= norm.z) return ScopaMesh.Axis.X;
        //     if (norm.z >= norm.y) return ScopaMesh.Axis.Z;
        //     return ScopaMesh.Axis.Y;
        // }

    }
}