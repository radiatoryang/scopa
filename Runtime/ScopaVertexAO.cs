using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Collections;
using Unity.Jobs;

namespace Scopa {
public class ScopaVertexAO
{
    public static bool useDebug = false;

    const int DEFAULT_BUFFER_SIZE = 8192;
    static List<Vector3> vert = new List<Vector3>(DEFAULT_BUFFER_SIZE);
    static List<Vector3> norm = new List<Vector3>(DEFAULT_BUFFER_SIZE);
    // static List<int> tris = new List<int>(DEFAULT_BUFFER_SIZE*3);

    public static void BakeObject(Mesh mesh, Transform transform, float rayLength = 25f, int sampleCount = 16)
    {
        float startTime = Time.realtimeSinceStartup;

        mesh.GetVertices(vert);
        mesh.GetNormals(norm);
        Color[] colors = new Color[vert.Count];

        var results = new NativeArray<RaycastHit>(vert.Count * sampleCount, Allocator.TempJob);
        var commands = new NativeArray<RaycastCommand>(vert.Count * sampleCount, Allocator.TempJob);

        for (int i = 0; i < mesh.vertexCount; i++)  {
            var pos = transform.localToWorldMatrix.MultiplyPoint3x4(vert[i]);
            var dir = transform.localToWorldMatrix.MultiplyVector(norm[i]);

            // first sample is always a sky sample that can heavily bias occ color toward white
            commands[i*sampleCount] = new RaycastCommand(
                pos + dir * 2f + Vector3.down * 0.5f,
                Vector3.up,
                rayLength * rayLength,
                1
            );

            for(int r=1; r<sampleCount-1; r++) {
                commands[i*sampleCount+r] = new RaycastCommand(
                    pos + dir * 0.35f + Random.onUnitSphere * 0.1f, 
                    dir + Random.onUnitSphere * 0.1f, 
                    rayLength,
                    1
                );
            }
        }

        var handle = RaycastCommand.ScheduleBatch(commands, results, 64);
        handle.Complete();

        for (int x = 0; x < results.Length; x++) {
            var index = Mathf.FloorToInt(x/sampleCount);
            if (results[x].collider == null) {
                if ( x % sampleCount == 0) {
                    colors[index] = Color.white * 0.69f; // bias from sky raycast
                } else {
                    colors[index] += Color.white / sampleCount / 2;
                }
            } else if (x % sampleCount != 0) {
                colors[index] += Color.white / sampleCount / 2 * Mathf.Pow(results[x].distance / rayLength, 2);
            }
        }

        commands.Dispose();
        results.Dispose();

        mesh.SetColors(colors);

        if (useDebug) 
            Debug.Log($"Baked AO for {mesh.name} in { Time.realtimeSinceStartup - startTime} ms");
    }

}

}