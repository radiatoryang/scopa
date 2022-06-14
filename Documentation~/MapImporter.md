# Importing MAPs

## Level geometry

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


## Collision

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


## Texturing

Quake 1 texture are stored in files called WADs (WAD2 format), which match a specific 256 color palette used across the entire game. To create and edit these WADs, use tools like TexMex or Wally. 

We import WADs as a bundle of Texture2Ds, and we can also generate basic opaque / alpha cutout Materials based on templates that you define.

(inspector image)

If "Find Materials" is enabled on the .MAP, we try to match each face's texture name with a similarly named Material anywhere in the project. You don't have to use WADs; this auto-detect function will work with all materials. (Note: Find Materials only works at editor-time, not runtime.)

You can also manually set a specific `MaterialOverride` for each texture name. This also lets you bind additional properties to the material:
- **`**Hotspot UVs**`**: automatically unwraps face to match a rectangle defined in a Hotspot Texture Atlas (`Project > Create > Scopa > Hotspot Texture`). The UVs can be randomly rotated and flipped for additional variation.


## Entities and Scripting

For more on entity handling, see [Entity Import workflow](EntityImport.md).