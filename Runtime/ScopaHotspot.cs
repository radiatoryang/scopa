// code modified from https://github.com/BennyKok/unity-hotspot-uv
// under MIT License
// original was Copyright (c) 2021 BennyKok

using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Scopa {
    /// <summary> hotspot UV texturing module </summary>
    public class ScopaHotspot {

        /// <summary> main hotspot UV function; grabs verts, returns FALSE if the face verts are too big for the hotspot atlas (based on the atlas' fallback threshold)</summary>
        public static bool TryGetHotspotUVs(List<Vector3> faceVerts, Vector3 normal, HotspotTexture atlas, out Vector2[] uvs, float scalar = 0.03125f) {
            uvs = PlanarProject(faceVerts, normal);

            var approximateSize = LargestVector2(uvs) * scalar - SmallestVector2(uvs) * scalar;

            if ( atlas.hotspotRotate == HotspotRotateMode.Random ) {
                RotateUVs(uvs, Random.Range(0, 4) * 90);
                approximateSize = LargestVector2(uvs) * scalar - SmallestVector2(uvs) * scalar;
            } else if ( (atlas.hotspotRotate == HotspotRotateMode.RotateHorizontalToVertical && approximateSize.x > approximateSize.y) ||
                (atlas.hotspotRotate == HotspotRotateMode.RotateVerticalToHorizontal && approximateSize.y > approximateSize.x) ) {
                RotateUVs(uvs, Random.value > 0.5f ? -90 : 90);
                approximateSize = LargestVector2(uvs) * scalar - SmallestVector2(uvs) * scalar;
            }

            var bestHotspot = atlas.GetBestUVFromUVs(approximateSize.x * atlas.hotspotScalar, approximateSize.y * atlas.hotspotScalar);
            var bestHotspotSize = LargestVector2(bestHotspot) - SmallestVector2(bestHotspot);

            FitUVs(uvs, bestHotspot);
            if ( approximateSize.x * atlas.hotspotScalar / bestHotspotSize.x > atlas.fallbackThreshold || approximateSize.y * atlas.hotspotScalar / bestHotspotSize.y > atlas.fallbackThreshold ) {
                return false;
            } else {
                return true;
            }
            
        }

        static Vector2[] PlanarProject(List<Vector3> faceVerts, Vector3 normal) {
            if ( faceVerts.Count < 3) {
                Debug.LogError("Cannot planar project for less than 3 vertices.");
                return null;
            }
            var uvs = new Vector2[faceVerts.Count];
            // var plane = new Plane(faceVerts[faceVerts.Count-3], faceVerts[faceVerts.Count-2], faceVerts[faceVerts.Count-1]);
            // var normal = plane.normal * Mathf.Sign(plane.distance);

            // use longest edge as vAxis
            Vector3 longestEdge = Vector3.zero;
            for(int i=0; i<faceVerts.Count; i++) {
                var newEdge = faceVerts[(i+1)%faceVerts.Count] - faceVerts[i];
                if ( newEdge.sqrMagnitude > longestEdge.sqrMagnitude ) {
                    longestEdge = newEdge;
                }
            }
            var vAxis = longestEdge;
            
            // var normalAbs = normal.Absolute();
            // var vAxis = normalAbs.y > normalAbs.x && normalAbs.y > normalAbs.z ? Vector3.right : Vector3.up; 

            var uAxis = Vector3.Cross( normal, vAxis );
            
            for(int i=0; i<faceVerts.Count; i++) {
                uvs[i].x = Vector3.Dot(uAxis, faceVerts[i]);
                uvs[i].y = Vector3.Dot(vAxis, faceVerts[i]);
            }

            // rotate planar projection to align with the longest edge
            Vector2 longestEdge2 = Vector2.zero;
            for(int i=0; i<uvs.Length; i++) {
                var newEdge = uvs[(i+1)%uvs.Length] - uvs[i];
                if ( newEdge.sqrMagnitude > longestEdge2.sqrMagnitude ) {
                    longestEdge2 = newEdge;
                }
            }
            RotateUVs(uvs, Vector2.Angle( Vector2.right, longestEdge2.normalized) );

            return uvs;
        }

        static void RotateUVs(Vector2[] uvs, float angle = 90) {
            var center = (SmallestVector2(uvs) + LargestVector2(uvs)) / 2;
            for (int i=0; i<uvs.Length; i++) {
                uvs[i] = Quaternion.Euler(0, 0, angle) * (uvs[i] - center) + (Vector3)center;
            }
        }

        static Vector2 SmallestVector2(Vector2[] v)
        {
            int len = v.Length;
            Vector2 l = v[0];
            for (int i = 0; i < len; i++)
            {
                if (v[i].x < l.x) l.x = v[i].x;
                if (v[i].y < l.y) l.y = v[i].y;
            }
            return l;
        }

        static Vector2 LargestVector2(Vector2[] v)
        {
            int len = v.Length;
            Vector2 l = v[0];
            for (int i = 0; i < len; i++)
            {
                if (v[i].x > l.x) l.x = v[i].x;
                if (v[i].y > l.y) l.y = v[i].y;
            }
            return l;
        }

        public static Vector2[] FitUVs(Vector2[] uvs, Vector2[] target, bool randomize = true)
        {
            // shift UVs to zeroed coordinates
            Vector2 smallestVector2 = SmallestVector2(uvs);
            Vector2 smallestVector2Target = SmallestVector2(target);

            int i;
            for (i = 0; i < uvs.Length; i++)
            {
                uvs[i] -= smallestVector2;
            }

            smallestVector2 = SmallestVector2(uvs);
            smallestVector2Target = SmallestVector2(target);

            // Debug.Log(uvs.Aggregate(" ", (x, y) => x + ", " + y));

            Vector2 largestVector2 = LargestVector2(uvs);
            Vector2 largestVector2Target = LargestVector2(target);
            float widthScale = (largestVector2.x - smallestVector2.x) / (largestVector2Target.x - smallestVector2Target.x);
            float heightScale = (largestVector2.y - smallestVector2.y) / (largestVector2Target.y - smallestVector2Target.y);
            float scale = Mathf.Max(widthScale, heightScale);

            // Debug.Log(scale);
            // Debug.Log(uvs.Aggregate("Before UVS ", (x, y) => x + ", " + y));

            for (i = 0; i < uvs.Length; i++)
            {
                uvs[i] /= scale;
            }

            for (i = 0; i < uvs.Length; i++)
            {
                uvs[i] += target[3];
            }
            // Debug.Log(target.Aggregate("Target ", (x, y) => x + ", " + y));
            // Debug.Log(uvs.Aggregate("UVS ", (x, y) => x + ", " + y));
            // Debug.Log(target[3]);

            if ( randomize ) {
                var center = (smallestVector2Target + largestVector2Target) / 2;
                bool flipX = Random.value < 0.5f;
                bool flipY = Random.value < 0.5f;
                for (i=0; i<uvs.Length; i++) {
                    if (flipX)
                        uvs[i].x = center.x - (uvs[i].x - center.x);
                    if (flipY)
                        uvs[i].y = center.y - (uvs[i].y - center.y);
                }
     
            }

            return uvs;
        }
    }
}