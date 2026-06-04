using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Server-only singleton: Flow Field pathfinder แบบ LoL Swarm
///
/// **คอนเซ็ปต์:**
///   1. แบ่ง world เป็น grid (เช่น 100×100 cells × 2m = 200m × 200m coverage)
///   2. Bake walkability ครั้งเดียวตอน start (CheckSphere ทุก cell)
///   3. ทุก updateInterval (0.5s): Dijkstra จาก player → ทุก cell มี cost
///   4. Per cell: ดู neighbour ที่ cost น้อยสุด → เก็บเป็น direction vector
///   5. Enemy sample flow ที่ตำแหน่งตัวเอง → ได้ direction ทันที (O(1))
///
/// **ข้อดี vs A*:**
///   • 1000+ enemies → ลื่น (per-enemy = 1 array lookup)
///   • Swarm feel: enemies รุมเข้าหา player พร้อมกัน (ไม่เรียงแถว)
///   • CPU cost: compute ครั้งเดียวต่อ field, ไม่ใช่ per-enemy
///
/// **ข้อจำกัด:**
///   • 1 field = 1 target → ทุก enemy ไปหา player คนเดียวกัน
///   • Static obstacles เท่านั้น (bake ครั้งเดียว) — dynamic obj ต้องการ rebake
///   • Grid ใหญ่กิน memory (100×100 × ~16 bytes = ~150KB)
///
/// **Setup:**
///   1. สร้าง empty GameObject "FlowFieldPathfinder" ใน gameplay scene
///   2. ตั้ง `Grid Size` (เช่น 100×100), `Cell Size` (2.0m)
///   3. `Grid Origin` = มุมล่าง-ซ้ายของ map (กำหนดให้ครอบคลุม playable area)
///   4. `Obstacle Layer` = "Wall" layer
///   5. Press play — bake walkable ที่ Start
/// </summary>
public class FlowFieldPathfinder : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────
    static FlowFieldPathfinder _instance;
    public static FlowFieldPathfinder Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = FindAnyObjectByType<FlowFieldPathfinder>();
            return _instance;
        }
    }

    // ── Inspector ─────────────────────────────────────────────────────────
    [Header("Grid")]
    [Tooltip("จำนวน cell แนวกว้าง × ยาว — 100×100 ครอบคลุม 200m×200m ที่ cellSize=2")]
    public Vector2Int gridSize = new(100, 100);

    [Tooltip("ขนาด cell (เมตร) — เล็กขึ้น = path ละเอียดขึ้น แต่ compute แพง")]
    public float cellSize = 2f;

    [Tooltip("World position ของมุมล่าง-ซ้าย (X-,Z-) ของ grid")]
    public Vector3 gridOrigin = new(-100f, 0f, -100f);

    [Header("Obstacle Detection")]
    [Tooltip("Layer ของ wall — ใช้ตอน bake walkable")]
    public LayerMask obstacleLayer;

    [Tooltip("รัศมี CheckSphere ตอน bake — ใหญ่ขึ้น = เข้มกว่า (cell ใกล้ wall จะเป็น blocked)")]
    public float obstacleCheckRadius = 0.7f;

    [Tooltip("ความสูงที่ check obstacle (เมตร) — เลี่ยง floor")]
    public float castHeight = 0.5f;

    [Header("Flow Update")]
    [Tooltip("ความถี่ recompute flow field (วินาที) — 0.5 = 2Hz, เพียงพอสำหรับ player เดินปกติ")]
    public float updateInterval = 0.5f;

    [Header("Debug Gizmos")]
    public bool drawGizmos       = false;
    public bool drawWalkableOnly = true;   // ถ้า false: วาดทุก cell รวม blocked (ช้า)
    public bool drawFlowArrows   = true;

    // ── Runtime data ──────────────────────────────────────────────────────
    bool[,]     _walkable;
    float[,]    _costs;
    Vector2[,]  _flow;
    bool        _baked;
    float       _nextUpdateAt;

    // Reusable Dijkstra queue + player position buffer — กัน GC
    readonly Queue<Vector2Int> _bfsQueue          = new(2048);
    readonly List<Vector3>      _alivePlayersBuf  = new(8);

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    void Start()
    {
        BakeWalkable();
    }

    void Update()
    {
        if (!_baked) return;

        // Compute เฉพาะ server (client ไม่ได้ใช้ flow field — เห็น enemy transform ผ่าน NGO sync เท่านั้น)
        // ถ้าไม่มี NetworkManager (offline test) → ทำงานปกติ
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

        if (Time.time < _nextUpdateAt) return;
        _nextUpdateAt = Time.time + updateInterval;

        // Multi-source: รวบรวมตำแหน่ง player ทุกคนที่ยังไม่ตาย
        CollectAlivePlayers(_alivePlayersBuf);
        if (_alivePlayersBuf.Count > 0) ComputeFlowField(_alivePlayersBuf);
    }

    // ── Public API ────────────────────────────────────────────────────────
    /// <summary>
    /// คืน flow direction (XZ plane) จาก worldPos ไปหา target — caller ใช้คูณกับ speed
    /// คืน Vector3.zero ถ้า cell อยู่นอก grid หรือเป็น obstacle หรือยังไม่ compute
    /// </summary>
    public Vector3 GetFlow(Vector3 worldPos)
    {
        if (!_baked || _flow == null) return Vector3.zero;

        WorldToGrid(worldPos, out int gx, out int gy);
        if (gx < 0 || gx >= gridSize.x || gy < 0 || gy >= gridSize.y) return Vector3.zero;
        if (!_walkable[gx, gy]) return Vector3.zero;

        Vector2 v = _flow[gx, gy];
        return new Vector3(v.x, 0f, v.y);
    }

    /// <summary>Force re-bake walkable map — เรียกเมื่อ environment เปลี่ยน</summary>
    public void RebakeWalkable() => BakeWalkable();

    // ── Bake (static obstacles) ───────────────────────────────────────────
    void BakeWalkable()
    {
        _walkable = new bool[gridSize.x, gridSize.y];
        _costs    = new float[gridSize.x, gridSize.y];
        _flow     = new Vector2[gridSize.x, gridSize.y];

        int blocked = 0;
        for (int x = 0; x < gridSize.x; x++)
        for (int y = 0; y < gridSize.y; y++)
        {
            Vector3 worldPos = GridToWorld(x, y) + Vector3.up * castHeight;
            bool hit = Physics.CheckSphere(worldPos, obstacleCheckRadius, obstacleLayer,
                                           QueryTriggerInteraction.Ignore);
            _walkable[x, y] = !hit;
            if (hit) blocked++;
        }

        _baked = true;
        Debug.Log($"[FlowField] Baked {gridSize.x}×{gridSize.y} grid — {blocked} blocked cells " +
                  $"({100f * blocked / (gridSize.x * gridSize.y):F1}%)");
    }

    // ── Compute flow toward multiple targets (Multi-source Dijkstra) ──────
    /// <summary>
    /// คำนวณ flow field โดยใช้ทุก player เป็น source พร้อมกัน
    /// — Voronoi-like behavior: enemy แต่ละตัวจะเดินไปหา player ที่ใกล้สุดโดยอัตโนมัติ
    /// — ถ้า list ว่าง → ไม่ compute (เก็บ field เดิมไว้)
    /// </summary>
    void ComputeFlowField(List<Vector3> targets)
    {
        if (targets == null || targets.Count == 0) return;

        // 1) Reset costs
        for (int x = 0; x < gridSize.x; x++)
        for (int y = 0; y < gridSize.y; y++)
            _costs[x, y] = float.MaxValue;

        // 2) Seed BFS ด้วยทุก source cell (multi-source)
        _bfsQueue.Clear();
        foreach (var t in targets)
        {
            WorldToGrid(t, out int tx, out int ty);
            if (tx < 0 || tx >= gridSize.x || ty < 0 || ty >= gridSize.y) continue;
            if (_costs[tx, ty] > 0f)   // กัน enqueue ซ้ำถ้า 2 players อยู่ cell เดียวกัน
            {
                _costs[tx, ty] = 0f;
                _bfsQueue.Enqueue(new Vector2Int(tx, ty));
            }
        }
        if (_bfsQueue.Count == 0) return;   // ไม่มี source ใน grid bounds

        const float DIAG = 1.41421356f;

        while (_bfsQueue.Count > 0)
        {
            Vector2Int cell = _bfsQueue.Dequeue();
            float baseCost = _costs[cell.x, cell.y];

            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = cell.x + dx;
                int ny = cell.y + dy;
                if (nx < 0 || nx >= gridSize.x || ny < 0 || ny >= gridSize.y) continue;
                if (!_walkable[nx, ny]) continue;

                float step    = (dx != 0 && dy != 0) ? DIAG : 1f;
                float newCost = baseCost + step;

                if (newCost < _costs[nx, ny])
                {
                    _costs[nx, ny] = newCost;
                    _bfsQueue.Enqueue(new Vector2Int(nx, ny));
                }
            }
        }

        // 3) Convert costs → flow vectors (gradient descent)
        for (int x = 0; x < gridSize.x; x++)
        for (int y = 0; y < gridSize.y; y++)
        {
            if (!_walkable[x, y]) { _flow[x, y] = Vector2.zero; continue; }

            float bestCost = _costs[x, y];
            int   bestDx = 0, bestDy = 0;

            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx;
                int ny = y + dy;
                if (nx < 0 || nx >= gridSize.x || ny < 0 || ny >= gridSize.y) continue;
                if (!_walkable[nx, ny]) continue;

                if (_costs[nx, ny] < bestCost)
                {
                    bestCost = _costs[nx, ny];
                    bestDx = dx; bestDy = dy;
                }
            }

            if (bestDx == 0 && bestDy == 0) _flow[x, y] = Vector2.zero;
            else _flow[x, y] = new Vector2(bestDx, bestDy).normalized;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    void WorldToGrid(Vector3 world, out int x, out int y)
    {
        x = Mathf.FloorToInt((world.x - gridOrigin.x) / cellSize);
        y = Mathf.FloorToInt((world.z - gridOrigin.z) / cellSize);
    }

    Vector3 GridToWorld(int x, int y) => new(
        gridOrigin.x + x * cellSize + cellSize * 0.5f,
        gridOrigin.y,
        gridOrigin.z + y * cellSize + cellSize * 0.5f);

    /// <summary>
    /// รวบรวมตำแหน่ง player ทุกคนที่ยังไม่ตาย — buffer reuse กัน GC
    /// </summary>
    void CollectAlivePlayers(List<Vector3> buffer)
    {
        buffer.Clear();
        if (Unity.Netcode.NetworkManager.Singleton == null) return;

        foreach (var c in Unity.Netcode.NetworkManager.Singleton.ConnectedClientsList)
        {
            var obj = c.PlayerObject;
            if (obj == null) continue;
            var pm = obj.GetComponent<playermove>();
            if (pm != null && pm.isDead.Value) continue;
            buffer.Add(obj.transform.position);
        }
    }

    // ── Gizmos ────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!drawGizmos || _flow == null) return;

        for (int x = 0; x < gridSize.x; x++)
        for (int y = 0; y < gridSize.y; y++)
        {
            Vector3 center = GridToWorld(x, y);
            bool walk = _walkable[x, y];

            if (!walk)
            {
                if (!drawWalkableOnly)
                {
                    Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
                    Gizmos.DrawCube(center + Vector3.up * 0.1f,
                        new Vector3(cellSize * 0.9f, 0.05f, cellSize * 0.9f));
                }
                continue;
            }

            if (!drawFlowArrows) continue;

            Vector2 v = _flow[x, y];
            if (v.sqrMagnitude < 0.001f) continue;

            Vector3 dir = new Vector3(v.x, 0f, v.y) * cellSize * 0.4f;
            Vector3 start = center + Vector3.up * 0.1f;
            Vector3 end   = start + dir;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawSphere(end, cellSize * 0.07f);
        }
    }
#endif
}
