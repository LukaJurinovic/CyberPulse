using UnityEngine;

namespace CyberPulse.Systems
{
    /// <summary>
    /// Pushes the player's world-space position into the global shader property
    /// _CyberPlayerPosition every frame so GridFloor.shader can compute proximity glow.
    /// Attach this to the Player GameObject.
    /// </summary>
    public class GridFloorUpdater : MonoBehaviour
    {
        private static readonly int PlayerPositionID = Shader.PropertyToID("_CyberPlayerPosition");

        private void Update()
        {
            Shader.SetGlobalVector(PlayerPositionID, transform.position);
        }
    }
}
