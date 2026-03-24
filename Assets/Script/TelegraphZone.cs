using System.Collections;
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
    public enum AoEType { Circle, Line }

    [Header("Materials (ถ้าปล่อยว่างจะสร้าง runtime)")]
    public Material warningMaterial;   // transparent red — assign in Inspector
    public Material dangerMaterial;    // brighter red ตอนใกล้ระเบิด

    // ── Client-side visual ────────────────────────────────────────────────
    private GameObject visual;
    private Renderer   visualRenderer;
    private float      totalWarning;
    private float      elapsed;
    private bool       initialized;

    // ── Server-side params (set before Spawn, read via InitClientRpc) ─────
    [HideInInspector] public AoEType aoeType       = AoEType.Circle;
    [HideInInspector] public float   radius        = 3f;
    [HideInInspector] public float   lineLength    = 8f;
    [HideInInspector] public float   lineWidth     = 1.5f;
    [HideInInspector] public float   warningDuration = 2.5f;
    [HideInInspector] public float   damage        = 30f;

    // ── Spawn Entry Point ─────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (IsServer)
            StartCoroutine(TelegraphSequence());
    }

    /// <summary>Server เรียกทันทีหลัง Spawn เพื่อส่งพารามิเตอร์ไปทุก client</summary>
    public void BroadcastInit()
    {
        InitClientRpc((int)aoeType, radius, lineLength, lineWidth, warningDuration, damage);
    }

    // ── ClientRpc ─────────────────────────────────────────────────────────
    [ClientRpc]
    void InitClientRpc(int type, float r, float len, float wid, float warn, float dmg)
    {
        aoeType         = (AoEType)type;
        radius          = r;
        lineLength      = len;
        lineWidth       = wid;
        warningDuration = warn;
        damage          = dmg;
        totalWarning    = warn;
        elapsed         = 0f;
        initialized     = true;

        CreateVisual();
    }

    [ClientRpc]
    void ExplodeClientRpc()
    {
        // flash สั้น ๆ แล้วซ่อน visual
        if (visual) visual.SetActive(false);
    }

    // ── Server Sequence ────────────────────────────────────────────────────
    IEnumerator TelegraphSequence()
    {
        yield return new WaitForSeconds(warningDuration);

        // ระเบิด: ดาเมจผู้เล่นที่อยู่ในโซน
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
            bool inZone = aoeType == AoEType.Circle
                ? IsInCircle(playerPos)
                : IsInLine(playerPos);

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
        // แปลง pos เป็น local space ของ zone
        Vector3 local = Quaternion.Inverse(transform.rotation) * (pos - transform.position);
        return Mathf.Abs(local.x) <= lineWidth * 0.5f
            && Mathf.Abs(local.z) <= lineLength * 0.5f;
    }

    // ── Client Visual ──────────────────────────────────────────────────────
    void CreateVisual()
    {
        if (aoeType == AoEType.Circle)
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.transform.SetParent(transform);
            visual.transform.localPosition = Vector3.zero;
            // Cylinder ใน Unity สูง 2 unit → scale Y บาง, XZ = diameter
            visual.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
        }
        else
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale    = new Vector3(lineWidth, 0.02f, lineLength);
        }

        // ลบ collider ออกจาก visual (ป้องกันกระทบ physics)
        Destroy(visual.GetComponent<Collider>());

        visualRenderer = visual.GetComponent<Renderer>();
        if (visualRenderer != null)
            visualRenderer.material = GetWarningMaterial();
    }

    void Update()
    {
        if (!initialized || visual == null || !visual.activeSelf) return;

        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / totalWarning);

        // กระพริบเร็วขึ้นเมื่อใกล้ระเบิด
        float urgency     = Mathf.Lerp(1f, 8f, progress);
        float blink       = Mathf.Sin(Time.time * urgency * Mathf.PI) * 0.5f + 0.5f;
        Color baseColor   = new Color(1f, 1f - progress * 0.8f, 0f, Mathf.Lerp(0.35f, 0.75f, progress));
        if (visualRenderer) visualRenderer.material.color = baseColor * (0.7f + blink * 0.3f);
    }

    Material GetWarningMaterial()
    {
        if (warningMaterial != null) return warningMaterial;

        // Fallback: สร้าง material runtime
        var shader = Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Standard");
        var mat = new Material(shader);

        // URP transparent
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend",   0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;

        // Standard transparent fallback
        mat.SetFloat("_Mode", 3f);
        mat.EnableKeyword("_ALPHABLEND_ON");

        mat.color = new Color(1f, 0.8f, 0f, 0.4f);
        return mat;
    }
}
