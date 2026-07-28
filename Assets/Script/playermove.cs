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
    [Tooltip("วินาทีที่ shield ค่อยๆ สลายจนหมด (นับจากครั้งล่าสุดที่ AddShield)")]
    public float shieldDuration = 1f;

    // NetworkVariable เพื่อให้ทุก client อ่านค่า shield ของตัวเองได้ (HUD, damage absorb)
    public NetworkVariable<float> netShieldHP = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ใช้เฉพาะบน Server สำหรับ decay interpolation
    private float shieldAtLastAdd = 0f;
    private float shieldAddTime   = -999f;

    // ── Network State ─────────────────────────────────────────────────────
    public NetworkVariable<float> netHealth       = new(100f,  NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> netMaxHealth     = new(100f,  NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool>  isDead           = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    /// <summary>วินาทีที่เหลือก่อน respawn — 0 = ไม่ได้ตาย</summary>
    public NetworkVariable<float> respawnCountdown = new(0f,   NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>จำนวน FetchItem ที่ player ถืออยู่ — สำหรับ ZoneObjective Type B (FetchAndDeliver)
    /// อ่านโดย QuestCarryHUD บน owner client / เขียนโดย server เท่านั้น</summary>
    public NetworkVariable<int> carriedQuestItems = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ── Events ────────────────────────────────────────────────────────────
    /// <summary>ยิง event เมื่อ local player spawn → FollowCamera subscribe ที่นี่</summary>
    public static event System.Action<Transform> OnLocalPlayerSpawned;

    private Vector2           moveInput;
    private Rigidbody         rb;
    private PlayerStatManager _statManager;

    /// <summary>ทิศที่ผู้เล่นกด input อยู่ (world space XZ, normalized)
    /// Vector3.zero ถ้าไม่ได้กด — ใช้โดย dash weapons</summary>
    public Vector3 MoveDirection => new Vector3(moveInput.x, 0f, moveInput.y).normalized;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        rb           = GetComponent<Rigidbody>();
        _statManager = GetComponent<PlayerStatManager>();

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
        // Damage feedback (owner only) — fires when HP decreases
        if (curr < prev && IsOwner)
        {
            float dmg       = prev - curr;
            float intensity = Mathf.Clamp01(dmg / maxHealth * 3f); // small hits = small flash
            DamageFeedbackUI.Instance?.ShowFlash(intensity);
            CameraShake.Instance?.Shake(0.2f, 0.25f * Mathf.Max(intensity, 0.4f));
        }

        if (curr <= 0f) onDeath.Invoke();
    }

    // ── Update ────────────────────────────────────────────────────────────
    void Update()
    {
        // Health Regen รันบน Server
        float totalRegen = healthRegenPerSecond + tempHealthRegenBonus;
        if (IsServer && totalRegen > 0f && netHealth.Value < maxHealth)
            netHealth.Value = Mathf.Min(netHealth.Value + totalRegen * Time.deltaTime, maxHealth);

        // Shield decay — depletes to 0 over shieldDuration * Duration stat (Server only)
        if (IsServer && netShieldHP.Value > 0f)
        {
            float durationMult      = _statManager?.GetDurationMultiplier() ?? 1f;
            float effectiveDuration = shieldDuration * durationMult;
            float elapsed           = Time.time - shieldAddTime;
            netShieldHP.Value = elapsed >= effectiveDuration ? 0f
                              : Mathf.Lerp(shieldAtLastAdd, 0f, elapsed / effectiveDuration);
        }
    }

    /// <summary>ตั้งเป็น true ระหว่าง dash — ทำให้ FixedUpdate ไม่เขียนทับ velocity</summary>
    [HideInInspector] public bool isDashing;
    /// <summary>ตั้งเป็น true ระหว่างโดน knockback — ทำให้ FixedUpdate ไม่เขียนทับ velocity</summary>
    [HideInInspector] public bool isKnockedBack;

    [ClientRpc]
    public void ApplyKnockbackClientRpc(Vector3 velocity, float duration)
    {
        if (IsOwner && rb != null && !isDead.Value)
        {
            StartCoroutine(KnockbackCoroutine(velocity, duration));
        }
    }

    private IEnumerator KnockbackCoroutine(Vector3 velocity, float duration)
    {
        isKnockedBack = true;
        rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);
        yield return new WaitForSeconds(duration);
        isKnockedBack = false;
    }

    void FixedUpdate()
    {
        if (!IsOwner || rb == null || isDead.Value) return;
        if (isDashing || isKnockedBack) return;   // ปล่อยให้ dash หรือ knockback ควบคุม position เอง

        Vector3 movement = new Vector3(moveInput.x, 0f, moveInput.y);
        var   sm            = IsOwner ? _statManager : null;
        float effectiveSpeed = moveSpeed * (sm != null ? sm.GetMoveSpeedMultiplier() : 1f) * (1f + tempMoveSpeedBonus);
        rb.linearVelocity = new Vector3(movement.x * effectiveSpeed, rb.linearVelocity.y, movement.z * effectiveSpeed);
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
        if (netShieldHP.Value > 0f)
        {
            float absorbed = Mathf.Min(netShieldHP.Value, amount);
            netShieldHP.Value -= absorbed;
            amount            -= absorbed;
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

    // ── Quest Carry Items (FetchAndDeliver) ───────────────────────────────
    /// <summary>เพิ่ม carry counter +1 — server only (เรียกจาก FetchItem.CollectServerRpc)</summary>
    public void AddCarriedQuestItem()
    {
        if (!IsServer) return;
        carriedQuestItems.Value++;
    }

    /// <summary>คืนจำนวนที่ถือ + reset เป็น 0 — server only (เรียกจาก ZoneObjective drain)</summary>
    public int DrainCarriedQuestItems()
    {
        if (!IsServer) return 0;
        int n = carriedQuestItems.Value;
        carriedQuestItems.Value = 0;
        return n;
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
        netShieldHP.Value += amount;
        shieldAtLastAdd    = netShieldHP.Value;
        shieldAddTime      = Time.time;
    }

    /// <summary>Temporary move speed bonus (additive %) — จาก Blade of Exile</summary>
    [HideInInspector] public float tempMoveSpeedBonus = 0f;

    /// <summary>Temporary HP regen bonus (additive HP/s) — จาก SupportArenaWeapon</summary>
    [HideInInspector] public float tempHealthRegenBonus = 0f;

    /// <summary>
    /// หารด้วย netMaxHealth ไม่ใช่ field maxHealth — maxHealth ถูกเซ็ตเฉพาะบน owner
    /// สำเนาของผู้เล่นคนอื่นบนเครื่องเราจะค้างที่ค่า default ของ prefab ทำให้หลอดยาวเกินจริง
    /// </summary>
    public float GetHealthPercent()
        => netMaxHealth.Value > 0f ? netHealth.Value / netMaxHealth.Value : 0f;
    public float GetCurrentHealth() => netHealth.Value;

    /// <summary>
    /// ตั้งค่า base stats จาก CharacterData — เรียกจาก PlayerWeaponManager.OnNetworkSpawn
    /// Client ส่งแค่ index ให้ server ไปอ่านค่าเอง (กัน HP spoofing)
    /// </summary>
    public void SetBaseStats(CharacterData cd)
    {
        if (cd == null) return;

        // Local prediction — ค่าจริงบน server มาจาก CharacterData เดียวกันอยู่แล้ว
        maxHealth = cd.baseHealth;
        moveSpeed = cd.baseMoveSpeed;

        if (IsServer)
        {
            // Host: update NetworkVariables โดยตรง
            netHealth.Value    = cd.baseHealth;
            netMaxHealth.Value = cd.baseHealth;
        }
        else if (IsSpawned)
        {
            // Non-host Client: ส่งแค่ index — server อ่าน baseHealth จาก asset เอง
            var visual = GetComponent<PlayerVisual>();
            int idx    = visual != null ? visual.IndexOfCharacter(cd) : -1;
            SyncBaseStatsServerRpc(idx);
        }
    }

    /// <summary>
    /// Client บอก Server แค่ว่าเลือก "ตัวละครไหน" (index) — ไม่ใช่ค่า HP
    /// Server อ่าน baseHealth/baseMoveSpeed จาก CharacterData เอง → client spoof ค่าไม่ได้
    /// </summary>
    [ServerRpc]   // RequireOwnership = true (default) — only owner calls
    void SyncBaseStatsServerRpc(int charIndex)
    {
        var visual = GetComponent<PlayerVisual>();
        if (visual == null)
        {
            Debug.LogWarning("[playermove] ไม่มี PlayerVisual — ข้าม base stat sync");
            return;
        }

        // ถ้า server validate index ไว้แล้ว (ผ่าน PlayerVisual.SetCharacterServerRpc) ใช้ค่านั้นก่อน
        // ไม่งั้นค่อยใช้ index ที่ client ส่งมา (ยังปลอดภัย เพราะ GetCharacterData bounds-check ให้)
        int idx = visual.CharacterIndex >= 0 ? visual.CharacterIndex : charIndex;

        var cd = visual.GetCharacterData(idx);
        if (cd == null)
        {
            Debug.LogWarning($"[playermove] charIndex {idx} ไม่ถูกต้อง — ข้าม base stat sync");
            return;
        }

        maxHealth          = cd.baseHealth;
        moveSpeed          = cd.baseMoveSpeed;
        netHealth.Value    = cd.baseHealth;
        netMaxHealth.Value = cd.baseHealth;
    }
}
