using System.Collections;
using System.Collections.Generic;
using TMPro;
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
    /// <summary>cache ของ GetVfxDuration — CalcTTL วน GetComponentsInChildren
    /// บน prefab ที่มี ParticleSystem ได้ถึง 30 ตัว ไม่ควรคิดใหม่ทุกครั้ง</summary>
    private readonly Dictionary<string, float>          _ttlByKey     = new();
    private readonly Dictionary<int, Queue<GameObject>> _pools        = new();
    private readonly Dictionary<GameObject, int>        _projToId     = new();

    // ── ADR-006: VFXAsset id space ──────────────────────────────────────
    // vfxDatabase.assets ใช้ id เดียวกับ index ในลิสต์ (0, 1, 2, ...) ซึ่งชนกับ
    // id ของ vfxDatabase.entries (legacy) ได้ถ้าใช้ dictionary ร่วมกันตรงๆ
    // จึง offset id ฝั่ง asset ขึ้นไปไกลๆ ก่อนใช้เป็น key ภายในของ _pools/_stats
    // เพื่อ "ยืม" โครงสร้าง pool/recursion-guard เดิมได้ทั้งชุดโดยไม่ต้องแยกคลาส
    // PlayById(int id) รับ id แบบดิบ (ไม่บวก offset) แล้วบวกเองก่อนใช้งานภายใน
    private const int ASSET_POOL_ID_BASE = 1_000_000;

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
        PreallocateDamageText();
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

        // ── VFXAsset list pools (ADR-006 — ทางใหม่ id เสถียรข้ามเครื่อง) ───────
        // id = index ใน vfxDatabase.assets ตรงๆ · offset ด้วย ASSET_POOL_ID_BASE
        // ก่อนใช้เป็น key ภายในกัน id ชนกับ pool ของ entries (legacy) ด้านบน
        int assetPoolsBuilt = 0;
        if (vfxDatabase != null && vfxDatabase.assets != null)
        {
            for (int i = 0; i < vfxDatabase.assets.Count; i++)
            {
                var a = vfxDatabase.assets[i];
                if (a == null || a.prefab == null) continue; // ช่องว่าง — validator (Assets/Editor) เป็นคนเตือนแยก

                int poolId = ASSET_POOL_ID_BASE + i;
                int size = Mathf.Max(minPoolSize, Mathf.CeilToInt(a.poolSize * poolSizeMultiplier));

                var q = new Queue<GameObject>(size);
                for (int j = 0; j < size; j++)
                    q.Enqueue(CreateInstance(a.prefab));
                _pools[poolId] = q;

                _stats[poolId] = new PoolStats
                {
                    key          = $"[VFXAsset#{i}] {a.name}",
                    authored     = a.poolSize,
                    configured   = size,
                    live         = size,
                    skipReparent = a.prefab.GetComponentInChildren<NetworkObject>(true) != null,
                };
                assetPoolsBuilt++;
            }
        }

        // ── Projectile registry ───────────────────────────────────────
        for (int i = 0; i < projectilePrefabs.Count; i++)
            if (projectilePrefabs[i] != null)
                _projToId[projectilePrefabs[i]] = i;

        int databaseCount = (vfxDatabase != null && vfxDatabase.entries != null) ? vfxDatabase.entries.Count : 0;
        Debug.Log($"[VFXPool] Built {_pools.Count} VFX pools " +
                  $"({databaseCount} type-mapped, {assetPoolsBuilt} VFXAsset-mapped), " +
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
    /// color = null (ค่า default) → ไม่แตะสีของ prefab เลย พฤติกรรมเดิมทุกประการ (ADR-006 Action Item 7)
    ///         ใส่ค่ามา → tint prefab เดียวกันเป็นสีต่างกันได้ ไม่ต้องสร้าง asset แยกตามสี
    /// </summary>
    public void PlayByName(string key, Vector3 pos, float scale = 1f, Vector3 direction = default, float arcAngle = 360f, float roll = 0f, Color? color = null)
    {
        if (string.IsNullOrEmpty(key) || key == "None") return;
        if (!_keyToPoolId.TryGetValue(key, out int id))
        {
            Debug.LogWarning($"[VFXPool] ไม่พบ mapping สำหรับ VFX key '{key}' — กำหนดใน VFXDatabase");
            return;
        }
        PlayFromPool(id, pos, scale, direction, arcAngle, roll, color);
    }

    /// <summary>
    /// เล่น VFX จาก VFXAsset id (ADR-006 — ทางใหม่ id เสถียรข้ามเครื่อง แทน string key)
    /// id มาจาก "index ใน VFXDatabase.assets" เท่านั้น (ดู VFXDatabase.GetIdForAsset) —
    /// -1 = ไม่มี VFX (เทียบเท่า key ว่างของ PlayByName ตาม ADR-006 §3)
    /// เตรียมไว้ให้เฟสถัดไป (TelegraphInit.detonateVfxId ฯลฯ) เรียกใช้
    /// </summary>
    public void PlayById(int id, Vector3 pos, float scale = 1f, Vector3 direction = default, float arcAngle = 360f, float roll = 0f, Color? color = null)
    {
        if (id < 0) return;
        if (vfxDatabase == null || vfxDatabase.assets == null || id >= vfxDatabase.assets.Count)
        {
            Debug.LogWarning($"[VFXPool] PlayById: id {id} อยู่นอกขอบเขต VFXDatabase.assets");
            return;
        }
        int poolId = ASSET_POOL_ID_BASE + id;
        if (!_pools.ContainsKey(poolId))
        {
            Debug.LogWarning($"[VFXPool] PlayById: id {id} ไม่มี pool (prefab ว่าง หรือช่องว่างใน VFXDatabase.assets)");
            return;
        }
        PlayFromPoolCore(poolId, pos, Vector3.one * scale, direction, arcAngle, roll, useUniformScale: true, uniformScale: scale, color: color);
    }

    /// <summary>
    /// Overload: pass non-uniform Vector3 scale — ใช้สำหรับ Telegraph Line/Cross/Cone ที่ scale แต่ละแกนต่างกัน
    /// </summary>
    public void PlayByName3D(string key, Vector3 pos, Vector3 scale3D, Vector3 direction = default, float arcAngle = 360f, float roll = 0f, Color? color = null)
    {
        if (string.IsNullOrEmpty(key) || key == "None") return;
        if (!_keyToPoolId.TryGetValue(key, out int id))
        {
            Debug.LogWarning($"[VFXPool] ไม่พบ mapping สำหรับ VFX key '{key}'");
            return;
        }
        PlayFromPool3D(id, pos, scale3D, direction, arcAngle, roll, color);
    }

    /// <summary>คืน true ถ้ามี prefab assign สำหรับ key นี้ใน pool</summary>
    public bool HasMapping(string key) => !string.IsNullOrEmpty(key) && _keyToPoolId.ContainsKey(key);

    /// <summary>อายุจริงของ VFX ตาม key (วินาที) — ตรงกับเวลาที่ instance จะถูกคืน pool
    ///
    /// มีไว้ให้ผู้เรียกที่ต้อง "ต่ออายุ" เอฟเฟกต์ค้างพื้น ได้ spawn ตามอายุของ VFX เอง
    /// ไม่ใช่ตามจังหวะดาเมจ (ดู PlayerWeaponManager.SpawnLightningZonesServerSide)
    ///
    /// อ่านจาก prefab ได้ผลเท่ากับอ่านจาก instance — CalcTTL ดูแค่ค่า main ของ
    /// ParticleSystem กับการมีอยู่ของ VisualEffect ซึ่ง Rent ไม่ได้แก้
    ///
    /// คืน 0 ถ้าไม่รู้จัก key — ผู้เรียกต้องมี fallback เอง</summary>
    public float GetVfxDuration(string key)
    {
        if (string.IsNullOrEmpty(key) || key == "None") return 0f;
        if (_ttlByKey.TryGetValue(key, out float cached)) return cached;
        if (!_keyToPoolId.TryGetValue(key, out int poolId)) return 0f;

        var   prefab = GetPrefabForId(poolId);
        float ttl    = prefab != null ? CalcTTL(prefab, poolId, includeInactive: true) : 0f;
        _ttlByKey[key] = ttl;
        return ttl;
    }

    /// <summary>
    /// คืน designedRadius ของ VFXAsset ตาม id (ADR-006) — เทียบเท่า GetDesignedRadius(string) ฝั่งเก่า
    /// -1 = ไม่พบ id | 0 = fixed size (ไม่ควร scale)
    /// </summary>
    public float GetDesignedRadiusById(int id)
    {
        if (id < 0) return -1f;
        if (vfxDatabase == null || vfxDatabase.assets == null || id >= vfxDatabase.assets.Count) return -1f;
        var a = vfxDatabase.assets[id];
        return a != null ? a.designedRadius : -1f;
    }

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
    void PlayFromPool3D(int poolId, Vector3 pos, Vector3 scale3D, Vector3 direction, float arcAngle, float roll, Color? color = null)
    {
        PlayFromPoolCore(poolId, pos, scale3D, direction, arcAngle, roll, useUniformScale: false, uniformScale: 1f, color: color);
    }

    /// <summary>
    /// เล่น VFX จาก pool บน client ที่เรียก (ถูกเรียกจาก ClientRpc ใน PlayerWeaponManager)
    /// direction = ทิศที่ VFX หันหน้าไป — ใช้กับ Slash/Melee VFX Graph
    /// </summary>
    public void PlayFromPool(int poolId, Vector3 pos, float scale = 1f, Vector3 direction = default, float arcAngle = 360f, float roll = 0f, Color? color = null)
    {
        PlayFromPoolCore(poolId, pos, Vector3.one * scale, direction, arcAngle, roll, useUniformScale: true, uniformScale: scale, color: color);
    }

    // ── Recursion guard ──────────────────────────────────────────────────
    // บางครั้ง prefab ที่ instantiate มา Awake() แล้วเรียก PlayFromPool/PlayByName
    // กลับมา → infinite recursion → InsufficientExecutionStackException ตอน Internal_CloneSingle
    // → fail fast แทน hang เครื่อง
    [System.NonSerialized] int _playDepth;
    const int MAX_PLAY_DEPTH = 8;

    void PlayFromPoolCore(int poolId, Vector3 pos, Vector3 scale3D, Vector3 direction, float arcAngle, float roll, bool useUniformScale, float uniformScale, Color? color = null)
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
        try { PlayFromPoolCoreImpl(poolId, pos, scale3D, direction, arcAngle, roll, useUniformScale, uniformScale, q, color); }
        finally { _playDepth--; }
    }

    void PlayFromPoolCoreImpl(int poolId, Vector3 pos, Vector3 scale3D, Vector3 direction, float arcAngle, float roll, bool useUniformScale, float uniformScale, Queue<GameObject> q, Color? color = null)
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

        // ── Tint (ADR-006 Action Item 7) ──────────────────────────────────
        // ต้องตั้งก่อน Play() เพื่อให้อนุภาคที่ spawn รอบแรกได้สีนี้ด้วย
        // เรียกทุกครั้งแม้ color == null — instance มาจาก pool ใช้ซ้ำ ถ้าไม่คืนค่า
        // ตัวที่เคยถูก tint แดงจะโผล่มาแดงค้างในครั้งถัดไปที่ไม่ได้ส่งสีมา
        ApplyColorTint(go, srcPrefab, color);

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

    /// <summary>
    /// Tint VFX instance เป็นสีที่กำหนด (ADR-006 Action Item 7) — กันไม่ให้ต้องสร้าง
    /// asset แยกตามสีเช่น Detonate_Fire_Purple / Detonate_Fire_Blue
    ///
    /// ลองชื่อ exposed property ที่ VFX Graph มักตั้งไว้คุมสี ("Color" / "TintColor" / "MainColor")
    /// ถ้า prefab ไม่มี property เหล่านี้และไม่มี ParticleSystem เลย → ไม่ทำอะไรเงียบๆ ไม่ error
    ///
    /// **ต้องเรียกทุกครั้งที่ยืม instance ออกจาก pool ไม่ใช่เฉพาะตอนมีสี** — instance ถูกใช้ซ้ำ
    /// สีที่ tint ไว้รอบก่อนติดอยู่กับตัว object ไม่ได้หายไปตอนคืน pool
    /// color == null → คืนค่าที่ prefab เขียนไว้เดิม ซึ่งคือพฤติกรรมก่อนมี ADR-006 ทุกประการ
    ///
    /// `srcPrefab` ต้องเป็น prefab ต้นทางของ `go` (instance สร้างจาก Instantiate ตัวนี้)
    /// ลำดับ component ใน GetComponentsInChildren จึงตรงกันดัชนีต่อดัชนี
    /// </summary>
    void ApplyColorTint(GameObject go, GameObject srcPrefab, Color? color)
    {
        var vfxGraphs    = go.GetComponentsInChildren<VisualEffect>(true);
        var srcVfxGraphs = srcPrefab != null ? srcPrefab.GetComponentsInChildren<VisualEffect>(true) : null;

        for (int i = 0; i < vfxGraphs.Length; i++)
        {
            var vfxGraph = vfxGraphs[i];
            // หาว่า graph ตัวนี้เปิด property ชื่อไหนไว้ให้คุมสี — ไม่มีเลยก็ข้าม
            string prop = vfxGraph.HasVector4("Color")     ? "Color"
                        : vfxGraph.HasVector4("TintColor") ? "TintColor"
                        : vfxGraph.HasVector4("MainColor") ? "MainColor"
                        : null;
            if (prop == null) continue;

            if (color.HasValue)
            {
                vfxGraph.SetVector4(prop, color.Value);
            }
            else if (srcVfxGraphs != null && i < srcVfxGraphs.Length)
            {
                // คืนค่าที่ prefab เขียนไว้ — ถ้าไม่ทำ instance ที่เคยถูก tint จะค้างสีเดิม
                vfxGraph.SetVector4(prop, srcVfxGraphs[i].GetVector4(prop));
            }
        }

        // startColor คุมสีของอนุภาคที่ spawn ใหม่หลังจากนี้ — ต้องตั้งก่อน ps.Play()
        var particles    = go.GetComponentsInChildren<ParticleSystem>(true);
        var srcParticles = srcPrefab != null ? srcPrefab.GetComponentsInChildren<ParticleSystem>(true) : null;

        for (int i = 0; i < particles.Length; i++)
        {
            var main = particles[i].main;

            if (color.HasValue)
            {
                main.startColor = color.Value;
            }
            else if (srcParticles != null && i < srcParticles.Length)
            {
                // คืนเป็น MinMaxGradient ทั้งก้อน ไม่ใช่ Color เดี่ยว — prefab อาจตั้งเป็น
                // gradient หรือสุ่มระหว่างสองสีไว้ ซึ่งการ tint ครั้งก่อนยุบทิ้งไปแล้ว
                main.startColor = srcParticles[i].main.startColor;
            }
        }
    }

    GameObject GetPrefabForId(int poolId)
    {
        if (poolId >= ASSET_POOL_ID_BASE)
        {
            int idx = poolId - ASSET_POOL_ID_BASE;
            if (vfxDatabase != null && vfxDatabase.assets != null && idx >= 0 && idx < vfxDatabase.assets.Count)
                return vfxDatabase.assets[idx]?.prefab;
            return null;
        }
        if (vfxDatabase != null && poolId >= 0 && poolId < vfxDatabase.entries.Count)
            return vfxDatabase.entries[poolId]?.prefab;
        return null;
    }

    /// <param name="includeInactive">true เมื่ออ่านจาก prefab asset —
    /// prefab ที่ยังไม่ Instantiate มี activeInHierarchy = false ทั้งก้อน
    /// GetComponentsInChildren แบบ default จึงคืนศูนย์ตัว แล้วตกไปใช้ค่าเดา 3f เงียบๆ</param>
    float CalcTTL(GameObject go, int poolId = -1, bool includeInactive = false)
    {
        // fixedDuration จาก Inspector — ใช้เมื่อกำหนดไว้ (VFX Graph)
        if (poolId >= ASSET_POOL_ID_BASE)
        {
            int idx = poolId - ASSET_POOL_ID_BASE;
            if (vfxDatabase != null && vfxDatabase.assets != null && idx >= 0 && idx < vfxDatabase.assets.Count)
            {
                var a = vfxDatabase.assets[idx];
                if (a != null && a.fixedDuration > 0f) return a.fixedDuration;
            }
        }
        else if (vfxDatabase != null && poolId >= 0 && poolId < vfxDatabase.entries.Count)
        {
            float fd = vfxDatabase.entries[poolId].fixedDuration;
            if (fd > 0f) return fd;
        }

        // VFX Graph ไม่มี fixedDuration → ใช้ค่า default
        // ค้นลง child ด้วยเหตุผลเดียวกับใน PlayFromPool (VisualEffect อาจไม่ได้อยู่บน root)
        if (go.GetComponentInChildren<VisualEffect>(true) != null) return 2f;

        // ParticleSystem — คำนวณจาก duration + lifetime
        float maxTTL = 0f;
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(includeInactive))
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

    // ─── Floating Damage Text ────────────────────────────────────────────
    // รวมมาจาก FloatingDamageTextPool (Round 8) — pool นี้อยู่ในทุก gameplay scene อยู่แล้ว
    // จึง pre-allocate ตอนโหลดฉากได้ฟรี ไม่ต้องให้ผู้ใช้ลาก component เพิ่ม
    // FloatingDamageText อยู่ท้ายไฟล์นี้ — ถูก AddComponent ตอน runtime เท่านั้น
    // (คลาสที่ชื่อไม่ตรงไฟล์แปะใน Inspector ไม่ได้ แต่ AddComponent ได้ปกติ)

    [Header("Floating Damage Text")]
    [Tooltip("Prefab ของตัวเลขดาเมจ (optional) — เว้นว่างจะสร้าง TextMeshPro เปล่าให้\n" +
             "ใส่ prefab ที่มี TextMeshPro เพื่อคุม font/material เอง")]
    public GameObject damageTextPrefab;

    [Min(1)]
    [Tooltip("จำนวน pre-allocate ตอนโหลดฉาก — solo วัดจริง HitEffect peak 559 แต่ตัวเลข\n" +
             "อยู่บนจอสั้นกว่า VFX มาก · โตเองได้ถ้าหมด (เตือนครั้งเดียว)")]
    public int damageTextCapacity = 200;

    [Tooltip("อายุตัวเลข (วินาที) ก่อนจางหาย")]
    public float damageTextDuration = 0.8f;
    [Tooltip("ความเร็วลอยขึ้น")]
    public float damageTextFloatSpeed = 1.2f;

    [Range(0f, 1f)]
    [Tooltip("เริ่มจางที่กี่ % ของอายุ — 0.5 = ทึบครึ่งแรก แล้วค่อยจางในครึ่งหลัง\n" +
             "ตั้ง 0 = จางตั้งแต่วินาทีแรก (แบบเดิม ซึ่งดูเหมือนหายวับเพราะช่วงที่ยังอ่านออกกินเวลาเกือบทั้งหมด)")]
    public float damageTextFadeStart = 0.5f;
    [Tooltip("ขนาดตัวอักษรปกติ")]
    public float damageTextNormalSize = 4f;
    [Tooltip("ขนาดตัวอักษรตอนคริต")]
    public float damageTextCritSize = 6f;
    [Tooltip("สีปกติ")]
    public Color damageTextNormalColor = Color.white;
    [Tooltip("สีตอนคริต")]
    public Color damageTextCritColor = new Color(1f, 0.85f, 0.1f, 1f);
    [Tooltip("ระยะสุ่มแนวนอน (±X, ±Z) กันตัวเลขซ้อนกันตอนตีรัว")]
    public float damageTextRandomOffset = 0.3f;

    private readonly Queue<FloatingDamageText> _dmgTextPool = new();
    private bool _dmgTextWarnedGrow;
    private int  _dmgTextCreated;

    void PreallocateDamageText()
    {
        for (int i = 0; i < damageTextCapacity; i++)
        {
            var item = CreateDamageTextItem();
            item.gameObject.SetActive(false);
            _dmgTextPool.Enqueue(item);
        }
    }

    FloatingDamageText CreateDamageTextItem()
    {
        _dmgTextCreated++;
        GameObject go;
        if (damageTextPrefab != null)
        {
            go = Instantiate(damageTextPrefab, transform);
        }
        else
        {
            go = new GameObject($"FloatingDamageText_{_dmgTextCreated}");
            go.transform.SetParent(transform, false);
        }

        var fdt = go.GetComponent<FloatingDamageText>();
        if (fdt == null) fdt = go.AddComponent<FloatingDamageText>();
        return fdt;
    }

    /// <summary>แสดงเลขดาเมจลอยที่ตำแหน่ง — เรียกจาก Enemy.NotifyHitClientRpc บนทุก client</summary>
    public void PlayDamageNumber(Vector3 position, float damage, bool isCrit)
    {
        FloatingDamageText item;
        if (_dmgTextPool.Count > 0)
        {
            item = _dmgTextPool.Dequeue();
            if (item == null)   // ค้างในคิวหลัง scene unload
            {
                PlayDamageNumber(position, damage, isCrit);
                return;
            }
        }
        else
        {
            if (!_dmgTextWarnedGrow)
            {
                Debug.LogWarning($"[VFXPool] damage-text pool หมด ({damageTextCapacity}) — โตอัตโนมัติ ไม่ใช่ error · เตือนครั้งเดียว");
                _dmgTextWarnedGrow = true;
            }
            item = CreateDamageTextItem();
        }

        item.Init(damage, isCrit, position, this);
    }

    /// <summary>FloatingDamageText คืนตัวเองเมื่อหมดอายุ</summary>
    public void ReturnDamageText(FloatingDamageText item)
    {
        if (item == null) return;
        item.gameObject.SetActive(false);
        _dmgTextPool.Enqueue(item);
    }

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

/// <summary>
/// ตัวเลขดาเมจลอยขึ้นแล้วจาง — สร้างโดย NetworkedVFXPool.PlayDamageNumber เท่านั้น
///
/// อยู่ไฟล์เดียวกับ pool ตามการตัดสินใจ Round 8: คลาสที่ชื่อไม่ตรงชื่อไฟล์
/// แปะใน Inspector/prefab ไม่ได้ แต่ AddComponent ตอน runtime ได้ปกติ ซึ่งคือ
/// ทางเดียวที่คลาสนี้ถูกสร้างอยู่แล้ว · ค่าปรับแต่งทั้งหมดอ่านจาก pool
/// (แก้ใน Inspector ของ NetworkedVFXPool แล้วมีผลกับตัวเลขใบถัดไปทันที)
/// </summary>
public class FloatingDamageText : MonoBehaviour
{
    private TextMeshPro textMesh;
    private float   _duration = 0.8f;
    private float   _floatSpeed = 1.2f;
    private float   _fadeStart = 0.5f;
    private float   _elapsedTime;
    private Vector3 _startPos;
    private Camera  _cam;
    private NetworkedVFXPool _pool;
    private Color   _activeColor;

    void EnsureTextMesh()
    {
        if (textMesh == null)
        {
            textMesh = GetComponent<TextMeshPro>();
            if (textMesh == null)
            {
                textMesh = gameObject.AddComponent<TextMeshPro>();
                textMesh.alignment = TextAlignmentOptions.Center;
                textMesh.rectTransform.sizeDelta = new Vector2(3f, 1f);
            }
        }
    }

    public void Init(float damage, bool isCrit, Vector3 basePos, NetworkedVFXPool pool)
    {
        _pool        = pool;
        _elapsedTime = 0f;
        _duration    = pool.damageTextDuration;
        _floatSpeed  = pool.damageTextFloatSpeed;
        _fadeStart   = pool.damageTextFadeStart;

        EnsureTextMesh();

        // สุ่มเฉพาะแนวนอน — ทิศลอยขึ้นต้องเหมือนกันทุกใบ
        Vector3 offset = new Vector3(
            Random.Range(-pool.damageTextRandomOffset, pool.damageTextRandomOffset),
            0f,
            Random.Range(-pool.damageTextRandomOffset, pool.damageTextRandomOffset)
        );
        _startPos = basePos + offset;
        transform.position = _startPos;

        textMesh.text      = Mathf.RoundToInt(damage).ToString();
        textMesh.fontSize  = isCrit ? pool.damageTextCritSize  : pool.damageTextNormalSize;
        textMesh.fontStyle = isCrit ? FontStyles.Bold : FontStyles.Normal;
        _activeColor       = isCrit ? pool.damageTextCritColor : pool.damageTextNormalColor;
        textMesh.color     = _activeColor;
        textMesh.alpha     = 1f;   // รีเซ็ตความโปร่ง — ตัวนี้ถูกใช้ซ้ำจาก pool

        gameObject.SetActive(true);
    }

    void Update()
    {
        _elapsedTime += Time.deltaTime;
        if (_elapsedTime >= _duration)
        {
            if (_pool != null) _pool.ReturnDamageText(this);
            else               gameObject.SetActive(false);
            return;
        }

        float t = _elapsedTime / _duration;

        // ลอยขึ้น
        transform.position = _startPos + Vector3.up * (_floatSpeed * t);

        // จางหาย — ทึบเต็มจนถึง _fadeStart แล้วค่อยไล่ลงเป็นศูนย์
        // ใช้ textMesh.alpha ไม่ใช่ .color เพราะ .color เขียนทับ vertex color ทั้งชุด
        // ทุกเฟรม ส่วน alpha เป็นช่องที่ TMP เตรียมไว้ให้คุมความโปร่งอย่างเดียว
        float fade = _fadeStart >= 1f
            ? 1f
            : Mathf.Clamp01((t - _fadeStart) / (1f - _fadeStart));
        textMesh.alpha = 1f - fade;
    }

    void LateUpdate()
    {
        // cache Camera.main — รูปแบบเดียวกับ WorldHPBar
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        transform.LookAt(
            transform.position + _cam.transform.rotation * Vector3.forward,
            _cam.transform.rotation * Vector3.up
        );
    }
}
