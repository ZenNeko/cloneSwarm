using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Zone Objective — 2-Phase quest system
///
/// Phase 1 (Activating)
///   ผู้เล่นยืนใน zone ครบ activationTime → "เปิดใช้งาน" objective
///   → server สุ่ม QuestType จาก availableQuests
///
/// Phase 2 (Active Quest) — สุ่มประเภท
///   • FetchAndDeliver : เก็บ FetchItem N ชิ้น → ส่งใน zone จนครบ requiredDeliveryCount
///   (เพิ่มประเภทใหม่ได้ — ขยาย enum + switch ใน Phase 2 logic)
///
/// Phase 3 (Complete) → reward + despawn
///
/// Server: ตรวจ player + drive progress / deliveredCount / phase transition
/// Client: อ่าน NetworkVariable → shader _Progress + UI counter + announcements
/// </summary>
public class ZoneObjective : NetworkBehaviour
{
    public enum Phase     : int { Activating, Active, Complete }
    public enum QuestType : int { FetchAndDeliver, Survive }

    [Header("Common Settings")]
    public float zoneRadius      = 3f;
    [Tooltip("วินาทีก่อน timeout ของ Phase 1 Activation (0 = ไม่มี)")]
    public float timeoutDuration = 60f;
    [Tooltip("เพดานเวลาของ quest Survive (วินาที) — 0 = ใช้ surviveTime × 2 อัตโนมัติ\n" +
             "จำเป็นเพราะ surviveTime เดินเฉพาะตอนมีคนอยู่ในโซน ถ้าทุกคนออกถาวรจะไม่มีวันจบ")]
    public float surviveQuestTimeLimit = 0f;

    [Header("Phase 1: Activation")]
    [Tooltip("วินาทีที่ต้องยืนใน zone ก่อน quest จะเริ่ม")]
    public float activationTime = 5f;

    [Header("Phase 2: Random Quest Selection")]
    [Tooltip("รายการ quest ที่จะสุ่มหลัง activation — ต้องมีอย่างน้อย 1 ตัว")]
    public List<QuestType> availableQuests = new() { QuestType.FetchAndDeliver, QuestType.Survive };

    [Header("Quest: FetchAndDeliver")]
    [Tooltip("Prefab ของ item ที่ต้องเก็บ (มี FetchItem.cs + NetworkObject)")]
    public GameObject fetchItemPrefab;
    [Tooltip("จำนวนต้องส่งให้ครบ — = จำนวน item ที่ spawn")]
    [Min(1)] public int requiredDeliveryCount = 5;
    [Tooltip("เวลา fail ของ quest (วินาที) — เริ่มนับใหม่หลัง activation จบ\n" +
             "0 = ไม่มี timeout (quest จะอยู่จน complete หรือ player หาย)")]
    public float fetchQuestTimeLimit = 90f;

    [Header("Quest: Survive")]
    [Tooltip("วินาทีที่ต้องอยู่ใน zone (timer pause เมื่อไม่มีใครอยู่)")]
    public float surviveTime          = 20f;
    [Tooltip("จำนวน enemy เพิ่มต่อ spawn tick ระหว่างทำ quest (0 = ไม่ boost)")]
    [Min(0)] public int surviveExtraSpawnsPerTick = 2;

    [Header("Rewards")]
    public float      expReward  = 80f;
    public float      healAmount = 20f;
    [Tooltip("ObjectiveOrb prefab — spawn ที่ตำแหน่ง zone เมื่อ complete")]
    public GameObject orbPrefab;

    [Header("Visual")]
    [Tooltip("Quad prefab — ถ้าปล่อยว่างจะสร้าง runtime Quad")]
    public GameObject zoneVisualPrefab;

    // ── Server-side runtime data (set by ObjectiveManager ก่อน Spawn) ────
    [HideInInspector] public List<Vector3> itemSpawnPositions = new();

    // ── Network State ─────────────────────────────────────────────────────
    public NetworkVariable<float> progress       = new(0f,    NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int>   playersInZone  = new(0,     NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int>   phaseInt       = new(0,     NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int>   activeQuestInt = new(-1,    NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int>   deliveredCount = new(0,     NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int>   requiredCount  = new(0,     NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public Phase     CurrentPhase    => (Phase)phaseInt.Value;
    public QuestType ActiveQuestType => (QuestType)Mathf.Max(0, activeQuestInt.Value);
    public bool      HasActiveQuest  => activeQuestInt.Value >= 0;

    // ── Static Events ─────────────────────────────────────────────────────
    public static event System.Action<ZoneObjective>           OnObjectiveSpawned;
    public static event System.Action<ZoneObjective>           OnObjectiveCompleted;
    public static event System.Action<ZoneObjective>           OnObjectiveExpired;
    /// <summary>Phase ของ objective เปลี่ยน (Activating → Active → Complete)</summary>
    public static event System.Action<ZoneObjective, Phase>    OnPhaseChanged;
    /// <summary>FetchAndDeliver: ยิงเมื่อ deliveredCount เปลี่ยน — UI subscribe เพื่อแสดง "X/N"</summary>
    public static event System.Action<ZoneObjective, int, int> OnDeliveryProgress;

    // ── Server-side spawned items + cached refs ───────────────────────────
    private readonly List<NetworkObject> spawnedItems = new();
    private EnemySpawner cachedSpawner;
    private bool         spawnBoostActive;

    // ── Client Visual ─────────────────────────────────────────────────────
    private GameObject runtimeDisc;
    private Material   discMat;

    static readonly int ID_Progress = Shader.PropertyToID("_Progress");
    static readonly int ID_ColorA   = Shader.PropertyToID("_ColorA");
    static readonly int ID_ColorB   = Shader.PropertyToID("_ColorB");

    // Color palette per phase
    static readonly Color COL_ACTIVATING = new(0.20f, 0.85f, 1.00f, 0.85f);   // cyan
    static readonly Color COL_QUEST      = new(1.00f, 0.85f, 0.20f, 0.85f);   // gold
    static readonly Color COL_COMPLETE   = new(0.10f, 1.00f, 0.35f, 0.90f);   // green
    static readonly Color COL_EXPIRED    = new(1.00f, 0.15f, 0.05f, 0.70f);   // red

    // ── Lifecycle ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (IsServer)
            StartCoroutine(ObjectiveStateMachine());

        CreateDisc();
        progress.OnValueChanged       += (_, v) => UpdateShader(v);
        deliveredCount.OnValueChanged += OnDeliveredCountChanged;
        phaseInt.OnValueChanged       += OnPhaseChangedClient;

        OnObjectiveSpawned?.Invoke(this);
        AnnounceHUD("ZONE OBJECTIVE — Stand to activate!", COL_ACTIVATING);
        SetDiscColor(COL_ACTIVATING);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SERVER STATE MACHINE
    // ═══════════════════════════════════════════════════════════════════════
    IEnumerator ObjectiveStateMachine()
    {
        float timeoutAt = timeoutDuration > 0 ? Time.time + timeoutDuration : float.MaxValue;

        // ── Phase 1: Activation — fill progress while ผู้เล่นยืน ─────────
        phaseInt.Value = (int)Phase.Activating;
        progress.Value = 0f;

        while (progress.Value < 1f)
        {
            if (Time.time >= timeoutAt) { ExpireAndDespawn(); yield break; }

            int count = CountPlayersInZone();
            playersInZone.Value = count;

            if (count > 0)
                progress.Value = Mathf.Min(1f, progress.Value + Time.deltaTime / Mathf.Max(0.01f, activationTime));

            yield return null;
        }

        // ── Pick random quest type ───────────────────────────────────────
        if (availableQuests == null || availableQuests.Count == 0)
        {
            Debug.LogWarning("[ZoneObjective] availableQuests empty — complete ทันที");
            CompleteAndReward();
            yield break;
        }
        QuestType chosen = availableQuests[Random.Range(0, availableQuests.Count)];
        activeQuestInt.Value = (int)chosen;
        progress.Value       = 0f;
        phaseInt.Value       = (int)Phase.Active;

        // ── Phase 2: Run chosen quest ────────────────────────────────────
        // แต่ละ quest มี timeout ของตัวเอง — reset นาฬิกาตอนเริ่ม quest
        switch (chosen)
        {
            case QuestType.FetchAndDeliver:
                float fetchDeadline = fetchQuestTimeLimit > 0
                    ? Time.time + fetchQuestTimeLimit
                    : float.MaxValue;
                yield return StartCoroutine(QuestFetchAndDeliver(fetchDeadline));
                break;
            case QuestType.Survive:
                // surviveTime เดินเฉพาะตอนมีคนอยู่ในโซน → ต้องมี deadline แยกจริงๆ
                float surviveLimit    = surviveQuestTimeLimit > 0f ? surviveQuestTimeLimit : surviveTime * 2f;
                float surviveDeadline = Time.time + surviveLimit;
                yield return StartCoroutine(QuestSurvive(surviveDeadline));
                break;
            default:
                Debug.LogError($"[ZoneObjective] Unimplemented QuestType: {chosen}");
                CompleteAndReward();
                break;
        }

        yield break;
    }

    // ── Quest: FetchAndDeliver ────────────────────────────────────────────
    IEnumerator QuestFetchAndDeliver(float timeoutAt)
    {
        requiredCount.Value  = requiredDeliveryCount;
        deliveredCount.Value = 0;
        SpawnFetchItems();

        while (deliveredCount.Value < requiredCount.Value)
        {
            if (Time.time >= timeoutAt) { ExpireAndDespawn(); yield break; }

            int count = CountPlayersInZone();
            playersInZone.Value = count;

            if (count > 0)
            {
                int drained = DrainPlayersCarriedItems();
                if (drained > 0)
                {
                    deliveredCount.Value = Mathf.Min(requiredCount.Value, deliveredCount.Value + drained);
                    progress.Value       = requiredCount.Value > 0
                        ? (float)deliveredCount.Value / requiredCount.Value
                        : 1f;
                }
            }

            yield return null;
        }

        progress.Value = 1f;
        CompleteAndReward();
    }

    // ── Quest: Survive ────────────────────────────────────────────────────
    /// <summary>
    /// Survive: ผู้เล่นต้องอยู่ใน zone จนครบ surviveTime
    ///   - timer pause เมื่อไม่มีใครอยู่ (ไม่ reset — fair กับ multi-player rotation)
    ///   - เร่ง spawn enemy ผ่าน EnemySpawner.BoostSpawn ระหว่าง quest
    ///   - clear boost ทั้งกรณี complete และ expire/cleanup
    /// </summary>
    IEnumerator QuestSurvive(float timeoutAt)
    {
        // ตั้ง requiredCount = surviveTime (เป็นวินาที — ใช้แสดง UI ได้)
        requiredCount.Value  = Mathf.CeilToInt(surviveTime);
        deliveredCount.Value = 0;

        // เริ่ม spawn boost
        ApplySpawnBoost();

        float survived = 0f;
        while (survived < surviveTime)
        {
            if (Time.time >= timeoutAt) { ClearSpawnBoostIfActive(); ExpireAndDespawn(); yield break; }

            int count = CountPlayersInZone();
            playersInZone.Value = count;

            if (count > 0)
            {
                survived          += Time.deltaTime;
                progress.Value     = Mathf.Min(1f, survived / surviveTime);
                deliveredCount.Value = Mathf.Min(requiredCount.Value, Mathf.FloorToInt(survived));
            }
            // else: timer pause (ไม่ลด survived)

            yield return null;
        }

        ClearSpawnBoostIfActive();
        progress.Value       = 1f;
        deliveredCount.Value = requiredCount.Value;
        CompleteAndReward();
    }

    void ApplySpawnBoost()
    {
        if (surviveExtraSpawnsPerTick <= 0) return;
        if (cachedSpawner == null) cachedSpawner = Object.FindAnyObjectByType<EnemySpawner>();
        if (cachedSpawner == null) { Debug.LogWarning("[ZoneObjective] EnemySpawner not found — skip boost"); return; }

        cachedSpawner.BoostSpawn(surviveExtraSpawnsPerTick);
        spawnBoostActive = true;
    }

    void ClearSpawnBoostIfActive()
    {
        if (!spawnBoostActive) return;
        cachedSpawner?.ClearSpawnBoost();
        spawnBoostActive = false;
        Debug.Log($"[ZoneObjective] Spawn rate restored (removed boost of +{surviveExtraSpawnsPerTick} spawns/tick)");
    }

    // ── Server: Spawn FetchItems ──────────────────────────────────────────
    void SpawnFetchItems()
    {
        if (fetchItemPrefab == null)
        {
            Debug.LogWarning("[ZoneObjective] fetchItemPrefab not assigned — skip spawn");
            return;
        }
        if (itemSpawnPositions == null || itemSpawnPositions.Count == 0)
        {
            Debug.LogWarning("[ZoneObjective] itemSpawnPositions empty — ObjectiveManager ไม่ได้ pick locations");
            return;
        }

        int count = Mathf.Min(requiredDeliveryCount, itemSpawnPositions.Count);
        if (count < requiredDeliveryCount)
        {
            Debug.LogWarning($"[ZoneObjective] locations ไม่พอ ({itemSpawnPositions.Count} < {requiredDeliveryCount}) — adjust requiredCount");
            requiredCount.Value = count;
        }

        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(fetchItemPrefab, itemSpawnPositions[i], Quaternion.identity);
            var no = go.GetComponent<NetworkObject>();
            if (no == null) { Destroy(go); continue; }
            no.Spawn(true);
            spawnedItems.Add(no);
        }
        Debug.Log($"[ZoneObjective] 📦 Spawned {count} fetch items");
    }

    // ── Server: shared helpers ────────────────────────────────────────────
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

    int DrainPlayersCarriedItems()
    {
        if (NetworkManager.Singleton == null) return 0;
        int total = 0;
        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
        {
            var obj = c.PlayerObject;
            if (obj == null) continue;
            float dx = obj.transform.position.x - transform.position.x;
            float dz = obj.transform.position.z - transform.position.z;
            if (dx * dx + dz * dz > zoneRadius * zoneRadius) continue;

            var pm = obj.GetComponent<playermove>();
            if (pm != null) total += pm.DrainCarriedQuestItems();
        }
        return total;
    }

    void CleanupSpawnedItems()
    {
        foreach (var no in spawnedItems)
            if (no != null && no.IsSpawned) no.Despawn(true);
        spawnedItems.Clear();
    }

    void ResetAllPlayersCarriedItems()
    {
        if (NetworkManager.Singleton == null) return;
        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
        {
            var obj = c.PlayerObject;
            if (obj == null) continue;
            var pm = obj.GetComponent<playermove>();
            if (pm != null) pm.DrainCarriedQuestItems();
        }
    }

    void ExpireAndDespawn()
    {
        CleanupSpawnedItems();
        ClearSpawnBoostIfActive();
        ResetAllPlayersCarriedItems();
        ObjectiveExpiredClientRpc();
        OnObjectiveExpired?.Invoke(this);
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
    }

    void CompleteAndReward()
    {
        phaseInt.Value = (int)Phase.Complete;
        progress.Value = 1f;
        ResetAllPlayersCarriedItems();
        GiveRewards();
        ObjectiveCompleteClientRpc();
        OnObjectiveCompleted?.Invoke(this);
        Debug.Log("[ZoneObjective] ✅ Completed!");

        StartCoroutine(DespawnAfter(2f));
    }

    IEnumerator DespawnAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (NetworkObject != null && NetworkObject.IsSpawned) NetworkObject.Despawn(true);
    }

    void GiveRewards()
    {
        SharedExperienceManager.Instance?.AddExp(expReward);

        if (healAmount > 0 && NetworkManager.Singleton != null)
            foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
                c.PlayerObject?.GetComponent<playermove>()?.Heal(healAmount);

        if (orbPrefab != null)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 0.6f;
            var orbGo = Instantiate(orbPrefab, spawnPos, Quaternion.identity);
            var no    = orbGo.GetComponent<NetworkObject>();
            if (no != null) no.Spawn(true);
            else Debug.LogWarning("[ZoneObjective] orbPrefab ไม่มี NetworkObject");
        }

        Debug.Log($"[ZoneObjective] Reward — EXP+{expReward} Heal+{healAmount}" +
                  (orbPrefab != null ? " + Orb" : ""));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CLIENT VISUAL + ANNOUNCEMENTS
    // ═══════════════════════════════════════════════════════════════════════
    [ClientRpc]
    void ObjectiveCompleteClientRpc()
    {
        SetDiscColor(COL_COMPLETE);
        if (discMat != null) discMat.SetFloat(ID_Progress, 1f);
        VFXFactory.Play("None", transform.position);
        AnnounceHUD("OBJECTIVE COMPLETE!  +EXP  +HEAL  ★ORB", Color.green);
    }

    [ClientRpc]
    void ObjectiveExpiredClientRpc()
    {
        SetDiscColor(COL_EXPIRED);
        VFXFactory.Play("EnemyDeath", transform.position);
        AnnounceHUD("OBJECTIVE EXPIRED", new Color(1f, 0.40f, 0.05f));
    }

    void OnPhaseChangedClient(int oldPhase, int newPhase)
    {
        var p = (Phase)newPhase;
        OnPhaseChanged?.Invoke(this, p);

        switch (p)
        {
            case Phase.Activating:
                SetDiscColor(COL_ACTIVATING);
                break;
            case Phase.Active:
                SetDiscColor(COL_QUEST);
                AnnounceHUD(GetQuestAnnouncement(), COL_QUEST);
                break;
            case Phase.Complete:
                SetDiscColor(COL_COMPLETE);
                break;
        }
    }

    string GetQuestAnnouncement()
    {
        return ActiveQuestType switch
        {
            QuestType.FetchAndDeliver => $"QUEST: Deliver {requiredCount.Value} items!",
            QuestType.Survive          => $"QUEST: Survive {surviveTime:F0}s in the zone!",
            _                          => "QUEST STARTED",
        };
    }

    void OnDeliveredCountChanged(int _, int newCount)
    {
        OnDeliveryProgress?.Invoke(this, newCount, requiredCount.Value);
    }

    // ── Visual: Quad + ZoneObjectiveFill shader ──────────────────────────
    void CreateDisc()
    {
        if (zoneVisualPrefab != null)
        {
            runtimeDisc = Instantiate(zoneVisualPrefab, transform);
            ApplyDiscScale();   // ปรับขนาดให้ตรงกับ zoneRadius
            discMat     = runtimeDisc.GetComponentInChildren<Renderer>()?.material;
            return;
        }

        runtimeDisc = GameObject.CreatePrimitive(PrimitiveType.Quad);
        runtimeDisc.transform.SetParent(transform, false);
        runtimeDisc.transform.localPosition = new Vector3(0, 0.02f, 0);
        runtimeDisc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ApplyDiscScale();
        Object.Destroy(runtimeDisc.GetComponent<Collider>());

        var rend = runtimeDisc.GetComponent<Renderer>();
        var sh   = Shader.Find("Swarm/ZoneObjectiveFill");
        if (sh == null)
        {
            Debug.LogWarning("[ZoneObjective] Shader 'Swarm/ZoneObjectiveFill' ไม่พบ");
            return;
        }

        discMat        = new Material(sh);
        rend.material  = discMat;
        discMat.SetFloat(ID_Progress, 0f);
    }

    /// <summary>
    /// ตั้ง localScale ของ disc ให้ตรงกับ zoneRadius (= diameter)
    /// — Quad/Plane 1 unit × scale = world units → scale = diameter = zoneRadius * 2
    /// </summary>
    void ApplyDiscScale()
    {
        if (runtimeDisc == null) return;
        float diameter = zoneRadius ;
        runtimeDisc.transform.localScale = new Vector3(diameter, diameter, 1f);
    }

    void Update()
    {
        // sync visual scale ทุก frame เผื่อ designer ปรับ zoneRadius ใน Inspector ตอน play
        if (runtimeDisc != null) ApplyDiscScale();
    }

    void UpdateShader(float p)
    {
        if (discMat == null) return;
        discMat.SetFloat(ID_Progress, p);
    }

    void SetDiscColor(Color c)
    {
        if (discMat == null) return;
        discMat.SetColor(ID_ColorA, c);
        discMat.SetColor(ID_ColorB, c);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    static void AnnounceHUD(string text, Color color)
    {
        GameHUD.Instance?.ShowAnnouncement(text, color);
    }

    void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.cyan;
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, zoneRadius);
#endif
    }
}
