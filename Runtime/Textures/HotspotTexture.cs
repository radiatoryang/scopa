// from https://github.com/BennyKok/unity-hotspot-uv
// under MIT License
// Copyright (c) 2021 BennyKok

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scopa
{
    public enum HotspotRotateMode {
        Disabled,
        Random,
        RotateHorizontalToVertical,
        RotateVerticalToHorizontal
    }

    [CreateAssetMenu(fileName = "New Hotspot Texture", menuName = "Scopa/Hotspot Texture", order = 300)]
    public class HotspotTexture : ScriptableObject
    {
        public Texture target;

        [Tooltip("(default: 0.05) increase to make higher resolution hotspots more likely")]
        public float hotspotScalar = 0.05f;

        [Tooltip("(default: 0.5) when trying to decide the best hotspot for a face, how much to prioritize aspect ratio / proportions vs. resolution?")]
        [Range(0f, 1f)]
        public float resolutionBias = 0.5f;

        [Tooltip("(default: Random) rotate shapes for more variation, or in case the hotspot atlas doesn't have certain horizontal or vertical elements")]
        public HotspotRotateMode hotspotRotate = HotspotRotateMode.Random;

        [Tooltip("(optional) if a face is __ times bigger than the largest hotspot, use the fallback material instead")]
        public float fallbackThreshold = 1.5f;

        [Tooltip("(optional) if a face is bigger than the largest hotspot (e.g. a floor or ceiling) then instead fallback to this other material, preferably a tiling material")]
        public Material fallbackMaterial;

        public List<Rect> rects = new List<Rect>();

        public Rect GetRandomRect()
        {
            // return rects[0];
            return rects[Random.Range(0, rects.Count)];
        }

        public Rect GetBestRect(float pixelWidth, float pixelHeight) {
            // smaller sorting score = better match
            return rects.OrderBy( rect => 
                (Mathf.Abs(rect.width - pixelWidth) + Mathf.Abs(rect.height - pixelHeight))    
                * (resolutionBias + (1f - resolutionBias) * Mathf.Abs( (rect.width / rect.height) - (pixelWidth / pixelHeight))) // weight toward the closest aspect ratio
            ).FirstOrDefault();
        }

        public Vector2[] RectToUV(Rect rect) {
            var list = new Vector2[4];
            var size = new Vector2(target.width, target.height);

            // Transforming from texture space to UV space
            list[0] = rect.TopLeft() / size;
            list[1] = rect.TopRight() / size;
            list[2] = rect.BottomRight() / size;
            list[3] = rect.BottomLeft() / size;

            // Fliping along the y axis
            for (int i = 0; i < list.Length; i++) {
                list[i].y = 1 - list[i].y;
            }

            return list;
        }

        public Vector2[] GetRandomUV() {
            return RectToUV( GetRandomRect() );
        }

        public Vector2[] GetBestUVFromPixels(float pixelWidth, float pixelHeight)
        {
            return RectToUV( GetBestRect(pixelWidth, pixelHeight) );
        }

        public Vector2[] GetBestUVFromUVs(float uvWidth, float uvHeight)
        {
            return RectToUV( GetBestRect(uvWidth * target.width, uvHeight * target.height));
        }
    }

    public static class RectExtensions
    {
        public static Vector2 TopLeft(this Rect rect)
        {
            return new Vector2(rect.xMin, rect.yMin);
        }
        public static Vector2 TopRight(this Rect rect)
        {
            return new Vector2(rect.xMax, rect.yMin);
        }
        public static Vector2 BottomRight(this Rect rect)
        {
            return new Vector2(rect.xMax, rect.yMax);
        }
        public static Vector2 BottomLeft(this Rect rect)
        {
            return new Vector2(rect.xMin, rect.yMax);
        }
        public static Rect ScaleSizeBy(this Rect rect, float scale, Vector2 pivotPoint)
        {
            Rect result = rect;
            result.x -= pivotPoint.x;
            result.y -= pivotPoint.y;
            result.xMin *= scale;
            result.xMax *= scale;
            result.yMin *= scale;
            result.yMax *= scale;
            result.x += pivotPoint.x;
            result.y += pivotPoint.y;
            return result;
        }
    }
}
