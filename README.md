# Scopa

*Scopa* ("broom" in Latin) is a Unity level design plugin that adds import and workflow tools for making .MAP levels with Quake 1 / Half-Life 1 BSP-style construction. Relive the golden age of level design... today!

# WARNING: this is in early development, it's broken and not ready for public use yet.

- generates a prefab with 3D meshes, colliders (box colliders and mesh colliders), parses entity data for your components
- imports .MAPs (Quake 1 / Valve 220 format only) and .WADs (Quake 1 WAD2 / Half-Life GoldSrc WAD3 format only)
- procedural environment art tools built into brush import pipeline
    - configurable mesh normal smoothing
    - hotspot UV texturing
    - (PLANNED) mesh subdivisions
    - (PLANNED) automatic rule-based detail placement (for grass, rocks, or whatever)
- .FGD generator tool to export entity definitions for the level editor
    - (PLANNED) default set of basic trigger, logic, light entities
    - (PLANNED) bind any MonoBehaviour variable to an FGD entity property simply with an `[FGD]` attribute
- works at editor time or runtime (so you can add modding support)

To build .MAP levels, we recommend [TrenchBroom](https://github.com/TrenchBroom/TrenchBroom).

## Installation

This is a [Unity Package](https://docs.unity3d.com/Manual/PackagesList.html) for Unity 2020.1 or later. It is self-contained with zero dependencies.

To install, just open [Package Manager](https://docs.unity3d.com/Manual/upm-ui.html) and add `https://github.com/radiatoryang/scopa.git` [(more info and help)](https://docs.unity3d.com/2021.2/Documentation/Manual/upm-ui-giturl.html)

## Usage

Put a .MAP or .WAD file in your `/Assets/` folder and it'll import automatically, generating assets / prefabs for you to use. Play with the file import settings.

**Do your edits in the level editor, not in Unity!** If you edit the prefab instance, your changes may be erased when you re-import the .MAP again. We strongly recommend treating this .MAP file as the "single source of truth." 

To learn about more features, such as runtime support, entity handling, and the FGD generator, read the [Documentation](https://github.com/radiatoryang/scopa/blob/main/Documentation~/Scopa.md).

## Limitations

- **This package doesn't have game code.** It imports MAPs, sets up colliders, and imports entity data. You still have to make the game yourself.

## Contributions

Issues and pull requests are currently NOT accepted at this time. Development is still very early.

## Acknowledgments / Credits

- based on [Sledge Formats](https://github.com/LogicAndTrick/sledge-formats)
- hotspot UV implementation based on [Unity Hotspot UV](https://github.com/BennyKok/unity-hotspot-uv)
- looking for a BSP plugin? try [Unity3D BSP Importer](https://github.com/wfowler1/Unity3D-BSP-Importer)
