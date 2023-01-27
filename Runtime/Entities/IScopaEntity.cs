using System.Collections.Generic;
using UnityEngine;

namespace Scopa {
    /// <summary> interface for components to listen for inputs from the built-in ScopaEntity component; 
    /// if you don't use built-in ScopaEntity, you need to make your own component to dispatch these OnEntity* functions </summary>
    public interface IScopaEntityLogic {
        /// <summary> implement this method for when something activates this entity. Like when a button targets a door, the door could open. Return true / false based on whether activation worked (e.g. entity not reset yet, or entity locked)</summary>
        public virtual void OnEntityActivate( IScopaEntityLogic activator ) { }

        /// <summary> implement this method for when something locks this entity. Locked entities cannot activate. </summary>
        public virtual void OnEntityLocked() { }

        /// <summary> implement this method for when something unlocks this entity. Only unlocked entities can activate. </summary>
        public virtual void OnEntityUnlocked() { }

        /// <summary> implement this method for when something kills / destroys this entity. The game object will be destroyed in the next frame, so do your cleanup fast!</summary>
        public virtual void OnEntityKilled() { }

        /// <summary> implement this method for when this entity resets itself (after wait, if any) and can be activated again. Like when a door opens, the door could close itself. </summary>
        public virtual void OnEntityReset() { }
    }

    /// <summary> interface for components that can receive raw entity data during map import </summary>
    public interface IScopaEntityData {
        /// <summary> holds all the basic mostly-raw entity data </summary>
        ScopaEntityData entityData { get; set; }
    }

    /// <summary> interface for components that can do any special processing at import time and/or use BindFgd </summary>
    public interface IScopaEntityImport {
        /// <summary> implement this method to configure a component at import time, whether at runtime or editor time </summary>
        public virtual void OnEntityImport( ScopaEntityData entityData ) { }

        /// <summary> if false, ignore BindFGD attributes during FGD generation *and* don't run OnEntityImport() </summary>
        public virtual bool IsImportEnabled() { return true; }
    }
}