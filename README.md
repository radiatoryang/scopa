# Scopa

*Scopa* ("broom" in Latin) is a Unity level design plugin that adds Quake 1 .MAP file import. Relive the golden age of level design... today!

- generates a prefab with 3D meshes, colliders (box colliders and mesh colliders), hooks for entity data
- editor time: auto-imports .MAP files as if they were 3D models
- all parsing and model gen functions work at runtime too, if you want to add modding support... it's just a bunch of static functions in `Scopa.cs`

To build .MAP levels, we recommend the free open source multiplatform 3D level design tool [TrenchBroom](https://github.com/TrenchBroom/TrenchBroom).

# WARNING: this is in early development, it's broken and not ready for public use yet.

## Installation

This is a custom [Unity Package](https://docs.unity3d.com/Manual/PackagesList.html) that can be automatically installed / updated in Unity 2019.3 or later.

1. in Unity, open the [Package Manager window](https://docs.unity3d.com/Manual/upm-ui.html)
2. click the "+" button and select "add package from Git URL" [(more info)](https://docs.unity3d.com/2021.2/Documentation/Manual/upm-ui-giturl.html)
3. paste the .git URL of this repo: `https://github.com/radiatoryang/scopa.git` and click Add.

If you're using an older version of Unity, or don't want to use the Package Manager, then clone, submodule, or download+unzip this repo into your /Assets/ or /Packages/ folder. 

## Usage

To use in editor, just put a .MAP file somewhere in the /Assets/ folder. Scopa's custom ScriptedImporter will detect the file and automatically try to import it like a model prefab.

**We strongly recommend treating this .MAP file as the "single source of truth."** When you want to edit the model, do your edits in a .MAP level editor like TrenchBroom. Do NOT attempt to edit the game object prefab instance itself, because your changes will be erased when you re-import the .MAP again.

### Level geometry

When possible, Scopa follows Quake conventions with sensible defaults:

- Converts Quake's Z-up axis to Unity's Y-up axis.
- Brushes imported at default scaling factor of 0.03125 (1 Unity meter = 32 Quake units).
- Every brush entity is imported as a separate mesh. World brushes ("worldspawn") are merged into one mesh; each brush entity is another mesh.
    - Brush entities with "illusionary" in the class name (e.g. "func_illusionary") won't have colliders.
    - Brush entities with "trigger" in the class name (e.g. "trigger_multiple") will have their colliders marked as non-solid triggers.
    - Any face that has a texture name containing "sky", "skip", "trigger", "clip", "hint", or "nodraw" will be discarded from the mesh.

To optimize world collisions as much as possible, Scopa generates two Unity collider types:

- **Box Colliders** will be used for every axis-aligned orthogonal brush. If it's a boxy shape that fits the grid with 90 degree angles, then it gets a Box Collider.
    - These are the most efficient and most stable type of collider.
    - (TODO) If your project doesn't need precise collision, you can set a "tolerance" to ignore complex shapes and assign a Box Collider anyway.
- **Mesh Colliders (convex)** will be used for everything else. 
    - Convex Mesh Colliders are much more efficient than big complicated concave shapes.
    - If you rotate a box brush, Scopa has no way of knowing it's actually a box, and will generate a Mesh Collider for it instead.
    - Scopa can't tell if two brushes are the same shape. It will generate a separate collision mesh for each.

### Texturing

Scopa can import Quake 1 WADs (WAD2 format) as Texture2Ds and generate basic Materials. Just put a .WAD file somewhere in the /Assets/ folder, and it will get detected and imported like any other asset file.

- TODO: read Worldspawn wads path, attempt to auto-connect WAD textures already in the project
- TODO: specify a "master material" template for generating opaque materials, alpha cutout materials
- TODO: configure which Unity material pairs with each texture name in the .MAP
- to author WADs, use tools like TexMex or Wally
- TODO: Half-Life 1 GoldSrc WAD3 support

### Entity data / game logic

To let people define game objects and add behaviours in the .MAP, you will need to write a custom .FGD file to load into the level editor.

- (TODO) Basic .FGD example and template
- (TODO) FGD generator?
- (TODO) Tutorial: How to write a .FGD

### Runtime usage (TODO)

## Limitations

- **This package doesn't have game code or entity logic.** It imports MAP files as 3D models, sets up colliders, and provides hooks for importing entity data. That's it.
- **This package does not import Quake textures / WADs.** Instead, you must configure which Unity material pairs with each texture name.

## Acknowledgments / Credits

- based on [Sledge Formats](https://github.com/LogicAndTrick/sledge-formats)
- looking for a BSP plugin? try [Unity3D BSP Importer](https://github.com/wfowler1/Unity3D-BSP-Importer)
