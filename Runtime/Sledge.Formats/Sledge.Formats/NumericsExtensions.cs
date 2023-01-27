using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace Sledge.Formats
{
    public static class NumericsExtensions
    {
        public const float Epsilon = 0.0001f;

        // Vector2
        public static Vector3 ToVector3(this Vector2 self)
        {
            return new Vector3(self, 0);
        }

        // Vector3
        public static bool EquivalentTo(this Vector3 self, Vector3 test, float delta = Epsilon)
        {
            var xd = Math.Abs(self.X - test.X);
            var yd = Math.Abs(self.Y - test.Y);
            var zd = Math.Abs(self.Z - test.Z);
            return xd < delta && yd < delta && zd < delta;
        }

        public static Vector3 Parse(string x, string y, string z, NumberStyles ns, IFormatProvider provider)
        {
            return new Vector3(float.Parse(x, ns, provider), float.Parse(y, ns, provider), float.Parse(z, ns, provider));
        }

        public static bool TryParse(string x, string y, string z, NumberStyles ns, IFormatProvider provider, out Vector3 vec)
        {
            if (float.TryParse(x, ns, provider, out var a) && float.TryParse(y, ns, provider, out var b) && float.TryParse(z, ns, provider, out var c))
            {
                vec = new Vector3(a, b, c);
                return true;
            }

            vec = Vector3.Zero;
            return false;
        }

        public static Vector3 Normalise(this Vector3 self) => Vector3.Normalize(self);
        public static Vector3 Absolute(this Vector3 self) => Vector3.Abs(self);
        public static float Dot(this Vector3 self, Vector3 other) => Vector3.Dot(self, other);
        public static Vector3 Cross(this Vector3 self, Vector3 other) => Vector3.Cross(self, other);
        public static Vector3 Round(this Vector3 self, int num = 8) => new Vector3((float) Math.Round(self.X, num), (float) Math.Round(self.Y, num), (float) Math.Round(self.Z, num));
        
        public static Vector3 ClosestAxis(this Vector3 self)
        {
            // VHE prioritises the axes in order of X, Y, Z.
            var norm = Vector3.Abs(self);

            if (norm.X >= norm.Y && norm.X >= norm.Z) return Vector3.UnitX;
            if (norm.Y >= norm.Z) return Vector3.UnitY;
            return Vector3.UnitZ;
        }

        public static Precision.Vector3 ToPrecisionVector3(this Vector3 self)
        {
            return new Precision.Vector3(self.X, self.Y, self.Z);
        }

        public static Vector2 ToVector2(this Vector3 self)
        {
            return new Vector2(self.X, self.Y);
        }

        // Vector4
        public static Vector4 ToVector4(this Color self)
        {
            return new Vector4(self.R, self.G, self.B, self.A) / 255f;
        }

        // Color
        public static Color ToColor(this Vector4 self)
        {
            var mul = self * 255;
            return Color.FromArgb((byte) mul.W, (byte) mul.X, (byte) mul.Y, (byte) mul.Z);
        }

        public static Color ToColor(this Vector3 self)
        {
            var mul = self * 255;
            return Color.FromArgb(255, (byte) mul.X, (byte) mul.Y, (byte) mul.Z);
        }

        // Matrix
        public static Vector3 Transform(this Matrix4x4 self, Vector3 vector) => Vector3.Transform(vector, self);

        // Plane
        public static Plane PlaneFromVertices(IEnumerable<Vector3> vertices)
        {
            var verts = vertices.Take(3).ToList();
            return PlaneFromVertices(verts[0], verts[1], verts[2]);
        }

        public static Plane PlaneFromVertices(Vector3 a, Vector3 b, Vector3 c)
        {
            var ab = b - a;
            var ac = c - a;

            var normal = ac.Cross(ab).Normalise();
            var d = normal.Dot(a);

            return new Plane(normal, d);
        }

        // https://github.com/ericwa/ericw-tools/blob/master/qbsp/map.cc @TextureAxisFromPlane
        public static (Vector3 uAxis, Vector3 vAxis, Vector3 snappedNormal) GetQuakeTextureAxes(this Plane plane)
        {
            var baseaxis = new[]
            {
                new Vector3(0, 0, 1), new Vector3(1, 0, 0), new Vector3(0, -1, 0), // floor
                new Vector3(0, 0, -1), new Vector3(1, 0, 0), new Vector3(0, -1, 0), // ceiling
                new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, -1), // west wall
                new Vector3(-1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, -1), // east wall
                new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, -1), // south wall
                new Vector3(0, -1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, -1) // north wall
            };

            var best = 0f;
            var bestaxis = 0;

            for (var i = 0; i < 6; i++)
            {
                var dot = plane.Normal.Dot(baseaxis[i * 3]);
                if (!(dot > best)) continue;

                best = dot;
                bestaxis = i;
            }

            return (baseaxis[bestaxis * 3 + 1], baseaxis[bestaxis * 3 + 2], baseaxis[bestaxis * 3]);
        }
    }
}