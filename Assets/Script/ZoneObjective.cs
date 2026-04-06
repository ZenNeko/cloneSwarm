using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Zone Objective — ยืนบนพื้นที่ที่กำหนดเพื่อรับรางวัล
///
/// Server: ตรวจสอบ distance ผู้เล่น → เติม progress → complete → reward → despawn
/// Client: อ่าน NetworkVariable → อัปเดต shader property _Progress บน disc พื้น
///
/// Shader: Assets/Shaders/ZoneObjectiveFill.shader
///   — Quad นอนราบ, UV center=(0.5,0.5), radial fill ตามเข็มนาฬิกาจากด้านบน
/// </summary>
public class ZoneObjective : NetworkBehaviour
{
    [Header("Settings")]
    public float zoneRadius      = 3f;
    [Tooltip("วินาทีที่ต้องยืนอยู่รวม (progress หยุดเมื่อออก แต่ไม่รีเซ็ต)")]
    public float requiredTime    = 8f;
    [Tooltip("วินาทีก่อน timeout (0 = ไม่มี)")]
    public float timeoutDuration = 60f;

    [Header("Rewards")]
    public float      expReward  = 80f;
    public float      healAmount = 20f;
    [Tooltip("ObjectiveOrb prefab (มี NetworkObject) — spawn ณ ตำแหน่ง zone เมื่อ complete\n" +
             "ปล่อยว่างเพื่อไม่ให้ spawn orb")]
    public GameObject orbPrefab;

    [Header("Visual")]
    [Tooltip("Quad prefab — ถ้าปล่อยว่างจะสร้าง runtime Quad")]
    public GameObject zoneVisualPrefab;

    // ── Network State ─────────────────────────────────────────────────────
    public NetworkVariable<float> progress      = new(0f,    NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int>   playersInZone = new(0,     NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool>  isComplete    = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ── Static Events ─────────────────────────────────────────────────────
    public static event System.Action<ZoneObjective> OnObjectiveSpawned;
    public static event System.Action<ZoneObjective> OnObjectiveCompleted;
    public static event System.Action<ZoneObjective> OnObjectiveExpired;

    // ── Client Visual ─────────────────────────────────────────────────────
    private GameObject runtimeDisc;
    private Material   discMat;       // instance material บน Quad
    private float      pulseT;

    // Shader property IDs (cached)
    static readonly int ID_Progress = Shader.PropertyToID("_Progress");
    static readonly int ID_ColorA   = Shader.PropertyToID("_ColorA");
    static readonly int ID_ColorB   = Shader.PropertyToID("_ColorB");

    // ── Lifecycle ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (IsServer)
            StartCoroutine(ObjectiveLoop());

        CreateDisc();
        progress.OnValueChanged      += (_, v) => UpdateShader(v);
        playersInZone.OnValueChanged += OnPlayersInZoneChanged;
        OnObjectiveSpawned?.Invoke(this);

        AnnounceHUD("ZONE OBJECTIVE!", new Color(0.20f, 0.85f, 1.00f));
    }

    // ── Server Logic ──────────────────────────────────────────────────────
    IEnumerator ObjectiveLoop()
    {
        float timeoutAt = timeoutDuration > 0 ? Time.time + timeoutDuration : float.MaxValue;

        while (progress.Value < 1f)
        {
            int count = CountPlayersInZone();
            playersInZone.Value = count;

            if (count > 0)
                progress.Value = Mathf.Min(1f, progress.Value + Time.deltaTime / requiredTime);

            if (Time.time >= timeoutAt)
            {
                ObjectiveExpiredClientRpc();
                OnObjectiveExpired?.Invoke(this);
                if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
                yield break;
            }

            yield return null;
        }

        isComplete.Value = true;
        GiveRewards();
        ObjectiveCompleteClientRpc();
        OnObjectiveCompleted?.Invoke(this);
        Debug.Log("[ZoneObjective] ✅ Completed!");

        yield return new WaitForSeconds(2f);
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
    }

    int CountPlayersInZone()
    {
        if (NetworkManager.Singleton == null) return 0;
        int count = 0;
        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
        {
            var obj = c.PlayerObject;
            if (obj == null) continue;
            float dx = obj.transform.position.x - transform.position.x;
            float dz = obj.transform.position.z - transform.position.z;
            if (dx * dx + dz * dz <= zoneRadius * zoneRadius) count++;
        }
        return count;
    }

    void GiveRewards()
    {
        SharedExperienceManager.Instance?.AddExp(expReward);

        if (healAmount > 0 && NetworkManager.Singleton != null)
            foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
                c.PlayerObject?.GetComponent<playermove>()?.Heal(healAmount);

        // Spawn Objective Orb ณ ตำแหน่ง zone (เล็กน้อยขึ้นในอากาศ)
        if (orbPrefab != null)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 0.6f;
            var orbGo = Instantiate(orbPrefab, spawnPos, Quaternion.identity);
            var no    = orbGo.GetComponent<NetworkObject>();
            if (no != null) no.Spawn(true);
            else Debug.LogWarning("[ZoneObjective] orbPrefab ไม่มี NetworkObject component");
        }

        Debug.Log($"[ZoneObjective] Reward — EXP+{expReward} Heal+{healAmount}" +
                  (orbPrefab != null ? " + OrbSpawned" : ""));
    }

    // ── ClientRpc ─────────────────────────────────────────────────────────
    [ClientRpc]
    void ObjectiveCompleteClientRpc()
    {
        // Snap fill ให้เต็ม + เปลี่ยนสีเป็นเขียว
        if (discMat != null)
        {
            discMat.SetFloat(ID_Progress, 1f);
            discMat.SetColor(ID_ColorA, new Color(0f, 1f, 0.35f, 0.90f));
            discMat.SetColor(ID_ColorB, new Color(0f, 1f, 0.35f, 0.90f));
        }
        VFXFactory.Play(VFXType.OrbPickup, transform.position);
        AnnounceHUD("OBJECTIVE COMPLETE!  +EXP  +HEAL  ★ORB", Color.green);
    }

    [ClientRpc]
    void ObjectiveExpiredClientRpc()
    {
        // เปลี่ยนสีเป็นแดง
        if (discMat != null)
        {
            discMat.SetColor(ID_ColorA, new Color(1f, 0.15f, 0.05f, 0.70f));
            discMat.SetColor(ID_ColorB, new Color(1f, 0.15f, 0.05f, 0.70f));
        }
        VFXFactory.Play(VFXType.EnemyDeath, transform.position);
        AnnounceHUD("OBJECTIVE EXPIRED", new Color(1f, 0.40f, 0.05f));
    }

    // ── Client Update: scale pulse ─────────────────────────────────────────
    void Update()
    {
        if (runtimeDisc == null || isComplete.Value) return;
        pulseT += Time.deltaTime;

        float s = playersInZone.Value > 0
            ? 1f + Mathf.Sin(pulseT * 5f) * 0.03f    // เต้นเร็วเมื่อมีผู้เล่น
            : 1f + Mathf.Sin(pulseT * 1.5f) * 0.01f; // idle เบาๆ

        runtimeDisc.transform.localScale = new Vector3(zoneRadius, zoneRadius , 1f);
    }

    // ── Visual: Quad + ZoneObjectiveFill shader ────────────────────────────
    void CreateDisc()
    {
        if (zoneVisualPrefab != null)
        {
            runtimeDisc = Instantiate(zoneVisualPrefab, transform);
            discMat     = runtimeDisc.GetComponentInChildren<Renderer>()?.material;
            return;
        }

        // Quad นอนราบ — Euler(90,0,0) ทำให้หน้า Quad ชี้ขึ้น
        runtimeDisc = GameObject.CreatePrimitive(PrimitiveType.Quad);
        runtimeDisc.transform.SetParent(transform, false);
        runtimeDisc.transform.localPosition = new Vector3(0, 0.02f, 0);
        runtimeDisc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        runtimeDisc.transform.localScale    = new Vector3(zoneRadius , zoneRadius , 1f);
        Object.Destroy(runtimeDisc.GetComponent<Collider>());

        var rend = runtimeDisc.GetComponent<Renderer>();
        var sh   = Shader.Find("Swarm/ZoneObjectiveFill");
        if (sh == null)
        {
            Debug.LogWarning("[ZoneObjective] Shader 'Swarm/ZoneObjectiveFill' ไม่พบ — ตรวจสอบ Assets/Shaders/");
            return;
        }

        discMat        = new Material(sh);
        rend.material  = discMat;
        discMat.SetFloat(ID_Progress, 0f);
    }

    // ── Shader update ─────────────────────────────────────────────────────
    void UpdateShader(float p)
    {
        if (discMat == null) return;
        discMat.SetFloat(ID_Progress, p);
    }

    void OnPlayersInZoneChanged(int _, int count)
    {
        if (count > 0)
            VFXFactory.Play(VFXType.LaserHit, transform.position + Vector3.up * 0.1f);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    static void AnnounceHUD(string text, Color color)
    {
        Object.FindAnyObjectByType<GameHUD>()?.ShowAnnouncement(text, color);
    }

    void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.cyan;
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, zoneRadius);
#endif
    }
}
