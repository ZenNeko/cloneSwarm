using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class playermove : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Health")]
    /// <summary>HP สูงสุดฝั่ง server เท่านั้น — GainMaxHealth/SetBaseStats เขียนตัวนี้ใต้ IsServer
    /// **client ต้องอ่าน netMaxHealth แทน** ค่าตัวนี้บน client ค้างตั้งแต่ตอน spawn
    /// (ระเบียบเดียวกับ Enemy.maxHealth vs Enemy.netMaxHealth)</summary>
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

    // ── Shield layers (ADR-008 D1) ──────────────────────────────────────────
    // list ของชั้นโล่ฝั่ง server เท่านั้น — netShieldHP.Value คือผลรวม replicate ให้ HUD/absorb เหมือนเดิม
    // คณิตล้วนอยู่ใน ShieldStack.cs (แยกไฟล์เพื่อเทสได้แบบเดียวกับ TelegraphGeometry)
    private readonly List<ShieldLayer> _shieldLayers = new List<ShieldLayer>();

    // ── Network State ─────────────────────────────────────────────────────
    public NetworkVariable<float> netHealth       = new(100f,  NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> netMaxHealth     = new(100f,  NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool>  isDead           = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    /// <summary>เลขลำดับจาก LimitCutAction — 0 = ไม่มีเลข · ทุก client อ่านได้เพื่อโชว์เหนือหัวกันและกัน</summary>
    public NetworkVariable<int>   limitCutNumber   = new(0,     NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    [Header("Respawn")]
    [Tooltip("DeathSpot = จุดที่ตาย\nSpawnPoint = จุดเกิดเดิมของตัวเอง (จาก GameSessionManager)\nNextToTeammate = ข้างเพื่อนที่ยังรอด")]
    public RespawnPlace respawnPlace = RespawnPlace.DeathSpot;

    [Tooltip("วินาทีที่อมตะหลังเกิดใหม่ - 0 = ปิด\nจำเป็นเมื่อเกิดที่จุดที่ตาย เพราะกองศัตรูที่ฆ่าเราไปยังอยู่ที่เดิม")]
    public float respawnInvulnSeconds = 2f;

    /// <summary>จุดที่จะไปโผล่หลังนับถอยหลังจบ — DeathSpot อยู่ลำดับแรกให้มีค่า 0
    /// prefab/scene ที่ยังไม่มีค่านี้จึงได้ DeathSpot เป็น default อัตโนมัติ</summary>
    public enum RespawnPlace { DeathSpot, SpawnPoint, NextToTeammate }

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
            float dmg = prev - curr;

            // ต้องอ่าน netMaxHealth ไม่ใช่ field maxHealth — GainMaxHealth มี if (!IsServer) return
            // ค่า maxHealth บน client จึงค้างที่ baseHealth ตอน spawn ตลอดรัน (remote player ค้างที่
            // ค่า default ของ prefab ด้วยซ้ำ เพราะ SetBaseStats เป็น owner-only)
            // ใช้ค่าค้างทำให้ vignette แรงเกินจริงเฉพาะบนเครื่อง client — host ไม่เจอเพราะเป็น server
            float maxHp     = netMaxHealth.Value > 0f ? netMaxHealth.Value : maxHealth;
            float intensity = Mathf.Clamp01(dmg / maxHp * 3f); // small hits = small flash
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

        // Shield decay (Server only) — ลบรายเฟรมต่อชั้นผ่าน ShieldStack.Tick (ADR-008 D2)
        // ไม่ใช่ Lerp จาก snapshot แบบเดิม — ของเดิมเขียนทับการหักดาเมจของ TakeDamage เพราะคำนวณ
        // ค่าสัมบูรณ์จาก shieldAtLastAdd ทุกเฟรม ตอนนี้ Absorb (TakeDamage) กับ Tick (ที่นี่) ต่างคน
        // ต่างลบจาก amount ตรงๆ บวก/ลบกันได้ไม่ว่าใครมาก่อนมาหลัง
        if (IsServer && _shieldLayers.Count > 0)
        {
            ShieldStack.Tick(_shieldLayers, Time.deltaTime, Time.time);
            netShieldHP.Value = ShieldStack.Sum(_shieldLayers);
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
        if (!IsOwner || rb == null || isDead.Value || GamePause.LocalInputSuspended) return;
        if (isDashing || isKnockedBack) return;   // ปล่อยให้ dash หรือ knockback ควบคุม position เอง

        Vector3 movement = new Vector3(moveInput.x, 0f, moveInput.y);
        float effectiveSpeed = GetEffectiveMoveSpeed();
        rb.linearVelocity = new Vector3(movement.x * effectiveSpeed, rb.linearVelocity.y, movement.z * effectiveSpeed);
    }

    /// <summary>
    /// ความเร็วเดินจริงหลังคูณ stat และบัฟชั่วคราว — สกิลที่อิง "% ของ move speed"
    /// (เช่น Valor dash) ต้องอ่านจากที่นี่ ไม่ใช่คำนวณสูตรซ้ำเอง
    /// </summary>
    public float GetEffectiveMoveSpeed()
    {
        var sm = _statManager;
        return moveSpeed * (sm != null ? sm.GetMoveSpeedMultiplier() : 1f) * (1f + tempMoveSpeedBonus);
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

        // อมตะสั้นๆ หลังเกิดใหม่ — เกิดที่จุดที่ตายแปลว่าโผล่กลางกองเดิมที่เพิ่งฆ่าเรา
        // ไม่มีช่วงนี้จะโดนตีตายซ้ำก่อนขยับทัน (โปรเจกต์นี้ไม่เคยมี i-frame ให้ player มาก่อน)
        if (_invulnUntil > 0f && Time.time < _invulnUntil) return;

        var status = GetComponent<PlayerStatusManager>();
        if (status != null) amount *= status.GetDamageTakenMult();

        // Armor flat reduction
        var sm = GetComponent<PlayerStatManager>();
        if (sm != null) amount = Mathf.Max(1f, amount - sm.GetArmorValue());

        // Shield absorption — ดูดจากชั้นใกล้หมดอายุก่อน (ADR-008 D1) ผ่าน ShieldStack.Absorb
        // amount ที่คืนมาคือดาเมจที่เหลือหลังโล่ดูดไม่ไหว (overkill) — ไม่แตะ netShieldHP.Value ตรงๆ อีกต่อไป
        if (_shieldLayers.Count > 0)
        {
            amount = ShieldStack.Absorb(_shieldLayers, amount, Time.time);
            netShieldHP.Value = ShieldStack.Sum(_shieldLayers);
            if (amount <= 0f) return;
        }

        netHealth.Value = Mathf.Max(0f, netHealth.Value - amount);
        if (netHealth.Value <= 0f) Die();
    }

    /// <summary>พิกัดตอนล้ม — บันทึกเองแทนการอ่าน transform ตอน respawn
    /// เพราะศพยังมี Rigidbody ที่ไม่ kinematic กองศัตรูดันให้เลื่อนได้ระหว่างรอเกิด</summary>
    private Vector3 _deathPosition;
    /// <summary>เวลาที่หมดอมตะ (Time.time ฝั่ง server) — 0 = ไม่ได้อมตะ</summary>
    private float   _invulnUntil;

    void Die()
    {
        // ── Second Chance (Talent ถาวร) — ชุบชีวิตทันที 1 ครั้งต่อเกม ─────
        var talents = GetComponent<CloneSwarm.Meta.PlayerTalentApplier>();
        if (talents != null && talents.SecondChanceAvailable)
        {
            talents.ConsumeSecondChance();
            netHealth.Value = maxHealth * 0.3f;
            SecondChanceClientRpc();
            Debug.Log($"[Player] 💫 Second Chance! ชุบชีวิตที่ HP={netHealth.Value:F0}");
            return;
        }

        _deathPosition         = transform.position;
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

    /// <summary>ยิงบนทุก client เมื่อผู้เล่นคนนี้ใช้ Second Chance — UI/VFX subscribe ได้</summary>
    public static event System.Action<playermove> OnSecondChanceUsed;

    [ClientRpc]
    void SecondChanceClientRpc()
    {
        OnSecondChanceUsed?.Invoke(this);
        if (IsOwner)
        {
            DamageFeedbackUI.Instance?.ShowFlash(1f);
            CameraShake.Instance?.Shake(0.4f, 0.5f);
        }
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
        // ย้ายตำแหน่งต้องผ่าน owner — NetworkTransform ของ player เป็น AuthorityMode: Owner
        // server เขียน transform.position ตรงๆ จะถูก owner เขียนทับทันที (ของเดิมทำแบบนั้น
        // จึงได้ผลเฉพาะบน host) · ทางเดียวกับ ApplyKnockbackClientRpc ที่ CLAUDE.md ระบุไว้
        //
        // ส่ง teleport ก่อนปลด isDead — ระหว่างที่ยังตายอยู่ FixedUpdate ของ owner return ทันที
        // จึงไม่มีอะไรมาแย่งเขียน velocity ตอนวาร์ป
        TeleportClientRpc(GetRespawnPosition());

        netHealth.Value        = maxHealth * 0.3f;
        isDead.Value           = false;
        respawnCountdown.Value = 0f;
        _invulnUntil           = respawnInvulnSeconds > 0f ? Time.time + respawnInvulnSeconds : 0f;
        Debug.Log($"[Player] Respawn HP={netHealth.Value:F0}/{maxHealth:F0} ที่ {respawnPlace} " +
                  $"· อมตะ {respawnInvulnSeconds:F1}s");
    }

    /// <summary>จุดที่จะไปเกิด — จุดเกิดเดิมของตัวเอง หรือข้างเพื่อนถ้าปิดตัวเลือกไว้</summary>
    Vector3 GetRespawnPosition()
    {
        if (respawnPlace == RespawnPlace.DeathSpot) return _deathPosition;

        if (respawnPlace == RespawnPlace.SpawnPoint)
        {
            var session = GameSessionManager.Instance;
            if (session != null) return session.GetSpawnPosition(OwnerClientId);

            Debug.LogWarning("[Player] ไม่พบ GameSessionManager — ตกไปใช้จุดเกิดข้างเพื่อนแทน");
        }

        // เกิดข้างผู้เล่นที่ยังรอด (พฤติกรรมเดิม)
        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
        {
            var po = c.PlayerObject;
            if (po == null) continue;

            var pm = po.GetComponent<playermove>();
            if (pm == null || pm == this || pm.isDead.Value) continue;

            Vector2 rnd = Random.insideUnitCircle.normalized * 3f;
            return pm.transform.position + new Vector3(rnd.x, 0f, rnd.y);
        }

        return transform.position;   // ไม่เหลือใครเลย — อยู่ที่เดิม
    }

    /// <summary>วาร์ปผู้เล่นไปพิกัดที่ server กำหนด — เฉพาะ owner เท่านั้นที่ขยับตัวเองได้</summary>
    [ClientRpc]
    void TeleportClientRpc(Vector3 pos)
    {
        if (!IsOwner) return;

        // เคลียร์ velocity ด้วย ไม่งั้นความเร็วก่อนตายจะพาลอยต่อจากจุดใหม่
        if (rb != null)
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position        = pos;
        }
        transform.position = pos;
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
    

    /// <summary>
    /// เพิ่ม shield เป็นชั้นใหม่ — เรียกจาก ServerRpc เท่านั้น (server-authoritative)
    /// พารามิเตอร์ default ให้พฤติกรรมเหมือนเดิมทุกจุดกับตอนที่ยังเป็นตัวเลขเดียว:
    /// decays=true (สลายต่อเนื่องแบบเดิม), duration = shieldDuration * Duration stat ตอนที่เรียก,
    /// sourceId = ShieldSourceId.Unassigned (สแตกเป็นชั้นใหม่เสมอ ไม่ไป replace ชั้นเดิม)
    /// เรียกเปลี่ยนพฤติกรรมได้ผ่าน param ที่เหลือ — เช่น decays=false สำหรับโล่แบบ Immortal Shieldbow
    /// </summary>
    public void AddShield(float amount, bool decays = true, float duration = -1f, int sourceId = ShieldSourceId.Unassigned)
    {
        if (!IsServer) return;
        if (!ShieldStack.IsValidAmount(amount)) return;

        float effectiveDuration = duration > 0f ? duration : GetEffectiveShieldDuration();
        ShieldStack.Add(_shieldLayers, amount, effectiveDuration, decays, sourceId, Time.time);
        netShieldHP.Value = ShieldStack.Sum(_shieldLayers);
    }

    /// <summary>
    /// รีเฟรชชั้นโล่ที่มี sourceId นี้อยู่แล้ว (แทนที่ amount/duration/expiry) แทนการสแตกซ้อนไม่จำกัด
    /// ถ้ายังไม่มีชั้นของ sourceId นี้ → สร้างใหม่ให้เหมือน AddShield ครั้งแรก
    /// ใช้กับแหล่งที่ตั้งใจให้ "ยืนอยู่ในโซนแล้วต่ออายุ" ไม่ใช่พอกไปเรื่อยๆ เช่น Support Arena (ADR-008 D5)
    /// sourceId ต้องไม่ใช่ ShieldSourceId.Unassigned — นั่นคือ id ของแหล่งที่ตั้งใจให้สแตกอิสระ
    /// </summary>
    public void RefreshShield(int sourceId, float amount, bool decays = true, float duration = -1f)
    {
        if (!IsServer) return;
        if (!ShieldStack.IsValidAmount(amount)) return;
        if (sourceId == ShieldSourceId.Unassigned)
        {
            Debug.LogWarning("[playermove] RefreshShield ถูกเรียกด้วย ShieldSourceId.Unassigned — " +
                              "ต้องใช้ id เฉพาะแหล่ง ไม่งั้นจะไป replace ชั้นของแหล่งอื่นที่ไม่ได้ตั้ง sourceId โดยไม่ตั้งใจ");
            return;
        }

        float effectiveDuration = duration > 0f ? duration : GetEffectiveShieldDuration();
        ShieldStack.Refresh(_shieldLayers, sourceId, amount, effectiveDuration, decays, Time.time);
        netShieldHP.Value = ShieldStack.Sum(_shieldLayers);
    }

    /// <summary>shieldDuration คูณ Duration stat ณ ขณะที่เรียก — ค่านี้ถูก "ตรึง" ไว้ในตัวชั้นเอง
    /// (ShieldLayer.duration) ตอนสร้าง/รีเฟรช ไม่ได้อ่านสดทุกเฟรมเหมือนโค้ดเดิมอีกต่อไป (ADR-008 D2)</summary>
    private float GetEffectiveShieldDuration()
    {
        float durationMult = _statManager?.GetDurationMultiplier() ?? 1f;
        return shieldDuration * durationMult;
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
