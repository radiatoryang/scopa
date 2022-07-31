using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Scopa {
    /// <summary> 
    /// A simple Unity Event dispatcher for common entity system events, like OnActivate or OnReset.
    /// Recommended usage: put this on an entity prefab to setup behavior that is too simple to bother with coding manually.
    /// </summary>
    public class EntityEvents : MonoBehaviour, IScopaEntityLogic {

        public UnityEvent onStart, onEntityActivate, onEntityReset, onEntityLocked, onEntityUnlocked;

        void Start() {
            onStart.Invoke();
        }

        public void OnEntityActivate( IScopaEntityLogic activator ) {
            onEntityActivate.Invoke();
        }

        public void OnEntityReset() {
            onEntityReset.Invoke();
        }

        public void OnEntityLocked() {
            onEntityLocked.Invoke();
        }

        public void OnEntityUnlocked() {
            onEntityUnlocked.Invoke();
        }

    }
}