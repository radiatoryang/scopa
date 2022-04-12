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

        /// <summary> main hotspot UV function; grabs verts, returns UVs</summary>
        public static Vector2[] GetHotspotUVs(List<Vector3> faceVerts, HotspotTexture atlas) {
            var uvs = PlanarProject(faceVerts);
            // Debug.Log(projection.Aggregate(" ", (x, y) => x + ", " + y));
            var approximateSize = LargestVector2(uvs) - SmallestVector2(uvs);

            FitUVs(uvs, atlas.GetBestUVFromUVs(approximateSize.x, approximateSize.y) );

            return uvs;
        }

        static Vector2[] PlanarProject(List<Vector3> faceVerts) {
            if ( faceVerts.Count < 3) {
                Debug.LogError("Cannot planar project for less than 3 vertices.");
                return null;
            }

            var uvs = new Vector2[faceVerts.Count];
            var plane = new Plane(faceVerts[0], faceVerts[1], faceVerts[2]);
            var normal = plane.normal * Mathf.Sign(plane.distance);
            
            var vAxis = plane.GetClosestAxisToNormal() != Vector3.up ? Vector3.up : Vector3.right; // assume V axis is up
            // TODO: but if facing up or down, instead we need to set U axis to the longest most-parallel edges, and then derive the V axis
            var uAxis = Vector3.Cross( normal, vAxis );
            
            for(int i=0; i<faceVerts.Count; i++) {
                uvs[i].x = Vector3.Dot(uAxis, faceVerts[i]);
                uvs[i].y = Vector3.Dot(vAxis, faceVerts[i]);
            }

            return uvs;
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