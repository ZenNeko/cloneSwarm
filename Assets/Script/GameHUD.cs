using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD ในเกมทั้งหมด — HP, EXP, Level, Timer, Announcement
///
/// ── Extensible Ability Slots (Q / E / R) ──
///   ค้นหา IHUDAbility ผ่าน GetComponentsInChildren อัตโนมัติ
///   → ไม่ต้อง hardcode character ใดๆ ลงใน GameHUD
///
/// ── Extensible Passive Bar ──
///   ค้นหา IHUDPassiveBar ผ่าน GetComponentInChildren อัตโนมัติ
///   → รองรับ ChargeManager (Riven), GunnerPassive, HunterPassive ฯลฯ
///
/// เพิ่ม character ใหม่: สร้าง ability/weapon script ที่ implement
///   IHUDAbility (ระบุ HUDSlotKey = "Q"/"E"/"R") หรือ IHUDPassiveBar
///   แล้ว GameHUD จะ detect และแสดงผลอัตโนมัติ
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("HP")]
    public Image           hpFill;
    public TextMeshProUGUI hpText;

    [Header("Shield Bar")]
    public Image           shieldFill;
    public GameObject      shieldBarRoot;

    [Header("EXP")]
    public Image           expFill;
    public TextMeshProUGUI levelText;

    [Header("Timer")]
    public TextMeshProUGUI timerLabel;

    [Header("Announcement")]
    public TextMeshProUGUI announcementLabel;
    public float           announcementDuration = 2.5f;

    [Header("Respawn Overlay")]
    public GameObject      respawnPanel;
    public TextMeshProUGUI respawnCountdownText;

    // ─────────────────────────────────────────────────────────────────────
    [Header("── Passive Bar ──")]
    public GameObject      chargeBarRoot;
    public Image           chargeBarFill;
    public TextMeshProUGUI chargeBarText;

    // ─────────────────────────────────────────────────────────────────────
    [Header("── Ability Slots ──")]
    public AbilitySlotUI qSlot;
    public AbilitySlotUI eSlot;

    [Header("Ability Slot Colors")]
    [Tooltip("สีเหล่านี้ลงที่ AbilitySlotUI.slotBg ไม่ใช่ที่ icon\nicon ถูกคงไว้สีเต็มเพื่อให้เห็นภาพสกิลชัดตลอด")]
    public Color abilityReadyColor    = Color.white;
    public Color abilityCooldownColor = new Color(0.35f, 0.35f, 0.35f);
    public Color abilityActiveColor   = new Color(1f, 0.85f, 0.1f);    // glow เมื่อ active mode

    // ─────────────────────────────────────────────────────────────────────
    [System.Serializable]
    public class AbilitySlotUI
    {
        public GameObject      root;
        [Tooltip("พื้นหลังของ slot — ตัวที่รับสี ready / cooldown / active\nว่างไว้ = fallback ไปย้อมที่ iconImage แบบเดิม (พร้อม warning)")]
        public Image           slotBg;
        public Image           iconImage;
        public Image           cooldownFill;
        public TextMeshProUGUI cooldownText;
        public TextMeshProUGUI keyHintText;
        [UnityEngine.Serialization.FormerlySerializedAs("exileGlow")]
        public GameObject      activeGlow;

        /// <summary>warning เรื่อง slotBg ว่าง ต้องดังครั้งเดียว ไม่ใช่ทุกเฟรม</summary>
        [System.NonSerialized] public bool warnedNoBg;

        /// <summary>ข้อความคูลดาวน์ที่เขียนไปล่าสุด — กัน rebuild mesh ซ้ำค่าเดิมทุกเฟรม</summary>
        [System.NonSerialized] public string lastCooldownText;
    }

    // ── Internal ──────────────────────────────────────────────────────────
    public static GameHUD Instance { get; private set; }

    private playermove   localPlayer;
    private float        localElapsed;

    // ── กันการเขียน .text ซ้ำค่าเดิมทุกเฟรม (ดู SetTextCached) ──────────
    private int    lastTimerSecond = -1;
    private string lastTimerText;
    private string lastChargeText;

    // Interface references — ค้นหาจาก player components
    private IHUDAbility    qAbility;
    private IHUDAbility    eAbility;
    private IHUDPassiveBar passiveBar;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// ล้าง <see cref="Instance"/> ตอนถูกทำลาย — **จำเป็น ไม่ใช่ความสะอาด**
    ///
    /// คนเรียกทั้งสิบจุด (BossController · BossTether · LimitCutAction · TelegraphZone ·
    /// FloorHazard) เขียน `GameHUD.Instance?.ShowAnnouncement(...)` ซึ่ง **กันไม่ได้** —
    /// `?.` เทียบ reference ตรงๆ ไม่ผ่าน `==` ที่ Unity override ไว้ object ที่ Destroy แล้ว
    /// จึงลอดผ่านไปเรียกเมธอด แล้วไปพังข้างในตอนแตะ announcementLabel
    ///
    /// ช่วงที่โดนคือระหว่าง HUD เก่าถูกทำลายกับ HUD ใหม่ Awake ซึ่ง Play Again
    /// วิ่งผ่านทุกครั้ง · ล้างที่นี่แล้ว `?.` ถึงจะกันได้จริงอย่างที่คนเขียนตั้งใจ
    /// (กติกาเดียวกับ `LobbyState.OnNetworkDespawn`)
    /// </summary>
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (announcementLabel) announcementLabel.gameObject.SetActive(false);

        if (shieldBarRoot) shieldBarRoot.SetActive(false);

        // ซ่อนของที่ยังไม่รู้ว่าจะมีไหม จนกว่าจะหาเจอจริง
        //
        // `WaitAndFindAbilities` รอ 0.5 วิแล้วสแกนได้ถึง 12 รอบ = แย่สุด 6.5 วิแรกของทุกรัน
        // ก่อนหน้านี้ช่วงนั้นโชว์ค่าที่ค้างมาจาก prefab — แถบ CHARGE ขึ้น "0" และช่อง Q/E
        // ขึ้นไอคอนของตัวละครอื่น ซึ่ง **ดูเหมือนใช้งานได้** ผู้เล่นจึงอ่านผิดโดยไม่รู้ตัว
        SetPassiveBarVisible(false);
        SetAbilitySlotVisible(qSlot, false);
        SetAbilitySlotVisible(eSlot, false);

        StartCoroutine(WaitAndSubscribeTimeline());
        StartCoroutine(WaitAndFindLocalPlayer());
    }

    void OnEnable()
    {
        playermove.OnLocalPlayerSpawned              += OnPlayerSpawned;
        SharedExperienceManager.OnSharedExpChanged   += UpdateExpBar;
        SharedExperienceManager.OnSharedLevelChanged += UpdateLevel;
        GameTimeline.OnMainBossTime                  += OnMainBossPhase;
        WinLoseUI.OnAnyResultTriggered               += HideRespawnOverlay;
    }

    void OnDisable()
    {
        playermove.OnLocalPlayerSpawned              -= OnPlayerSpawned;
        SharedExperienceManager.OnSharedExpChanged   -= UpdateExpBar;
        SharedExperienceManager.OnSharedLevelChanged -= UpdateLevel;
        GameTimeline.OnMainBossTime                  -= OnMainBossPhase;
        WinLoseUI.OnAnyResultTriggered               -= HideRespawnOverlay;

        if (localPlayer != null)
        {
            localPlayer.netHealth.OnValueChanged        -= OnHealthChanged;
            localPlayer.netMaxHealth.OnValueChanged     -= OnHealthChanged;
            localPlayer.isDead.OnValueChanged           -= OnDeadChanged;
            localPlayer.respawnCountdown.OnValueChanged -= OnCountdownChanged;
        }

        if (GameTimeline.Instance != null)
            GameTimeline.Instance.gameTime.OnValueChanged -= OnTimeChanged;
    }

    // ── Update ────────────────────────────────────────────────────────────
    void Update()
    {
        localElapsed += Time.deltaTime;
        UpdateTimerLabel(localElapsed);

        // Shield bar
        if (localPlayer != null && shieldBarRoot != null)
        {
            float shield    = localPlayer.netShieldHP.Value;
            float maxHP     = localPlayer.netMaxHealth.Value;
            bool  hasShield = shield > 0.5f;
            shieldBarRoot.SetActive(hasShield);
            if (hasShield && shieldFill)
                shieldFill.fillAmount = Mathf.Clamp01(shield / (maxHP * 0.5f));
        }

        // Ability slots — poll ทุก frame (ไม่ต้องใช้ events)
        UpdateAbilitySlot(qSlot, qAbility);
        UpdateAbilitySlot(eSlot, eAbility);

        // Passive bar
        UpdatePassiveBar();
    }

    // ── Timeline ──────────────────────────────────────────────────────────
    IEnumerator WaitAndSubscribeTimeline()
    {
        while (GameTimeline.Instance == null) yield return null;
        localElapsed = GameTimeline.Instance.gameTime.Value;
        GameTimeline.Instance.gameTime.OnValueChanged += OnTimeChanged;
    }

    void OnTimeChanged(float _, float v) => localElapsed = v;

    // ── Find Local Player ─────────────────────────────────────────────────
    IEnumerator WaitAndFindLocalPlayer()
    {
        while (localPlayer == null)
        {
            foreach (var pm in FindObjectsByType<playermove>(FindObjectsSortMode.None))
                if (pm.IsOwner) { OnPlayerSpawned(pm.transform); break; }
            yield return new WaitForSeconds(0.4f);
        }
        StartCoroutine(WaitAndFindAbilities());
    }

    // ── Ability Discovery (Interface-based) ───────────────────────────────
    IEnumerator WaitAndFindAbilities()
    {
        // รอ 0.5s ให้ OnNetworkSpawn + SpawnWeapon/SpawnAbility เสร็จก่อน
        yield return new WaitForSeconds(0.5f);

        // สแกนซ้ำสูงสุด 12 ครั้ง (6s) — apply ผลทันทีทุก iteration
        for (int attempt = 0; attempt < 12; attempt++)
        {
            foreach (var pwm in FindObjectsByType<PlayerWeaponManager>(FindObjectsSortMode.None))
            {
                if (!pwm.IsOwner) continue;
                ScanAbilities(pwm.gameObject);
            }

            // ครบทุกอย่างแล้ว → หยุดเลย
            bool hasAbility = qAbility != null || eAbility != null;
            if (hasAbility && passiveBar != null) break;

            yield return new WaitForSeconds(0.5f);
        }
    }

    void ScanAbilities(GameObject root)
    {
        // หา IHUDAbility ทุกตัว — assign ตาม HUDSlotKey
        bool slotAssigned = false;
        foreach (var ab in root.GetComponentsInChildren<IHUDAbility>(true))
        {
            switch (ab.HUDSlotKey)
            {
                case "Q": if (qAbility == null) { qAbility = ab; slotAssigned = true; } break;
                case "E": if (eAbility == null) { eAbility = ab; slotAssigned = true; } break;
            }
        }

        // apply เฉพาะตอนเพิ่งได้ ability มา — WaitAndFindAbilities สแกนซ้ำได้ถึง 12 รอบ
        // ถ้า apply ทุกรอบ warning ข้างล่างจะพ่นซ้ำ 12 ครั้ง
        if (slotAssigned) ApplyAbilitySlots();

        // หา IHUDPassiveBar ที่ IsActivePassive=true ก่อน (ถูก character)
        // fallback → ตัวแรกที่เจอ (กรณีไม่มีตัวไหน active)
        if (passiveBar == null)
        {
            foreach (var bar in root.GetComponentsInChildren<IHUDPassiveBar>(true))
            {
                if (bar.IsActivePassive) { passiveBar = bar; break; }
            }
            if (passiveBar == null)
                passiveBar = root.GetComponentInChildren<IHUDPassiveBar>(true);
        }
    }

    /// <summary>ค่าที่เปลี่ยนแค่ตอนได้ ability มา (key hint + icon)
    /// ต่างจาก UpdateAbilitySlot ที่ poll ทุกเฟรมเพื่อ cooldown / สี / glow</summary>
    void ApplyAbilitySlots()
    {
        // โชว์เฉพาะช่องที่หา ability เจอจริง — ตัวละครไม่ได้มีครบทุกช่องเสมอไป
        // และก่อนหาเจอก็ไม่ควรโชว์ของค้างจาก prefab
        SetAbilitySlotVisible(qSlot, qAbility != null);
        SetAbilitySlotVisible(eSlot, eAbility != null);

        ApplySlotIdentity(qSlot, qAbility);
        ApplySlotIdentity(eSlot, eAbility);
    }

    void ApplySlotIdentity(AbilitySlotUI slot, IHUDAbility ab)
    {
        if (slot == null || ab == null) return;

        if (slot.keyHintText != null) slot.keyHintText.text = ab.HUDKeyLabel;

        if (slot.iconImage != null)
        {
            // สีสถานะย้ายไปอยู่ที่ slotBg แล้ว icon จึงต้องเป็นสีเต็ม
            // (ของเดิมโค้ดย้อม icon ทุกเฟรม ค่าที่ค้างใน scene จึงเชื่อถือไม่ได้)
            if (slot.slotBg != null) slot.iconImage.color = Color.white;

            if (ab.HUDIcon != null)
            {
                slot.iconImage.sprite = ab.HUDIcon;
            }
            else
            {
                // ไม่เงียบ — ปล่อยผ่านเฉยๆ แปลว่า HUD ค้างรูป placeholder ของตัวละครอื่น
                // แล้วดูเหมือนทำงานปกติ (ดู handoff 2026-08-22 §7)
                Debug.LogWarning(
                    $"[GameHUD] slot {ab.HUDSlotKey} ({ab.GetType().Name}) ไม่มี icon — " +
                    "เติม AbilityData.icon ไม่งั้นจะค้างรูปที่ตั้งไว้ใน prefab");
            }
        }
    }

    /// <summary>ลงสีสถานะ (ready / cooldown / active) ที่พื้นหลังของ slot
    ///
    /// ถ้ายังไม่ได้ต่อ slotBg ใน Inspector จะถอยไปย้อม icon แบบเดิมแทน —
    /// ไม่ปล่อยให้เงียบ เพราะ "ไม่มีสีอะไรเลย" ดูเหมือน HUD พังมากกว่าดูเหมือนยังไม่ได้ต่อสาย</summary>
    void ApplySlotColor(AbilitySlotUI slot, Color color)
    {
        if (slot == null) return;

        if (slot.slotBg != null)
        {
            slot.slotBg.color = color;
            return;
        }

        if (slot.iconImage != null) slot.iconImage.color = color;

        if (!slot.warnedNoBg)
        {
            slot.warnedNoBg = true;
            Debug.LogWarning(
                "[GameHUD] ability slot ยังไม่ได้ต่อ slotBg — สีสถานะลงที่ icon ไปก่อน " +
                "ลาก child ชื่อ BG ของ Q_Slot / E_Slot ใส่ช่อง Slot Bg");
        }
    }

    // ── Ability Slot Update (Polling) ─────────────────────────────────────
    void UpdateAbilitySlot(AbilitySlotUI slot, IHUDAbility ab)
    {
        if (ab == null) return;

        if (ab.IsActiveMode && ab.ActiveMax > 0f)
        {
            // Active Mode: แสดง countdown drain จาก max → 0
            float norm = ab.ActiveRemaining / ab.ActiveMax;
            if (slot.cooldownFill  != null) slot.cooldownFill.fillAmount = norm;
            ApplySlotColor(slot, abilityActiveColor);
            if (slot.activeGlow    != null) slot.activeGlow.SetActive(true);
            SetTextCached(slot.cooldownText, ref slot.lastCooldownText,
                          ab.ActiveRemaining > 0.5f ? $"{ab.ActiveRemaining:F1}" : "");
        }
        else if (ab.IsOnCooldown && ab.CooldownMax > 0f)
        {
            // Cooldown: fill drain จาก 1 → 0
            float norm = ab.CooldownRemaining / ab.CooldownMax;
            if (slot.cooldownFill  != null) slot.cooldownFill.fillAmount = norm;
            ApplySlotColor(slot, abilityCooldownColor);
            if (slot.activeGlow    != null) slot.activeGlow.SetActive(false);
            SetTextCached(slot.cooldownText, ref slot.lastCooldownText,
                          ab.CooldownRemaining > 1f
                              ? $"{Mathf.CeilToInt(ab.CooldownRemaining)}"
                              : $"{ab.CooldownRemaining:F1}");
        }
        else
        {
            // Ready
            if (slot.cooldownFill  != null) slot.cooldownFill.fillAmount = 0f;
            ApplySlotColor(slot, abilityReadyColor);
            if (slot.activeGlow    != null) slot.activeGlow.SetActive(false);
            SetTextCached(slot.cooldownText, ref slot.lastCooldownText, "");
        }
    }

    // ── Passive Bar Update (Polling) ──────────────────────────────────────
    void UpdatePassiveBar()
    {
        // ไม่มี passive bar = ไม่มีอะไรให้โชว์ · ของเดิม return เฉยๆ แล้วปล่อยให้แถบ
        // ค้างค่าจาก prefab อยู่บนจอตลอดไปถ้าหาไม่เจอ
        if (passiveBar == null) { SetPassiveBarVisible(false); return; }
        SetPassiveBarVisible(true);

        float norm = passiveBar.NormalizedValue;
        if (chargeBarFill != null)
        {
            chargeBarFill.fillAmount = Mathf.Clamp01(norm);
            chargeBarFill.color      = passiveBar.IsTriggered
                ? passiveBar.TriggeredColor : passiveBar.BarColor;
        }
        SetTextCached(chargeBarText, ref lastChargeText, passiveBar.BarText);
    }

    /// <summary>
    /// ซ่อน/โชว์แถบ passive ทั้งอัน
    ///
    /// `chargeBarRoot` เคยเป็นฟิลด์ที่ **ไม่มีใครอ้างถึงเลยนอกจากบรรทัดประกาศ** และในซีน
    /// ก็ปล่อยว่างไว้ · ถ้ายังว่างอยู่ให้ถอยไปซ่อนชิ้นส่วนที่ต่อสายไว้แทน จะได้ไม่ต้องรอ
    /// ให้มีคนไปลากใส่ Inspector ก่อนถึงจะทำงานถูก (กติกาเดียวกับ fallback ของ slotBg)
    /// </summary>
    void SetPassiveBarVisible(bool v)
    {
        if (chargeBarRoot != null)
        {
            if (chargeBarRoot.activeSelf != v) chargeBarRoot.SetActive(v);
            return;
        }

        if (chargeBarFill != null && chargeBarFill.enabled != v) chargeBarFill.enabled = v;
        if (chargeBarText != null && chargeBarText.enabled != v) chargeBarText.enabled = v;
    }

    // ── HP ────────────────────────────────────────────────────────────────
    void OnPlayerSpawned(Transform t)
    {
        var pm = t.GetComponent<playermove>();
        if (pm == null) return;

        // มาถึงได้สองทาง — event `playermove.OnLocalPlayerSpawned` กับลูป poll ทุก 0.4 วิ
        // ใน WaitAndFindLocalPlayer · ถ้า foreach ของ coroutine วิ่งก่อน OnNetworkSpawn
        // ในเฟรมเดียวกันจะได้ subscribe ซ้ำ แล้ว OnDisable ถอดแค่ครั้งเดียว
        if (localPlayer == pm) return;

        localPlayer = pm;
        localPlayer.netHealth.OnValueChanged        += OnHealthChanged;
        localPlayer.netMaxHealth.OnValueChanged     += OnHealthChanged;
        localPlayer.isDead.OnValueChanged           += OnDeadChanged;
        localPlayer.respawnCountdown.OnValueChanged += OnCountdownChanged;
        RefreshHP();
        if (respawnPanel) respawnPanel.SetActive(false);
    }

    void OnHealthChanged(float _, float __) => RefreshHP();

    void OnDeadChanged(bool _, bool isDead)
    {
        // ถ้า WinLoseUI กำลังแสดง → ไม่ต้องโชว์ respawn overlay (เกมจบแล้ว)
        if (WinLoseUI.IsShowing) { if (respawnPanel) respawnPanel.SetActive(false); return; }
        if (respawnPanel) respawnPanel.SetActive(isDead);
    }

    void OnCountdownChanged(float _, float v)
    {
        if (WinLoseUI.IsShowing) return;   // ไม่ update countdown หลังเกมจบ
        if (respawnCountdownText)
            respawnCountdownText.text = v > 0 ? $"RESPAWN IN: {Mathf.CeilToInt(v)}" : "RESPAWNING...";
    }

    /// <summary>เรียกจาก WinLoseUI.OnAnyResultTriggered — ซ่อน respawn overlay ทันที</summary>
    void HideRespawnOverlay()
    {
        if (respawnPanel) respawnPanel.SetActive(false);
    }

    void RefreshHP()
    {
        if (localPlayer == null) return;
        float cur = localPlayer.netHealth.Value;
        float max = localPlayer.netMaxHealth.Value;
        if (hpFill) hpFill.fillAmount = Mathf.Clamp01(max > 0 ? cur / max : 0f);
        if (hpText) hpText.text       = $"{Mathf.CeilToInt(cur)} / {Mathf.CeilToInt(max)}";
    }

    // ── EXP ───────────────────────────────────────────────────────────────
    void UpdateExpBar(float current, float toNext)
    {
        if (expFill) expFill.fillAmount = toNext > 0 ? Mathf.Clamp01(current / toNext) : 0f;
    }

    void UpdateLevel(int level)
    {
        if (levelText) levelText.text = $"Lv {level}";
    }

    // ── Timer ─────────────────────────────────────────────────────────────
    void UpdateTimerLabel(float elapsed)
    {
        if (timerLabel == null) return;

        // นาฬิกาเดินทุกเฟรมแต่ตัวเลขเปลี่ยนวินาทีละครั้ง — เทียบวินาทีก่อน
        // ไม่งั้นได้สตริงใหม่ + rebuild mesh ฟรีๆ 60 ครั้งต่อวินาที
        int total = Mathf.FloorToInt(elapsed);
        if (total == lastTimerSecond) return;
        lastTimerSecond = total;

        SetTextCached(timerLabel, ref lastTimerText, $"{total / 60:00}:{total % 60:00}");
    }

    // ── Announcement ──────────────────────────────────────────────────────
    [Tooltip("สีแบนเนอร์ตอนบอสใหญ่มา — เดิมเป็น Color.red (#FF0000) ซึ่งอยู่นอกชุดสี\n" +
             "ชุด P3R ใช้ #D82020 · แดงสดกินตาเกินไปบนจอที่มีศัตรูเต็มอยู่แล้ว")]
    public Color mainBossAnnouncementColor = new Color32(0xD8, 0x20, 0x20, 0xFF);

    void OnMainBossPhase() => ShowAnnouncement("MAIN BOSS!", mainBossAnnouncementColor);

    public void ShowAnnouncement(string text, Color color)
    {
        if (announcementLabel == null) return;
        StopCoroutine(nameof(FadeAnnouncement));
        StartCoroutine(nameof(FadeAnnouncement), (text, color));
    }

    IEnumerator FadeAnnouncement(object args)
    {
        var (text, color) = ((string, Color))args;
        announcementLabel.text  = text;
        announcementLabel.color = new Color(color.r, color.g, color.b, 1f);
        announcementLabel.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(announcementDuration * 0.6f);
        float t = 0f, fade = announcementDuration * 0.4f;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            var c = announcementLabel.color;
            c.a = Mathf.Lerp(1f, 0f, t / fade);
            announcementLabel.color = c;
            yield return null;
        }
        announcementLabel.gameObject.SetActive(false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    void SetAbilitySlotVisible(AbilitySlotUI slot, bool v)
    {
        if (slot?.root == null) return;
        if (slot.root.activeSelf != v) slot.root.SetActive(v);
    }

    /// <summary>
    /// เขียน `.text` เฉพาะตอนค่าต่างจริง
    ///
    /// การเขียน `TMP_Text.text` บังคับสร้าง mesh ใหม่ทุกครั้ง **แม้ค่าจะเท่าเดิม** ·
    /// จอนี้เขียนนาฬิกากับตัวเลขคูลดาวน์ทุกเฟรมทั้งที่เปลี่ยนวินาทีละครั้ง
    /// = สร้างสตริงใหม่กับ rebuild mesh ~60 ครั้ง/วินาทีเปล่าๆ บนจอที่ต้องลื่นที่สุด
    /// </summary>
    static void SetTextCached(TextMeshProUGUI label, ref string cache, string value)
    {
        if (label == null || cache == value) return;
        cache      = value;
        label.text = value;
    }
}
