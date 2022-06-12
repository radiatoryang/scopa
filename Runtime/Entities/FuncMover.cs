using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Scopa.Formats.Map.Objects;

namespace Scopa {
    /// <summary> example built-in behavior for sliding objects (doors, buttons) Quake-style, has 2 states: closed and opened </summary>
    public class FuncMover : MonoBehaviour, IScopaEntity {
        
        [Tooltip("Move offset when open, e.g. 128 0 0 means move +128 units on X axis. In level editor, use Quake axis and scale; will be automatically converted to Unity space on import.")]
        [FgdVar("movedir", FgdVar.VarType.Vector3Scaled, "Move Direction")] public Vector3 moveDir = Vector3.zero;

        [Tooltip("Upon opening, how many seconds to wait before closing. Set to -1 to never close automatically.")]
        [FgdVar("wait", FgdVar.VarType.Float, "Wait Before Close (-1 = forever)")] public float wait = -1f;

        public void OnScopaImport( ScopaEntity entity ) {
            // if ( entity.TryGetVector3Scaled("movedir", out var newMoveDir) ) {
            //     moveDir = newMoveDir;
            // }
            // if ( entity.TryGetFloat("wait", out var newWaitTime) ) {
            //     wait = newWaitTime;
            // }
        }
    }
}