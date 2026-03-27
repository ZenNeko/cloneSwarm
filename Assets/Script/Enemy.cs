using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public class Enemy : NetworkBehaviour
{
    [Header("Movement")]
    public float speed = 3f;

    [Header("Contact Damage")]
    public float contactDamage  = 10f;
    public float damageCooldown = 1f;
    [Tooltip("ระยะที่ถือว่าชนกับผู้เล่น (แทน OnTriggerStay)")]
    public float damageRadius   = 1.2f;

    [Header("Health")]
    public float maxHealth = 30f;
    public UnityEvent onDeath;

    [Header("Experience")]
    public float     expReward   = 10f;
    public GameObject expOrbPrefab;

    // ── Network State ─────────────────────────────────────────────────────
    public NetworkVariable<float> netHealth = new NetworkVariable<float>(
        30f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Transform currentTarget;
    private float     damageTimer;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        netHealth.Value = maxHealth;
        currentTarget   = FindNearestPlayer();
    }

    // ── Update: Server only ───────────────────────────────────────────────
    void Update()
    {
        if (!IsServer) return;

        // Re-target ทุก 60 frame (~1 วินาที)
        if (Time.frameCount % 60 == 0 || currentTarget == null)
            currentTarget = FindNearestPlayer();

        if (currentTarget != null)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, currentTarget.position, speed * Time.deltaTime);

            // Distance-based damage (ไม่ต้องใช้ trigger collider)
            damageTimer += Time.deltaTime;
            if (damageTimer >= damageCooldown)
            {
                float dist = Vector3.Distance(transform.position, currentTarget.position);
                if (dist <= damageRadius)
                {
                    currentTarget.GetComponent<playermove>()?.TakeDamage(contactDamage);
                    damageTimer = 0f;
                }
            }
        }
    }

    // ── Damage ────────────────────────────────────────────────────────────
    public void EnemyTakeDamage(float amount)
    {
        if (!IsServer) return;

        netHealth.Value = Mathf.Max(0f, netHealth.Value - amount);
        if (netHealth.Value > 0f) return;

        onDeath.Invoke();
        SpawnExpOrb();
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        else Destroy(gameObject);
    }

    // ── Drop ExpOrb ───────────────────────────────────────────────────────
    void SpawnExpOrb()
    {
        if (expOrbPrefab != null)
        {
            GameObject orb = Instantiate(expOrbPrefab, transform.position, Quaternion.identity);
            orb.GetComponent<NetworkObject>()?.Spawn(true);
            orb.GetComponent<ExpOrb>()?.SetExpAmount(expReward);
        }
        else
        {
            // Fallback: ให้ EXP ตรงกับ player ที่ใกล้ที่สุด
            currentTarget?.GetComponent<ExperienceManager>()?.AddExp(expReward);
        }
    }

    // ── Find Nearest Player ───────────────────────────────────────────────
    Transform FindNearestPlayer()
    {
        if (NetworkManager.Singleton == null) return null;

        Transform nearest = null;
        float     minDist = float.MaxValue;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var playerObj = client.PlayerObject;
            if (playerObj == null) continue;
            var pm = playerObj.GetComponent<playermove>();
            if (pm != null && pm.isDead.Value) continue;   // ข้ามผู้เล่นที่ตายแล้ว

            float dist = Vector3.Distance(transform.position, playerObj.transform.position);
            if (dist < minDist) { minDist = dist; nearest = playerObj.transform; }
        }
        return nearest;
    }

    /// <summary>
    /// เรียกจาก EnemySpawner หลัง Spawn — คูณ stats ตาม wave
    /// </summary>
    /// <summary>
    /// เรียกจาก EnemySpawner หลัง Spawn — คูณ stats ตาม wave
    /// </summary>
    /// <param name="healthMult">HP multiplier (1 + wave × healthMultPerWave)</param>
    /// <param name="speedMult">Speed multiplier</param>
    /// <param name="expMult">EXP reward multiplier (1 + wave × expMultPerWave) — ตั้งค่าได้ใน WaveManager</param>
    public void ApplyWaveScaling(float healthMult, float speedMult, float expMult = 1f)
    {
        if (!IsServer) return;
        maxHealth       = maxHealth * healthMult;
        netHealth.Value = maxHealth;
        speed           = speed * speedMult;
        expReward       = expReward * expMult;
    }

    public float GetHealthPercent() => netHealth.Value / maxHealth;
    public float GetCurrentHealth() => netHealth.Value;
}
