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

    public static void BakeObject(Mesh mesh, Transform transform, float rayLength = 25f, bool stochastic = true, int sampleCount = 16)
    {
        float startTime = Time.realtimeSinceStartup;

        mesh.GetVertices(vert);
        mesh.GetNormals(norm);
        Color[] colors = new Color[vert.Count];

        for (int i = 0; i < mesh.vertexCount; i++)  {
            var pos = transform.localToWorldMatrix.MultiplyPoint3x4(vert[i]);
            var dir = transform.localToWorldMatrix.MultiplyVector(norm[i]);
            var results = new NativeArray<RaycastHit>(sampleCount, Allocator.TempJob);
            var commands = new NativeArray<RaycastCommand>(sampleCount, Allocator.TempJob);

            if ( !Physics.Raycast(pos + dir * 2f + Vector3.down * 0.5f, Vector3.up, rayLength * rayLength, 1, QueryTriggerInteraction.Ignore) ) {
                // if vertex has LoS directly upward to sky, then bias 50% to no occlusion
                colors[i] = Color.white * 0.5f;
            } 
            
            for (int r=0; r < sampleCount; r++) {
                Vector3 randomVector = !stochastic ? Vector3.zero : Random.onUnitSphere * 0.1f;

                commands[r] = new RaycastCommand(
                    // transform.localToWorldMatrix.MultiplyPoint3x4(vert[i]) + randomVector, 
                    // transform.localToWorldMatrix.MultiplyPoint3x4(vert[i]) + (transform.localToWorldMatrix.MultiplyVector(norm[i]) + randomVector) * 0.1f, 
                    pos + dir * 0.35f, 
                    dir + randomVector, 
                    // randomVector,
                    rayLength,
                    1
                );

                // TODO: early out where we see if there's LoS

            }
            
            JobHandle handle = RaycastCommand.ScheduleBatch(commands, results, 1, default(JobHandle));
            handle.Complete();

            for (int x = 0; x < results.Length; x++) {
                if (results[x].collider == null) {
                    colors[i] += Color.white / sampleCount / 2;
                } else {
                    colors[i] += Color.white / sampleCount / 2 * Mathf.Pow(results[x].distance / rayLength, 2);
                }
            }
            commands.Dispose();
            results.Dispose();
        }

        mesh.SetColors(colors);

        if (useDebug) 
            Debug.Log($"Baked AO for {mesh.name} in { Time.realtimeSinceStartup - startTime} ms");
    }

}

}