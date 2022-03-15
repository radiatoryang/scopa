using UnityEngine;
using System;
using System.Globalization;

namespace Scopa {
    public class NumericsExtensions {
        public static Vector3 Parse(string x, string y, string z, NumberStyles ns, IFormatProvider provider) {
            return new Vector3(
                float.Parse(x, ns, provider), 
                float.Parse(y, ns, provider),
                float.Parse(z, ns, provider)
            );
        }

        public const double Epsilon = Double.Epsilon;
    }

    public class Util {
        public static void Assert(bool expression) {
            UnityEngine.Assertions.Assert.IsTrue(expression);
        }

        public static void Assert(bool expression, string message) {
            UnityEngine.Assertions.Assert.IsTrue(expression, message);
        }
    }
}