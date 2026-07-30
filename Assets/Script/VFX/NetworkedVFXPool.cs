using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
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

    // ─── Pool sizing (ปรับได้โดยไม่ต้องแก้ VFXDatabase asset) ─────────────
    [Header("Pool Sizing")]
    [Min(0.25f)]
    [Tooltip("คูณ poolSize ของทุก entry ตอน pre-allocate\n" +
             "1 = ใช้ค่าใน VFXDatabase ตรงๆ ← ค่าที่ควรใช้ตอนนี้\n\n" +
             "เคยตั้ง 1.5 ไว้ชั่วคราวตอนยังไม่รู้ peak จริง · 2026-07-29 เอา peak จาก\n" +
             "Log VFX Report ไป bake ลง VFXDatabase แล้ว จึงกลับมาเป็น 1\n" +
             "ถ้าจะตั้งเกิน 1 อีก ให้รู้ตัวว่ามันคูณทับค่าใน asset")]
    public float poolSizeMultiplier = 1f;

    [Min(1)]
    [Tooltip("พื้นขั้นต่ำต่อ pool — กัน entry ที่ตั้งไว้ 5 แล้วหมดทันทีที่มีศัตรูสองสามตัว")]
    public int minPoolSize = 8;

    [Tooltip("เตือนใน Console ครั้งแรกที่แต่ละ key ต้องโตเกิน pool\n" +
             "ปิดได้ถ้ารำคาญ — ตัวเลขยังถูกเก็บใน report อยู่ดี")]
    public bool warnOnFirstGrow = true;

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

    // ─── Diagnostics ─────────────────────────────────────────────────────
    /// <summary>สถิติต่อ pool — ใช้ตอบว่า "key ไหนตั้งไว้น้อยเกิน" หลังเล่นจบรอบ</summary>
    public class PoolStats
    {
        public string key;
        public int    configured;   // poolSize หลังคูณ multiplier แล้ว (ที่ pre-allocate จริง)
        public int    authored;     // poolSize ดิบใน VFXDatabase asset
        public int    live;         // instance ที่มีอยู่จริงตอนนี้ = configured + ที่โตเพิ่ม
        public int    inUse;        // กำลังเล่นอยู่ตอนนี้
        public int    peakInUse;    // สูงสุดที่เคยใช้พร้อมกัน  ← ตัวเลขที่ต้องเอาไปตั้ง
        public int    grows;        // จำนวนครั้งที่ต้อง Instantiate เพิ่มเพราะ pool หมด
        public bool   warned;

        /// <summary>prefab นี้มี NetworkObject ติดมาด้วย → ห้าม SetParent ตอนคืน pool
        /// NGO โยน "NetworkObject can only be re-parented after being spawned!" ทุกครั้ง
        /// (VFX ในโปรเจกต์นี้เป็น local ล้วน — prefab ที่มี NetworkObject คือ config ที่ผิด
        /// แต่โค้ดต้องไม่พังเพราะมัน)</summary>
        public bool   skipReparent;

        /// <summary>ค่าที่ควรตั้งใน VFXDatabase — peak บวก headroom 25%</summary>
        public int Recommended => Mathf.Max(1, Mathf.CeilToInt(peakInUse * 1.25f));
        /// <summary>true = ตั้งไว้น้อยกว่าที่ใช้จริง</summary>
        public bool IsUndersized => peakInUse > authored;
    }

    private readonly Dictionary<int, PoolStats> _stats = new();


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
        _stats.Clear();

        // ── VFXDatabase entries pools ────────────────────────────────────────
        if (vfxDatabase != null && vfxDatabase.entries != null)
        {
            for (int i = 0; i < vfxDatabase.entries.Count; i++)
            {
                var m = vfxDatabase.entries[i];
                if (m == null || string.IsNullOrEmpty(m.key) || m.prefab == null) continue;

                _keyToPoolId[m.key] = i;

                int size = Mathf.Max(minPoolSize, Mathf.CeilToInt(m.poolSize * poolSizeMultiplier));

                var q = new Queue<GameObject>(size);
                for (int j = 0; j < size; j++)
                    q.Enqueue(CreateInstance(m.prefab));
                _pools[i] = q;

                _stats[i] = new PoolStats
                {
                    key          = m.key,
                    authored     = m.poolSize,
                    configured   = size,
                    live         = size,
                    skipReparent = m.prefab.GetComponentInChildren<NetworkObject>(true) != null,
                };
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

    // ─── Rent / Release ──────────────────────────────────────────────────
    // ทางเข้า-ออก pool ทางเดียว — เดิม logic "dequeue ไม่ได้ก็สร้างใหม่" ถูกก๊อปไว้ 4 ที่
    // (PlayFromPoolCoreImpl · PlayFromPoolParented · PlayFromPoolParentedLoop · PlayBeam)
    // ทำให้สถิติเก็บไม่ครบถ้าเพิ่มจุดที่ 5 แล้วลืม

    /// <summary>ดึง instance จาก pool — ถ้าหมดจะสร้างเพิ่มและนับไว้ใน stats</summary>
    GameObject Rent(int poolId, Queue<GameObject> q)
    {
        _stats.TryGetValue(poolId, out var st);

        GameObject go = q.Count > 0 ? q.Dequeue() : null;

        // instance ที่ถูก Destroy ไปแล้ว (scene unload) จะเป็น null ทั้งที่ยังอยู่ในคิว
        if (go == null)
        {
            var srcPrefab = GetPrefabForId(poolId);
            if (srcPrefab == null)
            {
                Debug.LogWarning($"[VFXPool] pool id={poolId} หมด และหา prefab ไม่ได้");
                return null;
            }
            go = CreateInstance(srcPrefab);
            if (st != null)
            {
                st.live++;
                st.grows++;
                WarnGrowOnce(st);
            }
        }

        if (st != null)
        {
            st.inUse++;
            if (st.inUse > st.peakInUse) st.peakInUse = st.inUse;
        }
        return go;
    }

    /// <summary>คืน instance เข้า pool</summary>
    void Release(int poolId, GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);

        // prefab ที่มี NetworkObject ห้าม reparent — NGO โยน error ทุกครั้ง
        // ตัดสินไว้ตั้งแต่ BuildPools แล้ว ไม่ GetComponent ซ้ำใน hot path
        _stats.TryGetValue(poolId, out var st0);
        if (st0 == null || !st0.skipReparent) go.transform.SetParent(transform, false);

        if (_pools.TryGetValue(poolId, out var q)) q.Enqueue(go);
        else { Destroy(go); if (_stats.TryGetValue(poolId, out var s)) s.live--; }

        if (_stats.TryGetValue(poolId, out var st) && st.inUse > 0) st.inUse--;
    }

    void WarnGrowOnce(PoolStats st)
    {
        if (st.warned) return;
        st.warned = true;
        if (!warnOnFirstGrow) return;
        Debug.LogWarning(
            $"[VFXPool] '{st.key}' pool หมด (pre-allocate {st.configured} จาก asset {st.authored}) — " +
            $"โตอัตโนมัติแล้ว ไม่ใช่ error · จะไม่เตือนซ้ำสำหรับ key นี้อีก\n" +
            $"ดูตัวเลขที่ควรตั้งจริงได้ที่ DevTools (F1) → ปุ่ม Log VFX Report");
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
                    go = Rent(poolId, q);
                    isDatabasePool = go != null;
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
        Release(poolId, go);
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
        GameObject go = Rent(poolId, q);
        if (go == null) return;

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
        // ต้องค้นลง child ด้วย ไม่ใช่ GetComponent เฉพาะ root — VFX Graph บาง prefab
        // วาง VisualEffect ไว้บนลูก (เช่น PS_Piercing_Generic → VEG_Piercing_Generic)
        // ถ้าหาแค่ root จะได้ null แล้วตกไป branch ParticleSystem ซึ่งไม่มี → เงียบ ไม่มี VFX
        var vfxGraphs = go.GetComponentsInChildren<VisualEffect>(true);
        if (vfxGraphs.Length > 0)
        {
            foreach (var vfxGraph in vfxGraphs)
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
        // ค้นลง child ด้วยเหตุผลเดียวกับใน PlayFromPool (VisualEffect อาจไม่ได้อยู่บน root)
        if (go.GetComponentInChildren<VisualEffect>(true) != null) return 2f;

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
        Release(poolId, go);
    }

    /// <summary>
    /// เล่น VFX และกำหนด parent (เช่น ติดกับตัวผู้เล่น)
    /// </summary>
    public void PlayParented(string key, Transform parent, float scale = 1f)
    {
        if (string.IsNullOrEmpty(key) || key == "None") return;
        if (!_keyToPoolId.TryGetValue(key, out int id))
        {
            Debug.LogWarning($"[VFXPool] ไม่พบ mapping สำหรับ VFX key '{key}'");
            return;
        }
        PlayFromPoolParented(id, parent, scale);
    }

    void PlayFromPoolParented(int poolId, Transform parent, float scale)
    {
        if (poolId < 0) return;
        if (!_pools.TryGetValue(poolId, out var q)) return;

        GameObject srcPrefab = GetPrefabForId(poolId);
        GameObject go = Rent(poolId, q);
        if (go == null) return;

        // กำหนด parent และ local transform
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.up * 0.5f; // ชดเชยความสูงให้อยู่ช่วงตัวผู้เล่น
        go.transform.localRotation = Quaternion.identity;

        Vector3 prefabScale = srcPrefab != null ? srcPrefab.transform.localScale : Vector3.one;
        go.transform.localScale = prefabScale * scale;
        go.SetActive(true);

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

    /// <summary>
    /// คืน prefab ที่ใช้สำหรับ key นี้
    /// </summary>
    public GameObject GetPrefabForKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (_keyToPoolId.TryGetValue(key, out int id))
        {
            return GetPrefabForId(id);
        }
        return null;
    }

    /// <summary>
    /// เล่น VFX แบบ looping และกำหนด parent (จะไม่มีการคืน pool อัตโนมัติ)
    /// </summary>
    public GameObject PlayParentedLoop(string key, Transform parent, float scale = 1f)
    {
        if (string.IsNullOrEmpty(key) || key == "None") return null;
        if (!_keyToPoolId.TryGetValue(key, out int id))
        {
            Debug.LogWarning($"[VFXPool] ไม่พบ mapping สำหรับ VFX key '{key}'");
            return null;
        }
        return PlayFromPoolParentedLoop(id, parent, scale);
    }

    GameObject PlayFromPoolParentedLoop(int poolId, Transform parent, float scale)
    {
        if (poolId < 0) return null;
        if (!_pools.TryGetValue(poolId, out var q)) return null;

        GameObject srcPrefab = GetPrefabForId(poolId);
        GameObject go = Rent(poolId, q);
        if (go == null) return null;

        // กำหนด parent และ local transform
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.up * 0.5f; // ชดเชยความสูงให้อยู่ช่วงตัวผู้เล่น
        go.transform.localRotation = Quaternion.identity;

        Vector3 prefabScale = srcPrefab != null ? srcPrefab.transform.localScale : Vector3.one;
        go.transform.localScale = prefabScale * scale;
        go.SetActive(true);

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

        return go;
    }

    /// <summary>
    /// หยุดเล่นและดึง VFX แบบ loop กลับคืนสู่ pool
    /// </summary>
    public void StopParented(string key, GameObject go)
    {
        if (go == null || string.IsNullOrEmpty(key)) return;
        if (!_keyToPoolId.TryGetValue(key, out int id))
        {
            Destroy(go);
            return;
        }
        Release(id, go);
    }

    // ─── Projectile Registry API ─────────────────────────────────────────
    /// <summary>คืน ID ของ projectile prefab (-1 = ไม่อยู่ใน registry)</summary>
    public int GetProjectileId(GameObject prefab)
        => prefab != null && _projToId.TryGetValue(prefab, out int id) ? id : -1;

    /// <summary>คืน Vfx prefab ดั้งเดิมจาก string key ใน database</summary>
    public GameObject GetVfxPrefab(string key)
    {
        if (vfxDatabase == null || string.IsNullOrEmpty(key)) return null;
        var entry = vfxDatabase.entries.Find(e => e.key == key);
        return entry?.prefab;
    }

    /// <summary>คืน projectile prefab จาก ID (null = ไม่พบ → ใช้ default)</summary>
    public GameObject GetProjectilePrefab(int id)
        => id >= 0 && id < projectilePrefabs.Count ? projectilePrefabs[id] : null;

    // ─── Diagnostics API ─────────────────────────────────────────────────

    /// <summary>สถิติทุก pool เรียงจากตัวที่ตั้งไว้ขาดมากสุดก่อน</summary>
    public List<PoolStats> GetStatsSorted()
    {
        var list = new List<PoolStats>(_stats.Values);
        list.Sort((a, b) =>
        {
            int over = (b.peakInUse - b.authored).CompareTo(a.peakInUse - a.authored);
            return over != 0 ? over : b.peakInUse.CompareTo(a.peakInUse);
        });
        return list;
    }

    /// <summary>สรุปสั้นสำหรับ overlay — เฉพาะตัวที่ตั้งไว้น้อยเกิน</summary>
    public string BuildShortReport(int topN = 5)
    {
        var list = GetStatsSorted();
        var sb = new System.Text.StringBuilder();
        int shown = 0;

        for (int i = 0; i < list.Count && shown < topN; i++)
        {
            var s = list[i];
            if (!s.IsUndersized) continue;
            sb.AppendLine($"<color=#ff8080>{s.key}</color> peak {s.peakInUse} / set {s.authored} → <b>{s.Recommended}</b>");
            shown++;
        }

        if (shown == 0) sb.AppendLine("<color=#80ff80>all pools OK</color> - none exceeded");
        return sb.ToString();
    }

    /// <summary>
    /// ตารางเต็มลง Console — เรียกจาก DevTools ตอนจบรอบ
    /// เอาคอลัมน์ "ควรตั้ง" ไปใส่ poolSize ใน VFXDatabase asset ได้ตรงๆ
    /// </summary>
    public void LogReport()
    {
        var list = GetStatsSorted();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[VFXPool] report — {list.Count} pools · multiplier ×{poolSizeMultiplier} · floor {minPoolSize}");
        sb.AppendLine($"{"key",-26} {"ตั้งไว้",7} {"prealloc",9} {"peak",6} {"live",6} {"โต",5}  ควรตั้ง");

        int undersized = 0;
        foreach (var s in list)
        {
            bool bad = s.IsUndersized;
            if (bad) undersized++;
            sb.AppendLine($"{s.key,-26} {s.authored,7} {s.configured,9} {s.peakInUse,6} {s.live,6} {s.grows,5}  " +
                          $"{s.Recommended}{(bad ? "   ← ตั้งน้อยเกิน" : "")}");
        }

        sb.AppendLine(undersized == 0
            ? "ทุก pool พอ — ไม่ต้องแก้ VFXDatabase"
            : $"{undersized} pool ตั้งไว้น้อยกว่าที่ใช้จริง — เอาคอลัมน์ 'ควรตั้ง' ไปใส่ poolSize ใน VFXDatabase");
        sb.AppendLine("หมายเหตุ: peak เป็นค่าของรอบนี้เท่านั้น · solo กับ 4 คนใช้ไม่เท่ากัน");

        Debug.Log(sb.ToString());
    }

    /// <summary>ล้างสถิติ — ใช้เมื่ออยากวัดเฉพาะช่วง (เช่น เริ่มนับตอนบอสโผล่)</summary>
    public void ResetStats()
    {
        foreach (var s in _stats.Values)
        {
            s.peakInUse = s.inUse;
            s.grows     = 0;
            s.warned    = false;
        }
        Debug.Log("[VFXPool] เคลียร์สถิติแล้ว — เริ่มนับ peak ใหม่");
    }
}
