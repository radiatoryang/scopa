# Scopa documentation

This plugin and documentation assumes familiarity with Quake-style .MAP files and concepts. 

If you're never made a Quake / Half-Life / Source engine level before, then this tool will be less useful to you AND these docs will be hard to follow.

# WARNING: much of this document isn't implemented / is broken / is subject to change. THIS TOOL IS NOT READY FOR PUBLIC USE.

## Installation

This is a custom [Unity Package](https://docs.unity3d.com/Manual/PackagesList.html) that can be automatically installed / updated in Unity 2019.3 or later.

1. in Unity, open the [Package Manager window](https://docs.unity3d.com/Manual/upm-ui.html)
2. click the "+" button and select "add package from Git URL" [(more info)](https://docs.unity3d.com/2021.2/Documentation/Manual/upm-ui-giturl.html)
3. paste the .git URL of this repo: `https://github.com/radiatoryang/scopa.git` and click Add.

If you're using an older version of Unity, or don't want to use the Package Manager, then clone, submodule, or download+unzip this repo into your /Assets/ or /Packages/ folder. 

## Usage

Scopa works at editor-time or runtime.

### Editor-time usage

Put a .MAP or .WAD somewhere in the `/Assets/` folder, and Unity will automatically detect and import it like any other asset. 

Select the file in the Project tab and configure the import settings in the Inspector:

(image)

Hover over each setting for a tooltip with more information.

To save your changes, don't forget to click Apply at the bottom of the Inspector.

### Runtime usage

First, create an asset to configure how maps get imported at runtime:
1. in Unity, go to `Project tab > Create > Scopa > Map Config Asset` for .MAPs (or `Wad Config Asset` for .WADs)
2. This config asset is just Scriptable Object container for import settings. Select the asset and configure in the Inspector.

You can also simply create a `new ScopaMapConfig()` or `new ScopaWadConfig()` and configure it via code.

Then in your game code:
1. add `using Scopa;` at the top of the C# file
2. call `ScopaCore.ParseMap()` to read the .MAP data
3. call `ScopaCore.BuildMapIntoGameObject()` with the map data and config.

## Pipeline

Some general advice -- **treat the .MAP file as the "single source of truth."** Do all level edits in a .MAP level editor like TrenchBroom. Avoid editing the imported game object prefab instance in Unity, **because your changes might be erased when you re-import the .MAP again.**

### Level geometry

(geometry inspector)

Every entity (including worldspawn) is imported as two types of game objects:
- an **entity object** which holds entity information and colliders; one per entity
- a **mesh object** with mesh filter and mesh renderer component; one per material
    - all of the entity's brushes are merged together, then sorted by material; each material gets its own mesh object
    - for example: if a func_wall uses 3 different textures, that may result in 3 mesh objects

Basically, every brush entity is a separate mesh. For maximum optimization and fewest draw calls:
- use static batching
- use as few entities as possible
- use as few textures per entity as possible

When possible, we follow Quake conventions with sensible defaults:
- Converts Quake's Z-up axis to Unity's Y-up axis
- Brushes and entity origins are imported at a default scaling factor of 0.03125 (1 Unity meter = 32 Quake units)
- By default, texture names containing "sky", "skip", "trigger", "clip", "hint", "nodraw", or "null" will cause that face to be discarded from the mesh.


### Collision

(collision inspector)

Giant complex 3D levels are tricky, so we provide several options for generating colliders:
- **None**: if you want to setup collision yourself.
- **Box Colliders**: the most efficient and stable collider.
    - But not very accurate, if the shape isn't an axis-aligned box.
- **Convex Mesh Colliders**: somewhat efficient colliders, can be any convex shape. Limited to 255 faces at most.
    - An ok compromise, well suited for brush-based construction. 
    - If you rotate a box to be off-axis, we have no way of knowing it's actually a box and will make a mesh collider for it anyway.
    - We can't tell if two brushes are the same shape, and will generate a separate collision mesh for each.
- **Box and Convex**: uses box colliders when the brush is an axis-aligned box shape, otherwise generates a convex mesh collider.
    - Generates lots of components, and thus lots of Unity import warnings.
- **Big Concave Mesh Collider**: the least efficient collider, basically forces PhysX to test collisions against every triangle. All brushes get merged together into one big collider component.
    - Generates the fewest meshes and fewest components.
    - But usually less stable / more process intensive
    - Cannot be used for triggers / as physics rigidbodies. (Scopa will fallback to Box and Convex mode for triggers.)

Your preferred collision setup will depend on the game concept and camera perspective.
- Anything with boxy levels and flat floors could use box colliders for everything.
- Zoomed out cameras mean collision can be less accurate, and box colliders could maybe be good enough.
- Lots of physics objects moving at fast speeds? Probably avoid concave mesh colliders.

Again, we follow Quake conventions with sensible defaults:
- By default, entities with "illusionary" (e.g. func_illusionary) in the classname won't have any generated colliders.
- By default, entities with "trigger" or "water" in the classname (e.g. trigger_multiple, func_water) will have their colliders marked as non-solid triggers, and force Box and Convex collider mode.


### Texturing

Quake 1 texture are stored in files called WADs (WAD2 format), which match a specific 256 color palette used across the entire game. To create and edit these WADs, use tools like TexMex or Wally. 

We import WADs as a bundle of Texture2Ds, and we can also generate basic opaque / alpha cutout Materials based on templates that you define.

(inspector image)

If "Find Materials" is enabled on the .MAP, we try to match each face's texture name with a similarly named Material anywhere in the project. You don't have to use WADs; this auto-detect function will work with all materials. (Note: Find Materials only works at editor-time, not runtime.)

You can also manually set a specific `MaterialOverride` for each texture name. This also lets you bind additional properties to the material:
- **`**Hotspot UVs**`**: automatically unwraps face to match a rectangle defined in a Hotspot Texture Atlas (`Project > Create > Scopa > Hotspot Texture`). The UVs can be randomly rotated and flipped for additional variation.


### Entities

Each entity has a classname, and based on that classname we can swap in a prefab template. For example, for every entity type "light_wall_torch_small", we can replace it with a "Light - Torch" prefab. 

This gives you strong control over every entity. You can configure tags, layers, static flags, renderer settings, add extra colliders... or to read entity information, add a `Scopa Entity` component, and then have your components poll it for data.

### FGD generation

To place items, NPCs, etc. in a level editor, you need to load a .FGD entity definition file into the tool.

Quake modders still write their FGDs by hand, which is sad, so instead we provide a built-in FGD generator. The workflow looks like this:

1. in Unity, go to the Project tab and select `Create > Scopa > FGD Config Asset`.
2. select the new FGD Config Asset file and edit it in the Inspector tab: create new entity types, add properties, etc.
3. when you're ready, click the `Export FGD...` button at the top of the Inspector, and save your new .FGD file

(PLANNED) Additionally, it can also generate API docs as .MD or .HTML, to serve as documentation for level designers.

We recommend saving the .FGD file **outside of the /Assets/ folder**, because you should not set your level editor to load your entire Unity project.

### Built-in default entities

We provide a basic `ScopaBuiltinFGD` that defines some basic useful entity types and demonstrates how to setup FGD bindings.

TODO: move this documentation to generated API docs

#### 1. (planned) Mesh groups

**Scopa has no map compiling nor VIS process. The difference between world brushes vs. brush entities doesn't matter.** Instead, brush entities are useful because each entity creates another game object / mesh group. This gives you control over how your level geometry gets imported and chunked:

- `func_detail`: static mesh with colliders
- `func_detail_illusionary`: static mesh with NO colliders (also NOT Navigation Static)
- `func_wall`: non-static mesh with colliders (can be moved, rotated, or resized during the game)
- `func_illusionary`: non-static mesh with NO colliders (can be moved, rotated, or resized during the game)
- `func_physbox`: non-static mesh with colliders AND a rigidbody with physics simulation

World brushes are treated like func_detail, except they're bound to the worldspawn entity.

Each entity brush also uses a `ScopaBuiltinBrush` entity base, which adds the following properties:

- `_convex`: if enabled, merges all colliders into one convex mesh collider, useful for turning a func_detail staircase into a ramp... this obviously does nothing for illusionary entities, which have no colliders
- `_shadow`: lets you control the Cast Shadows setting on the mesh renderers, can be set to Off, On, Both Sides, and Shadows Only.
- `_phong`: smooth face normals for entire mesh to avoid hard seams
- `_layer`: override the Unity layer for this entity; the name must match the layer name exactly!
- `_tag`: set a Unity tag for this entity; the tag name must match an existing defined tag name exactly!

Theoretically, the most optimized approach is to make your entire map one giant func_detail_illusionary. This would combine all brushes into the fewest meshes possible, mark it for static batching, and omit colliders.

#### 2. (planned) Triggers, logic, buttons and doors

We implement a basic trigger / scripting system. In Quake / Half-Life, it's up to the entity to decide what happens when it is activated

- `trigger_once`: when a collider enters this brush group, it activates its `target`, and then never again
- `trigger_multiple`: same as above, except it can reset itself and be triggered again

- `logic_relay`: point entity, triggers something else when triggered
- `logic_counter`: point entity, counts how many times it gets triggered, and then fires once it reaches a threshold
- `logic_timer`: point entity, triggers repeatedly after a certain amount of seconds
- `logic_lock`: point entity, can lock or unlock an entity; locked entities cannot activate at all

- `func_button`: activated when a moving object touches it
- `func_door`: if named, it slides open when triggered; otherwise, automatically opens when something enters its trigger; unless locked
- `func_door_rotating`: same as above, except rotates around a user-defined local axis
- `func_rotate`: spins around, like a fan

These entities all use a `ScopaTriggerable` entity base, which adds the following properties:

- `targetname`: the name of this object, only for trigger / logic purposes... it does not affect the Unity game object name at all
- `delay`: time in seconds to wait before activating
- `target`: the targetname of the thing to trigger... **if multiple entities share the same targetname, they will all be triggered**
- `killtarget`: the targetname of the thing to `Destroy()`, or delete from the game state
- `trigger_tag`: if set, only objects with this tag can physically trigger the object
- `trigger_layer`: if set, only objects on this layer can physically trigger the object
- `locked`: default state if the object starts locked

There are no plans for a Half-Life 2 / Source Engine-like entity I/O system, since it is not implemented in TrenchBroom.

#### 3. (planned) Lights

We follow the Half-Life pattern here and provide three different light entities corresponding to basic light types in Unity:

- `light`: Point light, local
- `light_spot`: Spotlight, local
- `light_environment`: Directional light, global

We also borrow some inspiration from Half-Life 2 / Source Engine:

- `env_cubemap`: Reflection probe
    - overrides `OnScopaLateImport()` to fire raycasts in all directions and configure its own box bounds

Unity does not have fully runtime lightmapping or GI baking. If you support modding for your game, then user-generated maps can only use realtime lights. 

We purposely don't support ambient lighting, since overriding the Light Settings asset is a bit messy and complicated.

#### 4. (planned) Audio

- `ambient_generic`: All purpose audio source, can be looped with adjustable attenuation


## Legal / Acknowledgments / Credits

See the README.
