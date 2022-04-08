# Scopa documentation

This plugin and documentation assumes familiarity with Quake-style .MAP files and concepts. 

If you're never made a Quake / Half-Life / Source engine level before, then this tool will be less useful to you AND these docs will be hard to follow.

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

You can also manually set a specific Material for each texture name.


### Entity data / game logic

To let people define game objects and add behaviours in the .MAP, you should write a custom .FGD file to load into the level editor.

- (TODO) Basic .FGD example and template
- (TODO) FGD generator?
- (TODO) Tutorial: How to write a .FGD

Each entity has a classname, and based on that classname we can swap in a prefab template. For example, for every entity type "light_wall_torch_small", we can replace it with a "Light - Torch" prefab. 

This gives you strong control over every entity. You can configure tags, layers, static flags, renderer settings, add extra colliders... or to read entity information, add a Scopa Entity component, and then have your components poll it for data.




## Limitations

- **This package doesn't have game code or entity logic.** It imports MAP files as 3D models, sets up colliders, and provides hooks for importing entity data. That's it.

## Acknowledgments / Credits

- based on [Sledge Formats](https://github.com/LogicAndTrick/sledge-formats)
- looking for a BSP plugin? try [Unity3D BSP Importer](https://github.com/wfowler1/Unity3D-BSP-Importer)