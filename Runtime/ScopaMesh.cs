using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Scopa.Formats.Map.Formats;
using Scopa.Formats.Map.Objects;
using Scopa.Formats.Texture.Wad;
using Scopa.Formats.Id;
using UnityEngine;
using Mesh = UnityEngine.Mesh;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Scopa {
    /// <summary>main class for Scopa mesh generation / geo functions</summary>
    public static class ScopaMesh {
        // to avoid GC, we use big static lists so we just allocate once
        static List<Face> allFaces = new List<Face>(1024);
        static List<Vector3> verts = new List<Vector3>(4096);
        static List<int> tris = new List<int>(8192);
        static List<Vector2> uvs = new List<Vector2>(4096);

        public static void AddFaceForOcclusion(Face brushFace) {
            allFaces.Add(brushFace);
        }

        public static void ClearFaceOcclusionList() {
            allFaces.Clear();
        }

        /// <summary> given a brush / solid (and optional textureFilter texture name) it generates mesh data for verts / tris / UV list buffers
        /// BUT it also occludes unseen faces at the same time too</summary>
        public static void BufferMeshDataFromSolid(Solid solid, ScopaMapConfig config, ScopaMapConfig.MaterialOverride textureFilter = null, bool includeDiscardedFaces = false) {
            foreach (var face in solid.Faces) {
                if ( face.Vertices == null || face.Vertices.Count == 0) // this shouldn't happen though
                    continue;

                if ( !includeDiscardedFaces && face.discardWhenBuildingMesh )
                    continue;

                if ( textureFilter != null && textureFilter.textureName.GetHashCode() != face.TextureName.GetHashCode() )
                    continue;

                // test for unseen / hidden faces, and discard
                if ( !includeDiscardedFaces && config.removeHiddenFaces ) {
                    for(int i=0; i<allFaces.Count; i++) {
                        if (allFaces[i].OccludesFace(face)) {
                            // Debug.Log("discarding unseen face at " + face);
                            // face.DebugDrawVerts(Color.yellow);
                            face.discardWhenBuildingMesh = true;
                            break;
                        }
                    }

                    if ( face.discardWhenBuildingMesh )
                        continue;
                }

                BufferScaledMeshDataForFace(
                    face, 
                    config.scalingFactor, 
                    verts, 
                    tris, 
                    uvs, 
                    config.globalTexelScale,
                    textureFilter?.material?.mainTexture != null ? textureFilter.material.mainTexture.width : config.defaultTexSize, 
                    textureFilter?.material?.mainTexture != null ? textureFilter.material.mainTexture.height : config.defaultTexSize,
                    textureFilter != null ? textureFilter.materialConfig : null
                );
            }
        }

        /// <summary> utility function that actually generates the Mesh object </summary>
        public static Mesh BuildMeshFromBuffers(string meshName, ScopaMapConfig config, Vector3 meshOrigin = default(Vector3), float smoothNormalAngle = 0) {
            var mesh = new Mesh();
            mesh.name = meshName;

            if(verts.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            if ( meshOrigin != default(Vector3) ) {
                for(int i=0; i<verts.Count; i++) {
                    verts[i] -= meshOrigin;
                }
            }

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetUVs(0, uvs);

            mesh.RecalculateBounds();

            mesh.RecalculateNormals(UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds); // built-in Unity method provides a base for SmoothNormalsJobs
            if ( smoothNormalAngle > 0.01f)
                mesh.SmoothNormalsJobs(smoothNormalAngle);

            if ( config.addTangents && smoothNormalAngle >= 0 )
                mesh.RecalculateTangents();
            
            #if UNITY_EDITOR
            if ( config.addLightmapUV2 ) {
                UnwrapParam.SetDefaults( out var unwrap);
                unwrap.packMargin *= 2;
                Unwrapping.GenerateSecondaryUVSet( mesh, unwrap );
            }

            if ( config.meshCompression != ScopaMapConfig.ModelImporterMeshCompression.Off)
                UnityEditor.MeshUtility.SetMeshCompression(mesh, (ModelImporterMeshCompression)config.meshCompression);
            #endif

            mesh.Optimize();

            return mesh;
        }

        /// <summary> build mesh fragment (verts / tris / uvs), usually run for each face of a solid </summary>
        static void BufferScaledMeshDataForFace(Face face, float scalingFactor, List<Vector3> verts, List<int> tris, List<Vector2> uvs, float scalar = 1f, int textureWidth = 128, int textureHeight = 128, ScopaMaterialConfig materialConfig = null) {
            var lastVertIndexOfList = verts.Count;

            // add all verts and UVs
            for( int v=0; v<face.Vertices.Count; v++) {
                verts.Add(face.Vertices[v] * scalingFactor);

                if ( materialConfig == null || !materialConfig.enableHotspotUv) {
                    uvs.Add(new Vector2(
                        (Vector3.Dot(face.Vertices[v], face.UAxis / face.XScale) + (face.XShift % textureWidth)) / (textureWidth),
                        (Vector3.Dot(face.Vertices[v], face.VAxis / -face.YScale) + (-face.YShift % textureHeight)) / (textureHeight)
                    ) * scalar);
                }
            }

            if ( materialConfig != null && materialConfig.enableHotspotUv && materialConfig.rects.Count > 0) {
                // uvs.AddRange( ScopaHotspot.GetHotspotUVs( verts.GetRange(lastVertIndexOfList, face.Vertices.Count), hotspotAtlas ) );
                if ( !ScopaHotspot.TryGetHotspotUVs( face.Vertices, face.Plane.normal, materialConfig, out var hotspotUVs, scalingFactor )) {
                    // TODO: wow uhh I really fucked up with this design... no easy way to suddenly put this in a different material
                    // ... it will need a pre-pass
                }
                uvs.AddRange( hotspotUVs );
            }

            // verts are already in correct order, add as basic fan pattern (since we know it's a convex face)
            for(int i=2; i<face.Vertices.Count; i++) {
                tris.Add(lastVertIndexOfList);
                tris.Add(lastVertIndexOfList + i - 1);
                tris.Add(lastVertIndexOfList + i);
            }
        }

        public static bool IsMeshBufferEmpty() {
            return verts.Count == 0 || tris.Count == 0;
        }

        public static void ClearMeshBuffers()
        {
            verts.Clear();
            tris.Clear();
            uvs.Clear();
        }

    }
}