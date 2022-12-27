# FGD Exporter / Creator

<img align="right" style="margin-left: 32px; max-width: 32%; height: auto;" src="FgdExportInspector.png" />

Scopa can export .FGD entity definition files with .OBJ model previews.

This is good for generating a simple level editor SDK usable in TrenchBroom.

## How to create a FGD / SDK

1. In Unity's Project tab, use `Create > Scopa > Fgd Config Asset`
2. Select the new ScopaFgdConfig.
3. In Unity's Inspector tab, configure the FGD entities.
4. Click the Export FGD button.

## FGD settings

**Export Models**: (default: true) if enabled, will attempt to generate a .OBJ preview model for each entity prefab (based on Mesh Renderers and Skinned Mesh Renderers) and export it alongside the FGD with low res textures.



