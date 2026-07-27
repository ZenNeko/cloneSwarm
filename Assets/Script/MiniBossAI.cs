using UnityEngine;

/// <summary>
/// Mini-Boss AI — refactored to inherit from BossController
/// คลาสนี้เหลือไว้เพื่อความเข้ากันได้กับ Prefab เดิม และจัดการ Event เฉพาะของ Mini Boss
/// </summary>
public class MiniBossAI : BossController
{
    // ── Static Events (ALL clients) ───────────────────────────────────────
    public static event System.Action<MiniBossAI> OnAnyMiniBossSpawned;
    public static event System.Action<MiniBossAI> OnAnyMiniBossDefeated;

    // ── Backward Compatibility ────────────────────────────────────────────
    public string bossName => bossDisplayName;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        // Fire on ALL clients (ใช้โดย MiniBossHUDUI)
        OnAnyMiniBossSpawned?.Invoke(this);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        // Fire on ALL clients (ใช้โดย MiniBossHUDUI)
        OnAnyMiniBossDefeated?.Invoke(this);
    }
}
