using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// AoE warning zone — spawn โดย MainBoss
///
/// Flow (Server):
///   Spawn → InitClientRpc(type, params) → รอ warningDuration → ระเบิด → Despawn
///
/// Visual (Client):
///   สร้าง primitive ตอน Init → กระพริบตอนใกล้ระเบิด → ซ่อนตอน Despawn
/// </summary>
public class TelegraphZone : NetworkBehaviour
{

    [Header("3D Visual Prefabs — ออกแบบ scale=1 ให้มีขนาดมาตรฐาน 1 unit")]
    [Tooltip("Disc Ø1 unit (Cylinder/Plane นอนบน XZ)\n" +
             "ใช้กับ AoEType.Circle และ AoEType.Chase")]
    public GameObject circlePrefab;
    [Tooltip("Line 1×1 unit — ยาว 1 unit ทาง +Z, กว้าง 1 unit ทาง X\n" +
             "ใช้กับ AoEType.Line และ AoEType.Cross (Cross spawn 2 ตัว scale คนละแกน)")]
    public GameObject linePrefab;
    [Tooltip("Donut Ø1 unit — ห่วงนอนบน XZ\n" +
             "ใช้กับ AoEType.Donut")]
    public GameObject donutPrefab;

    [Tooltip("VFX ตอนระเบิด (impact shockwave) — ปล่อยว่างได้")]
    public GameObject detonateVfxPrefab;

    [Header("Materials (fallback primitive mode — ใช้เมื่อไม่ใส่ prefab)")]
    public Material warningMaterial;   // transparent red — assign in Inspector

    // ── Palette กลาง ──────────────────────────────────────────────────────
    // ใช้ทั้งสองเส้นทางการวาด: ส่งเข้า _WarningColor/_DangerColor ของ TelegraphUniversal
    // และใช้ lerp เองในโหมด primitive fallback (Cone ใช้ทางนี้เสมอเพราะยังไม่มี prefab)
    // action ทับเป็นรายท่าได้ผ่าน SpawnAoEActionBase.overrideTelegraphColors
    [Header("Telegraph Colors")]
    [Tooltip("สีตอนเริ่ม telegraph — default ตรงกับ _WarningColor ของ shader")]
    public Color warningColor      = new Color(1f, 0.64f, 0.024f, 0.5f);
    [Tooltip("สีตอนใกล้ระเบิด — default ตรงกับ _DangerColor ของ shader")]
    public Color dangerColor       = new Color(1f, 0f, 0.099f, 0.85f);
    [Tooltip("Chase: สีเริ่ม — แยกจาก AoE ปกติเพื่อให้อ่านออกว่านี่คือท่าไล่ตาม")]
    public Color chaseWarningColor = new Color(1f, 0.2f, 1f, 0.5f);
    [Tooltip("Chase: สีตอนใกล้ระเบิด")]
    public Color chaseDangerColor  = new Color(0.8f, 0f, 0.6f, 0.85f);
    [Tooltip("Stack: สีเริ่ม — กลไกนี้ต้องวิ่ง **เข้า** หาคนอื่น ตรงข้ามกับ AoE ปกติ")]
    public Color stackWarningColor = new Color(0.35f, 0.75f, 1f, 0.5f);
    [Tooltip("Stack: สีตอนใกล้ระเบิด")]
    public Color stackDangerColor  = new Color(0.1f, 0.45f, 1f, 0.85f);
    [Tooltip("Gaze: สีเริ่ม — ต้องหันหลัง ตำแหน่งไม่ช่วย")]
    public Color gazeWarningColor  = new Color(0.75f, 0.55f, 1f, 0.5f);
    [Tooltip("Gaze: สีตอนใกล้ระเบิด")]
    public Color gazeDangerColor   = new Color(0.5f, 0.15f, 0.9f, 0.85f);
    [Tooltip("วงในที่ปลอดภัยของ Donut")]
    public Color safeZoneColor     = new Color(0.1f, 0.8f, 0.9f, 0.3f);
    [Tooltip("ColorMatch: สีประจำ slot ผู้เล่น 0-3 — ต้องตรงกับสีที่ HUD บอก ไม่งั้นกลไกอ่านไม่ออก")]
    public Color[] slotColors = {
        new Color(1f, 0.25f, 0.25f, 1f),   // 0 แดง
        new Color(0.3f, 0.5f, 1f, 1f),     // 1 น้ำเงิน
        new Color(0.3f, 0.9f, 0.4f, 1f),   // 2 เขียว
        new Color(1f, 0.9f, 0.25f, 1f),    // 3 เหลือง
    };

    /// <summary>
    /// สีประจำ slot — จุดเดียวที่นิยามสีผู้เล่นสำหรับกลไก ColorMatch
    /// `ColorMatchAoEAction` อ่านผ่าน telegraph prefab ที่มันถืออยู่แล้ว จะได้ไม่ hardcode ซ้ำสองที่
    /// (เดิมซ้ำกันระหว่าง zone material กับข้อความ HUD ซึ่งเพี้ยนจากกันได้เงียบๆ)
    /// </summary>
    public Color GetSlotColor(int slot)
    {
        if (slotColors == null || slotColors.Length == 0) return Color.white;
        return slotColors[Mathf.Abs(slot) % slotColors.Length];
    }

    // ── Ring / Edge กลาง ──────────────────────────────────────────────────
    // ล้อโครงเดียวกับ Telegraph Colors ข้างบน: ค่ากลางอยู่ตรงนี้ · action ทับรายท่าได้
    // ผ่าน SpawnAoEActionBase.overrideTelegraphEffects (ดู ResolveEffects)
    //
    // **ค่าพวกนี้ทับค่าบน Mat_Tele_Universal เสมอ** — ตั้งแต่ย้ายมาเป็นค่ากลางที่นี่
    // ช่อง _Ring*/_Edge*/_BaseAlpha บน material กลายเป็นค่าที่ไม่มีผล ปรับแล้วจะไม่เห็นอะไรเปลี่ยน
    // (default ด้านล่างคัดมาจาก material ตอนย้าย ของที่เห็นอยู่จึงไม่เปลี่ยน)
    [Header("Telegraph Rings / Edge")]
    [Tooltip("จังหวะเต้นของ alpha · 0 = ปิด")]
    [Range(0f, 2f)] public float pulseAmount = 1f;
    [Tooltip("ความถี่ pulse (รอบ/วินาที)")]
    [Min(0f)] public float pulseSpeed = 4f;
    [Tooltip("การกระพริบ · 0 = ปิด")]
    [Range(0f, 2f)] public float blinkAmount = 1f;
    [Tooltip("ความสว่างของวงที่ไหลออก · 0 = ปิด")]
    [Range(0f, 5f)] public float ringAmount = 3f;
    [Tooltip("ความเร็ววง (เมตร/วินาที) · shader คูณ (1 + FillProgress) ให้เร็วขึ้นเองเมื่อใกล้ระเบิด")]
    [Min(0f)] public float ringSpeed = 1f;
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
    [Tooltip("ความทึบรวมของ zone")]
    [Range(0f, 1f)] public float baseAlpha = 1f;

    // สีของ ring กับแถบขอบ — shader ย้อมสองอย่างนี้ด้วย `_OutlineColor` ตัวเดียวกัน
    // (ตรวจจากกราฟ: _OutlineColor แตกไปทั้ง Lerp ของขอบ และสาย Multiply ของ ring)
    // เดิมเซ็ตเป็นสีอันตรายค้างไว้ ring จึงแดงตั้งแต่วินาทีแรก ต่างจากพื้นวงที่ค่อยๆ เปลี่ยน
    [Tooltip("ให้สี ring/ขอบ ตามหมวดกลไก (Gaze ม่วง · Stack ฟ้า · Chase ชมพู) แล้วไล่ warning→danger เอง\n" +
             "ปิด = ใช้สองสีด้านล่างแทน — จะเสียสัญญาณว่าท่านี้เป็นหมวดไหน")]
    public bool  ringFollowsCategory = true;
    [Tooltip("สี ring/ขอบ ตอนเริ่ม (ใช้เมื่อปิด ringFollowsCategory)")]
    public Color ringWarningColor = new Color(1f, 0.64f, 0.024f, 1f);
    [Tooltip("สี ring/ขอบ ตอนใกล้ระเบิด (ใช้เมื่อปิด ringFollowsCategory)")]
    public Color ringDangerColor  = new Color(1f, 0f, 0.099f, 1f);

    [Header("Audio (Optional)")]
    [Tooltip("เสียงเตือนตอน telegraph เริ่ม (one-shot)")]
    public AudioClip warningClip;
    [Tooltip("เสียงระเบิดตอน detonate")]
    public AudioClip detonateClip;
    [Range(0f, 1f)] public float warningVolume  = 0.5f;
    [Range(0f, 1f)] public float detonateVolume = 0.7f;

    // ── Client-side visual ────────────────────────────────────────────────
    private GameObject     visual;
    private List<Renderer> visualRenderers   = new List<Renderer>();
    private List<Renderer> safeZoneRenderers = new List<Renderer>();
    private float          totalWarning;
    private float          elapsed;
    private bool           initialized;
    private static Material cachedFallbackMaterial;
    private MaterialPropertyBlock mpb;
    private Vector3        visualBaseScale = Vector3.one;

    /// <summary>
    /// ความหนาของ zone เป็น**หน่วยโลก** — จุดเดียวที่กำหนดเรื่องนี้ ใช้ทั้งทาง prefab และ primitive
    ///
    /// ยึดทรงกระบอกเป็นมาตรฐาน: ทุกทรงหนาเท่ากันและ**จัดกึ่งกลางที่ระดับ Y เดียวกัน**
    /// zone เกิดที่ Y ของบอส (~1 เหนือพื้น) ก้อนหนา 2 จึงกิน Y 0..2 — ขอบล่างแตะพื้นพอดี
    /// ซึ่งเป็นขอบที่วาดตรงตำแหน่งจริง ต่างจากแผ่นบางลอยที่เหลื่อมทั้งแผ่นตามมุมกล้อง
    ///
    /// mesh แต่ละตัวสูงไม่เท่ากันที่ scale 1 จึงต้องหารก่อนใช้ — ดู ScaleY()
    /// </summary>
    private const float ZoneThickness = 2f;

    /// <summary>
    /// แปลงความหนาหน่วยโลกเป็น scale.y ของ mesh นั้นๆ
    /// Unity Cylinder สูง 2 ที่ scale 1 · Cube สูง 1 — ถ้าใส่ค่าเดียวกันทั้งคู่ Line จะบางกว่าครึ่งหนึ่ง
    /// </summary>
    private const float ScaleYCylinder = ZoneThickness * 0.5f;   // 2 units tall mesh
    private const float ScaleYCube     = ZoneThickness;          // 1 unit tall mesh

    // ── Server-side params (set before Spawn, read via InitClientRpc) ─────
    [HideInInspector] public AoEType aoeType         = AoEType.Circle;
    [HideInInspector] public float   radius          = 3f;
    [HideInInspector] public float   lineLength      = 8f;
    [HideInInspector] public float   lineWidth       = 1.5f;
    [HideInInspector] public float   warningDuration = 2.5f;
    [HideInInspector] public float   damage          = 30f;
    [HideInInspector] public float   innerRadius     = 1.5f;   // Donut: safe zone inner radius
    [HideInInspector] public ulong   chaseTargetClientId = ulong.MaxValue; // Chase: target player

    [HideInInspector] public bool    isChasing         = false;
    [HideInInspector] public bool    isStackMarker     = false;
    [HideInInspector] public bool    isGaze            = false;
    [HideInInspector] public KnockbackMode knockbackMode = KnockbackMode.FromCenter;
    [HideInInspector] public float   knockbackDistance = 0f;   // หน่วยระยะทาง ไม่ใช่แรง
    [HideInInspector] public float   knockbackDuration = 0.2f;
    [HideInInspector] public Vector3 knockbackFixedDirection = Vector3.forward;

    [HideInInspector] public bool    isRotatingChase   = false;
    [HideInInspector] public NetworkObject casterNetworkObject;

    // VFX ตอนระเบิดแบบต่อ action — telegraph prefab มีตัวเดียวใช้ร่วมทั้งเกม
    // ถ้าไม่มีช่องนี้ ทุก AoE จะระเบิดหน้าตาเหมือนกันหมด · ว่าง = ใช้ detonateVfxPrefab บน prefab
    [HideInInspector] public string  detonateVfxKey = "";

    [HideInInspector] public float   coneAngle = 90f;    // Cone: มุมกางทั้งหมด (องศา)

    // สีที่ action สั่งมาเป็นรายท่า — ทับ palette กลางบน prefab
    [HideInInspector] public bool    overrideColors;
    [HideInInspector] public Color   overrideWarningColor = Color.yellow;
    [HideInInspector] public Color   overrideDangerColor  = Color.red;

    // สีขอบแยกอิสระจากสีพื้น — ไม่ override = ขอบใช้สีอันตรายของหมวดเดียวกัน (พฤติกรรมเดิม)
    [HideInInspector] public bool    overrideOutlineColorFlag;
    [HideInInspector] public Color   overrideOutlineColor = Color.white;

    // หน้าตาทั้งชุดที่ action สั่งมาเป็นรายท่า — ทับค่าบน material
    // ไม่ override = ไม่ดันเข้า shader เลย material จึงคุมเองทั้งหมด
    // ค่า default ตรงกับ Mat_Tele_Universal เพื่อให้ติ๊กเปิดแล้วหน้าตายังเหมือนเดิมจนกว่าจะลงมือปรับ
    [HideInInspector] public bool    overrideEffects;
    [HideInInspector] public float   overridePulseAmount  = 1f;
    [HideInInspector] public float   overrideBlinkAmount  = 1f;
    [HideInInspector] public float   overrideRingAmount   = 3f;
    [HideInInspector] public float   overrideRingSpeed    = 1f;
    [HideInInspector] public float   overridePulseSpeed   = 4f;
    [HideInInspector] public float   overrideRingSpacing  = 1.5f;
    [HideInInspector] public float   overrideRingWidth    = 0.15f;
    [HideInInspector] public float   overrideOutlineWidth = 0.15f;
    [HideInInspector] public float   overrideEdgeGlow     = 1.5f;
    [HideInInspector] public float   overrideEdgeStrength = 2f;
    [HideInInspector] public float   overrideBaseAlpha    = 1f;

    /// <summary>
    /// ลำดับความสำคัญของสี — ColorMatch ต้องชนะทุกอย่างเพราะสีคือ**เงื่อนไขของกลไก**
    /// ไม่ใช่การตกแต่ง · ถัดมาคือสีที่ designer สั่งมาต่อท่า แล้วค่อยเรียงตามหมวดกลไก
    ///
    /// **Gaze > Stack > Chase** — เรียงตาม "ถ้าทำตามสัญชาตญาณปกติแล้วตายแค่ไหน"
    /// Gaze ตำแหน่งไม่ช่วยเลยต้องหันหลัง · Stack ต้องวิ่งเข้าไม่ใช่ออก · ส่วน Chase ยังหนีถูกอยู่
    /// สีบอกได้ทีละอย่าง จึงให้ตัวที่กลับด้านปฏิกิริยามากที่สุดชนะ
    /// </summary>
    (Color warn, Color danger) ResolveColors()
    {
        if (isColorMatch.Value)
        {
            ulong reqId = requiredClientId.Value;
            int slot = PlayerSlotRegistry.Instance != null
                ? PlayerSlotRegistry.Instance.GetSlot(reqId) : -1;
            if (slot < 0) slot = (int)(reqId % 4);

            Color c = GetSlotColor(slot);
            return (c, c * 0.8f);
        }

        if (overrideColors) return (overrideWarningColor, overrideDangerColor);
        if (isGaze)         return (gazeWarningColor,  gazeDangerColor);
        if (isStackMarker)  return (stackWarningColor, stackDangerColor);
        if (isChasing)      return (chaseWarningColor, chaseDangerColor);
        return (warningColor, dangerColor);
    }

    /// <summary>
    /// zone เดียวติดหลายหมวดพร้อมกัน — สีบอกได้อย่างเดียว สัญญาณที่เหลือหายไปเงียบๆ
    /// designer ต้องรู้ ไม่งั้นจะงงว่าทำไม Stack ที่ตั้งไว้ไม่เป็นสีฟ้า
    /// </summary>
    void WarnIfMultipleColorCategories()
    {
        int categories = (isGaze ? 1 : 0) + (isStackMarker ? 1 : 0) + (isChasing ? 1 : 0);
        if (categories <= 1) return;

        string on = $"{(isGaze ? "Gaze " : "")}{(isStackMarker ? "Stack " : "")}{(isChasing ? "Chase" : "")}".Trim();
        Debug.LogWarning($"[TelegraphZone] zone เดียวติดหลายหมวด ({on}) — สีจะใช้ตัวที่สำคัญสุด " +
                         "ตามลำดับ Gaze > Stack > Chase · หมวดที่เหลือจะไม่มีสีบอก");
    }

    // ขยาย/หด และกวาด — client เล่นภาพเอง server คำนวณค่าสุดท้ายตอน resolve
    // ทั้งสองฝั่งรู้ warningDuration กับอัตราอยู่แล้ว จึงไม่ต้อง sync ระหว่างทาง
    [HideInInspector] public float   scaleStart = 1f;
    [HideInInspector] public float   scaleEnd   = 1f;
    [HideInInspector] public float   sweepDegreesPerSecond = 0f;
    private Quaternion initialRotation;

    /// <summary>ตัวคูณขนาด ณ วินาทีนี้ — ใช้ทั้งภาพฝั่ง client และ hit test ฝั่ง server</summary>
    public float CurrentScale
    {
        get
        {
            if (Mathf.Approximately(scaleStart, scaleEnd)) return scaleEnd;
            float t = warningDuration > 0.0001f ? Mathf.Clamp01(elapsed / warningDuration) : 1f;
            return Mathf.Lerp(scaleStart, scaleEnd, t);
        }
    }

    // ── Rabbit & Steel: Color Match ──
    public NetworkVariable<bool> isColorMatch = new NetworkVariable<bool>(false);
    public NetworkVariable<ulong> requiredClientId = new NetworkVariable<ulong>(ulong.MaxValue);

    [Header("Chase Settings")]
    [Tooltip("Chase: ระยะเวลาก่อนระเบิดที่ zone หยุดติดตาม (วินาที)\n" +
             "ให้ผู้เล่นมีเวลาวิ่งหนี — 0.8 = lock-in 0.8 วินาทีสุดท้าย")]
    [Range(0f, 3f)]
    public float chaseLockInTime = 0.8f;
    [Tooltip("Chase: smoothing — 0 = snap | 1 = lazy follow")]
    [Range(0f, 1f)]
    public float chaseSmoothing = 0.15f;

    // ── Spawn Entry Point ─────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (isChasing && chaseTargetClientId == ulong.MaxValue)
            {
                SelectNearestChaseTarget();
            }
            StartCoroutine(TelegraphSequence());
        }
    }

    private void SelectNearestChaseTarget()
    {
        if (NetworkManager.Singleton == null) return;
        float minDst = float.MaxValue;
        ulong nearestId = ulong.MaxValue;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
            {
                var pm = client.PlayerObject.GetComponent<playermove>();
                if (pm != null && pm.isDead.Value) continue;

                float dst = Vector3.Distance(transform.position, client.PlayerObject.transform.position);
                if (dst < minDst)
                {
                    minDst = dst;
                    nearestId = client.ClientId;
                }
            }
        }
        chaseTargetClientId = nearestId;
    }

    /// <summary>Server เรียกทันทีหลัง Spawn เพื่อส่งพารามิเตอร์ไปทุก client</summary>
    public void BroadcastInit()
    {
        InitClientRpc(new TelegraphInit
        {
            aoeType             = (int)aoeType,
            radius              = radius,
            lineLength          = lineLength,
            lineWidth           = lineWidth,
            warningDuration     = warningDuration,
            damage              = damage,
            innerRadius         = innerRadius,
            chaseTargetClientId = chaseTargetClientId,
            coneAngle             = coneAngle,
            scaleStart            = scaleStart,
            scaleEnd              = scaleEnd,
            sweepDegreesPerSecond = sweepDegreesPerSecond,
            isChasing           = isChasing,
            isStackMarker       = isStackMarker,
            isGaze              = isGaze,
            isRotatingChase     = isRotatingChase,
            detonateVfxKey      = detonateVfxKey ?? "",
            overrideColors      = overrideColors,
            warningColor        = overrideWarningColor,
            dangerColor         = overrideDangerColor,
            overrideOutlineColor = overrideOutlineColorFlag,
            outlineColor        = overrideOutlineColor,
            overrideEffects     = overrideEffects,
            pulseAmount         = overridePulseAmount,
            blinkAmount         = overrideBlinkAmount,
            ringAmount          = overrideRingAmount,
            ringSpeed           = overrideRingSpeed,
            pulseSpeed          = overridePulseSpeed,
            ringSpacing         = overrideRingSpacing,
            ringWidth           = overrideRingWidth,
            outlineWidth        = overrideOutlineWidth,
            edgeGlow            = overrideEdgeGlow,
            edgeStrength        = overrideEdgeStrength,
            baseAlpha           = overrideBaseAlpha,
        });
    }

    // ── ClientRpc ─────────────────────────────────────────────────────────
    [ClientRpc]
    void InitClientRpc(TelegraphInit init)
    {
        aoeType               = (AoEType)init.aoeType;
        radius                = init.radius;
        lineLength            = init.lineLength;
        lineWidth             = init.lineWidth;
        warningDuration       = init.warningDuration;
        damage                = init.damage;
        innerRadius           = init.innerRadius;
        chaseTargetClientId   = init.chaseTargetClientId;
        coneAngle             = init.coneAngle;
        scaleStart            = init.scaleStart;
        scaleEnd              = init.scaleEnd;
        sweepDegreesPerSecond = init.sweepDegreesPerSecond;
        initialRotation       = transform.rotation;
        isChasing             = init.isChasing;
        isStackMarker         = init.isStackMarker;
        isGaze                = init.isGaze;
        isRotatingChase       = init.isRotatingChase;
        detonateVfxKey        = init.detonateVfxKey.ToString();
        overrideColors        = init.overrideColors;
        overrideWarningColor  = init.warningColor;
        overrideDangerColor   = init.dangerColor;
        overrideOutlineColorFlag = init.overrideOutlineColor;
        overrideOutlineColor  = init.outlineColor;
        overrideEffects       = init.overrideEffects;
        overridePulseAmount   = init.pulseAmount;
        overrideBlinkAmount   = init.blinkAmount;
        overrideRingAmount    = init.ringAmount;
        overrideRingSpeed     = init.ringSpeed;
        overridePulseSpeed    = init.pulseSpeed;
        overrideRingSpacing   = init.ringSpacing;
        overrideRingWidth     = init.ringWidth;
        overrideOutlineWidth  = init.outlineWidth;
        overrideEdgeGlow      = init.edgeGlow;
        overrideEdgeStrength  = init.edgeStrength;
        overrideBaseAlpha     = init.baseAlpha;
        totalWarning          = init.warningDuration;
        elapsed               = 0f;
        initialized           = true;

        // Chase: แจ้ง player ที่ถูก target ว่าต้องวิ่งหนี
        if (isChasing && NetworkManager.Singleton != null
            && NetworkManager.Singleton.LocalClientId == chaseTargetClientId)
        {
            GameHUD.Instance?.ShowAnnouncement("⚡ TARGETED — RUN AWAY!", Color.magenta);
        }

        WarnIfMultipleColorCategories();

        // Visual: 3D prefab (preferred) > runtime primitive (fallback)
        if (!TrySpawnVfxPrefab())
            CreateVisual();

        PushShaderColors();

        // จำขนาดตั้งต้นไว้คูณกับ CurrentScale ตอนวงขยาย/หด
        if (visual != null)
        {
            visualBaseScale = visual.transform.localScale;
            if (!Mathf.Approximately(scaleStart, 1f))
                visual.transform.localScale = visualBaseScale * scaleStart;
        }

        // Audio: warning cue ตอน telegraph เริ่ม
        if (warningClip != null)
            SoundManager.Instance.PlaySfx(warningClip, transform.position, warningVolume);
    }

    /// <summary>
    /// Instantiate 3D prefab สำหรับ aoeType ปัจจุบัน + scale ตาม params
    /// คืน true ถ้าใช้ prefab สำเร็จ → caller skip primitive creation
    ///
    /// **Prefab convention** — ออกแบบในขนาดมาตรฐานที่ scale=1:
    ///   • circlePrefab : เส้นผ่านศูนย์กลาง 1 unit นอนบน XZ (Circle, Chase, Donut fallback)
    ///   • linePrefab   : ยาว 1 unit ทาง +Z, กว้าง 1 unit ทาง X (Line, Cross ×2)
    ///   • donutPrefab  : เส้นผ่านศูนย์กลาง 1 unit นอนบน XZ
    ///
    /// **Cross logic** — spawn linePrefab 2 ตัว scale คนละแกน (ไม่ต้องหมุน):
    ///   arm1 = (lineWidth, 1, lineLength)  → ยาวทาง Z
    ///   arm2 = (lineLength, 1, lineWidth)  → ยาวทาง X
    /// </summary>
    bool TrySpawnVfxPrefab()
    {
        // เลือก prefab ตาม AoEType (Cross reuse Line)
        //
        // **Donut ยังใช้ torus ไปก่อน** — แผนคือเปลี่ยนไปใช้ `circlePrefab` (จานแบน) แล้วเจาะรู
        // ด้วย `_InnerRadius` ในสูตร เพราะ torus อบรูกับความหนาแถบมากับ mesh ปรับ innerRadius
        // เท่าไหร่ภาพก็ไม่ขยับ และแถบวงบางๆ ไม่ตรงกับ `IsInDonut` ที่คิดเป็น annulus ตัน
        //
        // แต่สลับได้ **ก็ต่อเมื่อ shader มี `_InnerRadius` แล้วเท่านั้น** — ถ้าสลับก่อน
        // จะได้จานตันไม่มีรูเลย ซึ่งแย่กว่า torus รูผิดขนาด · สลับพร้อมกับตอนต่อโหนดในกราฟ
        GameObject prefab = aoeType switch
        {
            AoEType.Circle => circlePrefab,
            AoEType.Donut  => donutPrefab,
            AoEType.Line   => linePrefab,
            AoEType.Cross  => linePrefab,     // Cross ใช้ Line prefab (arm 1)
            _              => null,
        };
        if (prefab == null) return false;

        visual = Instantiate(prefab, transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;

        // ── Scale Mapping (XZ = ขนาดจริง · Y = แบนติดพื้นเสมอ) ──
        // hit test ทุกตัวคิดบนระนาบ XZ ล้วน แต่เดิม Y ถูกตั้งเป็น 1 ทำให้ Cylinder สูง 2 หน่วยจริง
        // กล้องอยู่ที่ offset (0,10,-5) เอียงจากแนวดิ่ง ~26.6° (tan ≈ 0.5) ขอบบนของก้อนจึง
        // เหลื่อมจากรอยเท้าราว สูง × 0.5 — วงสูง 2 หน่วยคลาด ~1 เมตร เทียบกับรัศมี 3–5 เมตร
        // ผู้เล่นตัดสินจากขอบที่เห็น แต่ดาเมจตัดสินจากรอยเท้า · แบนแล้วสองอย่างนี้ทับกันสนิท
        Vector3 s = aoeType switch
        {
            AoEType.Circle => new Vector3(radius * 2f, ScaleYCylinder, radius * 2f),
            AoEType.Donut  => new Vector3(radius * 2f, ScaleYCylinder, radius * 2f),
            AoEType.Line   => new Vector3(lineWidth,   ScaleYCube,     lineLength),
            AoEType.Cross  => new Vector3(lineWidth,   ScaleYCube,     lineLength),   // arm 1
            _              => Vector3.one,
        };
        visual.transform.localScale = s;

        // Cross: spawn arm 2 (perpendicular, scale แกน X/Z สลับ)
        GameObject arm2 = null;
        if (aoeType == AoEType.Cross)
        {
            arm2 = Instantiate(linePrefab, transform);
            arm2.transform.localPosition = Vector3.zero;
            arm2.transform.localRotation = Quaternion.identity;
            arm2.transform.localScale    = new Vector3(lineLength, ScaleYCube, lineWidth);
        }

        // ส่ง params เข้า VFX Graph (ถ้ามี)
        var vfx = visual.GetComponent<UnityEngine.VFX.VisualEffect>();
        if (vfx != null)
        {
            if (vfx.HasFloat("WarningDuration")) vfx.SetFloat("WarningDuration", warningDuration);
            if (vfx.HasFloat("InnerRadius"))     vfx.SetFloat("InnerRadius",     innerRadius);
        }

        // เก็บ renderer ทั้งหมด (visual + arm2) เพื่อให้ Update() ปรับสี warning→danger ได้
        // Cross วาดสองกล่องทับกัน = union ตรงกับ hit test อยู่แล้ว · ที่ผิดคือเส้นขอบตกแต่ง
        // ที่พาดกลางเขต ซึ่งตัดออกจากขอบเขตงานแล้ว (ดู ADR-005)

        CollectRenderersAndApplyShaderParams(visual);
        if (arm2 != null) CollectRenderersAndApplyShaderParams(arm2);

        return true;
    }

    [ClientRpc]
    public void NotifyColorClientRpc(ulong targetClientId, string colorName, Color uiColor)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == targetClientId)
        {
            GameHUD.Instance?.ShowAnnouncement($"Stand in the {colorName} circle!", uiColor);
        }
    }

    /// <summary>
    /// ครึ่งขนาดของแขนอีกข้าง (หน่วยเมตร) เรียงคู่กับ `visualRenderers`
    ///
    /// Cross เป็นทรงเดียวที่ renderer สองตัวต้องได้ค่าคนละชุด — แขนแต่ละข้างต้องรู้ขนาด
    /// ของอีกข้างถึงจะคิด `max(dOwn, dSibling)` เพื่อลบเส้นขอบด้านในออกได้
    /// (0,0) = ไม่มีพี่น้อง ซึ่งทำให้ dSibling ≤ 0 เสมอ สูตรจึงกลับไปเป็น dOwn เฉยๆ
    /// </summary>

    void CollectRenderersAndApplyShaderParams(GameObject root)
    {
        mpb ??= new MaterialPropertyBlock();

        var rends = root.GetComponentsInChildren<Renderer>();
        foreach (var r in rends)
        {
            if (r == null) continue;
            visualRenderers.Add(r);

            var sharedMat = r.sharedMaterial;
            if (sharedMat == null) continue;

            r.GetPropertyBlock(mpb);

            // เริ่ม fill ที่ 0 (กันค่าค้างจาก material asset)
            if (sharedMat.HasProperty("_FillProgress")) mpb.SetFloat("_FillProgress", 0f);

            r.SetPropertyBlock(mpb);
        }
    }

    /// <summary>
    /// ดันสีเข้า shader ให้ renderer ทุกตัวที่เก็บไว้ — **จุดเดียวที่ทำเรื่องนี้**
    ///
    /// เรียกหลังสร้าง visual เสร็จ จึงครอบทั้งเส้นทาง prefab และเส้นทาง primitive
    /// เดิมดันเฉพาะใน CollectRenderersAndApplyShaderParams ซึ่งรันแค่ตอนใช้ prefab —
    /// Cone ที่ปั้น mesh เองเลยไม่เคยได้สีตามหมวดกลไกเลยสักครั้ง
    /// </summary>
    void PushShaderColors()
    {
        var (warn, danger) = ResolveColors();
        mpb ??= new MaterialPropertyBlock();

        // Line/Cross เป็นกล่องสี่เหลี่ยม ต้องใช้ระยะแบบเหลี่ยม ไม่งั้นขอบจะโค้งบนกล่อง
        float outlineShape = aoeType is AoEType.Line or AoEType.Cross ? 0f : 1f;

        for (int i = 0; i < visualRenderers.Count; i++)
        {
            var r = visualRenderers[i];
            if (r == null) continue;
            var sharedMat = r.sharedMaterial;
            if (sharedMat == null) continue;

            r.GetPropertyBlock(mpb);


            // Donut: รูตรงกลางเป็นสัดส่วนของรัศมีนอก · 0 = ไม่เจาะ (shader มี guard)
            if (sharedMat.HasProperty("_InnerRadius"))
                mpb.SetFloat("_InnerRadius",
                             aoeType == AoEType.Donut && radius > 0.0001f
                                 ? Mathf.Clamp01(innerRadius / radius) : 0f);

            if (sharedMat.HasProperty("_WarningColor")) mpb.SetColor("_WarningColor", warn);
            if (sharedMat.HasProperty("_DangerColor"))  mpb.SetColor("_DangerColor",  danger);
            // สีเริ่มต้นของ ring/ขอบ ที่ progress = 0 · Update() จะไล่ต่อเองทุกเฟรม
            // ตั้งตรงนี้ด้วยเพื่อไม่ให้เฟรมแรกโผล่มาเป็นสีอันตรายก่อนแล้วค่อยกระโดดกลับ
            if (sharedMat.HasProperty("_OutlineColor"))
                mpb.SetColor("_OutlineColor", ResolveRingColor(0f));
            if (sharedMat.HasProperty("_OutlineShape")) mpb.SetFloat("_OutlineShape", outlineShape);

            // ── หน้าตาวง/ขอบ — ดันเสมอ เหมือนที่ทำกับสี ──
            // ค่ากลางอยู่บน prefab ตัวนี้ · action ทับเป็นรายท่าได้ · โครงเดียวกับ ResolveColors()
            // เดิมตอนไม่ override จะไม่แตะ shader เลยแล้วปล่อยให้ material คุม — เปลี่ยนแล้ว
            // เพราะ "ค่ากลาง" ต้องอยู่ที่เดียว ถ้าปล่อยสองทางจะเดาไม่ออกว่าอันไหนชนะ
            SetIfPresent(sharedMat, "_PulseAmount",  Fx(overridePulseAmount,  pulseAmount));
            SetIfPresent(sharedMat, "_PulseSpeed",   Fx(overridePulseSpeed,   pulseSpeed));
            SetIfPresent(sharedMat, "_BlinkAmount",  Fx(overrideBlinkAmount,  blinkAmount));
            SetIfPresent(sharedMat, "_RingAmount",   Fx(overrideRingAmount,   ringAmount));
            SetIfPresent(sharedMat, "_RingSpeed",    Fx(overrideRingSpeed,    ringSpeed));
            SetIfPresent(sharedMat, "_RingSpacing",  Fx(overrideRingSpacing,  ringSpacing));
            SetIfPresent(sharedMat, "_RingWidth",    Fx(overrideRingWidth,    ringWidth));
            SetIfPresent(sharedMat, "_OutlineWidth", Fx(overrideOutlineWidth, outlineWidth));
            SetIfPresent(sharedMat, "_EdgeGlow",     Fx(overrideEdgeGlow,     edgeGlow));
            SetIfPresent(sharedMat, "_EdgeStrength", Fx(overrideEdgeStrength, edgeStrength));
            SetIfPresent(sharedMat, "_BaseAlpha",    Fx(overrideBaseAlpha,    baseAlpha));

            r.SetPropertyBlock(mpb);
        }
    }

    /// <summary>
    /// ดันค่าเข้า mpb เฉพาะเมื่อ material มี property นั้นจริง
    ///
    /// เช็คก่อนเสมอเพราะ zone ตัวเดียวกันถูกใช้กับทั้ง `TelegraphUniversal` และ material
    /// สำรอง (URP/Lit) ที่ `GetWarningMaterial()` ปั้นเอง — ตัวหลังไม่มี property พวกนี้เลย
    /// การ set ลง MaterialPropertyBlock ที่ shader ไม่รู้จักไม่ error แต่ก็ทำให้ debug ยากขึ้นเปล่าๆ
    /// </summary>
    void SetIfPresent(Material sharedMat, string prop, float value)
    {
        if (sharedMat.HasProperty(prop)) mpb.SetFloat(prop, value);
    }

    /// <summary>
    /// เลือกระหว่างค่าที่ action สั่งมากับค่ากลางบน prefab — คู่กับ `ResolveColors()`
    /// gate เดียวคุมทั้งชุด ไม่ใช่รายช่อง เพื่อให้ designer อ่านออกว่า "ท่านี้คุมหน้าตาเองทั้งหมด"
    /// ไม่ใช่ "ท่านี้คุมบางช่อง ที่เหลือมาจากไหนไม่รู้"
    /// </summary>
    float Fx(float fromAction, float central) => overrideEffects ? fromAction : central;

    /// <summary>
    /// สี ring + แถบขอบ ณ วินาทีนี้ — ไล่ warning→danger ตาม progress
    ///
    /// ทำ lerp ฝั่ง C# เพราะ shader ย้อม ring ด้วย `_OutlineColor` ตัวเดียวซึ่งไม่มีคู่สีของตัวเอง
    /// (พื้นวงมี `_WarningColor`/`_DangerColor` ให้ shader lerp เองอยู่แล้ว แต่ ring ไม่มี)
    /// ถ้าอยากให้ ring แยกสีจากขอบได้จริง ต้องเพิ่มคู่สีในกราฟ — ดู §10 ของ handoff
    ///
    /// ลำดับ: action สั่งสีตายตัว > ตามหมวดกลไก > คู่สีกลางบน prefab
    /// ตามหมวดเป็นค่าตั้งต้นเพราะขอบเป็นตัวบอกว่าท่านี้คือหมวดไหน — ทิ้งไปแล้วสัญญาณหาย
    /// </summary>
    Color ResolveRingColor(float progress)
    {
        if (overrideOutlineColorFlag) return overrideOutlineColor;

        if (ringFollowsCategory)
        {
            var (warn, danger) = ResolveColors();
            return Color.Lerp(warn, danger, progress);
        }
        return Color.Lerp(ringWarningColor, ringDangerColor, progress);
    }

    [ClientRpc]
    void ExplodeClientRpc()
    {
        if (visual) visual.SetActive(false);

        // Scale ตาม radius ของ AoE
        float scale = aoeType switch
        {
            AoEType.Circle => radius,
            AoEType.Donut  => radius,
            _              => Mathf.Max(lineWidth, lineLength * 0.3f),
        };

        // Detonate VFX (impact shockwave)
        // key ต่อ action มาก่อน — ผ่าน pool ตาม convention #2
        if (!string.IsNullOrEmpty(detonateVfxKey) && detonateVfxKey != "None")
        {
            NetworkedVFXPool.Instance?.PlayByName(detonateVfxKey, transform.position, scale);
        }
        else if (detonateVfxPrefab != null)
        {
            // ทางเดิม — asset ที่ยังไม่ได้ตั้ง key ต้องทำงานเหมือนเดิม
            var fx = Instantiate(detonateVfxPrefab, transform.position, Quaternion.identity);
            fx.transform.localScale = Vector3.one * scale;
            Destroy(fx, 3f);   // auto cleanup หลัง 3 วินาที
        }

        // Audio: detonate boom
        if (detonateClip != null)
            SoundManager.Instance.PlaySfx(detonateClip, transform.position, detonateVolume);
    }

    // ── Server Sequence ────────────────────────────────────────────────────
    IEnumerator TelegraphSequence()
    {
        if (isChasing)
            yield return StartCoroutine(ChaseSequence());
        else
        {
            if (casterNetworkObject != null)
            {
                float timer = 0f;
                while (timer < warningDuration)
                {
                    if (casterNetworkObject != null)
                    {
                        transform.position = casterNetworkObject.transform.position;
                        SyncChaseTransformClientRpc(transform.position, transform.rotation);
                    }
                    timer += Time.deltaTime;
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(warningDuration);
            }
        }

        // ลำแสงกวาด — server ไม่ต้อง sync ระหว่างทาง เพราะดาเมจลงครั้งเดียวตอนจบ
        // ตั้งมุมสุดท้ายให้ตรงกับที่ client เห็นแล้วค่อยคิด hit test
        if (Mathf.Abs(sweepDegreesPerSecond) > 0.001f)
        {
            transform.rotation = initialRotation
                               * Quaternion.Euler(0f, sweepDegreesPerSecond * warningDuration, 0f);
        }

        DealDamage();
        ExplodeClientRpc();

        yield return new WaitForSeconds(0.1f);
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
    }

    /// <summary>
    /// Chase: server ติดตาม target player → หยุดก่อนระเบิด chaseLockInTime วินาที
    /// (ให้ผู้เล่นมีเวลาวิ่งหนีออกจาก final position)
    /// sync ไป clients ทุก 80ms
    /// </summary>
    IEnumerator ChaseSequence()
    {
        float timer        = 0f;
        float syncTimer    = 0f;
        const float syncInterval = 0.08f;

        // ระยะเวลา follow ก่อนเข้า lock-in
        float followDuration = Mathf.Max(0f, warningDuration - chaseLockInTime);

        // Phase 1: Follow player ด้วย smoothing
        while (timer < followDuration)
        {
            if (casterNetworkObject != null)
            {
                transform.position = casterNetworkObject.transform.position;
            }

            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.ConnectedClients.TryGetValue(chaseTargetClientId, out var client) &&
                client.PlayerObject != null)
            {
                Vector3 tp = client.PlayerObject.transform.position;

                if (isRotatingChase)
                {
                    Vector3 dir = (tp - transform.position).normalized;
                    dir.y = 0f;
                    if (dir != Vector3.zero)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(dir);
                        transform.rotation = chaseSmoothing > 0f
                            ? Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Pow(chaseSmoothing, Time.deltaTime * 60f))
                            : targetRot;
                    }
                }
                else
                {
                    Vector3 targetPos = new Vector3(tp.x, transform.position.y, tp.z);
                    // Lazy follow — smooth lerp แทน snap
                    transform.position = chaseSmoothing > 0f
                        ? Vector3.Lerp(transform.position, targetPos, 1f - Mathf.Pow(chaseSmoothing, Time.deltaTime * 60f))
                        : targetPos;
                }

                syncTimer += Time.deltaTime;
                if (syncTimer >= syncInterval)
                {
                    SyncChaseTransformClientRpc(transform.position, transform.rotation);
                    syncTimer = 0f;
                }
            }

            timer += Time.deltaTime;
            yield return null;
        }

        // Phase 2: Lock-in — หยุดติดตาม sync ตำแหน่งสุดท้ายให้ทุก client
        if (casterNetworkObject != null)
        {
            transform.position = casterNetworkObject.transform.position;
        }
        SyncChaseTransformClientRpc(transform.position, transform.rotation);
        NotifyChaseLockedClientRpc();

        if (chaseLockInTime > 0f)
        {
            float timer2 = 0f;
            while (timer2 < chaseLockInTime)
            {
                if (casterNetworkObject != null)
                {
                    transform.position = casterNetworkObject.transform.position;
                    SyncChaseTransformClientRpc(transform.position, transform.rotation);
                }
                timer2 += Time.deltaTime;
                yield return null;
            }
        }
    }

    [ClientRpc]
    void NotifyChaseLockedClientRpc()
    {
        // Visual feedback: เปลี่ยนสี Chase visual เป็น "ใกล้ระเบิด" (จะถูก override โดย Update tick ถัดไป
        // — ใช้ตอนนี้แค่ trigger announcement)
        if (NetworkManager.Singleton != null
            && NetworkManager.Singleton.LocalClientId == chaseTargetClientId)
        {
            GameHUD.Instance?.ShowAnnouncement("⚠ LOCKED IN!", new Color(1f, 0.3f, 0.2f));
        }
    }

    [ClientRpc]
    void SyncChaseTransformClientRpc(Vector3 newPos, Quaternion newRot)
    {
        // อัปเดตตำแหน่งและทิศทางบน client — visual เป็น child จะหมุน/เลื่อนตามอัตโนมัติ
        transform.position = newPos;
        transform.rotation = newRot;
    }

    void DealDamage()
    {
        if (!IsServer || NetworkManager.Singleton == null) return;

        List<playermove> playersInZone = new List<playermove>();
        List<playermove> allActivePlayers = new List<playermove>();

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var playerObj = client.PlayerObject;
            if (playerObj == null) continue;

            var pm = playerObj.GetComponent<playermove>();
            if (pm == null || pm.isDead.Value) continue;

            allActivePlayers.Add(pm);
            Vector3 playerPos = playerObj.transform.position;
            bool inZone = aoeType switch
            {
                AoEType.Circle => IsInCircle(playerPos),
                AoEType.Cross  => IsInLine(playerPos) || IsInLineCross(playerPos),
                AoEType.Donut  => IsInDonut(playerPos),
                AoEType.Cone   => IsInCone(playerPos),
                _              => IsInLine(playerPos),    // Line (can chase too)
            };

            if (inZone && isGaze)
            {
                // Gaze: Check if player is facing away from the Gaze source (the TelegraphZone center)
                Vector3 dirToSource = (transform.position - playerPos).normalized;
                dirToSource.y = 0f;
                dirToSource = dirToSource.normalized;
                float dot = Vector3.Dot(playerObj.transform.forward, dirToSource);

                // If dot <= 0, they are facing perpendicular or away -> safe
                if (dot <= 0f)
                {
                    inZone = false;
                }
            }

            if (inZone)
            {
                playersInZone.Add(pm);
            }
        }

        List<playermove> hitPlayers = new List<playermove>();

        if (isColorMatch.Value)
        {
            foreach (var pm in allActivePlayers)
            {
                bool isTarget = pm.OwnerClientId == requiredClientId.Value;
                bool inZone = playersInZone.Contains(pm);

                if (isTarget && !inZone)
                {
                    hitPlayers.Add(pm); // เจ้าของสีไม่ได้ยืนในวง = โดนดาเมจ
                }
                else if (!isTarget && inZone)
                {
                    hitPlayers.Add(pm); // คนอื่นมายืนเหยียบวง = โดนดาเมจ
                }
            }
        }
        else
        {
            hitPlayers = playersInZone;
        }

        // Calculate final damage (split for Stack Marker)
        int count = hitPlayers.Count;
        float finalDamage = (isStackMarker && count > 0) ? (damage / count) : damage;

        foreach (var pm in hitPlayers)
        {
            pm.TakeDamage(finalDamage);
            Debug.Log($"[TelegraphZone] ⚡ Hit player {pm.OwnerClientId} — {finalDamage} dmg (isStack={isStackMarker}, isColorMatch={isColorMatch.Value})");

            if (knockbackDistance > 0f)
            {
                Vector3 pushDir = ResolveKnockbackDirection(pm);
                // playermove ใส่ velocity คงที่ตลอด duration → ระยะที่ได้ = speed × duration พอดี
                float speed = knockbackDuration > 0.0001f ? knockbackDistance / knockbackDuration : knockbackDistance;
                pm.ApplyKnockbackClientRpc(pushDir * speed, knockbackDuration);
            }
        }
    }

    /// <summary>คำนวณทิศผลักตาม KnockbackMode — คืนเวกเตอร์ normalize บนระนาบ XZ</summary>
    Vector3 ResolveKnockbackDirection(playermove pm)
    {
        Vector3 playerPos = pm.transform.position;
        Vector3 dir;

        switch (knockbackMode)
        {
            case KnockbackMode.FromCaster:
                Vector3 casterPos = casterNetworkObject != null
                    ? casterNetworkObject.transform.position
                    : transform.position;
                dir = playerPos - casterPos;
                break;

            case KnockbackMode.TowardCenter:
                dir = transform.position - playerPos;
                break;

            case KnockbackMode.FixedDirection:
                dir = knockbackFixedDirection;
                break;

            case KnockbackMode.AlongLine:
                dir = transform.forward;
                break;

            default:   // FromCenter
                dir = playerPos - transform.position;
                break;
        }

        dir.y = 0f;
        // ผู้เล่นยืนทับจุดอ้างอิงพอดี (เช่น ยืนกลางวงตอน TowardCenter) → เวกเตอร์เป็นศูนย์
        // ถ้าปล่อยไว้ normalize จะได้ zero แล้ว knockback หายเงียบๆ — ใช้ทิศของโซนแทน
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;

        return dir.normalized;
    }

    /// <summary>
    /// ตัวคูณขนาดที่ใช้ตอน resolve ดาเมจ — ดาเมจลงครั้งเดียวตอนจบเสมอ จึงเป็น scaleEnd
    /// ไม่ใช้ CurrentScale เพราะ elapsed เป็นตัวนับฝั่ง client ไม่ควรให้ hit test ฝั่ง server พึ่ง
    /// </summary>
    float HitScale => scaleEnd > 0.0001f ? scaleEnd : 1f;

    bool IsInCircle(Vector3 pos)
    {
        Vector2 d = new Vector2(pos.x - transform.position.x, pos.z - transform.position.z);
        return d.magnitude <= radius * HitScale;
    }

    /// <summary>Cone: อยู่ในรัศมี **และ** อยู่ในมุมกางจากทิศที่ zone หันอยู่</summary>
    bool IsInCone(Vector3 pos)
    {
        Vector3 d = pos - transform.position;
        d.y = 0f;

        float r = radius * HitScale;
        if (d.sqrMagnitude > r * r) return false;
        if (coneAngle >= 360f) return true;
        if (d.sqrMagnitude < 0.0001f) return true;   // ยืนทับจุดยอดกรวย

        return Vector3.Angle(transform.forward, d) <= coneAngle * 0.5f;
    }

    bool IsInLine(Vector3 pos)
    {
        Vector3 local = Quaternion.Inverse(transform.rotation) * (pos - transform.position);
        return Mathf.Abs(local.x) <= lineWidth * HitScale * 0.5f
            && Mathf.Abs(local.z) <= lineLength * HitScale * 0.5f;
    }

    // Line ที่หมุน 90° (ใช้สำหรับ Cross)
    bool IsInLineCross(Vector3 pos)
    {
        Quaternion rot90 = transform.rotation * Quaternion.Euler(0f, 90f, 0f);
        Vector3 local = Quaternion.Inverse(rot90) * (pos - transform.position);
        return Mathf.Abs(local.x) <= lineWidth * HitScale * 0.5f
            && Mathf.Abs(local.z) <= lineLength * HitScale * 0.5f;
    }

    bool IsInDonut(Vector3 pos)
    {
        float dist = new Vector2(pos.x - transform.position.x,
                                 pos.z - transform.position.z).magnitude;
        return dist >= innerRadius * HitScale && dist <= radius * HitScale;
    }

    // ── Client Visual ──────────────────────────────────────────────────────
    void CreateVisual()
    {
        visualRenderers.Clear();

        switch (aoeType)
        {
            case AoEType.Circle:
                visual = new GameObject("Visual_Circle");
                visual.transform.SetParent(transform);
                visual.transform.localPosition = Vector3.zero;
                CreateCylinderPrimitive(visual.transform, Vector3.zero, Quaternion.identity, radius * 2f);
                break;

            case AoEType.Line:
                visual = new GameObject("Visual_Line");
                visual.transform.SetParent(transform);
                visual.transform.localPosition = Vector3.zero;
                CreateLinePrimitive(visual.transform, Vector3.zero, Quaternion.identity, lineWidth, lineLength);
                break;

            case AoEType.Cross:
                visual = new GameObject("Visual_Cross");
                visual.transform.SetParent(transform);
                visual.transform.localPosition = Vector3.zero;
                CreateLinePrimitive(visual.transform, Vector3.zero, Quaternion.identity,          lineWidth, lineLength);
                CreateLinePrimitive(visual.transform, Vector3.zero, Quaternion.Euler(0f, 90f, 0f), lineWidth, lineLength);
                break;

            case AoEType.Donut:
                visual = new GameObject("Visual_Donut");
                visual.transform.SetParent(transform);
                visual.transform.localPosition = Vector3.zero;
                // outer danger ring
                CreateCylinderPrimitive(visual.transform, Vector3.zero, Quaternion.identity, radius * 2f);
                // inner safe zone — teal, slightly higher to avoid z-fighting
                CreateSafeCylinderPrimitive(visual.transform, new Vector3(0f, 0.001f, 0f),
                                            Quaternion.identity, innerRadius * 2f);
                break;

            case AoEType.Cone:
                visual = new GameObject("Visual_Cone");
                visual.transform.SetParent(transform);
                visual.transform.localPosition = Vector3.zero;
                // ปั้นที่ Ø1 (รัศมี 0.5) ตาม prefab convention แล้วขยายด้วย transform
                // shader วัดระยะขอบจากตำแหน่ง object space เทียบ Ø1 — ถ้าปั้นที่รัศมีจริง
                // Length จะเกิน 1 ทั้งใบ Step ติด 1 หมด แล้วกรวยจะกลายเป็นสีขอบทั้งอัน
                visual.transform.localScale = new Vector3(radius * 2f, 1f, radius * 2f);
                CreateConePrimitive(visual.transform, 0.5f, coneAngle);
                break;
        }
    }

    /// <summary>
    /// พัดรูปกรวยบนระนาบ XZ — ไม่มี primitive สำเร็จรูปของ Unity ที่เป็นเซกเตอร์แบน
    /// ต้องปั้น mesh เอง ไม่งั้น Cone จะไม่มี telegraph ให้เห็นเลย (กลไกที่มองไม่เห็นเงื่อนไข = ห้าม ship)
    /// กางรอบแกน +Z ข้างละครึ่งมุม ให้ตรงกับ IsInCone ที่วัดจาก transform.forward
    /// </summary>
    void CreateConePrimitive(Transform parent, float coneRadius, float angleDeg)
    {
        int segments = Mathf.Clamp(Mathf.CeilToInt(angleDeg / 5f), 3, 96);

        var verts = new Vector3[segments + 2];
        var tris  = new int[segments * 3];

        verts[0] = Vector3.zero;   // จุดยอด
        float half = angleDeg * 0.5f;
        for (int i = 0; i <= segments; i++)
        {
            float a = Mathf.Deg2Rad * Mathf.Lerp(-half, half, i / (float)segments);
            // มุม 0 = +Z · บวกไปทาง +X ให้ตรงกับ Quaternion.LookRotation
            verts[i + 1] = new Vector3(Mathf.Sin(a) * coneRadius, 0f, Mathf.Cos(a) * coneRadius);
        }
        for (int i = 0; i < segments; i++)
        {
            tris[i * 3 + 0] = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = i + 2;
        }

        var mesh = new Mesh { name = "TelegraphCone" };
        mesh.vertices  = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject("ConeFan");
        go.transform.SetParent(parent);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = GetWarningMaterial();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        visualRenderers.Add(r);

        coneMesh = mesh;   // mesh ที่ new เองต้องลบเอง
    }

    private Mesh coneMesh;

    void OnDestroy()
    {
        if (coneMesh != null) Destroy(coneMesh);
        coneMesh = null;
    }

    void CreateSafeCylinderPrimitive(Transform parent, Vector3 localPos, Quaternion localRot, float diameter)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.transform.SetParent(parent);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale    = new Vector3(diameter, ScaleYCylinder, diameter);
        Destroy(go.GetComponent<Collider>());
        var r = go.GetComponent<Renderer>();
        if (r)
        {
            r.sharedMaterial = GetWarningMaterial();
            safeZoneRenderers.Add(r);
        }
    }

    void CreateCylinderPrimitive(Transform parent, Vector3 localPos, Quaternion localRot, float diameter)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.transform.SetParent(parent);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale    = new Vector3(diameter, ScaleYCylinder, diameter);
        Destroy(go.GetComponent<Collider>());
        var r = go.GetComponent<Renderer>();
        if (r) { r.sharedMaterial = GetWarningMaterial(); visualRenderers.Add(r); }
    }

    void CreateLinePrimitive(Transform parent, Vector3 localPos, Quaternion localRot, float width, float length)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.SetParent(parent);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale    = new Vector3(width, ScaleYCube, length);
        Destroy(go.GetComponent<Collider>());
        var r = go.GetComponent<Renderer>();
        if (r) { r.sharedMaterial = GetWarningMaterial(); visualRenderers.Add(r); }
    }

    void Update()
    {
        if (!initialized || visual == null || !visual.activeSelf) return;

        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / totalWarning);

        // วงขยาย/หด — ภาพฝั่ง client · ดาเมจใช้ scaleEnd ตอน resolve (ดู HitScale)
        if (!Mathf.Approximately(scaleStart, scaleEnd))
            visual.transform.localScale = visualBaseScale * CurrentScale;

        // ลำแสงกวาด — ทุก client คำนวณเองจากสูตรเดียวกัน ไม่ต้อง sync
        if (Mathf.Abs(sweepDegreesPerSecond) > 0.001f)
            transform.rotation = initialRotation * Quaternion.Euler(0f, sweepDegreesPerSecond * elapsed, 0f);

        // Fallback color (สำหรับ primitive ที่ไม่มี _FillProgress shader graph property)
        // ใช้ palette ตัวเดียวกับเส้นทาง shader จะได้ไม่เพี้ยนกันระหว่างทรงที่มี prefab กับที่ไม่มี
        // (Cone ใช้ทางนี้เสมอเพราะยังไม่มี prefab ของตัวเอง)
        var (warn, danger) = ResolveColors();
        float urgency  = Mathf.Lerp(1f, 8f, progress);
        float blink    = Mathf.Sin(Time.time * urgency * Mathf.PI) * 0.5f + 0.5f;
        Color baseColor = Color.Lerp(warn, danger, progress);
        float dim = 0.7f + blink * 0.3f;
        Color fallbackCol = new Color(baseColor.r * dim, baseColor.g * dim, baseColor.b * dim,
                                      baseColor.a * dim);

        // สี ring/ขอบ ไล่ตาม progress — ต้องอัปเดตทุกเฟรมเพราะ shader ไม่มีคู่สีของ ring ให้ lerp เอง
        Color ringCol = ResolveRingColor(progress);

        foreach (var r in visualRenderers)
            if (r) ApplyTelegraphState(r, progress, fallbackCol, ringCol);

        // safe zone (Donut center) — สีคงที่ ไม่กระพริบ
        foreach (var r in safeZoneRenderers)
            if (r) ApplyTelegraphState(r, 0f, safeZoneColor, ringCol);   // safe zone fill=0 ตลอด
    }

    /// <summary>
    /// อัปเดต state ของ telegraph material:
    /// • Shader Graph (TelegraphUniversal) → drive _FillProgress, ปล่อยให้ shader lerp _WarningColor→_DangerColor เอง
    /// • Standard/URP fallback              → set _BaseColor/_Color จากค่า fallbackColor ที่ C# คำนวณ
    /// </summary>
    void ApplyTelegraphState(Renderer r, float progress, Color fallbackColor, Color ringColor)
    {
        var sharedMat = r.sharedMaterial;
        if (sharedMat == null) return;

        mpb ??= new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);

        // Path 1 — Shader Graph มี _FillProgress: ใช้ shader-side warning→danger lerp
        if (sharedMat.HasProperty("_FillProgress"))
        {
            mpb.SetFloat("_FillProgress", progress);
            // พื้นวง shader lerp เองจาก _WarningColor/_DangerColor แต่ ring/ขอบ ไม่มีคู่สี
            // จึงต้องป้อนสีที่ lerp แล้วเข้าไปเองทุกเฟรม
            if (sharedMat.HasProperty("_OutlineColor")) mpb.SetColor("_OutlineColor", ringColor);
            r.SetPropertyBlock(mpb);
            return;
        }

        // Path 2 — Fallback: เซ็ตสีตรงๆ ให้ shader ทั่วไป
        if (sharedMat.HasProperty("_BaseColor")) mpb.SetColor("_BaseColor", fallbackColor);
        if (sharedMat.HasProperty("_Color"))     mpb.SetColor("_Color",     fallbackColor);
        if (sharedMat.HasProperty("_TintColor")) mpb.SetColor("_TintColor", fallbackColor);
        if (sharedMat.HasProperty("_EmissionColor"))
        {
            mpb.SetColor("_EmissionColor", new Color(fallbackColor.r, fallbackColor.g, fallbackColor.b) * 1.5f);
        }

        r.SetPropertyBlock(mpb);
    }

    Material GetWarningMaterial()
    {
        if (warningMaterial != null) return warningMaterial;

        if (cachedFallbackMaterial == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Standard");
            cachedFallbackMaterial = new Material(shader);

            cachedFallbackMaterial.SetFloat("_Surface", 1f);
            cachedFallbackMaterial.SetFloat("_Blend",   0f);
            cachedFallbackMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            cachedFallbackMaterial.renderQueue = 3000;

            cachedFallbackMaterial.SetFloat("_Mode", 3f);
            cachedFallbackMaterial.EnableKeyword("_ALPHABLEND_ON");

            cachedFallbackMaterial.color = new Color(1f, 0.8f, 0f, 0.4f);
        }
        return cachedFallbackMaterial;
    }
}
