using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralized VFX Object Pool + Projectile Prefab Registry
///
/// VFX Pool:
///   — แต่ละ client pre-allocate pool ของตัวเอง (local Instantiate)
///   — เล่น VFX จาก pool แทน Instantiate/Destroy → ไม่มี GC spike
///   — broadcast ผ่าน PlayerWeaponManager.RequestVfxServerRpc → PlayVfxClientRpc
///
/// Projectile Registry:
///   — Server ใช้ GetProjectilePrefab(id) เพื่อ Spawn projectile NetworkObject
///   — ไม่ pool เพราะ Projectile เป็น NetworkObject (NGO จัดการ replication เอง)
///
/// Setup:
///   1. วาง NetworkedVFXPool GameObject ในทุก gameplay scene
///   2. ลาก VFX prefabs ทั้งหมดใส่ vfxEntries (index = ID ที่ใช้ใน RPC)
///   3. ลาก Projectile NetworkObject prefabs ทั้งหมดใส่ projectilePrefabs
///   4. WeaponData.hitVfxPrefab และ WeaponData.projectilePrefab
///      ต้องอยู่ใน list นี้ด้วยเพื่อให้ระบบหา ID ได้
/// </summary>
public class NetworkedVFXPool : MonoBehaviour
{
    [System.Serializable]
    public class VFXEntry
    {
        [Tooltip("VFX prefab — ต้องตรงกับที่ WeaponData ใช้")]
        public GameObject prefab;
        [Min(1), Tooltip("จำนวน pre-allocate ต่อ client\nปรับเพิ่มถ้า VFX overlap กันเยอะ")]
        public int poolSize = 5;
    }

    public static NetworkedVFXPool Instance { get; private set; }

    // ─── VFX Registry & Pool ────────────────────────────────────────────
    [Header("VFX Registry & Pool")]
    [Tooltip("ลาก VFX prefabs ทั้งหมด — index = ID ที่ใช้ใน RPC")]
    public List<VFXEntry> vfxEntries = new();

    // ─── Projectile Registry ────────────────────────────────────────────
    [Header("Projectile Registry")]
    [Tooltip("ลาก Projectile NetworkObject prefabs ทั้งหมด\n" +
             "Server ใช้ GetProjectilePrefab(id) สำหรับ Spawn\n" +
             "index = ID ที่ WeaponBase.FireProjectile ส่งไปใน ServerRpc")]
    public List<GameObject> projectilePrefabs = new();

    // ─── Runtime ────────────────────────────────────────────────────────
    private readonly Dictionary<GameObject, int>           _vfxToId  = new();
    private readonly Dictionary<int, Queue<GameObject>>    _pools    = new();
    private readonly Dictionary<GameObject, int>           _projToId = new();

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
        _vfxToId.Clear();
        _pools.Clear();
        _projToId.Clear();

        for (int i = 0; i < vfxEntries.Count; i++)
        {
            var e = vfxEntries[i];
            if (e?.prefab == null) continue;

            _vfxToId[e.prefab] = i;
            var q = new Queue<GameObject>(e.poolSize);
            for (int j = 0; j < e.poolSize; j++)
                q.Enqueue(CreateInstance(e.prefab));
            _pools[i] = q;
        }

        for (int i = 0; i < projectilePrefabs.Count; i++)
            if (projectilePrefabs[i] != null)
                _projToId[projectilePrefabs[i]] = i;

        Debug.Log($"[VFXPool] Built {_pools.Count} VFX pools, {_projToId.Count} projectile entries");
    }

    GameObject CreateInstance(GameObject prefab)
    {
        var go = Instantiate(prefab);
        go.SetActive(false);
        return go;
    }

    // ─── VFX API ─────────────────────────────────────────────────────────
    /// <summary>คืน ID ของ VFX prefab (-1 = ไม่อยู่ใน pool)</summary>
    public int GetVfxId(GameObject prefab)
        => prefab != null && _vfxToId.TryGetValue(prefab, out int id) ? id : -1;

    /// <summary>
    /// เล่น VFX จาก pool บน client ที่เรียก (ถูกเรียกจาก ClientRpc ใน PlayerWeaponManager)
    /// </summary>
    public void PlayFromPool(int vfxId, Vector3 pos, float scale = 1f)
    {
        if (vfxId < 0 || vfxId >= vfxEntries.Count) return;
        if (!_pools.TryGetValue(vfxId, out var q)) return;

        GameObject go;
        if (q.Count > 0)
        {
            go = q.Dequeue();
        }
        else
        {
            // pool หมด → ขยาย pool โดยสร้างใหม่ 1 ชิ้น
            go = CreateInstance(vfxEntries[vfxId].prefab);
            Debug.LogWarning($"[VFXPool] Pool exhausted id={vfxId} ('{vfxEntries[vfxId].prefab.name}'), growing");
        }

        go.transform.position   = pos;
        go.transform.rotation   = Quaternion.identity;
        go.transform.localScale = Vector3.one * scale;
        go.SetActive(true);

        // Reset + play ทุก ParticleSystem (รวม children)
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
        {
            ps.Clear();
            ps.Play();
        }

        StartCoroutine(ReturnToPool(go, vfxId, CalcTTL(go)));
    }

    float CalcTTL(GameObject go)
    {
        float maxTTL = 0f;
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
        {
            float t = ps.main.duration + ps.main.startLifetime.constantMax;
            if (t > maxTTL) maxTTL = t;
        }
        return maxTTL > 0f ? maxTTL + 0.1f : 3f;
    }

    IEnumerator ReturnToPool(GameObject go, int vfxId, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (go == null) yield break;
        go.SetActive(false);
        if (_pools.TryGetValue(vfxId, out var q)) q.Enqueue(go);
    }

    // ─── Projectile Registry API ─────────────────────────────────────────
    /// <summary>คืน ID ของ projectile prefab (-1 = ไม่อยู่ใน registry)</summary>
    public int GetProjectileId(GameObject prefab)
        => prefab != null && _projToId.TryGetValue(prefab, out int id) ? id : -1;

    /// <summary>คืน projectile prefab จาก ID (null = ไม่พบ → ใช้ default)</summary>
    public GameObject GetProjectilePrefab(int id)
        => id >= 0 && id < projectilePrefabs.Count ? projectilePrefabs[id] : null;
}
