using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scopa {
    /// <summary> attach this component to a game object with a mesh filter, and it'll DrawMeshInstanced() detail meshes all over it </summary>
    [ExecuteInEditMode]
    public class ScopaDetailDrawer : MonoBehaviour {
        [Tooltip("enable to force the detail drawer to run at edit time, even if editor preview isn't enabled in the MaterialConfig")]
        public bool forcePreviewInEditor = false;

        [Tooltip("if the level uses static batching then this MUST be pre-assigned before runtime, or else static batching will make the world mesh unreadable")]
        public Mesh worldMesh;
        public ScopaMaterialConfig detailConfig;

        bool triedBuildingData = false;
        Dictionary<MaterialDetailGroup, List<Matrix4x4[]>> detailData = new Dictionary<MaterialDetailGroup, List<Matrix4x4[]>>();

        MaterialPropertyBlock matBlock;
        public const int INSTANCE_LIMIT = 1023; // this is only 1023 because we're using DrawMeshInstanced()
        public const int MAX_SAMPLE_ATTEMPTS = 8;

        Camera mainCam;

        #if UNITY_2022_2_OR_NEWER
        // see bug: https://issuetracker.unity3d.com/issues/drawn-mesh-using-graphics-dot-drawmeshinstanced-flickers-when-setting-the-camera-parameter
        [Tooltip("if your game uses multiple Cameras, you may want to enable this, to draw instanced details ONLY for Camera.main while in play mode? keep this disabled unless you're on Unity 2022.1+, since there's a bug where instances will flicker")]
        public bool inPlayModeOnlyDrawForMainCamera = true;
        #endif

        void OnEnable() {
            Reset();
        }

        void Reset() {
            triedBuildingData = false;
            detailData.Clear();
        }

        void Update() {
            if ( detailConfig == null || !detailConfig.enableDetailInstancing || (!Application.isPlaying && !forcePreviewInEditor && !detailConfig.drawDetailsInEditor) ) {
                if ( triedBuildingData ) {
                    Reset();
                }
                return;
            }

            if ( detailConfig != null ) {
                if ( !triedBuildingData )
                    BuildDetailDataAll();
   
                DrawDetailGroupAll();
            }
        }

        public void BuildDetailDataAll() {
            triedBuildingData = true;

            if ( worldMesh == null && TryGetComponent<MeshFilter>(out var mf) ) {
                worldMesh = mf.sharedMesh;
            }

            if ( worldMesh == null ) {
                Debug.LogError("ScopaDetailDrawer couldn't access world mesh / couldn't find a MeshFilter!", this.gameObject);
                return;
            }

            matBlock = new MaterialPropertyBlock();
            mainCam = Camera.main;
            BuildDetailData();
        }

        void BuildDetailData() {
            // step 1: generate floor, wall, and ceiling data for the world mesh
            // we also need triangle sizes so we can weight randomness more uniformly
            // see https://gist.github.com/danieldownes/b1c9bab09cce013cc30a4198bfeda0aa

            var verts = worldMesh.vertices;
            var tris = worldMesh.triangles;

            var floors = new List<Polygon>();
            var floorSizes = new List<float>();

            var walls = new List<Polygon>();
            var wallSizes = new List<float>();

            var ceilings = new List<Polygon>();
            var ceilingSizes = new List<float>();

            // var scale = worldMesh.transform.lossyScale;
            var totalInstances = 0;

            for (int i=0; i<tris.Length; i+=3) {
                var poly = new Polygon(verts[tris[i]], verts[tris[i+1]], verts[tris[i+2]]);
                var worldNormal = transform.TransformDirection(poly.Plane.normal);
                if ( worldNormal.y > 0.5f) {
                    AddToPolygons( poly, floors, floorSizes );
                } else if ( worldNormal.y < -0.5f) {
                    AddToPolygons( poly, ceilings, ceilingSizes );
                } else {
                    AddToPolygons( poly, walls, wallSizes );
                }
            }

            // step 2: for each detail group, sample random points across the polygon surface(s)
            foreach( var detailGroup in detailConfig.detailGroups) {
                if ( !detailData.ContainsKey(detailGroup) )
                    detailData.Add(detailGroup, new List<Matrix4x4[]>() );

                var detailMeshRadius = detailGroup.detailMesh.bounds.extents.magnitude;
                var detailMeshHeight = Mathf.Abs(detailGroup.detailMesh.bounds.min.y);

                // TODO: although we sample randomly, we still want to group the resulting arrays of matrix transforms close together,
                // so that Unity can generate a good bounding box and possibly cull them
                var currentMatrixList = new List<Matrix4x4>(INSTANCE_LIMIT);

                // for each set...
                var surfaceSets = new Dictionary<List<Polygon>, List<float>>();
                if ( detailGroup.detailFloors )
                    surfaceSets.Add( floors, floorSizes );
                if ( detailGroup.detailCeilings )
                    surfaceSets.Add( ceilings, ceilingSizes );
                if ( detailGroup.detailWalls )
                    surfaceSets.Add( walls, wallSizes );

                foreach ( var surfaceSet in surfaceSets ) {
                    float currentDetailTotal = 0f;
                    float totalArea = surfaceSet.Value[surfaceSet.Value.Count-1];

                    // while desired density not reached yet...
                    while ( currentDetailTotal < totalArea ) {
                        // make sure we're under instance limit
                        if ( currentMatrixList.Count == INSTANCE_LIMIT ) {
                            totalInstances += INSTANCE_LIMIT;
                            detailData[detailGroup].Add( currentMatrixList.ToArray() );
                            currentMatrixList = new List<Matrix4x4>(INSTANCE_LIMIT);
                        }

                        // get a random polygon, weighted by polygon size
                        var random = Random.value * totalArea;
                        for( int i=0; i<surfaceSet.Value.Count; i++ ) {
                            if ( random < surfaceSet.Value[i] ) {
                                var selectedPoly = surfaceSet.Key[i];
                                var polyNormal = transform.TransformDirection(selectedPoly.Plane.normal);
                                var detailScale = Mathf.Lerp(detailGroup.detailScaleRange.x, detailGroup.detailScaleRange.y, Random.value);

                                // sample a random place on polygon... in world space though
                                var sampleAttempts = 0;
                                var detailPos = Vector3.zero;
                                while ( detailPos.sqrMagnitude < 0.01f  
                                    || (detailGroup.checkForCollider && Physics.CheckSphere(
                                        detailPos + polyNormal * detailMeshRadius * detailScale, 
                                        detailMeshRadius * detailScale - 0.1f,
                                        detailGroup.collisionMask,
                                        QueryTriggerInteraction.Ignore
                                    )) )
                                {
                                    detailPos = transform.TransformPoint( selectedPoly.GetRandomPointAsTriangle() ) + polyNormal * detailMeshHeight * detailScale;

                                    if ( sampleAttempts > MAX_SAMPLE_ATTEMPTS )
                                        break;
                                    sampleAttempts++;
                                }
                                if ( sampleAttempts > MAX_SAMPLE_ATTEMPTS ) 
                                    continue;

                                // increment currentDetailTotal so we know when to stop
                                currentDetailTotal += 1f / Mathf.Clamp(detailGroup.detailDensity, 0.001f, 10f);

                                // still give up if there wasn't sky here (for grass details)
                                if ( detailGroup.needSky && Physics.Raycast( detailPos, Vector3.up, 999f, detailGroup.collisionMask, QueryTriggerInteraction.Ignore )) {
                                    continue;
                                }

                                var detailRot = Quaternion.Euler(0f, Random.Range(0, 360), 0); // Quaternion.LookRotation( , worldMesh.transform.TransformDirection(selectedPoly.Plane.normal) );
                                currentMatrixList.Add( Matrix4x4.TRS(detailPos + detailGroup.detailMeshOffset * detailScale, detailRot, Vector3.one * detailScale) );

                                break;
                            }
                        }
                    }
                }

                // make sure we commit the matrix transforms, because maybe the matrix is still under the INSTANCE_LIMIT
                totalInstances += currentMatrixList.Count;
                detailData[detailGroup].Add( currentMatrixList.ToArray() );
            }
            
            Debug.Log($"BuildDetailData() {detailConfig.name}: {totalInstances} instances");
        }

        void AddToPolygons(Polygon poly, List<Polygon> polygons, List<float> sizes) {
            polygons.Add(poly);
            // this is actually "cumulative size"
            sizes.Add( poly.GetSizeAsTriangle() + (sizes.Count > 0 ? sizes[sizes.Count-1] : 0) );
        }

        public void DrawDetailGroupAll() {
            foreach(var kvp in detailData) {
                foreach( var matrices in kvp.Value) {
                    DrawDetailGroup( kvp.Key, matrices );
                }
            }
        }

        void DrawDetailGroup( MaterialDetailGroup detailConfig, Matrix4x4[] matrices) {
            Graphics.DrawMeshInstanced( 
                detailConfig.detailMesh,
                0,
                detailConfig.detailMeshMaterial,
                matrices,
                matrices.Length,
                matBlock,
                detailConfig.castShadows,
                detailConfig.receiveShadows,
                detailConfig.layer,
                #if UNITY_2022_2_OR_NEWER
                Application.isPlaying && inPlayModeOnlyDrawForMainCamera ? mainCam : null
                #else
                    null
                #endif
            );
        }
    }

    public struct DetailTriangle {
        /// <summary> point indices of a triangle </summary>
        Vector3 a, b, c;
    }

}