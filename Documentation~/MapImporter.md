# Map Importer

## Supported map file formats

- Quake 1 .MAP (recommended)
- Quake 2 .MAP*
- Half-Life .RMF*
- Source .VMF*
- Jackhammer .JMF*

<code>*</code> Note that we support the Quake 1 v220 Trenchbroom .MAP as the core feature set. For any other file format, anything beyond these core features (brushes and entity keyvalue pairs) is not supported:
- Non-brush geometry (VMF displacement, Quake 2 brush face flags, etc.) is not supported.
- Source VMF entity I/O is not supported, only standard Quake / Half-Life style keyvalues.
- Editor cameras, visgroups, and smoothing groups are ignored.
- basically, if [Sledge-Formats](https://github.com/LogicAndTrick/sledge-formats) doesn't support it, then we sure don't either.

## How to import a map file

**At editor-time,** put the map file somewhere in the /Assets/ folder. Unity will automatically detect the file and import it. Check the console for any import error messages.

**At runtime,** this is a two step process: create an import config asset at editor-time or runtime, then call `ScopaCore.ImportMap()` and pass in the config.

To create a config asset at editor-time:
1. In Unity, go to `Project tab > Create > Scopa > Map Config Asset`. This config asset is just a `ScriptableObject` container for a `ScopaMapConfig`. 
2. Select the asset and configure in the Inspector.

Or t create a config asset at runtime:
1. In C#, create a `new ScopaMapConfig()`.
2. Configure the public variables as you see fit.

Then in your game code, pass the config into `ScopaCore.ImportMap()`, which returns a reference to the root game object for the entire map file. 
1. In C#, add `using Scopa;` at the top.
2. Use `ScopaCore.ParseMap()` to read the map data. This can take a few seconds for large map files.
3. Use `ScopaCore.BuildMapIntoGameObject()` with the map data and config. Depending on the map size and config, it can take a while. For big complex MAPs with lots of features, expect 5-20 seconds.

## MAP Importer settings

**Save as Asset...** lets you externalize the map import settings into a separate asset file, which lets you share the same config for multiple MAP files.
- While useful, this can desynchronize your workflow. Editing the Map Config Asset does *not* automatically trigger reimport for all associated MAP files. The config asset does not track which MAP importers use it. You will have to manually reimport / refresh the MAP files to reflect a changed config.

### MESHES

(image: geometry inspector)

**Scaling Factor**: _(default: 0.03125, 1 Unity meter = 32 Quake map units)_ The global scaling factor for all brush geometry and certain entity data.
- We automatically convert Quake / Half-Life / Source Z-up axis to Unity's Y-up axis.
- Entity origins will be converted as well, but you have to manually configure custom entity properties for scaling. See the [Entity Import workflow](EntityImport.md).

**Snapping Threshold**: _(default: 1)_ vertex snap distance threshold in unscaled map units. Pretty important for minimizing seams and cracks on complex non-rectilinear brushes. In the map editor, avoid building smaller than this threshold. Set to 0 to disable for slightly faster import times, but you may get more seams and hairline cracks.
- We don't weld vertices because each brush face needs separate normals and UVs.

**Default Smoothing Angle**: _(default: 80 degrees)_ smooth shading on edges, which adds extra import time; set to -1 to disable default global smoothing, and/or override with `_phong` / `_phong_angle` entity keyvalues, or in a Scopa Material Config.
- Again, we don't weld vertices because each face needs separate normals and UVs. This will attempt to smooth a hard edge, but won't remove it entirely.

**Remove Hidden Faces**: _(default: true)_ Try to detect whether a face is completely covered by another face within the same entity, and discard it. It's far from perfect; it can't detect if a face is covered by 2+ faces. But it helps. Note the extra calculations increase map import times.

**Add Tangents**: _(default: true)_ Generate tangent data needed for normal mapping. If you're not using normal maps, disable for small memory savings.

**Add Lightmap UV2s**: _(EDITOR-ONLY) (default: true)_ Generate lightmap UVs using Unity's built-in lightmap unwrapper. If you're not using lightmaps, maybe disable for small memory savings.

**Mesh Compression**: _(EDITOR-ONLY) (default: Off)_ Use Unity's built-in mesh compressor. Reduces file size but may cause glitches and seams.

**Bake Vertex Color AO**: _(default: false)_ After all meshes and colliders are generated, fire semi-randomized physics raycasts for each mesh vertex to approximate ambient occlusion, and store as a grayscale vertex color. Just make sure your shader actually uses the vertex colors! In the shader, we suggest using vertex color AO to scale GI or Occlusion.

- **Occlusion Length**: _(default: 25)_ The length (in Unity meters) of the vertex color AO raycasts. Shorter raycasts limit AO to small details, while longer raycasts may feel more like local room shadowing obscurance effects.

**Cull Textures**: _(default: sky, trigger, skip, hint, nodraw, null, clip, origin)_ When a face's texture name contains any word in this list, discard that face from the mesh. But this does not affect mesh colliders.
- Because you're not compiling these maps into BSPs, you don't have to seal them from leaks, and likely won't need sky brushes.

### COLLIDERS

(image: collider inspector)

**Collider Mode**: _(default: Box and Convex)_ For each brush we add a collider. Axis-aligned boxy brushes use Box Colliders, anything else gets a convex Mesh Collider. You can also force just one type, or use a big complex expensive concave Mesh Collider.

- **None**: if you want to setup collision yourself.
- **Box Colliders**: the most efficient and stable collider.
    - But not very accurate, if the shape isn't an axis-aligned box.
    - Game with a zoomed out top down camera? Box colliders could be good enough.
- **Convex Mesh Colliders Only**: somewhat efficient colliders, can be any convex shape. Limited to 255 faces at most.
    - An ok compromise, well suited for brush-based construction. 
    - Scopa can't tell if two similar brushes are the same shape, and will generate a separate collision mesh for each.
- **Box and Convex**: uses box colliders when the brush is an axis-aligned box shape, otherwise generates a convex mesh collider.
    - This is the default setting because it provides the best accuracy and stability for first person games.
    - If you rotate a boxy brush off-grid, Scopa has no way of knowing it's actually a box, and will make a mesh collider for it anyway.
    - All "Trigger Entities" (see below) will use this mode.
- **Big Concave Mesh Collider**: the least efficient collider, basically forces PhysX to test collisions against every triangle. All brushes get merged together into one big collider component.
    - Generates the fewest meshes and fewest components.
    - Less stable and accurate. Terrible for fast moving physics objects. You've been warned.
    - Cannot be used for triggers / as physics rigidbodies. (Scopa will fallback to Box and Convex mode for triggers.)

**Nonsolid Entities**: _(default: illusionary)_ If an entity's classname contains a word in this list, do not generate a collider for it and disable Navigation Static for it too.

**Trigger Entities**: _(default: trigger, water)_ If an entity's classname contains a word in this list, mark that collider as a non-solid trigger and disable Navigation Static for it.

### TEXTURES & MATERIALS

(image: textures inspector)

**Find Materials**: _(EDITOR-ONLY) (default: true)_ Try to automatically match each texture name to a similarly named Material already in the project. This is editor-only because at runtime there is no searchable asset database.

**Global Texel Scale**: _(default: 1.0)_ the map-wide scaling factor for all texture faces; < 1.0 enlarges textures, > 1.0 shrinks textures. Use this in conjunction with the [WAD Exporter](WadExporter.md), e.g. if the WAD Exporter is set to Quarter resolution, then set this value to 0.25

**Default Tex Size**: _(default: 128)_ To calculate UVs, we need to know the texture image size in pixels; but if we can't find a matching texture, use this default size.

**Default Material**: _(optional)_ When we can't find a matching Material name, then use this default Material instead. If null, Scopa will attempt to fallback to an included blockout grid material, or the default Unity material.

**Material Overrides**: _(optional)_ Manually set a specific Material and Scopa Material Config for each texture name. This is also where you bind custom mesh functions too, see Scopa Material Config.

### GAME OBJECTS & ENTITIES

Every entity (including worldspawn) will have this game object hierarchy:
- top-most **entity parent** which holds entity information; _one per entity_
    - **mesh child** with mesh filter and mesh renderer component; _one per material_
        - all of the entity's brushes are merged together, then sorted by material; each material gets its own mesh object
        - for example: if a func_wall uses 3 different textures, that may result in 3 mesh objects
    - **collider child** with collider component; _one per brush (if using Box and Convex collider mode)_
        - we make a separate game object for every collider because Unity will complain otherwise
        - if using Concave collider mode, there will just be one mesh collider directly on the entity parent

(image: entities inspector)

**Merge To World**: _(default: func_group, func_detail)_ If an entity classname contains any word in this list, then merge its brushes (mesh and collider) into worldspawn and discard entity data. WARNING: most per-entity mesh and collider configs will be overriden by worldspawn; only the discarded entity's solidity will be respected.

**Static Entities**: _(default: worldspawn, func_wall)_ If an entity classname contains any word in this list AND it doesn't have prefab overrides (see Entity Overrides), then set its mesh objects to be static -- batching, lightmapping, navigation, reflection, everything. However, non-solid and trigger entities will NOT be navigation static.

**Layer**: _(default: Default)_ Set ALL objects to use this layer. For example, maybe you have a 'World' layer. To set per-entity layers, see Entity Prefab / Entity Overrides.

**Cast Shadows**: _(default: Two Sided)_ The shadow casting mode on all the mesh objects; but if a Mesh Prefab / Entity Override is defined, then use that prefab setting instead.

**Add Scopa Entity Component?**: _(default: true)_ if enabled, automatically add ScopaEntity component to all game objects (if not already present in the entityPrefab)... disable this if you don't want to use the built-in ScopaEntity at all, and override it with your own. See the [Entity Import workflow](EntityImport.md).

**Call OnEntityImport**: _(default: true)_ if enabled, will call `OnEntityImport()` on any script that implements `IScopaEntityImport`. See the [Entity Import workflow](EntityImport.md).

**Entity Prefab**: _(optional)_ Prefab template to use for the root of EVERY entity including worldspawn. Ignores the config-wide static / layer settings above.

**Mesh Prefab**: _(optional)_ Prefab template to use for each mesh + material in each entity. `meshFilter.sharedMesh` and `meshRenderer.sharedMaterial` will be overridden. Useful for setting layers, renderer settings, etc. Ignores the global static / layer settings above.

**Entity Overrides**: _(optional)_ Override the prefabs used for each entity type. For example, a door might need its own special prefab. Order matters, we use the first override that matches. Ignores the global static / layer settings above.

**FGD Asset**: _(optional)_ If there isn't an entity override defined above, then the next place we look for entity prefabs is in this FGD asset. Useful because it allows for multiple map configs to use the same set of entity overrides.

## Advice

For maximum optimization and fewest draw calls:
- use static batching
- use as few entities as possible
- use as few textures per entity as possible
- or use SRP Batcher and let the engine figure it out
