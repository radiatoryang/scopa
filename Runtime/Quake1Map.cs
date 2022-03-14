// MIT License
// based off Henry's Source importer https://github.com/Henry00IS/Chisel.Import.Source
// also based off https://github.com/Quixotic7/Chisel.Import.Quake1/

using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Scopa
{
    public class Quake1Map
    {

        private static void CalculateTextureCoordinates(ChiselBrush pr, ChiselSurface surface, Plane clip, int textureWidth, int textureHeight, MapAxis UAxis, MapAxis VAxis)
        {
            var localToPlaneSpace = (Matrix4x4)MathExtensions.GenerateLocalToPlaneSpaceMatrix(new float4(clip.normal, clip.distance));
            var planeSpaceToLocal = (Matrix4x4)math.inverse(localToPlaneSpace);

            UAxis.Translation %= textureWidth;
            VAxis.Translation %= textureHeight;

            if (UAxis.Translation < -textureWidth / 2f)
                UAxis.Translation += textureWidth;

            if (VAxis.Translation < -textureHeight / 2f)
                VAxis.Translation += textureHeight;

            var scaleX = textureWidth * UAxis.Scale * _conversionScale;
            var scaleY = textureHeight * VAxis.Scale * _conversionScale;

            var uoffset = Vector3.Dot(Vector3.zero, new Vector3(UAxis.Vector.X, UAxis.Vector.Z, UAxis.Vector.Y)) + (UAxis.Translation / textureWidth);
            var voffset = Vector3.Dot(Vector3.zero, new Vector3(VAxis.Vector.X, VAxis.Vector.Z, VAxis.Vector.Y)) + (VAxis.Translation / textureHeight);

            var uVector = new Vector4(UAxis.Vector.X / scaleX, UAxis.Vector.Z / scaleX, UAxis.Vector.Y / scaleX, uoffset);
            var vVector = new Vector4(VAxis.Vector.X / scaleY, VAxis.Vector.Z / scaleY, VAxis.Vector.Y / scaleY, voffset);
            var uvMatrix = new UVMatrix(uVector, -vVector);
            var matrix = uvMatrix.ToMatrix();

            matrix = matrix * planeSpaceToLocal;

            surface.surfaceDescription.UV0 = new UVMatrix(matrix);
        }

        // For Quake 1 Standard format
        private static bool GetTextureAxises(Plane plane, out Vector3 t1, out Vector3 t2)
        {
            // feel free to improve this uv mapping code, it has some issues.
            // • 45 degree angled walls may not have correct UV texture coordinates (are not correctly picking the dominant axis because there are two).
            // • negative vertex coordinates may not have correct UV texture coordinates.

            int dominantAxis = 0; // 0 == x, 1 == y, 2 == z

            // find the axis closest to the polygon's normal.
            float[] axes = {
                Mathf.Abs(plane.normal.x),
                Mathf.Abs(plane.normal.z),
                Mathf.Abs(plane.normal.y)
            };

            // defaults to use x-axis.
            dominantAxis = 0;
            // check whether the y-axis is more likely.
            if (axes[1] > axes[dominantAxis])
                dominantAxis = 1;
            // check whether the z-axis is more likely.
            if (axes[2] >= axes[dominantAxis])
                dominantAxis = 2;

            // x-axis:
            if (dominantAxis == 0) {
                t1 = new Vector3(0, 0, 1);
                t2 = new Vector3(0, 1, 0);
                return true;
            }

            // y-axis:
            if (dominantAxis == 1) {
                t1 = new Vector3(0, 0, 1);
                t2 = new Vector3(1, 0, 0);
                return true;
            }

            // z-axis:
            if (dominantAxis == 2) {
                t1 = new Vector3(1, 0, 0);
                t2 = new Vector3(0, 1, 0);
                return true;
            }

            t1 = null;
            t2 = null;
            return false;
        }
    }

}