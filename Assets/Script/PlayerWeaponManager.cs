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

    /// <summary>ชื่อ scene ของ Menu/Lobby — ใช้ block weapon firing นอก gameplay</summary>
    public const string MenuSceneName = "MenuScene";

    /// <summary>
    /// true เมื่ออยู่ใน gameplay scene (ไม่ใช่ MenuScene) — WeaponBase/AbilityBase ใช้
    /// guard auto-fire ตอนผู้เล่นยังอยู่ใน Online Session (lobby) ก่อนเริ่มเกมจริง
    /// </summary>
    public static bool WeaponsEnabledInScene =>
        UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != MenuSceneName;

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
    private readonly Dictionary<string, GameObject> _activeLoopVfxs = new();
    private readonly Dictionary<string, float> _serverWeaponDamages = new();

    public void RegisterWeaponDamage(string weaponName, float damage)
    {
        if (!IsServer) return;
        if (string.IsNullOrEmpty(weaponName)) weaponName = "Unknown";
        if (!_serverWeaponDamages.ContainsKey(weaponName))
            _serverWeaponDamages[weaponName] = 0f;
        _serverWeaponDamages[weaponName] += damage;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (playerMove  == null) playerMove  = GetComponent<playermove>();
        if (statManager == null) statManager = GetComponent<PlayerStatManager>();

        if (!IsOwner) return;

        // CharacterData: use manually assigned → then selection UI → then fallback
        var cd = characterData ?? CharacterSelectUI.SelectedCharacter;
        if (cd != null)
        {
            if (playerMove != null)
                playerMove.SetBaseStats(cd.baseHealth, cd.baseMoveSpeed);

            // Starting weapon (ปกติ — นับ weapon slot)
            var startWep = cd.startingWeapon ?? startingWeapon;
            if (startWep != null) AddWeapon(startWep);

            // Passive weapons (ไม่นับ weapon slot — ไม่แสดงใน Weapon UI)
            if (cd.passiveWeapons != null)
                foreach (var w in cd.passiveWeapons)
                    if (w != null) SpawnPassiveWeapon(w);

            // Abilities (Q/E/R) — ส่งให้ PlayerAbilityManager จัดการแยก
            var abilityMgr = GetComponent<PlayerAbilityManager>();
            abilityMgr?.InitAbilities(cd);
        }
        else if (startingWeapon != null)
        {
            AddWeapon(startingWeapon);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────
    /// <summary>เพิ่ม weapon ใหม่ — คืน false ถ้า slot เต็มหรือมีอยู่แล้ว
    /// Ability ใช้ PlayerAbilityManager.AddAbility(AbilityData) แทน</summary>
    public bool AddWeapon(WeaponData data)
    {
        if (data == null || HasWeapon(data) || slots.Count >= MaxWeaponSlots) return false;
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

        var oldW = oldData.prefab != null ? oldData.prefab.GetComponent<WeaponBase>() : null;
        if (oldW != null && !string.IsNullOrEmpty(oldW.weaponVfxType) && oldW.weaponVfxType != "None")
        {
            StopLoopVfxServerRpc(oldW.weaponVfxType);
        }
    }

    /// <summary>Fusion: ลบ 2 Super แล้วเพิ่ม Fusion weapon</summary>
    public void FuseWeapons(WeaponData superA, WeaponData superB, WeaponData result)
    {
        RemoveWeapon(superA);
        RemoveWeapon(superB);
        SpawnWeapon(result, 0);
        Debug.Log($"[WeaponManager] 🔥 {superA.weaponName} + {superB.weaponName} → {result.weaponName}");

        var oldA = superA.prefab != null ? superA.prefab.GetComponent<WeaponBase>() : null;
        if (oldA != null && !string.IsNullOrEmpty(oldA.weaponVfxType) && oldA.weaponVfxType != "None")
        {
            StopLoopVfxServerRpc(oldA.weaponVfxType);
        }

        var oldB = superB.prefab != null ? superB.prefab.GetComponent<WeaponBase>() : null;
        if (oldB != null && !string.IsNullOrEmpty(oldB.weaponVfxType) && oldB.weaponVfxType != "None")
        {
            StopLoopVfxServerRpc(oldB.weaponVfxType);
        }
    }

    // ── Queries ───────────────────────────────────────────────────────────
    public bool HasWeapon(WeaponData data) => slots.Exists(s => s.data == data);
    /// <summary>ยังมี weapon slot ว่าง (abilities ไม่นับ — อยู่ใน PlayerAbilityManager แล้ว)</summary>
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

    /// <summary>คืน WeaponBase script ทุกตัวที่ equipped — ใช้โดย HunterPassive</summary>
    public List<WeaponBase> GetAllWeaponScripts()
    {
        var list = new List<WeaponBase>();
        foreach (var s in slots)
            if (s.script != null) list.Add(s.script);
        return list;
    }

    /// <summary>Force-fire ทุก weapon ทันที ไม่สน cooldown — Hunter Passive trigger</summary>
    public void ForceFireAllWeapons()
    {
        foreach (var s in slots)
            s.script?.ExecuteFire();
    }

    // ── Internal ──────────────────────────────────────────────────────────

    /// <summary>
    /// Spawn passive weapon script เป็น child ของ player
    /// — ไม่นับ Weapon Slot, ไม่แสดงใน Weapon UI
    /// — ใช้สำหรับ HunterPassiveWeapon, GunnerPassiveWeapon ฯลฯ
    /// </summary>
    void SpawnPassiveWeapon(WeaponData data)
    {
        if (data?.prefab == null)
        {
            Debug.LogWarning($"[WeaponManager] Passive '{data?.weaponName}': ไม่มี prefab");
            return;
        }
        var go     = Instantiate(data.prefab, transform);
        var script = go.GetComponent<WeaponBase>();
        script?.Init(data, 0, this);
        Debug.Log($"[WeaponManager] 🔹 Passive '{data.weaponName}' spawned (no slot)");
    }

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
    public GameObject boomerangPrefab;   // BoomerangProjectile NetworkObject
    public GameObject grenadePrefab;       // GrenadeProjectile NetworkObject
    public GameObject minePrefab;          // MineObject NetworkObject
    public GameObject stickyRocketPrefab;  // StickyRocketProjectile NetworkObject
    public GameObject giantRocketPrefab;   // GiantRocketProjectile NetworkObject
    public GameObject missilePrefab;       // MissileProjectile NetworkObject
    public GameObject funnelPrefab;        // FunnelObject NetworkObject

    // ── ServerRpc: Boomerang ──────────────────────────────────────────────
    [ServerRpc(RequireOwnership = false)]
    public void SpawnBoomerangServerRpc(
        Vector3 spawnPos, Vector3 direction,
        float damage, float speed, float maxRange, bool isCrit = false, string weaponName = "Unknown")
    {
        if (boomerangPrefab == null)
        {
            Debug.LogError("[PlayerWeaponManager] boomerangPrefab ไม่ได้ assign — ลาก Proj_Boomerang.prefab ใส่ Inspector");
            return;
        }
        Quaternion rot = (direction != Vector3.zero ? Quaternion.LookRotation(direction) : Quaternion.identity)
                       * boomerangPrefab.transform.localRotation;   // คง prefab offset ไว้ (เหมือน StickyRocket)
        var go   = Instantiate(boomerangPrefab, spawnPos, rot);
        go.transform.localScale = boomerangPrefab.transform.localScale;
        var proj = go.GetComponent<BoomerangProjectile>();
        var no   = go.GetComponent<NetworkObject>();
        if (proj == null || no == null) { Destroy(go); return; }
        proj.damage        = damage;
        proj.speed         = speed;
        proj.maxRange      = maxRange;
        proj.ownerClientId = OwnerClientId;
        proj.isCrit        = isCrit;
        proj.weaponName    = weaponName;
        no.Spawn(true);
        proj.Init(direction);
    }

    // ── ServerRpc: Projectile ─────────────────────────────────────────────
    [ServerRpc(RequireOwnership = false)]
    public void FireProjectileServerRpc(
        Vector3 spawnPos, Vector3 baseDir,
        float damage, float projSpeed, int count, float spreadDeg,
        bool piercing = false, int projPrefabId = -1, float maxRange = -1f,
        bool isCrit = false, string weaponName = "Unknown")
    {
        // หา prefab จาก NetworkedVFXPool registry (ทุก client มีข้อมูลเดียวกัน)
        // fallback → projectilePrefab default บน manager
        GameObject prefab = NetworkedVFXPool.Instance?.GetProjectilePrefab(projPrefabId)
                            ?? projectilePrefab;
        if (prefab == null) return;

        baseDir.y = 0f;
        if (baseDir.sqrMagnitude < 0.001f) baseDir = Vector3.forward;
        baseDir = baseDir.normalized;
        count   = Mathf.Max(1, count);

        for (int i = 0; i < count; i++)
        {
            float   angle = (i - (count - 1) * 0.5f) * spreadDeg;
            Vector3 dir   = Quaternion.Euler(0f, angle, 0f) * baseDir;

            var proj = Instantiate(prefab, spawnPos, Quaternion.LookRotation(dir));
            var p    = proj.GetComponent<Projectile>();
            if (p == null) { Destroy(proj); continue; }
            p.damage   = damage;
            p.speed    = projSpeed;
            p.piercing = piercing;
            p.isCrit   = isCrit;
            p.weaponName = weaponName;
            p.ownerManager = this;
            if (maxRange > 0f) p.maxRange = maxRange;   // -1 = ใช้ค่าบน prefab
            p.InitDirection(dir);

            var netObj = proj.GetComponent<NetworkObject>();
            if (netObj == null)
                Debug.LogWarning($"[Projectile] '{prefab.name}' ไม่มี NetworkObject component — projectile จะ spawn บน Server เท่านั้น ไม่ถูก replicate ไปยัง clients!");
            else
                netObj.Spawn(true);
        }
    }

    // ── ServerRpc: Melee AoE ──────────────────────────────────────────────
    [ServerRpc(RequireOwnership = false)]
    public void FireMeleeServerRpc(Vector3 center, float radius, float damage, bool isCrit = false, string weaponName = "Unknown", float knockbackForce = 0f, Vector3 knockbackDir = default)
    {
        foreach (var c in OverlapEnemy(center, radius))
        {
            var enemy = c.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.EnemyTakeDamage(damage, isCrit);
                RegisterWeaponDamage(weaponName, damage);

                if (knockbackForce > 0f)
                {
                    Vector3 dir = knockbackDir != Vector3.zero ? knockbackDir : (enemy.transform.position - center);
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.001f)
                    {
                        enemy.transform.position += dir.normalized * knockbackForce;
                    }
                }
            }
        }
    }

    /// <summary>Melee AoE แบบ arc — เฉพาะ enemy ที่อยู่ใน cone ทิศ forward</summary>
    [ServerRpc(RequireOwnership = false)]
    public void FireArcMeleeServerRpc(Vector3 center, Vector3 forward, float radius, float arcAngle, float damage, bool isCrit = false, string weaponName = "Unknown", float knockbackForce = 0f, Vector3 knockbackDir = default)
    {
        float halfArc = arcAngle * 0.5f;
        foreach (var c in OverlapEnemy(center, radius))
        {
            Vector3 toEnemy = c.transform.position - center;
            toEnemy.y = 0f;
            if (toEnemy.sqrMagnitude < 0.001f || Vector3.Angle(forward, toEnemy) <= halfArc)
            {
                var enemy = c.GetComponent<Enemy>();
                if (enemy != null)
                {
                    enemy.EnemyTakeDamage(damage, isCrit);
                    RegisterWeaponDamage(weaponName, damage);

                    if (knockbackForce > 0f)
                    {
                        Vector3 dir = knockbackDir != Vector3.zero ? knockbackDir : forward;
                        dir.y = 0f;
                        if (dir.sqrMagnitude > 0.001f)
                        {
                            enemy.transform.position += dir.normalized * knockbackForce;
                        }
                    }
                }
            }
        }
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
        float fuseTime = 1.5f, bool cluster = false, string weaponName = "Unknown")
    {
        if (grenadePrefab == null)
        {
            Debug.LogError("[PlayerWeaponManager] grenadePrefab ไม่ได้ assign — ต้องการ NetworkObject prefab ที่มี GrenadeProjectile.cs");
            return;
        }
        var go = Instantiate(grenadePrefab, spawnPos, Quaternion.identity);
        var gp = go.GetComponent<GrenadeProjectile>();
        if (gp != null)
        {
            gp.damage    = damage;
            gp.radius    = radius;
            gp.fuseTime  = fuseTime;
            gp.cluster   = cluster;
            gp.targetPos = targetPos;
            gp.weaponName = weaponName;
            // อ้างอิง manager เพื่อให้ Cluster bomb เรียก FireProjectileServerRpc ได้
            gp.weaponManager = this;
        }
        go.GetComponent<NetworkObject>()?.Spawn(true);
    }

    // ── ServerRpc: Line AoE Box (LaserWeapon) ────────────────────────────
    /// <summary>
    /// AoE เส้นตรงแบบมีความกว้าง — ใช้ Physics.OverlapBox ตามแนวยิง
    /// damage enemy ทุกตัวในกล่องสี่เหลี่ยม (width × height × range)
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void FireLineAoEServerRpc(
        Vector3 origin, Vector3 direction,
        float damage, float range, float width = 1.5f, bool isCrit = false,
        float knockbackForce = 0f, string vfxKey = "None", string weaponName = "Unknown")
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;
        direction = direction.normalized;

        // Box center = origin + dir*(range/2) เพื่อให้ box เริ่มจาก origin
        Vector3    center      = origin + direction * (range * 0.5f);
        Vector3    halfExtents = new Vector3(width * 0.5f, 1.2f, range * 0.5f);
        Quaternion rotation    = Quaternion.LookRotation(direction);

        int mask = LayerMask.GetMask("Enemy");

        Collider[] cols;
        if (mask == 0)
        {
            // Enemy layer ไม่ได้ตั้งค่า → scan ทุก layer แล้วกรองด้วย Tag
            var all = Physics.OverlapBox(center, halfExtents, rotation);
            var list = new System.Collections.Generic.List<Collider>();
            foreach (var c in all)
                if (c.CompareTag("Enemy")) list.Add(c);
            cols = list.ToArray();
        }
        else
        {
            cols = Physics.OverlapBox(center, halfExtents, rotation, mask);
        }

        // ป้องกัน hit ซ้ำ (collider หลายอันบน enemy เดียวกัน)
        var seen = new System.Collections.Generic.HashSet<int>();
        foreach (var c in cols)
        {
            int id = c.gameObject.GetInstanceID();
            if (!seen.Add(id)) continue;
            // Enemy.NotifyHitClientRpc spawn HitEffect/CritHitEffect ที่ตัว enemy เอง
            var enemy = c.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.EnemyTakeDamage(damage, isCrit);
                RegisterWeaponDamage(weaponName, damage);
            }
            // Knockback (server-authoritative push along line direction)
            if (knockbackForce > 0f)
            {
                Vector3 push = direction * knockbackForce;
                c.transform.position += push;
            }
        }

        ShowLineAoEVfxClientRpc(origin, origin + direction * range, vfxKey);
    }

    [ClientRpc]
    void ShowLineAoEVfxClientRpc(Vector3 from, Vector3 to, string vfxKey)
    {
        if (string.IsNullOrEmpty(vfxKey) || vfxKey == "None") return;

        if (NetworkedVFXPool.Instance != null && NetworkedVFXPool.Instance.HasMapping(vfxKey))
        {
            VFXFactory.PlayBeam(vfxKey, "None", from, to, duration: 0.18f);
        }
        else
        {
            VFXFactory.PlayBeam("Default", "None", from, to, duration: 0.18f);
        }
    }

    // ── ServerRpc: Raycast Pierce (Railgun / PlasmaWhip / WindSlash) ────────
    [ServerRpc(RequireOwnership = false)]
    public void FireRaycastServerRpc(Vector3 origin, Vector3 direction, float damage,
                                     float maxDist = 50f, string vfxKey = "None",
                                     bool isCrit = false, bool playHitVfx = true, string weaponName = "Unknown",
                                     float thickness = 0f)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;
        direction = direction.normalized;

        var mask = LayerMask.GetMask("Enemy");
        RaycastHit[] hits;
        if (thickness > 0f)
        {
            hits = Physics.SphereCastAll(origin, thickness * 0.5f, direction, maxDist, mask);
        }
        else
        {
            hits = Physics.RaycastAll(origin, direction, maxDist, mask);
        }

        foreach (var h in hits)
        {
            var enemy = h.collider.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.EnemyTakeDamage(damage, isCrit);
                RegisterWeaponDamage(weaponName, damage);
            }
        }

        ShowRaycastVfxClientRpc(origin, origin + direction * maxDist, vfxKey);
    }

    [ClientRpc]
    void ShowRaycastVfxClientRpc(Vector3 from, Vector3 to, string vfxKey)
    {
        if (NetworkedVFXPool.Instance != null && NetworkedVFXPool.Instance.HasMapping(vfxKey))
        {
            VFXFactory.PlayBeam(vfxKey, "None", from, to, duration: 0.12f);
        }
        else
        {
            VFXFactory.PlayBeam("Default", vfxKey, from, to, duration: 0.12f);
        }
    }

    // ── ServerRpc: Drop Mine ──────────────────────────────────────────────
    [ServerRpc(RequireOwnership = false)]
    public void DropMineServerRpc(Vector3 position, float damage, float triggerRadius, string weaponName = "Unknown")
    {
        if (minePrefab == null) return;
        var go = Instantiate(minePrefab, position, Quaternion.identity);
        var m  = go.GetComponent<MineObject>();
        if (m != null) 
        { 
            m.damage = damage; 
            m.triggerRadius = triggerRadius; 
            m.weaponName = weaponName;
            m.weaponManager = this;
        }
        go.GetComponent<NetworkObject>()?.Spawn(true);
    }

    // ── ServerRpc: Hunter Missiles (Q) ───────────────────────────────────
    [ServerRpc(RequireOwnership = false)]
    public void SpawnMissilesServerRpc(
        Vector3[] spawnPositions, ulong[] targetNetIds,
        float damage, float explosionRadius, string weaponName = "Unknown")
    {
        if (missilePrefab == null) return;
        int count = Mathf.Min(spawnPositions.Length, targetNetIds.Length);
        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(missilePrefab, spawnPositions[i], missilePrefab.transform.localRotation);
            go.transform.localScale = missilePrefab.transform.localScale;
            var mp = go.GetComponent<MissileProjectile>();
            if (mp != null)
            {
                mp.Init(targetNetIds[i], damage, explosionRadius);
                mp.weaponName = weaponName;
                mp.weaponManager = this;
            }
            go.GetComponent<NetworkObject>()?.Spawn(true);
        }
    }

    // ── ServerRpc: Hunter Funnels (Ultimate) ──────────────────────────────
    [ServerRpc(RequireOwnership = false)]
    public void SpawnFunnelsServerRpc(
        Vector3 center, int count, float orbitRadius,
        float laserDamage, float laserCooldown,
        float attackRange, float lifetime, ulong ownerClientId,
        int beamCount = 1, string weaponName = "Unknown")
    {
        if (funnelPrefab == null) return;
        for (int i = 0; i < count; i++)
        {
            float   angle    = i * (360f / count);
            Vector3 offset   = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * orbitRadius * 0.4f;
            var     go       = Instantiate(funnelPrefab, center + offset + Vector3.up * 1.5f, Quaternion.identity);
            go.GetComponent<FunnelObject>()?.Init(
                center, orbitRadius, laserDamage, laserCooldown, attackRange, lifetime, ownerClientId, this, weaponName, beamCount);
            go.GetComponent<NetworkObject>()?.Spawn(true);
        }
    }

    // ── ServerRpc: Add Shield ─────────────────────────────────────────────
    [ServerRpc(RequireOwnership = false)]
    public void AddShieldServerRpc(float amount)
    {
        playerMove?.AddShield(amount);
    }

    // ── ServerRpc: Sticky Rocket (Gunner Q mode) ──────────────────────────
    [ServerRpc(RequireOwnership = false)]
    public void SpawnStickyRocketServerRpc(
        Vector3 spawnPos, Vector3 direction,
        float damage, float speed, float explosionRadius, string weaponName = "Unknown")
    {
        if (stickyRocketPrefab == null) return;
        Quaternion stickyRot = Quaternion.LookRotation(direction) * stickyRocketPrefab.transform.localRotation;
        var go = Instantiate(stickyRocketPrefab, spawnPos, stickyRot);
        go.transform.localScale = stickyRocketPrefab.transform.localScale;
        var sr = go.GetComponent<StickyRocketProjectile>();
        if (sr != null)
        {
            sr.damage          = damage;
            sr.moveSpeed       = speed;
            sr.explosionRadius = explosionRadius;
            sr.weaponName      = weaponName;
            sr.weaponManager   = this;
            sr.InitDirection(direction);
        }
        go.GetComponent<NetworkObject>()?.Spawn(true);
    }

    // ── ServerRpc: Giant Rocket (Gunner E) ────────────────────────────────
    [ServerRpc(RequireOwnership = false)]
    public void SpawnGiantRocketServerRpc(
        Vector3 spawnPos, Vector3 direction,
        float baseDamage, float speed,
        float maxRange, float explosionRadius, string weaponName = "Unknown")
    {
        if (giantRocketPrefab == null) return;

        // direction มาจาก client แล้ว (horizontal, normalized)
        Vector3 dir = direction;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
        dir = dir.normalized;

        Quaternion giantRot = Quaternion.LookRotation(dir) * giantRocketPrefab.transform.localRotation;
        var go = Instantiate(giantRocketPrefab, spawnPos, giantRot);
        go.transform.localScale = giantRocketPrefab.transform.localScale;
        var gr = go.GetComponent<GiantRocketProjectile>();
        if (gr != null)
        {
            gr.baseDamage      = baseDamage;
            gr.moveSpeed       = speed;
            gr.maxRange        = maxRange;
            gr.explosionRadius = explosionRadius;
            gr.weaponName      = weaponName;
            gr.weaponManager   = this;
            gr.InitDirection(spawnPos, dir);
        }
        go.GetComponent<NetworkObject>()?.Spawn(true);
    }

    // ── VFX Broadcast by string key → NetworkedVFXPool ──────────────────────
    /// <summary>Weapon scripts ทุกตัวใช้ช่องทางนี้ผ่าน ShowHitVfx() หรือ BroadcastVfxTypeServerRpc โดยตรง</summary>
    [ServerRpc(RequireOwnership = false)]
    public void BroadcastVfxTypeServerRpc(Vector3 pos, string vfxKey, float scale = 1f, Vector3 direction = default, float arcAngle = 360f, float roll = 0f)
        => BroadcastVfxTypeClientRpc(pos, vfxKey, scale, direction, arcAngle, roll);

    [ClientRpc]
    void BroadcastVfxTypeClientRpc(Vector3 pos, string vfxKey, float scale = 1f, Vector3 direction = default, float arcAngle = 360f, float roll = 0f)
        => NetworkedVFXPool.Instance?.PlayByName(vfxKey, pos, scale, direction, arcAngle, roll);

    // ── VFX Broadcast parented to player ────────────────────────────────
    [ServerRpc(RequireOwnership = false)]
    public void BroadcastVfxParentedServerRpc(string vfxKey, float scale = 1f, bool isLoop = false)
        => BroadcastVfxParentedClientRpc(vfxKey, scale, isLoop);

    [ClientRpc]
    void BroadcastVfxParentedClientRpc(string vfxKey, float scale = 1f, bool isLoop = false)
    {
        if (NetworkedVFXPool.Instance != null)
        {
            if (isLoop)
            {
                if (_activeLoopVfxs.TryGetValue(vfxKey, out var activeGo) && activeGo != null)
                {
                    // Update scale of existing looping VFX
                    GameObject srcPrefab = NetworkedVFXPool.Instance.GetPrefabForKey(vfxKey);
                    Vector3 prefabScale = srcPrefab != null ? srcPrefab.transform.localScale : Vector3.one;
                    activeGo.transform.localScale = prefabScale * scale;
                    return;
                }

                GameObject go = NetworkedVFXPool.Instance.PlayParentedLoop(vfxKey, this.transform, scale);
                if (go != null)
                {
                    _activeLoopVfxs[vfxKey] = go;
                }
            }
            else
            {
                NetworkedVFXPool.Instance.PlayParented(vfxKey, this.transform, scale);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void StopLoopVfxServerRpc(string vfxKey)
        => StopLoopVfxClientRpc(vfxKey);

    [ClientRpc]
    void StopLoopVfxClientRpc(string vfxKey)
    {
        if (_activeLoopVfxs.TryGetValue(vfxKey, out var go))
        {
            if (go != null && NetworkedVFXPool.Instance != null)
            {
                NetworkedVFXPool.Instance.StopParented(vfxKey, go);
            }
            _activeLoopVfxs.Remove(vfxKey);
        }
    }

    // ── Beam VFX Broadcast (Lightning Chain, Thunder Rail ฯลฯ) ───────────
    /// <summary>วาด LineRenderer beam จาก from→to บนทุก client</summary>
    [ServerRpc(RequireOwnership = false)]
    public void BroadcastBeamServerRpc(Vector3 from, Vector3 to, string beamVfxKey, string hitVfxKey)
        => BroadcastBeamClientRpc(from, to, beamVfxKey, hitVfxKey);

    [ClientRpc]
    void BroadcastBeamClientRpc(Vector3 from, Vector3 to, string beamVfxKey, string hitVfxKey)
        => VFXFactory.PlayBeam(beamVfxKey, hitVfxKey, from, to, duration: 0.15f);

    // ── Orbiter Orb Sync — ตำแหน่ง orb สำหรับ client ที่ไม่ใช่ owner ────────
    private readonly List<GameObject> _remoteOrbVisuals = new();

    [ServerRpc(RequireOwnership = false)]
    public void SyncOrbPositionsServerRpc(Vector3[] positions)
        => SyncOrbPositionsClientRpc(positions);

    [ClientRpc]
    void SyncOrbPositionsClientRpc(Vector3[] positions)
    {
        if (IsOwner) return;   // owner จัดการ local orb เอง

        // ปรับจำนวน visual orb ให้ตรงกับ positions
        while (_remoteOrbVisuals.Count < positions.Length)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.localScale = Vector3.one * 0.4f;
            Destroy(go.GetComponent<Collider>());
            var rend = go.GetComponent<Renderer>();
            if (rend != null) rend.material.color = new Color(0.1f, 0.9f, 0.8f);  // teal
            _remoteOrbVisuals.Add(go);
        }
        while (_remoteOrbVisuals.Count > positions.Length)
        {
            int last = _remoteOrbVisuals.Count - 1;
            if (_remoteOrbVisuals[last]) Destroy(_remoteOrbVisuals[last]);
            _remoteOrbVisuals.RemoveAt(last);
        }

        for (int i = 0; i < positions.Length; i++)
            if (_remoteOrbVisuals[i]) _remoteOrbVisuals[i].transform.position = positions[i];
    }

    [ServerRpc(RequireOwnership = false)]
    public void HideRemoteOrbsServerRpc() => HideRemoteOrbsClientRpc();

    [ClientRpc]
    void HideRemoteOrbsClientRpc()
    {
        if (IsOwner) return;
        foreach (var o in _remoteOrbVisuals) if (o) Destroy(o);
        _remoteOrbVisuals.Clear();
    }

    /// <summary>
    /// Compile and sync final weapon stats to client. Called on Server.
    /// </summary>
    public void SendFinalStats(float finalTime, int finalLevel, bool isWin)
    {
        if (!IsServer) return;

        // Compile a single formatted string: "Name1:Damage1,Name2:Damage2"
        var statsList = new List<string>();
        foreach (var kv in _serverWeaponDamages)
        {
            statsList.Add($"{kv.Key}:{kv.Value}");
        }
        string serializedStats = string.Join(",", statsList);

        SendFinalWeaponStatsClientRpc(serializedStats, finalTime, finalLevel, isWin);
    }

    [ClientRpc]
    private void SendFinalWeaponStatsClientRpc(string serializedStats, float finalTime, int finalLevel, bool isWin)
    {
        if (!IsOwner) return;

        // Deserialize on the client
        var weaponNames = new List<string>();
        var damages = new List<float>();

        if (!string.IsNullOrEmpty(serializedStats))
        {
            string[] pairs = serializedStats.Split(',');
            foreach (string pair in pairs)
            {
                string[] parts = pair.Split(':');
                if (parts.Length == 2)
                {
                    weaponNames.Add(parts[0]);
                    if (float.TryParse(parts[1], out float dmg))
                    {
                        damages.Add(dmg);
                    }
                    else
                    {
                        damages.Add(0f);
                    }
                }
            }
        }

        // Trigger analytics submission
        if (AnalyticsManager.Instance != null)
        {
            AnalyticsManager.Instance.SendSessionEndAnalytics(weaponNames.ToArray(), damages.ToArray(), finalTime, finalLevel, isWin);
        }
    }

    // ── ServerRpc: Generic Chain and Raycast-Chain ────────────────────────
    [ServerRpc(RequireOwnership = false)]
    public void FireChainServerRpc(
        Vector3 startPos, float damage, float searchRadius, int chainTargets, float chainSearchRadius, float chainDamageMult,
        bool searchHighestHP, string weaponName, string beamVfx, string hitVfx,
        float zoneRadius = 0f, float zoneDamage = 0f, int zoneTicks = 0, float zoneTickInterval = 0f, string zoneVfx = "None", bool isCrit = false)
    {
        if (!IsServer) return;

        int mask = LayerMask.GetMask("Enemy");
        var hitSet = new HashSet<int>();

        Enemy first = searchHighestHP 
            ? FindHighestHPEnemyServerSide(startPos, searchRadius, mask, hitSet)
            : FindNearestUnhitEnemyServerSide(startPos, searchRadius, mask, hitSet);
        if (first == null) return;

        Vector3 prevPos = startPos;
        Enemy current = first;
        float curDmg = damage;

        var hitPositions = new List<Vector3>();

        for (int i = 0; i <= chainTargets; i++)
        {
            if (current == null) break;

            Vector3 targetPos = current.transform.position + Vector3.up * 0.8f;

            current.EnemyTakeDamage(curDmg, isCrit);
            RegisterWeaponDamage(weaponName, curDmg);
            BroadcastBeamClientRpc(prevPos, targetPos, beamVfx, hitVfx);

            hitPositions.Add(current.transform.position);
            hitSet.Add(current.GetInstanceID());
            prevPos = targetPos;
            curDmg *= chainDamageMult;

            current = FindNearestUnhitEnemyServerSide(targetPos, chainSearchRadius, mask, hitSet);
        }

        if (hitPositions.Count > 0 && zoneTicks > 0 && zoneRadius > 0f)
        {
            StartCoroutine(SpawnLightningZonesServerSide(hitPositions, zoneTicks, zoneTickInterval, zoneRadius, zoneDamage, isCrit, weaponName, zoneVfx));
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void FireRaycastChainServerRpc(
        Vector3 origin, Vector3 direction, float damage, float range, int beamCount,
        int chainTargets, float chainDamage, float chainRadius, bool isCrit, string weaponName, string beamVfx,
        float zoneRadius = 0f, float zoneDamage = 0f, int zoneTicks = 0, float zoneTickInterval = 0f, string zoneVfx = "None",
        float thickness = 0f)
    {
        if (!IsServer) return;

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) direction = transform.forward;
        direction = direction.normalized;

        float angleStep = 360f / Mathf.Max(1, beamCount);
        int mask = LayerMask.GetMask("Enemy");
        var hitPositions = new List<Vector3>();

        for (int i = 0; i < beamCount; i++)
        {
            Vector3 bDir = Quaternion.Euler(0f, i * angleStep, 0f) * direction;
            bDir = bDir.normalized;

            RaycastHit[] hits;
            if (thickness > 0f)
            {
                hits = Physics.SphereCastAll(origin, thickness * 0.5f, bDir, range, mask);
            }
            else
            {
                hits = Physics.RaycastAll(origin, bDir, range, mask);
            }
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            Vector3 endPoint = origin + bDir * range;

            BroadcastBeamClientRpc(origin, endPoint, beamVfx, "None");

            Enemy firstHitEnemy = null;

            foreach (var hit in hits)
            {
                var enemy = hit.collider.GetComponent<Enemy>();
                if (enemy == null) continue;

                enemy.EnemyTakeDamage(damage, isCrit);
                RegisterWeaponDamage(weaponName, damage);
                hitPositions.Add(enemy.transform.position);

                if (firstHitEnemy == null)
                {
                    firstHitEnemy = enemy;
                }
            }

            if (firstHitEnemy != null && chainTargets > 0)
            {
                Vector3 startChainPos = firstHitEnemy.transform.position + Vector3.up * 0.8f;
                FireLightningChainServerSide(startChainPos, firstHitEnemy, mask, hitPositions, chainTargets, chainDamage, chainRadius, weaponName, beamVfx);
            }
        }

        if (hitPositions.Count > 0 && zoneTicks > 0 && zoneRadius > 0f)
        {
            StartCoroutine(SpawnLightningZonesServerSide(hitPositions, zoneTicks, zoneTickInterval, zoneRadius, zoneDamage, isCrit, weaponName, zoneVfx));
        }
    }

    // ── Helper methods for server-side chaining and zones ─────────────────

    private void FireLightningChainServerSide(
        Vector3 startPos, Enemy firstEnemy, int mask, List<Vector3> hitPositions,
        int chainTargets, float chainDamage, float chainRadius, string weaponName, string weaponVfx)
    {
        var hitSet = new HashSet<int>();
        hitSet.Add(firstEnemy.GetInstanceID());

        Vector3 prevPos = startPos;
        Enemy current = firstEnemy;
        float curDmg = chainDamage;

        for (int i = 0; i < chainTargets; i++)
        {
            Enemy next = FindNearestUnhitEnemyServerSide(current.transform.position + Vector3.up * 0.8f, chainRadius, mask, hitSet);
            if (next == null) break;

            Vector3 targetPos = next.transform.position + Vector3.up * 0.8f;

            BroadcastBeamClientRpc(prevPos, targetPos, weaponVfx, "None");

            next.EnemyTakeDamage(curDmg);
            RegisterWeaponDamage(weaponName, curDmg);
            hitPositions.Add(next.transform.position);
            hitSet.Add(next.GetInstanceID());

            current = next;
            prevPos = targetPos;
            curDmg *= 0.7f;
        }
    }

    private Enemy FindNearestUnhitEnemyServerSide(Vector3 center, float radius, int mask, HashSet<int> exclude)
    {
        var cols = Physics.OverlapSphere(center, radius, mask);
        Enemy best = null;
        float minD = float.MaxValue;
        foreach (var c in cols)
        {
            var e = c.GetComponent<Enemy>();
            if (e == null || exclude.Contains(e.GetInstanceID())) continue;
            float d = Vector3.Distance(center, c.transform.position);
            if (d < minD) { minD = d; best = e; }
        }
        return best;
    }

    private Enemy FindHighestHPEnemyServerSide(Vector3 center, float radius, int mask, HashSet<int> exclude)
    {
        var cols = Physics.OverlapSphere(center, radius, mask);
        Enemy best = null;
        float bestHP = -1f;
        foreach (var c in cols)
        {
            var e = c.GetComponent<Enemy>();
            if (e == null || exclude.Contains(e.GetInstanceID())) continue;
            if (e.netHealth.Value > bestHP) { bestHP = e.netHealth.Value; best = e; }
        }
        return best;
    }

    private System.Collections.IEnumerator SpawnLightningZonesServerSide(
        List<Vector3> positions, int zoneTicks, float zoneTickInterval,
        float zoneRadius, float zoneDamage, bool isCrit, string weaponName, string zoneVfx)
    {
        float effectiveZoneDmg = zoneDamage;

        for (int tick = 0; tick < zoneTicks; tick++)
        {
            yield return new WaitForSeconds(zoneTickInterval);
            foreach (var pos in positions)
            {
                foreach (var c in OverlapEnemy(pos + Vector3.up * 0.5f, zoneRadius))
                {
                    var enemy = c.GetComponent<Enemy>();
                    if (enemy != null)
                    {
                        enemy.EnemyTakeDamage(effectiveZoneDmg, isCrit);
                        RegisterWeaponDamage(weaponName, effectiveZoneDmg);
                    }
                }
                BroadcastVfxTypeClientRpc(pos + Vector3.up * 0.5f, zoneVfx, 1f);
            }
        }
    }

    void OnDestroy()
    {
        if (NetworkedVFXPool.Instance != null)
        {
            foreach (var kv in _activeLoopVfxs)
            {
                if (kv.Value != null)
                {
                    NetworkedVFXPool.Instance.StopParented(kv.Key, kv.Value);
                }
            }
        }
        _activeLoopVfxs.Clear();

        foreach (var o in _remoteOrbVisuals) if (o) Destroy(o);
        _remoteOrbVisuals.Clear();
    }
}
