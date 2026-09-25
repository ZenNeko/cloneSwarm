using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

public abstract class SpawnAoEActionBase : BossAction
{
    public enum TargetingMode { BossPosition, RandomPlayer, AllPlayers, NearestPlayer, StaticCoords, ArenaAnchor }

    /// <summary>ทิศของ Line / Cone — Cross กับทรงกลมไม่ได้หันหาใคร ใช้ angleDegrees ตรงๆ</summary>
    public enum AimMode { NearestPlayer, FixedAngle }

    [Header("Targeting")]
    public TargetingMode targetingMode = TargetingMode.BossPosition;
    [Tooltip("ค่า offset ทิศทางที่บวกเพิ่มจากพิกัดเป้าหมาย (แกน XZ)")]
    public Vector3 targetOffset = Vector3.zero;

    [Header("Direction  (Line / Cone / Cross)")]
    [Tooltip("Line / Cone: NearestPlayer = หันหาผู้เล่นใกล้จุดเกิดที่สุด (ค่าเดิม) · FixedAngle = ใช้ angleDegrees\n" +
             "Cross ไม่สนช่องนี้ — ใช้ angleDegrees เสมอ")]
    public AimMode aimMode = AimMode.NearestPlayer;
    [Tooltip("มุม (องศา) · 0 = เหนือ (+Z) · บวก = ตามเข็มนาฬิกา · Cross 45 = รูป ×\n" +
             "roll แบบหมุน/พลิกทำต่อจากมุมนี้ (ยกเว้น Line/Cone ที่หันหาผู้เล่น)")]
    public float angleDegrees = 0f;

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
    [Tooltip("VFX ตอนระเบิด (ADR-006 — ลาก VFXAsset ตรงๆ แทน string key เดิม) · ว่าง = ใช้ detonateVfxPrefab บน telegraph prefab\n" +
             "telegraph prefab มีตัวเดียวใช้ร่วมทั้งเกม ถ้าไม่ตั้งตรงนี้ทุก AoE จะระเบิดหน้าตาเหมือนกันหมด")]
    public VFXAsset detonateVfx;

    [Header("Telegraph Colors  (ปกติไม่ต้องแตะ)")]
    // สีปกติมาจาก palette บน TelegraphZone prefab ตามหมวดกลไก (Gaze / Stack / Chase / default)
    // ให้สี = ความหมาย ผู้เล่นเห็นสีม่วงก็รู้ทันทีว่าต้องหันหลัง โดยไม่ต้องจำว่าเป็นท่าไหน
    // ช่องนี้ไว้สำหรับท่าพิเศษที่จงใจให้หลุดจากภาษาสีกลาง — ใช้บ่อยเมื่อไหร่แปลว่าควรเพิ่มหมวดใหม่แทน
    [Tooltip("ทับสีจาก palette กลาง — ใช้เฉพาะท่าพิเศษ")]
    public bool  overrideTelegraphColors = false;
    [Tooltip("สีตอนเริ่ม telegraph")]
    public Color telegraphWarningColor = new Color(1f, 0.64f, 0.024f, 0.5f);
    [Tooltip("สีตอนใกล้ระเบิด")]
    public Color telegraphDangerColor  = new Color(1f, 0f, 0.099f, 0.85f);
    [Tooltip("ทับสีขอบแยกจากสีพื้น — ปิด = ขอบใช้สีอันตรายของหมวดเดียวกัน (Gaze ขอบม่วง Stack ขอบฟ้า)\n" +
             "แยก gate จากสีพื้นเพราะคนละเจตนา: ทับสีพื้น = ท่าหลุดจากภาษาสีกลาง · ทับสีขอบ = แค่ให้ขอบเด่นบนพื้นบางแบบ")]
    public bool  overrideTelegraphOutlineColor = false;
    [Tooltip("สีแถบขอบ")]
    public Color telegraphOutlineColor = Color.white;

    [Header("Telegraph Effects  (ปกติไม่ต้องแตะ)")]
    // หน้าตาปกติมาจาก material — ช่องนี้ไว้ทำท่าที่จงใจให้เงียบกว่าหรือดังกว่าปกติ
    // เช่นท่าที่ยิงรัวๆ ควรลด blink ลง ไม่งั้นจอกะพริบจนอ่านอะไรไม่ออก
    //
    // **ค่า default ทุกช่องตรงกับ Mat_Tele_Universal** — ติ๊กเปิดแล้วหน้าตายังเหมือนเดิม
    // จนกว่าจะลงมือปรับจริง · ถ้าไม่ทำแบบนี้ การติ๊กเปิดเพื่อแก้ค่าเดียวจะเผลอรีเซ็ตอีกสิบค่า
    //
    // ข้อยกเว้น — asset ที่สร้างก่อน 2026-08-12 เก็บ `ringAmount: 1` / `ringSpeed: 2` ไว้แล้ว
    // (ค่า default เดิมของโค้ด) Unity จะไม่เขียนทับให้ · ถ้าจะเปิด override บน asset เก่า
    // ให้ตั้งสองช่องนี้เป็น 3 / 1 เองเพื่อให้ตรงกับ material
    [Tooltip("ทับหน้าตา telegraph ทั้งชุดจาก material — ใช้เฉพาะท่าพิเศษ")]
    public bool  overrideTelegraphEffects = false;
    [Tooltip("จังหวะเต้นของ alpha · 0 = ปิด")]
    [Range(0f, 2f)] public float pulseAmount = 1f;
    [Tooltip("การกระพริบ · 0 = ปิด")]
    [Range(0f, 2f)] public float blinkAmount = 1f;
    [Tooltip("ความสว่างของวงที่ไหลออก · 0 = ปิด")]
    // เพดานเดิมคือ 3 ซึ่งพอดีกับค่าที่ material ตั้งไว้ — สไลเดอร์จะติดสุดตั้งแต่ค่า default
    // ขยายเป็น 5 ให้มีที่ให้ดันขึ้นได้จริง
    [Range(0f, 5f)] public float ringAmount = 3f;
    [Tooltip("ความเร็ววง (เมตร/วินาที) · shader คูณ (1 + FillProgress) ให้เร็วขึ้นเองเมื่อใกล้ระเบิด")]
    [Min(0f)] public float ringSpeed = 1f;
    [Tooltip("ความถี่ pulse (รอบ/วินาที)")]
    [Min(0f)] public float pulseSpeed = 4f;
    [Tooltip("ระยะห่างระหว่างวง (เมตร) · ยิ่งน้อยยิ่งถี่")]
    [Min(0.01f)] public float ringSpacing = 1.5f;
    [Tooltip("ความหนาของแต่ละวง (เมตร)")]
    [Min(0f)] public float ringWidth = 0.15f;
    [Tooltip("ความหนาแถบขอบ (เมตร)")]
    [Min(0f)] public float outlineWidth = 0.15f;
    [Tooltip("ความเรืองของขอบ")]
    [Min(0f)] public float edgeGlow = 1.5f;
    [Tooltip("ความคมของขอบ · สูง = ขอบชัดเป็นเส้น · ต่ำ = ฟุ้ง")]
    [Min(0f)] public float edgeStrength = 2f;
    [Tooltip("ความทึบรวมของ zone · ลดลงเมื่อมีหลาย zone ซ้อนกันจนอ่านพื้นไม่ออก")]
    [Range(0f, 1f)] public float baseAlpha = 1f;

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

    // หัวข้อกลุ่มวาดโดย SpawnAoEActionEditor (เป็น foldout) — ไม่ใส่ [Header] ซ้ำ
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
    // ตรงกับที่ ExecuteCoroutine รอจริง (TelegraphZone ระเบิดที่ warningDuration แล้ว despawn อีก 0.1s)
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

            SpawnOneWave(runner, telegraphPrefab, AoEWorld.FromRunner(runner));

            if (shot < shots - 1 && repeatInterval > 0f)
                yield return new WaitForSeconds(repeatInterval);
        }

        // รอให้ระลอกสุดท้ายระเบิดก่อนคืนค่า — ดูสัญญาใน BossAction.ExecuteCoroutine
        //
        // ของเดิมคืนทันทีที่ spawn เสร็จ ซึ่งไม่เป็นไรตอนที่ timeline รอด้วยนาฬิกา
        // แต่พอ timeline มารอ coroutine จริง ท่าที่คืนเร็วจะทำให้บอสขึ้นรอบใหม่
        // ตอนที่วงยังนับถอยหลังอยู่ · LimitCutAction ทำแบบนี้อยู่แล้วตั้งแต่แรก
        float warn = WarningFor(runner);
        if (warn > 0f) yield return new WaitForSeconds(warn);
    }

    /// <summary>เวลาเตือนจริงตามระดับความยาก — ทั้งตัว zone และการรอของท่าต้องใช้ค่านี้ค่าเดียว</summary>
    protected float WarningFor(NetworkBehaviour runner) => warningDuration * TuningOf(runner).bossWarningMult;

    /// <summary>
    /// จุดเกิด + ทิศของโซนทั้งหมดในหนึ่งระลอก
    ///
    /// **นิยามเดียวของ "ท่านี้ลงตรงไหน"** — ตอนยิงจริงเรียกผ่าน SpawnOneWave
    /// ตอนวาดพรีวิวใน Boss Designer เรียกตรงๆ ด้วย AoEWorld ที่ประกอบจากผู้เล่นสมมติ
    /// ถ้าแยกเป็นสองสูตร ภาพที่วาดจะเพี้ยนจากของจริงทันทีที่ใครแก้ข้างเดียว
    /// </summary>
    public List<(Vector3 pos, Quaternion rot)> ResolveWave(in AoEWorld world)
    {
        var result = new List<(Vector3, Quaternion)>();

        // roll ครั้งเดียวต่อระลอก แล้วใช้ transform เดียวกันกับทุกจุดเกิด
        // ถ้าแปลงแยกทีละจุด แพตเทิร์น AllPlayers จะกลายเป็นมั่วแทนลวดลายที่อ่านออก
        RollTransform rollTf = GetRollTransform(world);

        // จุดเกิดที่ผูกกับสนาม (พิกัดตายตัว / จุดในสนาม) หมุนรอบกลางสนาม — แพตเทิร์นสนามพลิก/หมุนทั้งผืน
        // จุดเกิดที่ผูกกับตัวคน (บอส / ผู้เล่น) อยู่ที่ตัวคนเสมอ หมุนแค่ targetOffset
        // เดิมหมุนทุกจุดรอบกลางสนาม: บอสไม่ได้ยืนกลางสนาม → กากบาท roll 45° ไปโผล่ห่างจากตัวบอส
        // (พรีวิววาดบอสไว้กลางสนามพอดี จึงไม่เคยเห็นอาการนี้ใน editor)
        bool arenaRelative = targetingMode == TargetingMode.StaticCoords || targetingMode == TargetingMode.ArenaAnchor;
        Vector3 offsetFix = rollTf.ApplyVector(targetOffset) - targetOffset;

        foreach (var rawPos in GetSpawnPositions(world))
        {
            Vector3 pos = arenaRelative ? rollTf.Apply(rawPos) : rawPos + offsetFix;

            // คำนวณทิศทางการหันหน้า: หากยิงใส่เป้าหมาย หรือหันไปทางเป้าหมาย
            Quaternion rot = Quaternion.identity;
            bool aimsAtPlayer = (GetAoEType() == AoEType.Line || GetAoEType() == AoEType.Cone)
                                && aimMode == AimMode.NearestPlayer;
            if (aimsAtPlayer)
            {
                // หมุนไปทางผู้เล่นที่ใกล้ที่สุดหรือเป้าหมายเพื่อให้พาดผ่านตัว
                if (world.TryNearestPlayer(pos, out Vector3 near))
                {
                    Vector3 dir = near - pos;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.0001f) rot = Quaternion.LookRotation(dir.normalized);
                }
            }
            else
            {
                // ทรงที่ไม่หันตามใคร — มุมที่ตั้งไว้ แล้วให้ roll หมุน/พลิกต่อจากนั้น
                // (ค่า default 0° = identity เหมือนเดิม ท่าเก่าไม่เปลี่ยน)
                rot = rollTf.Apply(Quaternion.Euler(0f, angleDegrees, 0f));
            }

            result.Add((pos, rot));
        }

        return result;
    }

    private void SpawnOneWave(NetworkBehaviour runner, GameObject telegraphPrefab, in AoEWorld world)
    {
        foreach (var (pos, rot) in ResolveWave(world))
            SpawnZoneAt(runner, telegraphPrefab, pos, rot);
    }

    /// <summary>
    /// สร้าง TelegraphZone หนึ่งลูกตรงตำแหน่ง/มุมที่กำหนดตรงๆ — ใช้เมื่อ subclass มี logic หาตำแหน่งเอง
    /// ไม่ผ่าน GetSpawnPositions()/targetingMode (เช่น KeepMovingAction ยิงตามจุดที่ผู้เล่นยืนนิ่ง,
    /// LimitCutAction ยิงตามคิวเลข, ColorMatchAoEAction ยิงตามผู้เล่นแต่ละคน)
    ///
    /// รวม plumbing ที่เดิมเคย copy-paste ในทุก subclass (Instantiate/GetComponent/คัดลอกคุณสมบัติ
    /// telegraph ทั้งชุด/Spawn/RegisterMechanic/BroadcastInit) ไว้จุดเดียว — subclass ที่เรียกจุดนี้
    /// จะได้ผลตอบรับ scaleStart/scaleEnd/knockback/สี/effect override ครบเหมือนกับ SpawnOneWave ด้วย
    /// (เดิมสามตัวนี้ตั้งได้แค่ warningDuration/damage/detonateVfx เท่านั้น)
    ///
    /// หมายเหตุ: **ไม่** ผ่าน RollTransform ให้ตำแหน่ง — ตำแหน่งพวกนี้มาจาก logic เฉพาะของ mechanic
    /// (ตำแหน่งผู้เล่นจริง ณ ขณะนั้น) การพลิก/หมุนแบบ roll เชิงพื้นที่จะทำให้วงไปเกิดผิดที่จากที่ mechanic ตั้งใจ
    /// ถ้าต้องการ roll เชิงพื้นที่ ผู้เรียกต้องแปลงตำแหน่งเองก่อนส่งเข้ามา (ดู SpawnOneWave)
    /// </summary>
    /// <param name="preSpawnConfigure">เรียกหลัง ConfigureTelegraphZone แต่ก่อน Spawn — ใช้ตั้งค่า
    /// NetworkVariable ที่ต้องถูกต้องตั้งแต่ snapshot แรกที่ client เห็น (เช่น ColorMatch ต้องตั้งสี
    /// ก่อนยิง Spawn ไม่งั้น client จะเห็นค่า default วูบหนึ่งเฟรมก่อนค่าจริงตามมา)</param>
    protected TelegraphZone SpawnZoneAt(NetworkBehaviour runner, GameObject telegraphPrefab, Vector3 pos, Quaternion rot, System.Action<TelegraphZone> preSpawnConfigure = null)
    {
        if (telegraphPrefab == null) return null;

        var go = Instantiate(telegraphPrefab, pos, rot);
        var zone = go.GetComponent<TelegraphZone>();
        var no = go.GetComponent<NetworkObject>();

        if (zone == null || no == null)
        {
            Destroy(go);
            return null;
        }

        zone.aoeType = GetAoEType();
        zone.scaleStart = scaleStart;
        zone.scaleEnd = scaleEnd;
        zone.sweepDegreesPerSecond = sweepDegreesPerSecond;
        var tuning = TuningOf(runner);
        zone.warningDuration = warningDuration * tuning.bossWarningMult;
        zone.damage = damage * tuning.bossDamageMult;
        zone.isChasing = isChasing;
        zone.isRotatingChase = isRotatingChase;
        zone.isStackMarker = isStackMarker;
        zone.isGaze = isGaze;
        zone.knockbackMode = knockbackMode;
        zone.knockbackDistance = knockbackDistance;
        zone.knockbackDuration = knockbackDuration;
        zone.knockbackFixedDirection = knockbackFixedDirection;
        zone.detonateVfxId = ResolveVfxId(detonateVfx);
        zone.overrideColors = overrideTelegraphColors;
        zone.overrideWarningColor = telegraphWarningColor;
        zone.overrideDangerColor  = telegraphDangerColor;
        zone.overrideOutlineColorFlag = overrideTelegraphOutlineColor;
        zone.overrideOutlineColor     = telegraphOutlineColor;
        zone.overrideEffects      = overrideTelegraphEffects;
        zone.overridePulseAmount  = pulseAmount;
        zone.overrideBlinkAmount  = blinkAmount;
        zone.overrideRingAmount   = ringAmount;
        zone.overrideRingSpeed    = ringSpeed;
        zone.overridePulseSpeed   = pulseSpeed;
        zone.overrideRingSpacing  = ringSpacing;
        zone.overrideRingWidth    = ringWidth;
        zone.overrideOutlineWidth = outlineWidth;
        zone.overrideEdgeGlow     = edgeGlow;
        zone.overrideEdgeStrength = edgeStrength;
        zone.overrideBaseAlpha    = baseAlpha;

        if (followCaster && runner != null)
        {
            zone.casterNetworkObject = runner.NetworkObject;
        }

        ConfigureTelegraphZone(zone);
        preSpawnConfigure?.Invoke(zone);

        no.Spawn(true);
        (runner as BossController)?.RegisterMechanic(no);
        zone.BroadcastInit();

        return zone;
    }

    /// <summary>
    /// แปลง VFXAsset เป็น id เสถียร (ADR-006) ผ่าน VFXDatabase ที่ NetworkedVFXPool ถืออยู่
    /// asset == null → GetIdForAsset คืน -1 อยู่แล้ว (เทียบเท่า "ไม่มี VFX" ของทางเดิม) ไม่ต้องเช็คซ้ำ
    /// -1 ก็คืนเมื่อหา NetworkedVFXPool/vfxDatabase ไม่เจอ (ยังไม่ spawn ในฉากนี้ ฯลฯ)
    /// </summary>
    private static int ResolveVfxId(VFXAsset asset)
    {
        var db = NetworkedVFXPool.Instance != null ? NetworkedVFXPool.Instance.vfxDatabase : null;
        return db != null ? db.GetIdForAsset(asset) : -1;
    }

    private List<Vector3> GetSpawnPositions(in AoEWorld world)
    {
        var list = new List<Vector3>();
        Vector3 basePos = world.bossPos;

        switch (targetingMode)
        {
            case TargetingMode.BossPosition:
                list.Add(basePos + targetOffset);
                break;
            case TargetingMode.RandomPlayer:
            {
                // AoEWorld กรอง isDead ให้แล้วตั้งแต่ตอนประกอบ — เดิมทางนี้ไม่กรองทั้งที่
                // AllPlayers กรอง สุ่มติดศพแล้ววงไปลงที่ศพ (บั๊กตระกูลเดียวกับ tether)
                var alive = world.alivePlayers;
                if (world.PlayerCount > 0)
                {
                    // RollKind.Target ให้ roll เป็นคนเลือก จะได้ reproduce ตาม seed ได้
                    int idx = TryGetTargetRoll(world, alive.Count, out int rolled)
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
                if (world.TryNearestPlayer(basePos, out Vector3 nearest))
                    list.Add(nearest + targetOffset);
                else
                    list.Add(basePos + targetOffset);
                break;
            case TargetingMode.AllPlayers:
                if (world.alivePlayers != null)
                    foreach (var p in world.alivePlayers)
                        list.Add(p + targetOffset);

                if (list.Count == 0)
                {
                    list.Add(basePos + targetOffset);
                }
                break;
            case TargetingMode.StaticCoords:
                list.Add(targetOffset);
                break;

            case TargetingMode.ArenaAnchor:
                list.Add(ResolveAnchorPosition(world) + targetOffset);
                break;
        }

        return list;
    }

    /// <summary>
    /// จุดยึดในสนาม · ถ้า roll เป็น RollKind.Anchor และมี anchorChoices ให้ roll เลือกจากลิสต์
    /// (เลือกจากลิสต์ที่ designer ตั้ง ไม่ใช่ index ดิบของ enum — ไม่งั้นจะได้จุดมั่วซั่ว)
    /// </summary>
    private Vector3 ResolveAnchorPosition(in AoEWorld world)
    {
        var rolls = world.rolls;

        ArenaAnchor chosen = arenaAnchor;
        if (anchorChoices != null && anchorChoices.Length > 0 && rolls != null
            && !string.IsNullOrEmpty(rollName)
            && rolls.GetKind(rollName) == RollKind.Anchor)
        {
            int value = rolls.Peek(rollName);
            if (value >= 0) chosen = anchorChoices[value % anchorChoices.Length];
        }

        if (world.arena == null)
        {
            Debug.LogWarning($"[{GetType().Name}] {name}: targetingMode = ArenaAnchor แต่ BossEncounterConfig.arena ว่าง — ใช้ตำแหน่งบอสแทน");
            return world.bossPos;
        }

        return ArenaAnchors.Resolve(world.arena, chosen, arenaDistanceScale);
    }

    private bool TryGetTargetRoll(in AoEWorld world, int count, out int index)
    {
        index = 0;
        var rolls = world.rolls;
        if (rolls == null || string.IsNullOrEmpty(rollName)) return false;
        if (rolls.GetKind(rollName) != RollKind.Target) return false;

        int value = rolls.Peek(rollName);
        if (value < 0) return false;

        index = value % count;
        return true;
    }
}
