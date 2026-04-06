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
    public Color abilityReadyColor    = Color.white;
    public Color abilityCooldownColor = new Color(0.35f, 0.35f, 0.35f);
    public Color abilityActiveColor   = new Color(1f, 0.85f, 0.1f);    // glow เมื่อ active mode

    // ─────────────────────────────────────────────────────────────────────
    [System.Serializable]
    public class AbilitySlotUI
    {
        public GameObject      root;
        public Image           iconImage;
        public Image           cooldownFill;
        public TextMeshProUGUI cooldownText;
        public TextMeshProUGUI keyHintText;
        [UnityEngine.Serialization.FormerlySerializedAs("exileGlow")]
        public GameObject      activeGlow;
    }

    // ── Internal ──────────────────────────────────────────────────────────
    private playermove   localPlayer;
    private float        localElapsed;

    // Interface references — ค้นหาจาก player components
    private IHUDAbility    qAbility;
    private IHUDAbility    eAbility;
    private IHUDPassiveBar passiveBar;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Start()
    {
        if (announcementLabel) announcementLabel.gameObject.SetActive(false);

        if (shieldBarRoot) shieldBarRoot.SetActive(false);

        StartCoroutine(WaitAndSubscribeTimeline());
        StartCoroutine(WaitAndFindLocalPlayer());
    }

    void OnEnable()
    {
        playermove.OnLocalPlayerSpawned              += OnPlayerSpawned;
        SharedExperienceManager.OnSharedExpChanged   += UpdateExpBar;
        SharedExperienceManager.OnSharedLevelChanged += UpdateLevel;
        GameTimeline.OnMainBossTime                  += OnMainBossPhase;
    }

    void OnDisable()
    {
        playermove.OnLocalPlayerSpawned              -= OnPlayerSpawned;
        SharedExperienceManager.OnSharedExpChanged   -= UpdateExpBar;
        SharedExperienceManager.OnSharedLevelChanged -= UpdateLevel;
        GameTimeline.OnMainBossTime                  -= OnMainBossPhase;

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
        foreach (var ab in root.GetComponentsInChildren<IHUDAbility>(true))
        {
            switch (ab.HUDSlotKey)
            {
                case "Q": if (qAbility == null) qAbility = ab; break;
                case "E": if (eAbility == null) eAbility = ab; break;
            }
        }

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

    void ApplyAbilitySlots()
    {
        // slot และ passive bar แสดงตลอด — ตัวละครทุกตัวมีครบ
        if (qSlot.keyHintText != null && qAbility != null) qSlot.keyHintText.text = qAbility.HUDKeyLabel;
        if (eSlot.keyHintText != null && eAbility != null) eSlot.keyHintText.text = eAbility.HUDKeyLabel;
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
            if (slot.iconImage     != null) slot.iconImage.color = abilityActiveColor;
            if (slot.activeGlow    != null) slot.activeGlow.SetActive(true);
            if (slot.cooldownText  != null)
                slot.cooldownText.text = ab.ActiveRemaining > 0.5f
                    ? $"{ab.ActiveRemaining:F1}" : "";
        }
        else if (ab.IsOnCooldown && ab.CooldownMax > 0f)
        {
            // Cooldown: fill drain จาก 1 → 0
            float norm = ab.CooldownRemaining / ab.CooldownMax;
            if (slot.cooldownFill  != null) slot.cooldownFill.fillAmount = norm;
            if (slot.iconImage     != null) slot.iconImage.color = abilityCooldownColor;
            if (slot.activeGlow    != null) slot.activeGlow.SetActive(false);
            if (slot.cooldownText  != null)
                slot.cooldownText.text = ab.CooldownRemaining > 1f
                    ? $"{Mathf.CeilToInt(ab.CooldownRemaining)}" : $"{ab.CooldownRemaining:F1}";
        }
        else
        {
            // Ready
            if (slot.cooldownFill  != null) slot.cooldownFill.fillAmount = 0f;
            if (slot.iconImage     != null) slot.iconImage.color = abilityReadyColor;
            if (slot.activeGlow    != null) slot.activeGlow.SetActive(false);
            if (slot.cooldownText  != null) slot.cooldownText.text = "";
        }
    }

    // ── Passive Bar Update (Polling) ──────────────────────────────────────
    void UpdatePassiveBar()
    {
        if (passiveBar == null) return;

        float norm = passiveBar.NormalizedValue;
        if (chargeBarFill != null)
        {
            chargeBarFill.fillAmount = Mathf.Clamp01(norm);
            chargeBarFill.color      = passiveBar.IsTriggered
                ? passiveBar.TriggeredColor : passiveBar.BarColor;
        }
        if (chargeBarText != null)
            chargeBarText.text = passiveBar.BarText;
    }

    // ── HP ────────────────────────────────────────────────────────────────
    void OnPlayerSpawned(Transform t)
    {
        localPlayer = t.GetComponent<playermove>();
        if (localPlayer == null) return;
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
        if (respawnPanel) respawnPanel.SetActive(isDead);
    }

    void OnCountdownChanged(float _, float v)
    {
        if (respawnCountdownText)
            respawnCountdownText.text = v > 0 ? $"RESPAWN IN: {Mathf.CeilToInt(v)}" : "RESPAWNING...";
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
        int m = Mathf.FloorToInt(elapsed / 60f);
        int s = Mathf.FloorToInt(elapsed % 60f);
        timerLabel.text = $"{m:00}:{s:00}";
    }

    // ── Announcement ──────────────────────────────────────────────────────
    void OnMainBossPhase() => ShowAnnouncement("MAIN BOSS!", Color.red);

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
    void SetAbilitySlotVisible(AbilitySlotUI slot, bool v) { if (slot?.root) slot.root.SetActive(v); }
}
