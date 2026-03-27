using System.Collections;
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
    public float shieldHP = 0f;

    private float shieldAtLastAdd = 0f;
    private float shieldAddTime   = -999f;

    // ── Network State ─────────────────────────────────────────────────────
    public NetworkVariable<float> netHealth       = new(100f,  NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> netMaxHealth     = new(100f,  NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool>  isDead           = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    /// <summary>วินาทีที่เหลือก่อน respawn — 0 = ไม่ได้ตาย</summary>
    public NetworkVariable<float> respawnCountdown = new(0f,   NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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
        {
            netHealth.Value    = maxHealth;
            netMaxHealth.Value = maxHealth;
        }

        if (IsOwner)
        {
            // แจ้ง Camera ว่า player ของเราอยู่ที่ไหน
            OnLocalPlayerSpawned?.Invoke(transform);

            // ดัก health เปลี่ยนเพื่อ trigger Death UI
            netHealth.OnValueChanged += OnHealthChanged;
        }
        else
        {
            // ── Non-owner: ปิด PlayerInput ป้องกันแย่ง input กัน ──────────
            var pi = GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (pi != null) pi.enabled = false;

            // ── Non-owner: ปิด Rigidbody physics ──────────────────────────
            // NetworkTransform จะ sync position แทน
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
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

        // Shield decay — shield depletes to 0 over 1 second
        if (IsServer && shieldHP > 0f)
        {
            float elapsed = Time.time - shieldAddTime;
            shieldHP = elapsed >= 1f ? 0f : Mathf.Lerp(shieldAtLastAdd, 0f, elapsed);
        }
    }

    void FixedUpdate()
    {
        if (!IsOwner || rb == null || isDead.Value) return;

        Vector3 movement = new Vector3(moveInput.x, 0f, moveInput.y);
        var   sm            = IsOwner ? GetComponent<PlayerStatManager>() : null;
        float effectiveSpeed = moveSpeed * (sm != null ? sm.GetMoveSpeedMultiplier() : 1f) * (1f + tempMoveSpeedBonus);
        rb.velocity = new Vector3(movement.x * effectiveSpeed, rb.velocity.y, movement.z * effectiveSpeed);
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
        if (!IsServer || isDead.Value) return;

        // Armor flat reduction
        var sm = GetComponent<PlayerStatManager>();
        if (sm != null) amount = Mathf.Max(1f, amount - sm.GetArmorValue());

        // Shield absorption
        if (shieldHP > 0f)
        {
            float absorbed = Mathf.Min(shieldHP, amount);
            shieldHP -= absorbed;
            amount   -= absorbed;
            if (amount <= 0f) return;
        }

        netHealth.Value = Mathf.Max(0f, netHealth.Value - amount);
        if (netHealth.Value <= 0f) Die();
    }

    void Die()
    {
        isDead.Value           = true;
        respawnCountdown.Value = 0f;

        // ตรวจว่ามีคนรอดอยู่ไหม
        bool anyoneAlive = false;
        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
        {
            var pm = c.PlayerObject?.GetComponent<playermove>();
            if (pm != null && pm != this && !pm.isDead.Value)
            { anyoneAlive = true; break; }
        }

        if (anyoneAlive)
            StartCoroutine(RespawnCoroutine());
        // ถ้าไม่มีใครรอด → GameTimeline.CheckLoseCondition จะจัดการ
    }

    IEnumerator RespawnCoroutine()
    {
        float gameTimeSec = GameTimeline.Instance?.GetGameTime() ?? 0f;
        // 10 วิ + 5 วิต่อนาที ไม่เกิน 60 วิ
        float delay = Mathf.Clamp(10f + (gameTimeSec / 60f) * 5f, 10f, 60f);
        respawnCountdown.Value = delay;

        while (respawnCountdown.Value > 0f)
        {
            yield return new WaitForSeconds(1f);
            respawnCountdown.Value = Mathf.Max(0f, respawnCountdown.Value - 1f);
        }

        Respawn();
    }

    void Respawn()
    {
        // Spawn ใกล้ผู้เล่นที่รอดอยู่
        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
        {
            var pm = c.PlayerObject?.GetComponent<playermove>();
            if (pm != null && !pm.isDead.Value)
            {
                Vector2 rnd = Random.insideUnitCircle.normalized * 3f;
                transform.position = pm.transform.position + new Vector3(rnd.x, 0f, rnd.y);
                break;
            }
        }

        netHealth.Value        = maxHealth * 0.3f;
        isDead.Value           = false;
        respawnCountdown.Value = 0f;
        Debug.Log($"[Player] Respawn HP={netHealth.Value:F0}/{maxHealth:F0}");
    }

    /// <summary>เรียกจาก UpgradeManager ผ่าน ServerRpc</summary>
    public void GainMaxHealth(float amount)
    {
        if (!IsServer) return;
        maxHealth          += amount;
        netMaxHealth.Value  = maxHealth;          // sync ไปทุก client
        netHealth.Value     = Mathf.Min(netHealth.Value + amount, maxHealth);
    }

    /// <summary>ฟื้น HP flat — เรียกจาก ZoneObjective (Server side)</summary>
    public void Heal(float amount)
    {
        if (!IsServer) return;
        netHealth.Value = Mathf.Min(netHealth.Value + amount, maxHealth);
    }

    /// <summary>ฟื้น HP เป็น % ของ maxHealth — Full Build Bonus</summary>
    public void HealPercent(float percent)
    {
        if (!IsServer) return;
        netHealth.Value = Mathf.Min(netHealth.Value + maxHealth * percent, maxHealth);
    }
    

    /// <summary>เพิ่ม shield — เรียกจาก ServerRpc เท่านั้น</summary>
    public void AddShield(float amount)
    {
        if (!IsServer) return;
        shieldHP       += amount;
        shieldAtLastAdd = shieldHP;
        shieldAddTime   = Time.time;
    }

    /// <summary>Temporary move speed bonus (additive %) — จาก Blade of Exile</summary>
    [HideInInspector] public float tempMoveSpeedBonus = 0f;

    public float GetHealthPercent() => netHealth.Value / maxHealth;
    public float GetCurrentHealth() => netHealth.Value;

    /// <summary>ตั้งค่า base stats จาก CharacterData — เรียกก่อน OnNetworkSpawn</summary>
    public void SetBaseStats(float hp, float speed)
    {
        maxHealth = hp;
        moveSpeed = speed;
        // ถ้า spawn แล้ว (Server) ให้ sync ทันที
        if (IsServer)
        {
            netHealth.Value    = hp;
            netMaxHealth.Value = hp;
        }
    }
}
