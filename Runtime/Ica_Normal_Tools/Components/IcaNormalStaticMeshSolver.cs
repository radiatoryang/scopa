using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Ica.Normal
{
    /// <summary>
    /// Recalculate normals of a static mesh. Useful for testing.
    /// </summary>
    public class IcaNormalStaticMeshSolver : MonoBehaviour
    {
        public Mesh TargetMesh;

        [Range(0f, 180f)]
        public float Angle = 180f;

        private void Start()
        {
            RecalculateNormals();
        }

        [ContextMenu("RecalculateNormals")]
        public void RecalculateNormals()
        {
            TargetMesh.RecalculateNormalsIca(Angle);
            
        }
    }
}