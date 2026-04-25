using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Boss Tether — Raid Mechanic
///
/// Server flow:
///   1. Spawn → SetPlayersClientRpc(clientIdA, clientIdB)
///   2. ถ้า player < 2 → ข้ามทันที
///   3. Server ตรวจระยะทุก frame
///      - ห่างพอ (>= requiredDistance) → TETHER BROKEN! → despawn
///      - หมดเวลา (>= duration)        → ดาเมจทั้งคู่ → despawn
///
/// Client visual:
///   LineRenderer เปล่งแสง cyan ระหว่างผู้เล่น 2 คน
///   กระพริบแดงเมื่อเหลือเวลา < 2s
///
/// Edge case: player online คนเดียว → ข้าม mechanic โดยอัตโนมัติ
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

    // ── Server-side state ─────────────────────────────────────────────────
    private ulong     clientIdA   = ulong.MaxValue;
    private ulong     clientIdB   = ulong.MaxValue;
    private float     timer       = 0f;
    private bool      resolved    = false;

    // ── Client-side visual ────────────────────────────────────────────────
    private LineRenderer lineRenderer;
    private Transform    playerATransform;
    private Transform    playerBTransform;
    private bool         clientInitialized;

    // ── Entry Point (Server calls this right after Spawn) ─────────────────
    public void Activate(ulong playerA, ulong playerB)
    {
        clientIdA = playerA;
        clientIdB = playerB;
        SetPlayersClientRpc(playerA, playerB);
    }

    /// <summary>ไม่มี player คู่ → ข้ามทันที</summary>
    public void SkipTether()
    {
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        else Destroy(gameObject);
    }

    // ── ClientRpc ─────────────────────────────────────────────────────────
    [ClientRpc]
    void SetPlayersClientRpc(ulong pA, ulong pB)
    {
        clientIdA = pA;
        clientIdB = pB;
        SetupLineRenderer();
        FindPlayerTransforms();
        clientInitialized = true;

        // แจ้ง local player ที่ถูก tether
        ulong myId = NetworkManager.Singleton.LocalClientId;
        if (myId == pA || myId == pB)
        {
            UnityEngine.Object.FindAnyObjectByType<GameHUD>()
                ?.ShowAnnouncement("🔗 TETHER! วิ่งออกจากกัน!", new Color(0f, 1f, 1f));
        }
    }

    [ClientRpc]
    void TetherBrokenClientRpc()
    {
        UnityEngine.Object.FindAnyObjectByType<GameHUD>()
            ?.ShowAnnouncement("✅ TETHER BROKEN!", Color.green);
    }

    [ClientRpc]
    void TetherFailedClientRpc()
    {
        UnityEngine.Object.FindAnyObjectByType<GameHUD>()
            ?.ShowAnnouncement("💥 TETHER EXPLODED!", Color.red);
    }

    // ── Server Update ──────────────────────────────────────────────────────
    void Update()
    {
        if (!IsServer || resolved) return;

        timer += Time.deltaTime;

        // ตรวจระยะ
        Transform tA = GetPlayerTransform(clientIdA);
        Transform tB = GetPlayerTransform(clientIdB);

        if (tA != null && tB != null)
        {
            float dist = Vector3.Distance(tA.position, tB.position);
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
            DealFailDamage(tA, tB);
            TetherFailedClientRpc();
            StartCoroutine(DespawnDelayed(0.5f));
        }
    }

    // ── Client Visual Update ───────────────────────────────────────────────
    void LateUpdate()
    {
        if (!clientInitialized || lineRenderer == null) return;

        // หา transforms ถ้ายังไม่ได้
        if (playerATransform == null || playerBTransform == null)
            FindPlayerTransforms();

        if (playerATransform == null || playerBTransform == null)
        {
            lineRenderer.enabled = false;
            return;
        }

        lineRenderer.enabled = true;
        lineRenderer.SetPosition(0, playerATransform.position + Vector3.up * 1f);
        lineRenderer.SetPosition(1, playerBTransform.position + Vector3.up * 1f);

        // กระพริบแดงเมื่อเหลือน้อยกว่า 2 วินาที
        float remaining = duration - timer;
        bool  urgent    = remaining < 2f;
        Color col       = urgent
            ? Color.Lerp(urgentColor, tetherColor, Mathf.Sin(Time.time * 10f) * 0.5f + 0.5f)
            : tetherColor;
        lineRenderer.startColor = col;
        lineRenderer.endColor   = col;
    }

    // ── Helpers ───────────────────────────────────────────────────────────
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
            lineRenderer.material = mat;
        }
    }

    void FindPlayerTransforms()
    {
        if (NetworkManager.Singleton == null) return;

        if (playerATransform == null && NetworkManager.Singleton.ConnectedClients.TryGetValue(clientIdA, out var cA))
            playerATransform = cA.PlayerObject?.transform;

        if (playerBTransform == null && NetworkManager.Singleton.ConnectedClients.TryGetValue(clientIdB, out var cB))
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

    IEnumerator DespawnDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        else Destroy(gameObject);
    }
}
