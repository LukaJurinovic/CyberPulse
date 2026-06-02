using UnityEngine;
using UnityEngine.InputSystem.UI;

namespace CyberPulse.UI
{
    /// <summary>
    /// Assigns the Input System UI module's default actions (Point/Click/Submit/…) at
    /// runtime. The module is added to the EventSystem by MainMenuBuilder, but actions
    /// assigned via AssignDefaultActions() at build time do not serialize into the saved
    /// scene (they live on a runtime-only generated asset), so the assignment must run at
    /// play time for uGUI buttons to respond under the New Input System.
    /// </summary>
    [RequireComponent(typeof(InputSystemUIInputModule))]
    public class InputModuleBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            var module = GetComponent<InputSystemUIInputModule>();
            if (module != null) module.AssignDefaultActions();
        }
    }
}
