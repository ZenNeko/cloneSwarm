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
    public enum AoEType { Circle, Line, Cross, Spread, Donut, Cone, Chase }

    [Header("Materials (ถ้าปล่อยว่างจะสร้าง runtime)")]
    public Material warningMaterial;   // transparent red — assign in Inspector
    public Material dangerMaterial;    // brighter red ตอนใกล้ระเบิด

    // ── Client-side visual ────────────────────────────────────────────────
    private GameObject         visual;
    private List<Renderer>     visualRenderers     = new List<Renderer>();
    private List<Renderer>     safeZoneRenderers   = new List<Renderer>();
    private float              totalWarning;
    private float              elapsed;
    private bool               initialized;

    // ── Server-side params (set before Spawn, read via InitClientRpc) ─────
    [HideInInspector] public AoEType aoeType         = AoEType.Circle;
    [HideInInspector] public float   radius          = 3f;
    [HideInInspector] public float   lineLength      = 8f;
    [HideInInspector] public float   lineWidth       = 1.5f;
    [HideInInspector] public float   warningDuration = 2.5f;
    [HideInInspector] public float   damage          = 30f;
    [HideInInspector] public int     spreadCount     = 5;
    [HideInInspector] public float   spreadAngle     = 60f;
    [HideInInspector] public float   innerRadius     = 1.5f;   // Donut: safe zone inner radius
    [HideInInspector] public float   coneAngle       = 90f;    // Cone: sweep angle (degrees)
    [HideInInspector] public ulong   chaseTargetClientId = ulong.MaxValue; // Chase: target player

    // ── Spawn Entry Point ─────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (IsServer)
            StartCoroutine(TelegraphSequence());
    }

    /// <summary>Server เรียกทันทีหลัง Spawn เพื่อส่งพารามิเตอร์ไปทุก client</summary>
    public void BroadcastInit()
    {
        InitClientRpc((int)aoeType, radius, lineLength, lineWidth, warningDuration, damage,
                      spreadCount, spreadAngle, innerRadius, coneAngle, chaseTargetClientId);
    }

    // ── ClientRpc ─────────────────────────────────────────────────────────
    [ClientRpc]
    void InitClientRpc(int type, float r, float len, float wid, float warn, float dmg,
                       int sCnt, float sAngle, float innerR, float coneAng, ulong chaseId)
    {
        aoeType               = (AoEType)type;
        radius                = r;
        lineLength            = len;
        lineWidth             = wid;
        warningDuration       = warn;
        damage                = dmg;
        spreadCount           = sCnt;
        spreadAngle           = sAngle;
        innerRadius           = innerR;
        coneAngle             = coneAng;
        chaseTargetClientId   = chaseId;
        totalWarning          = warn;
        elapsed               = 0f;
        initialized           = true;

        // Chase: แจ้ง player ที่ถูก target ว่าต้องวิ่งหนี
        if (aoeType == AoEType.Chase && NetworkManager.Singleton != null
            && NetworkManager.Singleton.LocalClientId == chaseTargetClientId)
        {
            UnityEngine.Object.FindAnyObjectByType<GameHUD>()
                ?.ShowAnnouncement("⚡ TARGETED — RUN AWAY!", Color.magenta);
        }

        CreateVisual();
    }

    [ClientRpc]
    void ExplodeClientRpc()
    {
        if (visual) visual.SetActive(false);
    }

    // ── Server Sequence ────────────────────────────────────────────────────
    IEnumerator TelegraphSequence()
    {
        if (aoeType == AoEType.Chase)
            yield return StartCoroutine(ChaseSequence());
        else
            yield return new WaitForSeconds(warningDuration);

        DealDamage();
        ExplodeClientRpc();

        yield return new WaitForSeconds(0.1f);
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
    }

    /// <summary>
    /// Chase: server อัปเดตตำแหน่ง zone ตาม target player ตลอด warningDuration
    /// sync ไป clients ทุก 80ms
    /// </summary>
    IEnumerator ChaseSequence()
    {
        float timer     = 0f;
        float syncTimer = 0f;
        const float syncInterval = 0.08f;

        while (timer < warningDuration)
        {
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.ConnectedClients.TryGetValue(chaseTargetClientId, out var client) &&
                client.PlayerObject != null)
            {
                Vector3 tp = client.PlayerObject.transform.position;
                transform.position = new Vector3(tp.x, transform.position.y, tp.z);

                syncTimer += Time.deltaTime;
                if (syncTimer >= syncInterval)
                {
                    SyncChasePositionClientRpc(transform.position);
                    syncTimer = 0f;
                }
            }

            timer += Time.deltaTime;
            yield return null;
        }
    }

    [ClientRpc]
    void SyncChasePositionClientRpc(Vector3 newPos)
    {
        // อัปเดตตำแหน่ง zone บน client — visual เป็น child จะเลื่อนตามอัตโนมัติ
        transform.position = newPos;
    }

    void DealDamage()
    {
        if (!IsServer || NetworkManager.Singleton == null) return;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var playerObj = client.PlayerObject;
            if (playerObj == null) continue;

            Vector3 playerPos = playerObj.transform.position;
            bool inZone = aoeType switch
            {
                AoEType.Circle => IsInCircle(playerPos),
                AoEType.Cross  => IsInLine(playerPos) || IsInLineCross(playerPos),
                AoEType.Spread => IsInSpread(playerPos),
                AoEType.Donut  => IsInDonut(playerPos),
                AoEType.Cone   => IsInCone(playerPos),
                AoEType.Chase  => IsInCircle(playerPos),  // detonate ที่ตำแหน่งสุดท้ายที่ zone หยุด
                _              => IsInLine(playerPos),    // Line
            };

            if (inZone)
            {
                playerObj.GetComponent<playermove>()?.TakeDamage(damage);
                Debug.Log($"[TelegraphZone] ⚡ Hit player {client.ClientId} — {damage} dmg");
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

    bool IsInCone(Vector3 pos)
    {
        Vector3 toTarget = pos - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.001f) return true;
        return toTarget.magnitude <= radius
            && Vector3.Angle(transform.forward, toTarget) <= coneAngle * 0.5f;
    }

    // Spread: ตรวจว่า pos อยู่ใน fan-shaped ray ใดๆ
    bool IsInSpread(Vector3 pos)
    {
        if (spreadCount <= 0) return false;
        float halfSpread = spreadAngle * 0.5f;
        float step       = spreadCount > 1 ? spreadAngle / (spreadCount - 1) : 0f;

        for (int i = 0; i < spreadCount; i++)
        {
            float   angle    = -halfSpread + i * step;
            Vector3 rayDir   = Quaternion.Euler(0f, angle, 0f) * transform.forward;
            Quaternion rayRot = Quaternion.LookRotation(rayDir);
            Vector3 local    = Quaternion.Inverse(rayRot) * (pos - transform.position);
            if (Mathf.Abs(local.x) <= lineWidth * 0.5f && local.z >= 0f && local.z <= lineLength)
                return true;
        }
        return false;
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

            case AoEType.Spread:
                visual = new GameObject("Visual_Spread");
                visual.transform.SetParent(transform);
                visual.transform.localPosition = Vector3.zero;
                float halfSpread = spreadAngle * 0.5f;
                float step       = spreadCount > 1 ? spreadAngle / (spreadCount - 1) : 0f;
                for (int i = 0; i < spreadCount; i++)
                {
                    float angle = -halfSpread + i * step;
                    Quaternion rot = Quaternion.Euler(0f, angle, 0f);
                    Vector3 barCenter = rot * (Vector3.forward * lineLength * 0.5f);
                    CreateLinePrimitive(visual.transform, barCenter, rot, lineWidth, lineLength);
                }
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
                int   fanLines = Mathf.Max(4, Mathf.RoundToInt(coneAngle / 10f));
                float fanStep  = fanLines > 1 ? coneAngle / (fanLines - 1) : 0f;
                float halfCone = coneAngle * 0.5f;
                for (int i = 0; i < fanLines; i++)
                {
                    float      fanAngle = -halfCone + i * fanStep;
                    Quaternion fanRot   = Quaternion.Euler(0f, fanAngle, 0f);
                    Vector3    fanCen   = fanRot * (Vector3.forward * radius * 0.5f);
                    CreateLinePrimitive(visual.transform, fanCen, fanRot, lineWidth * 0.4f, radius);
                }
                break;

            case AoEType.Chase:
                // วงกลม magenta ที่วิ่งตาม target (ตัว zone เลื่อน = visual เลื่อน)
                visual = new GameObject("Visual_Chase");
                visual.transform.SetParent(transform);
                visual.transform.localPosition = Vector3.zero;
                CreateCylinderPrimitive(visual.transform, Vector3.zero, Quaternion.identity, radius * 2f);
                // เปลี่ยนสีเริ่มต้นเป็น magenta เพื่อให้แยกออกจาก AoE ปกติ
                foreach (var rr in visualRenderers)
                    if (rr) rr.material.color = new Color(1f, 0f, 1f, 0.4f);
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

        float urgency  = Mathf.Lerp(1f, 8f, progress);
        float blink    = Mathf.Sin(Time.time * urgency * Mathf.PI) * 0.5f + 0.5f;
        // Chase: magenta; อื่นๆ: yellow → orange
        Color baseColor = aoeType == AoEType.Chase
            ? new Color(1f, 0f, Mathf.Lerp(1f, 0.3f, progress), Mathf.Lerp(0.35f, 0.75f, progress))
            : new Color(1f, 1f - progress * 0.8f, 0f, Mathf.Lerp(0.35f, 0.75f, progress));
        Color finalCol = baseColor * (0.7f + blink * 0.3f);

        foreach (var r in visualRenderers)
            if (r) r.material.color = finalCol;

        // safe zone (Donut center) — fixed teal, no blink
        foreach (var r in safeZoneRenderers)
            if (r) r.material.color = new Color(0.1f, 0.8f, 0.9f, 0.3f);
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
