using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Boss Tether — Raid Mechanic · 4 โหมดใน component เดียว (ดู docs/adr-002-tether-modes.md)
///
/// แกนที่ใช้ร่วมกันทุกโหมด:
///   เป้าหลัก = clientIdA (ต้องยังไม่ตายเสมอ) · anchor = ผู้เล่นอีกคน (clientIdB) หรือเสา (transform.position)
///   Server ตรวจระยะทุก frame → resolve ตาม mode → ClientRpc ประกาศ → despawn
///
/// | mode  | ระหว่างทาง                | หมดเวลา                          |
/// |-------|---------------------------|----------------------------------|
/// | Far   | ห่างพอ → สำเร็จทันที       | ยังไม่ขาด = ดาเมจทั้งคู่           |
/// | Close | ไม่ทำอะไร                  | ห่างกัน = ดาเมจทั้งคู่ · ใกล้ = รอด |
/// | Leash | ออกนอกรัศมี → ดึงกลับ      | จบเฉยๆ ไม่มีดาเมจ                |
///
/// Client visual:
///   LineRenderer จาก playerA → (playerB | เสา) · สีตามโหมดแบบ FF14 —
///   ฟ้า = ต้องออกห่าง · เขียว = ต้องอยู่ใกล้ · ส้ม = ห้ามออกนอกวง
///   กระพริบแดงเมื่อเหลือ < 2s เฉพาะโหมดที่มีบทลงโทษ (Far/Close)
///
/// Prefab ต้องการ: NetworkObject + BossTether.cs
/// </summary>
public class BossTether : NetworkBehaviour
{
    // ── ค่าที่ TetherAction ทับตอน spawn ──────────────────────────────────
    // failDamage / minSeparationGain / leashPull* เป็น **server-only**
    // client ไม่เคยได้ค่าจริง (ไม่ใช่ NetworkVariable) — อย่าเอาไปใช้ใน visual
    // duration / mode / requiredDistance ใช้ทั้งสองฝั่ง จึงถูกส่งไปกับ ClientRpc
    [Header("Mode")]
    [Tooltip("โหมดของ tether — TetherAction เป็นคนตั้งตอน spawn")]
    public TetherMode mode = TetherMode.Far;
    [Tooltip("ใช้เมื่อ mode = Close · ตอนนี้ทำแค่ OnTimeout")]
    public CloseFailMode closeFailMode = CloseFailMode.OnTimeout;

    [Header("Tether Settings")]
    [Tooltip("Far = ระยะที่ต้องวิ่งห่างให้สายขาด · Close = ระยะที่ห้ามเกิน · Leash = รัศมีที่ห้ามออก — ส่งให้ client วาดวง")]
    public float requiredDistance = 8f;
    [Tooltip("ความยาวของกลไก (วินาที) — ส่งให้ client ผ่าน ClientRpc")]
    public float duration         = 6f;
    [Tooltip("ดาเมจที่รับเมื่อพลาด — server-only · Leash ไม่ใช้")]
    public float failDamage       = 40f;
    [Tooltip("Far เท่านั้น: ถ้าตอนผูกห่างกันเกิน requiredDistance อยู่แล้ว ต้องวิ่งห่างเพิ่มอีกเท่านี้ถึงจะแตก — server-only")]
    public float minSeparationGain = 2f;

    [Header("Leash")]
    [Tooltip("ความเร็วที่ดึงผู้เล่นกลับเข้าหา anchor (หน่วย/วินาที) — server-only")]
    public float leashPullSpeed    = 8f;
    [Tooltip("แต่ละครั้งที่ดึง กินเวลากี่วินาที — ต้องน้อยกว่า leashPullInterval ไม่งั้นผู้เล่นจะคุมตัวเองไม่ได้เลย")]
    public float leashPullDuration = 0.22f;
    [Tooltip("เว้นกี่วินาทีถึงดึงอีกครั้ง — กัน RPC ท่วมและเว้นจังหวะให้ผู้เล่นคุมตัวเองได้")]
    public float leashPullInterval = 0.45f;

    [Header("Visual")]
    [Tooltip("สีสายโหมด Far — ฟ้า = ต้องวิ่งออกห่าง")]
    public Color tetherColor     = new Color(0f, 1f, 1f, 0.9f);
    [Tooltip("สีสายโหมด Close — เขียว = ต้องอยู่ใกล้")]
    public Color closeColor      = new Color(0.2f, 1f, 0.35f, 0.9f);
    [Tooltip("สีสายโหมด Leash — ส้ม = ห้ามออกนอกวง")]
    public Color leashColor      = new Color(1f, 0.6f, 0.1f, 0.9f);
    [Tooltip("สีเมื่อใกล้หมดเวลา (เฉพาะโหมดที่มีบทลงโทษ)")]
    public Color urgentColor     = new Color(1f, 0.2f, 0.2f, 1f);
    [Tooltip("ความหนาสาย")]
    public float lineWidth       = 0.12f;

    [Header("Pillar Anchor")]
    [Tooltip("สีเสา anchor")]
    public Color pillarColor  = new Color(0.7f, 0.75f, 0.85f, 1f);
    [Tooltip("ความสูงเสา (เมตร)")]
    public float pillarHeight = 3f;
    [Tooltip("รัศมีเสา (เมตร)")]
    public float pillarRadius = 0.5f;
    [Tooltip("จำนวนด้านของวงรัศมีในโหมด Leash — สูงขึ้น = วงกลมเนียนขึ้น")]
    public int   leashRingSegments = 48;

    // ── Server-side state ─────────────────────────────────────────────────
    private ulong clientIdA = ulong.MaxValue;
    private ulong clientIdB = ulong.MaxValue;
    private float timer     = 0f;
    private bool  resolved  = false;
    private float breakDistance;        // requiredDistance ที่ปรับตามระยะเริ่มต้นแล้ว (Far เท่านั้น)
    private bool  anchorStartedDead;    // anchor เป็นศพตอนผูก → ถ้าฟื้นระหว่างทางต้องปล่อยผ่าน
    private float lastPullTime = -99f;  // Leash: กันดึงถี่เกิน

    // ── Shared (server + client) ──────────────────────────────────────────
    private bool pillarAnchor;          // anchor เป็นเสา (transform.position) ไม่ใช่ผู้เล่นอีกคน

    // ── Client-side visual ────────────────────────────────────────────────
    private LineRenderer lineRenderer;
    private Transform    playerATransform;
    private Transform    playerBTransform;
    private bool         clientInitialized;
    private GameObject   pillarVisual;
    private GameObject   ringVisual;
    private Material     lineMaterial;    // สร้างเองด้วย new Material() → ต้องลบเอง
    private Material     pillarMaterial;
    private Material     ringMaterial;
    private float        clientTimer;     // ขึ้นทุก client (ใช้คำนวณ remaining สำหรับ urgent flash)

    // ── Entry Points (Server calls this right after Spawn) ────────────────
    // TetherAction ตั้ง mode / closeFailMode / ค่าตัวเลขบน component ให้ก่อนแล้ว

    /// <summary>anchor เป็นผู้เล่นอีกคน · playerB เป็นศพได้เฉพาะโหมด Close</summary>
    public void Activate(ulong playerA, ulong playerB)
    {
        pillarAnchor = false;
        clientIdA    = playerA;
        clientIdB    = playerB;
        NormalizeUnimplemented();

        anchorStartedDead = IsClientDead(playerB);
        ResolveBreakDistance(GetPlayerTransform(playerA), GetPlayerTransform(playerB)?.position);
        InitTetherClientRpc(playerA, playerB, duration, requiredDistance, (int)mode, false);
    }

    /// <summary>anchor เป็นเสาที่ตำแหน่งของ tether เอง — ใช้ตอนไม่มีผู้เล่นคนอื่นให้ผูก และใช้กับ Leash</summary>
    public void ActivateOnPillar(ulong playerA)
    {
        pillarAnchor = true;
        clientIdA    = playerA;
        clientIdB    = ulong.MaxValue;
        NormalizeUnimplemented();

        anchorStartedDead = false;   // เสาไม่มีวันฟื้น
        ResolveBreakDistance(GetPlayerTransform(playerA), transform.position);
        InitTetherClientRpc(playerA, ulong.MaxValue, duration, requiredDistance, (int)mode, true);
    }

    /// <summary>
    /// โหมดที่ยังไม่ได้ทำต้อง**ดังเสมอ** แล้ว fallback ไปของที่ทำแล้ว
    /// ถ้าปล่อยเงียบ designer จะเจอ tether ที่ spawn ขึ้นมาแล้วไม่ทำอะไร แล้วอ่านว่าเป็นบั๊ก
    /// เรียกก่อนส่ง ClientRpc เสมอ เพื่อให้ client ได้ mode ที่ normalize แล้ว
    /// </summary>
    void NormalizeUnimplemented()
    {
        if (mode == TetherMode.Transferable)
        {
            Debug.LogWarning("[BossTether] TetherMode.Transferable ยังไม่ได้ทำ (ADR-002) — ใช้ Far แทน");
            mode = TetherMode.Far;
        }

        if (mode == TetherMode.Close && closeFailMode != CloseFailMode.OnTimeout)
        {
            Debug.LogWarning($"[BossTether] CloseFailMode.{closeFailMode} ยังไม่ได้ทำ (ADR-002) — ใช้ OnTimeout แทน");
            closeFailMode = CloseFailMode.OnTimeout;
        }
    }

    /// <summary>
    /// Far เท่านั้น — ถ้าตอนผูกบังเอิญห่างกันเกิน requiredDistance อยู่แล้ว tether จะแตกฟรีตั้งแต่เฟรมแรก
    /// (เกิดง่ายมากเมื่อ requiredDistance สูงและสนามกว้าง) ยกเกณฑ์ให้ต้องวิ่งห่างเพิ่มจริงๆ
    /// เคสปกติที่เริ่มใกล้กัน breakDistance = requiredDistance เท่าเดิม
    /// Close/Leash ใช้ requiredDistance ตรงๆ — เริ่มห่างอยู่แล้วไม่ใช่การได้เปรียบ
    /// </summary>
    void ResolveBreakDistance(Transform tA, Vector3? anchorPos)
    {
        breakDistance = requiredDistance;
        if (mode != TetherMode.Far || tA == null || !anchorPos.HasValue) return;

        float startDistance = Vector3.Distance(tA.position, anchorPos.Value);
        if (startDistance >= requiredDistance)
            breakDistance = startDistance + minSeparationGain;
    }

    // ── ClientRpc ─────────────────────────────────────────────────────────
    // enum ส่งเป็น int แล้ว cast กลับ — ตาม TelegraphZone.InitClientRpc
    [ClientRpc]
    void InitTetherClientRpc(ulong pA, ulong pB, float dur, float reqDist, int modeInt, bool pillar)
    {
        mode             = (TetherMode)modeInt;
        pillarAnchor     = pillar;
        clientIdA        = pA;
        clientIdB        = pB;
        // prefab default ไม่ตรงกับค่าที่ action ใช้ — ต้องรับมาจาก server ทั้งคู่
        duration         = dur;
        requiredDistance = reqDist;
        clientTimer      = 0f;

        SetupLineRenderer();
        // Leash วาดวงบนพื้นบอกขอบรัศมี ไม่ใช่เสา — เสาจะไปโผล่ทับตัวผู้เล่นพอดี
        // และสิ่งที่ผู้เล่นต้องเห็นคือ "ขอบ" ไม่ใช่ "จุดกึ่งกลาง"
        if (mode == TetherMode.Leash) SetupLeashRing();
        else if (pillar)              SetupPillarVisual();
        FindPlayerTransforms();
        clientInitialized = true;

        // แจ้งเฉพาะ local player ที่ถูกผูก
        ulong myId = NetworkManager.Singleton.LocalClientId;
        if (myId == pA || (!pillar && myId == pB))
            GameHUD.Instance?.ShowAnnouncement(AnnouncementText(), BaseLineColor());
    }

    string AnnouncementText() => mode switch
    {
        TetherMode.Close => pillarAnchor ? "🔗 TETHER! อยู่ใกล้เสาไว้!" : "🔗 TETHER! อยู่ใกล้กันไว้!",
        TetherMode.Leash => "⛓ LEASH! ห้ามออกนอกวง!",
        _                => pillarAnchor ? "🔗 TETHER! วิ่งออกจากเสา!" : "🔗 TETHER! วิ่งออกจากกัน!",
    };

    /// <summary>สีสายตามโหมด — ภาษาสีแบบ FF14: ฟ้า = ต้องออก · เขียว = ต้องเข้า · ส้ม = ห้ามออกนอกวง</summary>
    Color BaseLineColor() => mode switch
    {
        TetherMode.Close => closeColor,
        TetherMode.Leash => leashColor,
        _                => tetherColor,
    };

    /// <summary>Leash ไม่มีบทลงโทษตอนหมดเวลา การกระพริบเตือนจึงหลอกผู้เล่น</summary>
    bool HasTimeoutPenalty() => mode != TetherMode.Leash;

    [ClientRpc]
    void TetherSucceededClientRpc()
    {
        GameHUD.Instance?.ShowAnnouncement("✅ TETHER BROKEN!", Color.green);
    }

    [ClientRpc]
    void TetherFailedClientRpc()
    {
        GameHUD.Instance?.ShowAnnouncement("💥 TETHER EXPLODED!", Color.red);
    }

    // ── Server Update ──────────────────────────────────────────────────────
    void Update()
    {
        if (!IsServer || resolved) return;

        timer += Time.deltaTime;

        Transform tA = GetPlayerTransform(clientIdA);
        Vector3? anchorPos = pillarAnchor
            ? (Vector3?)transform.position
            : GetPlayerTransform(clientIdB)?.position;

        // anchor ที่เป็นศพตอนผูก แล้วฟื้นระหว่างกลไก — playermove.Respawn จะ teleport ศพ
        // ไปหา "ผู้เล่นที่รอดคนแรกในลิสต์" ซึ่งอาจไม่ใช่คนที่ถูกผูก anchor เลยกระโดดข้ามสนาม
        // ปล่อยผ่านไปเลย ไม่งั้นคนเป็นแพ้ทั้งที่ไม่ได้ทำอะไรผิด
        if (anchorStartedDead && !IsClientDead(clientIdB))
        {
            Succeed("anchor ฟื้นระหว่าง tether");
            return;
        }

        bool  hasDist = tA != null && anchorPos.HasValue;
        float dist    = hasDist ? Vector3.Distance(tA.position, anchorPos.Value) : 0f;

        switch (mode)
        {
            case TetherMode.Far:
                if (hasDist && dist >= breakDistance) { Succeed("แยกออกจากกันสำเร็จ"); return; }
                break;

            case TetherMode.Close:
                // CloseFailMode.OnTimeout — ไม่ลงโทษระหว่างทาง เพื่อให้แยกกันหลบ AoE ได้
                break;

            case TetherMode.Leash:
                if (hasDist && dist > requiredDistance) PullBackToAnchor(tA, anchorPos.Value);
                break;
        }

        if (timer < duration) return;

        resolved = true;
        switch (mode)
        {
            case TetherMode.Close:
                // anchor หายไปเลย (หลุดเกม) ไม่ลงโทษ — ผู้เล่นทำอะไรไม่ได้อยู่แล้ว
                if (hasDist && dist > requiredDistance) Fail("หมดเวลาแล้วยังห่างกัน");
                else                                    Succeed("อยู่ใกล้กันจนหมดเวลา");
                break;

            case TetherMode.Leash:
                Succeed("หมดเวลา leash");   // ไม่มีดาเมจ
                break;

            default:   // Far
                Fail("หมดเวลาแล้วสายยังไม่ขาด");
                break;
        }
    }

    // ── Resolve ───────────────────────────────────────────────────────────
    void Succeed(string reason)
    {
        resolved = true;
        TetherSucceededClientRpc();
        Debug.Log($"[BossTether] ✅ {mode} — {reason}");
        StartCoroutine(DespawnDelayed(0.5f));
    }

    void Fail(string reason)
    {
        resolved = true;

        // TakeDamage เช็ค isDead ให้อยู่แล้ว ยิงใส่ศพจึงไม่มีผล
        GetPlayerTransform(clientIdA)?.GetComponent<playermove>()?.TakeDamage(failDamage);
        if (!pillarAnchor)
            GetPlayerTransform(clientIdB)?.GetComponent<playermove>()?.TakeDamage(failDamage);

        TetherFailedClientRpc();
        Debug.Log($"[BossTether] 💥 {mode} — {reason} · ดาเมจ {failDamage}");
        StartCoroutine(DespawnDelayed(0.5f));
    }

    /// <summary>
    /// Leash — ดึงกลับด้วยทางเดียวกับ knockback ที่มีอยู่ (playermove.ApplyKnockbackClientRpc)
    /// การเคลื่อนที่เป็น owner-authoritative (owner เขียน rb.linearVelocity เอง) server จึง
    /// clamp ตำแหน่งตรงๆ ไม่ได้ — จะสู้กับ owner แล้วยางยืด · ดู ADR-002 Decision 2
    /// เว้นจังหวะระหว่างการดึงเพื่อให้ผู้เล่นยังคุมตัวเองได้บ้าง ไม่กลายเป็นกำแพงล่องหน
    /// </summary>
    void PullBackToAnchor(Transform tA, Vector3 anchor)
    {
        if (Time.time - lastPullTime < leashPullInterval) return;
        lastPullTime = Time.time;

        Vector3 dir = anchor - tA.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        tA.GetComponent<playermove>()
          ?.ApplyKnockbackClientRpc(dir.normalized * leashPullSpeed, leashPullDuration);
    }

    // ── Client Visual Update ───────────────────────────────────────────────
    void LateUpdate()
    {
        if (!clientInitialized || lineRenderer == null) return;

        // นับเวลาฝั่ง client (ทำงานทุก client รวม pure client)
        clientTimer += Time.deltaTime;

        // หา transforms ถ้ายังไม่ได้
        if (playerATransform == null) FindPlayerTransforms();
        if (!pillarAnchor && playerBTransform == null) FindPlayerTransforms();

        bool missing = playerATransform == null || (!pillarAnchor && playerBTransform == null);
        if (missing)
        {
            lineRenderer.enabled = false;
            return;
        }

        lineRenderer.enabled = true;
        lineRenderer.SetPosition(0, playerATransform.position + Vector3.up * 1f);

        Vector3 endPos = pillarAnchor
            ? transform.position + Vector3.up * 1f
            : playerBTransform.position + Vector3.up * 1f;
        lineRenderer.SetPosition(1, endPos);

        // กระพริบแดงเมื่อเหลือน้อยกว่า 2 วินาที — เฉพาะโหมดที่หมดเวลาแล้วเจ็บจริง
        Color baseCol = BaseLineColor();
        float remaining = duration - clientTimer;
        bool  urgent    = HasTimeoutPenalty() && remaining < 2f;
        Color col       = urgent
            ? Color.Lerp(urgentColor, baseCol, Mathf.Sin(Time.time * 10f) * 0.5f + 0.5f)
            : baseCol;
        lineRenderer.startColor = col;
        lineRenderer.endColor   = col;
    }

    // ── Visual Setup ──────────────────────────────────────────────────────
    void SetupLineRenderer()
    {
        Color baseCol = BaseLineColor();

        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount  = 2;
        lineRenderer.startWidth     = lineWidth;
        lineRenderer.endWidth       = lineWidth;
        lineRenderer.startColor     = baseCol;
        lineRenderer.endColor       = baseCol;
        lineRenderer.useWorldSpace  = true;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Shader ที่ใช้ได้แน่นอนใน URP
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                  ?? Shader.Find("Sprites/Default")
                  ?? Shader.Find("Unlit/Color");
        if (shader != null)
        {
            lineMaterial = new Material(shader) { color = baseCol };
            lineRenderer.sharedMaterial = lineMaterial;
        }
    }

    /// <summary>
    /// Leash — วงบนพื้นรัศมี requiredDistance บอกขอบเขตที่ห้ามออก
    /// กลไกนี้เล่นไม่ได้เลยถ้าไม่เห็นขอบ จึงต้องส่ง requiredDistance ข้ามมาฝั่ง client ด้วย
    /// (เป็นเหตุผลเดียวที่ค่านี้ไม่ใช่ server-only แล้ว)
    /// </summary>
    void SetupLeashRing()
    {
        int segments = Mathf.Max(8, leashRingSegments);

        ringVisual = new GameObject("LeashRing");
        ringVisual.transform.SetParent(transform, worldPositionStays: false);
        ringVisual.transform.localPosition = new Vector3(0f, 0.1f, 0f);

        var ring = ringVisual.AddComponent<LineRenderer>();
        ring.positionCount     = segments;
        ring.loop              = true;
        ring.useWorldSpace     = false;
        ring.startWidth        = lineWidth;
        ring.endWidth          = lineWidth;
        ring.startColor        = leashColor;
        ring.endColor          = leashColor;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        for (int i = 0; i < segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(a) * requiredDistance, 0f, Mathf.Sin(a) * requiredDistance));
        }

        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                  ?? Shader.Find("Sprites/Default")
                  ?? Shader.Find("Unlit/Color");
        if (shader != null)
        {
            ringMaterial = new Material(shader) { color = leashColor };
            ring.sharedMaterial = ringMaterial;
        }
    }

    void SetupPillarVisual()
    {
        pillarVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillarVisual.name = "TetherPillar";
        pillarVisual.transform.SetParent(transform, worldPositionStays: false);
        // Cylinder primitive's mesh height is 2 units, so scale.y = height/2
        pillarVisual.transform.localPosition = new Vector3(0f, pillarHeight * 0.5f, 0f);
        pillarVisual.transform.localScale    = new Vector3(pillarRadius * 2f, pillarHeight * 0.5f, pillarRadius * 2f);
        Destroy(pillarVisual.GetComponent<Collider>());

        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                  ?? Shader.Find("Sprites/Default")
                  ?? Shader.Find("Unlit/Color");
        var rend = pillarVisual.GetComponent<Renderer>();
        if (shader != null)
        {
            pillarMaterial = new Material(shader) { color = pillarColor };
            rend.sharedMaterial = pillarMaterial;
        }
        else if (rend != null)
        {
            var mpb = new MaterialPropertyBlock();
            rend.GetPropertyBlock(mpb);
            var sharedMat = rend.sharedMaterial;
            if (sharedMat != null)
            {
                if (sharedMat.HasProperty("_BaseColor")) mpb.SetColor("_BaseColor", pillarColor);
                if (sharedMat.HasProperty("_Color"))     mpb.SetColor("_Color",     pillarColor);
            }
            rend.SetPropertyBlock(mpb);
        }
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    void FindPlayerTransforms()
    {
        if (NetworkManager.Singleton == null) return;

        if (playerATransform == null && NetworkManager.Singleton.ConnectedClients.TryGetValue(clientIdA, out var cA))
            playerATransform = cA.PlayerObject?.transform;

        if (!pillarAnchor && playerBTransform == null
            && NetworkManager.Singleton.ConnectedClients.TryGetValue(clientIdB, out var cB))
            playerBTransform = cB.PlayerObject?.transform;
    }

    Transform GetPlayerTransform(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return null;
        NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client);
        return client?.PlayerObject?.transform;
    }

    bool IsClientDead(ulong clientId)
    {
        var pm = GetPlayerTransform(clientId)?.GetComponent<playermove>();
        return pm != null && pm.isDead.Value;
    }

    IEnumerator DespawnDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        else Destroy(gameObject);
    }

    // ── Cleanup ───────────────────────────────────────────────────────────
    // Material ที่ new เองไม่ถูกเก็บกวาดพร้อม GameObject — บอสยิง tether หลายรอบต่อเกม
    // ถ้าไม่ลบเองจะสะสมไปเรื่อยๆ · ใช้ OnDestroy เพราะรันแน่ทั้ง despawn ปกติและตอนปิดฉาก
    void OnDestroy()
    {
        if (pillarVisual)   Destroy(pillarVisual);
        if (ringVisual)     Destroy(ringVisual);
        if (lineMaterial)   Destroy(lineMaterial);
        if (pillarMaterial) Destroy(pillarMaterial);
        if (ringMaterial)   Destroy(ringMaterial);
        pillarVisual   = null;
        ringVisual     = null;
        lineMaterial   = null;
        pillarMaterial = null;
        ringMaterial   = null;
    }
}
