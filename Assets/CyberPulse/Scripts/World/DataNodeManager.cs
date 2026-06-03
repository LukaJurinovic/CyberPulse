using System.Collections.Generic;
using UnityEngine;
using CyberPulse.Systems;

namespace CyberPulse.World
{
    /// <summary>
    /// Tracks all DataNodes in the scene. When every node is siphoned,
    /// advances GameManager to the Purge phase so enemies begin spawning.
    /// Nodes self-register via Register() from their Awake().
    ///
    /// Once ProceduralArenaGenerator publishes elevated anchors (Tower/Platform tops),
    /// a fraction of nodes are relocated up there so vertical movement is rewarded.
    /// </summary>
    public class DataNodeManager : MonoBehaviour
    {
        public static DataNodeManager Instance { get; private set; }

        [SerializeField, Range(0f, 1f)]
        private float _elevatedFraction = 0.4f;

        private static readonly List<DataNode> _pending = new();

        private readonly List<DataNode> _nodes = new();
        private int  _siphonedCount;
        private bool _relocated;

        public int TotalCount    => _nodes.Count;
        public int SiphonedCount => _siphonedCount;

        /// <summary>0–1 fraction of nodes siphoned. Suitable for a progress bar.</summary>
        public float Progress => _nodes.Count > 0 ? (float)_siphonedCount / _nodes.Count : 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            foreach (var node in _pending)
                if (node != null) AddNode(node);
            _pending.Clear();
        }

        private void OnEnable()
        {
            if (ProceduralArenaGenerator.Instance != null)
                ProceduralArenaGenerator.Instance.OnArenaGenerated += RelocateToElevated;
        }

        private void OnDisable()
        {
            if (ProceduralArenaGenerator.Instance != null)
                ProceduralArenaGenerator.Instance.OnArenaGenerated -= RelocateToElevated;
        }

        private void Start()
        {
            if (!_relocated
                && ProceduralArenaGenerator.Instance != null
                && ProceduralArenaGenerator.Instance.ElevatedAnchors.Count > 0)
                RelocateToElevated();
        }

        public static void Register(DataNode node)
        {
            if (Instance != null)
                Instance.AddNode(node);
            else
                _pending.Add(node);
        }

        private void AddNode(DataNode node)
        {
            if (_nodes.Contains(node)) return;
            _nodes.Add(node);
            node.OnSiphoned += OnNodeSiphoned;
        }

        private void OnNodeSiphoned()
        {
            _siphonedCount++;

            if (_siphonedCount >= _nodes.Count)
                GameManager.Instance?.SetPhase(GamePhase.Purge);
        }

        /// <summary>
        /// Move ~_elevatedFraction of nodes onto Tower/Platform tops. Each receiving
        /// node lifts ~1m above the anchor so it floats clearly above the surface.
        /// </summary>
        private void RelocateToElevated()
        {
            if (_relocated) return;
            if (ProceduralArenaGenerator.Instance == null) return;
            var anchors = ProceduralArenaGenerator.Instance.ElevatedAnchors;
            if (anchors.Count == 0 || _nodes.Count == 0) return;

            int target = Mathf.Clamp(Mathf.RoundToInt(_nodes.Count * _elevatedFraction), 1, _nodes.Count);
            int moved  = 0;
            int aIdx   = 0;
            for (int i = 0; i < _nodes.Count && moved < target; i++)
            {
                var node = _nodes[i];
                if (node == null) continue;
                var anchor = anchors[(aIdx++) % anchors.Count];
                node.transform.position = anchor + new Vector3(
                    Random.Range(-0.5f, 0.5f),
                    1.0f,
                    Random.Range(-0.5f, 0.5f));
                moved++;
            }
            _relocated = true;
        }
    }
}
