using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public static class MeshExtensions {

        public static void WeldVertices(Mesh aMesh, float aMaxDelta = 0.001f) {
            var verts = aMesh.vertices;
            var normals = aMesh.normals;
            var uvs = aMesh.uv;
            List<int> newVerts = new List<int>();
            int[] map = new int[verts.Length];
            // create mapping and filter duplicates.
            for(int i = 0; i < verts.Length; i++)
            {
                var p = verts[i];
                var n = normals[i];
                var uv = uvs[i];
                bool duplicate = false;
                for(int i2 = 0; i2 < newVerts.Count; i2++)
                {
                    int a = newVerts[i2];
                    if (
                        (verts[a] - p).sqrMagnitude <= aMaxDelta && // compare position
                        Vector3.Angle(normals[a], n) <= aMaxDelta && // compare normal
                        (uvs[a] - uv).sqrMagnitude <= aMaxDelta // compare first uv coordinate
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
            for(int i = 0; i < newVerts.Count; i++)
            {
                int a = newVerts[i];
                verts2[i] = verts[a];
                normals2[i] = normals[a];
                uvs2[i] = uvs[a];
            }
            // map the triangle to the new vertices
            var tris = aMesh.triangles;
            for(int i = 0; i < tris.Length; i++)
            {
                tris[i] = map[tris[i]];
            }

            aMesh.Clear();
            aMesh.vertices = verts2;
            aMesh.normals = normals2;
            aMesh.uv = uvs2;
            aMesh.triangles = tris;
        }

        public static Mesh LaplacianFilter (Mesh mesh, int times = 1) {
			mesh.vertices = LaplacianFilter(mesh.vertices, mesh.triangles, times);
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			return mesh;
		}

		public static Vector3[] LaplacianFilter(Vector3[] vertices, int[] triangles, int times) {
			var network = VertexConnection.BuildNetwork(triangles);
			for(int i = 0; i < times; i++) {
				vertices = LaplacianFilter(network, vertices, triangles);
			}
			return vertices;
		}

		static Vector3[] LaplacianFilter(Dictionary<int, VertexConnection> network, Vector3[] origin, int[] triangles) {
			Vector3[] vertices = new Vector3[origin.Length];
			for(int i = 0, n = origin.Length; i < n; i++) {
				var connection = network[i].Connection;
				var v = Vector3.zero;
				foreach(int adj in connection) {
					v += origin[adj];
				}
				vertices[i] = v / connection.Count;
			}
			return vertices;
		}

		/*
		 * HC (Humphrey’s Classes) Smooth Algorithm - Reduces Shrinkage of Laplacian Smoother
		 * alpha 0.0 ~ 1.0
		 * beta  0.0 ~ 1.0
		*/
		public static Mesh HCFilter (Mesh mesh, int times = 5, float alpha = 0.5f, float beta = 0.75f) {
			mesh.vertices = HCFilter(mesh.vertices, mesh.triangles, times, alpha, beta);
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			return mesh;
		}

		static Vector3[] HCFilter(Vector3[] vertices, int[] triangles, int times, float alpha, float beta) {
			alpha = Mathf.Clamp01(alpha);
			beta = Mathf.Clamp01(beta);

			var network = VertexConnection.BuildNetwork(triangles);

			Vector3[] origin = new Vector3[vertices.Length];
			Array.Copy(vertices, origin, vertices.Length);
			for(int i = 0; i < times; i++) {
				vertices = HCFilter(network, origin, vertices, triangles, alpha, beta);
			}
			return vertices;
		}
			
		public static Vector3[] HCFilter(Dictionary<int, VertexConnection> network, Vector3[] o, Vector3[] q, int[] triangles, float alpha, float beta) {
			Vector3[] p = LaplacianFilter(network, q, triangles);
			Vector3[] b = new Vector3[o.Length];

			for(int i = 0; i < p.Length; i++) {
				b[i] = p[i] - (alpha * o[i] + (1f - alpha) * q[i]);
			}

			for(int i = 0; i < p.Length; i++) {
				var adjacents = network[i].Connection;
				var bs = Vector3.zero;
				foreach(int adj in adjacents) {
					bs += b[adj];
				}
				p[i] = p[i] - (beta * b[i] + (1 - beta) / adjacents.Count * bs);
			}

			return p;
		}

        public class VertexConnection {

            public HashSet<int> Connection { get { return connection; } }

            HashSet<int> connection;

            public VertexConnection() {
                this.connection = new HashSet<int>();
            }

            public void Connect (int to) {
                connection.Add(to);
            }

            public static Dictionary<int, VertexConnection> BuildNetwork (int[] triangles) {
                var table = new Dictionary<int, VertexConnection>();

                for(int i = 0, n = triangles.Length; i < n; i += 3) {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    if(!table.ContainsKey(a)) {
                        table.Add(a, new VertexConnection());
                    }
                    if(!table.ContainsKey(b)) {
                        table.Add(b, new VertexConnection());
                    }
                    if(!table.ContainsKey(c)) {
                        table.Add(c, new VertexConnection());
                    }
                    table[a].Connect(b); table[a].Connect(c);
                    table[b].Connect(a); table[b].Connect(c);
                    table[c].Connect(a); table[c].Connect(b);
                }

                return table;
            }
	    }

    // public static void RecalculateNormals(this Mesh mesh, float angle) {
    //            Debug.Log("smoothing " + mesh.name);
    //     var trianglesOriginal = mesh.triangles;
    //     var triangles = trianglesOriginal.ToArray();
    
    //     var vertices = mesh.vertices;
    
    //     var mergeIndices = new Dictionary<int, int>();
    
    //     for (int i = 0; i < vertices.Length; i++) {
    //         var vertexHash = vertices[i].GetHashCode();                  
        
    //         if (mergeIndices.TryGetValue(vertexHash, out var index)) {
    //             for (int j = 0; j < triangles.Length; j++)
    //                 if (triangles[j] == i)
    //                     triangles[j] = index;
    //         } else
    //             mergeIndices.Add(vertexHash, i);
    //     }
    
    //     mesh.triangles = triangles;
    
    //     var normals = new Vector3[vertices.Length];
    
    //     mesh.RecalculateNormals();
    //     var newNormals = mesh.normals;
    
    //     for (int i = 0; i < vertices.Length; i++)
    //         if (mergeIndices.TryGetValue(vertices[i].GetHashCode(), out var index))
    //             normals[i] = newNormals[index];
    
    //     mesh.triangles = trianglesOriginal;
    //     mesh.normals = normals;
    // }

    /*====================================================
    *
    * Francesco Cucchiara - 3POINT SOFT
    * http://threepointsoft.altervista.org
    *
    =====================================================*/

    /* 
    * The following code was taken from: https://schemingdeveloper.com
    *
    * Visit our game studio website: http://stopthegnomes.com
    *
    * License: You may use this code however you see fit, as long as you include this notice
    *          without any modifications.
    *
    *          You may not publish a paid asset on Unity store if its main function is based on
    *          the following code, but you may publish a paid asset that uses this code.
    *
    *          If you intend to use this in a Unity store asset or a commercial project, it would
    *          be appreciated, but not required, if you let me know with a link to the asset. If I
    *          don't get back to you just go ahead and use it anyway!
    */

    public static void UnweldVertices(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        Vector2[] uvs = mesh.uv;

        List<Vector3> unweldedVerticesList = new List<Vector3>();
        int[][] unweldedSubTriangles = new int[mesh.subMeshCount][];
        List<Vector2> unweldedUvsList = new List<Vector2>();
        int currVertex = 0;

        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            int[] triangles = mesh.GetTriangles(i);
            Vector3[] unweldedVertices = new Vector3[triangles.Length];
            int[] unweldedTriangles = new int[triangles.Length];
            Vector2[] unweldedUVs = new Vector2[unweldedVertices.Length];

            for (int j = 0; j < triangles.Length; j++)
            {
                unweldedVertices[j] = vertices[triangles[j]]; //unwelded vertices are just all the vertices as they appear in the triangles array
                unweldedUVs[j] = uvs[triangles[j]];
                unweldedTriangles[j] = currVertex; //the unwelded triangle array will contain global progressive vertex indexes (1, 2, 3, ...)
                currVertex++;
            }

            unweldedVerticesList.AddRange(unweldedVertices);
            unweldedSubTriangles[i] = unweldedTriangles;
            unweldedUvsList.AddRange(unweldedUVs);
        }

        mesh.vertices = unweldedVerticesList.ToArray();
        mesh.uv = unweldedUvsList.ToArray();

        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            mesh.SetTriangles(unweldedSubTriangles[i], i, false);
        }

        // RecalculateTangents(mesh);
    }

    /// <summary>
    ///     Recalculate the normals of a mesh based on an angle threshold. This takes
    ///     into account distinct vertices that have the same position.
    /// </summary>
    /// <param name="mesh"></param>
    /// <param name="angle">
    ///     The smoothing angle. Note that triangles that already share
    ///     the same vertex will be smooth regardless of the angle! 
    /// </param>
    public static void RecalculateNormals(this Mesh mesh, float angle)
    {
        UnweldVertices(mesh);
        Debug.Log("smoothing " + mesh.name + " " + angle.ToString() );

        float cosineThreshold = Mathf.Cos(angle * Mathf.Deg2Rad);

        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = new Vector3[vertices.Length];

        // Holds the normal of each triangle in each sub mesh.
        Vector3[][] triNormals = new Vector3[mesh.subMeshCount][];

        Dictionary<VertexKey, List<VertexEntry>> dictionary = new Dictionary<VertexKey, List<VertexEntry>>(vertices.Length);

        for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; ++subMeshIndex)
        {

            int[] triangles = mesh.GetTriangles(subMeshIndex);

            triNormals[subMeshIndex] = new Vector3[triangles.Length / 3];

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int i1 = triangles[i];
                int i2 = triangles[i + 1];
                int i3 = triangles[i + 2];

                // Calculate the normal of the triangle
                Vector3 p1 = vertices[i2] - vertices[i1];
                Vector3 p2 = vertices[i3] - vertices[i1];
                Vector3 normal = Vector3.Cross(p1, p2);
                float magnitude = normal.magnitude;
                if (magnitude > 0)
                {
                    normal /= magnitude;
                }

                int triIndex = i / 3;
                triNormals[subMeshIndex][triIndex] = normal;

                List<VertexEntry> entry;
                VertexKey key;

                if (!dictionary.TryGetValue(key = new VertexKey(vertices[i1]), out entry))
                {
                    entry = new List<VertexEntry>(4);
                    dictionary.Add(key, entry);
                }

                entry.Add(new VertexEntry(subMeshIndex, triIndex, i1));

                if (!dictionary.TryGetValue(key = new VertexKey(vertices[i2]), out entry))
                {
                    entry = new List<VertexEntry>();
                    dictionary.Add(key, entry);
                }

                entry.Add(new VertexEntry(subMeshIndex, triIndex, i2));

                if (!dictionary.TryGetValue(key = new VertexKey(vertices[i3]), out entry))
                {
                    entry = new List<VertexEntry>();
                    dictionary.Add(key, entry);
                }

                entry.Add(new VertexEntry(subMeshIndex, triIndex, i3));
            }
        }

        // Each entry in the dictionary represents a unique vertex position.

        foreach (List<VertexEntry> vertList in dictionary.Values)
        {
            for (int i = 0; i < vertList.Count; ++i)
            {

                Vector3 sum = new Vector3();
                VertexEntry lhsEntry = vertList[i];

                for (int j = 0; j < vertList.Count; ++j)
                {
                    VertexEntry rhsEntry = vertList[j];

                    if (lhsEntry.VertexIndex == rhsEntry.VertexIndex)
                    {
                        sum += triNormals[rhsEntry.MeshIndex][rhsEntry.TriangleIndex];
                    }
                    else
                    {
                        // The dot product is the cosine of the angle between the two triangles.
                        // A larger cosine means a smaller angle.
                        float dot = Vector3.Dot(
                            triNormals[lhsEntry.MeshIndex][lhsEntry.TriangleIndex],
                            triNormals[rhsEntry.MeshIndex][rhsEntry.TriangleIndex]);
                        if (dot >= cosineThreshold)
                        {
                            sum += triNormals[rhsEntry.MeshIndex][rhsEntry.TriangleIndex];
                        }
                    }
                }

                normals[lhsEntry.VertexIndex] = sum.normalized;
            }
        }

        mesh.normals = normals;
    }

    private struct VertexKey
    {
        private readonly long _x;
        private readonly long _y;
        private readonly long _z;

        // Change this if you require a different precision.
        private const int Tolerance = 100000;

        // Magic FNV values. Do not change these.
        private const long FNV32Init = 0x811c9dc5;
        private const long FNV32Prime = 0x01000193;

        public VertexKey(Vector3 position)
        {
            _x = (long) (Mathf.Round(position.x * Tolerance));
            _y = (long) (Mathf.Round(position.y * Tolerance));
            _z = (long) (Mathf.Round(position.z * Tolerance));
        }

        public override bool Equals(object obj)
        {
            VertexKey key = (VertexKey) obj;
            return _x == key._x && _y == key._y && _z == key._z;
        }

        public override int GetHashCode()
        {
            long rv = FNV32Init;
            rv ^= _x;
            rv *= FNV32Prime;
            rv ^= _y;
            rv *= FNV32Prime;
            rv ^= _z;
            rv *= FNV32Prime;

            return rv.GetHashCode();
        }
    }

    private struct VertexEntry
    {
        public int MeshIndex;
        public int TriangleIndex;
        public int VertexIndex;

        public VertexEntry(int meshIndex, int triIndex, int vertIndex)
        {
            MeshIndex = meshIndex;
            TriangleIndex = triIndex;
            VertexIndex = vertIndex;
        }
    }

    
    /// <summary>
    /// Recalculates mesh tangents
    /// 
    /// For some reason the built-in RecalculateTangents function produces artifacts on dense geometries.
    /// 
    /// This implementation id derived from:
    /// 
    /// Lengyel, Eric. Computing Tangent Space Basis Vectors for an Arbitrary Mesh.
    /// Terathon Software 3D Graphics Library, 2001.
    /// http://www.terathon.com/code/tangent.html
    /// </summary>
    /// <param name="mesh"></param>
    public static void RecalculateTangents(Mesh mesh)
    {
        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;
        Vector2[] uv = mesh.uv;
        Vector3[] normals = mesh.normals;
        
        int triangleCount = triangles.Length;
        int vertexCount = vertices.Length;

        Vector3[] tan1 = new Vector3[vertexCount];
        Vector3[] tan2 = new Vector3[vertexCount];

        Vector4[] tangents = new Vector4[vertexCount];

        for (int a = 0; a < triangleCount; a += 3)
        {
            int i1 = triangles[a + 0];
            int i2 = triangles[a + 1];
            int i3 = triangles[a + 2];

            Vector3 v1 = vertices[i1];
            Vector3 v2 = vertices[i2];
            Vector3 v3 = vertices[i3];

            Vector2 w1 = uv[i1];
            Vector2 w2 = uv[i2];
            Vector2 w3 = uv[i3];

            float x1 = v2.x - v1.x;
            float x2 = v3.x - v1.x;
            float y1 = v2.y - v1.y;
            float y2 = v3.y - v1.y;
            float z1 = v2.z - v1.z;
            float z2 = v3.z - v1.z;

            float s1 = w2.x - w1.x;
            float s2 = w3.x - w1.x;
            float t1 = w2.y - w1.y;
            float t2 = w3.y - w1.y;
            
            float div = s1 * t2 - s2 * t1;
            float r = div == 0.0f ? 0.0f : 1.0f / div;

            Vector3 sDir = new Vector3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
            Vector3 tDir = new Vector3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);

            tan1[i1] += sDir;
            tan1[i2] += sDir;
            tan1[i3] += sDir;

            tan2[i1] += tDir;
            tan2[i2] += tDir;
            tan2[i3] += tDir;
        }
        
        for (int a = 0; a < vertexCount; ++a)
        {
            Vector3 n = normals[a];
            Vector3 t = tan1[a];
            
            Vector3.OrthoNormalize(ref n, ref t);
            tangents[a].x = t.x;
            tangents[a].y = t.y;
            tangents[a].z = t.z;

            tangents[a].w = (Vector3.Dot(Vector3.Cross(n, t), tan2[a]) < 0.0f) ? -1.0f : 1.0f;
        }

        mesh.tangents = tangents;
    }
    
}