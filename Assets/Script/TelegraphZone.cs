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
    [HideInInspector] public float   knockbackForce    = 0f;
    [HideInInspector] public float   knockbackDuration = 0.2f;

    [HideInInspector] public bool    isRotatingChase   = false;
    [HideInInspector] public NetworkObject casterNetworkObject;

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
        InitClientRpc((int)aoeType, radius, lineLength, lineWidth, warningDuration, damage,
                      innerRadius, chaseTargetClientId, isChasing, isStackMarker, isGaze, isRotatingChase);
    }

    // ── ClientRpc ─────────────────────────────────────────────────────────
    [ClientRpc]
    void InitClientRpc(int type, float r, float len, float wid, float warn, float dmg,
                       float innerR, ulong chaseId, bool chasing, bool stack, bool gaze, bool rotatingChase)
    {
        aoeType               = (AoEType)type;
        radius                = r;
        lineLength            = len;
        lineWidth             = wid;
        warningDuration       = warn;
        damage                = dmg;
        innerRadius           = innerR;
        chaseTargetClientId   = chaseId;
        isChasing             = chasing;
        isStackMarker         = stack;
        isGaze                = gaze;
        isRotatingChase       = rotatingChase;
        totalWarning          = warn;
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
        var rends = root.GetComponentsInChildren<Renderer>();
        foreach (var r in rends)
        {
            if (r == null) continue;
            visualRenderers.Add(r);

            var mat = r.material;

            // Chase: override สีเป็น magenta เพื่อแยกจาก AoE ปกติ
            if (isChasing)
            {
                if (mat.HasProperty("_WarningColor"))
                    mat.SetColor("_WarningColor", new Color(1f, 0.2f, 1f, 1f));
                if (mat.HasProperty("_DangerColor"))
                    mat.SetColor("_DangerColor", new Color(0.8f, 0f, 0.6f, 1f));
            }
            
            // Color Match: override สีตาม Client ID
            if (isColorMatch.Value)
            {
                Color c = ((int)(requiredClientId.Value % 4)) switch
                {
                    0 => Color.red,
                    1 => Color.blue,
                    2 => Color.green,
                    _ => Color.yellow,
                };
                if (mat.HasProperty("_WarningColor")) mat.SetColor("_WarningColor", c);
                if (mat.HasProperty("_DangerColor")) mat.SetColor("_DangerColor", c * 0.8f);
            }

            // เริ่ม fill ที่ 0 (กันค่าค้างจาก material asset)
            if (mat.HasProperty("_FillProgress")) mat.SetFloat("_FillProgress", 0f);
        }
    }

    [ClientRpc]
    void ExplodeClientRpc()
    {
        if (visual) visual.SetActive(false);

        // Detonate VFX (impact shockwave)
        if (detonateVfxPrefab != null)
        {
            var fx = Instantiate(detonateVfxPrefab, transform.position, Quaternion.identity);
            // Scale ตาม radius ของ AoE
            float scale = aoeType switch
            {
                AoEType.Circle => radius,
                AoEType.Donut  => radius,
                _              => Mathf.Max(lineWidth, lineLength * 0.3f),
            };
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

            if (knockbackForce > 0f)
            {
                Vector3 pushDir = (pm.transform.position - transform.position).normalized;
                pushDir.y = 0f;
                pushDir = pushDir.normalized;
                pm.ApplyKnockbackClientRpc(pushDir * knockbackForce, knockbackDuration);
            }
        }
    }

    bool IsInCircle(Vector3 pos)
    {
        Vector2 d = new Vector2(pos.x - transform.position.x, pos.z - transform.position.z);
        return d.magnitude <= radius;
    }

    bool IsInLine(Vector3 pos)
    {
        Vector3 local = Quaternion.Inverse(transform.rotation) * (pos - transform.position);
        return Mathf.Abs(local.x) <= lineWidth * 0.5f
            && Mathf.Abs(local.z) <= lineLength * 0.5f;
    }

    // Line ที่หมุน 90° (ใช้สำหรับ Cross)
    bool IsInLineCross(Vector3 pos)
    {
        Quaternion rot90 = transform.rotation * Quaternion.Euler(0f, 90f, 0f);
        Vector3 local = Quaternion.Inverse(rot90) * (pos - transform.position);
        return Mathf.Abs(local.x) <= lineWidth * 0.5f
            && Mathf.Abs(local.z) <= lineLength * 0.5f;
    }

    bool IsInDonut(Vector3 pos)
    {
        float dist = new Vector2(pos.x - transform.position.x,
                                 pos.z - transform.position.z).magnitude;
        return dist >= innerRadius && dist <= radius;
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
        }
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
            r.material       = GetWarningMaterial();
            r.material.color = new Color(0.1f, 0.8f, 0.9f, 0.3f);   // teal — safe zone
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
        if (r) { r.material = GetWarningMaterial(); visualRenderers.Add(r); }
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
        if (r) { r.material = GetWarningMaterial(); visualRenderers.Add(r); }
    }

    void Update()
    {
        if (!initialized || visual == null || !visual.activeSelf) return;

        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / totalWarning);

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
    static void ApplyTelegraphState(Renderer r, float progress, Color fallbackColor)
    {
        var mat = r.material;

        // Path 1 — Shader Graph มี _FillProgress: ใช้ shader-side warning→danger lerp
        if (mat.HasProperty("_FillProgress"))
        {
            mat.SetFloat("_FillProgress", progress);
            return;
        }

        // Path 2 — Fallback: เซ็ตสีตรงๆ ให้ shader ทั่วไป
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", fallbackColor);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     fallbackColor);
        if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", fallbackColor);
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(fallbackColor.r, fallbackColor.g, fallbackColor.b) * 1.5f);
        }
    }

    Material GetWarningMaterial()
    {
        if (warningMaterial != null) return warningMaterial;

        var shader = Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Standard");
        var mat = new Material(shader);

        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend",   0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;

        mat.SetFloat("_Mode", 3f);
        mat.EnableKeyword("_ALPHABLEND_ON");

        mat.color = new Color(1f, 0.8f, 0f, 0.4f);
        return mat;
    }
}
