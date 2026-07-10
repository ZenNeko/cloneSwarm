using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fence — วางเสาที่ตำแหน่ง player → เสาที่ใกล้กันเชื่อมกัน → beam สร้าง damage ใส่ enemy ที่ผ่าน
///
/// กลไก:
///   • OnFire → Instantiate เสา (local visual) ที่ตำแหน่ง player
///   • แต่ละ frame: เสาคู่ที่อยู่ใน linkRadius → วาด visual beam
///   • ทุก tick interval → FireLineAoE ตาม beam → enemy โดน damage
///   • เสามี lifetime → Destroy เมื่อหมดอายุ
///
/// Level data แนะนำ:
///   Lv1: dmg=15/tick, cd=4.0s, count=1, range=8
///   Lv2: dmg=20/tick, cd=3.5s, count=1, range=9
///   Lv3: dmg=25/tick, cd=3.0s, count=2, range=10
///   Lv4: dmg=30/tick, cd=2.5s, count=2, range=11
///   Lv5: dmg=40/tick, cd=2.0s, count=3, range=12
/// </summary>
public class FenceWeapon : WeaponBase
{
    [Header("Fence Settings")]
    [Tooltip("Prefab visual ของเสา (optional — ถ้าไม่ assign ใช้ cube)")]
    public GameObject pillarPrefab;

    [Tooltip("ระยะสูงสุดที่เสาเชื่อมกันได้")]
    public float linkRadius = 10f;

    [Tooltip("อายุเสา (วินาที) — scale ตาม Duration stat")]
    public float pillarLifetime = 8f;

    [Tooltip("ทุกกี่วินาทีที่ beam ทำ damage")]
    public float tickInterval = 0.5f;

    [Tooltip("ความกว้างของ beam damage (ส่งให้ FireLineAoE)")]
    public float beamWidth = 1.5f;

    [Tooltip("จำนวนเสาสูงสุดที่อยู่พร้อมกัน")]
    public int maxPillars = 8;

    private readonly List<FencePillar> _pillars = new();

    protected override void OnFire(WeaponLevelData ld)
    {
        int count = Mathf.Max(1, ld.projectileCount);
        for (int i = 0; i < count; i++)
        {
            // สุ่ม offset เล็กน้อยรอบ player (ไม่วางทับกันพอดี)
            Vector2 offset = Random.insideUnitCircle * 1.5f;
            Vector3 pos    = transform.position + new Vector3(offset.x, 0f, offset.y);
            SpawnPillar(pos, ld);
        }
    }

    void SpawnPillar(Vector3 position, WeaponLevelData ld)
    {
        // Enforce max
        while (_pillars.Count >= maxPillars)
        {
            var oldest = _pillars[0];
            _pillars.RemoveAt(0);
            if (oldest.go != null) Destroy(oldest.go);
        }

        // Visual
        GameObject go = pillarPrefab != null
            ? Instantiate(pillarPrefab, position, Quaternion.identity)
            : CreateFallbackPillar(position);

        float durationMult = manager.statManager != null ? manager.statManager.GetDurationMultiplier() : 1f;
        float lifetime     = pillarLifetime * durationMult;

        var pillar = new FencePillar
        {
            go       = go,
            position = position,
            spawnTime = Time.time,
            lifetime  = lifetime,
            damage    = ld.damage,
        };
        _pillars.Add(pillar);

        // เริ่ม tick damage coroutine
        StartCoroutine(PillarTickCoroutine(pillar, ld));
    }

    IEnumerator PillarTickCoroutine(FencePillar pillar, WeaponLevelData ld)
    {
        while (Time.time - pillar.spawnTime < pillar.lifetime)
        {
            yield return new WaitForSeconds(tickInterval);

            if (pillar.go == null) break;

            // หาเสาอื่นที่ยังอยู่ + อยู่ใน link radius
            foreach (var other in _pillars)
            {
                if (other == pillar || other.go == null) continue;
                float dist = Vector3.Distance(pillar.position, other.position);
                if (dist > linkRadius) continue;

                // ยิง damage ตาม beam (Line AoE)
                Vector3 dir    = other.position - pillar.position;
                float   dmg    = RollDamage(ld.damage, out bool isCrit);
                float   length = dir.magnitude;

                if (length > 0.1f)
                {
                    FireLineAoE(pillar.position + Vector3.up * 0.5f, dir.normalized,
                        dmg, length, beamWidth, isCrit);

                    // VFX beam (ถ้ามี)
                    ShowVfx(ResolveHitVfx("Beam_Laser"),
                        (pillar.position + other.position) * 0.5f + Vector3.up * 0.5f,
                        length * 0.5f, isCrit, isAttackHit: false,
                        direction: dir.normalized);
                }
            }
        }

        // Cleanup
        _pillars.Remove(pillar);
        if (pillar.go != null) Destroy(pillar.go);
    }

    void OnDestroy()
    {
        foreach (var p in _pillars)
            if (p.go != null) Destroy(p.go);
        _pillars.Clear();
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

    private class FencePillar
    {
        public GameObject go;
        public Vector3    position;
        public float      spawnTime;
        public float      lifetime;
        public float      damage;
    }
}
