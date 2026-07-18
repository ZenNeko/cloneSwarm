using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Fence — วางเสาตามทางเดินผู้เล่น → เสาเชื่อมเลเซอร์ตามลำดับขึงเลเซอร์เป็นแนวสายไฟทำดาเมจศัตรู
///
/// กลไก:
///   • OnFire → ปล่อยเสา 1 ต้นที่ตำแหน่งปัจจุบันของผู้เล่นโดยอัตโนมัติตามวินาที CD
///   • ทุกเฟรม (Visual) → วาดและอัพเดตโมเดลเลเซอร์ (LineRenderer) ค้างไว้ระหว่างเสาตามลำดับ (1->2, 2->3) โดยไม่มีการกระพริบ
///   • ทุก tick interval (Damage) → ยิงกล่องดาเมจด้วย FireLineAoE ตามแนวเลเซอร์เพื่อสร้างความเสียหายจริง
///   • จำนวนเสาสูงสุดขยายตาม projectileCount (maxPillars = 4 * count)
///   • อายุเสาแต่ละเสายาวขึ้นตาม SD_Duration
/// </summary>
public class FenceWeapon : WeaponBase
{
    [Header("Fence Settings")]
    [Tooltip("Prefab visual ของเสา (fallback — ถ้าไม่ได้ตั้ง secondaryVfxType ใน VFX Pool)")]
    public GameObject pillarPrefab;

    [Tooltip("อายุเสาพื้นฐาน (วินาที) — scale ตาม Duration stat")]
    public float pillarLifetime = 8f;

    [Tooltip("ทุกกี่วินาทีที่ beam ทำ damage")]
    public float tickInterval = 0.5f;

    [Tooltip("ความกว้างของ beam damage (ส่งให้ FireLineAoE)")]
    public float beamWidth = 1.5f;

    protected readonly List<FencePillar> _pillars = new();
    protected readonly List<GameObject> _localVisualsOnly = new();
    protected readonly List<GameObject> _activeVisualPillars = new();

    private class ActiveBeam
    {
        public GameObject beamGo;
        public GameObject pillarA;
        public GameObject pillarB;
    }
    private readonly List<ActiveBeam> _activeBeams = new();

    protected override void OnFire(WeaponLevelData ld)
    {
        // ปล่อยเสาทีละต้นที่พิกัดผู้เล่น
        SpawnPillar(transform.position, ld);
    }

    protected virtual int GetMaxPillars(WeaponLevelData ld)
    {
        return 4 * Mathf.Max(1, ld.projectileCount);
    }

    protected override void Update()
    {
        // อัปเดตและขึงเลเซอร์แบบเรียลไทม์ทุกเฟรมบนทุกเครื่อง (ทั้ง Owner และ Remote Clients)
        UpdateVisualBeams();

        if (manager == null || !manager.IsOwner) return;
        if (manager.playerMove != null && manager.playerMove.isDead.Value) return;
        if (!PlayerWeaponManager.WeaponsEnabledInScene) return;
        if (!UsesCooldownTimer) return;

        attackTimer += Time.deltaTime;

        var   ld              = data.GetLevelData(currentLevel);
        float effectiveCooldown = ld.cooldown;

        if (manager.statManager != null)
            effectiveCooldown *= manager.statManager.GetCooldownMultiplier();
        effectiveCooldown *= tempCooldownMult;

        if (attackTimer < effectiveCooldown) return;

        attackTimer = 0f;

        var effective = BuildEffectiveLevelData(ld);
        PlayFireSfx();
        OnFire(effective);
    }

    private void UpdateVisualBeams()
    {
        // 1. เคลียร์เสาที่ถูกทำลายทิ้งไปแล้ว
        _activeVisualPillars.RemoveAll(p => p == null);

        // 2. หาคู่เสาที่ควรจะขึงเลเซอร์เชื่อมกัน
        var desiredConnections = new List<System.Tuple<GameObject, GameObject>>();
        GetDesiredConnections(_activeVisualPillars, desiredConnections);

        // คำนวณระยะเชื่อมเลเซอร์อิงตามสถิติ Area size
        float areaMult = (manager != null && manager.statManager != null) ? manager.statManager.GetAreaMultiplier() : 1f;
        float range = 8f;
        if (data != null)
        {
            range = data.GetLevelData(currentLevel).range * areaMult;
        }

        // 3. กรองเฉพาะเสาคู่ที่ยังอยู่ในระยะเชื่อมเลเซอร์จริง
        var validConnections = new List<System.Tuple<GameObject, GameObject>>();
        foreach (var conn in desiredConnections)
        {
            if (conn.Item1 == null || conn.Item2 == null) continue;
            float dist = Vector3.Distance(conn.Item1.transform.position, conn.Item2.transform.position);
            if (dist <= range)
            {
                validConnections.Add(conn);
            }
        }

        // 4. สปอว์นหรือปรับปรุงพิกัดเลเซอร์ค้างไว้
        var newBeams = new List<ActiveBeam>();
        foreach (var conn in validConnections)
        {
            var existing = _activeBeams.Find(b =>
                (b.pillarA == conn.Item1 && b.pillarB == conn.Item2) ||
                (b.pillarA == conn.Item2 && b.pillarB == conn.Item1)
            );

            if (existing != null)
            {
                UpdateBeamPositions(existing);
                newBeams.Add(existing);
                _activeBeams.Remove(existing);
            }
            else
            {
                var beam = SpawnBeamVisual(conn.Item1, conn.Item2);
                if (beam != null)
                {
                    newBeams.Add(beam);
                }
            }
        }

        // 5. ทำลายเลเซอร์ที่การเชื่อมโยงขาดลงแล้ว
        foreach (var beam in _activeBeams)
        {
            if (beam.beamGo != null)
            {
                Destroy(beam.beamGo);
            }
        }

        _activeBeams.Clear();
        _activeBeams.AddRange(newBeams);
    }

    /// <summary>
    /// ค้นหาคู่การเชื่อมต่อเสาเลเซอร์ (ปกติ: เรียงลำดับ 0->1->2->3)
    /// </summary>
    protected virtual void GetDesiredConnections(List<GameObject> pillars, List<System.Tuple<GameObject, GameObject>> connections)
    {
        for (int i = 0; i < pillars.Count - 1; i++)
        {
            if (pillars[i] != null && pillars[i + 1] != null)
            {
                connections.Add(new System.Tuple<GameObject, GameObject>(pillars[i], pillars[i + 1]));
            }
        }
    }

    private void UpdateBeamPositions(ActiveBeam beam)
    {
        if (beam.beamGo == null || beam.pillarA == null || beam.pillarB == null) return;

        Vector3 from = beam.pillarA.transform.position + Vector3.up * 0.5f;
        Vector3 to = beam.pillarB.transform.position + Vector3.up * 0.5f;

        var lrs = beam.beamGo.GetComponentsInChildren<LineRenderer>(true);
        foreach (var lr in lrs)
        {
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
        }
    }

    private ActiveBeam SpawnBeamVisual(GameObject a, GameObject b)
    {
        string vfxKey = ResolveHitVfx("Beam_Laser");
        GameObject prefab = null;
        if (NetworkedVFXPool.Instance != null)
        {
            prefab = NetworkedVFXPool.Instance.GetVfxPrefab(vfxKey);
        }

        GameObject go = null;
        if (prefab != null)
        {
            go = Instantiate(prefab, transform);
        }
        else
        {
            // Fallback beam
            go = new GameObject("BeamFallback");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.startWidth = 0.18f;
            lr.endWidth   = 0.06f;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                      ?? Shader.Find("Sprites/Default")
                      ?? Shader.Find("Unlit/Color");
            if (shader != null)
            {
                lr.material = new Material(shader) { color = new Color(0.3f, 0.9f, 1f, 1f) };
                lr.startColor = new Color(0.7f, 0.9f, 1f, 1f);
                lr.endColor   = new Color(0.3f, 0.6f, 1f, 0.5f);
            }
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        if (go != null)
        {
            var vfxGraph = go.GetComponent<VisualEffect>();
            if (vfxGraph != null)
            {
                vfxGraph.Play();
            }
            else
            {
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
                {
                    ps.Play();
                }
            }

            var beam = new ActiveBeam
            {
                beamGo = go,
                pillarA = a,
                pillarB = b
            };
            UpdateBeamPositions(beam);
            return beam;
        }
        return null;
    }

    void SpawnPillar(Vector3 position, WeaponLevelData ld)
    {
        int maxPillars = GetMaxPillars(ld);

        // Enforce max active pillars
        while (_pillars.Count >= maxPillars)
        {
            var oldest = _pillars[0];
            _pillars.RemoveAt(0);
            if (oldest.go != null) Destroy(oldest.go);
        }

        // Visual — สปอว์นเสาตรงๆ จาก inspector pillarPrefab
        GameObject go = pillarPrefab != null
            ? Instantiate(pillarPrefab, position, Quaternion.identity)
            : CreateFallbackPillar(position);

        if (go != null)
        {
            _activeVisualPillars.Add(go);
        }

        float durationMult = manager.statManager != null ? manager.statManager.GetDurationMultiplier() : 1f;
        float lifetime     = pillarLifetime * durationMult;

        // Broadcast to other clients to spawn local visual
        if (manager != null)
        {
            manager.SpawnFencePillarClientRpc(position, lifetime);
        }

        var pillar = new FencePillar
        {
            go        = go,
            spawnTime = Time.time,
            lifetime  = lifetime,
            damage    = ld.damage,
            linkRange = ld.range,   // ใช้ range จาก WeaponData (สเกลตาม Area stat แล้ว)
        };
        _pillars.Add(pillar);

        // เริ่ม tick damage coroutine
        StartCoroutine(PillarTickCoroutine(pillar));
    }

    IEnumerator PillarTickCoroutine(FencePillar pillar)
    {
        while (Time.time - pillar.spawnTime < pillar.lifetime)
        {
            yield return new WaitForSeconds(tickInterval);

            if (pillar.go == null) break;
            Vector3 pillarPos = pillar.go.transform.position;

            // Snapshot ป้องกัน concurrent modification
            var snapshot = new List<FencePillar>(_pillars);
            int pillarIdx = snapshot.IndexOf(pillar);
            if (pillarIdx < 0) break; // เสานี้ไม่ได้อยู่ในรายการแล้ว (โดนลบ)

            CheckPillarDamage(pillar, pillarIdx, snapshot, pillarPos);
        }

        // Cleanup
        _pillars.Remove(pillar);
        _activeVisualPillars.Remove(pillar.go);
        if (pillar.go != null) Destroy(pillar.go);
    }

    /// <summary>
    /// ตรวจสอบและยิงความเสียหาย (ดาเมจ) ของเสาคู่ (ปกติ: index -> index + 1)
    /// </summary>
    protected virtual void CheckPillarDamage(FencePillar pillar, int pillarIdx, List<FencePillar> snapshot, Vector3 pillarPos)
    {
        int nextIdx = pillarIdx + 1;
        if (nextIdx < snapshot.Count)
        {
            var other = snapshot[nextIdx];
            if (other.go != null)
            {
                CheckAndApplyDamage(pillar, other, pillarPos);
            }
        }
    }

    protected void CheckAndApplyDamage(FencePillar a, FencePillar b, Vector3 posA)
    {
        if (a.go == null || b.go == null) return;

        Vector3 posB = b.go.transform.position;
        float dist = Vector3.Distance(posA, posB);

        // ใช้ระยะการขึงเลเซอร์จริง
        if (dist > a.linkRange) return;

        // ยิง damage ตาม beam (Line AoE)
        Vector3 dir    = posB - posA;
        float   dmg    = RollDamage(a.damage, out bool isCrit);
        float   length = dir.magnitude;

        if (length > 0.1f)
        {
            FireLineAoE(posA + Vector3.up * 0.5f, dir.normalized,
                dmg, length, beamWidth, isCrit);
        }
    }

    public void SpawnLocalPillarVisualOnly(Vector3 position, float lifetime)
    {
        // ดึงขีดจำกัดจากเลเวลปัจจุบัน
        int maxPillars = 8;
        if (data != null)
        {
            var ld = data.GetLevelData(currentLevel);
            maxPillars = GetMaxPillars(ld);
        }

        // Enforce max local visuals
        while (_localVisualsOnly.Count >= maxPillars)
        {
            var oldest = _localVisualsOnly[0];
            _localVisualsOnly.RemoveAt(0);
            if (oldest != null) Destroy(oldest);
        }

        GameObject go = pillarPrefab != null
            ? Instantiate(pillarPrefab, position, Quaternion.identity)
            : CreateFallbackPillar(position);

        if (go != null)
        {
            _localVisualsOnly.Add(go);
            _activeVisualPillars.Add(go);
            StartCoroutine(DestroyLocalVisualAfter(go, lifetime));
        }
    }

    IEnumerator DestroyLocalVisualAfter(GameObject go, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (go != null)
        {
            _localVisualsOnly.Remove(go);
            _activeVisualPillars.Remove(go);
            Destroy(go);
        }
    }

    void OnDestroy()
    {
        foreach (var p in _pillars)
            if (p.go != null) Destroy(p.go);
        _pillars.Clear();

        foreach (var go in _localVisualsOnly)
            if (go != null) Destroy(go);
        _localVisualsOnly.Clear();

        foreach (var beam in _activeBeams)
            if (beam.beamGo != null) Destroy(beam.beamGo);
        _activeBeams.Clear();
    }

    static GameObject CreateFallbackPillar(Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.position   = pos + Vector3.up * 1f;
        go.transform.localScale = new Vector3(0.4f, 2f, 0.4f);

        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = new Color(0.3f, 0.9f, 1f);
            mr.material = mat;
        }
        return go;
    }

    protected class FencePillar
    {
        public GameObject go;
        public float      spawnTime;
        public float      lifetime;
        public float      damage;
        public float      linkRange;    // ระยะเชื่อมเสา (จาก WeaponData range × Area stat)
    }
}
