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
    public enum AoEType { Circle, Line, Cross, Spread }

    [Header("Materials (ถ้าปล่อยว่างจะสร้าง runtime)")]
    public Material warningMaterial;   // transparent red — assign in Inspector
    public Material dangerMaterial;    // brighter red ตอนใกล้ระเบิด

    // ── Client-side visual ────────────────────────────────────────────────
    private GameObject         visual;
    private List<Renderer>     visualRenderers = new List<Renderer>();
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

    // ── Spawn Entry Point ─────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (IsServer)
            StartCoroutine(TelegraphSequence());
    }

    /// <summary>Server เรียกทันทีหลัง Spawn เพื่อส่งพารามิเตอร์ไปทุก client</summary>
    public void BroadcastInit()
    {
        InitClientRpc((int)aoeType, radius, lineLength, lineWidth, warningDuration, damage, spreadCount, spreadAngle);
    }

    // ── ClientRpc ─────────────────────────────────────────────────────────
    [ClientRpc]
    void InitClientRpc(int type, float r, float len, float wid, float warn, float dmg, int sCnt, float sAngle)
    {
        aoeType         = (AoEType)type;
        radius          = r;
        lineLength      = len;
        lineWidth       = wid;
        warningDuration = warn;
        damage          = dmg;
        spreadCount     = sCnt;
        spreadAngle     = sAngle;
        totalWarning    = warn;
        elapsed         = 0f;
        initialized     = true;

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
        yield return new WaitForSeconds(warningDuration);

        DealDamage();
        ExplodeClientRpc();

        yield return new WaitForSeconds(0.1f);
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
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
                _              => IsInLine(playerPos),   // Line
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
                    // center bar at half-length forward
                    Vector3 barCenter = rot * (Vector3.forward * lineLength * 0.5f);
                    CreateLinePrimitive(visual.transform, barCenter, rot, lineWidth, lineLength);
                }
                break;
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

        float urgency   = Mathf.Lerp(1f, 8f, progress);
        float blink     = Mathf.Sin(Time.time * urgency * Mathf.PI) * 0.5f + 0.5f;
        Color baseColor = new Color(1f, 1f - progress * 0.8f, 0f, Mathf.Lerp(0.35f, 0.75f, progress));
        Color finalCol  = baseColor * (0.7f + blink * 0.3f);

        foreach (var r in visualRenderers)
            if (r) r.material.color = finalCol;
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
