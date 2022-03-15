using UnityEngine;

namespace Scopa {
    public static class PlaneExtensions {

        public static Vector3 PointOnPlane(this Plane plane) {
            return plane.normal * plane.distance;
        }

    }

}