using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

public abstract class SpawnAoEActionBase : BossAction
{
    public enum TargetingMode { BossPosition, RandomPlayer, AllPlayers, NearestPlayer, StaticCoords, ArenaAnchor }

    [Header("Targeting")]
    public TargetingMode targetingMode = TargetingMode.BossPosition;
    [Tooltip("ค่า offset ทิศทางที่บวกเพิ่มจากพิกัดเป้าหมาย (แกน XZ)")]
    public Vector3 targetOffset = Vector3.zero;

    [Header("Arena Anchor  (targetingMode = ArenaAnchor)")]
    [Tooltip("จุดยึดในสนาม — ต้องผูก BossEncounterConfig.arena ด้วย")]
    public ArenaAnchor arenaAnchor = global::ArenaAnchor.Center;
    [Tooltip("1 = ขอบสนาม · 0.5 = ครึ่งทาง (Center ไม่สนค่านี้)")]
    public float arenaDistanceScale = 1f;
    [Tooltip("ใช้เมื่อ rollName เป็น RollKind.Anchor — roll จะเลือก 1 ตัวจากลิสต์นี้แทน arenaAnchor\n" +
             "ว่าง = ใช้ arenaAnchor ตัวเดียวเสมอ")]
    public ArenaAnchor[] anchorChoices = new ArenaAnchor[0];

    [Header("Repeat")]
    [Tooltip("ยิงซ้ำกี่ครั้งในท่าเดียว — 1 = ครั้งเดียวเหมือนเดิม")]
    [Min(1)] public int repeatCount = 1;
    [Tooltip("เว้นกี่วินาทีระหว่างแต่ละครั้ง")]
    [Min(0f)] public float repeatInterval = 0.6f;
    [Tooltip("สุ่ม roll ใหม่ทุกครั้งที่ซ้ำ — คู่กับ RollDefinition.excludePrevious จะได้ 'ไล่ไม่ซ้ำที่'\n" +
             "ปิดไว้ = ทั้งชุดใช้ค่า roll เดียวกัน (แพตเทิร์นเดียวยิงรัว)")]
    public bool rerollEachRepeat = false;

    [Header("Telegraph Properties")]
    public float warningDuration = 2.5f;
    public float damage = 25f;
    [Tooltip("VFX ตอนระเบิด — key ใน VFXDatabase · ว่าง = ใช้ detonateVfxPrefab บน telegraph prefab\n" +
             "telegraph prefab มีตัวเดียวใช้ร่วมทั้งเกม ถ้าไม่ตั้งตรงนี้ทุก AoE จะระเบิดหน้าตาเหมือนกันหมด")]
    public string detonateVfxKey = "";

    [Header("FFXIV Special Settings")]
    [Tooltip("เปิดให้ท่าโจมตีรูปแบบนี้วิ่งตามล่าผู้เล่นเป้าหมาย (Chase)")]
    public bool isChasing = false;
    [Tooltip("ให้จุดศูนย์กลางขยับตามตัวบอสผู้ปล่อยท่าตลอดเวลา")]
    public bool followCaster = false;
    [Tooltip("หมุนทิศทางตามตัว player แทนการขยับจุดศูนย์กลางไปทับตัว player (ใช้คู่กับ isChasing)")]
    public bool isRotatingChase = false;
    public bool isStackMarker = false;
    public bool isGaze = false;

    [Header("Knockback")]
    [Tooltip("ทิศทางการผลัก — ดูคำอธิบายแต่ละแบบใน KnockbackMode.cs")]
    public KnockbackMode knockbackMode = KnockbackMode.FromCenter;
    [Tooltip("ระยะผลักเป็นหน่วยระยะทาง (0 = ไม่ผลัก)\n" +
             "หมายเหตุ: ฟิลด์นี้เดิมคือ 'แรง' — ทุก asset เดิมตั้งไว้ 0 จึงไม่ต้องแปลงค่า")]
    [FormerlySerializedAs("knockbackForce")]
    [Min(0f)] public float knockbackDistance = 0f;
    [Tooltip("ระยะเวลาที่ผู้เล่นถูกผลัก (วินาที) — ความเร็วคำนวณจาก ระยะ ÷ เวลา")]
    [Min(0f)] public float knockbackDuration = 0.2f;
    [Tooltip("ใช้เฉพาะ KnockbackMode.FixedDirection — ทิศในพิกัดโลก (คิดเฉพาะแกน XZ)")]
    public Vector3 knockbackFixedDirection = Vector3.forward;

    [Header("Expanding / Sweeping")]
    [Tooltip("ตัวคูณขนาดตอนเริ่ม telegraph — 1 = ขนาดเต็มตั้งแต่แรก (พฤติกรรมเดิม)")]
    [Min(0f)] public float scaleStart = 1f;
    [Tooltip("ตัวคูณขนาดตอนระเบิด — >1 = วงขยาย · <1 = วงหด\n" +
             "หมายเหตุ: ดาเมจยัง resolve ครั้งเดียวตอนจบ ใช้ขนาด ณ วินาทีนั้น (ดู ADR-003)")]
    [Min(0f)] public float scaleEnd = 1f;
    [Tooltip("หมุน zone กี่องศาต่อวินาทีระหว่าง telegraph — ลำแสงกวาด · 0 = ไม่หมุน")]
    public float sweepDegreesPerSecond = 0f;

    protected abstract AoEType GetAoEType();
    protected abstract void ConfigureTelegraphZone(TelegraphZone zone);

    // warning + ช่วง resolve สั้นๆ หลัง telegraph ระเบิด × จำนวนครั้งที่ยิงซ้ำ
    public override float GetEditorDuration()
        => actionDelay + Mathf.Max(0, repeatCount - 1) * repeatInterval + warningDuration + 0.5f;

    public override IEnumerator ExecuteCoroutine(NetworkBehaviour runner, GameObject telegraphPrefab)
    {
        if (actionDelay > 0f)
        {
            yield return new WaitForSeconds(actionDelay);
        }

        if (telegraphPrefab == null)
        {
            Debug.LogWarning($"[{GetType().Name}] telegraphPrefab is null!");
            yield break;
        }

        int shots = Mathf.Max(1, repeatCount);
        for (int shot = 0; shot < shots; shot++)
        {
            // ซ้ำครั้งถัดไปสุ่มใหม่ได้ — ครั้งแรกใช้ค่าที่ AttackLoop roll มาให้แล้ว
            if (shot > 0 && rerollEachRepeat && !string.IsNullOrEmpty(rollName))
                (runner as BossController)?.Rolls?.Roll(rollName);

            SpawnOneWave(runner, telegraphPrefab);

            if (shot < shots - 1 && repeatInterval > 0f)
                yield return new WaitForSeconds(repeatInterval);
        }
    }

    private void SpawnOneWave(NetworkBehaviour runner, GameObject telegraphPrefab)
    {
        // roll ครั้งเดียวต่อระลอก แล้วใช้ transform เดียวกันกับทุกจุดเกิด
        // ถ้าแปลงแยกทีละจุด แพตเทิร์น AllPlayers จะกลายเป็นมั่วแทนลวดลายที่อ่านออก
        RollTransform rollTf = GetRollTransform(runner);

        List<Vector3> spawnPositions = GetSpawnPositions(runner);
        foreach (var rawPos in spawnPositions)
        {
            Vector3 pos = rollTf.Apply(rawPos);

            // คำนวณทิศทางการหันหน้า: หากยิงใส่เป้าหมาย หรือหันไปทางเป้าหมาย
            Quaternion rot = Quaternion.identity;
            if (GetAoEType() == AoEType.Line || GetAoEType() == AoEType.Cone)
            {
                // หมุนไปทางผู้เล่นที่ใกล้ที่สุดหรือเป้าหมายเพื่อให้พาดผ่านตัว
                Transform nearestPlayer = FindNearestPlayer(pos);
                if (nearestPlayer != null)
                {
                    Vector3 dir = (nearestPlayer.position - pos).normalized;
                    dir.y = 0f;
                    if (dir != Vector3.zero) rot = Quaternion.LookRotation(dir);
                }
            }
            else
            {
                // ทรงที่ไม่หันตามใคร ให้ roll หมุน/พลิกทิศได้
                rot = rollTf.Apply(rot);
            }

            var go = Instantiate(telegraphPrefab, pos, rot);
            var zone = go.GetComponent<TelegraphZone>();
            var no = go.GetComponent<NetworkObject>();

            if (zone != null && no != null)
            {
                zone.aoeType = GetAoEType();
                zone.scaleStart = scaleStart;
                zone.scaleEnd = scaleEnd;
                zone.sweepDegreesPerSecond = sweepDegreesPerSecond;
                zone.warningDuration = warningDuration;
                zone.damage = damage;
                zone.isChasing = isChasing;
                zone.isRotatingChase = isRotatingChase;
                zone.isStackMarker = isStackMarker;
                zone.isGaze = isGaze;
                zone.knockbackMode = knockbackMode;
                zone.knockbackDistance = knockbackDistance;
                zone.knockbackDuration = knockbackDuration;
                zone.knockbackFixedDirection = knockbackFixedDirection;
                zone.detonateVfxKey = detonateVfxKey;

                if (followCaster && runner != null)
                {
                    zone.casterNetworkObject = runner.NetworkObject;
                }

                ConfigureTelegraphZone(zone);

                no.Spawn(true);
                (runner as BossController)?.RegisterMechanic(no);
                zone.BroadcastInit();
            }
            else
            {
                Destroy(go);
            }
        }
    }

    private List<Vector3> GetSpawnPositions(NetworkBehaviour runner)
    {
        var list = new List<Vector3>();
        Vector3 basePos = runner.transform.position;

        switch (targetingMode)
        {
            case TargetingMode.BossPosition:
                list.Add(basePos + targetOffset);
                break;
            case TargetingMode.RandomPlayer:
            {
                // เดิมไม่กรอง isDead ทั้งที่ AllPlayers กรอง — สุ่มติดศพแล้ววงไปลงที่ศพ
                // บั๊กตระกูลเดียวกับ tether ที่แก้ไปแล้ว
                var alive = CollectAlivePlayerPositions(basePos.y);
                if (alive.Count > 0)
                {
                    // RollKind.Target ให้ roll เป็นคนเลือก จะได้ reproduce ตาม seed ได้
                    int idx = TryGetTargetRoll(runner, alive.Count, out int rolled)
                        ? rolled
                        : Random.Range(0, alive.Count);
                    list.Add(alive[idx] + targetOffset);
                }
                else
                {
                    list.Add(basePos + targetOffset);
                }
                break;
            }
            case TargetingMode.NearestPlayer:
                Transform nearest = FindNearestPlayer(basePos);
                if (nearest != null)
                {
                    Vector3 pos = nearest.position;
                    pos.y = basePos.y;
                    list.Add(pos + targetOffset);
                }
                else
                {
                    list.Add(basePos + targetOffset);
                }
                break;
            case TargetingMode.AllPlayers:
                if (NetworkManager.Singleton != null)
                {
                    foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
                    {
                        if (c.PlayerObject != null)
                        {
                            var pm = c.PlayerObject.GetComponent<playermove>();
                            if (pm != null && pm.isDead.Value) continue;

                            Vector3 pos = c.PlayerObject.transform.position;
                            pos.y = basePos.y;
                            list.Add(pos + targetOffset);
                        }
                    }
                }
                if (list.Count == 0)
                {
                    list.Add(basePos + targetOffset);
                }
                break;
            case TargetingMode.StaticCoords:
                list.Add(targetOffset);
                break;

            case TargetingMode.ArenaAnchor:
                list.Add(ResolveAnchorPosition(runner) + targetOffset);
                break;
        }

        return list;
    }

    /// <summary>
    /// จุดยึดในสนาม · ถ้า roll เป็น RollKind.Anchor และมี anchorChoices ให้ roll เลือกจากลิสต์
    /// (เลือกจากลิสต์ที่ designer ตั้ง ไม่ใช่ index ดิบของ enum — ไม่งั้นจะได้จุดมั่วซั่ว)
    /// </summary>
    private Vector3 ResolveAnchorPosition(NetworkBehaviour runner)
    {
        var boss = runner as BossController;
        ArenaDefinition arena = boss?.config?.arena;

        ArenaAnchor chosen = arenaAnchor;
        if (anchorChoices != null && anchorChoices.Length > 0 && boss?.Rolls != null
            && !string.IsNullOrEmpty(rollName)
            && boss.Rolls.GetKind(rollName) == RollKind.Anchor)
        {
            int value = boss.Rolls.Peek(rollName);
            if (value >= 0) chosen = anchorChoices[value % anchorChoices.Length];
        }

        if (arena == null)
        {
            Debug.LogWarning($"[{GetType().Name}] {name}: targetingMode = ArenaAnchor แต่ BossEncounterConfig.arena ว่าง — ใช้ตำแหน่งบอสแทน");
            return runner != null ? runner.transform.position : Vector3.zero;
        }

        return ArenaAnchors.Resolve(arena, chosen, arenaDistanceScale);
    }

    private List<Vector3> CollectAlivePlayerPositions(float y)
    {
        var list = new List<Vector3>();
        if (NetworkManager.Singleton == null) return list;

        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (c.PlayerObject == null) continue;
            var pm = c.PlayerObject.GetComponent<playermove>();
            if (pm == null || pm.isDead.Value) continue;

            Vector3 p = c.PlayerObject.transform.position;
            p.y = y;
            list.Add(p);
        }
        return list;
    }

    private bool TryGetTargetRoll(NetworkBehaviour runner, int count, out int index)
    {
        index = 0;
        var boss = runner as BossController;
        if (boss?.Rolls == null || string.IsNullOrEmpty(rollName)) return false;
        if (boss.Rolls.GetKind(rollName) != RollKind.Target) return false;

        int value = boss.Rolls.Peek(rollName);
        if (value < 0) return false;

        index = value % count;
        return true;
    }
}
