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
    public Material dangerMaterial;    // brighter red ตอนใกล้ระเบิด

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
        totalWarning          = init.warningDuration;
        elapsed               = 0f;
        initialized           = true;

        // Chase: แจ้ง player ที่ถูก target ว่าต้องวิ่งหนี
        if (isChasing && NetworkManager.Singleton != null
            && NetworkManager.Singleton.LocalClientId == chaseTargetClientId)
        {
            GameHUD.Instance?.ShowAnnouncement("⚡ TARGETED — RUN AWAY!", Color.magenta);
        }

        // Visual: 3D prefab (preferred) > runtime primitive (fallback)
        if (!TrySpawnVfxPrefab())
            CreateVisual();

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

        // ── 3D Scale Mapping (XZ plane — Y stays at prefab's design height) ──
        Vector3 s = aoeType switch
        {
            AoEType.Circle => new Vector3(radius * 2f, 1f, radius * 2f),
            AoEType.Donut  => new Vector3(radius * 2f, 1f, radius * 2f),
            AoEType.Line   => new Vector3(lineWidth,   1f, lineLength),
            AoEType.Cross  => new Vector3(lineWidth,   1f, lineLength),   // arm 1
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
            arm2.transform.localScale    = new Vector3(lineLength, 1f, lineWidth);
        }

        // ส่ง params เข้า VFX Graph (ถ้ามี)
        var vfx = visual.GetComponent<UnityEngine.VFX.VisualEffect>();
        if (vfx != null)
        {
            if (vfx.HasFloat("WarningDuration")) vfx.SetFloat("WarningDuration", warningDuration);
            if (vfx.HasFloat("InnerRadius"))     vfx.SetFloat("InnerRadius",     innerRadius);
        }

        // เก็บ renderer ทั้งหมด (visual + arm2) เพื่อให้ Update() ปรับสี warning→danger ได้
        // + ส่ง shader-graph params (Donut)
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

            // Chase: override สีเป็น magenta เพื่อแยกจาก AoE ปกติ
            if (isChasing)
            {
                if (sharedMat.HasProperty("_WarningColor"))
                    mpb.SetColor("_WarningColor", new Color(1f, 0.2f, 1f, 1f));
                if (sharedMat.HasProperty("_DangerColor"))
                    mpb.SetColor("_DangerColor", new Color(0.8f, 0f, 0.6f, 1f));
            }
            
            // Color Match: override สีตาม Client ID / Slot
            if (isColorMatch.Value)
            {
                ulong reqId = requiredClientId.Value;
                int slot = PlayerSlotRegistry.Instance != null
                    ? PlayerSlotRegistry.Instance.GetSlot(reqId) : -1;
                if (slot < 0) slot = (int)(reqId % 4);

                Color c = slot switch
                {
                    0 => Color.red,
                    1 => Color.blue,
                    2 => Color.green,
                    _ => Color.yellow,
                };
                if (sharedMat.HasProperty("_WarningColor")) mpb.SetColor("_WarningColor", c);
                if (sharedMat.HasProperty("_DangerColor")) mpb.SetColor("_DangerColor", c * 0.8f);
            }

            // เริ่ม fill ที่ 0 (กันค่าค้างจาก material asset)
            if (sharedMat.HasProperty("_FillProgress")) mpb.SetFloat("_FillProgress", 0f);

            r.SetPropertyBlock(mpb);
        }
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
                CreateConePrimitive(visual.transform, radius, coneAngle);
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
        go.transform.localPosition = new Vector3(0f, 0.02f, 0f);
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
        go.transform.localScale    = new Vector3(diameter, 0.02f, diameter);
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
        go.transform.localScale    = new Vector3(diameter, 0.02f, diameter);
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
        go.transform.localScale    = new Vector3(width, 0.02f, length);
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
        // warning (yellow) → danger (red): G channel ลดลงตาม progress
        float urgency  = Mathf.Lerp(1f, 8f, progress);
        float blink    = Mathf.Sin(Time.time * urgency * Mathf.PI) * 0.5f + 0.5f;
        Color baseColor = isChasing
            ? new Color(1f, 0f, Mathf.Lerp(1f, 0.3f, progress), Mathf.Lerp(0.35f, 0.75f, progress))
            : new Color(1f, Mathf.Lerp(0.85f, 0.0f, progress), 0f, Mathf.Lerp(0.45f, 0.85f, progress));
        Color fallbackCol = baseColor * (0.7f + blink * 0.3f);
        fallbackCol.a = baseColor.a * (0.7f + blink * 0.3f);

        foreach (var r in visualRenderers)
            if (r) ApplyTelegraphState(r, progress, fallbackCol);

        // safe zone (Donut center) — fixed teal, no blink
        Color safeCol = new Color(0.1f, 0.8f, 0.9f, 0.3f);
        foreach (var r in safeZoneRenderers)
            if (r) ApplyTelegraphState(r, 0f, safeCol);   // safe zone fill=0 ตลอด
    }

    /// <summary>
    /// อัปเดต state ของ telegraph material:
    /// • Shader Graph (TelegraphUniversal) → drive _FillProgress, ปล่อยให้ shader lerp _WarningColor→_DangerColor เอง
    /// • Standard/URP fallback              → set _BaseColor/_Color จากค่า fallbackColor ที่ C# คำนวณ
    /// </summary>
    void ApplyTelegraphState(Renderer r, float progress, Color fallbackColor)
    {
        var sharedMat = r.sharedMaterial;
        if (sharedMat == null) return;

        mpb ??= new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);

        // Path 1 — Shader Graph มี _FillProgress: ใช้ shader-side warning→danger lerp
        if (sharedMat.HasProperty("_FillProgress"))
        {
            mpb.SetFloat("_FillProgress", progress);
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
