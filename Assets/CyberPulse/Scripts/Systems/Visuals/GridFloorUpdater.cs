using UnityEngine;

namespace CyberPulse.Systems
{
    public class GridFloorUpdater : MonoBehaviour
    {
        private static readonly int PlayerPositionID = Shader.PropertyToID("_CyberPlayerPosition");

        private void Update()
        {
            Shader.SetGlobalVector(PlayerPositionID, transform.position);
        }
    }
}
