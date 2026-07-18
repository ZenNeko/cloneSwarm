using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Vortex Weapon — Redesigned as a revolving flamethrower
/// Sprays continuous fire streams that orbit/revolve around the player.
///
/// Uses Client-Simulated Visuals (rotating Particle Systems) + Server-Authoritative Sweep checks
/// to prevent network lag from spawning hundreds of NetworkObject fire projectiles.
///
/// Level Scaling:
///   Lv1: 1 stream, range=3.5, cd=N/A (Uses custom damageInterval)
///   Lv2: 1 stream, range=4.5, damage increased
///   Lv3: 2 streams (opposite sides), range=4.5
///   Lv4: 2 streams, range=5.5, damage increased
///   Lv5: 3 streams (120 degrees), range=6.0
///
/// Super: SpiralGalaxyWeapon (4 streams cross shape)
/// Fusion: Spiral Galaxy + OrbitalCannon = CosmicStormWeapon
/// </summary>
public class VortexWeapon : WeaponBase
{
    [Header("Flamethrower Settings")]
    [Tooltip("Rotation speed in degrees per second (positive = CCW, negative = CW)")]
    public float rotSpeed = 140f;

    [Tooltip("Damage checking interval in seconds")]
    public float damageInterval = 0.15f;

    [Tooltip("Width of the flamethrower sweep box")]
    public float streamWidth = 2.0f;

    [Tooltip("Nozzle transforms. If empty, the script will look for child GameObjects named 'Nozzle1', 'Nozzle2', etc.")]
    public List<Transform> nozzles = new();

    protected override bool UsesCooldownTimer => false; // Handle damage ticking manually

    protected float orbitAngle;
    private float damageTimer;

    private List<GameObject> vfxInstances = new();

    protected override void OnInit()
    {
        // Find nozzles automatically if not assigned (recursive search through all descendants)
        if (nozzles.Count == 0)
        {
            var allTransforms = GetComponentsInChildren<Transform>(true);
            for (int i = 1; i <= 4; i++)
            {
                string targetName = $"Nozzle{i}";
                foreach (var t in allTransforms)
                {
                    if (t.name == targetName)
                    {
                        nozzles.Add(t);
                        break;
                    }
                }
            }
        }


        // Dynamically instantiate the visual prefab from the NetworkedVFXPool under the PREDEFINED nozzles on all clients
        string vfxKey = ResolveHitVfx("VortexSpawn");
        GameObject vfxPrefab = null;
        if (NetworkedVFXPool.Instance != null && vfxKey != "None")
        {
            vfxPrefab = NetworkedVFXPool.Instance.GetVfxPrefab(vfxKey);
        }

        for (int i = 0; i < nozzles.Count; i++)
        {
            if (nozzles[i] == null) continue;
            if (vfxPrefab != null)
            {
                GameObject vfxGo = Instantiate(vfxPrefab, nozzles[i]);
                vfxGo.transform.localPosition = Vector3.zero;
                vfxGo.transform.localRotation = Quaternion.identity;
                vfxInstances.Add(vfxGo);

                // Play the visual effect (searches both root and children)
                var vfxGraph = vfxGo.GetComponentInChildren<UnityEngine.VFX.VisualEffect>();
                if (vfxGraph != null) vfxGraph.Play();
                else
                {
                    foreach (var ps in vfxGo.GetComponentsInChildren<ParticleSystem>())
                    {
                        ps.Play();
                    }
                }
            }
        }

        RebuildNozzles();
    }

    protected override void OnLevelUp()
    {
        RebuildNozzles();
    }

    protected override void OnFire(WeaponLevelData ld)
    {
        // OnFire is not called because UsesCooldownTimer = false
    }

    protected override void Update()
    {
        // 1. Rotate the orbit angle on all clients for smooth visual synchronization
        orbitAngle += rotSpeed * Time.deltaTime;
        UpdateNozzleRotations();

        // 2. Rebuild nozzles dynamically if stream count increases due to player stat changes
        int targetCount = GetStreamCount();
        if (targetCount > nozzles.Count)
        {
            RebuildNozzles();
        }

        // 3. Update VFX scale dynamically based on current level range
        UpdateVfxScales();

        // 4. Base checks and Owner-only damage checks
        if (manager == null || !manager.IsOwner) return;
        if (manager.playerMove != null && manager.playerMove.isDead.Value) return;
        if (!PlayerWeaponManager.WeaponsEnabledInScene) return;

        damageTimer += Time.deltaTime;
        if (damageTimer >= damageInterval)
        {
            damageTimer = 0f;
            SweepDamage();
        }
    }

    protected virtual int GetStreamCount()
    {
        // Pull base stream count directly from the WeaponData Level configuration (WD_Vortex or WD_SpiralGalaxy)
        int baseStreams = 1;
        if (data != null)
        {
            var ld = data.GetLevelData(currentLevel);
            baseStreams = ld.projectileCount;
        }

        int bonusProj = (manager != null && manager.statManager != null)
            ? manager.statManager.GetBonusProjectileCount()
            : 0;

        return baseStreams + bonusProj;
    }

    private void RebuildNozzles()
    {
        int targetCount = GetStreamCount();
        if (targetCount <= 0) return;

        // If we need more nozzles than currently available, dynamically create pivots
        string vfxKey = ResolveHitVfx("VortexSpawn");
        GameObject vfxPrefab = null;
        if (NetworkedVFXPool.Instance != null && vfxKey != "None")
        {
            vfxPrefab = NetworkedVFXPool.Instance.GetVfxPrefab(vfxKey);
        }

        while (nozzles.Count < targetCount)
        {
            int nextIndex = nozzles.Count + 1;
            GameObject newNozzleGo = new GameObject($"Nozzle{nextIndex}_Dynamic");
            newNozzleGo.transform.SetParent(this.transform, false);
            newNozzleGo.transform.localPosition = Vector3.zero;
            newNozzleGo.transform.localRotation = Quaternion.identity;
            newNozzleGo.transform.localScale = Vector3.one;
            nozzles.Add(newNozzleGo.transform);

            if (vfxPrefab != null)
            {
                GameObject vfxGo = Instantiate(vfxPrefab, newNozzleGo.transform);
                vfxGo.transform.localPosition = Vector3.zero;
                vfxGo.transform.localRotation = Quaternion.identity;
                vfxInstances.Add(vfxGo);

                var vfxGraph = vfxGo.GetComponentInChildren<UnityEngine.VFX.VisualEffect>();
                if (vfxGraph != null) vfxGraph.Play();
                else
                {
                    foreach (var ps in vfxGo.GetComponentsInChildren<ParticleSystem>())
                    {
                        ps.Play();
                    }
                }
            }
        }

        // Toggle active status based on targetCount
        for (int i = 0; i < nozzles.Count; i++)
        {
            if (nozzles[i] != null && nozzles[i] != transform)
            {
                nozzles[i].gameObject.SetActive(i < targetCount);
            }
        }
    }

    private void UpdateNozzleRotations()
    {
        int streamCount = GetStreamCount();
        if (streamCount <= 0) return;

        for (int i = 0; i < nozzles.Count; i++)
        {
            if (nozzles[i] == null || i >= streamCount) continue;

            // Divide the 360 degree circle evenly among all active streams
            float localOffset = i * (360f / streamCount);

            // Apply 45-degree angle offset for the spiral vortex effect (sprays outward/tangent)
            float finalAngle = orbitAngle + localOffset + 45f;
            nozzles[i].localRotation = Quaternion.Euler(0f, finalAngle, 0f);
        }
    }

    private void UpdateVfxScales()
    {
        if (NetworkedVFXPool.Instance == null) return;
        string vfxKey = ResolveHitVfx("VortexSpawn");
        float designed = NetworkedVFXPool.Instance.GetDesignedRadius(vfxKey);

        var ld = data.GetLevelData(currentLevel);
        float areaMult = manager.statManager != null ? manager.statManager.GetAreaMultiplier() : 1f;
        float range = ld.range * areaMult;

        float scaleVal = designed > 0f ? (range / designed) : range;

        for (int i = 0; i < vfxInstances.Count; i++)
        {
            if (vfxInstances[i] != null)
            {
                // Parent Scale Cancellation to ensure world scale matches range exactly
                Transform parent = vfxInstances[i].transform.parent;
                float px = parent != null ? parent.lossyScale.x : 1f;
                float py = parent != null ? parent.lossyScale.y : 1f;
                float pz = parent != null ? parent.lossyScale.z : 1f;
                if (px <= 0f) px = 1f;
                if (py <= 0f) py = 1f;
                if (pz <= 0f) pz = 1f;

                vfxInstances[i].transform.localScale = new Vector3(scaleVal / px, scaleVal / py, scaleVal / pz);
            }
        }
    }

    private void SweepDamage()
    {
        var ld = data.GetLevelData(currentLevel);
        var effective = BuildEffectiveLevelData(ld);

        float dmg = RollDamage(effective.damage, out bool isCrit);
        float range = effective.range; // Stat-multiplied range (includes GetAreaMultiplier())
        int streamCount = GetStreamCount();

        Vector3 origin = transform.position + Vector3.up * 0.5f;

        for (int i = 0; i < nozzles.Count; i++)
        {
            if (nozzles[i] == null || i >= streamCount) continue;

            // Get the world forward direction of the active nozzle
            Vector3 dir = nozzles[i].forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) continue;
            dir = dir.normalized;

            // Perform Line AoE damage sweep (using FireLineAoE with vfxKey: "None" since particles are simulated locally)
            FireLineAoE(origin, dir, dmg, range, streamWidth, isCrit, knockbackForce: 0f, vfxKey: "None");
        }
    }
}

