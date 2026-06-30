using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ตัวควบคุมกลางสำหรับบอสทุกตัว ทำหน้าที่จัดการ State, เลือดเปลี่ยน Phase, และยิงท่าโจมตีตาม BossEncounterConfig
/// สืบทอดคลาสนี้ไปเป็น MainBoss หรือ MiniBossAI ได้เพื่อทำ Event เฉพาะตัว
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

    protected Enemy enemy;
    protected int currentPhaseIndex = 0;
    protected int mechanicIndex = 0;
    protected bool deathHandled = false;
    
    protected Coroutine attackLoopCoroutine;
    protected Coroutine phaseTransitionCoroutine;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer) return;

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
        attackLoopCoroutine = StartCoroutine(AttackLoop());
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (enemy != null)
        {
            enemy.onDeath.RemoveListener(OnDeath);
            enemy.netHealth.OnValueChanged -= OnHealthChanged;
        }
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

            if (currentPhase.actions == null || currentPhase.actions.Count == 0)
            {
                yield return new WaitForSeconds(1f);
                continue;
            }

            BossAction action = currentPhase.actions[mechanicIndex % currentPhase.actions.Count];
            if (action != null)
            {
                yield return StartCoroutine(action.ExecuteCoroutine(this, telegraphZonePrefab));

                float cooldown = action.cooldownAfter > 0f ? action.cooldownAfter : config.attackInterval;
                yield return new WaitForSeconds(cooldown);
            }
            else
            {
                yield return new WaitForSeconds(config.attackInterval);
            }

            mechanicIndex++;
        }
    }

    // ── Death & Drops (Server Only) ───────────────────────────────────────
    protected virtual void OnDeath()
    {
        if (!IsServer || deathHandled) return;
        deathHandled = true;

        if (attackLoopCoroutine != null) StopCoroutine(attackLoopCoroutine);
        if (phaseTransitionCoroutine != null) StopCoroutine(phaseTransitionCoroutine);
        
        SpawnExtraDrops();
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
