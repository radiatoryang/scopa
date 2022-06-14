using System.Collections;
using System.Collections.Generic;
using Scopa.Formats.Map.Objects;
using UnityEngine;
using UnityEngine.AI;

namespace Scopa {
    /// <summary> example for sliding objects (doors, buttons) Quake-style, has 2 states: closed and opened </summary>
    public class FuncMover : MonoBehaviour, IScopaEntityLogic, IScopaEntityImport {

        ScopaEntity rootEntity;
        NavMeshObstacle navMeshObstacle;

        /// <summary> mover's start position in local space, cached on Awake() </summary>
        Vector3 startPos;
        
        /// <summary> "Move offset (in local map space) when open, e.g. 128 0 0 means move +128 units on X axis. In level editor, use Quake axis and scale; will be automatically converted to Unity space on import." </summary>
        [Header("Movement")]
        [Tooltip("Move offset (in local map space) when open, e.g. 128 0 0 means move +128 units on X axis. In level editor, use Quake axis and scale; will be automatically converted to Unity space on import.")]
        [BindFgd("movedir", BindFgd.VarType.Vector3Scaled, "Move Direction")] 
        public Vector3 moveDir = Vector3.zero;

        [Tooltip("Move speed is the velocity to move in units per second. In level editor, use Quake unit scale; it will be automatically converted to Unity scale on import.")]
        [BindFgd("speed", BindFgd.VarType.FloatScaled, "Move Speed")]
        public float moveSpeed = 5f;

        [Header("State")]
        [Tooltip("Is the mover currently open? If set in level editor, the mover will open immediately (and fire its target, if any) when the game starts.")]
        [BindFgd("open", BindFgd.VarType.Bool, "Is Open?")]
        public bool isOpen = false;

        [Tooltip("If toggle is enabled, then mover will switch to opposite state every time it is activated, and resets will be ignored.")]
        [BindFgd("toggle", BindFgd.VarType.Bool, "Toggle Mode?")]
        public bool toggle = false;

        [Header("Touch")]
        [Tooltip("Should the mover try to activate when a solid collider (with a rigidbody) collides with it? Good for detecting if the player touched buttons, automatic doors, etc.")]
        [BindFgd("touch", BindFgd.VarType.Bool, "Activate On Touch")]
        public bool activateOnTouch = true;

        [Tooltip("If OpenOnTouch is enabled, then should we only use activators with a certain Unity tag? For example, filter for 'Player' tag to only let players touch a button.")]
        [BindFgd("touch_tag", BindFgd.VarType.String, "Touch Tag filter")]
        public string touchTag = "";

        void Awake() {
            startPos = transform.localPosition;
        }

        void Start() {
            rootEntity = GetComponent<ScopaEntity>();
            if (isOpen)
                rootEntity.TryActivate( rootEntity, true );
        }

        public void OnEntityActivate( IScopaEntityLogic activator ) {
            if ( toggle )
                isOpen = !isOpen;
            else
                isOpen = true;
        }

        public void OnEntityReset() {
            Debug.Log(gameObject.name + " OnEntityReset!");
            if ( !toggle )
                isOpen = false;
        }

        void Update() {
            if ( !rootEntity.isLocked )
                transform.localPosition = Vector3.MoveTowards( transform.localPosition, isOpen ? startPos + moveDir : startPos, moveSpeed * Time.deltaTime);
        }

        void OnCollisionStay( Collision collision ) {
            if ( activateOnTouch && collision.rigidbody != null && rootEntity.canActivate) {
                if ( !string.IsNullOrWhiteSpace(touchTag) && !collision.gameObject.CompareTag(touchTag) )
                    return;
                // Debug.Log(gameObject.name + " OnCollisionEnter with " + collision.gameObject.name);
                var activator = collision.gameObject.GetComponent<ScopaEntity>();
                rootEntity.TryActivate( activator );
            }
        }

        public void OnEntityImport( Entity entityData ) { 
            // if using navmesh, then might need doors to carve navmesh obstacles, etc.
            navMeshObstacle = GetComponent<NavMeshObstacle>();
            if ( navMeshObstacle != null ) {
                navMeshObstacle.shape = NavMeshObstacleShape.Box;
                // grab all mesh filters and add all vertices together
                var verts = new List<Vector3>();
                var allMeshes = GetComponentsInChildren<MeshFilter>();
                foreach ( var mf in allMeshes ) {
                    verts.AddRange( mf.sharedMesh.vertices );
                }
                var bounds = GeometryUtility.CalculateBounds(verts.ToArray(), Matrix4x4.identity);
                navMeshObstacle.center = bounds.center;
                navMeshObstacle.size = bounds.size;
            }
        }
    }
}