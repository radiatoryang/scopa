using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Ica.Normal
{
    public static class ExtensionMethods
    {
        public static void RecalculateNormalsIca(this Mesh mesh, float angle = 180f)
        {
            var cache = new MeshDataCache();
            cache.Init(new List<Mesh>(){mesh},false);
            cache.RecalculateNormals(angle);
            mesh.SetNormals(cache.NormalData.AsArray().Reinterpret<Vector3>());
            cache.Dispose();
        }
    }
}