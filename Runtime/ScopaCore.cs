using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using Sledge.Formats.Map.Formats;
using Sledge.Formats.Map.Objects;
using Scopa.Formats.Texture.Wad;
using Scopa.Formats.Id;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine.SceneManagement;
using Mesh = UnityEngine.Mesh;
using Debug = UnityEngine.Debug;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Scopa {
    /// <summary>main class for core Scopa MAP functions</summary>
    public static class ScopaCore {
        /// <summary>The main high-level map file import function. Use this to support mods. For editor-time import with asset handling, see ImportMapInEditor().</summary>
        public static GameObject ImportMap(string mapFilepath, ScopaMapConfig currentConfig, out List<IScopaMeshResult> meshList)
        {
            var parseTimer = new Stopwatch();
            parseTimer.Start();
            var mapFile = ScopaCore.ParseMap(mapFilepath, currentConfig);
            var mapName = System.IO.Path.GetFileNameWithoutExtension(mapFilepath);
            parseTimer.Stop();

            // this is where the magic happens
            var buildTimer = new Stopwatch();
            buildTimer.Start();
            var gameObject = ScopaCore.BuildMapIntoGameObject(mapName, mapFile, currentConfig, out meshList);
            buildTimer.Stop();

            Debug.Log($"imported {mapFilepath}\n Parsed in {parseTimer.ElapsedMilliseconds} ms, Built in {buildTimer.ElapsedMilliseconds} ms", gameObject);
            return gameObject;
        }

        /// <summary>Parses the map file text data into a usable data structure. Also detects the file extension and selects the appropriate file format handler.</summary>
        public static MapFile ParseMap( string pathToMapFile, ScopaMapConfig config ) {
            IMapFormat importer = null;
            var fileExtension = System.IO.Path.GetExtension(pathToMapFile).ToLowerInvariant();
            switch(fileExtension) {
                case ".map":
                importer = new QuakeMapFormat(); break;
                case ".rmf":
                importer = new WorldcraftRmfFormat(); break;
                case ".vmf":
                importer = new HammerVmfFormat(); break;
                case ".jmf":
                importer = new JackhammerJmfFormat(); break;
                default:
                Debug.LogError($"No file importer found for {pathToMapFile}");
                return null;
            }

            MapFile mapFile = null;
            using (var fo = new FileStream(pathToMapFile, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan))
            {
                mapFile = importer.Read(fo);
            }

            return mapFile;
        }

        /// <summary>The main function for converting parsed MapFile data into a Unity GameObject with 3D mesh and colliders.
        /// Outputs a lists of built meshes (e.g. so UnityEditor can serialize them)</summary>
        public static GameObject BuildMapIntoGameObject( string mapName, MapFile mapFile, ScopaMapConfig config, out List<IScopaMeshResult> meshList ) {
            var rootGameObject = new GameObject( mapName );
            var mergedEntityData = PrepassEntityRecursive( mapFile.Worldspawn, mapFile.Worldspawn, config );

            meshList = new List<IScopaMeshResult>(8192);
            ScopaCore.AddGameObjectFromEntityRecursive(
                meshList, 
                rootGameObject, 
                new ScopaEntityData(mapFile.Worldspawn, 0), 
                mapName, 
                config, 
                config.findMaterials ? CacheMaterialSearch() : null,
                mergedEntityData
            );
            return rootGameObject;
        }

        static Dictionary<string, Material> CacheMaterialSearch() {
            #if UNITY_EDITOR
            // var findTimer = new Stopwatch();
            // findTimer.Start();

            var materialSearch = AssetDatabase.FindAssets("t:Material a:assets");

            var materials = new Dictionary<string, Material>(materialSearch.Length);
            foreach ( var materialSearchGUID in materialSearch) {
                var path = AssetDatabase.GUIDToAssetPath(materialSearchGUID);
                var firstAsset = AssetDatabase.LoadAssetAtPath<Material>(path);

                // if there's multiple Materials attached to one Asset, we have to do additional filtering
                if (AssetDatabase.IsSubAsset(firstAsset)) { 
                    var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                    foreach ( var asset in allAssets ) {
                        if ( asset != null && asset is Material) {
                            var key = asset.name.ToLowerInvariant();
                            if (!materials.ContainsKey(key))
                                materials.Add(key, asset as Material);
                        }
                    }
                } else { 
                    var key = firstAsset.name.ToLowerInvariant();
                    if (!materials.ContainsKey(key))
                        materials.Add(key, firstAsset);
                }
            }
            
            // findTimer.Stop();
            // Debug.Log($"CacheMaterialSearch: {findTimer.ElapsedMilliseconds} ms");
            // Debug.Log($"found {materials.Count} materials");
            
            return materials;
            #else
            Debug.Log("CacheMaterialSearch() is not available at runtime.");
            return null;
            #endif
        }

        /// <summary>Core recursive function for traversing entity tree and pre-passing each entity. 
        /// Before generating game objects, we may want to modify some of the MapFile data. 
        /// For example, when merging entities into worldspawn.</summary>
        static Dictionary<Solid, Entity> PrepassEntityRecursive( Worldspawn worldspawn, Entity ent, ScopaMapConfig config) {
            var mergedEntityData = new Dictionary<Solid, Entity>();
            for (int i=0; i<ent.Children.Count; i++) {
                if ( ent.Children[i] is Entity ) {
                    var moreMerges = PrepassEntityRecursive( worldspawn, ent.Children[i] as Entity, config );
                    foreach(var merge in moreMerges) {
                        mergedEntityData.Add(merge.Key, merge.Value);
                    }
                } // merge child brush to worldspawn
                else if ( config.IsEntityMergeToWorld(ent.ClassName) && ent.Children[i] is Solid ) {
                    mergedEntityData.Add(((Solid)ent.Children[i]), ent); // but preserve old entity data for mesh / collider generation
                    worldspawn.Children.Add(ent.Children[i]);
                    ent.Children.RemoveAt(i);
                    i--;
                    continue;
                }
            }
            return mergedEntityData;
        }

        /// <summary> The main core function for converting entities (worldspawn, func_, etc.) into 3D meshes. </summary>
        static void AddGameObjectFromEntityRecursive(List<IScopaMeshResult> meshList, GameObject rootGameObject, ScopaEntityData entityData, string namePrefix, ScopaMapConfig config, Dictionary<string, Material> materialSearch, Dictionary<Solid, Entity> mergedEntityData ) {
            AddGameObjectFromEntity(meshList, rootGameObject, entityData, namePrefix, config, materialSearch, mergedEntityData);
            var entityID = entityData.ID+1;
 
            foreach ( var child in entityData.Children ) {
                if ( child is Entity && config.IsEntityMergeToWorld(((Entity)child).ClassName) == false ) {
                    var childEntity = child as Entity;
                    AddGameObjectFromEntityRecursive(meshList, rootGameObject, new ScopaEntityData(childEntity, entityID), namePrefix, config, materialSearch, mergedEntityData);
                    entityID += 1 + child.Children.Count;
                }
            }
        }

        public static void AddGameObjectFromEntity(List<IScopaMeshResult> meshList, GameObject rootGameObject, ScopaEntityData entData, string namePrefix, ScopaMapConfig config, Dictionary<string, Material> materialSearch, Dictionary<Solid, Entity> mergedEntityData) {
            var entityOrigin = Vector3.zero;
            if ( entData.TryGetVector3Scaled("origin", out var scaledPos, config.scalingFactor) ) {
                entityOrigin = scaledPos;
            } 
            
            var entityName = $"{entData.ClassName}#{entData.ID}";
            var solids = entData.Children.Where( x => x is Solid).Cast<Solid>().ToArray();
            var brushJobs = solids.Length == 0 ? null : 
                new ScopaMesh.ScopaMeshJobGroup(
                    config,
                    $"{namePrefix}_{entityName}",
                    entityOrigin,
                    entData,
                    solids,
                    mergedEntityData,
                    materialSearch
                );

            var entityPrefab = config.GetEntityPrefabFor(entData.ClassName);
            var entityObject = InstantiateOrCreateEmpty(rootGameObject.transform, entityPrefab, entData, config);

            entityObject.name = entityName;
            if ( entData.TryGetString("targetname", out var targetName) )
                entityObject.name += " " + targetName;
            entityObject.transform.position = entityOrigin;

            // for point entities, import the "angle" property
            if ( solids.Length == 0 ) { // if there's no meshes and it's a point entity, then it has angles
                if ( entData.TryGetAngles3D("angles", out var angles) )
                    entityObject.transform.localRotation = angles;
                else if (entData.TryGetAngleSingle("angle", out var angle))
                    entityObject.transform.localRotation = angle;
            }
                
            if (brushJobs != null) {
                brushJobs.CompleteJobsAndGetResults();
                InstantiateRenderers(brushJobs, entityObject);
                InstantiateColliders(brushJobs, entityObject);
                meshList.AddRange(brushJobs.rendererResults);
                meshList.AddRange(brushJobs.colliderResults);
                brushJobs.DisposeJobsData();
            }

            // populate rest of entity data + notify custom user components that import is complete
            UpdateEntityComponents(config, entData, entityObject);
        }

        static void InstantiateRenderers(ScopaMesh.ScopaMeshJobGroup jobData, GameObject entityObject) {
            var entityMeshPrefab = jobData.config.GetMeshPrefabFor(jobData.entityData.ClassName);
            var results = jobData.rendererResults;
            var config = jobData.config;
            foreach(var result in results) {

                var thisMeshPrefab = entityMeshPrefab;
                var materialConfig = result.material.materialConfig;
                // {namePrefix}-{entityObject.name}-{textureKVP.Key}

                // the material config might have a meshPrefab defined too; use that if there isn't already a meshPrefab set already
                if (entityMeshPrefab == null && materialConfig != null && materialConfig.meshPrefab != null ) {
                    thisMeshPrefab = materialConfig.meshPrefab;
                }

                var newMeshObj = InstantiateOrCreateEmpty(entityObject.transform, thisMeshPrefab, jobData.entityData, config);
                newMeshObj.name = result.material.GetTextureNameForSearching();

                // // detect smoothing angle, if defined via map config or material config or entity
                // var smoothNormalAngle = config.defaultSmoothingAngle;
                // if (entData.TryGetBool("_phong", out var phong)) {
                //     if ( phong ) {
                //         if ( entData.TryGetFloat("_phong_angle", out var phongAngle) ) {
                //             smoothNormalAngle = Mathf.RoundToInt( phongAngle );
                //         }
                //     } else {
                //         smoothNormalAngle = -1;
                //     }
                // } else if ( textureKVP.Value != null && textureKVP.Value.materialConfig != null && textureKVP.Value.materialConfig.smoothingAngle >= 0 ) {
                //     smoothNormalAngle = textureKVP.Value.materialConfig.smoothingAngle;
                // }

                // you can inherit ScopaMaterialConfig + override OnBuildMeshObject for extra per-material import logic
                if ( materialConfig != null && materialConfig.useOnBuildMeshObject ) {
                    materialConfig.OnBuildMeshObject( newMeshObj, result.mesh, jobData );
                }

                // now, finally, at the very end, optimize
                // If optimize everything, just combine the two optimizations into one call
                    if ((config.optimizeMesh & ScopaMapConfig.ModelImporterMeshOptimization.OptimizeIndexBuffers) != 0 &&
                        (config.optimizeMesh & ScopaMapConfig.ModelImporterMeshOptimization.OptimizeVertexBuffers) != 0) {
                        result.mesh.Optimize();
                    } else {
                        if ((config.optimizeMesh & ScopaMapConfig.ModelImporterMeshOptimization.OptimizeIndexBuffers) != 0)
                            result.mesh.OptimizeIndexBuffers();
                        if ((config.optimizeMesh & ScopaMapConfig.ModelImporterMeshOptimization.OptimizeVertexBuffers) != 0)
                            result.mesh.OptimizeReorderVertexBuffer();
                    }

                // populate components... if the mesh components aren't there, then add them
                if ( newMeshObj.TryGetComponent<MeshFilter>(out var meshFilter) == false ) {
                    meshFilter = newMeshObj.AddComponent<MeshFilter>();
                }
                meshFilter.sharedMesh = result.mesh;

                bool addedMeshRenderer = false;
                if ( newMeshObj.TryGetComponent<MeshRenderer>(out var meshRenderer) == false ) {
                    meshRenderer = newMeshObj.AddComponent<MeshRenderer>();
                    addedMeshRenderer = true;
                }
                meshRenderer.sharedMaterial = result.material.material;

                if ( addedMeshRenderer ) { // if we added a generic mesh renderer, then set default shadow caster setting too
                    meshRenderer.shadowCastingMode = jobData.config.castShadows;
                }
            }
        }

        static void InstantiateColliders(ScopaMesh.ScopaMeshJobGroup jobData, GameObject entityObject) {
            var results = jobData.colliderResults;
            for (int i = 0; i < results.Count; i++) {
                var result = results[i];

                if (result.mesh != null && result.mesh.vertexCount > 0) { // MESH COLLIDER
                    var newGO = InstantiateOrCreateEmpty(entityObject.transform, null, jobData.entityData, jobData.config);
                    newGO.name = string.Format("Collider{0}", i.ToString("D5", System.Globalization.CultureInfo.InvariantCulture));

                    var newMeshCollider = newGO.AddComponent<MeshCollider>();
                    newMeshCollider.convex = result.solidity == ScopaMesh.ColliderSolidity.Trigger || result.solidity == ScopaMesh.ColliderSolidity.SolidConvex;
                    newMeshCollider.isTrigger = result.solidity == ScopaMesh.ColliderSolidity.Trigger;
                    newMeshCollider.sharedMesh = result.mesh;
                } else if (math.lengthsq(result.boxColliderData.size) > 0.01f) { // BOX COLLIDER
                    var newGO = InstantiateOrCreateEmpty(entityObject.transform, null, jobData.entityData, jobData.config);
                    newGO.name = string.Format("Collider{0}", i.ToString("D5", System.Globalization.CultureInfo.InvariantCulture));

                    var box = result.boxColliderData; // still have to convert from Quake space to Unity space
                    newGO.transform.localPosition = box.position.xzy * jobData.config.scalingFactor;
                    newGO.transform.localEulerAngles = new Vector3(box.eulerAngles.x, -box.eulerAngles.z, box.eulerAngles.y) * Mathf.Rad2Deg;

                    var boxCol = newGO.AddComponent<BoxCollider>();
                    boxCol.center = Vector3.zero;
                    boxCol.size = box.size.xzy * jobData.config.scalingFactor;
                    boxCol.isTrigger = result.solidity == ScopaMesh.ColliderSolidity.Trigger;
                }
            }
        }

        static void UpdateEntityComponents(ScopaMapConfig config, ScopaEntityData entData, GameObject entityObject) {
            var entityComponent = entityObject.GetComponent<IScopaEntityData>();

            if ( config.addScopaEntityComponent && entityComponent == null)
                entityComponent = entityObject.AddComponent<ScopaEntity>();

            if ( entityComponent != null)
                entityComponent.entityData = entData;

            var allEntityComponents = entityObject.GetComponentsInChildren<IScopaEntityImport>();
            foreach( var entComp in allEntityComponents ) { 
                if ( !entComp.IsImportEnabled() )
                    continue;

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
                            case BindFgd.VarType.Vector3Unscaled:
                                if ( entData.TryGetVector3Unscaled(attribute.propertyKey, out var vec3Unscaled) )
                                    objectFields[i].SetValue(entComp, vec3Unscaled);
                                break;
                            case BindFgd.VarType.Angles3D:
                                if ( entData.TryGetAngles3D(attribute.propertyKey, out var angle3D) )
                                    objectFields[i].SetValue(entComp, angle3D.eulerAngles);
                                break;
                            default:
                                Debug.LogError( $"BindFgd named {objectFields[i].Name} / {attribute.propertyKey} has FGD var type {attribute.propertyType} ... but no case handler for it yet!");
                                break;
                        }
                    }
                }

                if ( config.callOnEntityImport )
                    entComp.OnEntityImport( entData );
            }
        }

        static GameObject InstantiateOrCreateEmpty(Transform rootParent, GameObject prefab, ScopaEntityData entData, ScopaMapConfig config) {
            GameObject newObj = null;
            if ( prefab != null ) {
                #if UNITY_EDITOR
                newObj = PrefabUtility.InstantiatePrefab(prefab) as GameObject; // maintain prefab linkage
                #else
                newObj = UnityEngine.Object.Instantiate(entityPrefab);
                #endif
            } else {
                newObj = new GameObject();
            }

            // only set Layer if it's a generic game object OR if there's a layer override
            // if user set a specific prefab, it probably has its own static flags and layer
            // ... but if it's a generic game object we made, then we set it ourselves
            if ( entData.TryGetString("_layer", out var layerName) ) { // or did they set a specifc override on this entity?
                newObj.layer = LayerMask.NameToLayer(layerName);
            } else if ( prefab == null ) { 
                newObj.layer = config.layer;
            }

            if ( prefab == null && config.IsEntityStatic(entData.ClassName)) {
                SetGameObjectStatic(newObj, !config.IsEntityNonsolid(entData.ClassName) && !config.IsEntityTrigger(entData.ClassName));
            }

            newObj.transform.SetParent(rootParent);
            newObj.transform.localPosition = Vector3.zero;
            newObj.transform.localRotation = Quaternion.identity;
            newObj.transform.localScale = Vector3.one;

            return newObj;
        }

        static void SetGameObjectStatic(GameObject go, bool isNavigationStatic = true) {
            if ( isNavigationStatic ) {
                go.isStatic = true;
            } else {
                #if UNITY_EDITOR
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI 
                    | StaticEditorFlags.OccluderStatic 
                    | StaticEditorFlags.BatchingStatic 
                    | StaticEditorFlags.OccludeeStatic 
                    #if !UNITY_2022_2_OR_NEWER 
                    | StaticEditorFlags.OffMeshLinkGeneration 
                    #endif
                    | StaticEditorFlags.ReflectionProbeStatic
                );
                #endif
            }
        }

        public static bool IsValidPath(string newPath)
        {
            return !string.IsNullOrWhiteSpace(newPath) && System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(newPath));
        }

    }

    public interface IScopaMeshResult {
        public abstract Mesh GetMesh();
    }

    public class ScopaRendererMeshResult: IScopaMeshResult {
        public Mesh mesh;
        public ScopaEntityData entityData;
        public ScopaMapConfig.MaterialOverride material;

        public ScopaRendererMeshResult(Mesh mesh) {
            this.mesh = mesh;
        }

        public ScopaRendererMeshResult(Mesh mesh, ScopaEntityData entityData, ScopaMapConfig.MaterialOverride materialConfig) {
            this.mesh = mesh;
            this.entityData = entityData;
            this.material = materialConfig;
        }

        public Mesh GetMesh() => mesh;
    }

    public class ScopaColliderResult: IScopaMeshResult {
        public Mesh mesh;
        public ScopaMesh.ScopaBoxColliderData boxColliderData;
        public ScopaEntityData entityData;
        public ScopaMesh.ColliderSolidity solidity;

        public ScopaColliderResult(Mesh mesh, ScopaEntityData entityData, ScopaMesh.ColliderSolidity solidity) {
            this.mesh = mesh;
            this.entityData = entityData;
            this.solidity = solidity;
        }

        public ScopaColliderResult(ScopaMesh.ScopaBoxColliderData box, ScopaEntityData entityData, ScopaMesh.ColliderSolidity solidity) {
            this.boxColliderData = box;
            this.entityData = entityData;
            this.solidity = solidity;
        }

        public Mesh GetMesh() => mesh;
    }

}