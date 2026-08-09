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
///   • Survive         : ยืนในโซนจนครบเวลา (timer pause เมื่อไม่มีใคร)
///   • DestroyObjects  : ทำลายวัตถุ N ชิ้นที่กระจายรอบโซน
///   • KillInZone      : ฆ่า enemy N ตัวในรัศมีโซน
///   • SealTheRift     : ยืนในโซนจนครบเวลา แต่ progress ถอยหลังเมื่อไม่มีใคร
///   (เพิ่มประเภทใหม่ได้ — ต่อท้าย enum + เพิ่ม case ใน switch ของ Phase 2
///    ห้ามแทรกกลาง enum เพราะ availableQuests/activeQuestInt serialize เป็น int)
///
/// Phase 3 (Complete) → reward + despawn
///
/// Server: ตรวจ player + drive progress / deliveredCount / phase transition
/// Client: อ่าน NetworkVariable → shader _Progress + UI counter + announcements
/// </summary>
public class ZoneObjective : NetworkBehaviour
{
    public enum Phase     : int { Activating, Active, Complete }
    public enum QuestType : int { FetchAndDeliver, Survive, DestroyObjects, KillInZone, SealTheRift }

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
    [Tooltip("จำนวนต้องส่งให้ครบเมื่อเล่นคนเดียว — ผู้เล่นเพิ่มจะ scale ตาม fetchCountPerExtraPlayer")]
    [Min(1)] public int requiredDeliveryCount = 5;
    [Tooltip("จำนวน item ที่เพิ่มต่อผู้เล่น 1 คนที่เกินจากคนแรก")]
    [Min(0)] public int fetchCountPerExtraPlayer = 2;
    [Tooltip("เพดานจำนวน item ต่อ quest")]
    [Min(1)] public int fetchCountMax = 12;
    [Tooltip("เวลา fail ของ quest (วินาที) — เริ่มนับใหม่หลัง activation จบ\n" +
             "0 = ไม่มี timeout (quest จะอยู่จน complete หรือ player หาย)")]
    public float fetchQuestTimeLimit = 90f;
    [Tooltip("รัศมีวงในที่ item จะไป spawn (ห่างจากโซนอย่างน้อยเท่านี้)")]
    public float itemMinRadius  = 10f;
    [Tooltip("รัศมีวงนอกที่ item จะไป spawn")]
    public float itemMaxRadius  = 26f;
    [Tooltip("ระยะห่างขั้นต่ำระหว่าง item แต่ละชิ้น")]
    public float itemMinSpacing = 5f;

    [Header("Quest: Survive")]
    [Tooltip("วินาทีที่ต้องอยู่ใน zone (timer pause เมื่อไม่มีใครอยู่)")]
    public float surviveTime          = 20f;
    [Tooltip("จำนวน enemy เพิ่มต่อ spawn tick ระหว่างทำ quest (0 = ไม่ boost)")]
    [Min(0)] public int surviveExtraSpawnsPerTick = 2;

    [Header("Quest: DestroyObjects")]
    [Tooltip("Prefab วัตถุทำลายได้ (มี Enemy + DestructibleObjective + NetworkObject, layer = Enemy)")]
    public GameObject destructiblePrefab;
    [Tooltip("จำนวนวัตถุเมื่อเล่นคนเดียว")]
    [Min(1)] public int destroyCountBase           = 5;
    [Tooltip("จำนวนวัตถุที่เพิ่มต่อผู้เล่น 1 คนที่เกินจากคนแรก")]
    [Min(0)] public int destroyCountPerExtraPlayer = 2;
    [Tooltip("เพดานจำนวนวัตถุ (ตามที่ออกแบบไว้ 5-10)")]
    [Min(1)] public int destroyCountMax            = 10;
    [Tooltip("HP ต่อวัตถุ 1 ชิ้น")]
    public float destructibleHealth   = 60f;
    public float destructibleMinRadius  = 6f;
    public float destructibleMaxRadius  = 22f;
    public float destructibleMinSpacing = 4f;
    [Tooltip("เวลา fail ของ quest (วินาที) — 0 = ไม่มี timeout")]
    public float destroyQuestTimeLimit = 90f;

    [Header("Quest: KillInZone (Sacrifice)")]
    [Tooltip("จำนวน enemy ที่ต้องฆ่าในรัศมีโซน เมื่อเล่นคนเดียว")]
    [Min(1)] public int killCountBase           = 20;
    [Tooltip("จำนวนที่เพิ่มต่อผู้เล่น 1 คนที่เกินจากคนแรก")]
    [Min(0)] public int killCountPerExtraPlayer = 10;
    [Min(1)] public int killCountMax            = 80;
    [Tooltip("จำนวน enemy เพิ่มต่อ spawn tick ระหว่างทำ quest")]
    [Min(0)] public int killExtraSpawnsPerTick  = 2;
    [Tooltip("เวลา fail ของ quest (วินาที) — 0 = ไม่มี timeout")]
    public float killQuestTimeLimit = 75f;

    [Header("Quest: SealTheRift")]
    [Tooltip("วินาทีที่ต้องอยู่ในโซน — ต่างจาก Survive ตรง progress ถอยหลังเมื่อไม่มีใคร")]
    public float sealTime = 15f;
    [Tooltip("อัตราถอยหลังของ progress เมื่อโซนว่าง (× ของอัตราเดินหน้า)\n" +
             "1 = ถอยเร็วเท่าที่เดินหน้า, 0.5 = ถอยครึ่งเดียว")]
    [Min(0f)] public float sealDecayMultiplier = 0.5f;
    [Tooltip("จำนวน enemy เพิ่มต่อ spawn tick ระหว่างทำ quest")]
    [Min(0)] public int sealExtraSpawnsPerTick = 3;
    [Tooltip("เวลา fail ของ quest (วินาที) — 0 = ไม่มี timeout")]
    public float sealQuestTimeLimit = 60f;

    [Header("Rewards")]
    public float      expReward  = 80f;
    public float      healAmount = 20f;
    [Tooltip("ObjectiveOrb prefab — spawn ที่ตำแหน่ง zone เมื่อ complete")]
    public GameObject orbPrefab;

    [Header("Visual")]
    [Tooltip("Quad prefab — ถ้าปล่อยว่างจะสร้าง runtime Quad")]
    public GameObject zoneVisualPrefab;

    // ── Server-side runtime data ──────────────────────────────────────────
    /// <summary>Override ตำแหน่ง FetchItem — ถ้าว่าง (ปกติ) จะสุ่มรอบโซนเอง
    /// ObjectiveManager เติมให้เฉพาะตอนเปิด useFixedItemLocations</summary>
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

    /// <summary>วัตถุของ quest DestroyObjects ที่ยังไม่ถูกทำลาย (server only)</summary>
    private readonly HashSet<NetworkObject> trackedDestructibles = new();
    private int  destroyedCount;
    private int  killsInZone;
    private bool countingKillsInZone;

    /// <summary>กัน OnObjectiveCompleted/Expired ยิงซ้ำ — ดู NotifyRemoved()</summary>
    private bool removalNotified;

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
        {
            Enemy.OnEnemyDiedServer += HandleEnemyDiedServer;
            StartCoroutine(ObjectiveStateMachine());
        }

        CreateDisc();
        // named method ไม่ใช่ lambda — ต้อง unsubscribe ได้ใน OnNetworkDespawn
        progress.OnValueChanged       += OnProgressChanged;
        deliveredCount.OnValueChanged += OnDeliveredCountChanged;
        phaseInt.OnValueChanged       += OnPhaseChangedClient;

        OnObjectiveSpawned?.Invoke(this);
        AnnounceHUD("ZONE OBJECTIVE — Stand to activate!", COL_ACTIVATING);
        SetDiscColor(COL_ACTIVATING);
    }

    public override void OnNetworkDespawn()
    {
        progress.OnValueChanged       -= OnProgressChanged;
        deliveredCount.OnValueChanged -= OnDeliveredCountChanged;
        phaseInt.OnValueChanged       -= OnPhaseChangedClient;

        if (IsServer)
        {
            Enemy.OnEnemyDiedServer -= HandleEnemyDiedServer;
            ClearSpawnBoostIfActive();   // กัน boost ค้างถ้าโดน despawn กลางคัน
        }

        // ทางออกสุดท้าย — ถ้าหายไปโดยไม่ผ่าน complete/expire (host ปิด, scene reload)
        // UI ยังต้องได้ยินว่าให้ลบ indicator ทิ้ง
        NotifyRemoved(completed: false);
    }

    /// <summary>ยิง OnObjectiveCompleted/Expired ครั้งเดียวไม่ว่าจะเข้ามาทางไหน</summary>
    void NotifyRemoved(bool completed)
    {
        if (removalNotified) return;
        removalNotified = true;

        if (completed) OnObjectiveCompleted?.Invoke(this);
        else           OnObjectiveExpired?.Invoke(this);
    }

    // ── Server: นับศพ (ใช้ทั้ง DestroyObjects และ KillInZone) ─────────────
    void HandleEnemyDiedServer(Enemy enemy, Vector3 deathPos)
    {
        if (!IsServer || enemy == null) return;

        // วัตถุของ quest นี้เอง → นับเป็น destroy (และไม่ให้ไปนับซ้ำเป็น kill)
        var no = enemy.NetworkObject;
        if (no != null && trackedDestructibles.Remove(no))
        {
            destroyedCount++;
            return;
        }

        if (!countingKillsInZone) return;

        float dx = deathPos.x - transform.position.x;
        float dz = deathPos.z - transform.position.z;
        if (dx * dx + dz * dz <= zoneRadius * zoneRadius) killsInZone++;
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
            case QuestType.DestroyObjects:
                yield return StartCoroutine(QuestDestroyObjects(Deadline(destroyQuestTimeLimit)));
                break;
            case QuestType.KillInZone:
                yield return StartCoroutine(QuestKillInZone(Deadline(killQuestTimeLimit)));
                break;
            case QuestType.SealTheRift:
                // เหมือน Survive — progress เดินเฉพาะตอนมีคน จึงต้องมี hard deadline
                yield return StartCoroutine(QuestSealTheRift(
                    Deadline(sealQuestTimeLimit > 0f ? sealQuestTimeLimit : sealTime * 3f)));
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
        int target = ScaledCount(requiredDeliveryCount, fetchCountPerExtraPlayer, fetchCountMax);
        deliveredCount.Value = 0;

        // requiredCount ต้องเป็นจำนวนที่ spawn ได้จริง ไม่ใช่ที่ขอ
        // ไม่งั้นถ้าวางไม่ครบ quest จะจบไม่ลงเพราะรอ item ที่ไม่มีอยู่
        requiredCount.Value = SpawnFetchItems(target);

        if (requiredCount.Value == 0)
        {
            Debug.LogWarning("[ZoneObjective] วาง fetch item ไม่ได้เลย — complete ทันทีกัน quest ค้าง");
            progress.Value = 1f;
            CompleteAndReward();
            yield break;
        }

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
        ApplySpawnBoost(surviveExtraSpawnsPerTick);

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

    // ── Quest: DestroyObjects ─────────────────────────────────────────────
    /// <summary>
    /// ทำลายวัตถุ N ชิ้นที่กระจายรอบโซน (Crush the Statues / Vase Destruction)
    ///   - วัตถุคือ Enemy ที่ปิด movement → อาวุธทุกตัวยิงโดนโดยไม่ต้องแก้อะไร
    ///   - ไม่ต้องยืนในโซน — โซนเป็นแค่จุดอ้างอิงบนแผนที่
    ///   - จำนวนที่ต้องทำลายอ่านจาก trackedDestructibles ที่วางได้จริง ไม่ใช่ที่ขอ
    /// </summary>
    IEnumerator QuestDestroyObjects(float timeoutAt)
    {
        int target = ScaledCount(destroyCountBase, destroyCountPerExtraPlayer, destroyCountMax);
        destroyedCount = 0;
        SpawnDestructibles(target);

        requiredCount.Value  = trackedDestructibles.Count;
        deliveredCount.Value = 0;

        if (requiredCount.Value == 0)
        {
            Debug.LogWarning("[ZoneObjective] วางวัตถุไม่ได้เลย — complete ทันทีกัน quest ค้าง");
            CompleteAndReward();
            yield break;
        }

        while (destroyedCount < requiredCount.Value)
        {
            if (Time.time >= timeoutAt) { ExpireAndDespawn(); yield break; }

            playersInZone.Value = CountPlayersInZone();

            if (deliveredCount.Value != destroyedCount)
            {
                deliveredCount.Value = destroyedCount;
                progress.Value       = (float)destroyedCount / requiredCount.Value;
            }

            yield return null;
        }

        progress.Value = 1f;
        CompleteAndReward();
    }

    // ── Quest: KillInZone (Sacrifice) ─────────────────────────────────────
    /// <summary>
    /// ฆ่า enemy N ตัวที่ตายในรัศมีโซน — บังคับให้ดึงศัตรูเข้ามาตายในวง
    /// นับผ่าน Enemy.OnEnemyDiedServer (ดู HandleEnemyDiedServer)
    /// </summary>
    IEnumerator QuestKillInZone(float timeoutAt)
    {
        requiredCount.Value  = ScaledCount(killCountBase, killCountPerExtraPlayer, killCountMax);
        deliveredCount.Value = 0;
        killsInZone          = 0;
        countingKillsInZone  = true;

        ApplySpawnBoost(killExtraSpawnsPerTick);

        while (killsInZone < requiredCount.Value)
        {
            if (Time.time >= timeoutAt)
            {
                countingKillsInZone = false;
                ClearSpawnBoostIfActive();
                ExpireAndDespawn();
                yield break;
            }

            playersInZone.Value = CountPlayersInZone();

            if (deliveredCount.Value != killsInZone)
            {
                deliveredCount.Value = Mathf.Min(requiredCount.Value, killsInZone);
                progress.Value       = (float)deliveredCount.Value / requiredCount.Value;
            }

            yield return null;
        }

        countingKillsInZone = false;
        ClearSpawnBoostIfActive();
        progress.Value       = 1f;
        deliveredCount.Value = requiredCount.Value;
        CompleteAndReward();
    }

    // ── Quest: SealTheRift ────────────────────────────────────────────────
    /// <summary>
    /// เหมือน Survive แต่ progress "ถอยหลัง" เมื่อโซนว่าง แทนที่จะแค่หยุด
    /// (Seal the Rift ของ The Spell Brigade) — ลงโทษการวิ่งหนีออกจากวง
    /// </summary>
    IEnumerator QuestSealTheRift(float timeoutAt)
    {
        requiredCount.Value  = Mathf.CeilToInt(sealTime);
        deliveredCount.Value = 0;

        ApplySpawnBoost(sealExtraSpawnsPerTick);

        float sealProgress = 0f;
        while (sealProgress < sealTime)
        {
            if (Time.time >= timeoutAt) { ClearSpawnBoostIfActive(); ExpireAndDespawn(); yield break; }

            int count = CountPlayersInZone();
            playersInZone.Value = count;

            if (count > 0) sealProgress += Time.deltaTime;
            else           sealProgress  = Mathf.Max(0f, sealProgress - Time.deltaTime * sealDecayMultiplier);

            progress.Value       = Mathf.Clamp01(sealProgress / sealTime);
            deliveredCount.Value = Mathf.Min(requiredCount.Value, Mathf.FloorToInt(sealProgress));

            yield return null;
        }

        ClearSpawnBoostIfActive();
        progress.Value       = 1f;
        deliveredCount.Value = requiredCount.Value;
        CompleteAndReward();
    }

    // ── Spawn boost ───────────────────────────────────────────────────────
    void ApplySpawnBoost(int extraPerTick)
    {
        if (extraPerTick <= 0) return;
        if (cachedSpawner == null) cachedSpawner = Object.FindAnyObjectByType<EnemySpawner>();
        if (cachedSpawner == null) { Debug.LogWarning("[ZoneObjective] EnemySpawner not found — skip boost"); return; }

        cachedSpawner.BoostSpawn(extraPerTick);
        spawnBoostActive = true;
    }

    void ClearSpawnBoostIfActive()
    {
        if (!spawnBoostActive) return;
        cachedSpawner?.ClearSpawnBoost();
        spawnBoostActive = false;
        Debug.Log("[ZoneObjective] Spawn rate restored");
    }

    // ── Helpers: scaling + deadline ───────────────────────────────────────
    /// <summary>ปรับจำนวนตามจำนวนผู้เล่น (ทั้ง LoL Swarm และ Spell Brigade scale แบบนี้)</summary>
    int ScaledCount(int baseCount, int perExtraPlayer, int max)
    {
        int players = 1;
        if (NetworkManager.Singleton != null)
            players = Mathf.Max(1, NetworkManager.Singleton.ConnectedClientsList.Count);

        int n = baseCount + perExtraPlayer * (players - 1);
        return Mathf.Clamp(n, 1, Mathf.Max(1, max));
    }

    static float Deadline(float limitSeconds)
        => limitSeconds > 0f ? Time.time + limitSeconds : float.MaxValue;

    // ── Server: Spawn destructibles ───────────────────────────────────────
    void SpawnDestructibles(int count)
    {
        trackedDestructibles.Clear();

        if (destructiblePrefab == null)
        {
            Debug.LogWarning("[ZoneObjective] destructiblePrefab not assigned — skip spawn");
            return;
        }

        var positions = ObjectivePlacement.SampleRing(
            transform.position, count,
            destructibleMinRadius, destructibleMaxRadius, destructibleMinSpacing);

        foreach (var pos in positions)
        {
            var go = Instantiate(destructiblePrefab, pos, Quaternion.Euler(0f, Random.value * 360f, 0f));

            // ตั้ง HP ก่อน Spawn — DestructibleObjective อ่านค่านี้ใน OnNetworkSpawn
            var d = go.GetComponent<DestructibleObjective>();
            if (d != null) d.health = destructibleHealth;

            var no = go.GetComponent<NetworkObject>();
            if (no == null)
            {
                Debug.LogError("[ZoneObjective] destructiblePrefab ไม่มี NetworkObject");
                Destroy(go);
                continue;
            }

            no.Spawn(true);
            trackedDestructibles.Add(no);
            spawnedItems.Add(no);          // ให้ CleanupSpawnedItems เก็บกวาดตอน expire
        }

        Debug.Log($"[ZoneObjective] 🗿 Spawned {trackedDestructibles.Count}/{count} destructibles");
    }

    // ── Server: Spawn FetchItems ──────────────────────────────────────────
    /// <summary>
    /// วาง fetch item — ปกติสุ่มตำแหน่งสดรอบโซนทุกครั้ง (ObjectivePlacement)
    /// ถ้า itemSpawnPositions มีของ (ObjectiveManager.useFixedItemLocations เปิด)
    /// จะใช้จุดที่กำหนดไว้แทน
    /// </summary>
    /// <returns>จำนวนที่ spawn สำเร็จจริง</returns>
    int SpawnFetchItems(int target)
    {
        if (fetchItemPrefab == null)
        {
            Debug.LogWarning("[ZoneObjective] fetchItemPrefab not assigned — skip spawn");
            return 0;
        }

        List<Vector3> positions;
        if (itemSpawnPositions != null && itemSpawnPositions.Count > 0)
        {
            positions = new List<Vector3>(itemSpawnPositions);
            if (positions.Count > target) positions.RemoveRange(target, positions.Count - target);
        }
        else
        {
            positions = ObjectivePlacement.SampleRing(
                transform.position, target, itemMinRadius, itemMaxRadius, itemMinSpacing);
        }

        int spawned = 0;
        foreach (var pos in positions)
        {
            var go = Instantiate(fetchItemPrefab, pos, Quaternion.identity);
            var no = go.GetComponent<NetworkObject>();
            if (no == null)
            {
                Debug.LogError("[ZoneObjective] fetchItemPrefab ไม่มี NetworkObject");
                Destroy(go);
                continue;
            }
            no.Spawn(true);
            spawnedItems.Add(no);
            spawned++;
        }

        Debug.Log($"[ZoneObjective] 📦 Spawned {spawned}/{target} fetch items");
        return spawned;
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
        countingKillsInZone = false;
        trackedDestructibles.Clear();
        CleanupSpawnedItems();
        ClearSpawnBoostIfActive();
        ResetAllPlayersCarriedItems();
        ObjectiveExpiredClientRpc();
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
    }

    void CompleteAndReward()
    {
        countingKillsInZone = false;
        trackedDestructibles.Clear();
        CleanupSpawnedItems();      // เก็บของที่เหลือ เช่น fetch item ที่ไม่มีใครไปเอา
        ClearSpawnBoostIfActive();

        phaseInt.Value = (int)Phase.Complete;
        progress.Value = 1f;
        ResetAllPlayersCarriedItems();
        GiveRewards();
        ObjectiveCompleteClientRpc();
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
    // หมายเหตุ: NotifyRemoved ถูกเรียกจากใน ClientRpc (ไม่ใช่ฝั่ง server ตรงๆ)
    // เพื่อให้ทุก client ได้ยิน event ไม่ใช่แค่ host — UI จะได้ลบ indicator ทัน
    [ClientRpc]
    void ObjectiveCompleteClientRpc()
    {
        SetDiscColor(COL_COMPLETE);
        if (discMat != null) discMat.SetFloat(ID_Progress, 1f);
        VFXFactory.Play("None", transform.position);
        AnnounceHUD("OBJECTIVE COMPLETE!  +EXP  +HEAL  ★ORB", Color.green);
        NotifyRemoved(completed: true);
    }

    [ClientRpc]
    void ObjectiveExpiredClientRpc()
    {
        SetDiscColor(COL_EXPIRED);
        VFXFactory.Play("EnemyDeath", transform.position);
        AnnounceHUD("OBJECTIVE EXPIRED", new Color(1f, 0.40f, 0.05f));
        NotifyRemoved(completed: false);
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
            QuestType.Survive         => $"QUEST: Survive {surviveTime:F0}s in the zone!",
            QuestType.DestroyObjects  => $"QUEST: Destroy {requiredCount.Value} objects!",
            QuestType.KillInZone      => $"QUEST: Kill {requiredCount.Value} enemies inside the zone!",
            QuestType.SealTheRift     => $"QUEST: Seal the rift — hold the zone {sealTime:F0}s!",
            _                         => "QUEST STARTED",
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

    void OnProgressChanged(float _, float newValue) => UpdateShader(newValue);

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
