using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Main Boss — refactored to inherit from BossController
/// สร้างระบบ Legacy Fallback ให้อัตโนมัติ เพื่อให้ของเดิมทั้งหมดทำงานได้ 100% ครบถ้วน!
/// </summary>
public class MainBoss : BossController
{
    // ── Static Events (ALL clients) ───────────────────────────────────────
    public static event System.Action<MainBoss> OnAnyBossSpawned;
    public static event System.Action           OnAnyBossDespawned;

    // ── Legacy Settings (ใช้กรณีไม่มี BossEncounterConfig) ────────────────
    [Header("Phase Thresholds")]
    [Tooltip("จุดเปลี่ยนเลือด Phase 2")]
    public float legacyPhase2Threshold = 0.75f;
    [Tooltip("จุดเปลี่ยนเลือด Phase 3")]
    public float legacyPhase3Threshold = 0.30f;

    [Header("Legacy Attack Settings")]
    public float circleRadius = 5f;
    public float circleWarnTime = 2f;
    public float circleDamage = 20f;
    
    public float lineLength = 15f;
    public float lineWidth = 3f;
    public float lineWarnTime = 2f;
    public float lineDamage = 25f;
    
    public float crossLineLength = 15f;
    public float crossLineWidth = 3f;
    public float crossWarnTime = 2.5f;
    public float crossDamage = 25f;

    public float donutRadius = 10f;
    public float donutInnerRadius = 4f;
    public float donutWarnTime = 2.5f;
    public float donutDamage = 30f;

    public float chaseRadius = 4f;
    public float chaseWarnTime = 2.5f;
    public float chaseDamage = 20f;

    public float tetherDistance = 25f;
    public float tetherDuration = 10f;
    public float tetherFailDamage = 50f;
    public float tetherSoloSpawnOffset = 10f;

    // ── Backward Compatibility for BossHUDUI ──────────────────────────────
    public float phase2Threshold => (config != null && config.phases != null && config.phases.Count > 0) ? config.phases[0].transitionHealthPct : legacyPhase2Threshold;
    public float phase3Threshold => (config != null && config.phases != null && config.phases.Count > 1) ? config.phases[1].transitionHealthPct : legacyPhase3Threshold;

    public override void OnNetworkSpawn()
    {
        // 🚀 หากยังไม่ได้สร้าง Config ใน Unity ให้สร้างจำลองของเก่าทั้งหมดขึ้นมา (Runtime)
        if (config == null)
        {
            GenerateLegacyConfig();
        }

        base.OnNetworkSpawn();
        OnAnyBossSpawned?.Invoke(this);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        OnAnyBossDespawned?.Invoke();
    }

    protected override void OnPhaseChangedClient(int phaseIndex)
    {
        base.OnPhaseChangedClient(phaseIndex);

        int phaseNumber = phaseIndex + 1;
        string msg = phaseNumber switch
        {
            2 => "PHASE 2 — ENRAGE",
            3 => "PHASE 3 — FINAL FORM",
            _ => $"PHASE {phaseNumber}",
        };
        Color col = phaseNumber switch
        {
            2 => new Color(1f, 0.5f, 0f),
            3 => Color.red,
            _ => Color.white,
        };
        
        UnityEngine.Object.FindAnyObjectByType<GameHUD>()?.ShowAnnouncement(msg, col);
        NetworkedVFXPool.Instance?.PlayByName("PhaseShockwave", transform.position);
    }

    // ── ฟังก์ชันกู้คืนระบบเดิมทั้งหมด (Legacy Fallback) ─────────────────────
    private void GenerateLegacyConfig()
    {
        Debug.Log("[MainBoss] No config found! Generating Legacy Config dynamically...");
        config = ScriptableObject.CreateInstance<BossEncounterConfig>();
        config.attackInterval = 2.5f;
        config.firstAttackDelay = 4f;

        // --- สร้างท่าโจมตีต่างๆ (แบบเดียวกับของเดิม) ---
        var actCircle = ScriptableObject.CreateInstance<SpawnAoEAction>();
        actCircle.aoeType = AoEType.Circle; actCircle.radius = circleRadius; actCircle.warningDuration = circleWarnTime; actCircle.damage = circleDamage;

        var actLine = ScriptableObject.CreateInstance<SpawnAoEAction>();
        actLine.aoeType = AoEType.Line; actLine.lineLength = lineLength; actLine.lineWidth = lineWidth; actLine.warningDuration = lineWarnTime; actLine.damage = lineDamage;

        var actCrossX = ScriptableObject.CreateInstance<SpawnAoEAction>();
        actCrossX.aoeType = AoEType.Cross; actCrossX.lineLength = crossLineLength; actCrossX.lineWidth = crossLineWidth; actCrossX.warningDuration = crossWarnTime; actCrossX.damage = crossDamage; actCrossX.targetOffset = new Vector3(0, 0.1f, 0); // isX logic (hacky offset but works as marker)

        var actDonut = ScriptableObject.CreateInstance<SpawnAoEAction>();
        actDonut.aoeType = AoEType.Donut; actDonut.radius = donutRadius; actDonut.innerRadius = donutInnerRadius; actDonut.warningDuration = donutWarnTime; actDonut.damage = donutDamage;

        var actChase = ScriptableObject.CreateInstance<SpawnAoEAction>();
        actChase.aoeType = AoEType.Circle; actChase.isChasing = true; actChase.targetingMode = SpawnAoEAction.TargetingMode.RandomPlayer; actChase.radius = chaseRadius; actChase.warningDuration = chaseWarnTime; actChase.damage = chaseDamage;

        var actTether = ScriptableObject.CreateInstance<TetherAction>();
        actTether.tetherDistance = tetherDistance; actTether.tetherDuration = tetherDuration; actTether.tetherFailDamage = tetherFailDamage; actTether.tetherSoloSpawnOffset = tetherSoloSpawnOffset;

        // --- ตั้งค่า 3 Phases ---
        config.phases = new List<BossPhase>
        {
            new BossPhase // Phase 1
            {
                transitionHealthPct = legacyPhase2Threshold,
                invincibilityDuration = 1.5f,
                cameraShakeMagnitude = 0.4f,
                actions = new List<BossAction> { actCircle, actLine }
            },
            new BossPhase // Phase 2
            {
                transitionHealthPct = legacyPhase3Threshold,
                invincibilityDuration = 1.5f,
                cameraShakeMagnitude = 0.5f,
                actions = new List<BossAction> { actTether, actChase, actCrossX }
            },
            new BossPhase // Phase 3
            {
                transitionHealthPct = 0f,
                invincibilityDuration = 2.0f,
                cameraShakeMagnitude = 0.6f,
                actions = new List<BossAction> { actDonut }
            }
        };
    }
}
