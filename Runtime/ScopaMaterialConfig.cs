// from https://github.com/BennyKok/unity-hotspot-uv
// under MIT License
// hotspot code Copyright (c) 2021 BennyKok

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Scopa
{
    /// <summary> ScriptableObject asset used to configure hotspot UVs, surface detail instancing, and maybe more. </summary>
    [CreateAssetMenu(fileName = "New Scopa Material Config", menuName = "Scopa/Material Config", order = 300)]
    public class ScopaMaterialConfig : ScriptableObject
    {
        [Tooltip("if defined, Scopa will use this mesh prefab when instantiating brush meshes\n (NOTE: if the map importer or entity config has a mesh prefab defined, that will override this")]
        public GameObject meshPrefab;

        [Tooltip("(default: -1) if 0 or higher, this value will override the map config settings' smoothing angle for meshes with this material\n (note: entities can override *this* setting with _phong and _phong_angle)")]
        public float smoothingAngle = -1;

        /// <summary>make a new class that inherits from ScopaMaterialConfig + override OnBuildMeshObject() to add custom components / access mesh data at .MAP import time</summary>
        public virtual void OnBuildMeshObject(GameObject meshObject, Mesh mesh) {

        }

        #region HOTSPOT

        [Header("HOTSPOT UVS")]
        [Tooltip("if enabled, will try to re-UV brush faces to the 'best' fitting atlas segment")]
        public bool enableHotspotUv = true;

        [Tooltip("the mainTexture used for hotspot UV calculations / visual preview in the HotspotEditor")]
        [FormerlySerializedAs("target")]
        public Texture hotspotTexture;

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

        [Tooltip("the hotspot atlas / rectangle data; edit this visually in editor with Scopa > Hotspot Atlas Editor")]
        public List<Rect> rects = new List<Rect>();

        public Rect GetRandomHotspotRect()
        {
            return rects[Random.Range(0, rects.Count)];
        }

        public Rect GetBestHotspotRect(float pixelWidth, float pixelHeight) {
            // smaller sorting score = better match

            // return rects.OrderBy( rect => 
            //     (Mathf.Abs(rect.width - pixelWidth) + Mathf.Abs(rect.height - pixelHeight))    
            //     * (resolutionBias + (1f - resolutionBias) * Mathf.Abs( (rect.width / rect.height) - (pixelWidth / pixelHeight))) // weight toward the closest aspect ratio
            // ).FirstOrDefault();

            return rects.OrderBy( rect => 
                resolutionBias 
                * (Mathf.Abs(rect.width - pixelWidth) / rect.width
                + Mathf.Abs(rect.height - pixelHeight) / rect.height)    
                + (1f - resolutionBias) 
                * Mathf.Abs( (rect.width / rect.height) - (pixelWidth / pixelHeight)) * 2
            ).FirstOrDefault();
        }

        public Vector2[] GetRandomHotspotUV() {
            return ScopaHotspot.HotspotRectToUV( GetRandomHotspotRect(), hotspotTexture.width, hotspotTexture.height );
        }

        public Vector2[] GetBestHotspotUVFromPixels(float pixelWidth, float pixelHeight)
        {
            return ScopaHotspot.HotspotRectToUV( GetBestHotspotRect(pixelWidth, pixelHeight), hotspotTexture.width, hotspotTexture.height );
        }

        public Vector2[] GetBestHotspotUVFromUVs(float uvWidth, float uvHeight)
        {
            return ScopaHotspot.HotspotRectToUV( GetBestHotspotRect(uvWidth * hotspotTexture.width, uvHeight * hotspotTexture.height), hotspotTexture.width, hotspotTexture.height);
        }

        #endregion
    }

    public enum HotspotRotateMode {
        Disabled,
        Random,
        RotateHorizontalToVertical,
        RotateVerticalToHorizontal
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
