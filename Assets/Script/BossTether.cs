using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Boss Tether — Raid Mechanic
///
/// Co-op flow (>= 2 players):
///   1. Activate(playerA, playerB) → SetPlayersClientRpc(A, B)
///   2. Server ตรวจระยะ player A vs player B ทุก frame
///      - ห่างพอ → TETHER BROKEN!
///      - หมดเวลา → ดาเมจทั้งคู่
///
/// Solo flow (== 1 player):
///   1. ActivateSolo(playerA) → SetSoloPlayerClientRpc(A)
///   2. ทุก client สร้าง "เสา anchor" ที่ตำแหน่ง tether transform
///   3. Server ตรวจระยะ player A vs pillar (= transform.position)
///      - ห่างพอ → TETHER BROKEN!
///      - หมดเวลา → ดาเมจ player A คนเดียว
///
/// Client visual:
///   LineRenderer cyan ระหว่าง playerA → (playerB | pillar)
///   กระพริบแดงเมื่อเหลือเวลา < 2s (ใช้ clientTimer ที่ขึ้นทุก client)
///
/// Prefab ต้องการ: NetworkObject + BossTether.cs
/// </summary>
public class BossTether : NetworkBehaviour
{
    [Header("Tether Settings")]
    [Tooltip("ระยะขั้นต่ำที่ต้องวิ่งห่างกัน (เมตร)")]
    public float requiredDistance = 8f;
    [Tooltip("เวลาที่ให้วิ่งแยก (วินาที)")]
    public float duration         = 6f;
    [Tooltip("ดาเมจที่รับถ้าไม่แยกทัน")]
    public float failDamage       = 40f;

    [Header("Visual")]
    [Tooltip("สีสาย tether ปกติ")]
    public Color tetherColor     = new Color(0f, 1f, 1f, 0.9f);
    [Tooltip("สีเมื่อใกล้หมดเวลา")]
    public Color urgentColor     = new Color(1f, 0.2f, 0.2f, 1f);
    [Tooltip("ความหนาสาย")]
    public float lineWidth       = 0.12f;

    [Header("Solo Mode (Pillar)")]
    [Tooltip("สีเสา anchor ในโหมด solo")]
    public Color pillarColor  = new Color(0.7f, 0.75f, 0.85f, 1f);
    [Tooltip("ความสูงเสา (เมตร)")]
    public float pillarHeight = 3f;
    [Tooltip("รัศมีเสา (เมตร)")]
    public float pillarRadius = 0.5f;

    // ── Server-side state ─────────────────────────────────────────────────
    private ulong     clientIdA   = ulong.MaxValue;
    private ulong     clientIdB   = ulong.MaxValue;
    private float     timer       = 0f;
    private bool      resolved    = false;
    private bool      soloMode    = false;

    // ── Client-side visual ────────────────────────────────────────────────
    private LineRenderer lineRenderer;
    private Transform    playerATransform;
    private Transform    playerBTransform;
    private bool         clientInitialized;
    private GameObject   pillarVisual;
    private float        clientTimer;     // ขึ้นทุก client (ใช้คำนวณ remaining สำหรับ urgent flash)

    // ── Entry Points (Server calls this right after Spawn) ────────────────

    /// <summary>โหมด co-op — tether ระหว่างผู้เล่น 2 คน</summary>
    public void Activate(ulong playerA, ulong playerB)
    {
        soloMode  = false;
        clientIdA = playerA;
        clientIdB = playerB;
        SetPlayersClientRpc(playerA, playerB);
    }

    /// <summary>โหมด solo — tether ระหว่างผู้เล่น 1 คนกับเสา anchor (= transform.position)</summary>
    public void ActivateSolo(ulong playerA)
    {
        soloMode  = true;
        clientIdA = playerA;
        clientIdB = ulong.MaxValue;
        SetSoloPlayerClientRpc(playerA);
    }

    /// <summary>ไม่มี player → ข้ามทันที</summary>
    public void SkipTether()
    {
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        else Destroy(gameObject);
    }

    // ── ClientRpc ─────────────────────────────────────────────────────────
    [ClientRpc]
    void SetPlayersClientRpc(ulong pA, ulong pB)
    {
        soloMode    = false;
        clientIdA   = pA;
        clientIdB   = pB;
        clientTimer = 0f;
        SetupLineRenderer();
        FindPlayerTransforms();
        clientInitialized = true;

        // แจ้ง local player ที่ถูก tether
        ulong myId = NetworkManager.Singleton.LocalClientId;
        if (myId == pA || myId == pB)
        {
            GameHUD.Instance
                ?.ShowAnnouncement("🔗 TETHER! วิ่งออกจากกัน!", new Color(0f, 1f, 1f));
        }
    }

    [ClientRpc]
    void SetSoloPlayerClientRpc(ulong pA)
    {
        soloMode    = true;
        clientIdA   = pA;
        clientIdB   = ulong.MaxValue;
        clientTimer = 0f;
        SetupLineRenderer();
        SetupPillarVisual();
        FindPlayerTransforms();
        clientInitialized = true;

        // แจ้ง local player ถ้าโดน tether
        ulong myId = NetworkManager.Singleton.LocalClientId;
        if (myId == pA)
        {
            GameHUD.Instance
                ?.ShowAnnouncement("🔗 TETHER! วิ่งออกจากเสา!", new Color(0f, 1f, 1f));
        }
    }

    [ClientRpc]
    void TetherBrokenClientRpc()
    {
        GameHUD.Instance
            ?.ShowAnnouncement("✅ TETHER BROKEN!", Color.green);
    }

    [ClientRpc]
    void TetherFailedClientRpc()
    {
        GameHUD.Instance
            ?.ShowAnnouncement("💥 TETHER EXPLODED!", Color.red);
    }

    // ── Server Update ──────────────────────────────────────────────────────
    void Update()
    {
        if (!IsServer || resolved) return;

        timer += Time.deltaTime;

        // ตรวจระยะ
        Transform tA = GetPlayerTransform(clientIdA);
        Vector3?  anchorPos = soloMode
            ? (Vector3?)transform.position
            : GetPlayerTransform(clientIdB)?.position;

        if (tA != null && anchorPos.HasValue)
        {
            float dist = Vector3.Distance(tA.position, anchorPos.Value);
            if (dist >= requiredDistance)
            {
                resolved = true;
                TetherBrokenClientRpc();
                StartCoroutine(DespawnDelayed(0.5f));
                return;
            }
        }

        // หมดเวลา
        if (timer >= duration)
        {
            resolved = true;
            if (soloMode)
                DealSoloFailDamage(tA);
            else
                DealFailDamage(tA, GetPlayerTransform(clientIdB));
            TetherFailedClientRpc();
            StartCoroutine(DespawnDelayed(0.5f));
        }
    }

    // ── Client Visual Update ───────────────────────────────────────────────
    void LateUpdate()
    {
        if (!clientInitialized || lineRenderer == null) return;

        // นับเวลาฝั่ง client (ทำงานทุก client รวม pure client)
        clientTimer += Time.deltaTime;

        // หา transforms ถ้ายังไม่ได้
        if (playerATransform == null) FindPlayerTransforms();
        if (!soloMode && playerBTransform == null) FindPlayerTransforms();

        bool missing = playerATransform == null || (!soloMode && playerBTransform == null);
        if (missing)
        {
            lineRenderer.enabled = false;
            return;
        }

        lineRenderer.enabled = true;
        lineRenderer.SetPosition(0, playerATransform.position + Vector3.up * 1f);

        Vector3 endPos = soloMode
            ? transform.position + Vector3.up * 1f
            : playerBTransform.position + Vector3.up * 1f;
        lineRenderer.SetPosition(1, endPos);

        // กระพริบแดงเมื่อเหลือน้อยกว่า 2 วินาที
        float remaining = duration - clientTimer;
        bool  urgent    = remaining < 2f;
        Color col       = urgent
            ? Color.Lerp(urgentColor, tetherColor, Mathf.Sin(Time.time * 10f) * 0.5f + 0.5f)
            : tetherColor;
        lineRenderer.startColor = col;
        lineRenderer.endColor   = col;
    }

    // ── Visual Setup ──────────────────────────────────────────────────────
    void SetupLineRenderer()
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount  = 2;
        lineRenderer.startWidth     = lineWidth;
        lineRenderer.endWidth       = lineWidth;
        lineRenderer.startColor     = tetherColor;
        lineRenderer.endColor       = tetherColor;
        lineRenderer.useWorldSpace  = true;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Shader ที่ใช้ได้แน่นอนใน URP
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                  ?? Shader.Find("Sprites/Default")
                  ?? Shader.Find("Unlit/Color");
        if (shader != null)
        {
            var mat = new Material(shader);
            mat.color = tetherColor;
            lineRenderer.sharedMaterial = mat;
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
            var mat = new Material(shader);
            mat.color = pillarColor;
            rend.sharedMaterial = mat;
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

        if (!soloMode && playerBTransform == null
            && NetworkManager.Singleton.ConnectedClients.TryGetValue(clientIdB, out var cB))
            playerBTransform = cB.PlayerObject?.transform;
    }

    Transform GetPlayerTransform(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return null;
        NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client);
        return client?.PlayerObject?.transform;
    }

    void DealFailDamage(Transform tA, Transform tB)
    {
        tA?.GetComponent<playermove>()?.TakeDamage(failDamage);
        tB?.GetComponent<playermove>()?.TakeDamage(failDamage);
        Debug.Log($"[BossTether] 💥 Tether failed — dealt {failDamage} dmg to both players");
    }

    void DealSoloFailDamage(Transform tA)
    {
        tA?.GetComponent<playermove>()?.TakeDamage(failDamage);
        Debug.Log($"[BossTether] 💥 Solo tether failed — dealt {failDamage} dmg to player");
    }

    IEnumerator DespawnDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        else Destroy(gameObject);
    }

    // ── Cleanup ───────────────────────────────────────────────────────────
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (pillarVisual) Destroy(pillarVisual);
        pillarVisual = null;
    }
}
