# Entity import workflow

An overview of Scopa's entity workflow:

1. Generate FGD: Scopa helps you build entity definition files (.FGDs) with built-in documentation for mappers
2. Add entities to MAP: mappers use the FGD in their level editor, add NPCs, buttons, doors, etc. to the level
3. Import MAP: Scopa imports the map and converts everything to Unity game objects and meshes, with various ways to bind entity info to MonoBehaviours

That last step is the trickiest part, and the focus of this article.

# Configuring Unity objects based on entity data

The general workflow looks like this:

1. make a Unity prefab for the entity type
2. in the MAP or FGD inspector, assign the prefab as the entity type's Entity Prefab
3. in your Unity script, use any of three data binding methods to configure the prefab:
    - poll `ScopaEntity` component at runtime (simple for prototyping, good for beginners)
    - use `[BindFgd]` attribute (convenient for experienced coders)
    - implement `OnScopaImport()` function (maximum control for experienced coders)

Let's explore these three binding methods in detail.

## BINDING METHOD 1: poll ScopaEntity at runtime

Upon MAP import, every entity gets a component called `ScopaEntity` which you can poll for entity data.

For example, imagine you had a Unity script `Zombie.cs` and you want it read the `health` property from `monster_zombie` entity data.

1. make a prefab `Zombie.prefab` with a `Zombie.cs` script on it
2. in the MAP importer inspector, assign `Zombie.prefab` as the `monster_zombie`'s Entity Prefab <br /> *(upon import, Scopa will instantiate a `Zombie.prefab` for every `monster_zombie`, and add a `ScopaEntity` component if there isn't one already)*
3. in `Zombie.cs`, poll the `ScopaEntity` component for entity data:

```csharp
using UnityEngine;
using Scopa;

public class Zombie : MonoBehaviour {

    public int health = 10;

    void Start() {
        // data is automatically stored in a ScopaEntity component that gets added upon MAP import
        var entityData = GetComponent<ScopaEntity>();

        // attempt to fetch entity property with key called "health" (case-sensitive)
        // this fetch might fail, because maybe the mapper didn't define any "health", etc.
        if ( entityData.TryGetInt("health", out var newHealth) ) {
            // if the fetch is successful, then use the newHealth value
            health = newHealth;
            // ... or do whatever you want (e.g. adjust health based on current difficulty mode?)
        }
    }

}
```

**Strengths:** easy to understand, quick to prototype.

**Weaknesses:** doesn't scale well, difficult to maintain. Synchronizing the FGD definition with the Unity script depends entirely on the user. Lots of repetitive boilerplate style code to write.


## BINDING METHOD 2: use BindFgd attribute (recommended)

We recommend using the `[BindFgd]` attribute in most cases. It is the most convenient method, but requires a little bit more Unity and C# knowledge.

Let's return to the zombie example. The first few steps are similar:

1. make a prefab `Zombie.prefab` with a `Zombie.cs` script on it
2. in the FGD generator inspector (not the MAP inspector!), assign `Zombie.prefab` as the `monster_zombie`'s Entity Prefab
3. in `Zombie.cs`, implement the `IScopaEntityImport` interface and then tag relevant properties with the `BindFgd` attribute:

```csharp
using UnityEngine;
using Scopa;

// important: to use BindFgd, you must add the IScopaEntityImport interface
public class Zombie : MonoBehaviour, IScopaEntityImport {

    // BindFgd: Scopa will push this property definition to the FGD, but also sync with values from the MAP too.
    // Tooltip: Scopa will automatically push the info into the FGD's level editor help text.
    [BindFgd("health", BindFgd.VarType.Int, "Initial Health")]  
    [Tooltip("The zombie's initial health. Don't set it over 99!")]  
    public int health = 10;

}
```

**Strengths:** very convenient. Less boilerplate code, FGD and user docs easier to maintain.

**Weaknesses:** requires C# reflection, which you may not like. Also no additional config code is possible, this only handles the simplest use case.


## BINDING METHOD 3: implement OnEntityImport function

When you need maximum control, you can manually configure the object at import time. This is similar to binding method 1 (polling ScopaEntity) but with two differences: (a) no ScopaEntity component is necessary, and (b) this happens at import time, not at runtime.

1. make a prefab `Zombie.prefab` with a `Zombie.cs` script on it
2. in the MAP or FGD inspector, assign `Zombie.prefab` as the `monster_zombie`'s Entity Prefab
3. in `Zombie.cs`, implement the `IScopaEntityImport` interface and then declare a `OnEntityImport()` function:

```csharp
using UnityEngine;
using Scopa;
using Scopa.Formats.Map.Objects;

public class Zombie : MonoBehaviour, IScopaEntityImport {

    public int health = 10;

    // OnEntityImport() will be automatically called at import time
    public void OnEntityImport( Entity entityData ) { 
        if ( entityData.TryGetInt("health", out var newHealth) ) {
            health = newHealth;
        }
    }

}
```

**Strengths:** more control and possibility. No ScopaEntity component required.

**Weaknesses:** similar tech debt problems as polling method. Some boilerplate code required, no automatic sync with FGD definitions.


