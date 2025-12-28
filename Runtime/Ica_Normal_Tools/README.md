# How to install
You can install this package by pasting git url to package manager https://github.com/burak-efe/Ica_Normal_Tools.git

# Warning!!
This package still in Beta

# Ica Normal Tools
A Super Fast Normal and Tangent recalculation library for Unity. 
With power of Burst Compiler, Job System and Advanced Mesh API.

## When do you need?

 - You want to create custom character creation system  based on blend shapes.
 - You want to reduce disk size of skinned meshes that have a lot of blend shapes.
 - You want to recalculate normals and tangents of your procedural meshes with smoothing angle and no UV seams or artifacts.
 -  You want to split meshes without visible artifact between boundaries.

## What problem does it solve?
When vertex positions change in any mesh, meshs normal data also should be recalculated to correct lightning. For this reason unity gives the Mesh.RecalculateNormals() or bake the normal and tangent deltas in skinned meshes for every BlendShape. <br />

But there is a problem, firstly method not counting vertices that same position on space. Which causes seams on UV island bounds and submesh bounds (when using multiple material).<br />
Also built in method not takes angle as an argument,so smooth all vertices no matter of how sharp is angle. Another downside this method not suitable for fix blendshape normals directly.<br />

On skinned mesh renderers that stored delta values can only be correct when blend shapes not change same vertices. Which is very rare on morphs that used for character creation and face animations.
![](https://imgur.com/jQ9bSZn.gif)
![](https://imgur.com/4T421VY.gif)
![compare](https://github.com/burak-efe/Ica-Normal-Recalculation/assets/82805019/9fee8357-13d9-40f2-8e76-44c5d894b08a)


## Ica Normal Tools Provides 2 Normal Recalculation method
1: Cached: This method suitable for recalculating same meshs normals and tangents over and over. But mesh structure should not be changed. Its suitable for skinned mesh renderers with blendshapes.
	 
2: Uncached: Suitable for one time only normal recalculation, slower than cached method. It is a good choice for procedural meshes.<br />

## And 2 way to use calculated data
1: Write to Mesh : This Write new normals directly to mesh asset like unity built in method.<br />
2: Write to Material : This method needs a very basic custom shader whic included in the package. <br />
   This method compatible with meshes that require different normals but shared same mesh, like skinned mesh renderers that use blendshapes and sharing same model. For example common humanoid model on your game<br />
   

## How To Use
1: Enable read/write permission on asset import setting <br />

### If you want to use Uncached method on Mesh
 Now you can use uncached method by MyMesh.RecalculateNormalsIca(120f); this will write directly to the mesh, <br />
  if you do not pass value then all normals will be smooth.<br />

### If you want to use Cached Method on SMR
Add IcaNormalMorphedMeshSolverComponent to to your  Object. Assign SMRs and prefabs pairs <br />
Make sure you assign model prefab that in zero pose (e.g. T-pose) to component. <br />
Invoke RecalculateNormals Method after blendshape changes. <br />

### If you want to use on your procedural mesh
If you have already have mesh data you can use underlying methods that takes native collections as input. This will result faster recalculation. <br />


### if you want use write to material output
Make sure you are using Normal receiver shader. Or create your own shader based on, which is very easy.



## Tips:
For BlendShaped Character Models > use Cached method and write to custom shader<br />
For Procedural Created Meshes > use Uncached method and write to mesh<br />

## About custom shader:<br />
NormalReceiver Shader just basic shader graph that sends custom normal and tangent data to material output. And can be used in all render pipelines.<br />

## Requirements
1: Burst Package <br />
2: Collections Package <br />
3: Mathematics Package <br />
4: Shader Graph Package (for custom material) <br />

## Caveats
1: Meshes should be imported as Read/Write enabled. <br />
2: Currently you need to manually call method for every blendshape change.

## Why not Compute Shaders
To calculate normals of the vertices we need to get mesh vertices after blend shape skinning but before Bone skinning. There is no way to get that from skinned mesh renderer component as I aware of. This can be achievable by writing custom skinned mesh renderer but this option way outside of the scope of this project.

## TODO
Create common mesh data asset to reduce ram usage <br />


