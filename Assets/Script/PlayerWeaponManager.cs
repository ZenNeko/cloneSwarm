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

    [ClientRpc]
    public void PlayWeaponHitSfxClientRpc(string weaponName, Vector3 pos)
    {
        var slot = slots.Find(s => s.data != null && s.data.weaponName == weaponName);
        if (slot != null && slot.script != null)
        {
            slot.script.PublicPlayHitSfx(pos);
        }
    }

    [ClientRpc]
    public void SpawnOrbitalWarningClientRpc(Vector3 position, float radius, float duration)
    {
        if (IsOwner) return; // Owner runs locally
        var slot = slots.Find(s => s.script is OrbitalStrikeWeapon);
        if (slot != null && slot.script is OrbitalStrikeWeapon osw)
        {
            osw.SpawnLocalWarningVisualOnly(position, radius, duration);
        }
    }

    [ClientRpc]
    public void SpawnFencePillarClientRpc(Vector3 position, float lifetime)
    {
        if (IsOwner) return; // Owner runs locally
        var slot = slots.Find(s => s.script is FenceWeapon);
        if (slot != null && slot.script is FenceWeapon fw)
        {
            fw.SpawnLocalPillarVisualOnly(position, lifetime);
        }
    }

    [Rpc(SendTo.Server)]
    public void SpawnSupportArenaServerRpc(Vector3 position, float radius, float lifetime, bool evo, float healAmount)
    {
        if (!IsOwner)
        {
            var slot = slots.Find(s => s.script is SupportArenaWeapon);
            if (slot != null && slot.script is SupportArenaWeapon saw)
            {
                saw.SpawnLocalArena(position, radius, lifetime, evo, healAmount);
            }
        }
        SpawnSupportArenaClientRpc(position, radius, lifetime, evo, healAmount);
    }

    [ClientRpc]
    public void SpawnSupportArenaClientRpc(Vector3 position, float radius, float lifetime, bool evo, float healAmount)
    {
        if (IsOwner || IsServer) return;
        var slot = slots.Find(s => s.script is SupportArenaWeapon);
        if (slot != null && slot.script is SupportArenaWeapon saw)
        {
            saw.SpawnLocalArena(position, radius, lifetime, evo, healAmount);
        }
    }

    [Rpc(SendTo.Server)]
    public void AddWeaponServerRpc(string weaponName)
    {
        if (!IsOwner)
        {
            var data = FindWeaponDataByName(weaponName);
            if (data != null) AddWeapon(data);
        }
        AddWeaponClientRpc(weaponName);
    }

    [ClientRpc]
    public void AddWeaponClientRpc(string weaponName)
    {
        if (IsOwner || IsServer) return;
        var data = FindWeaponDataByName(weaponName);
        if (data != null)
        {
            AddWeapon(data);
        }
    }

    [Rpc(SendTo.Server)]
    public void UpgradeWeaponServerRpc(string weaponName)
    {
        if (!IsOwner)
        {
            var data = FindWeaponDataByName(weaponName);
            if (data != null) UpgradeWeapon(data);
        }
        UpgradeWeaponClientRpc(weaponName);
    }

    [ClientRpc]
    public void UpgradeWeaponClientRpc(string weaponName)
    {
        if (IsOwner || IsServer) return;
        var data = FindWeaponDataByName(weaponName);
        if (data != null)
        {
            UpgradeWeapon(data);
        }
    }

    [Rpc(SendTo.Server)]
    public void ReplaceWeaponServerRpc(string oldName, string newName)
    {
        if (!IsOwner)
        {
            var oldData = FindWeaponDataByName(oldName);
            var newData = FindWeaponDataByName(newName);
            if (oldData != null && newData != null) ReplaceWeapon(oldData, newData);
        }
        ReplaceWeaponClientRpc(oldName, newName);
    }

    [ClientRpc]
    public void ReplaceWeaponClientRpc(string oldName, string newName)
    {
        if (IsOwner || IsServer) return;
        var oldData = FindWeaponDataByName(oldName);
        var newData = FindWeaponDataByName(newName);
        if (oldData != null && newData != null)
        {
            ReplaceWeapon(oldData, newData);
        }
    }

    [Rpc(SendTo.Server)]
    public void FuseWeaponsServerRpc(string superAName, string superBName, string resultName)
    {
        if (!IsOwner)
        {
            var superA = FindWeaponDataByName(superAName);
            var superB = FindWeaponDataByName(superBName);
            var result = FindWeaponDataByName(resultName);
            if (superA != null && superB != null && result != null) FuseWeapons(superA, superB, result);
        }
        FuseWeaponsClientRpc(superAName, superBName, resultName);
    }

    [ClientRpc]
    public void FuseWeaponsClientRpc(string superAName, string superBName, string resultName)
    {
        if (IsOwner || IsServer) return;
        var superA = FindWeaponDataByName(superAName);
        var superB = FindWeaponDataByName(superBName);
        var result = FindWeaponDataByName(resultName);
        if (superA != null && superB != null && result != null)
        {
            FuseWeapons(superA, superB, result);
        }
    }

    [Rpc(SendTo.Server)]
    public void SpawnPassiveWeaponServerRpc(string weaponName)
    {
        if (!IsOwner)
        {
            var data = FindWeaponDataByName(weaponName);
            if (data != null) SpawnPassiveWeapon(data);
        }
        SpawnPassiveWeaponClientRpc(weaponName);
    }

    [ClientRpc]
    public void SpawnPassiveWeaponClientRpc(string weaponName)
    {
        if (IsOwner || IsServer) return;
        var data = FindWeaponDataByName(weaponName);
        if (data != null)
        {
            SpawnPassiveWeapon(data);
        }
    }

    private WeaponData FindWeaponDataByName(string name)
    {
        var upgradeManager = GetComponent<UpgradeManager>();
        if (upgradeManager != null && upgradeManager.allWeapons != null)
        {
            // 1. ค้นหาในอาวุธทั่วไป
            var found = upgradeManager.allWeapons.Find(w => w.weaponName == name);
            if (found != null) return found;

            // 2. ค้นหาในอาวุธเวอร์ชัน Super (ผ่านความสัมพันธ์ของอาวุธทั่วไปที่มี)
            foreach (var w in upgradeManager.allWeapons)
            {
                if (w != null && w.superVersion != null && w.superVersion.weaponName == name)
                    return w.superVersion;
            }

            // 3. ค้นหาในอาวุธเวอร์ชัน Fusion (ผ่านสูตรผสม)
            if (upgradeManager.allRecipes != null)
            {
                foreach (var r in upgradeManager.allRecipes)
                {
                    if (r != null && r.fusionResult != null && r.fusionResult.weaponName == name)
                        return r.fusionResult;
                }
            }
        }
        return null;
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
                playerMove.SetBaseStats(cd);

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

        if (IsOwner)
        {
            AddWeaponServerRpc(data.weaponName);
        }
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
        Debug.Log($"[WeaponManager] [Upgrade] {data.weaponName} Lv{next + 1}");

        if (IsOwner)
        {
            UpgradeWeaponServerRpc(data.weaponName);
        }
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

        if (IsOwner)
        {
            ReplaceWeaponServerRpc(oldData.weaponName, newData.weaponName);
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

        if (IsOwner)
        {
            FuseWeaponsServerRpc(superA.weaponName, superB.weaponName, result.weaponName);
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

        if (IsOwner)
        {
            SpawnPassiveWeaponServerRpc(data.weaponName);
        }
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

    /// <summary>
    /// ถอด weapon ออกจาก slot — **ฝั่งที่เรียกเท่านั้น ไม่ replicate ข้ามเน็ตเวิร์กเอง**
    ///
    /// ตัวที่ใช้ในเกมจริง (`ReplaceWeapon` / `FuseWeapons`) ยิง RPC ของตัวเองต่อท้ายอยู่แล้ว
    /// เปิด public ให้ WeaponTestManager ล้างอาวุธก่อนติดตั้งตัวใหม่ได้ — ไม่งั้นพอทดสอบครบ
    /// 6 ช่อง `AddWeapon` จะคืน false เงียบๆ แล้วปุ่มที่เหลือจะกดไม่ติดโดยไม่มีอะไรบอก
    /// ถ้าเรียกจากที่อื่นนอกเหนือจากนี้ ต้องจัดการ replicate เอง
    /// </summary>
    public void RemoveWeapon(WeaponData data)
    {
        var slot = slots.Find(s => s.data == data);
        if (slot == null) return;
        if (slot.go != null) Destroy(slot.go);
        slots.Remove(slot);
    }

    [Header("Weapon Prefabs")]
    public GameObject boomerangPrefab;   // BoomerangProjectile NetworkObject
    public GameObject grenadePrefab;       // GrenadeProjectile NetworkObject
    public GameObject molotovPrefab;       // MolotovProjectile NetworkObject
    public GameObject minePrefab;          // MineObject NetworkObject
    public GameObject stickyRocketPrefab;  // StickyRocketProjectile NetworkObject
    public GameObject giantRocketPrefab;   // GiantRocketProjectile NetworkObject
    public GameObject missilePrefab;       // MissileProjectile NetworkObject
    public GameObject funnelPrefab;        // FunnelObject NetworkObject

    // ── ServerRpc: Boomerang ──────────────────────────────────────────────
    [Rpc(SendTo.Server)]
    public void SpawnBoomerangServerRpc(
        Vector3 spawnPos, Vector3 direction,
        float damage, float speed, float maxRange, bool isCrit = false, string weaponName = "Unknown", int projPrefabId = -1)
    {
        var targetPrefab = boomerangPrefab;
        if (projPrefabId >= 0 && NetworkedVFXPool.Instance != null)
        {
            var resolved = NetworkedVFXPool.Instance.GetProjectilePrefab(projPrefabId);
            if (resolved != null) targetPrefab = resolved;
        }

        if (targetPrefab == null)
        {
            Debug.LogError("[PlayerWeaponManager] boomerangPrefab ไม่ได้ assign — ลาก Proj_Boomerang.prefab ใส่ Inspector");
            return;
        }
        Quaternion rot = (direction != Vector3.zero ? Quaternion.LookRotation(direction) : Quaternion.identity)
                       * targetPrefab.transform.localRotation;   // คง prefab offset ไว้ (เหมือน StickyRocket)
        var go   = Instantiate(targetPrefab, spawnPos, rot);
        go.transform.localScale = targetPrefab.transform.localScale;
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
    [Rpc(SendTo.Server)]
    public void FireProjectileServerRpc(
        Vector3 spawnPos, Vector3 baseDir,
        float damage, float projSpeed, int count, float spreadDeg,
        bool piercing = false, int projPrefabId = -1, float maxRange = -1f,
        bool isCrit = false, string weaponName = "Unknown",
        ulong targetNetworkObjectId = 999999,
        float explosionRadius = 0f)
    {
        GameObject prefab = null;
        if (projPrefabId >= 0 && NetworkedVFXPool.Instance != null)
        {
            prefab = NetworkedVFXPool.Instance.GetProjectilePrefab(projPrefabId);
        }

        if (prefab == null)
        {
            var slot = slots.Find(s => s.data != null && s.data.weaponName == weaponName);
            if (slot != null && slot.script != null)
            {
                prefab = slot.script.PublicGetProjectilePrefab();
            }
        }

        if (prefab == null)
        {
            prefab = projectilePrefab; // fallback to default bullet
        }

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
            var bsp  = proj.GetComponent<BouncingSpikeProjectile>();
            var tp   = proj.GetComponent<TrainProjectile>();
            var mmp  = proj.GetComponent<MagicMissileProjectile>();
            if (p == null && bsp == null && tp == null && mmp == null) { Destroy(proj); continue; }

            // Lookup weapon slot to override hit VFX from WeaponBase prefab configuration
            string weaponVfx = "None";
            var slot = slots.Find(s => s.data != null && s.data.weaponName == weaponName);
            if (slot != null && slot.script != null)
            {
                weaponVfx = slot.script.weaponVfxType;
            }

            if (p != null)
            {
                p.damage   = damage;
                p.speed    = projSpeed;
                p.piercing = piercing;
                p.isCrit   = isCrit;
                p.weaponName = weaponName;
                p.ownerManager = this;
                p.explosionRadius = explosionRadius;   // 0 = ตีเฉพาะตัวที่ชนตามปกติ
                if (maxRange > 0f) p.maxRange = maxRange;
                if (!string.IsNullOrEmpty(weaponVfx) && weaponVfx != "None")
                    p.hitVFX = weaponVfx;

                p.InitDirection(dir);

                var netObj = proj.GetComponent<NetworkObject>();
                if (netObj == null)
                    Debug.LogWarning($"[Projectile] '{prefab.name}' ไม่มี NetworkObject component!");
                else
                    netObj.Spawn(true);
            }
            else if (bsp != null)
            {
                bsp.damage   = damage;
                bsp.speed    = projSpeed;
                bsp.piercing = piercing;
                bsp.isCrit   = isCrit;
                bsp.weaponName = weaponName;
                bsp.ownerManager = this;
                if (maxRange > 0f) bsp.maxRange = maxRange;
                if (!string.IsNullOrEmpty(weaponVfx) && weaponVfx != "None")
                    bsp.hitVfxKey = weaponVfx;

                var netObj = proj.GetComponent<NetworkObject>();
                if (netObj == null)
                    Debug.LogWarning($"[BouncingSpike] '{prefab.name}' ไม่มี NetworkObject component!");
                else
                {
                    netObj.Spawn(true);
                    bsp.Init(dir);
                }
            }
            else if (tp != null)
            {
                tp.damage   = damage;
                tp.speed    = projSpeed;
                tp.piercing = piercing;
                tp.isCrit   = isCrit;
                tp.weaponName = weaponName;
                tp.ownerManager = this;
                if (maxRange > 0f) tp.maxRange = maxRange;
                tp.isSuper = weaponName.Contains("Express") || weaponName.Contains("Super") || (slot != null && slot.data != null && slot.data.tier == WeaponTier.Super);

                var netObj = proj.GetComponent<NetworkObject>();
                if (netObj == null)
                    Debug.LogWarning($"[TrainProjectile] '{prefab.name}' ไม่มี NetworkObject component!");
                else
                {
                    netObj.Spawn(true);
                    tp.Init(dir);
                }
            }
            else if (mmp != null)
            {
                mmp.damage = damage;
                mmp.speed = projSpeed;
                mmp.isCrit = isCrit;
                mmp.weaponName = weaponName;
                mmp.ownerManager = this;

                // ดึงสเตตัสและการตั้งค่าดีบัฟจากสล็อตอาวุธ
                if (slot != null && slot.script is MagicMissileWeapon mmw)
                {
                    mmp.slowPercent = mmw.slowPercent;
                    mmp.slowDuration = mmw.slowDuration;
                    mmp.evoEnabled = mmw.evoEnabled;
                    mmp.freezeChance = mmw.freezeChance;
                    mmp.freezeDuration = mmw.freezeDuration;

                    float baseRadius = mmw.impactRadius;
                    if (slot.data != null)
                    {
                        var ld = slot.data.GetLevelData(slot.level);
                        if (ld != null && ld.radius > 0f)
                        {
                            baseRadius = ld.radius;
                        }
                    }

                    float areaMult = 1f;
                    if (statManager != null)
                    {
                        areaMult = statManager.GetAreaMultiplier();
                    }
                    mmp.impactRadius = baseRadius * areaMult;
                }

                if (!string.IsNullOrEmpty(weaponVfx) && weaponVfx != "None")
                    mmp.hitVfxKey = weaponVfx;

                Transform targetTransform = null;
                if (targetNetworkObjectId != 999999 && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out var netObjTarget))
                {
                    targetTransform = netObjTarget.transform;
                }

                var netObj = proj.GetComponent<NetworkObject>();
                if (netObj == null)
                    Debug.LogWarning($"[MagicMissile] '{prefab.name}' ไม่มี NetworkObject component!");
                else
                {
                    netObj.Spawn(true);
                    mmp.Init(targetTransform, dir);
                }
            }
        }
    }

    // ── ServerRpc: Melee AoE ──────────────────────────────────────────────
    [Rpc(SendTo.Server)]
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
    [Rpc(SendTo.Server)]
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

    /// <summary>ดึงศัตรูเข้าหาจุดศูนย์กลาง (Gravity Well — reverse knockback)</summary>
    [Rpc(SendTo.Server)]
    public void PullEnemiesServerRpc(Vector3 center, float radius, float force)
    {
        foreach (var c in OverlapEnemy(center, radius))
        {
            var enemy = c.GetComponent<Enemy>();
            if (enemy != null)
            {
                Vector3 dir = center - enemy.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    // ดูดเข้าหาศูนย์กลาง แต่ไม่ให้เลยจุดศูนย์กลาง
                    float dist = dir.magnitude;
                    float move = Mathf.Min(force, dist);
                    enemy.transform.position += dir.normalized * move;
                }
            }
        }
    }

    // ── Helper: หาศัตรูในรัศมี (Layer + Tag fallback) ─────────────────────
    //
    // **คืน collider ตัวเดียวต่อ Enemy หนึ่งตัวเสมอ**
    //
    // ศัตรูตัวหนึ่งมี collider ได้หลายตัวบน GameObject เดียวกัน (เช่น TargetDummy มี
    // BoxCollider ที่เป็น trigger + SphereCollider ที่เป็นของแข็ง) และ Physics.queriesHitTriggers
    // มีค่า default เป็น true ผลลัพธ์จึงมี collider ของศัตรูตัวเดิมโผล่มาหลายครั้ง
    //
    // ผู้เรียกทุกจุดวนลูปแล้วเรียก c.GetComponent<Enemy>() ซึ่งคืน Enemy ตัวเดียวกัน
    // แล้วตี EnemyTakeDamage ซ้ำ — ดาเมจเข้าสองเท่า knockback ผลักสองเท่า และสถิติ DPS
    // นับเกินจริงสองเท่า โดยไม่มีอะไรฟ้อง
    //
    // กรองที่นี่ที่เดียวเพราะมีผู้เรียกกว่าสิบจุด (melee / arc / line / mine / aura ฯลฯ)
    // การไปไล่ใส่ตัวกันซ้ำทีละจุดคือเชิญให้มีจุดที่ลืม
    public static Collider[] OverlapEnemy(Vector3 center, float radius)
    {
        int mask = LayerMask.GetMask("Enemy");
        // ถ้าไม่มี Layer "Enemy" → scan ทุก layer แล้วกรองด้วย Tag
        Collider[] raw = mask == 0
            ? Physics.OverlapSphere(center, radius)
            : Physics.OverlapSphere(center, radius, mask);

        var result = new System.Collections.Generic.List<Collider>(raw.Length);
        // เก็บตัว Enemy ตรงๆ ใช้การเทียบ reference — ไม่ใช้ GetInstanceID()
        // ซึ่ง Unity 6000.7 ประกาศเลิกใช้แล้ว (CS0619 นับเป็น error ในโปรเจกต์นี้)
        var seen   = new System.Collections.Generic.HashSet<Enemy>();

        foreach (var c in raw)
        {
            if (mask == 0 && !c.CompareTag("Enemy")) continue;

            var e = c.GetComponent<Enemy>();
            if (e == null)
            {
                // collider ที่ไม่มี Enemy บนตัวเอง (เช่น hitbox ลูก) ปล่อยผ่านไปตามเดิม
                // ผู้เรียกจะ GetComponent ได้ null แล้วข้ามเอง — พฤติกรรมนี้ไม่เปลี่ยน
                result.Add(c);
                continue;
            }

            if (seen.Add(e))
                result.Add(c);
        }

        return result.ToArray();
    }

    // ── ServerRpc: Grenade (throw → AoE on land) ──────────────────────────
    [Rpc(SendTo.Server)]
    public void ThrowGrenadeServerRpc(
        Vector3 spawnPos, Vector3 targetPos,
        float damage, float radius,
        float fuseTime = 1.5f, bool cluster = false, string weaponName = "Unknown", int projPrefabId = -1, bool isCrit = false,
        GrenadeClusterSettings clusterSettings = default)
    {
        var targetPrefab = grenadePrefab;
        if (projPrefabId >= 0 && NetworkedVFXPool.Instance != null)
        {
            var resolved = NetworkedVFXPool.Instance.GetProjectilePrefab(projPrefabId);
            if (resolved != null) targetPrefab = resolved;
        }

        if (targetPrefab == grenadePrefab)
        {
            var slot = slots.Find(s => s.data != null && s.data.weaponName == weaponName);
            if (slot != null && slot.script != null)
            {
                var resolved = slot.script.PublicGetProjectilePrefab();
                if (resolved != null) targetPrefab = resolved;
            }
            else if (weaponName.Contains("Molotov") || weaponName.Contains("Napalm"))
            {
                if (molotovPrefab != null) targetPrefab = molotovPrefab;
            }
        }

        if (targetPrefab == null)
        {
            Debug.LogError("[PlayerWeaponManager] grenadePrefab ไม่ได้ assign — ต้องการ NetworkObject prefab ที่มี GrenadeProjectile.cs หรือ MolotovProjectile.cs");
            return;
        }
        var go = Instantiate(targetPrefab, spawnPos, Quaternion.identity);
        var gp = go.GetComponent<GrenadeProjectile>();
        if (gp != null)
        {
            gp.damage    = damage;
            // 0 = ให้ prefab ลูกระเบิดตัดสินเอง (ค่าที่ตั้งไว้บนตัวมัน)
            // อาวุธเดิมทุกตัวส่งค่าไม่เป็นศูนย์อยู่แล้วจึงยังชนะเหมือนเดิม —
            // เป็นทางให้ย้ายการตั้งค่าไปอยู่ที่ prefab ลูกทีละอาวุธโดยไม่กระทบตัวอื่น
            // (รูปแบบเดียวกับ SupportArenaWeapon: ld.duration > 0 ? ld.duration : arenaDuration)
            if (radius   > 0f) gp.radius   = radius;
            if (fuseTime > 0f) gp.fuseTime = fuseTime;
            gp.cluster   = cluster;
            gp.isCrit    = isCrit;
            gp.targetPos = targetPos;
            gp.weaponName = weaponName;
            gp.weaponManager = this;

            // อาวุธคุมค่า cluster เองได้ — ไม่ส่งมาก็ใช้ค่าบน prefab ลูกระเบิดตามเดิม
            if (clusterSettings.overrideProjectile)
            {
                gp.clusterPellets      = clusterSettings.pellets;
                gp.clusterDmgPercent   = clusterSettings.dmgPercent;
                gp.clusterProjSpeed    = clusterSettings.projSpeed;
                gp.clusterSpreadRadius = clusterSettings.spreadRadius;
                gp.clusterChildRadius  = clusterSettings.childRadius;
                gp.clusterChildFuse    = clusterSettings.childFuse;
            }

            // ให้ระเบิดใช้ VFX ที่ตั้งไว้บน prefab อาวุธ ตามกฎใน CLAUDE.md ที่ว่า
            // "VFX/SFX อยู่บน weapon prefab ไม่ใช่บน ScriptableObject"
            // บล็อกแบบนี้เคยมีให้เฉพาะ MolotovProjectile ด้านล่าง ส่วนระเบิดถูกลืม —
            // Grenade / Splitter Bomb / Cluster Bomb จึงแสดงเอฟเฟกต์ตัวเดียวกันหมดมาตลอด
            // และการตั้ง weaponVfxType บน prefab อาวุธไม่เคยมีผลอะไรเลย
            var gpSlot = slots.Find(s => s.data != null && s.data.weaponName == weaponName);
            if (gpSlot != null && gpSlot.script != null
                && !string.IsNullOrEmpty(gpSlot.script.weaponVfxType)
                && gpSlot.script.weaponVfxType != "None")
            {
                gp.explosionVfxKey = gpSlot.script.weaponVfxType;
            }
        }
        var mp = go.GetComponent<MolotovProjectile>();
        if (mp != null)
        {
            mp.damage    = damage;
            mp.radius    = radius;
            mp.fuseTime  = fuseTime;
            mp.targetPos = targetPos;
            mp.weaponName = weaponName;
            mp.weaponManager = this;
            mp.isCrit    = isCrit;

            // Lookup weapon slot to override VFX keys from WeaponBase prefab configuration
            var slot = slots.Find(s => s.data != null && s.data.weaponName == weaponName);
            if (slot != null && slot.script != null)
            {
                if (!string.IsNullOrEmpty(slot.script.weaponVfxType) && slot.script.weaponVfxType != "None")
                    mp.explosionVfxKey = slot.script.weaponVfxType;
                if (!string.IsNullOrEmpty(slot.script.secondaryVfxType) && slot.script.secondaryVfxType != "None")
                    mp.zoneVfxKey = slot.script.secondaryVfxType;
            }
        }
        go.GetComponent<NetworkObject>()?.Spawn(true);
    }

    // ── ServerRpc: Line AoE Box (LaserWeapon) ────────────────────────────
    /// <summary>
    /// AoE เส้นตรงแบบมีความกว้าง — ใช้ Physics.OverlapBox ตามแนวยิง
    /// damage enemy ทุกตัวในกล่องสี่เหลี่ยม (width × height × range)
    /// </summary>
    [Rpc(SendTo.Server)]
    public void FireLineAoEServerRpc(
        Vector3 origin, Vector3 direction,
        float damage, float range, float width = 1.5f, bool isCrit = false,
        float knockbackForce = 0f, string vfxKey = "None", string weaponName = "Unknown",
        float slowPercent = 1f, float slowDuration = 0f, float freezeChance = 0f, float freezeDuration = 0f)
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
            int id = c.gameObject.GetId();
            if (!seen.Add(id)) continue;
            // Enemy.NotifyHitClientRpc spawn HitEffect/CritHitEffect ที่ตัว enemy เอง
            var enemy = c.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.EnemyTakeDamage(damage, isCrit);
                RegisterWeaponDamage(weaponName, damage);

                // Apply Slow/Freeze debuffs if configured
                if (slowDuration > 0f && slowPercent < 1f)
                {
                    enemy.ApplySlowDebuff(slowDuration, slowPercent);
                }
                if (freezeDuration > 0f && freezeChance > 0f && UnityEngine.Random.value < freezeChance)
                {
                    enemy.ApplyFreeze(freezeDuration);
                }
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
    [Rpc(SendTo.Server)]
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
    [Rpc(SendTo.Server)]
    public void DropMineServerRpc(Vector3 position, float damage, float triggerRadius, string weaponName = "Unknown", bool isCrit = false)
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
            m.isCrit = isCrit;
        }
        go.GetComponent<NetworkObject>()?.Spawn(true);
    }

    // ── ServerRpc: Hunter Missiles (Q) ───────────────────────────────────
    [Rpc(SendTo.Server)]
    public void SpawnMissilesServerRpc(
        Vector3[] spawnPositions, ulong[] targetNetIds,
        float damage, float explosionRadius, string weaponName = "Unknown", bool isCrit = false)
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
                mp.isCrit = isCrit;
            }
            go.GetComponent<NetworkObject>()?.Spawn(true);
        }
    }

    // ── ServerRpc: Hunter Funnels (Ultimate) ──────────────────────────────
    [Rpc(SendTo.Server)]
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
    [Rpc(SendTo.Server)]
    public void AddShieldServerRpc(float amount)
    {
        if (playerMove == null) return;

        // amount และตัวตั้งของมัน (damage × shieldPercent × จำนวนศัตรู) คำนวณบน client ทั้งคู่
        // (ดู BunnyHopWeapon.cs / StormBunnyWeapon.cs) — ต้องกันค่าติดลบ/NaN แล้ว clamp เพดานก่อนเชื่อ
        // เพดานอิง maxHealth จริง (โตตาม talent/augment) แทนเลขคงที่ 1000 แบบที่ PlayerAugmentManager
        // ใช้กับ Second Wind — เจตนาเดียวกัน แค่เพดานลอยตามผู้เล่นแทนค่าคงที่ (ADR-008 D3)
        if (float.IsNaN(amount) || amount <= 0f) return;
        amount = Mathf.Clamp(amount, 0f, playerMove.maxHealth);

        playerMove.AddShield(amount);
    }

    // ── ServerRpc: Sticky Rocket (Gunner Q mode) ──────────────────────────
    [Rpc(SendTo.Server)]
    public void SpawnStickyRocketServerRpc(
        Vector3 spawnPos, Vector3 direction,
        float damage, float speed, float explosionRadius, string weaponName = "Unknown", bool isCrit = false)
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
            sr.isCrit          = isCrit;
            sr.InitDirection(direction);
        }
        go.GetComponent<NetworkObject>()?.Spawn(true);
    }

    // ── ServerRpc: Giant Rocket (Gunner E) ────────────────────────────────
    [Rpc(SendTo.Server)]
    public void SpawnGiantRocketServerRpc(
        Vector3 spawnPos, Vector3 direction,
        float baseDamage, float speed,
        float maxRange, float explosionRadius, string weaponName = "Unknown", bool isCrit = false)
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
            gr.isCrit          = isCrit;
            gr.InitDirection(spawnPos, dir);
        }
        go.GetComponent<NetworkObject>()?.Spawn(true);
    }

    // ── VFX Broadcast by string key → NetworkedVFXPool ──────────────────────
    /// <summary>Weapon scripts ทุกตัวใช้ช่องทางนี้ผ่าน ShowHitVfx() หรือ BroadcastVfxTypeServerRpc โดยตรง</summary>
    [Rpc(SendTo.Server)]
    public void BroadcastVfxTypeServerRpc(Vector3 pos, string vfxKey, float scale = 1f, Vector3 direction = default, float arcAngle = 360f, float roll = 0f)
        => BroadcastVfxTypeClientRpc(pos, vfxKey, scale, direction, arcAngle, roll);

    [ClientRpc]
    public void BroadcastVfxTypeClientRpc(Vector3 pos, string vfxKey, float scale = 1f, Vector3 direction = default, float arcAngle = 360f, float roll = 0f)
        => NetworkedVFXPool.Instance?.PlayByName(vfxKey, pos, scale, direction, arcAngle, roll);

    // ── Temporary Damage Buff for SuperBigAoE ──────────────────────────
    [ClientRpc]
    public void ApplyDamageBuffClientRpc(float amount, float duration)
    {
        if (statManager != null)
        {
            StartCoroutine(DamageBuffRoutine(statManager, amount, duration));
        }

        // เล่นเอฟเฟกต์สีส้ม/ทองล้อมตัวผู้เล่นชั่วคราวเพื่อบอกว่ากำลังรับบัฟอยู่
        BroadcastVfxParentedClientRpc("O_AoE_RadiantAura", 0.6f, false);

        // สร้าง UI แจ้งเตือนเวลาลอยเหนือหัว
        FloatingBuffUI.Create(transform, duration);
    }

    private System.Collections.IEnumerator DamageBuffRoutine(PlayerStatManager sm, float amount, float duration)
    {
        sm.tempDamageBonusMult += amount;
        yield return new WaitForSeconds(duration);
        sm.tempDamageBonusMult -= amount;
    }

    [Rpc(SendTo.Server)]
    public void ApplySuperBigAoEExplosionServerRpc(Vector3 center, float radius, float buffAmount, float buffDuration, float expMultiplier)
    {
        // 1. บัฟผู้เล่นในระยะ
        var playerMask = LayerMask.GetMask("Player");
        foreach (var c in Physics.OverlapSphere(center, radius, playerMask))
        {
            var targetManager = c.GetComponentInParent<PlayerWeaponManager>();
            if (targetManager != null)
            {
                targetManager.ApplyDamageBuffClientRpc(buffAmount, buffDuration);
            }
        }

        // 2. อัปเกรดลูกแก้ว EXP ทั้งหมดในระยะระเบิด
        for (int i = ExpOrb.ActiveOrbs.Count - 1; i >= 0; i--)
        {
            var orb = ExpOrb.ActiveOrbs[i];
            if (orb != null && Vector3.Distance(orb.transform.position, center) <= radius)
            {
                orb.UpgradeOrb(expMultiplier);
            }
        }
    }

    // ── VFX Broadcast parented to player ────────────────────────────────
    [Rpc(SendTo.Server)]
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

    [Rpc(SendTo.Server)]
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
    [Rpc(SendTo.Server)]
    public void BroadcastBeamServerRpc(Vector3 from, Vector3 to, string beamVfxKey, string hitVfxKey)
        => BroadcastBeamClientRpc(from, to, beamVfxKey, hitVfxKey);

    [ClientRpc]
    void BroadcastBeamClientRpc(Vector3 from, Vector3 to, string beamVfxKey, string hitVfxKey)
        => VFXFactory.PlayBeam(beamVfxKey, hitVfxKey, from, to, duration: 0.15f);

    // ── Orbiter Orb Sync — ตำแหน่ง orb สำหรับ client ที่ไม่ใช่ owner ────────
    private readonly List<GameObject> _remoteOrbVisuals = new();

    [Rpc(SendTo.Server)]
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

    [Rpc(SendTo.Server)]
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
    [Rpc(SendTo.Server)]
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
            hitSet.Add(current.GetId());
            prevPos = targetPos;
            curDmg *= chainDamageMult;

            current = FindNearestUnhitEnemyServerSide(targetPos, chainSearchRadius, mask, hitSet);
        }

        if (hitPositions.Count > 0 && zoneTicks > 0 && zoneRadius > 0f)
        {
            StartCoroutine(SpawnLightningZonesServerSide(hitPositions, zoneTicks, zoneTickInterval, zoneRadius, zoneDamage, isCrit, weaponName, zoneVfx));
        }
    }

    [Rpc(SendTo.Server)]
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
                FireLightningChainServerSide(startChainPos, firstHitEnemy, mask, hitPositions, chainTargets, chainDamage, chainRadius, weaponName, beamVfx, isCrit);
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
        int chainTargets, float chainDamage, float chainRadius, string weaponName, string weaponVfx, bool isCrit = false)
    {
        var hitSet = new HashSet<int>();
        hitSet.Add(firstEnemy.GetId());

        Vector3 prevPos = startPos;
        Enemy current = firstEnemy;
        float curDmg = chainDamage;

        for (int i = 0; i < chainTargets; i++)
        {
            Enemy next = FindNearestUnhitEnemyServerSide(current.transform.position + Vector3.up * 0.8f, chainRadius, mask, hitSet);
            if (next == null) break;

            Vector3 targetPos = next.transform.position + Vector3.up * 0.8f;

            BroadcastBeamClientRpc(prevPos, targetPos, weaponVfx, "None");

            next.EnemyTakeDamage(curDmg, isCrit);
            RegisterWeaponDamage(weaponName, curDmg);
            hitPositions.Add(next.transform.position);
            hitSet.Add(next.GetId());

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
            if (e == null || exclude.Contains(e.GetId())) continue;
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
            if (e == null || exclude.Contains(e.GetId())) continue;
            if (e.netHealth.Value > bestHP) { bestHP = e.netHealth.Value; best = e; }
        }
        return best;
    }

    public void SpawnDamageZone(Vector3 position, int ticks, float tickInterval, float radius, float damage, bool isCrit, string weaponName, string zoneVfx,
        float burnDuration = 0f, float burnDmgPerTick = 0f, float burnInterval = 1f)
    {
        StartCoroutine(SpawnLightningZonesServerSide(new List<Vector3> { position }, ticks, tickInterval, radius, damage, isCrit, weaponName, zoneVfx, burnDuration, burnDmgPerTick, burnInterval));
    }

    private System.Collections.IEnumerator SpawnLightningZonesServerSide(
        List<Vector3> positions, int zoneTicks, float zoneTickInterval,
        float zoneRadius, float zoneDamage, bool isCrit, string weaponName, string zoneVfx,
        float burnDuration = 0f, float burnDmgPerTick = 0f, float burnInterval = 1f)
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

                        if (burnDuration > 0f && burnDmgPerTick > 0f)
                        {
                            enemy.ApplyBurnDot(burnDuration, burnDmgPerTick, burnInterval, isCrit, weaponName, this);
                        }
                    }
                }
                string activeVfx = zoneVfx;
                if (NetworkedVFXPool.Instance != null && !NetworkedVFXPool.Instance.HasMapping(activeVfx))
                {
                    activeVfx = "O_AoE_RadiantAura";
                }

                float scale = 1f;
                if (NetworkedVFXPool.Instance != null && zoneRadius > 0f)
                {
                    float designed = NetworkedVFXPool.Instance.GetDesignedRadius(activeVfx);
                    if (designed > 0f) scale = zoneRadius / designed;
                }
                BroadcastVfxTypeClientRpc(pos + Vector3.up * 0.5f, activeVfx, scale);
            }
        }
    }

    // ── ServerRpc: Debuffs (Slow / Freeze) ────────────────────────────────
    [Rpc(SendTo.Server)]
    public void ApplySlowToEnemiesServerRpc(Vector3 center, float radius, float duration, float slowPercent)
    {
        foreach (var c in OverlapEnemy(center, radius))
        {
            var enemy = c.GetComponent<Enemy>();
            enemy?.ApplySlowDebuff(duration, slowPercent);
        }
    }

    [Rpc(SendTo.Server)]
    public void ApplyFreezeToEnemiesServerRpc(Vector3 center, float radius, float duration)
    {
        foreach (var c in OverlapEnemy(center, radius))
        {
            var enemy = c.GetComponent<Enemy>();
            enemy?.ApplyFreeze(duration);
        }
    }


    public override void OnDestroy()
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
        base.OnDestroy();
    }
}
