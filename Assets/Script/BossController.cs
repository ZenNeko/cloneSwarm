using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ตัวควบคุมกลางสำหรับบอสทุกตัว ทำหน้าที่จัดการ State, เลือดเปลี่ยน Phase, และยิงท่าโจมตีตาม BossEncounterConfig
/// ใช้เป็น component ตรงๆ บน prefab บอสได้เลย หรือสืบทอดไปทำ Event เฉพาะตัว
/// </summary>
[RequireComponent(typeof(Enemy))]
public class BossController : NetworkBehaviour
{
    [Header("Display")]
    public string bossDisplayName = "";

    [Header("Config")]
    public BossEncounterConfig config;

    [Header("Prefabs")]
    public GameObject telegraphZonePrefab;
    public GameObject tetherPrefab;

    // ── Static Events (ALL clients) ───────────────────────────────────────
    public static event System.Action<BossController>                OnAnyBossSpawned;
    public static event System.Action<BossController>                OnAnyBossDespawned;
    public static event System.Action<BossController, string, float> OnAnyCastStarted;
    public static event System.Action<BossController>                OnAnyCastEnded;

    // ── Phase Threshold Properties for BossHUDUI ─────────────────────────
    public virtual float phase2Threshold => (config != null && config.phases != null && config.phases.Count > 0) ? config.phases[0].transitionHealthPct : 0.75f;
    public virtual float phase3Threshold => (config != null && config.phases != null && config.phases.Count > 1) ? config.phases[1].transitionHealthPct : 0.30f;

    public NetworkVariable<int> FightSeed = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public RollContext Rolls { get; private set; }

    protected Enemy enemy;
    protected int currentPhaseIndex = 0;
    protected int mechanicIndex = 0;
    protected bool deathHandled = false;
    protected float currentPhaseStartTime = 0f;

    // NetworkObject ที่ action ยิงออกไป (telegraph / tether) — ไม่มี back-reference กลับหาบอส
    // จึงต้องให้บอสถือทะเบียนเอง ไม่งั้นบอสตายแล้วของพวกนี้ยังระเบิดต่อ
    readonly List<NetworkObject> _activeMechanics = new();
    
    protected Coroutine attackLoopCoroutine;
    protected Coroutine phaseTransitionCoroutine;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        OnAnyBossSpawned?.Invoke(this);

        FightSeed.OnValueChanged += OnFightSeedChanged;
        if (FightSeed.Value != 0)
        {
            InitRolls(FightSeed.Value);
        }

        if (IsServer)
        {
            int s;
            do { s = new System.Random().Next(); } while (s == 0);
            FightSeed.Value = s;

            enemy = GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.onDeath.AddListener(OnDeath);
                enemy.netHealth.OnValueChanged += OnHealthChanged;
            }

            if (config == null || config.phases == null || config.phases.Count == 0)
            {
                Debug.LogWarning($"[BossController] {gameObject.name} ไม่มี config หรือ phases ว่าง!");
                return;
            }

            currentPhaseIndex = 0;
            mechanicIndex = 0;
            currentPhaseStartTime = Time.time;
            attackLoopCoroutine = StartCoroutine(AttackLoop());
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        FightSeed.OnValueChanged -= OnFightSeedChanged;
        CleanupMechanics();
        OnAnyBossDespawned?.Invoke(this);
        if (enemy != null)
        {
            enemy.onDeath.RemoveListener(OnDeath);
            enemy.netHealth.OnValueChanged -= OnHealthChanged;
        }
    }

    private void OnFightSeedChanged(int oldVal, int newVal) => InitRolls(newVal);

    private void InitRolls(int seed)
    {
        Rolls = new RollContext(seed, config != null ? config.rolls : null);

        // ท่าไม่ deterministic อีกต่อไปหลังเสียบ roll — ต้องจด seed ไว้ถึงจะ reproduce บั๊กได้
        int rollCount = config?.rolls != null ? config.rolls.Length : 0;
        Debug.Log($"[BossController] {gameObject.name} roll seed = {seed} · roll definitions = {rollCount}");
    }

    // ── Phase Tracking (Server Only) ──────────────────────────────────────
    protected virtual void OnHealthChanged(float oldHP, float newHP)
    {
        if (!IsServer || config == null || config.phases == null) return;

        // เช็คว่ามี Phase ถัดไปหรือไม่
        if (currentPhaseIndex < config.phases.Count - 1)
        {
            float pct = enemy.maxHealth > 0 ? newHP / enemy.maxHealth : 0f;
            BossPhase currentPhaseInfo = config.phases[currentPhaseIndex];

            // ถ้าเลือดลดต่ำกว่าจุดเชื่อมต่อ Phase ของปัจจุบัน
            if (pct <= currentPhaseInfo.transitionHealthPct)
            {
                currentPhaseIndex++;
                mechanicIndex = 0; // รีเซ็ตการวนโจมตีสำหรับ Phase ใหม่

                if (phaseTransitionCoroutine != null) StopCoroutine(phaseTransitionCoroutine);
                phaseTransitionCoroutine = StartCoroutine(PhaseTransitionInvincibility(config.phases[currentPhaseIndex]));
                
                PhaseChangedClientRpc(currentPhaseIndex);
                Debug.Log($"[BossController] {gameObject.name} เลื่อนเป็น Phase {currentPhaseIndex + 1} ({pct:P0} HP)");
            }
        }
    }

    protected IEnumerator PhaseTransitionInvincibility(BossPhase newPhaseInfo)
    {
        if (attackLoopCoroutine != null) StopCoroutine(attackLoopCoroutine);

        if (enemy != null && newPhaseInfo.invincibilityDuration > 0)
        {
            enemy.serverInvincible = true;
            yield return new WaitForSeconds(newPhaseInfo.invincibilityDuration);
            enemy.serverInvincible = false;
        }

        phaseTransitionCoroutine = null;
        currentPhaseStartTime = Time.time;
        attackLoopCoroutine = StartCoroutine(AttackLoop());
    }

    [ClientRpc]
    protected void PhaseChangedClientRpc(int phaseIndex)
    {
        OnPhaseChangedClient(phaseIndex);
    }

    /// <summary>Override ได้ในคลาสลูกเพื่อจัดการแสดงผล UI หรือ Effect เมื่อเปลี่ยน Phase</summary>
    protected virtual void OnPhaseChangedClient(int phaseIndex)
    {
        if (config == null || config.phases == null || phaseIndex >= config.phases.Count) return;

        BossPhase phase = config.phases[phaseIndex];

        // ประกาศเฟส + VFX — data-driven จาก BossPhase (ย้ายมาจาก MainBoss เดิม)
        if (!string.IsNullOrEmpty(phase.announcementText))
        {
            GameHUD.Instance?.ShowAnnouncement(phase.announcementText, phase.announcementColor);
        }
        if (phase.phaseVfx != null && NetworkedVFXPool.Instance != null && NetworkedVFXPool.Instance.vfxDatabase != null)
        {
            int id = NetworkedVFXPool.Instance.vfxDatabase.GetIdForAsset(phase.phaseVfx);
            NetworkedVFXPool.Instance.PlayById(id, transform.position);
        }

        if (phase.cameraShakeMagnitude > 0)
        {
            CameraShake.Instance?.Shake(0.5f, phase.cameraShakeMagnitude);
        }
    }

    // ── Attack Loop (Server Only) ─────────────────────────────────────────
    protected IEnumerator AttackLoop()
    {
        yield return new WaitForSeconds(config.firstAttackDelay);

        while (true)
        {
            if (!NetworkObject.IsSpawned || enemy == null || enemy.netHealth.Value <= 0) yield break;

            if (config.phases == null || currentPhaseIndex >= config.phases.Count)
            {
                yield return new WaitForSeconds(1f);
                continue;
            }

            BossPhase currentPhase = config.phases[currentPhaseIndex];

            // ── Check Enrage Timer ──
            bool isEnraged = false;
            if (currentPhase.enrageTime > 0f && Time.time - currentPhaseStartTime >= currentPhase.enrageTime)
            {
                isEnraged = true;
            }

            var currentActionList = isEnraged && currentPhase.enrageActions != null && currentPhase.enrageActions.Count > 0
                ? currentPhase.enrageActions
                : currentPhase.actions;

            if (currentActionList == null || currentActionList.Count == 0)
            {
                yield return new WaitForSeconds(1f);
                continue;
            }

            BossAction action = currentActionList[mechanicIndex % currentActionList.Count];
            if (action != null)
            {
                if (!string.IsNullOrEmpty(action.rollName) && Rolls != null)
                {
                    Rolls.Roll(action.rollName);
                }

                if (action.castTime > 0f && !string.IsNullOrEmpty(action.castName))
                {
                    CastStartClientRpc(action.castName, action.castTime);
                    yield return new WaitForSeconds(action.castTime);
                }

                yield return StartCoroutine(action.ExecuteCoroutine(this, telegraphZonePrefab));

                if (action.castTime > 0f && !string.IsNullOrEmpty(action.castName))
                {
                    CastEndClientRpc();
                }

                float phaseInterval = currentPhase.attackInterval > 0f ? currentPhase.attackInterval : config.attackInterval;
                float cooldown = action.cooldownAfter > 0f ? action.cooldownAfter : phaseInterval;
                yield return new WaitForSeconds(cooldown);
            }
            else
            {
                float phaseInterval = currentPhase.attackInterval > 0f ? currentPhase.attackInterval : config.attackInterval;
                yield return new WaitForSeconds(phaseInterval);
            }

            mechanicIndex++;
        }
    }

    [ClientRpc]
    private void CastStartClientRpc(string castName, float castTime)
    {
        OnAnyCastStarted?.Invoke(this, castName, castTime);
    }

    [ClientRpc]
    private void CastEndClientRpc()
    {
        OnAnyCastEnded?.Invoke(this);
    }

    // ── Death & Drops (Server Only) ───────────────────────────────────────
    protected virtual void OnDeath()
    {
        if (!IsServer || deathHandled) return;
        deathHandled = true;

        CastEndClientRpc();

        if (attackLoopCoroutine != null) StopCoroutine(attackLoopCoroutine);
        if (phaseTransitionCoroutine != null) StopCoroutine(phaseTransitionCoroutine);
        
        CleanupMechanics();
        SpawnExtraDrops();
    }

    /// <summary>ให้ BossAction แจ้งว่ายิงกลไกอะไรออกไป — บอสจะเก็บกวาดให้ตอนตาย</summary>
    public void RegisterMechanic(NetworkObject no)
    {
        if (!IsServer || no == null) return;
        _activeMechanics.Add(no);
    }

    /// <summary>ลบกลไกที่ยังค้างอยู่ — เรียกตอนบอสตายหรือถูก despawn</summary>
    void CleanupMechanics()
    {
        for (int i = _activeMechanics.Count - 1; i >= 0; i--)
        {
            var no = _activeMechanics[i];
            if (no != null && no.IsSpawned) no.Despawn(true);
        }
        _activeMechanics.Clear();
    }

    protected void SpawnExtraDrops()
    {
        if (config == null) return;

        // ExpOrb extras
        if (config.extraExpOrbs > 0 && enemy != null && enemy.expOrbPrefab != null)
        {
            for (int i = 0; i < config.extraExpOrbs; i++)
            {
                Vector2 rand = Random.insideUnitCircle * config.dropScatterRadius;
                Vector3 pos  = transform.position + new Vector3(rand.x, 0f, rand.y);
                var orb = Instantiate(enemy.expOrbPrefab, pos, Quaternion.identity);
                orb.GetComponent<NetworkObject>()?.Spawn(true);
                orb.GetComponent<ExpOrb>()?.SetExpAmount(config.extraExpPerOrb);
            }
        }

        // Bonus GameObject drops
        if (config.bonusDrops != null)
        {
            foreach (var drop in config.bonusDrops)
            {
                if (drop == null || drop.prefab == null || drop.count <= 0) continue;

                for (int i = 0; i < drop.count; i++)
                {
                    Vector2 rand = drop.scatterRadius > 0f ? Random.insideUnitCircle * drop.scatterRadius : Vector2.zero;
                    Vector3 pos = transform.position + new Vector3(rand.x, 0f, rand.y);

                    var go = Instantiate(drop.prefab, pos, Quaternion.identity);
                    var no = go.GetComponent<NetworkObject>();
                    if (no != null) no.Spawn(true);
                }
            }
        }
    }
}
