# Built-in default entities

We provide a basic `ScopaBuiltinFGD` that defines some basic useful entity types that follow typical Quake level scripting traditions.


## Mesh group entities

Instead, brush entities are useful because each entity creates another game object / mesh group. This gives you control over how your level geometry gets imported and chunked:

- `worldspawn`, `func_detail`: static mesh with colliders
- `func_detail_illusionary`: static mesh with NO colliders (also NOT Navigation Static)
- `func_wall`: non-static mesh with colliders (can be moved, rotated, or resized during the game)
- `func_illusionary`: non-static mesh with NO colliders (can be moved, rotated, or resized during the game)
- `func_physbox`: non-static mesh with colliders AND a rigidbody with physics simulation

Theoretically, the most optimized approach is to make your entire map with `func_detail_illusionary`. With default import settings this would merge all brushes into worldspawn, mark it for static batching, and omit colliders.

Each entity brush also uses a `ScopaBuiltinBrush` entity base, which adds the following properties:

- `_convex`: if enabled, merges all colliders into one convex mesh collider, useful for turning a func_detail staircase into a ramp... does nothing for illusionary entities, which have no colliders
- `_shadow`: lets you control the Cast Shadows setting on the mesh renderers, can be set to Off, On, Both Sides, and Shadows Only.
- `_phong`: smooth face normals for entire mesh to avoid hard seams
- `_layer`: override the Unity layer for this entity; the name must match the layer name exactly!
- `_tag`: set a Unity tag for this entity; the tag name must match an existing defined tag name exactly!


#### Triggers, logic, buttons and doors

We implement a basic trigger / scripting system.

- `trigger`: when a collider enters this brush group, it activates its `target`; it can do this once or multiple times
- `logic_relay`: point entity, triggers something else when triggered
- `logic_counter`: point entity, counts how many times it gets triggered, and then fires once it reaches a threshold
- `logic_timer`: point entity, triggers repeatedly after a certain amount of seconds, until triggered again
- `func_mover`: all purpose sliding object for buttons, doors, platforms, anything with 2 states

The core logic / triggering capabilities depend on `ScopaEntity`'s use of `BindFgd`, which adds the following properties:

- `targetname`: the name of this object, only for trigger / logic purposes... it does not affect the Unity game object name at all
- `delay`: time in seconds to wait before activating
- `target`: the targetname of the thing to trigger... **if multiple entities share the same targetname, they will all be triggered**
- `locktarget`: locked entities cannot activate nor reset
- `unlocktarget`: unlocked entities can activate nor reset
- `killtarget`: the targetname of the thing to `Destroy()`, or delete from the game state
- `trigger_tag`: if set, only objects with this tag can physically trigger the object
- `trigger_layer`: if set, only objects on this layer can physically trigger the object
- `locked`: default state if the object starts locked

There are no plans for a Half-Life 2 / Source Engine-like entity I/O system, since it is not implemented in TrenchBroom.


#### Lights

We follow the Half-Life pattern here and provide three different light entities corresponding to basic light types in Unity:

- `light`: Point light, local
- `light_spot`: Spotlight, local
- `light_environment`: Directional light, global

We also borrow some inspiration from Half-Life 2 / Source Engine:

- `env_cubemap`: Reflection probe
    - TODO: overrides `OnScopaLateImport()` to fire raycasts in all directions and configure its own box bounds

Unity does not have runtime lightmapping GI baking. If you support modding for your game, then user-generated maps can only use realtime lights. 

We don't support ambient lighting, since overriding the per-scene Light Settings asset is complicated. We leave this as an exercise for the reader.