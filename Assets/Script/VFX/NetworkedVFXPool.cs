using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Centralized VFX Object Pool + Projectile Prefab Registry
///
/// VFX Type Pool (VFXType enum):
///   — Weapon scripts ใช้ VFXType enum → PlayByType(VFXType, pos)
///   — กำหนด prefab + pool size ใน vfxTypeMappings[]
///   — VFXFactory.Play() / VFXFactory.PlayBeam() delegate มาที่นี่
///
/// Beam Pool:
///   — beamPrefab (LineRenderer) pool สำหรับ PlayBeam()
///
/// Projectile Registry:
///   — Server ใช้ GetProjectilePrefab(id) เพื่อ Spawn projectile NetworkObject
///   — ไม่ pool เพราะ Projectile เป็น NetworkObject (NGO จัดการ replication เอง)
///
/// Setup:
///   1. วาง NetworkedVFXPool GameObject ในทุก gameplay scene
///   2. ลาก VFX prefabs ใส่ vfxTypeMappings — กำหนด VFXType + prefab + poolSize
///   3. ลาก beamPrefab (LineRenderer) — ใช้โดย PlayBeam()
///   4. ลาก Projectile NetworkObject prefabs ทั้งหมดใส่ projectilePrefabs
/// </summary>
public class NetworkedVFXPool : MonoBehaviour
{
    // ── Entry types ───────────────────────────────────────────────────────
    [System.Serializable]
    public class VFXTypeMapping
    {
        public VFXType    type;
        [Tooltip("Prefab ที่มี ParticleSystem หรือ VFX Effect")]
        public GameObject prefab;
        [Min(1), Tooltip("จำนวน pre-allocate ต่อ client")]
        public int        poolSize = 5;
        [Tooltip("radius ที่ prefab ถูกออกแบบมา (Particle System → Shape → Radius)\n" +
                 "0 = fixed size ไม่ scale (เช่น HitEffect)\n" +
                 "ใช้โดย WeaponBase.ComputeVfxScale() เพื่อ scale VFX ตาม stat จริง")]
        public float      designedRadius = 0f;
        [Tooltip("ระยะเวลา VFX (วินาที) — ใช้กับ VFX Graph ที่ไม่มี ParticleSystem\n" +
                 "0 = auto detect จาก ParticleSystem duration")]
        public float      fixedDuration  = 0f;
    }

    public static NetworkedVFXPool Instance { get; private set; }

    // ─── VFX Type Pool (VFXType enum) ───────────────────────────────────
    [Header("VFX Type Mappings (VFXType enum — weapon scripts)")]
    [Tooltip("กำหนด VFXType → prefab สำหรับ weapon scripts ทั้งหมด")]
    public VFXTypeMapping[] vfxTypeMappings = new VFXTypeMapping[0];

    // ─── Beam Pool ───────────────────────────────────────────────────────
    [Header("Beam Prefab (LineRenderer)")]
    [Tooltip("Prefab ที่มี LineRenderer — ใช้โดย PlayBeam()")]
    public GameObject beamPrefab;
    [Min(1), Tooltip("จำนวน beam pre-allocate")]
    public int        beamPoolSize = 8;

    // ─── Projectile Registry ────────────────────────────────────────────
    [Header("Projectile Registry")]
    [Tooltip("ลาก Projectile NetworkObject prefabs ทั้งหมด\n" +
             "Server ใช้ GetProjectilePrefab(id) สำหรับ Spawn\n" +
             "index = ID ที่ WeaponBase.FireProjectile ส่งไปใน ServerRpc")]
    public List<GameObject> projectilePrefabs = new();

    // ─── Runtime ─────────────────────────────────────────────────────────
    private readonly Dictionary<VFXType, int>           _typeToPoolId = new();
    private readonly Dictionary<int, Queue<GameObject>> _pools        = new();
    private readonly Dictionary<GameObject, int>        _projToId     = new();
    private Queue<GameObject>                           _beamPool;

    // ─── Lifecycle ───────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildPools();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void BuildPools()
    {
        _typeToPoolId.Clear();
        _pools.Clear();
        _projToId.Clear();

        // ── VFXType enum pools ────────────────────────────────────────
        for (int i = 0; i < vfxTypeMappings.Length; i++)
        {
            var m = vfxTypeMappings[i];
            if (m?.prefab == null) continue;

            _typeToPoolId[m.type] = i;

            var q = new Queue<GameObject>(m.poolSize);
            for (int j = 0; j < m.poolSize; j++)
                q.Enqueue(CreateInstance(m.prefab));
            _pools[i] = q;
        }

        // ── Beam pool ─────────────────────────────────────────────────
        _beamPool = new Queue<GameObject>(beamPoolSize);
        if (beamPrefab != null)
        {
            for (int j = 0; j < beamPoolSize; j++)
                _beamPool.Enqueue(CreateInstance(beamPrefab));
        }

        // ── Projectile registry ───────────────────────────────────────
        for (int i = 0; i < projectilePrefabs.Count; i++)
            if (projectilePrefabs[i] != null)
                _projToId[projectilePrefabs[i]] = i;

        Debug.Log($"[VFXPool] Built {_pools.Count} VFX pools " +
                  $"({vfxTypeMappings.Length} type-mapped), " +
                  $"beam pool={_beamPool.Count}, " +
                  $"{_projToId.Count} projectile entries");
    }

    GameObject CreateInstance(GameObject prefab)
    {
        var go = Instantiate(prefab);
        go.SetActive(false);
        return go;
    }

    // ─── VFX API ─────────────────────────────────────────────────────────

    /// <summary>
    /// คืน designedRadius ของ VFXType
    /// -1 = ไม่พบ type | 0 = fixed size (ไม่ควร scale)
    /// </summary>
    public float GetDesignedRadius(VFXType type)
    {
        if (!_typeToPoolId.TryGetValue(type, out int id)) return -1f;
        if (id < 0 || id >= vfxTypeMappings.Length) return -1f;
        return vfxTypeMappings[id].designedRadius;
    }

    /// <summary>
    /// เล่น VFX จาก VFXType enum — ใช้โดย VFXFactory.Play() และ weapon scripts โดยตรง
    /// direction = ทิศที่ VFX หันหน้าไป (สำหรับ VFX Graph / mesh-based VFX)
    /// </summary>
    public void PlayByType(VFXType type, Vector3 pos, float scale = 1f, Vector3 direction = default)
    {
        if (!_typeToPoolId.TryGetValue(type, out int id))
        {
            Debug.LogWarning($"[VFXPool] ไม่พบ mapping สำหรับ VFXType.{type} — กำหนดใน vfxTypeMappings");
            return;
        }
        PlayFromPool(id, pos, scale, direction);
    }

    /// <summary>
    /// Spawn beam (LineRenderer) จาก from → to แล้วคืน pool หลัง duration
    /// ใช้โดย VFXFactory.PlayBeam()
    /// </summary>
    public void PlayBeam(Vector3 from, Vector3 to, float duration = 0.15f)
    {
        if (beamPrefab == null) { Debug.LogWarning("[VFXPool] beamPrefab ไม่ได้กำหนด"); return; }

        GameObject go = _beamPool.Count > 0
            ? _beamPool.Dequeue()
            : CreateInstance(beamPrefab);

        go.SetActive(true);
        go.transform.position = from;
        go.transform.rotation = Quaternion.identity;

        var lr = go.GetComponentInChildren<LineRenderer>(includeInactive: true);
        if (lr != null)
        {
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
        }
        else
        {
            Debug.LogWarning($"[VFXPool] beamPrefab '{beamPrefab.name}' ไม่มี LineRenderer component");
        }

        StartCoroutine(ReturnBeamToPool(go, duration));
    }

    IEnumerator ReturnBeamToPool(GameObject go, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (go == null) yield break;
        go.SetActive(false);
        _beamPool.Enqueue(go);
    }

    /// <summary>
    /// เล่น VFX จาก pool บน client ที่เรียก (ถูกเรียกจาก ClientRpc ใน PlayerWeaponManager)
    /// direction = ทิศที่ VFX หันหน้าไป — ใช้กับ Slash/Melee VFX Graph
    /// </summary>
    public void PlayFromPool(int poolId, Vector3 pos, float scale = 1f, Vector3 direction = default)
    {
        if (poolId < 0) return;
        if (!_pools.TryGetValue(poolId, out var q)) return;

        GameObject go;
        if (q.Count > 0)
        {
            go = q.Dequeue();
        }
        else
        {
            GameObject srcPrefab = GetPrefabForId(poolId);
            if (srcPrefab == null)
            {
                Debug.LogWarning($"[VFXPool] Pool exhausted id={poolId} และหา prefab ไม่ได้");
                return;
            }
            go = CreateInstance(srcPrefab);
            Debug.LogWarning($"[VFXPool] Pool exhausted id={poolId} ('{srcPrefab.name}'), growing");
        }

        if (go == null)
        {
            GameObject srcPrefab = GetPrefabForId(poolId);
            if (srcPrefab == null) return;
            go = CreateInstance(srcPrefab);
        }

        // ── Position + Rotation ──────────────────────────────────────────
        go.transform.position   = pos;
        go.transform.rotation   = direction.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(direction, Vector3.up)
            : Quaternion.identity;
        go.transform.localScale = Vector3.one * scale;
        go.SetActive(true);

        // ── Play: VFX Graph หรือ ParticleSystem ──────────────────────────
        var vfxGraph = go.GetComponent<VisualEffect>();
        if (vfxGraph != null)
        {
            vfxGraph.Stop();
            vfxGraph.Play();
        }
        else
        {
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
            {
                ps.Clear();
                ps.Play();
            }
        }

        StartCoroutine(ReturnToPool(go, poolId, CalcTTL(go, poolId)));
    }

    GameObject GetPrefabForId(int poolId)
    {
        if (poolId >= 0 && poolId < vfxTypeMappings.Length)
            return vfxTypeMappings[poolId]?.prefab;
        return null;
    }

    float CalcTTL(GameObject go, int poolId = -1)
    {
        // fixedDuration จาก Inspector — ใช้เมื่อกำหนดไว้ (VFX Graph)
        if (poolId >= 0 && poolId < vfxTypeMappings.Length)
        {
            float fd = vfxTypeMappings[poolId].fixedDuration;
            if (fd > 0f) return fd;
        }

        // VFX Graph ไม่มี fixedDuration → ใช้ค่า default
        if (go.GetComponent<VisualEffect>() != null) return 2f;

        // ParticleSystem — คำนวณจาก duration + lifetime
        float maxTTL = 0f;
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
        {
            float t = ps.main.duration + ps.main.startLifetime.constantMax;
            if (t > maxTTL) maxTTL = t;
        }
        return maxTTL > 0f ? maxTTL + 0.1f : 3f;
    }

    IEnumerator ReturnToPool(GameObject go, int poolId, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (go == null) yield break;
        go.SetActive(false);
        if (_pools.TryGetValue(poolId, out var q)) q.Enqueue(go);
    }

    // ─── Projectile Registry API ─────────────────────────────────────────
    /// <summary>คืน ID ของ projectile prefab (-1 = ไม่อยู่ใน registry)</summary>
    public int GetProjectileId(GameObject prefab)
        => prefab != null && _projToId.TryGetValue(prefab, out int id) ? id : -1;

    /// <summary>คืน projectile prefab จาก ID (null = ไม่พบ → ใช้ default)</summary>
    public GameObject GetProjectilePrefab(int id)
        => id >= 0 && id < projectilePrefabs.Count ? projectilePrefabs[id] : null;
}
