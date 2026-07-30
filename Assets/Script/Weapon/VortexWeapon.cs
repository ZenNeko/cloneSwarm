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

    [Tooltip("Distance of the nozzles from the center (player position)")]
    public float orbitRadius = 1.2f;

    [Tooltip("Nozzle transforms. If empty, the script will look for child GameObjects named 'Nozzle1', 'Nozzle2', etc.")]
    public List<Transform> nozzles = new();

    protected override bool UsesCooldownTimer => false; // Handle damage ticking manually

    protected float orbitAngle;
    private float damageTimer;

    protected List<GameObject> vfxInstances = new();

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

        // Force all predefined nozzles to be direct children of this weapon root
        // to prevent uneven rotation/scale if they were nested under other rotating/scaled transforms
        for (int i = 0; i < nozzles.Count; i++)
        {
            if (nozzles[i] != null && nozzles[i] != transform)
            {
                nozzles[i].SetParent(this.transform, false);
            }
        }

        // Dynamically instantiate the visual prefab under the PREDEFINED nozzles on all clients
        for (int i = 0; i < nozzles.Count; i++)
        {
            if (nozzles[i] == null) continue;

            string key = IsIceStream(i) ? ResolveSecondaryVfx("None") : ResolveHitVfx("None");
            if (key == "None" || string.IsNullOrEmpty(key)) key = ResolveHitVfx("None");

            GameObject vfxPrefab = null;
            if (NetworkedVFXPool.Instance != null && key != "None")
            {
                vfxPrefab = NetworkedVFXPool.Instance.GetVfxPrefab(key);
            }

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
            else
            {
                vfxInstances.Add(null);
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

        return baseStreams;
    }

    protected virtual bool IsIceStream(int index) => false;

    private void RebuildNozzles()
    {
        int targetCount = GetStreamCount();
        if (targetCount <= 0) return;

        while (nozzles.Count < targetCount)
        {
            int nextIndex = nozzles.Count + 1;
            GameObject newNozzleGo = new GameObject($"Nozzle{nextIndex}_Dynamic");
            newNozzleGo.transform.SetParent(this.transform, false);
            newNozzleGo.transform.localPosition = Vector3.zero;
            newNozzleGo.transform.localRotation = Quaternion.identity;
            newNozzleGo.transform.localScale = Vector3.one;
            nozzles.Add(newNozzleGo.transform);

            int currentIdx = nozzles.Count - 1;
            string key = IsIceStream(currentIdx) ? ResolveSecondaryVfx("None") : ResolveHitVfx("None");
            if (key == "None" || string.IsNullOrEmpty(key)) key = ResolveHitVfx("None");

            GameObject vfxPrefab = null;
            if (NetworkedVFXPool.Instance != null && key != "None")
            {
                vfxPrefab = NetworkedVFXPool.Instance.GetVfxPrefab(key);
            }

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
            else
            {
                vfxInstances.Add(null);
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

            // 1. Position the nozzle on the circle of radius orbitRadius at radialAngle
            float radialAngle = orbitAngle + localOffset;
            float rad = radialAngle * Mathf.Deg2Rad;
            nozzles[i].localPosition = new Vector3(Mathf.Sin(rad) * orbitRadius, 0f, Mathf.Cos(rad) * orbitRadius);

            // 2. Rotate the nozzle by finalAngle so it sprays outward diagonally (45 degrees offset)
            float finalAngle = radialAngle + 45f;
            nozzles[i].localRotation = Quaternion.Euler(0f, finalAngle, 0f);
        }
    }

    private void UpdateVfxScales()
    {
        if (NetworkedVFXPool.Instance == null) return;

        var ld = data.GetLevelData(currentLevel);
        float areaMult = manager.statManager != null ? manager.statManager.GetAreaMultiplier() : 1f;
        float range = ld.range * areaMult;

        for (int i = 0; i < vfxInstances.Count; i++)
        {
            if (vfxInstances[i] != null)
            {
                string key = IsIceStream(i) ? ResolveSecondaryVfx("None") : ResolveHitVfx("None");
                if (key == "None" || string.IsNullOrEmpty(key)) key = ResolveHitVfx("None");

                float designed = NetworkedVFXPool.Instance.GetDesignedRadius(key);
                float scaleVal = designed > 0f ? (range / designed) : range;

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

        for (int i = 0; i < nozzles.Count; i++)
        {
            if (nozzles[i] == null || i >= streamCount) continue;
            SweepNozzleDamage(i, dmg, range, isCrit);
        }
    }

    protected virtual void SweepNozzleDamage(int index, float dmg, float range, bool isCrit)
    {
        if (nozzles[index] == null) return;
        Vector3 dir = nozzles[index].forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        dir = dir.normalized;

        // Damage origin starts at the nozzle's world position, height locked to gameplay plane (0.5f above player root)
        Vector3 origin = nozzles[index].position;
        origin.y = transform.position.y + 0.5f;

        FireLineAoE(origin, dir, dmg, range, streamWidth, isCrit, knockbackForce: 0f, vfxKey: "None");
    }
}
