using System.Collections.Generic;
using UnityEngine;
using CyberPulse.Systems;

namespace CyberPulse.World
{
    /// <summary>
    /// Tracks all DataNodes in the scene. When every node is siphoned,
    /// advances GameManager to the Purge phase so enemies begin spawning.
    /// Nodes self-register via Register() from their Awake().
    /// </summary>
    public class DataNodeManager : MonoBehaviour
    {
        public static DataNodeManager Instance { get; private set; }

        // Nodes that called Register() before this manager's Awake ran.
        private static readonly List<DataNode> _pending = new();

        private readonly List<DataNode> _nodes = new();
        private int _siphonedCount;

        public int TotalCount    => _nodes.Count;
        public int SiphonedCount => _siphonedCount;

        /// <summary>0–1 fraction of nodes siphoned. Suitable for a progress bar.</summary>
        public float Progress => _nodes.Count > 0 ? (float)_siphonedCount / _nodes.Count : 0f;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            foreach (var node in _pending)
                if (node != null) AddNode(node);
            _pending.Clear();

        }

        // ── Registration (called from DataNode.Start) ─────────────────────────

        public static void Register(DataNode node)
        {
            if (Instance != null)
                Instance.AddNode(node);
            else
                _pending.Add(node);  // manager not awake yet — queued for Awake()
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
    }
}
