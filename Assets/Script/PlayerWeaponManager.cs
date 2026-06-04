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
        float damage, float speed, float maxRange, bool isCrit = false)
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
        no.Spawn(true);
        proj.Init(direction);
    }

    // ── ServerRpc: Projectile ─────────────────────────────────────────────
    [ServerRpc(RequireOwnership = false)]
    public void FireProjectileServerRpc(
        Vector3 spawnPos, Vector3 baseDir,
        float damage, float projSpeed, int count, float spreadDeg,
        bool piercing = false, int projPrefabId = -1, float maxRange = -1f,
        bool isCrit = false)
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
    public void FireMeleeServerRpc(Vector3 center, float radius, float damage, bool isCrit = false)
    {
        foreach (var c in OverlapEnemy(center, radius))
            c.GetComponent<Enemy>()?.EnemyTakeDamage(damage, isCrit);
    }

    /// <summary>Melee AoE แบบ arc — เฉพาะ enemy ที่อยู่ใน cone ทิศ forward</summary>
    [ServerRpc(RequireOwnership = false)]
    public void FireArcMeleeServerRpc(Vector3 center, Vector3 forward, float radius, float arcAngle, float damage, bool isCrit = false)
    {
        float halfArc = arcAngle * 0.5f;
        foreach (var c in OverlapEnemy(center, radius))
        {
            Vector3 toEnemy = c.transform.position - center;
            toEnemy.y = 0f;
            if (toEnemy.sqrMagnitude < 0.001f || Vector3.Angle(forward, toEnemy) <= halfArc)
                c.GetComponent<Enemy>()?.EnemyTakeDamage(damage, isCrit);
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
        float fuseTime = 1.5f, bool cluster = false)
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
        float knockbackForce = 0f)
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
            c.GetComponent<Enemy>()?.EnemyTakeDamage(damage, isCrit);
            // Knockback (server-authoritative push along line direction)
            if (knockbackForce > 0f)
            {
                Vector3 push = direction * knockbackForce;
                c.transform.position += push;
            }
        }

        ShowLineAoEVfxClientRpc(origin, origin + direction * range);
    }

    [ClientRpc]
    void ShowLineAoEVfxClientRpc(Vector3 from, Vector3 to)
    {
        VFXFactory.PlayBeam(VFXType.None, from, to, duration: 0.18f);
    }

    // ── ServerRpc: Raycast Pierce (Railgun / PlasmaWhip / WindSlash) ────────
    [ServerRpc(RequireOwnership = false)]
    public void FireRaycastServerRpc(Vector3 origin, Vector3 direction, float damage,
                                     float maxDist = 50f, int vfxTypeInt = (int)VFXType.None,
                                     bool isCrit = false, bool playHitVfx = true)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;
        direction = direction.normalized;

        var mask = LayerMask.GetMask("Enemy");
        var hits = Physics.RaycastAll(origin, direction, maxDist, mask);
        foreach (var h in hits)
        {
            // Enemy.NotifyHitClientRpc spawn HitEffect/CritHitEffect ที่ตัว enemy เอง
            // (playHitVfx flag เก็บไว้เพื่อ backward-compat แต่ไม่ใช้แล้ว)
            h.collider.GetComponent<Enemy>()?.EnemyTakeDamage(damage, isCrit);
        }

        ShowRaycastVfxClientRpc(origin, origin + direction * maxDist, vfxTypeInt);
    }

    [ClientRpc]
    void ShowRaycastVfxClientRpc(Vector3 from, Vector3 to, int vfxTypeInt)
    {
        VFXFactory.PlayBeam((VFXType)vfxTypeInt, from, to, duration: 0.12f);
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

    // ── ServerRpc: Hunter Missiles (Q) ───────────────────────────────────
    [ServerRpc(RequireOwnership = false)]
    public void SpawnMissilesServerRpc(
        Vector3[] spawnPositions, ulong[] targetNetIds,
        float damage, float explosionRadius)
    {
        if (missilePrefab == null) return;
        int count = Mathf.Min(spawnPositions.Length, targetNetIds.Length);
        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(missilePrefab, spawnPositions[i], missilePrefab.transform.localRotation);
            go.transform.localScale = missilePrefab.transform.localScale;
            go.GetComponent<MissileProjectile>()?.Init(targetNetIds[i], damage, explosionRadius);
            go.GetComponent<NetworkObject>()?.Spawn(true);
        }
    }

    // ── ServerRpc: Hunter Funnels (Ultimate) ──────────────────────────────
    [ServerRpc(RequireOwnership = false)]
    public void SpawnFunnelsServerRpc(
        Vector3 center, int count, float orbitRadius,
        float laserDamage, float laserCooldown,
        float attackRange, float lifetime, ulong ownerClientId,
        int beamCount = 1)
    {
        if (funnelPrefab == null) return;
        for (int i = 0; i < count; i++)
        {
            float   angle    = i * (360f / count);
            Vector3 offset   = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * orbitRadius * 0.4f;
            var     go       = Instantiate(funnelPrefab, center + offset + Vector3.up * 1.5f, Quaternion.identity);
            go.GetComponent<FunnelObject>()?.Init(
                center, orbitRadius, laserDamage, laserCooldown, attackRange, lifetime, ownerClientId, beamCount);
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
        float damage, float speed, float explosionRadius)
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
            sr.InitDirection(direction);
        }
        go.GetComponent<NetworkObject>()?.Spawn(true);
    }

    // ── ServerRpc: Giant Rocket (Gunner E) ────────────────────────────────
    [ServerRpc(RequireOwnership = false)]
    public void SpawnGiantRocketServerRpc(
        Vector3 spawnPos, Vector3 direction,
        float baseDamage, float speed,
        float maxRange, float explosionRadius)
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
            gr.InitDirection(spawnPos, dir);
        }
        go.GetComponent<NetworkObject>()?.Spawn(true);
    }

    // ── VFX Broadcast by VFXType → NetworkedVFXPool ──────────────────────
    /// <summary>Weapon scripts ทุกตัวใช้ช่องทางนี้ผ่าน ShowHitVfx() หรือ BroadcastVfxTypeServerRpc โดยตรง</summary>
    [ServerRpc(RequireOwnership = false)]
    public void BroadcastVfxTypeServerRpc(Vector3 pos, int vfxTypeInt, float scale = 1f, Vector3 direction = default, float arcAngle = 360f, float roll = 0f)
        => BroadcastVfxTypeClientRpc(pos, vfxTypeInt, scale, direction, arcAngle, roll);

    [ClientRpc]
    void BroadcastVfxTypeClientRpc(Vector3 pos, int vfxTypeInt, float scale = 1f, Vector3 direction = default, float arcAngle = 360f, float roll = 0f)
        => NetworkedVFXPool.Instance?.PlayByType((VFXType)vfxTypeInt, pos, scale, direction, arcAngle, roll);

    // ── Beam VFX Broadcast (Lightning Chain, Thunder Rail ฯลฯ) ───────────
    /// <summary>วาด LineRenderer beam จาก from→to บนทุก client</summary>
    [ServerRpc(RequireOwnership = false)]
    public void BroadcastBeamServerRpc(Vector3 from, Vector3 to, int vfxTypeInt)
        => BroadcastBeamClientRpc(from, to, vfxTypeInt);

    [ClientRpc]
    void BroadcastBeamClientRpc(Vector3 from, Vector3 to, int vfxTypeInt)
        => VFXFactory.PlayBeam((VFXType)vfxTypeInt, from, to, duration: 0.15f);

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
}
