using UnityEngine;
using System;
using System.Globalization;

namespace Scopa {
    public static class Vector3Extensions {

        public static Vector3 Abs(this Vector3 vec) {
            return new Vector3(
                Mathf.Abs(vec.x), 
                Mathf.Abs(vec.y),
                Mathf.Abs(vec.z)
            );
        }
 
        public static Vector3 Parse(string x, string y, string z, NumberStyles ns, IFormatProvider provider) {
            return new Vector3(
                float.Parse(x, ns, provider), 
                float.Parse(y, ns, provider),
                float.Parse(z, ns, provider)
            );
        }

        public static bool Approximately(Vector3 a, Vector3 b, float delta = 0.0001f) {
            if ( Mathf.Abs( a.x - b.x) > delta)
                return false;
            if ( Mathf.Abs( a.y - b.y) > delta)
                return false;
            if ( Mathf.Abs( a.z - b.z) > delta)
                return false;
            
            return true;
        }

    }
}