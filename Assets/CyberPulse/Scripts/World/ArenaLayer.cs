using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using CyberPulse.Enemy;

namespace CyberPulse.World
{
    /// <summary>
    /// Per-layer context for the vertical layered progression (plan Bucket B).
    ///
    /// Each stacked arena floor has one ArenaLayer on its root. It:
    ///   • collects the layer's <see cref="DataNode"/>s and reveals them progressively
    ///     (one per cleared wave) so the layer doesn't show all its objectives at once;
    ///   • tracks the enemies WaveDirector spawns into it and how many are still alive;
    ///   • bakes its <see cref="NavMeshSurface"/> once the procedural arena geometry is
    ///     generated, so each stacked floor gets its own correct NavMesh;
    ///   • fires <see cref="OnCleared"/> when the layer is fully done — all nodes
    ///     siphoned, all assigned waves spawned, and no enemies left alive — which
    ///     LayerManager uses to unlock the door to the next layer.
    /// </summary>
    public class ArenaLayer : MonoBehaviour
    {
        [SerializeField] private int   _index;
        [SerializeField] private float _floorY;
        [SerializeField] private float _arenaHalf       = 24f;
        [SerializeField] private float _spawnClearance  = 7f;
        [SerializeField] private int   _initialVisibleNodes = 1;
        [Tooltip("An enemy that drops this far below the floor has clipped off the layer and is destroyed, " +
                 "so it can't survive on a sealed-off lower floor and block this layer from clearing.")]
        [SerializeField] private float _fallCullDepth   = 3f;

        [SerializeField] private ProceduralArenaGenerator _generator;
        [SerializeField] private NavMeshSurface            _navSurface;
        [SerializeField] private LockedDoor                _exitDoor;
        [SerializeField] private LayerMask                 _obstacleMask;

        private static readonly Vector2 EntryXZ = new Vector2(0f, -22f);

        private readonly List<DataNode> _nodes        = new();
        private readonly Queue<DataNode> _hiddenQueue = new();
        private readonly List<EnemyHealth> _liveEnemies = new();

        private int  _siphonedCount;
        private int  _aliveEnemies;
        private bool _allWavesSpawned;
        private bool _cleared;
        private bool _navBaked;
        private bool _started;
        private bool _nodesResolved;

        public int        Index    => _index;
        public float      FloorY   => _floorY;
        public LockedDoor ExitDoor => _exitDoor;
        public bool       Cleared  => _cleared;
        public bool       IsTopLayer => _exitDoor == null;

        /// <summary>Objective nodes siphoned so far in this layer (for HUD).</summary>
        public int NodesSiphoned => _siphonedCount;
        /// <summary>Total objective nodes in this layer (for HUD).</summary>
        public int NodesTotal    => _nodes.Count;
        /// <summary>Enemies still alive in this layer (for HUD).</summary>
        public int EnemiesAlive  => _aliveEnemies;

        /// <summary>Fires once when this layer becomes fully cleared.</summary>
        public event Action<ArenaLayer> OnCleared;

        private void OnEnable()
        {
            if (_generator != null) _generator.OnArenaGenerated += BakeNavMesh;
        }

        private void OnDisable()
        {
            if (_generator != null) _generator.OnArenaGenerated -= BakeNavMesh;
        }

        private void Start()
        {
            _started = true;

            GetComponentsInChildren(true, _nodes);

            int visible = 0;
            foreach (var node in _nodes)
            {
                if (node == null) continue;
                node.OnSiphoned += HandleNodeSiphoned;

                if (visible < _initialVisibleNodes)
                {
                    node.Reveal();
                    visible++;
                }
                else
                {
                    node.Hide();
                    _hiddenQueue.Enqueue(node);
                }
            }

            if (!_navBaked && _generator != null && _generator.HasGenerated)
                BakeNavMesh();

            ResolveNodeOverlaps();
        }

        /// <summary>A random walkable-ish spawn point inside this layer, away from the entry.</summary>
        public Vector3 RandomSpawnPoint()
        {
            for (int i = 0; i < 12; i++)
            {
                float x = UnityEngine.Random.Range(-_arenaHalf, _arenaHalf);
                float z = UnityEngine.Random.Range(-_arenaHalf, _arenaHalf);
                if (Vector2.Distance(new Vector2(x, z), EntryXZ) < _spawnClearance) continue;
                return new Vector3(x, _floorY, z);
            }
            return new Vector3(0f, _floorY, 12f);
        }

        public float FlightHeight => _floorY + 6f;

        /// <summary>Track an enemy spawned into this layer; clears decrement its alive count.</summary>
        public void RegisterEnemy(EnemyHealth enemy)
        {
            if (enemy == null) return;
            _aliveEnemies++;
            _liveEnemies.Add(enemy);
            enemy.OnDeath += () => HandleEnemyDeath(enemy);
        }

        private void Update()
        {
            if (_liveEnemies.Count == 0) return;

            float killY = _floorY - _fallCullDepth;
            for (int i = _liveEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = _liveEnemies[i];
                if (enemy == null || enemy.IsDead) { _liveEnemies.RemoveAt(i); continue; }
                if (enemy.transform.position.y < killY)
                    enemy.TakeDamage(enemy.MaxHealth);
            }
        }

        /// <summary>Reveal the next hidden objective node (called after each cleared wave).</summary>
        public void RevealNextNode()
        {
            while (_hiddenQueue.Count > 0)
            {
                var node = _hiddenQueue.Dequeue();
                if (node == null) continue;
                node.Reveal();
                return;
            }
        }

        /// <summary>WaveDirector calls this once it has spawned every wave assigned to this layer.</summary>
        public void MarkAllWavesSpawned()
        {
            if (_allWavesSpawned) return;
            _allWavesSpawned = true;
            while (_hiddenQueue.Count > 0) RevealNextNode();
            CheckCleared();
        }

        private void HandleNodeSiphoned()
        {
            _siphonedCount++;
            CheckCleared();
        }

        private void HandleEnemyDeath(EnemyHealth enemy)
        {
            _liveEnemies.Remove(enemy);
            _aliveEnemies = Mathf.Max(0, _aliveEnemies - 1);
            CheckCleared();
        }

        private void CheckCleared()
        {
            if (_cleared || !_started) return;
            if (!_allWavesSpawned)        return;
            if (_aliveEnemies > 0)        return;
            if (_siphonedCount < _nodes.Count) return;

            _cleared = true;
            OnCleared?.Invoke(this);
        }

        private void BakeNavMesh()
        {
            if (!_navBaked && _navSurface != null)
            {
                _navBaked = true;
                _navSurface.BuildNavMesh();
            }

            ResolveNodeOverlaps();
        }

        /// <summary>
        /// Relocates any data node overlapping arena geometry to the nearest clear floor spot
        /// (falling back to lifting it straight up). Runs once, after the arena has generated.
        /// </summary>
        private void ResolveNodeOverlaps()
        {
            if (_nodesResolved) return;
            if (_generator == null || !_generator.HasGenerated) return;
            if (_nodes.Count == 0) return;
            _nodesResolved = true;

            foreach (var node in _nodes)
            {
                if (node == null) continue;
                var t = node.transform;
                if (!IsBlocked(t.position)) continue;

                bool placed = false;
                for (int i = 0; i < 24; i++)
                {
                    float x = UnityEngine.Random.Range(-_arenaHalf, _arenaHalf);
                    float z = UnityEngine.Random.Range(-_arenaHalf, _arenaHalf);
                    if (Vector2.Distance(new Vector2(x, z), EntryXZ) < _spawnClearance) continue;

                    var candidate = new Vector3(x, _floorY + 1.2f, z);
                    if (!IsBlocked(candidate)) { t.position = candidate; placed = true; break; }
                }

                if (!placed)
                {
                    var p = t.position;
                    for (int i = 0; i < 14 && IsBlocked(p); i++) p.y += 0.5f;
                    t.position = p;
                }
            }
        }

        private bool IsBlocked(Vector3 pos) =>
            Physics.CheckSphere(pos, 0.6f, _obstacleMask, QueryTriggerInteraction.Ignore);
    }
}
