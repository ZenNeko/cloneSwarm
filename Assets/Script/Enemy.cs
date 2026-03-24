using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public class Enemy : NetworkBehaviour
{
    [Header("Movement")]
    public float speed = 3f;

    [Header("Contact Damage")]
    public float contactDamage = 10f;
    public float damageCooldown = 1f;

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
            transform.position = Vector3.MoveTowards(
                transform.position, currentTarget.position, speed * Time.deltaTime);

        damageTimer += Time.deltaTime;
    }

    void OnTriggerStay(Collider other)
    {
        if (!IsServer) return;
        if (!other.CompareTag("Player") || damageTimer < damageCooldown) return;

        playermove pm = other.GetComponent<playermove>();
        if (pm == null) return;

        pm.TakeDamage(contactDamage);
        damageTimer = 0f;
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

            float dist = Vector3.Distance(transform.position, playerObj.transform.position);
            if (dist < minDist) { minDist = dist; nearest = playerObj.transform; }
        }
        return nearest;
    }

    public float GetHealthPercent() => netHealth.Value / maxHealth;
    public float GetCurrentHealth() => netHealth.Value;
}
