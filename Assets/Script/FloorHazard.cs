using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Floor Hazard — Raid Mechanic
///
/// Server flow:
///   1. Spawn → Activate(center, arenaR, safeCount, safeR, warnDur, dmg)
///   2. Server สุ่มวาง Safe Zone 1–3 จุดภายใน arena radius
///   3. BroadcastInitClientRpc → clients สร้าง visual (แดง=อันตราย, เขียว=ปลอดภัย)
///   4. หมดเวลา → ดาเมจทุก player ที่อยู่นอก Safe Zone → despawn
///
/// Client visual:
///   - Cylinder แดงใหญ่ = danger zone (arena)
///   - Cylinder เขียวเล็ก = safe zones
///   - กระพริบเมื่อเหลือเวลา < 2s
///
/// Prefab ต้องการ: NetworkObject + FloorHazard.cs
/// </summary>
public class FloorHazard : NetworkBehaviour
{
    // ── Server-side state ─────────────────────────────────────────────────
    private Vector3   arenaCenter;
    private float     arenaRadius;
    private float     safeZoneRadius;
    private float     warnDuration;
    private float     failDamage;
    private bool      activated;
    private float     serverTimer;
    private Vector3[] serverSafeZones;

    // ── Client-side visual ────────────────────────────────────────────────
    private GameObject   dangerVisual;
    private GameObject[] safeVisuals;
    private Vector3[]    clientSafeZones;
    private float        clientSafeZoneRadius;
    private float        clientWarnDuration;
    private float        clientTimer;
    private bool         clientReady;

    // ── Entry Point (Server calls this right after Spawn) ─────────────────
    public void Activate(Vector3 center, float arenaR, int safeCount,
                         float safeR, float warnDur, float dmg)
    {
        arenaCenter    = center;
        arenaRadius    = arenaR;
        safeZoneRadius = safeR;
        warnDuration   = warnDur;
        failDamage     = dmg;
        serverTimer    = 0f;

        // สุ่มวาง Safe Zones
        safeCount = Mathf.Clamp(safeCount, 1, 3);
        serverSafeZones = new Vector3[safeCount];
        for (int i = 0; i < safeCount; i++)
        {
            float maxOffset = Mathf.Max(0.5f, arenaR - safeR * 1.5f);
            Vector2 rand = Random.insideUnitCircle * maxOffset;
            serverSafeZones[i] = center + new Vector3(rand.x, 0f, rand.y);
        }

        activated = true;
        BroadcastInitClientRpc(
            center, arenaR, safeR, warnDur, dmg,
            serverSafeZones[0],
            safeCount > 1 ? serverSafeZones[1] : Vector3.zero,
            safeCount > 2 ? serverSafeZones[2] : Vector3.zero,
            safeCount);
    }

    // ── ClientRpc ─────────────────────────────────────────────────────────
    [ClientRpc]
    void BroadcastInitClientRpc(
        Vector3 center, float arenaR, float safeR, float warnDur, float dmg,
        Vector3 pos0, Vector3 pos1, Vector3 pos2, int count)
    {
        arenaCenter          = center;
        clientWarnDuration   = warnDur;
        clientSafeZoneRadius = safeR;
        clientTimer          = 0f;
        failDamage           = dmg;

        clientSafeZones = new Vector3[count];
        clientSafeZones[0] = pos0;
        if (count > 1) clientSafeZones[1] = pos1;
        if (count > 2) clientSafeZones[2] = pos2;

        CreateVisuals(center, arenaR, safeR);
        clientReady = true;

        GameHUD.Instance
            ?.ShowAnnouncement("☢ FLOOR HAZARD! วิ่งเข้า Safe Zone!", new Color(1f, 0.5f, 0f));
    }

    [ClientRpc]
    void FloorDetonateClientRpc()
    {
        GameHUD.Instance
            ?.ShowAnnouncement("💥 FLOOR EXPLODES!", Color.red);
        DestroyVisuals();
    }

    // ── Server Update ──────────────────────────────────────────────────────
    void Update()
    {
        if (!IsServer || !activated) return;

        serverTimer += Time.deltaTime;

        if (serverTimer >= warnDuration)
        {
            activated = false;
            DealDamage();
            FloorDetonateClientRpc();
            StartCoroutine(DespawnDelayed(0.5f));
        }
    }

    // ── Client Visual Update ───────────────────────────────────────────────
    void LateUpdate()
    {
        if (!clientReady || dangerVisual == null) return;

        clientTimer += Time.deltaTime;

        float remaining = clientWarnDuration - clientTimer;
        bool  urgent    = remaining < 2f;

        Color dangerCol = urgent
            ? Color.Lerp(
                new Color(1f, 0f,   0f, 0.55f),
                new Color(1f, 0.3f, 0f, 0.25f),
                Mathf.Sin(Time.time * 12f) * 0.5f + 0.5f)
            : new Color(1f, 0f, 0f, 0.35f);

        var dangerRend = dangerVisual.GetComponent<Renderer>();
        if (dangerRend != null)
            dangerRend.material.color = dangerCol;
    }

    // ── Visuals ───────────────────────────────────────────────────────────
    void CreateVisuals(Vector3 center, float arenaR, float safeR)
    {
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                  ?? Shader.Find("Sprites/Default")
                  ?? Shader.Find("Unlit/Color");

        // ── Danger zone (big red flat cylinder) ──
        dangerVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        dangerVisual.transform.position   = center + Vector3.up * 0.02f;
        dangerVisual.transform.localScale = new Vector3(arenaR * 2f, 0.04f, arenaR * 2f);
        Destroy(dangerVisual.GetComponent<Collider>());

        if (shader != null)
        {
            var mat = new Material(shader);
            mat.color = new Color(1f, 0f, 0f, 0.35f);
            ApplyTransparency(mat);
            dangerVisual.GetComponent<Renderer>().material = mat;
        }
        dangerVisual.GetComponent<Renderer>().shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;

        // ── Safe zones (small green flat cylinders) ──
        if (clientSafeZones == null) return;
        safeVisuals = new GameObject[clientSafeZones.Length];
        for (int i = 0; i < clientSafeZones.Length; i++)
        {
            var safe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            safe.transform.position   = clientSafeZones[i] + Vector3.up * 0.04f;
            safe.transform.localScale = new Vector3(safeR * 2f, 0.05f, safeR * 2f);
            Destroy(safe.GetComponent<Collider>());

            if (shader != null)
            {
                var safeMat = new Material(shader);
                safeMat.color = new Color(0f, 0.9f, 0.2f, 0.55f);
                ApplyTransparency(safeMat);
                safe.GetComponent<Renderer>().material = safeMat;
            }
            safe.GetComponent<Renderer>().shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            safeVisuals[i] = safe;
        }
    }

    void ApplyTransparency(Material mat)
    {
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.renderQueue = 3000;
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
    }

    // ── Damage ────────────────────────────────────────────────────────────
    void DealDamage()
    {
        if (NetworkManager.Singleton == null) return;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var playerObj = client.PlayerObject;
            if (playerObj == null) continue;

            Vector3 pos = playerObj.transform.position;
            if (!IsInAnySafeZone(pos))
            {
                playerObj.GetComponent<playermove>()?.TakeDamage(failDamage);
                Debug.Log($"[FloorHazard] 💥 Client {client.ClientId} outside Safe Zone — {failDamage} dmg");
            }
            else
            {
                Debug.Log($"[FloorHazard] ✅ Client {client.ClientId} safe!");
            }
        }
    }

    bool IsInAnySafeZone(Vector3 pos)
    {
        if (serverSafeZones == null) return false;
        float r2 = safeZoneRadius * safeZoneRadius;
        foreach (var sz in serverSafeZones)
        {
            float dx = pos.x - sz.x;
            float dz = pos.z - sz.z;
            if (dx * dx + dz * dz <= r2) return true;
        }
        return false;
    }

    // ── Cleanup ───────────────────────────────────────────────────────────
    void DestroyVisuals()
    {
        if (dangerVisual) Destroy(dangerVisual);
        if (safeVisuals != null)
            foreach (var s in safeVisuals)
                if (s) Destroy(s);
        safeVisuals  = null;
        dangerVisual = null;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        DestroyVisuals();
    }

    IEnumerator DespawnDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        else Destroy(gameObject);
    }
}
