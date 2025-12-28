using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ica.Normal
{
    /// <summary>
    /// The main Component of the package
    /// </summary>
    public class IcaNormalSkinnedMeshSolver : MonoBehaviour
    {
        public enum NormalOutputEnum
        {
            WriteToMesh,
            WriteToMaterial
        }

        public NormalOutputEnum NormalOutputTarget = NormalOutputEnum.WriteToMesh;

        [Range(0, 180)]
        public float Angle = 180f;

        public bool RecalculateOnStart;
        public bool AlsoRecalculateTangents;

        //TODO
        //[Tooltip("Cache asset will faster initialization")]
        //public MeshDataCacheAsset DataCacheAsset;

        [Tooltip("Asset of this model in zero pose.")]
        public List<SmrPair> SmrPairs;

        private List<Mesh> _meshes;
        internal MeshDataCache _meshDataCache;
        private List<GameObject> TempObjects;
        private List<Mesh> _tempMeshes;
        private List<SkinnedMeshRenderer> TempSMRs;
        private List<List<Material>> _materials;

        private List<ComputeBuffer> _normalBuffers;
        private List<ComputeBuffer> _tangentBuffers;
        private bool _isComputeBuffersCreated;


        private bool _isInitialized;

        private void Start()
        {
            Init();
        }

        public void Init()
        {
            if (_isInitialized)
            {
                Dispose();
            }

            var meshCount = SmrPairs.Count;

            _meshes = new List<Mesh>(meshCount);
            TempObjects = new List<GameObject>(meshCount);
            TempSMRs = new List<SkinnedMeshRenderer>(meshCount);
            _tempMeshes = new List<Mesh>(meshCount);

            foreach (var pair in SmrPairs)
            {
                if (pair.Prefab == null)
                {
                    Debug.LogError("IcaNormal: Prefab of the pair is null!", this);
                    return;
                }

                if (pair.SMR == null)
                {
                    Debug.LogError("IcaNormal: SMR of the pair is null!", this);
                    return;
                }

                _meshes.Add(pair.SMR.sharedMesh);
                _tempMeshes.Add(new Mesh() { indexFormat = IndexFormat.UInt32 });

                var obj = Instantiate(pair.Prefab, transform);
                obj.SetActive(false);
                TempObjects.Add(obj);
                TempSMRs.Add(obj.GetComponentInChildren<SkinnedMeshRenderer>());
            }

            _meshDataCache = new MeshDataCache();
            _meshDataCache.Init(_meshes, AlsoRecalculateTangents);

            if (NormalOutputTarget == NormalOutputEnum.WriteToMesh)
            {
                foreach (var mesh in _meshes)
                    mesh.MarkDynamic();
            }
            else if (NormalOutputTarget == NormalOutputEnum.WriteToMaterial)
            {
                SetupForWriteToMaterial();
            }


            _isInitialized = true;
            if (RecalculateOnStart)
                RecalculateNormals();
        }

        private void SetupForWriteToMaterial()
        {
            var meshCount = SmrPairs.Count;
            _normalBuffers = new List<ComputeBuffer>(meshCount);
            _tangentBuffers = new List<ComputeBuffer>(meshCount);
            _materials = new List<List<Material>>(meshCount);
            for (int i = 0; i < meshCount; i++)
            {
                var smr = SmrPairs[i].SMR;
                var mats = new List<Material>(1);
                smr.GetMaterials(mats);
                _materials.Add(mats);
                var nBuffer = new ComputeBuffer(_meshes[i].vertexCount, sizeof(float) * 3);
                var tBuffer = new ComputeBuffer(_meshes[i].vertexCount, sizeof(float) * 4);
                _tangentBuffers.Add(tBuffer);
                _normalBuffers.Add(nBuffer);
                for (int matIndex = 0; matIndex < mats.Count; matIndex++)
                {
                    mats[matIndex].SetBuffer("normalsOutBuffer", nBuffer);
                    mats[matIndex].SetBuffer("tangentsOutBuffer", tBuffer);
                    mats[matIndex].SetFloat("_Initialized", 1);
                }
            }

            _meshDataCache.ApplyNormalsToBuffers(_normalBuffers);
            _isComputeBuffersCreated = true;
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private void Dispose()
        {
            _meshDataCache.Dispose();

            //Compute buffers need to be destroyed
            if (_isComputeBuffersCreated)
            {
                foreach (var buffer in _normalBuffers)
                    buffer.Dispose();

                foreach (var buffer in _tangentBuffers)
                    buffer.Dispose();
            }

            foreach (var tempMesh in _tempMeshes)
            {
                Destroy(tempMesh);
            }

            foreach (var tempObject in TempObjects)
            {
                Destroy(tempObject);
            }
        }

        [ContextMenu("RecalculateNormals")]
        public void RecalculateNormals()
        {
            if (!_isInitialized)
            {
                Init();
            }

            UpdateVertices();
            RecalculateCached();
        }

        private void RecalculateCached()
        {
            _meshDataCache.RecalculateNormals(Angle, AlsoRecalculateTangents);
            SetNormals();

            if (AlsoRecalculateTangents)
            {
                SetTangents();
            }
        }

        private void SetNormals()
        {
            switch (NormalOutputTarget)
            {
                case NormalOutputEnum.WriteToMesh:
                    _meshDataCache.ApplyNormalsToMeshes(_meshes);
                    break;
                case NormalOutputEnum.WriteToMaterial:
                    _meshDataCache.ApplyNormalsToBuffers(_normalBuffers);
                    break;
            }
        }

        private void SetTangents()
        {
            switch (NormalOutputTarget)
            {
                case NormalOutputEnum.WriteToMesh:
                    _meshDataCache.ApplyTangentsToMeshes(_meshes);
                    break;
                case NormalOutputEnum.WriteToMaterial:
                    _meshDataCache.ApplyTangentsToBuffers(_tangentBuffers);
                    break;
            }
        }

        /// <summary>
        /// Vertex Data need to be updated after blend shape changes.
        /// </summary>
        internal void UpdateVertices()
        {
            for (int meshIndex = 0; meshIndex < SmrPairs.Count; meshIndex++)
            {
                var smr = SmrPairs[meshIndex].SMR;
                Ica.Utils.Mesh.MeshUtils.TransferBlendShapeValues(smr, TempSMRs[meshIndex]);
                TempSMRs[meshIndex].BakeMesh(_tempMeshes[meshIndex]);
            }

            var tempMDA = Mesh.AcquireReadOnlyMeshData(_tempMeshes);
            _meshDataCache.UpdateOnlyVertexData(tempMDA);
            tempMDA.Dispose();
        }
    }
}