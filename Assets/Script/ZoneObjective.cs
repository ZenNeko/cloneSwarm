using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Zone Objective — ยืนบนพื้นที่ที่กำหนดเพื่อรับรางวัล
///
/// Server: ตรวจสอบ distance ผู้เล่น → เติม progress → complete → reward → despawn
/// Client: อ่าน NetworkVariable แสดง visual + progress bar
/// </summary>
public class ZoneObjective : NetworkBehaviour
{
    [Header("Settings")]
    public float zoneRadius        = 3f;
    [Tooltip("วินาทีที่ต้องยืนอยู่รวม (progress หยุดเมื่อออก แต่ไม่รีเซ็ต)")]
    public float requiredTime      = 8f;
    [Tooltip("วินาทีก่อน timeout (0 = ไม่มี)")]
    public float timeoutDuration   = 60f;

    [Header("Rewards")]
    public float expReward         = 80f;
    public float healAmount        = 20f;

    [Header("Visual")]
    [Tooltip("วงกลมบนพื้น — ถ้าปล่อยว่างจะสร้าง runtime")]
    public GameObject zoneVisualPrefab;

    // ── Network State ─────────────────────────────────────────────────────
    public NetworkVariable<float> progress    = new(0f,    NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int>   playersInZone = new(0,   NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool>  isComplete  = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ── Static Events ─────────────────────────────────────────────────────
    public static event System.Action<ZoneObjective> OnObjectiveSpawned;
    public static event System.Action<ZoneObjective> OnObjectiveCompleted;
    public static event System.Action<ZoneObjective> OnObjectiveExpired;

    // ── Client Visual ─────────────────────────────────────────────────────
    private GameObject runtimeVisual;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (IsServer)
            StartCoroutine(ObjectiveLoop());

        CreateVisual();
        progress.OnValueChanged += (_, _) => UpdateVisualProgress();
        OnObjectiveSpawned?.Invoke(this);
    }

    // ── Server Logic ───────────────────────────────────────────────────────
    IEnumerator ObjectiveLoop()
    {
        float timeoutAt = timeoutDuration > 0 ? Time.time + timeoutDuration : float.MaxValue;

        while (progress.Value < 1f)
        {
            // นับผู้เล่นในโซน
            int count = CountPlayersInZone();
            playersInZone.Value = count;

            if (count > 0)
                progress.Value = Mathf.Min(1f, progress.Value + Time.deltaTime / requiredTime);

            // Timeout
            if (Time.time >= timeoutAt)
            {
                ObjectiveExpiredClientRpc();
                OnObjectiveExpired?.Invoke(this);
                if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
                yield break;
            }

            yield return null;
        }

        // Complete!
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
            Vector2 d = new Vector2(
                obj.transform.position.x - transform.position.x,
                obj.transform.position.z - transform.position.z);
            if (d.magnitude <= zoneRadius) count++;
        }
        return count;
    }

    void GiveRewards()
    {
        // EXP โบนัส (shared)
        SharedExperienceManager.Instance?.AddExp(expReward);

        // ฟื้น HP ผู้เล่นทุกคน
        if (healAmount > 0 && NetworkManager.Singleton != null)
            foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
                c.PlayerObject?.GetComponent<playermove>()?.Heal(healAmount);

        Debug.Log($"[ZoneObjective] Reward — EXP+{expReward} Heal+{healAmount}");
    }

    // ── ClientRpc ─────────────────────────────────────────────────────────
    [ClientRpc]
    void ObjectiveCompleteClientRpc()
    {
        if (runtimeVisual) runtimeVisual.GetComponent<Renderer>()
            ?.material.SetColor("_BaseColor", Color.green);
    }

    [ClientRpc]
    void ObjectiveExpiredClientRpc()
    {
        Debug.Log("[ZoneObjective] ⏰ Expired");
    }

    // ── Visual ────────────────────────────────────────────────────────────
    void CreateVisual()
    {
        if (zoneVisualPrefab != null)
        {
            runtimeVisual = Instantiate(zoneVisualPrefab, transform);
            return;
        }

        // Fallback: cylinder สีฟ้าโปร่งใส
        runtimeVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        runtimeVisual.transform.SetParent(transform);
        runtimeVisual.transform.localPosition = Vector3.zero;
        runtimeVisual.transform.localScale    = new Vector3(zoneRadius * 2f, 0.02f, zoneRadius * 2f);
        Destroy(runtimeVisual.GetComponent<Collider>());

        var rend = runtimeVisual.GetComponent<Renderer>();
        if (rend != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Standard");
            var mat = new Material(shader);
            mat.SetFloat("_Surface", 1f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetFloat("_Mode", 3f);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
            mat.color = new Color(0f, 0.6f, 1f, 0.35f);
            rend.material = mat;
        }
    }

    void UpdateVisualProgress()
    {
        if (runtimeVisual == null) return;
        var rend = runtimeVisual.GetComponent<Renderer>();
        if (rend == null) return;
        // เปลี่ยนสีตาม progress: ฟ้า → เขียว
        rend.material.color = Color.Lerp(
            new Color(0f, 0.6f, 1f, 0.35f),
            new Color(0f, 1f, 0.3f, 0.55f),
            progress.Value);
    }

    void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.cyan;
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, zoneRadius);
#endif
    }
}
