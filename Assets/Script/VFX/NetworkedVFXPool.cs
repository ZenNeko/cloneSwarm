using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Centralized VFX Object Pool + Projectile Prefab Registry
///
/// VFX Type Pool (VFXDatabase string keys):
///   — Weapon scripts ใช้ string key → PlayByName(string, pos)
///   — กำหนด prefab + pool size ใน VFXDatabase asset
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
///   2. ลาก VFXDatabase asset ใส่ vfxDatabase field
///   3. ลาก beamPrefab (LineRenderer) — ใช้โดย PlayBeam()
///   4. ลาก Projectile NetworkObject prefabs ทั้งหมดใส่ projectilePrefabs
/// </summary>
public class NetworkedVFXPool : MonoBehaviour
{
    // ── Entry types ───────────────────────────────────────────────────────
    public static NetworkedVFXPool Instance { get; private set; }

    // ─── VFX Database ───────────────────────────────────────────────────
    [Header("VFX Database")]
    [Tooltip("VFX Database ScriptableObject")]
    public VFXDatabase vfxDatabase;



    // ─── Projectile Registry ────────────────────────────────────────────
    [Header("Projectile Registry")]
    [Tooltip("ลาก Projectile NetworkObject prefabs ทั้งหมด\n" +
             "Server ใช้ GetProjectilePrefab(id) สำหรับ Spawn\n" +
             "index = ID ที่ WeaponBase.FireProjectile ส่งไปใน ServerRpc")]
    public List<GameObject> projectilePrefabs = new();

    // ─── Runtime ─────────────────────────────────────────────────────────
    private readonly Dictionary<string, int>            _keyToPoolId  = new();
    private readonly Dictionary<int, Queue<GameObject>> _pools        = new();
    private readonly Dictionary<GameObject, int>        _projToId     = new();


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
        _keyToPoolId.Clear();
        _pools.Clear();
        _projToId.Clear();

        // ── VFXDatabase entries pools ────────────────────────────────────────
        if (vfxDatabase != null && vfxDatabase.entries != null)
        {
            for (int i = 0; i < vfxDatabase.entries.Count; i++)
            {
                var m = vfxDatabase.entries[i];
                if (m == null || string.IsNullOrEmpty(m.key) || m.prefab == null) continue;

                _keyToPoolId[m.key] = i;

                var q = new Queue<GameObject>(m.poolSize);
                for (int j = 0; j < m.poolSize; j++)
                    q.Enqueue(CreateInstance(m.prefab));
                _pools[i] = q;
            }
        }
        else
        {
            Debug.LogWarning("[VFXPool] VFXDatabase is null or empty!");
        }



        // ── Projectile registry ───────────────────────────────────────
        for (int i = 0; i < projectilePrefabs.Count; i++)
            if (projectilePrefabs[i] != null)
                _projToId[projectilePrefabs[i]] = i;

        int databaseCount = (vfxDatabase != null && vfxDatabase.entries != null) ? vfxDatabase.entries.Count : 0;
        Debug.Log($"[VFXPool] Built {_pools.Count} VFX pools " +
                  $"({databaseCount} type-mapped), " +
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
    /// คืน designedRadius ของ VFX key
    /// -1 = ไม่พบ key | 0 = fixed size (ไม่ควร scale)
    /// </summary>
    public float GetDesignedRadius(string key)
    {
        if (string.IsNullOrEmpty(key)) return -1f;
        if (!_keyToPoolId.TryGetValue(key, out int id)) return -1f;
        if (vfxDatabase == null || id < 0 || id >= vfxDatabase.entries.Count) return -1f;
        return vfxDatabase.entries[id].designedRadius;
    }

    /// <summary>
    /// เล่น VFX จาก key string — ใช้โดย VFXFactory.Play() และ weapon scripts โดยตรง
    /// direction = ทิศที่ VFX หันหน้าไป (สำหรับ VFX Graph / mesh-based VFX)
    /// </summary>
    public void PlayByName(string key, Vector3 pos, float scale = 1f, Vector3 direction = default, float arcAngle = 360f, float roll = 0f)
    {
        if (string.IsNullOrEmpty(key) || key == "None") return;
        if (!_keyToPoolId.TryGetValue(key, out int id))
        {
            Debug.LogWarning($"[VFXPool] ไม่พบ mapping สำหรับ VFX key '{key}' — กำหนดใน VFXDatabase");
            return;
        }
        PlayFromPool(id, pos, scale, direction, arcAngle, roll);
    }

    /// <summary>
    /// Overload: pass non-uniform Vector3 scale — ใช้สำหรับ Telegraph Line/Cross/Cone ที่ scale แต่ละแกนต่างกัน
    /// </summary>
    public void PlayByName3D(string key, Vector3 pos, Vector3 scale3D, Vector3 direction = default, float arcAngle = 360f, float roll = 0f)
    {
        if (string.IsNullOrEmpty(key) || key == "None") return;
        if (!_keyToPoolId.TryGetValue(key, out int id))
        {
            Debug.LogWarning($"[VFXPool] ไม่พบ mapping สำหรับ VFX key '{key}'");
            return;
        }
        PlayFromPool3D(id, pos, scale3D, direction, arcAngle, roll);
    }

    /// <summary>คืน true ถ้ามี prefab assign สำหรับ key นี้ใน pool</summary>
    public bool HasMapping(string key) => !string.IsNullOrEmpty(key) && _keyToPoolId.ContainsKey(key);

    /// <summary>
    /// Spawn beam (LineRenderer) จาก from → to แล้วคืน pool หลัง duration
    /// ใช้โดย VFXFactory.PlayBeam()
    /// </summary>
    public void PlayBeam(string beamKey, Vector3 from, Vector3 to, float duration = 0.15f)
    {
        GameObject go = null;
        int poolId = -1;
        bool isDatabasePool = false;

        if (!string.IsNullOrEmpty(beamKey) && beamKey != "Default" && beamKey != "None")
        {
            if (_keyToPoolId.TryGetValue(beamKey, out poolId))
            {
                if (_pools.TryGetValue(poolId, out var q))
                {
                    isDatabasePool = true;
                    go = q.Count > 0 ? q.Dequeue() : CreateInstance(GetPrefabForId(poolId));
                }
            }
        }

        if (go == null)
        {
            // Fallback: ถ้าไม่มี custom beam key หรือไม่พบ → สร้าง fallback beam
            go = CreateFallbackBeam();
        }

        go.SetActive(true);
        go.transform.position = from;
        go.transform.rotation = Quaternion.identity;

        var lrs = go.GetComponentsInChildren<LineRenderer>(includeInactive: true);
        if (lrs != null && lrs.Length > 0)
        {
            foreach (var lr in lrs)
            {
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.SetPosition(0, from);
                lr.SetPosition(1, to);
            }
        }
        else
        {
            Debug.LogWarning($"[VFXPool] beamPrefab / custom beam '{go.name}' ไม่มี LineRenderer component");
        }

        if (isDatabasePool)
        {
            StartCoroutine(ReturnCustomBeamToPool(go, poolId, duration));
        }
        else
        {
            StartCoroutine(DestroyFallbackBeam(go, duration));
        }
    }

    IEnumerator ReturnCustomBeamToPool(GameObject go, int poolId, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (go == null) yield break;
        go.SetActive(false);
        if (_pools.TryGetValue(poolId, out var q))
        {
            q.Enqueue(go);
        }
        else
        {
            Destroy(go);
        }
    }

    /// <summary>สร้าง LineRenderer แบบ runtime — ใช้เมื่อ beamPrefab ไม่ได้ assign</summary>
    GameObject CreateFallbackBeam()
    {
        var go = new GameObject("BeamFallback");
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.startWidth = 0.18f;
        lr.endWidth   = 0.06f;
        lr.useWorldSpace = true;

        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                  ?? Shader.Find("Sprites/Default")
                  ?? Shader.Find("Unlit/Color");
        if (shader != null)
        {
            var mat = new Material(shader) { color = new Color(0.4f, 0.8f, 1f, 1f) };
            lr.material = mat;
            lr.startColor = new Color(0.7f, 0.9f, 1f, 1f);
            lr.endColor   = new Color(0.3f, 0.6f, 1f, 0.5f);
        }
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    IEnumerator DestroyFallbackBeam(GameObject go, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (go != null) Destroy(go);
    }

    /// <summary>Internal: 3D scale variant — ใช้โดย PlayByName3D</summary>
    void PlayFromPool3D(int poolId, Vector3 pos, Vector3 scale3D, Vector3 direction, float arcAngle, float roll)
    {
        PlayFromPoolCore(poolId, pos, scale3D, direction, arcAngle, roll, useUniformScale: false, uniformScale: 1f);
    }

    /// <summary>
    /// เล่น VFX จาก pool บน client ที่เรียก (ถูกเรียกจาก ClientRpc ใน PlayerWeaponManager)
    /// direction = ทิศที่ VFX หันหน้าไป — ใช้กับ Slash/Melee VFX Graph
    /// </summary>
    public void PlayFromPool(int poolId, Vector3 pos, float scale = 1f, Vector3 direction = default, float arcAngle = 360f, float roll = 0f)
    {
        PlayFromPoolCore(poolId, pos, Vector3.one * scale, direction, arcAngle, roll, useUniformScale: true, uniformScale: scale);
    }

    // ── Recursion guard ──────────────────────────────────────────────────
    // บางครั้ง prefab ที่ instantiate มา Awake() แล้วเรียก PlayFromPool/PlayByName
    // กลับมา → infinite recursion → InsufficientExecutionStackException ตอน Internal_CloneSingle
    // → fail fast แทน hang เครื่อง
    [System.NonSerialized] int _playDepth;
    const int MAX_PLAY_DEPTH = 8;

    void PlayFromPoolCore(int poolId, Vector3 pos, Vector3 scale3D, Vector3 direction, float arcAngle, float roll, bool useUniformScale, float uniformScale)
    {
        if (poolId < 0) return;
        if (!_pools.TryGetValue(poolId, out var q)) return;

        if (_playDepth >= MAX_PLAY_DEPTH)
        {
            Debug.LogError($"[VFXPool] ⚠️ Recursion guard tripped at depth {_playDepth} for poolId={poolId} — " +
                           $"prefab '{GetPrefabForId(poolId)?.name}' อาจมี script ที่เรียก VFXFactory.Play / PlayByName ใน Awake/OnEnable");
            return;
        }
        _playDepth++;
        try { PlayFromPoolCoreImpl(poolId, pos, scale3D, direction, arcAngle, roll, useUniformScale, uniformScale, q); }
        finally { _playDepth--; }
    }

    void PlayFromPoolCoreImpl(int poolId, Vector3 pos, Vector3 scale3D, Vector3 direction, float arcAngle, float roll, bool useUniformScale, float uniformScale, Queue<GameObject> q)
    {
        GameObject srcPrefab = GetPrefabForId(poolId);
        GameObject go;
        if (q.Count > 0)
        {
            go = q.Dequeue();
        }
        else
        {
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
            if (srcPrefab == null) return;
            go = CreateInstance(srcPrefab);
        }

        // ── Position + Rotation ──────────────────────────────────────────
        go.transform.position   = pos;
        Quaternion baseRot = direction.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(direction, Vector3.up)
            : Quaternion.identity;
        
        Quaternion prefabRot = srcPrefab != null ? srcPrefab.transform.rotation : Quaternion.identity;
        go.transform.rotation = Mathf.Abs(roll) > 0.01f
            ? baseRot * Quaternion.Euler(0f, 0f, roll) * prefabRot
            : baseRot * prefabRot;

        Vector3 prefabScale = srcPrefab != null ? srcPrefab.transform.localScale : Vector3.one;
        go.transform.localScale = Vector3.Scale(scale3D, prefabScale);
        go.SetActive(true);

        // ── Play: VFX Graph หรือ ParticleSystem ──────────────────────────
        var vfxGraph = go.GetComponent<VisualEffect>();
        if (vfxGraph != null)
        {
            // maxAngle = เป้าหมาย sweep (VFX Graph animate จาก 0 → maxAngle)
            if (vfxGraph.HasFloat("maxAngle"))
                vfxGraph.SetFloat("maxAngle", arcAngle);
            // fallback สำหรับ VFX Graph ที่ set ArcAngle ตรงๆ (ไม่มี animation)
            else if (vfxGraph.HasFloat("ArcAngle"))
                vfxGraph.SetFloat("ArcAngle", arcAngle);
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
        if (vfxDatabase != null && poolId >= 0 && poolId < vfxDatabase.entries.Count)
            return vfxDatabase.entries[poolId]?.prefab;
        return null;
    }

    float CalcTTL(GameObject go, int poolId = -1)
    {
        // fixedDuration จาก Inspector — ใช้เมื่อกำหนดไว้ (VFX Graph)
        if (vfxDatabase != null && poolId >= 0 && poolId < vfxDatabase.entries.Count)
        {
            float fd = vfxDatabase.entries[poolId].fixedDuration;
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
