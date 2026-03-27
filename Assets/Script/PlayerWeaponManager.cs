using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// จัดการ Weapon Slots (max 6) บน Player Prefab
/// — Spawn/Destroy weapon GameObjects เป็น children
/// — ServerRpc สำหรับ fire projectile / melee
/// — WeaponBase อ่าน IsOwner, playerMove, statManager จาก class นี้
/// </summary>
public class PlayerWeaponManager : NetworkBehaviour
{
    [Header("References")]
    public GameObject        projectilePrefab;
    [HideInInspector] public playermove         playerMove;
    [HideInInspector] public PlayerStatManager  statManager;

    public const int MaxWeaponSlots = 6;

    [Header("Starting Character")]
    [Tooltip("ตั้งค่าโดย CharacterSelectUI ก่อนเริ่มเกม — ถ้าว่างจะใช้ startingWeapon fallback")]
    public CharacterData characterData;
    [Tooltip("Fallback: weapon เริ่มต้นถ้าไม่ได้เลือก character")]
    public WeaponData    startingWeapon;

    // ── Internal slot data ────────────────────────────────────────────────
    private class WeaponSlot
    {
        public WeaponData data;
        public int        level;   // 0-indexed
        public WeaponBase script;
        public GameObject go;
    }

    private List<WeaponSlot> slots = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (playerMove  == null) playerMove  = GetComponent<playermove>();
        if (statManager == null) statManager = GetComponent<PlayerStatManager>();
        if (IsOwner)
        {
            // CharacterData: use manually assigned → then selection UI → then fallback
            var cd = characterData ?? CharacterSelectUI.SelectedCharacter;
            if (cd != null)
            {
                if (playerMove != null)
                    playerMove.SetBaseStats(cd.baseHealth, cd.baseMoveSpeed);
                var startWep = cd.startingWeapon ?? startingWeapon;
                if (startWep != null) AddWeapon(startWep);

                // Kit weapons เพิ่มเติม (เช่น Riven: Valor + Blade of Exile)
                if (cd.additionalWeapons != null)
                    foreach (var w in cd.additionalWeapons)
                        if (w != null) AddWeapon(w);
            }
            else if (startingWeapon != null)
            {
                AddWeapon(startingWeapon);
            }
        }
    }

    // ── Public API ────────────────────────────────────────────────────────
    /// <summary>เพิ่ม weapon ใหม่ — คืน false ถ้า slot เต็มหรือมีอยู่แล้ว</summary>
    public bool AddWeapon(WeaponData data)
    {
        if (data == null || slots.Count >= MaxWeaponSlots || HasWeapon(data)) return false;
        SpawnWeapon(data, 0);
        return true;
    }

    /// <summary>อัพเกรด weapon ที่มีอยู่ Lv+1 — คืน false ถ้าไม่พบหรือ max lv</summary>
    public bool UpgradeWeapon(WeaponData data)
    {
        var slot = slots.Find(s => s.data == data);
        if (slot == null) return false;
        int next = slot.level + 1;
        if (next >= data.levels.Length) return false;
        slot.level = next;
        slot.script?.SetLevel(next);
        Debug.Log($"[WeaponManager] ⬆ {data.weaponName} Lv{next + 1}");
        return true;
    }

    /// <summary>แทน weapon เดิมด้วย Super/Fusion version</summary>
    public void ReplaceWeapon(WeaponData oldData, WeaponData newData)
    {
        RemoveWeapon(oldData);
        SpawnWeapon(newData, 0);
        Debug.Log($"[WeaponManager] ✨ {oldData.weaponName} → {newData.weaponName}");
    }

    /// <summary>Fusion: ลบ 2 Super แล้วเพิ่ม Fusion weapon</summary>
    public void FuseWeapons(WeaponData superA, WeaponData superB, WeaponData result)
    {
        RemoveWeapon(superA);
        RemoveWeapon(superB);
        SpawnWeapon(result, 0);
        Debug.Log($"[WeaponManager] 🔥 {superA.weaponName} + {superB.weaponName} → {result.weaponName}");
    }

    // ── Queries ───────────────────────────────────────────────────────────
    public bool HasWeapon(WeaponData data) => slots.Exists(s => s.data == data);
    public bool HasFreeSlot()              => slots.Count < MaxWeaponSlots;

    /// <summary>คืน level 0-indexed, -1 ถ้าไม่มี</summary>
    public int GetWeaponLevel(WeaponData data)
    {
        var slot = slots.Find(s => s.data == data);
        return slot != null ? slot.level : -1;
    }

    public List<WeaponData> GetEquippedWeapons()
    {
        var list = new List<WeaponData>();
        foreach (var s in slots) list.Add(s.data);
        return list;
    }

    // ── Internal ──────────────────────────────────────────────────────────
    void SpawnWeapon(WeaponData data, int level)
    {
        var slot = new WeaponSlot { data = data, level = level };

        if (data.prefab != null)
        {
            slot.go     = Instantiate(data.prefab, transform);
            slot.script = slot.go.GetComponent<WeaponBase>();
            slot.script?.Init(data, level, this);
        }
        else
        {
            Debug.LogWarning($"[WeaponManager] {data.weaponName}: ไม่มี prefab");
        }

        slots.Add(slot);
        Debug.Log($"[WeaponManager] ➕ {data.weaponName} Lv{level + 1}");
    }

    void RemoveWeapon(WeaponData data)
    {
        var slot = slots.Find(s => s.data == data);
        if (slot == null) return;
        if (slot.go != null) Destroy(slot.go);
        slots.Remove(slot);
    }

    [Header("Weapon Prefabs")]
    public GameObject grenadePrefab;    // GrenadeProjectile NetworkObject
    public GameObject minePrefab;       // MineObject NetworkObject

    // ── ServerRpc: Projectile ─────────────────────────────────────────────
    [ServerRpc(RequireOwnership = false)]
    public void FireProjectileServerRpc(
        Vector3 spawnPos, Vector3 baseDir,
        float damage, float projSpeed, int count, float spreadDeg,
        bool piercing = false)
    {
        if (projectilePrefab == null) return;

        baseDir.y = 0f;
        if (baseDir.sqrMagnitude < 0.001f) baseDir = Vector3.forward;
        baseDir = baseDir.normalized;
        count   = Mathf.Max(1, count);

        for (int i = 0; i < count; i++)
        {
            float   angle = (i - (count - 1) * 0.5f) * spreadDeg;
            Vector3 dir   = Quaternion.Euler(0f, angle, 0f) * baseDir;

            var proj = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(dir));
            var p    = proj.GetComponent<Projectile>();
            if (p == null) { Destroy(proj); continue; }
            p.damage   = damage;
            p.speed    = projSpeed;
            p.piercing = piercing;
            p.InitDirection(dir);
            proj.GetComponent<NetworkObject>()?.Spawn(true);
        }
    }

    // ── ServerRpc: Melee AoE ──────────────────────────────────────────────
    [ServerRpc(RequireOwnership = false)]
    public void FireMeleeServerRpc(Vector3 center, float radius, float damage)
    {
        foreach (var c in OverlapEnemy(center, radius))
            c.GetComponent<Enemy>()?.EnemyTakeDamage(damage);
    }

    // ── Helper: หาศัตรูในรัศมี (Layer + Tag fallback) ─────────────────────
    public static Collider[] OverlapEnemy(Vector3 center, float radius)
    {
        int mask = LayerMask.GetMask("Enemy");
        // ถ้าไม่มี Layer "Enemy" → scan ทุก layer แล้วกรองด้วย Tag
        if (mask == 0)
        {
            var all     = Physics.OverlapSphere(center, radius);
            var enemies = new System.Collections.Generic.List<Collider>();
            foreach (var c in all)
                if (c.CompareTag("Enemy")) enemies.Add(c);
            return enemies.ToArray();
        }
        return Physics.OverlapSphere(center, radius, mask);
    }

    // ── ServerRpc: Grenade (throw → AoE on land) ──────────────────────────
    [ServerRpc(RequireOwnership = false)]
    public void ThrowGrenadeServerRpc(
        Vector3 spawnPos, Vector3 targetPos,
        float damage, float radius,
        float fuseTime = 1.5f, bool cluster = false)
    {
        if (grenadePrefab == null) return;
        var go = Instantiate(grenadePrefab, spawnPos, Quaternion.identity);
        var gp = go.GetComponent<GrenadeProjectile>();
        if (gp != null)
        {
            gp.damage    = damage;
            gp.radius    = radius;
            gp.fuseTime  = fuseTime;
            gp.cluster   = cluster;
            gp.targetPos = targetPos;
            // อ้างอิง manager เพื่อให้ Cluster bomb เรียก FireProjectileServerRpc ได้
            gp.weaponManager = this;
        }
        go.GetComponent<NetworkObject>()?.Spawn(true);
    }

    // ── ServerRpc: Raycast Pierce (Railgun / PlasmaWhip) ──────────────────
    [ServerRpc(RequireOwnership = false)]
    public void FireRaycastServerRpc(Vector3 origin, Vector3 direction, float damage, float maxDist = 50f)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;
        direction = direction.normalized;

        var mask = LayerMask.GetMask("Enemy");
        var hits = Physics.RaycastAll(origin, direction, maxDist, mask);
        foreach (var h in hits)
            h.collider.GetComponent<Enemy>()?.EnemyTakeDamage(damage);

        // Notify all clients to show beam VFX
        ShowRaycastVfxClientRpc(origin, origin + direction * maxDist);
    }

    [ClientRpc]
    void ShowRaycastVfxClientRpc(Vector3 from, Vector3 to)
    {
        // TODO: ใส่ LineRenderer / VFX effect ที่นี่
        Debug.DrawLine(from, to, Color.cyan, 0.2f);
    }

    // ── ServerRpc: Drop Mine ──────────────────────────────────────────────
    [ServerRpc(RequireOwnership = false)]
    public void DropMineServerRpc(Vector3 position, float damage, float triggerRadius)
    {
        if (minePrefab == null) return;
        var go = Instantiate(minePrefab, position, Quaternion.identity);
        var m  = go.GetComponent<MineObject>();
        if (m != null) { m.damage = damage; m.triggerRadius = triggerRadius; }
        go.GetComponent<NetworkObject>()?.Spawn(true);
    }

    // ── ServerRpc: Add Shield ─────────────────────────────────────────────
    [ServerRpc(RequireOwnership = false)]
    public void AddShieldServerRpc(float amount)
    {
        playerMove?.AddShield(amount);
    }
}
