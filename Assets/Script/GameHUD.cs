using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD ในเกมทั้งหมด — HP, EXP, Level, Timer, Announcement
/// + Charge Bar (Riven) + Ability Slots Q/E
/// (รวม ChargeBarUI และ AbilityHUDUI เข้ามาแล้ว)
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("HP")]
    public Image           hpFill;
    public TextMeshProUGUI hpText;             // "85 / 100"

    [Header("Shield Bar (Riven passive)")]
    [Tooltip("Image ข้างบน HP bar แสดง shield — ซ่อนถ้าไม่มี shield")]
    public Image           shieldFill;
    public GameObject      shieldBarRoot;      // parent ที่ SetActive

    [Header("EXP")]
    public Image           expFill;
    public TextMeshProUGUI levelText;          // "Lv 5"

    [Header("Timer")]
    public TextMeshProUGUI timerLabel;         // "05:32"

    [Header("Announcement")]
    public TextMeshProUGUI announcementLabel;
    public float           announcementDuration = 2.5f;

    [Header("Respawn Overlay")]
    public GameObject      respawnPanel;
    public TextMeshProUGUI respawnCountdownText;

    // ─────────────────────────────────────────────────────────────────────
    [Header("── Charge Bar (ซ่อนอัตโนมัติถ้าตัวละครไม่ใช่ Riven) ──")]
    public GameObject      chargeBarRoot;      // parent — ซ่อน/แสดงทั้งก้อน
    public Image           chargeBarFill;      // Image Type = Filled, Horizontal
    public TextMeshProUGUI chargeBarText;      // แสดง "READY!" หรือ "0–100"

    [Header("Charge Bar Colors")]
    public Color chargeNormalColor = new Color(0.2f, 0.8f, 1f);
    public Color chargeFillColor   = new Color(1f, 0.9f, 0f);
    public Color chargeExileColor  = new Color(1f, 0.55f, 0.1f);

    // ─────────────────────────────────────────────────────────────────────
    [Header("── Ability Slots Q / E (ซ่อนถ้าไม่ใช่ Riven) ──")]
    public AbilitySlotUI qSlot;
    public AbilitySlotUI eSlot;

    [Header("Ability Slot Colors")]
    public Color abilityReadyColor    = Color.white;
    public Color abilityCooldownColor = new Color(0.35f, 0.35f, 0.35f);
    public Color abilityExileColor    = new Color(1f, 0.6f, 0.1f);

    // ─────────────────────────────────────────────────────────────────────
    [System.Serializable]
    public class AbilitySlotUI
    {
        public GameObject      root;           // ซ่อน/แสดงทั้ง slot
        public Image           iconImage;
        public Image           cooldownFill;   // Image Type=Filled, Radial360, fillOrigin=Top
        public TextMeshProUGUI cooldownText;   // วินาทีที่เหลือ / "!"
        public TextMeshProUGUI keyHintText;    // "Q" / "E"
        public GameObject      exileGlow;      // optional
    }

    // ── Internal ──────────────────────────────────────────────────────────
    private playermove            localPlayer;
    private float                 localElapsed;

    // Riven-specific
    private ChargeManager         chargeManager;
    private ValorWeapon           valorWeapon;
    private BladeOfExileWeapon    exileWeapon;
    private bool                  rivenEventsSubscribed;
    private bool                  wasExileActive;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Start()
    {
        if (announcementLabel) announcementLabel.gameObject.SetActive(false);

        // ซ่อน Riven UI จนกว่าจะยืนยัน
        SetChargeBarVisible(false);
        SetAbilitySlotVisible(qSlot, false);
        SetAbilitySlotVisible(eSlot, false);
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

        UnsubscribeRivenEvents();
    }

    // ── Update ────────────────────────────────────────────────────────────
    void Update()
    {
        localElapsed += Time.deltaTime;
        UpdateTimerLabel(localElapsed);

        // Shield bar update (Server-side value → poll ทุก frame)
        if (localPlayer != null && shieldBarRoot != null)
        {
            float shield = localPlayer.shieldHP;
            float maxHP  = localPlayer.netMaxHealth.Value;
            bool  hasShield = shield > 0.5f;
            shieldBarRoot.SetActive(hasShield);
            if (hasShield && shieldFill)
                shieldFill.fillAmount = Mathf.Clamp01(shield / (maxHP * 0.5f)); // แสดง relative to 50% max HP
        }

        // Ability cooldown text update (ต้องการ smooth countdown)
        UpdateAbilityCooldown(qSlot, valorWeapon?.IsOnCooldown ?? false,
                              valorWeapon?.CooldownRemaining ?? 0f,
                              valorWeapon?.CooldownMax       ?? 8f);
        UpdateAbilityCooldownExile();
    }

    // ── Timeline ──────────────────────────────────────────────────────────
    IEnumerator WaitAndSubscribeTimeline()
    {
        while (GameTimeline.Instance == null) yield return null;
        localElapsed = GameTimeline.Instance.gameTime.Value;
        GameTimeline.Instance.gameTime.OnValueChanged += OnTimeChanged;
    }

    void OnTimeChanged(float _, float v) => localElapsed = v;

    // ── Find Local Player (+ Riven check) ─────────────────────────────────
    IEnumerator WaitAndFindLocalPlayer()
    {
        while (localPlayer == null)
        {
            foreach (var pm in FindObjectsByType<playermove>(FindObjectsSortMode.None))
            {
                if (pm.IsOwner) { OnPlayerSpawned(pm.transform); break; }
            }
            yield return new WaitForSeconds(0.4f);
        }

        // พยายามหา Riven weapons ซ้ำจนกว่าจะเจอ (weapon อาจ spawn ช้า)
        StartCoroutine(WaitAndFindRivenAbilities());
    }

    IEnumerator WaitAndFindRivenAbilities()
    {
        // รอนานสุด 5 วินาที
        float timeout = 5f;
        while (timeout > 0f && (chargeManager == null || valorWeapon == null || exileWeapon == null))
        {
            foreach (var pwm in FindObjectsByType<PlayerWeaponManager>(FindObjectsSortMode.None))
            {
                if (!pwm.IsOwner) continue;
                if (chargeManager == null) chargeManager = pwm.GetComponent<ChargeManager>();
                if (valorWeapon   == null) valorWeapon   = pwm.GetComponentInChildren<ValorWeapon>();
                if (exileWeapon   == null) exileWeapon   = pwm.GetComponentInChildren<BladeOfExileWeapon>();
            }
            timeout -= 0.4f;
            yield return new WaitForSeconds(0.4f);
        }

        bool isRiven = chargeManager != null &&
                       pwm_HasBunnyHop();

        if (isRiven)
        {
            SubscribeRivenEvents();

            SetChargeBarVisible(true);
            if (chargeBarFill)  chargeBarFill.fillAmount = 0f;
            if (chargeBarFill)  chargeBarFill.color      = chargeNormalColor;
            if (chargeBarText)  chargeBarText.text        = "0";

            SetAbilitySlotVisible(qSlot, valorWeapon != null);
            SetAbilitySlotVisible(eSlot, exileWeapon != null);

            if (qSlot.keyHintText != null)
                qSlot.keyHintText.text = valorWeapon?.activateKey.ToString() ?? "Q";
            if (eSlot.keyHintText != null)
                eSlot.keyHintText.text = exileWeapon?.activateKey.ToString() ?? "E";
        }
    }

    bool pwm_HasBunnyHop()
    {
        foreach (var pwm in FindObjectsByType<PlayerWeaponManager>(FindObjectsSortMode.None))
            if (pwm.IsOwner && pwm.GetComponentInChildren<BunnyHopWeapon>() != null) return true;
        return false;
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

    // ── Riven: Subscribe / Unsubscribe ────────────────────────────────────
    void SubscribeRivenEvents()
    {
        if (rivenEventsSubscribed) return;
        rivenEventsSubscribed = true;

        ChargeManager.OnChargeChanged              += OnChargeChanged;
        ValorWeapon.OnCooldownChanged              += OnValorCooldown;
        ValorWeapon.OnActivated                    += OnValorActivated;
        BladeOfExileWeapon.OnCooldownChanged       += OnExileCooldown;
        BladeOfExileWeapon.OnExileStateChanged     += OnExileState;
    }

    void UnsubscribeRivenEvents()
    {
        if (!rivenEventsSubscribed) return;
        rivenEventsSubscribed = false;

        ChargeManager.OnChargeChanged              -= OnChargeChanged;
        ValorWeapon.OnCooldownChanged              -= OnValorCooldown;
        ValorWeapon.OnActivated                    -= OnValorActivated;
        BladeOfExileWeapon.OnCooldownChanged       -= OnExileCooldown;
        BladeOfExileWeapon.OnExileStateChanged     -= OnExileState;
    }

    // ── Riven: Charge Bar ─────────────────────────────────────────────────
    void OnChargeChanged(float normalized)
    {
        if (chargeBarFill == null) return;

        bool exile = chargeManager != null && chargeManager.IsExileActive;

        chargeBarFill.fillAmount = normalized;
        chargeBarFill.color      = normalized >= 0.99f ? chargeFillColor
                                 : exile                ? chargeExileColor
                                                        : chargeNormalColor;
        if (chargeBarText != null)
            chargeBarText.text = normalized >= 0.99f ? "READY!"
                               : $"{Mathf.RoundToInt(normalized * 100)}";
    }

    // ── Riven: Exile (charge bar color change) ───────────────────────────
    void OnExileState(BladeOfExileWeapon src, bool active)
    {
        if (src != exileWeapon) return;

        // อัปเดตสี Charge Bar
        if (chargeBarFill && chargeBarFill.fillAmount < 0.99f)
            chargeBarFill.color = active ? chargeExileColor : chargeNormalColor;

        // Exile glow บน E slot
        if (eSlot.exileGlow   != null) eSlot.exileGlow.SetActive(active);
        if (eSlot.iconImage   != null) eSlot.iconImage.color = active ? abilityExileColor : abilityReadyColor;

        wasExileActive = active;

        if (!active)
        {
            // Exile หมด → เข้า cooldown ทันที
            SetAbilityCD(eSlot, 1f, exileWeapon?.CooldownMax ?? 80f);
        }
    }

    // ── Riven: Valor cooldown ─────────────────────────────────────────────
    void OnValorCooldown(ValorWeapon src, float normalized)
    {
        if (src != valorWeapon) return;
        SetAbilityCD(qSlot, normalized, valorWeapon.CooldownMax);
    }

    void OnValorActivated(ValorWeapon src)
    {
        if (src != valorWeapon) return;
        // Flash effect สามารถเพิ่มได้ทีหลัง
    }

    // ── Riven: Exile cooldown ─────────────────────────────────────────────
    void OnExileCooldown(BladeOfExileWeapon src, float normalized)
    {
        if (src != exileWeapon) return;
        SetAbilityCD(eSlot, normalized, exileWeapon.CooldownMax);
    }

    // ── Ability slot helpers ──────────────────────────────────────────────
    void SetAbilityCD(AbilitySlotUI slot, float normalized, float cdMax)
    {
        bool onCD = normalized > 0.001f;

        if (slot.cooldownFill  != null) slot.cooldownFill.fillAmount = onCD ? normalized : 0f;
        if (slot.iconImage     != null) slot.iconImage.color = onCD ? abilityCooldownColor : abilityReadyColor;

        if (slot.cooldownText != null)
        {
            if (!onCD) { slot.cooldownText.text = ""; return; }
            float remain = normalized * cdMax;
            slot.cooldownText.text = remain > 1f ? $"{Mathf.CeilToInt(remain)}" : $"{remain:F1}";
        }
    }

    void UpdateAbilityCooldown(AbilitySlotUI slot, bool onCD, float remaining, float cdMax)
    {
        // smooth text update ทุก frame (normalized คำนวณใหม่จาก remaining)
        if (!onCD || cdMax <= 0f) return;
        float norm = remaining / cdMax;
        if (slot.cooldownFill != null) slot.cooldownFill.fillAmount = norm;
        if (slot.cooldownText != null)
            slot.cooldownText.text = remaining > 1f ? $"{Mathf.CeilToInt(remaining)}" : $"{remaining:F1}";
    }

    void UpdateAbilityCooldownExile()
    {
        if (exileWeapon == null) return;

        if (exileWeapon.IsExileActive)
        {
            // แสดง exile duration ที่เหลือ
            float norm = exileWeapon.exileDuration > 0f
                       ? exileWeapon.ExileRemaining / exileWeapon.exileDuration
                       : 0f;
            if (eSlot.cooldownFill != null) eSlot.cooldownFill.fillAmount = norm;
            if (eSlot.cooldownText != null)
                eSlot.cooldownText.text = $"{exileWeapon.ExileRemaining:F0}";
        }
        else if (exileWeapon.IsOnCooldown && exileWeapon.CooldownMax > 0f)
        {
            UpdateAbilityCooldown(eSlot, true, exileWeapon.CooldownRemaining, exileWeapon.CooldownMax);
        }
    }

    void SetChargeBarVisible(bool v)
    {
        if (chargeBarRoot) chargeBarRoot.SetActive(v);
    }

    void SetAbilitySlotVisible(AbilitySlotUI slot, bool v)
    {
        if (slot.root != null) slot.root.SetActive(v);
    }
}
