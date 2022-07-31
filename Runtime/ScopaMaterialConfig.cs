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
    [System.Serializable]
    public class MaterialDetailGroup {
        [Tooltip("(optional) only used in editor, to help you write notes to yourself")]
        public string groupLabel = "New Detail Group";
        public Mesh detailMesh;
        public Material detailMeshMaterial;
        public Vector3 detailMeshOffset, detailMeshRotationOffset;

        [Tooltip("statistically, how many details to place per square meter in world space? bigger number = more details placed (on average)")]
        public float detailDensity = 0.1f;

        [Tooltip("randomly scale the detail meshes with this scale range")]
        // public Vector2 detailScaleRange = new Vector2(1, 1);
        public Vector3 minScale = Vector3.one, maxScale = Vector3.one;

        public bool detailFloors = true, detailWalls = false, detailCeilings = false;

        [Tooltip("(recommended: 50-75%) what % of the detail's bounding box must be within the spawned surface? set to 0% or negative to disable; 100%+ = detail will never hang over any edge, but that means thin faces probably won't get any valid details")]
        [Range(0f, 1.5f)]
        public float surfaceExtentsPercentage = 1f;

        [Tooltip("before placing a detail, what if it's in a collider? adds additional processing cost to the detail generation process")]
        public bool checkForCollider = false;
        public LayerMask collisionMask = 1;
        [Tooltip("good for foliage; will raycast upwards from detail position, and if it hits something (via Collision Mask) then it won't spawn")]
        public bool needSky = false;

        [Layer] public int layer;
        public ShadowCastingMode castShadows;
        public bool receiveShadows = false;
    }

    /// <summary> ScriptableObject asset used to configure hotspot UVs, surface detail instancing, and maybe more. </summary>
    [CreateAssetMenu(fileName = "New Scopa Material Config", menuName = "Scopa/Material Config", order = 300)]
    public class ScopaMaterialConfig : ScriptableObject
    {
        [Tooltip("if defined, Scopa will use this mesh prefab when instantiating brush meshes\n (NOTE: if the map importer or entity config has a mesh prefab defined, that will override this")]
        public GameObject meshPrefab;

        [Tooltip("(default: -1) if 0 or higher, this value will override the map config settings' smoothing angle for meshes with this material\n (note: entities can override *this* setting with _phong and _phong_angle)")]
        public float smoothingAngle = -1;

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
            // return rects[0];
            return rects[Random.Range(0, rects.Count)];
        }

        public Rect GetBestHotspotRect(float pixelWidth, float pixelHeight) {
            // smaller sorting score = better match
            return rects.OrderBy( rect => 
                (Mathf.Abs(rect.width - pixelWidth) + Mathf.Abs(rect.height - pixelHeight))    
                * (resolutionBias + (1f - resolutionBias) * Mathf.Abs( (rect.width / rect.height) - (pixelWidth / pixelHeight))) // weight toward the closest aspect ratio
            ).FirstOrDefault();
        }

        public Vector2[] HotspotRectToUV(Rect rect) {
            var list = new Vector2[4];
            var size = new Vector2(hotspotTexture.width, hotspotTexture.height);

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

        public Vector2[] GetRandomHotspotUV() {
            return HotspotRectToUV( GetRandomHotspotRect() );
        }

        public Vector2[] GetBestHotspotUVFromPixels(float pixelWidth, float pixelHeight)
        {
            return HotspotRectToUV( GetBestHotspotRect(pixelWidth, pixelHeight) );
        }

        public Vector2[] GetBestHotspotUVFromUVs(float uvWidth, float uvHeight)
        {
            return HotspotRectToUV( GetBestHotspotRect(uvWidth * hotspotTexture.width, uvHeight * hotspotTexture.height));
        }

        #endregion

        [Header("SURFACE DETAIL INSTANCING")]
        [Tooltip("draw mesh instances (1023 per batch) via Graphics.DrawMeshInstanced() based on random points scattered on brush face surfaces... good for small non-solid details like grass, small rocks, etc.")]
        public bool enableDetailInstancing = false;

        [Tooltip("if enabled, will run the detail building and instance rendering outside of play mode")]
        public bool drawDetailsInEditor = true;
        
        [Tooltip("you can define multiple detail prop types, and each group can have different placement rules")]
        public MaterialDetailGroup[] detailGroups;

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
