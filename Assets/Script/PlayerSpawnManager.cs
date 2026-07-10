using System.Collections;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-only singleton: จัด spawn point ให้ player ตอนเกิดครั้งแรก
///
/// **ทำงานยังไง:**
///   1. `playermove.OnNetworkSpawn` เรียก `AssignSpawnPoint(this)` (server-only)
///   2. Manager เลือก spawn point ตาม mode (RoundRobin / Random)
///   3. เพิ่ม jitter offset (กัน 2 player ทับกันถ้า spawn point เดียวกัน)
///   4. ตั้ง position บน server → NGO sync ไปทุก client อัตโนมัติ
///
/// **Setup:**
///   1. สร้าง empty GameObject "PlayerSpawnManager" ใน SampleScene (gameplay scene)
///   2. Add component นี้
///   3. สร้าง child GameObjects เป็น spawn points (ตั้งตำแหน่งตามต้องการ)
///      เช่น SpawnPoint_1, SpawnPoint_2, SpawnPoint_3, SpawnPoint_4
///   4. ลากทั้งหมดเข้า `spawnPoints` array
///   5. ถ้าใช้ Random ก็เปิด `useRandom` (ค่า default = round-robin)
/// </summary>
public class PlayerSpawnManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────
    static PlayerSpawnManager _instance;
    public static PlayerSpawnManager Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = FindAnyObjectByType<PlayerSpawnManager>();
            return _instance;
        }
    }

    [Title("Spawn Points")]
    [Required("ต้องลาก Transform spawn point อย่างน้อย 1 จุด")]
    [Tooltip("ลำดับ player ที่ join → ใช้ index ของ array นี้")]
    public Transform[] spawnPoints;

    [Title("Strategy")]
    [Tooltip("ปิด = round-robin ตามลำดับ join (player 1 → SpawnPoint[0], player 2 → SpawnPoint[1] …)\n" +
             "เปิด = สุ่มเลือก spawn point")]
    public bool useRandom = false;

    [Tooltip("รัศมีสุ่ม offset จาก center ของ spawn point (เมตร) — กัน 2 player ทับกัน\n" +
             "ตั้ง 0 = ใช้ตำแหน่งเป๊ะของ spawn point")]
    [Range(0f, 5f)] public float jitterRadius = 0.5f;

    [Title("Debug")]
    public bool drawGizmos = true;
    public Color gizmoColor = new Color(0.2f, 0.8f, 1f, 0.6f);

    [Title("Auto Re-assign")]
    [Tooltip("เปิด = ตอน scene load (Start) จะวาง player ที่มีอยู่แล้วทุกคนไปยัง spawn points ทันที\n" +
             "ใช้สำหรับ scene transition: MenuScene → SampleScene (player object ค้างอยู่ ไม่ re-spawn)\n" +
             "ปิด = วางเฉพาะ player ที่ network spawn ใน scene นี้")]
    public bool reassignExistingOnStart = true;

    int _nextIndex;   // round-robin counter

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    void Start()
    {
        if (!reassignExistingOnStart) return;
        StartCoroutine(WaitAndReassignAll());
    }

    /// <summary>
    /// รอจนกว่า NGO migrate player objects เข้า scene เสร็จ → re-assign ทุกคน
    /// แก้ปัญหา: Start ของ PlayerSpawnManager รันก่อน player NetworkObject พร้อมใช้
    /// </summary>
    IEnumerator WaitAndReassignAll()
    {
        // รอ 1 frame ให้ scene transition settle
        yield return null;

        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogWarning("[PlayerSpawnManager] NetworkManager.Singleton เป็น null — skip");
            yield break;
        }
        if (!nm.IsServer) yield break;

        // Poll รอจน ConnectedClientsList มี client + ทุกคนมี PlayerObject พร้อม
        const float TIMEOUT = 3f;
        float elapsed = 0f;
        while (elapsed < TIMEOUT)
        {
            if (nm.ConnectedClientsList.Count > 0)
            {
                bool allReady = true;
                foreach (var c in nm.ConnectedClientsList)
                {
                    if (c.PlayerObject == null) { allReady = false; break; }
                }
                if (allReady) break;
            }
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        // Reset counter ให้เริ่มจาก 0 ใหม่ (กัน round-robin ค้างจาก scene ก่อนหน้า)
        _nextIndex = 0;

        int count = 0;
        foreach (var c in nm.ConnectedClientsList)
        {
            var pm = c.PlayerObject != null ? c.PlayerObject.GetComponent<playermove>() : null;
            if (pm != null)
            {
                AssignSpawnPoint(pm);
                count++;
            }
        }
        Debug.Log($"[PlayerSpawnManager] Re-assigned {count} existing players (waited {elapsed:F1}s)");
    }

    // ── Public API ────────────────────────────────────────────────────────
    /// <summary>
    /// Server-only: ตั้ง position ของ player ที่ spawn ใหม่
    /// เรียกจาก playermove.OnNetworkSpawn (after IsServer check)
    /// </summary>
    public void AssignSpawnPoint(playermove player)
    {
        if (player == null) return;
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[PlayerSpawnManager] spawnPoints ว่าง — ใช้ default position (0,0,0)");
            return;
        }

        // เลือก spawn point
        Transform sp;
        if (useRandom)
        {
            sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
        }
        else
        {
            sp = spawnPoints[_nextIndex % spawnPoints.Length];
            _nextIndex++;
        }
        if (sp == null) return;

        // คำนวณ position + jitter (offset แนวระนาบ XZ)
        Vector3 pos = sp.position;
        if (jitterRadius > 0f)
        {
            Vector2 rand = Random.insideUnitCircle * jitterRadius;
            pos += new Vector3(rand.x, 0f, rand.y);
        }

        // Teleport บน server — NGO จะ sync ไปทุก client
        // ตั้งทั้ง transform + rigidbody เพื่อกัน physics step เขียนทับ
        player.transform.position = pos;
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position = pos;
            rb.linearVelocity = Vector3.zero;       // เคลียร์ velocity เผื่อมีค่าค้าง
            rb.angularVelocity = Vector3.zero;
        }

        Debug.Log($"[PlayerSpawnManager] Spawn '{player.name}' → {sp.name} ({pos:F2})");
    }

    /// <summary>Reset counter — เรียกตอนเริ่ม wave ใหม่ / scene restart</summary>
    [Button("Reset Spawn Index"), GUIColor(1f, 0.7f, 0.3f)]
    public void ResetIndex() => _nextIndex = 0;

    // ── Gizmos ────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!drawGizmos || spawnPoints == null) return;

        Gizmos.color = gizmoColor;
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            var sp = spawnPoints[i];
            if (sp == null) continue;

            // Cylinder ที่ตำแหน่ง spawn + รัศมี jitter
            Gizmos.DrawWireSphere(sp.position + Vector3.up * 0.5f, 0.5f);
            if (jitterRadius > 0f)
            {
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.2f);
                Gizmos.DrawWireSphere(sp.position, jitterRadius);
                Gizmos.color = gizmoColor;
            }

            // Label index
            UnityEditor.Handles.Label(sp.position + Vector3.up * 1.3f, $"Spawn {i + 1}");
        }
    }
#endif
}
