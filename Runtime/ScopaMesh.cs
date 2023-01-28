using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Sledge.Formats.Map.Formats;
using Sledge.Formats.Map.Objects;
using Sledge.Formats.Precision;
using Scopa.Formats.Texture.Wad;
using Scopa.Formats.Id;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Mesh = UnityEngine.Mesh;
using Vector3 = UnityEngine.Vector3;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Scopa {
    /// <summary>main class for Scopa mesh generation / geo functions</summary>
    public static class ScopaMesh {
        // to avoid GC, we use big static lists so we just allocate once
        // TODO: will have to reorganize this for multithreading later on
        static List<Face> allFaces = new List<Face>(8192);
        static HashSet<Face> discardedFaces = new HashSet<Face>(8192);

        static List<Vector3> verts = new List<Vector3>(4096);
        static List<Vector3> faceVerts = new List<Vector3>(64);
        static List<int> tris = new List<int>(8192);
        static List<int> faceTris = new List<int>(32);
        static List<Vector2> uvs = new List<Vector2>(4096);
        static List<Vector2> faceUVs = new List<Vector2>(64);

        const float EPSILON = 0.01f;


        public static void AddFaceForCulling(Face brushFace) {
            allFaces.Add(brushFace);
        }

        public static void ClearFaceCullingList() {
            allFaces.Clear();
            discardedFaces.Clear();
        }

        public static void DiscardFace(Face brushFace) {
            discardedFaces.Add(brushFace);
        }

        public static bool IsFaceCulledDiscard(Face brushFace) {
            return discardedFaces.Contains(brushFace);
        }
        
        public static void FaceCullingJobs() {
            var vertCount = 0;
            var cullingOffsets = new NativeArray<int>(allFaces.Count, Allocator.TempJob);
            for(int i=0; i<allFaces.Count; i++) {
                cullingOffsets[i] = vertCount;
                vertCount += allFaces[i].Vertices.Count;
            }

            var cullingVerts = new NativeArray<Vector3>(vertCount, Allocator.TempJob);
            for(int i=0; i<allFaces.Count; i++) {
                for(int v=cullingOffsets[i]; v < (i<cullingOffsets.Length-1 ? cullingOffsets[i+1] : vertCount); v++) {
                    cullingVerts[v] = allFaces[i].Vertices[v-cullingOffsets[i]].ToUnity();
                }
            }
            
            var cullingResults = new NativeArray<bool>(allFaces.Count, Allocator.TempJob);
            for(int i=0; i<allFaces.Count; i++) {
                cullingResults[i] = IsFaceCulledDiscard(allFaces[i]);
            }
            
            var jobData = new FaceCullingJob();
            jobData.vertices = cullingVerts;
            jobData.cullingOffsets = cullingOffsets;
            jobData.results = cullingResults;
            var handle = jobData.Schedule(cullingResults.Length, 16);
            handle.Complete();

            // int culledFaces = 0;
            for(int i=0; i<cullingResults.Length; i++) {
                // if (!allFaces[i].discardWhenBuildingMesh && cullingResults[i])
                //     culledFaces++;
                if(cullingResults[i])
                    discardedFaces.Add(allFaces[i]);
            }
            // Debug.Log($"Culled {culledFaces} faces!");

            cullingOffsets.Dispose();
            cullingVerts.Dispose();
            cullingResults.Dispose();
        }

        public struct FaceCullingJob : IJobParallelFor
        {
            [ReadOnlyAttribute]
            public NativeArray<Vector3> vertices;

            [ReadOnlyAttribute]
            public NativeArray<int> cullingOffsets;
            
            public NativeArray<bool> results;

            public void Execute(int i)
            {
                if (results[i])
                    return;

                var offsetStart = cullingOffsets[i];
                var offsetEnd = i<cullingOffsets.Length-1 ? cullingOffsets[i+1] : vertices.Length;

                var Plane = new UnityEngine.Plane( vertices[offsetStart], vertices[offsetStart+1], vertices[offsetStart+2] );

                var Center = vertices[offsetStart];
                for( int n=offsetStart; n<offsetEnd; n++) {
                    Center += vertices[n];
                }
                Center /= vertices.Length;

                var ignoreAxis = GetMainAxisToNormal(Plane); // 2D math is easier, so let's ignore the least important axis

                // test against all other faces
                for(int n=0; n<cullingOffsets.Length; n++) {
                    var otherOffsetStart = cullingOffsets[n];
                    var otherOffsetEnd = n<cullingOffsets.Length-1 ? cullingOffsets[n+1] : vertices.Length;
                    var otherPlane = new UnityEngine.Plane( vertices[otherOffsetStart], vertices[otherOffsetStart+1], vertices[otherOffsetStart+2] );

                    // first, test (1) share similar plane distance and (2) face opposite directions
                    // we are testing the NEGATIVE case for early out
                    if ( Mathf.Abs( Plane.distance + otherPlane.distance) > 1f || Vector3.Dot(Plane.normal, otherPlane.normal) > -0.99f ) {
                        continue;
                    }

                    // then, test whether this face's vertices are completely inside the other
                    var otherVertices = new Vector3[otherOffsetEnd-otherOffsetStart];
                    NativeArray<Vector3>.Copy(vertices, otherOffsetStart, otherVertices, 0, otherVertices.Length);

                    var vertNotInOtherFace = false;
                    for( int x=offsetStart; x<offsetEnd; x++ ) {
                        var smallFaceVert = vertices[x] + (Center - vertices[x]).normalized * 0.2f;
                        switch (ignoreAxis) {
                            case Axis.X: if (!IsInPolygonYZ3(smallFaceVert, otherVertices)) vertNotInOtherFace = true; break;
                            case Axis.Y: if (!IsInPolygonXZ3(smallFaceVert, otherVertices)) vertNotInOtherFace = true; break;
                            case Axis.Z: if (!IsInPolygonXY3(smallFaceVert, otherVertices)) vertNotInOtherFace = true; break;
                        }

                        if (vertNotInOtherFace)
                            break;
                    }

                    if (vertNotInOtherFace)
                        continue;

                    // if we got this far, then this face should be culled
                    var tempResult = true;
                    results[i] = tempResult;
                    return;
                }
               
            }
        }

        public enum Axis { X, Y, Z}

        public static Axis GetMainAxisToNormal(UnityEngine.Plane Plane) {
            // VHE prioritises the axes in order of X, Y, Z.
            // so in Unity land, that's X, Z, and Y
            var norm = Plane.normal.Absolute();

            if (norm.x >= norm.y && norm.x >= norm.z) return Axis.X;
            if (norm.z >= norm.y) return Axis.Z;
            return Axis.Y;
        }

        public static Axis GetMainAxisToNormal(Vector3 norm) {
            // VHE prioritises the axes in order of X, Y, Z.
            // so in Unity land, that's X, Z, and Y
            norm = norm.Absolute();

            if (norm.x >= norm.y && norm.x >= norm.z) return Axis.X;
            if (norm.z >= norm.y) return Axis.Z;
            return Axis.Y;
        }

        public static bool IsInPolygonYZ3(Vector3 testPoint, Vector3[] vertices){
            // Get the angle between the point and the
            // first and last vertices.
            int max_point = vertices.Length - 1;
            float total_angle = GetAngle(
                vertices[max_point].y, vertices[max_point].z,
                testPoint.y, testPoint.z,
                vertices[0].y, vertices[0].z);

            // Add the angles from the point
            // to each other pair of vertices.
            for (int i = 0; i < max_point; i++) {
                total_angle += GetAngle(
                    vertices[i].y, vertices[i].z,
                    testPoint.y, testPoint.z,
                    vertices[i + 1].y, vertices[i + 1].z);
            }

            // The total angle should be 2 * PI or -2 * PI if
            // the point is in the polygon and close to zero
            // if the point is outside the polygon.
            // The following statement was changed. See the comments.
            //return (Math.Abs(total_angle) > 0.000001);
            return Mathf.Abs(total_angle) > EPSILON;
        }

        public static bool IsInPolygonXY3( Vector3 testPoint, Vector3[] vertices){
            // Get the angle between the point and the
            // first and last vertices.
            int max_point = vertices.Length - 1;
            float total_angle = GetAngle(
                vertices[max_point].x, vertices[max_point].y,
                testPoint.x, testPoint.y,
                vertices[0].x, vertices[0].y);

            // Add the angles from the point
            // to each other pair of vertices.
            for (int i = 0; i < max_point; i++) {
                total_angle += GetAngle(
                    vertices[i].x, vertices[i].y,
                    testPoint.x, testPoint.y,
                    vertices[i + 1].x, vertices[i + 1].y);
            }

            // The total angle should be 2 * PI or -2 * PI if
            // the point is in the polygon and close to zero
            // if the point is outside the polygon.
            // The following statement was changed. See the comments.
            //return (Math.Abs(total_angle) > 0.000001);
            return Mathf.Abs(total_angle) > EPSILON;
        }

        public static bool IsInPolygonXZ3( Vector3 testPoint, Vector3[] vertices){
            // Get the angle between the point and the
            // first and last vertices.
            int max_point = vertices.Length - 1;
            float total_angle = GetAngle(
                vertices[max_point].x, vertices[max_point].z,
                testPoint.x, testPoint.z,
                vertices[0].x, vertices[0].z);

            // Add the angles from the point
            // to each other pair of vertices.
            for (int i = 0; i < max_point; i++) {
                total_angle += GetAngle(
                    vertices[i].x, vertices[i].z,
                    testPoint.x, testPoint.z,
                    vertices[i + 1].x, vertices[i + 1].z);
            }

            // The total angle should be 2 * PI or -2 * PI if
            // the point is in the polygon and close to zero
            // if the point is outside the polygon.
            // The following statement was changed. See the comments.
            //return (Math.Abs(total_angle) > 0.000001);
            return Mathf.Abs(total_angle) > EPSILON;
        }

        // Return the angle ABC.
        // Return a value between PI and -PI.
        // Note that the value is the opposite of what you might
        // expect because Y coordinates increase downward.
        static float GetAngle(float Ax, float Ay,
            float Bx, float By, float Cx, float Cy)
        {
            // Get the dot product.
            float dot_product = DotProduct(Ax, Ay, Bx, By, Cx, Cy);

            // Get the cross product.
            float cross_product = CrossProductLength(Ax, Ay, Bx, By, Cx, Cy);

            // Calculate the angle.
            return (float)Mathf.Atan2(cross_product, dot_product);
        }

        static float DotProduct(float Ax, float Ay,
            float Bx, float By, float Cx, float Cy)
        {
            // Get the vectors' coordinates.
            float BAx = Ax - Bx;
            float BAy = Ay - By;
            float BCx = Cx - Bx;
            float BCy = Cy - By;

            // Calculate the dot product.
            return (BAx * BCx + BAy * BCy);
        }

        // Return the cross product AB x BC.
        // The cross product is a vector perpendicular to AB
        // and BC having length |AB| * |BC| * Sin(theta) and
        // with direction given by the right-hand rule.
        // For two vectors in the X-Y plane, the result is a
        // vector with X and Y components 0 so the Z component
        // gives the vector's length and direction.
        static float CrossProductLength(float Ax, float Ay,
            float Bx, float By, float Cx, float Cy)
        {
            // Get the vectors' coordinates.
            float BAx = Ax - Bx;
            float BAy = Ay - By;
            float BCx = Cx - Bx;
            float BCy = Cy - By;

            // Calculate the Z coordinate of the cross product.
            return (BAx * BCy - BAy * BCx);
        }


        /// <summary> given a brush / solid (and optional textureFilter texture name) it generates mesh data for verts / tris / UV list buffers
        /// BUT it also occludes unseen faces at the same time too</summary>
        public static void BufferMeshDataFromSolid(Solid solid, ScopaMapConfig mapConfig, ScopaMapConfig.MaterialOverride textureFilter = null, bool includeDiscardedFaces = false) {
            foreach (var face in solid.Faces) {
                if ( face.Vertices == null || face.Vertices.Count == 0) // this shouldn't happen though
                    continue;

                if ( !includeDiscardedFaces && IsFaceCulledDiscard(face) )
                    continue;

                if ( textureFilter != null && textureFilter.textureName.GetHashCode() != face.TextureName.GetHashCode() )
                    continue;

                // test for unseen / hidden faces, and discard
                // if ( !includeDiscardedFaces && mapConfig.removeHiddenFaces ) {
                //     for(int i=0; i<allFaces.Count; i++) {
                //         if (allFaces[i].OccludesFace(face)) {
                //             Debug.Log("discarding unseen face at " + face);
                //             // face.DebugDrawVerts(Color.yellow);
                //             face.discardWhenBuildingMesh = true;
                //             break;
                //         }
                //     }

                //     if ( face.discardWhenBuildingMesh )
                //         continue;
                // }

                BufferScaledMeshFragmentForFace(
                    solid,
                    face, 
                    mapConfig, 
                    verts, 
                    tris, 
                    uvs, 
                    textureFilter?.material?.mainTexture != null ? textureFilter.material.mainTexture.width : mapConfig.defaultTexSize, 
                    textureFilter?.material?.mainTexture != null ? textureFilter.material.mainTexture.height : mapConfig.defaultTexSize,
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
            if ( smoothNormalAngle > 0.01f) {
                mesh.SmoothNormalsJobs(smoothNormalAngle);
            }

            if ( config.addTangents )
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
        static void BufferScaledMeshFragmentForFace(Solid brush, Face face, ScopaMapConfig mapConfig, List<Vector3> verts, List<int> tris, List<Vector2> uvs, int textureWidth = 128, int textureHeight = 128, ScopaMaterialConfig materialConfig = null) {
            var lastVertIndexOfList = verts.Count;

            faceVerts.Clear();
            faceUVs.Clear();
            faceTris.Clear();

            // add all verts and UVs
            for( int v=0; v<face.Vertices.Count; v++) {
                faceVerts.Add(face.Vertices[v].ToUnity() * mapConfig.scalingFactor);
                
                faceUVs.Add(new Vector2(
                    (Vector3.Dot(face.Vertices[v].ToUnity(), face.UAxis.ToUnity() / face.XScale) + (face.XShift % textureWidth)) / (textureWidth),
                    (Vector3.Dot(face.Vertices[v].ToUnity(), face.VAxis.ToUnity() / -face.YScale) + (-face.YShift % textureHeight)) / (textureHeight)
                ) * mapConfig.globalTexelScale);
            }

            // verts are already in correct order, add as basic fan pattern (since we know it's a convex face)
            for(int i=2; i<face.Vertices.Count; i++) {
                faceTris.Add(lastVertIndexOfList);
                faceTris.Add(lastVertIndexOfList + i - 1);
                faceTris.Add(lastVertIndexOfList + i);
            }

            // user override
            if (materialConfig != null) {
                materialConfig.OnBuildBrushFace(brush, face, mapConfig, faceVerts, faceUVs, faceTris);
            }

            // add back to global mesh buffer
            verts.AddRange(faceVerts);
            uvs.AddRange(faceUVs);
            tris.AddRange(faceTris);
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

        public static void WeldVertices(this Mesh aMesh, float aMaxDelta = 0.1f, float maxAngle = 180f)
        {
            var verts = aMesh.vertices;
            var normals = aMesh.normals;
            var uvs = aMesh.uv;
            List<int> newVerts = new List<int>();
            int[] map = new int[verts.Length];
            // create mapping and filter duplicates.
            for (int i = 0; i < verts.Length; i++)
            {
                var p = verts[i];
                var n = normals[i];
                var uv = uvs[i];
                bool duplicate = false;
                for (int i2 = 0; i2 < newVerts.Count; i2++)
                {
                    int a = newVerts[i2];
                    if (
                        (verts[a] - p).sqrMagnitude <= aMaxDelta // compare position
                        && Vector3.Angle(normals[a], n) <= maxAngle // compare normal
                        // && (uvs[a] - uv).sqrMagnitude <= aMaxDelta // compare first uv coordinate
                        )
                    {
                        map[i] = i2;
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate)
                {
                    map[i] = newVerts.Count;
                    newVerts.Add(i);
                }
            }
            // create new vertices
            var verts2 = new Vector3[newVerts.Count];
            var normals2 = new Vector3[newVerts.Count];
            var uvs2 = new Vector2[newVerts.Count];
            for (int i = 0; i < newVerts.Count; i++)
            {
                int a = newVerts[i];
                verts2[i] = verts[a];
                normals2[i] = normals[a];
                uvs2[i] = uvs[a];
            }
            // map the triangle to the new vertices
            var tris = aMesh.triangles;
            for (int i = 0; i < tris.Length; i++)
            {
                tris[i] = map[tris[i]];
            }
            aMesh.Clear();
            aMesh.vertices = verts2;
            aMesh.normals = normals2;
            aMesh.triangles = tris;
            aMesh.uv = uvs2;
        }

        public static void SmoothNormalsJobs(this Mesh aMesh, float weldingAngle = 80, float maxDelta = 0.1f) {
            var meshData = Mesh.AcquireReadOnlyMeshData(aMesh);
            var verts = new NativeArray<Vector3>(meshData[0].vertexCount, Allocator.TempJob);
            meshData[0].GetVertices(verts);
            var normals = new NativeArray<Vector3>(meshData[0].vertexCount, Allocator.TempJob);
            meshData[0].GetNormals(normals);
            var smoothNormalsResults = new NativeArray<Vector3>(meshData[0].vertexCount, Allocator.TempJob);
            
            var jobData = new SmoothJob();
            jobData.cos = Mathf.Cos(weldingAngle * Mathf.Deg2Rad);
            jobData.maxDelta = maxDelta;
            jobData.verts = verts;
            jobData.normals = normals;
            jobData.results = smoothNormalsResults;
            var handle = jobData.Schedule(smoothNormalsResults.Length, 8);
            handle.Complete();

            meshData.Dispose(); // must dispose this early, before modifying mesh

            aMesh.SetNormals(smoothNormalsResults);

            verts.Dispose();
            normals.Dispose();
            smoothNormalsResults.Dispose();
        }

        public struct SmoothJob : IJobParallelFor
        {
            [ReadOnlyAttribute]
            public NativeArray<Vector3> verts, normals;
            
            public NativeArray<Vector3> results;

            public float cos, maxDelta;

            public void Execute(int i)
            {
                var tempResult = normals[i];
                var resultCount = 1;
                
                for(int i2 = 0; i2 < verts.Length; i2++) {
                    if ( (verts[i2] - verts[i] ).sqrMagnitude <= maxDelta
                        && Vector3.Dot(normals[i2], normals[i] ) >= cos ) {
                        tempResult += normals[i2];
                        resultCount++;
                    }
                }

                if (resultCount > 1)
                    tempResult = (tempResult / resultCount).normalized;
                results[i] = tempResult;
            }
        }

        public static void SnapBrushVertices(Solid sledgeSolid, float snappingDistance = 4f) {
            // snap nearby vertices together within in each solid -- but always snap to the FURTHEST vertex from the center
            var origin = new System.Numerics.Vector3();
            var vertexCount = 0;
            foreach(var face in sledgeSolid.Faces) {
                for(int i=0; i<face.Vertices.Count; i++) {
                    origin += face.Vertices[i];
                }
                vertexCount += face.Vertices.Count;
            }
            origin /= vertexCount;

            foreach(var face1 in sledgeSolid.Faces) {
                foreach (var face2 in sledgeSolid.Faces) {
                    if ( face1 == face2 )
                        continue;

                    for(int a=0; a<face1.Vertices.Count; a++) {
                        for(int b=0; b<face2.Vertices.Count; b++ ) {
                            if ( (face1.Vertices[a] - face2.Vertices[b]).LengthSquared() < snappingDistance * snappingDistance ) {
                                if ( (face1.Vertices[a] - origin).LengthSquared() > (face2.Vertices[b] - origin).LengthSquared() )
                                    face2.Vertices[b] = face1.Vertices[a];
                                else
                                    face1.Vertices[a] = face2.Vertices[b];
                            }
                        }
                    }
                }
            }
        }

    }
}