using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace Scopa {
    /// <summary> 
    /// Generic example for a Quake-style sliding / rotating object (door, button, simple elevator platform) with 2 states: closed or opened.
    /// Opens when activated, and then closes when the root ScopaEntity resets. 
    /// If you plan to use the touch functions, make sure the mover has a kinematic rigidbody so that OnTrigger*() or OnCollision*() events fire.
    /// When possible, try to activate it via a ScopaEntity component with TryActivate() to ensure all events propagate properly. 
    /// But you can also directly Open(), Close(), or Toggle() via C#.
    /// </summary>
    public class FuncMover : MonoBehaviour, IScopaEntityLogic, IScopaEntityImport {

        [Tooltip("if false, FGD generator and entity data importer will ignore this component")]
        public bool isImportEnabled = true;

        ScopaEntity rootEntity;
        NavMeshObstacle navMeshObstacle;

        /// <summary> hacky override for mover's transform to move / rotate, if null it will use the current game object; beware that overriding this does NOT override the collider that will detect OnTrigger / OnCollision events for touch detection </summary>
        public Transform targetTransform;

        /// <summary> mover's start position in local space, cached on Awake() </summary>
        Vector3 startPos;

        /// <summary> mover's start rotation in local space, cached on Awake() </summary>
        Quaternion startRot;

        /// <summary> the amount the mover moved in the last frame </summary>
        public Vector3 lastMoveDelta { get; private set;}

        /// <summary> the amount the mover rotated in the last frame </summary>
        public Quaternion lastRotateDelta { get; private set;}

        /// <summary> is the mover currently in the process of moving? </summary>
        public bool isMoving { get; private set;}

        /// <summary> "Move offset (in local map space) when open, e.g. 128 0 0 means move +128 units on X axis. In level editor, use Quake axis and scale; will be automatically converted to Unity space on import." </summary>
        [Header("Movement")]
        [Tooltip("Move offset (in local map space) when open, e.g. 128 0 0 means move +128 units on X axis. In level editor, use Quake axis and scale; will be automatically converted to Unity space on import.")]
        [BindFgd("movedir", BindFgd.VarType.Vector3Scaled, "Move Direction")] 
        public Vector3 moveDir = Vector3.zero;

        [Tooltip("Move speed is the velocity to move in units per second. In level editor, use Quake unit scale; it will be automatically converted to Unity scale on import.")]
        [BindFgd("speed", BindFgd.VarType.FloatScaled, "Move Speed")]
        public float moveSpeed = 5f;

        [Tooltip("Rotational offset in degrees. In level edtior, use Quake axis (Z-up), and it will be converted to Unity Y-up on import. e.g. '0 0 180' in-editor means rotate 180 degrees yaw away from original rotation")]
        [BindFgd("rotate", BindFgd.VarType.Angles3D, "Rotate Direction")]
        public Vector3 rotateOffset = Vector3.zero;

        [Tooltip("Speed, in degrees per second, to rotate toward the rotation offset when opened.")]
        [BindFgd("rotate_speed", BindFgd.VarType.Float, "Rotate Speed")]
        public float rotateSpeed = 180f;

        public UnityEvent onMoveStart, onMoveEnd;

        // TODO: the math for this is more complicated than I thought, maybe implement later
        // [Tooltip("Local offset (in local map space / Quake units) for the rotation origin / pivot point... 0 0 0 means rotate around the center of the object")]
        // [BindFgd("origin", BindFgd.VarType.Vector3Scaled, "Origin Pivot Offset")]
        // public Vector3 originOffset = Vector3.zero;

        [Tooltip("Is the mover currently open? If set in level editor, the mover will open immediately (and fire its target, if any) when the game starts.")]
        [BindFgd("open", BindFgd.VarType.Bool, "Starts Open?")]
        public bool isOpen = false;

        [Tooltip("If toggle is enabled, then mover will switch to opposite state every time it is activated, and resets will be ignored.")]
        [BindFgd("toggle", BindFgd.VarType.Bool, "Toggle Mode?")]
        public bool toggle = false;

        [Header("Interact")]
        [Tooltip("Should the mover try to activate when a solid collider (with a rigidbody) collides with it? Good for detecting if the player touched buttons, automatic doors, etc.")]
        [BindFgd("touch", BindFgd.VarType.Bool, "Activate On Touch?")]
        public bool activateOnTouch = true;

        [Tooltip("If OpenOnTouch is enabled, then should we only use activators with a certain Unity tag? For example, filter for 'Player' tag to only let players touch a button.")]
        [BindFgd("touch_tag", BindFgd.VarType.String, "Touch Tag filter")]
        public string touchTag = "";

        void Awake() {
            if ( targetTransform == null )
                targetTransform = transform;

            startPos = targetTransform.localPosition;
            startRot = targetTransform.localRotation;
        }

        void Start() {
            rootEntity = GetComponentInParent<ScopaEntity>();
            if (isOpen)
                rootEntity.TryActivate( rootEntity, true );
        }

        public void OnEntityActivate( IScopaEntityLogic activator ) {
            Debug.Log($"Func_Mover {gameObject.name} is activating...");
            if ( toggle )
                isOpen = !isOpen;
            else
                isOpen = true;
        }

        public void OnEntityReset() {
            // Debug.Log(gameObject.name + " OnEntityReset!");
            if ( !toggle )
                isOpen = false;
        }

        public bool IsImportEnabled() {
            return isImportEnabled;
        }

        void Update() {
            if ( !rootEntity.isLocked ) {
                // movement, including event dispatch and delta tracking
                var newMove = Vector3.MoveTowards( 
                    targetTransform.localPosition, 
                    isOpen ? startPos + moveDir : startPos, 
                    moveSpeed * Time.deltaTime
                );
                var newMoveDelta = newMove - targetTransform.localPosition;
                
                // rotation
                var newRot = Quaternion.RotateTowards( 
                    targetTransform.localRotation, 
                    isOpen ? startRot * Quaternion.Euler(rotateOffset) : startRot, 
                    rotateSpeed * Time.deltaTime
                );
                lastRotateDelta = newRot * Quaternion.Inverse(targetTransform.localRotation);

                if ( !isMoving && (newMoveDelta.sqrMagnitude > 0.001f || Quaternion.Angle(targetTransform.localRotation, newRot) > 0.001f )) {
                    isMoving = true;
                    onMoveStart.Invoke();
                } else if ( isMoving && newMoveDelta.sqrMagnitude <= 0.001f && Quaternion.Angle(targetTransform.localRotation, newRot) < 0.001f) {
                    isMoving = false;
                    onMoveEnd.Invoke();
                }

                lastMoveDelta = newMoveDelta;
                targetTransform.localPosition = newMove;
                targetTransform.localRotation = newRot;
            }
        }

        /// <summary>Open / move the mover. Use force=true to ignore any locked state. If the mover is already open, this does nothing.</summary>
        public void Open(bool force = false) {
            if ( !rootEntity.isLocked || force ) {
                if (!isOpen)
                    rootEntity.TryActivate(null, force);
            }
        }

        /// <summary>Close / move back to its initial position. Use force=true to ignore any locked state. If the mover is already closed, this does nothing.</summary>
        public void Close(bool force = false) {
            if ( !rootEntity.isLocked || force ) {
                if (isOpen)
                    rootEntity.TryActivate(null, force);
            }
        }

        /// <summary>Move the mover to its opposite state. Use force=true to ignore any locked state.</summary>
        public void Toggle(bool force = false) {
            if ( !rootEntity.isLocked || force ) {
                rootEntity.TryActivate(null, force);
            }
        }

        // I don't use OnCollisionEnter because it seems unreliable here, for whatever reason
        void OnCollisionStay( Collision collision ) {
            if ( activateOnTouch && collision.rigidbody != null && rootEntity.canActivate) {
                if ( !string.IsNullOrWhiteSpace(touchTag) && !collision.gameObject.CompareTag(touchTag) )
                    return;
                // Debug.Log(gameObject.name + " OnCollisionEnter with " + collision.gameObject.name);
                var activator = collision.gameObject.GetComponent<ScopaEntity>();
                rootEntity.TryActivate( activator );
            }
        }

        // I don't use OnTriggerEnter because it seems unreliable here, for whatever reason
        void OnTriggerStay( Collider other ) {
            if ( !other.isTrigger && activateOnTouch && rootEntity.canActivate ) {
                if ( !string.IsNullOrWhiteSpace(touchTag) && !other.gameObject.CompareTag(touchTag) )
                    return;

                var activator = other.gameObject.GetComponent<ScopaEntity>();
                rootEntity.TryActivate( activator );
            }
        }

        public void OnEntityImport( ScopaEntityData entityData ) { 
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
                if (verts.Count > 0) {
                    var bounds = GeometryUtility.CalculateBounds(verts.ToArray(), Matrix4x4.identity);
                    navMeshObstacle.center = bounds.center;
                    navMeshObstacle.size = bounds.size;
                }
            }
        }
    }
}