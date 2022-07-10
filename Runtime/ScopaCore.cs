using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Scopa.Formats.Map.Formats;
using Scopa.Formats.Map.Objects;
using Scopa.Formats.Texture.Wad;
using Scopa.Formats.Id;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mesh = UnityEngine.Mesh;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Scopa {
    /// <summary>main class for core Scopa MAP functions</summary>
    public static class ScopaCore {
        // to avoid GC, we use big static lists so we just allocate once
        static List<Face> allFaces = new List<Face>();
        static List<Vector3> verts = new List<Vector3>(4096);
        static List<int> tris = new List<int>(8192);
        static List<Vector2> uvs = new List<Vector2>(4096);
        

        // (editor only) search for all materials in the project once per import, save results here
        static Dictionary<string, Material> materials = new Dictionary<string, Material>(512);

        /// <summary>Parses the .MAP text data into a usable data structure.</summary>
        public static MapFile ParseMap( string pathToMapFile, ScopaMapConfig config ) {
            IMapFormat importer = null;
            if ( pathToMapFile.EndsWith(".map")) {
                importer = new QuakeMapFormat();
            } 

            if ( importer == null) {
                Debug.LogError($"No file importer found for {pathToMapFile}");
                return null;
            }

            Solid.weldingThreshold = config.weldingThreshold;
            var mapFile = importer.ReadFromFile( pathToMapFile );
            mapFile.name = System.IO.Path.GetFileNameWithoutExtension( pathToMapFile );

            return mapFile;
        }

        /// <summary>The main function for converting parsed MapFile data into a Unity GameObject with 3D mesh and colliders.
        /// Outputs a lists of built meshes (e.g. so UnityEditor can serialize them)</summary>
        public static GameObject BuildMapIntoGameObject( MapFile mapFile, Material defaultMaterial, ScopaMapConfig config, out Dictionary<Mesh, Transform> meshList ) {
            var rootGameObject = new GameObject( mapFile.name );

            BuildMapPrepass( mapFile, config );
            CacheMaterialSearch();
            meshList = ScopaCore.AddGameObjectFromEntityRecursive(rootGameObject, mapFile.Worldspawn, mapFile.name, defaultMaterial, config);

            // create a separate physics scene for the object, or else we can't raycast against it?
            // var csp = new CreateSceneParameters(LocalPhysicsMode.Physics3D);
            // var prefabScene = SceneManager.CreateScene("Scopa_PrefabScene", csp);
            // SceneManager.MoveGameObjectToScene( rootGameObject, prefabScene );
            // var physicsScene = prefabScene.GetPhysicsScene();

            // we have to wait until the whole map is built before we bake AO, because we need all the colliders in place
            if ( config.bakeVertexColorAO ) {
                foreach ( var meshKVP in meshList ) {
                    if ( meshKVP.Value != null && meshKVP.Value.gameObject.isStatic ) // if the Transform is null, that means it's a collision mesh and we should ignore it for AO purposes
                        ScopaVertexAO.BakeObject( meshKVP.Key, meshKVP.Value, config.occlusionLength );
                }
            }

            return rootGameObject;
        }

        static void CacheMaterialSearch() {
            #if UNITY_EDITOR
            materials.Clear();
            var materialSearch = AssetDatabase.FindAssets("t:Material");
            foreach ( var materialSearchGUID in materialSearch) {
                // if there's multiple Materials attached to one Asset, we have to do additional filtering
                var allAssets = AssetDatabase.LoadAllAssetsAtPath( AssetDatabase.GUIDToAssetPath(materialSearchGUID) );
                foreach ( var asset in allAssets ) {
                    if ( asset != null && !materials.ContainsKey(asset.name) && asset is Material ) {
                        // Debug.Log("loaded " + asset.name);
                        materials.Add(asset.name, asset as Material);
                    }
                }

            }
            #else
            Debug.Log("CacheMaterialSearch() is not available at runtime.");
            #endif
        }

        /// <summary>Before generating game objects, we may want to modify some of the MapFile data. For example, when merging entities into worldspawn.</summary>
        static void BuildMapPrepass( MapFile mapFile, ScopaMapConfig config ) {
            PrepassEntityRecursive( mapFile.Worldspawn, mapFile.Worldspawn, config );
        }

        /// <summary>Core recursive function for traversing entity tree and pre-passing each entity</summary>
        static void PrepassEntityRecursive( Worldspawn worldspawn, Entity ent, ScopaMapConfig config) {
            bool mergeToWorld = config.IsEntityMergeToWorld(ent.ClassName);
            if ( mergeToWorld ) {
                ent.discard = true;
            }

            for (int i=0; i<ent.Children.Count; i++) {
                if ( ent.Children[i] is Entity ) {
                    PrepassEntityRecursive( worldspawn, ent.Children[i] as Entity, config );
                } // merge child brush to worldspawn
                else if ( mergeToWorld && ent.Children[i] is Solid ) {
                    ((Solid)ent.Children[i]).entityDataOverride = ent; // but preserve old entity data for mesh / collider generation
                    worldspawn.Children.Add(ent.Children[i]);
                    ent.Children.RemoveAt(i);
                    i--;
                    continue;
                }
            }

        }

        /// <summary> The main core function for converting entities (worldspawn, func_, etc.) into 3D meshes. </summary>
        public static Dictionary<Mesh, Transform> AddGameObjectFromEntityRecursive( GameObject rootGameObject, Entity ent, string namePrefix, Material defaultMaterial, ScopaMapConfig config ) {
            var allMeshes = new Dictionary<Mesh, Transform>();

            var newMeshes = AddGameObjectFromEntity(rootGameObject, ent, namePrefix, defaultMaterial, config) ;
            if ( newMeshes != null ) {
                foreach ( var meshKVP in newMeshes ) {
                    allMeshes.Add( meshKVP.Key, meshKVP.Value );
                }
            }

            foreach ( var child in ent.Children ) {
                if ( child is Entity && ((Entity)child).discard == false ) {
                    var newMeshChildren = AddGameObjectFromEntityRecursive(rootGameObject, child as Entity, namePrefix, defaultMaterial, config);
                    if ( newMeshChildren.Count > 0) {
                        foreach ( var meshKVP in newMeshChildren ) {
                            allMeshes.Add( meshKVP.Key, meshKVP.Value );
                        }
                    }
                }
            }

            return allMeshes;
        }

        public static Dictionary<Mesh, Transform> AddGameObjectFromEntity( GameObject rootGameObject, Entity entData, string namePrefix, Material defaultMaterial, ScopaMapConfig config ) {
            var solids = entData.Children.Where( x => x is Solid).Cast<Solid>();
            allFaces.Clear(); // used later for testing unseen faces
            var lastSolidID = -1;

            // detect per-entity smoothing angle, if defined
            var entitySmoothNormalAngle = 0;
            if (entData.TryGetInt("_phong", out var phong) && phong >= 1) {
                if ( entData.TryGetFloat("_phong_angle", out var phongAngle) ) {
                    entitySmoothNormalAngle = Mathf.RoundToInt( phongAngle );
                } else {
                    entitySmoothNormalAngle = 89;
                }
            }

            // for worldspawn, pivot point should be 0, 0, 0... else, see if origin is defined... otherwise, calculate min of bounds
            var calculateOrigin = entData.ClassName.ToLowerInvariant() != "worldspawn";
            var entityOrigin = calculateOrigin ? Vector3.one * 999999 : Vector3.zero;
            if ( entData.TryGetVector3Unscaled("origin", out var newVec) ) {
                entityOrigin = newVec;
                calculateOrigin = false;
            }

            var entityNeedsCollider = !config.IsEntityNonsolid(entData.ClassName);
            var entityIsTrigger = config.IsEntityTrigger(entData.ClassName);

            // pass 1: gather all faces for occlusion checks later + build material list + cull any faces we're obviously not going to use
            var materialLookup = new Dictionary<string, ScopaMapConfig.MaterialOverride>();
            foreach (var solid in solids) {
                lastSolidID = solid.id;
                foreach (var face in solid.Faces) {
                    if ( face.Vertices == null || face.Vertices.Count == 0) // this shouldn't happen though
                        continue;

                    // skip tool textures and other objects?
                    if ( config.IsTextureNameCulled(face.TextureName) ) {
                        face.discardWhenBuildingMesh = true;
                        continue;
                    }
                    
                    allFaces.Add(face); // TODO: we need this for per-entity optimization later

                    // start calculating min bounds, if needed
                    if ( calculateOrigin ) {
                        for(int i=0; i<face.Vertices.Count; i++) {
                            entityOrigin.x = Mathf.Min(entityOrigin.x, face.Vertices[i].x);
                            entityOrigin.y = Mathf.Min(entityOrigin.y, face.Vertices[i].y);
                            entityOrigin.z = Mathf.Min(entityOrigin.z, face.Vertices[i].z);
                        }
                    }

                    // match this face's texture name to a material
                    if ( !materialLookup.ContainsKey(face.TextureName) ) {
                        var newMaterial = defaultMaterial;

                        var materialOverride = config.GetMaterialOverrideFor(face.TextureName);

                        // this face is using a hotspot material, so...
                        if ( materialOverride != null && materialOverride.materialConfig != null && materialOverride.materialConfig.enableHotspotUv && materialOverride.materialConfig.fallbackMaterial != null) {
                            // detect if the face is too big to fit any hotspot! if it is, then use the fallback material (which is hopefully a regular tiling material?)
                            if ( ScopaHotspot.TryGetHotspotUVs(face.Vertices, face.Plane.normal, materialOverride.materialConfig, out var uvs, config.scalingFactor ) == false ) {
                                newMaterial = materialOverride.materialConfig.fallbackMaterial;
                                face.TextureName = newMaterial.name;
                                materialOverride = null;
                            }
                        }
                        
                        // if still no better material found, then search the AssetDatabase for a matching texture name
                        if ( config.findMaterials && materialOverride == null && materials.Count > 0 && materials.ContainsKey(face.TextureName) ) {
                            newMaterial = materials[face.TextureName];
                        }

                        // if a material override wasn't found, generate one
                        if ( materialOverride == null ) {
                            if ( newMaterial == null)
                                Debug.Log(face.TextureName + " This shouldn't be null!");
                            materialOverride = new ScopaMapConfig.MaterialOverride(face.TextureName, newMaterial);
                        }

                        // temporarily merge entries with the same Material by renaming the face's texture name
                        var matchingKey = materialLookup.Where( kvp => kvp.Value.material == materialOverride.material && kvp.Value.materialConfig == materialOverride.materialConfig).FirstOrDefault().Key;
                        if ( !string.IsNullOrEmpty(matchingKey) ) {
                            face.TextureName = matchingKey;
                        } else { // otherwise add to lookup
                            materialLookup.Add( face.TextureName, materialOverride );
                        }
                    }
                }
            }
            entityOrigin *= config.scalingFactor;
            if ( entData.TryGetVector3Scaled("origin", out var pos, config.scalingFactor) ) {
                entityOrigin = pos;
            }

            // pass 2: now build one mesh + one game object per textureName
            var meshList = new Dictionary<Mesh, Transform>();

            // user can specify a template entityPrefab if desired
            var entityPrefab = config.GetEntityPrefabFor(entData.ClassName);
            var meshPrefab = config.GetMeshPrefabFor(entData.ClassName);

            GameObject entityObject = null; 
            if ( entityPrefab != null ) {
                #if UNITY_EDITOR
                entityObject = UnityEditor.PrefabUtility.InstantiatePrefab(entityPrefab) as GameObject; // maintain prefab linkage
                #else
                entityObject = Instantiate(entityPrefab);
                #endif
            } else {
                entityObject = new GameObject();
            }

            entityObject.name = entData.ClassName + "#" + entData.ID.ToString();
            if ( entData.TryGetString("targetname", out var targetName) )
                entityObject.name += " " + targetName;
            entityObject.transform.position = entityOrigin;
            // for point entities, import the "angle" property
            entityObject.transform.localRotation = Quaternion.identity;
            if ( materialLookup.Count == 0 ) { // if there's no meshes and it's a point entity, then it has angles
                if ( entData.TryGetAngles3D("angles", out var angles) )
                    entityObject.transform.localRotation = angles;
                else if (entData.TryGetAngleSingle("angle", out var angle))
                    entityObject.transform.localRotation = angle;
            }
            entityObject.transform.localScale = Vector3.one;
            entityObject.transform.SetParent(rootGameObject.transform);

            // populate the rest of the entity data    
            var entityComponent = entityObject.GetComponent<IScopaEntityData>();

            if ( config.addScopaEntityComponent && entityComponent == null)
                entityComponent = entityObject.AddComponent<ScopaEntity>();

            if ( entityComponent != null)
                entityComponent.entityData = entData;

            // only set Layer if it's a generic game object OR if there's a layer override
            if ( entData.TryGetString("_layer", out var layerName) ) {
                entityObject.layer = LayerMask.NameToLayer(layerName);
            }
            else if ( entityPrefab == null) { 
                entityObject.layer = config.layer;
            }

            // main loop: for each material, build a mesh and add a game object with mesh components
            foreach ( var textureKVP in materialLookup ) {
                ClearMeshBuffers();
                
                foreach ( var solid in solids) {
                    // var matName = textureKVP.Value.textureName;
                    // if ( textureKVP.Value.material == defaultMaterial ) {
                    //     textureKVP.Value.textureName = textureKVP.Key;
                    // }
                    BufferMeshDataFromSolid( solid, config, textureKVP.Value, false);
                    // textureKVP.Value.name = matName;
                }

                if ( verts.Count == 0 || tris.Count == 0) 
                    continue;
                    
                // finally, add mesh as game object, while we still have all the entity information
                GameObject newMeshObj = null;
                var thisMeshPrefab = meshPrefab;

                // the material config might have a meshPrefab defined too; use that if there isn't already a meshPrefab set already
                if ( meshPrefab == null && textureKVP.Value.materialConfig != null && textureKVP.Value.materialConfig.meshPrefab != null ) {
                    thisMeshPrefab = textureKVP.Value.materialConfig.meshPrefab;
                }

                if ( thisMeshPrefab != null ) {
                    #if UNITY_EDITOR
                    newMeshObj = UnityEditor.PrefabUtility.InstantiatePrefab(thisMeshPrefab) as GameObject; // maintain prefab linkage
                    #else
                    newMeshObj = Instantiate(thisMeshPrefab);
                    #endif
                } else {
                    newMeshObj = new GameObject();
                }

                newMeshObj.name = textureKVP.Key;
                newMeshObj.transform.SetParent(entityObject.transform);
                newMeshObj.transform.localPosition = Vector3.zero;
                newMeshObj.transform.localRotation = Quaternion.identity;
                newMeshObj.transform.localScale = Vector3.one;

                // if user set a specific prefab, it probably has its own static flags and layer
                // ... but if it's a generic game object we made, then we set it ourselves
                if ( !string.IsNullOrEmpty(layerName) ) { // or did they set a specifc override on this entity?
                    entityObject.layer = LayerMask.NameToLayer(layerName);
                } else if ( thisMeshPrefab == null ) { 
                    newMeshObj.layer = config.layer;
                }

                if ( thisMeshPrefab == null && config.IsEntityStatic(entData.ClassName)) {
                    SetGameObjectStatic(newMeshObj, entityNeedsCollider && !entityIsTrigger);
                }

                var newMesh = BuildMeshFromBuffers(
                    namePrefix + "-" + entData.ClassName + "#" + entData.ID.ToString() + "-" + textureKVP.Key, 
                    config, 
                    entityOrigin,
                    entitySmoothNormalAngle
                );
                meshList.Add(newMesh, newMeshObj.transform);

                // populate components... if the mesh components aren't there, then add them
                if ( newMeshObj.TryGetComponent<MeshFilter>(out var meshFilter) == false ) {
                    meshFilter = newMeshObj.AddComponent<MeshFilter>();
                }
                meshFilter.sharedMesh = newMesh;

                bool addedMeshRenderer = false;
                if ( newMeshObj.TryGetComponent<MeshRenderer>(out var meshRenderer) == false ) {
                    meshRenderer = newMeshObj.AddComponent<MeshRenderer>();
                    addedMeshRenderer = true;
                }
                meshRenderer.sharedMaterial = textureKVP.Value.material;

                if ( addedMeshRenderer ) { // if we added a generic mesh renderer, then set default shadow caster setting too
                    meshRenderer.shadowCastingMode = config.castShadows;
                }

                // detail instancing detection
                if ( textureKVP.Value.materialConfig != null && textureKVP.Value.materialConfig.enableDetailInstancing ) {
                    var detailDrawer = newMeshObj.GetComponent<ScopaDetailDrawer>() ? newMeshObj.GetComponent<ScopaDetailDrawer>() : newMeshObj.AddComponent<ScopaDetailDrawer>();
                    detailDrawer.worldMesh = meshFilter.sharedMesh;
                    detailDrawer.detailConfig = textureKVP.Value.materialConfig;
                }
            }

            // collision pass, now treat it all as one object and ignore texture names
            if ( config.colliderMode != ScopaMapConfig.ColliderImportMode.None && entityNeedsCollider ) {
                var collisionMeshes = ScopaCore.AddColliders( entityObject, entData, config, namePrefix );
                foreach ( var cMesh in collisionMeshes ) {
                    meshList.Add( cMesh, null ); // collision meshes have their KVP Value's Transform set to null, so that Vertex Color AO bake knows to ignore them
                }
            }

            // now that we've finished building the gameobject, notify any custom user components that import is complete
            var allEntityComponents = entityObject.GetComponentsInChildren<IScopaEntityImport>();
            foreach( var entComp in allEntityComponents ) {
                // scan for any FGD attribute and update accordingly
                FieldInfo[] objectFields = entComp.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
                for (int i = 0; i < objectFields.Length; i++) {
                    var attribute = Attribute.GetCustomAttribute(objectFields[i], typeof(BindFgd)) as BindFgd;
                    if (attribute != null) {
                        switch( attribute.propertyType ) {
                            case BindFgd.VarType.String:
                                if ( entData.TryGetString(attribute.propertyKey, out var stringProp) )
                                    objectFields[i].SetValue(entComp, stringProp);
                                break;
                            case BindFgd.VarType.Bool:
                                if ( entData.TryGetBool(attribute.propertyKey, out var boolValue) )
                                    objectFields[i].SetValue(entComp, boolValue);
                                break;
                            case BindFgd.VarType.Int:
                                if ( entData.TryGetInt(attribute.propertyKey, out var intProp) )
                                    objectFields[i].SetValue(entComp, intProp);
                                break;
                            case BindFgd.VarType.IntScaled:
                                if ( entData.TryGetIntScaled(attribute.propertyKey, out var intScaledProp, config.scalingFactor) )
                                    objectFields[i].SetValue(entComp, intScaledProp);
                                break;
                            case BindFgd.VarType.Float:
                                if ( entData.TryGetFloat(attribute.propertyKey, out var floatProp) )
                                    objectFields[i].SetValue(entComp, floatProp);
                                break;
                            case BindFgd.VarType.FloatScaled:
                                if ( entData.TryGetFloatScaled(attribute.propertyKey, out var floatScaledProp, config.scalingFactor) )
                                    objectFields[i].SetValue(entComp, floatScaledProp);
                                break;
                            case BindFgd.VarType.Vector3Scaled:
                                if ( entData.TryGetVector3Scaled(attribute.propertyKey, out var vec3Scaled, config.scalingFactor) )
                                    objectFields[i].SetValue(entComp, vec3Scaled);
                                break;
                            default:
                                Debug.LogError( $"BindFgd named {objectFields[i].Name} / {attribute.propertyKey} has FGD var type {attribute.propertyType} ... but no case handler for it yet!");
                                break;
                        }
                    }
                }
                entComp.OnEntityImport( entData );
            }
            
            return meshList;
        }

        static GameObject Instantiate(GameObject prefab) {
            // using InstantiatePrefab didn't actually help, since the instance still doesn't auto-update and requires manual reimport anyway
            // #if UNITY_EDITOR
            //     return PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            // #else
                return UnityEngine.Object.Instantiate(prefab);
            // #endif
        }

        static void SetGameObjectStatic(GameObject go, bool isNavigationStatic = true) {
            if ( isNavigationStatic ) {
                go.isStatic = true;
            } else {
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI 
                    | StaticEditorFlags.OccluderStatic 
                    | StaticEditorFlags.BatchingStatic 
                    | StaticEditorFlags.OccludeeStatic 
                    | StaticEditorFlags.OffMeshLinkGeneration 
                    | StaticEditorFlags.ReflectionProbeStatic
                );
            }
        }

        /// <summary> given a brush / solid (and optional textureFilter texture name) it generates mesh data for verts / tris / UV list buffers
        ///  but if returnMesh is true, then it will also CLEAR THOSE BUFFERS and GENERATE A MESH </summary>
        public static void BufferMeshDataFromSolid(Solid solid, ScopaMapConfig config, ScopaMapConfig.MaterialOverride textureFilter = null, bool includeDiscardedFaces = false) {
           
            foreach (var face in solid.Faces) {
                if ( face.Vertices == null || face.Vertices.Count == 0) // this shouldn't happen though
                    continue;

                if ( !includeDiscardedFaces && face.discardWhenBuildingMesh )
                    continue;

                if ( textureFilter != null && textureFilter.textureName.GetHashCode() != face.TextureName.GetHashCode() )
                    continue;

                // test for unseen / hidden faces, and discard
                if ( !includeDiscardedFaces ) {
                    for(int i=0; i<allFaces.Count; i++) {
                        if (allFaces[i].OccludesFace(face)) {
                            // Debug.Log("discarding unseen face at " + face);
                            // face.DebugDrawVerts(Color.yellow);
                            face.discardWhenBuildingMesh = true;
                            break;
                        }
                    }
                    

                    if ( face.discardWhenBuildingMesh )
                        continue;
                }

                BufferScaledMeshDataForFace(
                    face, 
                    config.scalingFactor, 
                    verts, 
                    tris, 
                    uvs, 
                    config.globalTexelScale,
                    textureFilter?.material?.mainTexture != null ? textureFilter.material.mainTexture.width : config.defaultTexSize, 
                    textureFilter?.material?.mainTexture != null ? textureFilter.material.mainTexture.height : config.defaultTexSize,
                    textureFilter != null ? textureFilter.materialConfig : null
                );
            }
        }

        /// <summary> utility function that actually generates the Mesh object </summary>
        static Mesh BuildMeshFromBuffers(string meshName, ScopaMapConfig config, Vector3 meshOrigin = default(Vector3), float smoothNormalAngle = 0) {
            var mesh = new Mesh();
            mesh.name = meshName;

            if ( meshOrigin != default(Vector3) ) {
                for(int i=0; i<verts.Count; i++) {
                    verts[i] -= meshOrigin;
                }
            }

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetUVs(0, uvs);

            mesh.RecalculateBounds();
            if ( smoothNormalAngle > 0.1 )
                mesh.RecalculateNormals(smoothNormalAngle);
            else if ( smoothNormalAngle >= 0)
                mesh.RecalculateNormals(); // built-in Unity method

            if ( config.addTangents && smoothNormalAngle >= 0 )
                mesh.RecalculateTangents();
            
            #if UNITY_EDITOR
            if ( config.addLightmapUV2 ) {
                UnwrapParam.SetDefaults( out var unwrap);
                unwrap.packMargin *= 2;
                Unwrapping.GenerateSecondaryUVSet( mesh, unwrap );
            }

            if ( config.meshCompression != ScopaMapConfig.ModelImporterMeshCompression.Off)
                UnityEditor.MeshUtility.SetMeshCompression(mesh, (ModelImporterMeshCompression)config.meshCompression);
            #endif

            mesh.Optimize();

            return mesh;
        }

        /// <summary> build mesh fragment (verts / tris / uvs), usually run for each face of a solid </summary>
        static void BufferScaledMeshDataForFace(Face face, float scalingFactor, List<Vector3> verts, List<int> tris, List<Vector2> uvs, float scalar = 1f, int textureWidth = 128, int textureHeight = 128, ScopaMaterialConfig materialConfig = null) {
            var lastVertIndexOfList = verts.Count;

            // add all verts and UVs
            for( int v=0; v<face.Vertices.Count; v++) {
                verts.Add(face.Vertices[v] * scalingFactor);

                if ( materialConfig == null || !materialConfig.enableHotspotUv) {
                    uvs.Add(new Vector2(
                        (Vector3.Dot(face.Vertices[v], face.UAxis / face.XScale) + (face.XShift % textureWidth)) / (textureWidth),
                        (Vector3.Dot(face.Vertices[v], face.VAxis / -face.YScale) + (-face.YShift % textureHeight)) / (textureHeight)
                    ) * scalar);
                }
            }

            if ( materialConfig != null && materialConfig.enableHotspotUv && materialConfig.rects.Count > 0) {
                // uvs.AddRange( ScopaHotspot.GetHotspotUVs( verts.GetRange(lastVertIndexOfList, face.Vertices.Count), hotspotAtlas ) );
                if ( !ScopaHotspot.TryGetHotspotUVs( face.Vertices, face.Plane.normal, materialConfig, out var hotspotUVs, scalingFactor )) {
                    // TODO: wow uhh I really fucked up with this design... no easy way to suddenly put this in a different material
                    // ... it will need a pre-pass
                }
                uvs.AddRange( hotspotUVs );
            }

            // verts are already in correct order, add as basic fan pattern (since we know it's a convex face)
            for(int i=2; i<face.Vertices.Count; i++) {
                tris.Add(lastVertIndexOfList);
                tris.Add(lastVertIndexOfList + i - 1);
                tris.Add(lastVertIndexOfList + i);
            }
        }

        /// <summary> for each solid in an Entity, add either a Box Collider or a Mesh Collider component... or make one big merged Mesh Collider </summary>
        public static List<Mesh> AddColliders(GameObject gameObject, Entity ent, ScopaMapConfig config, string namePrefix, bool forceBoxCollidersForAll = false) {
            var meshList = new List<Mesh>();

            var solids = ent.Children.Where( x => x is Solid).Cast<Solid>();
            if ( solids.Count() == 0)
                return meshList;

            bool isTrigger = config.IsEntityTrigger(ent.ClassName);
            bool forceConvex = ent.TryGetInt("_convex", out var num) && num == 1;

            // just one big Mesh Collider... one collider to rule them all
            if ( forceConvex || (!isTrigger && config.colliderMode == ScopaMapConfig.ColliderImportMode.MergeAllToOneConcaveMeshCollider) ) {
                ClearMeshBuffers();
                foreach ( var solid in solids ) {
                    // omit non-solids and triggers
                    if ( solid.entityDataOverride != null && (config.IsEntityNonsolid(solid.entityDataOverride.ClassName) || config.IsEntityTrigger(solid.entityDataOverride.ClassName)) )
                        continue;

                    BufferMeshDataFromSolid(solid, config, null, true);
                }

                var newMesh = BuildMeshFromBuffers(namePrefix + "-" + ent.ClassName + "#" + ent.ID.ToString() + "-Collider", config, gameObject.transform.position, -1 );
                var newMeshCollider = gameObject.AddComponent<MeshCollider>();
                newMeshCollider.convex = forceConvex;
                // newMeshCollider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation 
                //     | MeshColliderCookingOptions.EnableMeshCleaning 
                //     | MeshColliderCookingOptions.WeldColocatedVertices 
                //     | MeshColliderCookingOptions.UseFastMidphase;
                newMeshCollider.isTrigger = isTrigger;
                newMeshCollider.sharedMesh = newMesh;

                meshList.Add( newMesh );

            } // otherwise, generate individual colliders for each brush solid
            else 
            { 
                foreach ( var solid in solids ) {   
                    // does the brush have an entity data override that was non solid? then ignore this brush
                    if ( solid.entityDataOverride != null && config.IsEntityNonsolid(solid.entityDataOverride.ClassName) )
                        continue;

                    // box collider is the simplest, so we should always try it first       
                    if ( config.colliderMode != ScopaMapConfig.ColliderImportMode.ConvexMeshColliderOnly && TryAddBoxCollider(gameObject, solid, config, isTrigger) ) 
                        continue;

                    // otherwise, use a convex mesh collider
                    var newMeshCollider = AddMeshCollider(gameObject, solid, config, solid.entityDataOverride != null ? config.IsEntityTrigger(solid.entityDataOverride.ClassName) : isTrigger);
                    newMeshCollider.name = namePrefix + "-" + ent.ClassName + "#" + solid.id + "-Collider";
                    meshList.Add( newMeshCollider ); 
                }
            }

            return meshList;
        }

        /// <summary> given a brush solid, calculate the AABB bounds for all its vertices, and add that Box Collider to the gameObject </summary>
        static bool TryAddBoxCollider(GameObject gameObject, Solid solid, ScopaMapConfig config, bool isTrigger = false) {
            verts.Clear();

            for ( int x=0; x<solid.Faces.Count; x++ ) {
                if ( config.colliderMode != ScopaMapConfig.ColliderImportMode.BoxColliderOnly && !solid.Faces[x].Plane.IsOrthogonal() ) {
                    return false;
                } else {
                    for( int y=0; y<solid.Faces[x].Vertices.Count; y++) {
                        verts.Add( solid.Faces[x].Vertices[y] * config.scalingFactor - gameObject.transform.position);
                    }
                }
            }

            var bounds = GeometryUtility.CalculateBounds(verts.ToArray(), Matrix4x4.identity);
            var newGO = new GameObject("BoxCollider#" + solid.id.ToString() );
            newGO.transform.SetParent( gameObject.transform );
            newGO.transform.localPosition = Vector3.zero;
            newGO.transform.localRotation = Quaternion.identity;
            newGO.transform.localScale = Vector3.one;
            var boxCol = newGO.AddComponent<BoxCollider>();
            boxCol.center = bounds.center;
            boxCol.size = bounds.size;
            boxCol.isTrigger = isTrigger;
            return true;
        }

        /// <summary> given a brush solid, build a convex mesh from its vertices, and add that Mesh Collider to the gameObject </summary>
        static Mesh AddMeshCollider(GameObject gameObject, Solid solid, ScopaMapConfig config, bool isTrigger = false) {
            ClearMeshBuffers();
            BufferMeshDataFromSolid(solid, config, null, true);
            var newMesh = BuildMeshFromBuffers( solid.id.ToString() + "-Collider", config, gameObject.transform.position, -1);
        
            var newGO = new GameObject("MeshColliderConvex#" + solid.id.ToString() );
            newGO.transform.SetParent( gameObject.transform );
            newGO.transform.localPosition = Vector3.zero;
            newGO.transform.localRotation = Quaternion.identity;
            newGO.transform.localScale = Vector3.one;
            var newMeshCollider = newGO.AddComponent<MeshCollider>();
            newMeshCollider.convex = true;
            // newMeshCollider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation 
            //     | MeshColliderCookingOptions.EnableMeshCleaning 
            //     | MeshColliderCookingOptions.WeldColocatedVertices 
            //     | MeshColliderCookingOptions.UseFastMidphase;
            newMeshCollider.isTrigger = isTrigger;
            newMeshCollider.sharedMesh = newMesh;
            
            return newMesh;
        }

        static float Frac(float decimalNumber) {
            if ( Mathf.Round(decimalNumber) > decimalNumber ) {
                return (Mathf.Ceil(decimalNumber) - decimalNumber);
            } else {
                return (decimalNumber - Mathf.Floor(decimalNumber));
            }
        }

        static void ClearMeshBuffers() {
            verts.Clear();
            tris.Clear();
            uvs.Clear();
        }

        public static bool IsValidPath(string newPath) {
            return !string.IsNullOrWhiteSpace(newPath) && System.IO.Directory.Exists( System.IO.Path.GetDirectoryName(newPath) );
        }

    }

}