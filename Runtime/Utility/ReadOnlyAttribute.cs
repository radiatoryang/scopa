// from https://answers.unity.com/questions/489942/how-to-make-a-readonly-property-in-inspector.html

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scopa {
    /// <summary> makes something visible but non-editable in the Unity Editor inspector </summary>
    public class ReadOnly : PropertyAttribute
    {

    }
}