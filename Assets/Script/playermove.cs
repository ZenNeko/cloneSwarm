using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class playermove : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Health")]
    public float maxHealth = 100f;
    public UnityEvent onDeath;

    [Header("Health Regen")]
    [Tooltip("HP ที่ฟื้นต่อวินาที (0 = ปิด)")]
    public float healthRegenPerSecond = 0f;

    // ── Network State ─────────────────────────────────────────────────────
    // Server เขียน, ทุก client อ่าน
    public NetworkVariable<float> netHealth = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ── Events ────────────────────────────────────────────────────────────
    /// <summary>ยิง event เมื่อ local player spawn → FollowCamera subscribe ที่นี่</summary>
    public static event System.Action<Transform> OnLocalPlayerSpawned;

    private Vector2    moveInput;
    private Rigidbody  rb;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();

        if (IsServer)
            netHealth.Value = maxHealth;

        if (IsOwner)
        {
            // แจ้ง Camera ว่า player ของเราอยู่ที่ไหน
            OnLocalPlayerSpawned?.Invoke(transform);

            // ดัก health เปลี่ยนเพื่อ trigger Death UI
            netHealth.OnValueChanged += OnHealthChanged;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
            netHealth.OnValueChanged -= OnHealthChanged;
    }

    void OnHealthChanged(float prev, float curr)
    {
        if (curr <= 0f) onDeath.Invoke();
    }

    // ── Update ────────────────────────────────────────────────────────────
    void Update()
    {
        // Health Regen รันบน Server
        if (IsServer && healthRegenPerSecond > 0f && netHealth.Value < maxHealth)
            netHealth.Value = Mathf.Min(netHealth.Value + healthRegenPerSecond * Time.deltaTime, maxHealth);
    }

    void FixedUpdate()
    {
        // Input รันบน Owner เท่านั้น
        if (!IsOwner || rb == null) return;

        Vector3 movement = new Vector3(moveInput.x, 0f, moveInput.y);
        rb.velocity = new Vector3(movement.x * moveSpeed, rb.velocity.y, movement.z * moveSpeed);
    }

    // ── Input (New Input System) ──────────────────────────────────────────
    public void Move(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        moveInput = context.ReadValue<Vector2>().normalized;
    }

    // ── Damage / Health (Server only) ────────────────────────────────────
    /// <summary>เรียกจาก Enemy.cs (Server side) เท่านั้น</summary>
    public void TakeDamage(float amount)
    {
        if (!IsServer) return;
        netHealth.Value = Mathf.Max(0f, netHealth.Value - amount);
        if (netHealth.Value <= 0f)
            NetworkObject.Despawn(true);
    }

    /// <summary>เรียกจาก UpgradeManager ผ่าน ServerRpc</summary>
    public void GainMaxHealth(float amount)
    {
        if (!IsServer) return;
        maxHealth       += amount;
        netHealth.Value  = Mathf.Min(netHealth.Value + amount, maxHealth);
    }

    public float GetHealthPercent() => netHealth.Value / maxHealth;
    public float GetCurrentHealth() => netHealth.Value;
}
