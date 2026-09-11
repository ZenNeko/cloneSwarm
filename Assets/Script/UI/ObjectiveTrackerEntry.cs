using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// แถวเดียวใน ObjectiveTrackerHUD — ผูกกับ ZoneObjective หนึ่งอัน
/// บอกว่า "ต้องทำอะไร" · คืบหน้าแค่ไหน · เหลือเวลาเท่าไร
///
/// สร้างเป็น Prefab แล้ว assign ให้ ObjectiveTrackerHUD.entryPrefab
/// โครงเดียวกับ MiniBossBarEntry ที่ BossHUDUI ใช้อยู่
///
/// **Prefab hierarchy แนะนำ:**
///   ObjectiveTrackerEntry   (มี ObjectiveTrackerEntry.cs + LayoutElement)
///   ├── HeaderText    TextMeshProUGUI  — "DELIVER" / "SURVIVE" / "ACTIVATING"
///   ├── TimerText     TextMeshProUGUI  — "0:42"
///   ├── TaskText      TextMeshProUGUI  — ต้องทำอะไร
///   ├── ProgressText  TextMeshProUGUI  — "3 / 5"
///   ├── BarBg         Image
///   └── BarFill       Image            — Filled หรือ anchor ซ้ายก็ได้
///
/// ปิด Raycast Target ทุกชิ้น — สกิลผูกกับคลิกซ้าย/ขวาแล้ว (ดู AbilityInput)
/// แผงที่กินคลิกจะทำให้สกิลกดไม่ติดตรงมุมนั้น · ObjectiveTrackerHUD เตือนให้ถ้าลืม
/// </summary>
public class ObjectiveTrackerEntry : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI headerText;
    public TextMeshProUGUI taskText;
    public TextMeshProUGUI progressText;
    public TextMeshProUGUI timerText;
    [Tooltip("Image ธรรมดา anchor ซ้าย (ขยับ anchorMax.x) หรือ Image Type = Filled ก็ได้")]
    public Image           barFill;

    [Header("สี")]
    public Color activatingColor  = new(0.20f, 0.85f, 1.00f);
    public Color questColor       = new(1.00f, 0.80f, 0.20f);
    public Color completeColor    = new(0.35f, 0.95f, 0.40f);
    [Tooltip("ใช้ทั้งตอนเวลาใกล้หมดและตอน objective หมดอายุ")]
    public Color urgentColor      = new(1.00f, 0.35f, 0.25f);
    public Color timerNormalColor = Color.white;

    [Header("พฤติกรรม")]
    [Tooltip("เหลือน้อยกว่านี้ (วินาที) ตัวนับเปลี่ยนเป็นสี urgent")]
    public float urgentSeconds = 10f;
    [Tooltip("กะพริบตัวนับตอนเวลาใกล้หมด")]
    public bool  blinkWhenUrgent = true;
    [Tooltip("ค้างแถวไว้กี่วินาทีหลัง complete/expire ก่อนถูกลบ")]
    public float hideDelay = 4f;
    [Tooltip("ข้อความ Phase 1 — มีคนยืนในโซนแล้ว")]
    public string activateHint      = "Stand in the zone to activate";
    [Tooltip("ข้อความ Phase 1 — ยังไม่มีใครยืนในโซน")]
    public string activateHintEmpty = "Stand in the zone to activate  (no one inside)";

    // ══════════════════════════════════════════════════════════════════════
    ZoneObjective _objective;
    float         _removeAt = -1f;

    /// <summary>true = จบแล้วและครบเวลาค้างจอ — ObjectiveTrackerHUD เก็บกวาดได้</summary>
    public bool ReadyToRemove => _removeAt > 0f && Time.unscaledTime >= _removeAt;

    /// <summary>objective ที่แถวนี้ผูกอยู่ — null เมื่อจบแล้วกำลังค้างจอ</summary>
    public ZoneObjective Objective => _objective;

    // ค่าที่วาดไปแล้ว — เขียน UI เฉพาะตอนเปลี่ยนจริง แบบเดียวกับ QuestCarryHUD
    // ตัวนับเปลี่ยนวินาทีละครั้ง ถ้าประกอบสตริงใหม่ทุกเฟรมก็ alloc ทิ้งฟรี 60 ครั้ง/วินาที
    string _lastHeader   = null;
    string _lastTask     = null;
    string _lastProgress = null;
    int    _lastTimerSec = int.MinValue;
    float  _lastFill     = -1f;
    bool   _wasUrgent;

    // ── Init ──────────────────────────────────────────────────────────────
    /// <summary>เรียกจาก ObjectiveTrackerHUD หลัง Instantiate</summary>
    public void Initialize(ZoneObjective objective)
    {
        _objective = objective;
        _removeAt  = -1f;
        Refresh();
    }

    /// <summary>objective จบแล้ว — โชว์ผลค้างไว้ hideDelay วินาทีก่อนถูกลบ</summary>
    public void MarkFinished(bool completed)
    {
        _objective = null;
        _removeAt  = Time.unscaledTime + hideDelay;   // unscaled — ต้องอ่านได้ตอนเกมหยุด

        Color c = completed ? completeColor : urgentColor;
        SetHeader(completed ? "COMPLETE" : "EXPIRED", c);
        SetTask("");
        SetProgress("");
        SetTimerText("");
        if (barFill != null) barFill.color = c;
    }

    // ── Per-frame ─────────────────────────────────────────────────────────
    void Update()
    {
        if (_objective == null) return;   // จบแล้ว กำลังค้างจอ

        // objective หายไปโดยไม่ผ่าน event (เปลี่ยนซีน / host หลุด) — ปิดตัวเอง
        if (!_objective.isActiveAndEnabled) { MarkFinished(false); return; }

        Refresh();
    }

    void Refresh()
    {
        if (_objective == null) return;

        bool  activating = _objective.CurrentPhase == ZoneObjective.Phase.Activating;
        Color accent     = activating ? activatingColor : questColor;

        // หัวแถวเป็นชื่อประเภท quest — มีหลาย objective พร้อมกันจะได้แยกออกจากกัน
        // ถ้าเขียน "OBJECTIVE" เหมือนกันหมดสามแถว จะอ่านไม่รู้ว่าอันไหนคืออันไหน
        SetHeader(activating ? "ACTIVATING" : QuestLabel(_objective), accent);

        // ── ต้องทำอะไร ────────────────────────────────────────────────────
        if (activating)
        {
            SetTask(_objective.playersInZone.Value > 0 ? activateHint : activateHintEmpty);
        }
        else
        {
            // ข้อความชุดเดียวกับ announcement — แหล่งความจริงเดียว ไม่มีสองชุดให้ลืมอัปเดต
            SetTask(_objective.HasActiveQuest ? _objective.GetQuestAnnouncement() : "");
        }

        // ── ความคืบหน้า ───────────────────────────────────────────────────
        float fill;
        if (activating)
        {
            fill = _objective.progress.Value;
            SetProgress($"{Mathf.RoundToInt(fill * 100f)}%   in zone {_objective.playersInZone.Value}");
        }
        else
        {
            int have = _objective.deliveredCount.Value;
            int need = _objective.requiredCount.Value;
            fill = need > 0 ? Mathf.Clamp01((float)have / need) : _objective.progress.Value;

            // Survive / SealTheRift นับเป็นวินาที ไม่ใช่จำนวนชิ้น — ใส่หน่วยให้ตรงความหมาย
            bool timeBased = _objective.ActiveQuestType == ZoneObjective.QuestType.Survive
                          || _objective.ActiveQuestType == ZoneObjective.QuestType.SealTheRift;

            SetProgress(need > 0
                ? (timeBased ? $"{have}s / {need}s" : $"{have} / {need}")
                : "");
        }
        SetFill(fill, accent);

        // ── เหลือเวลาเท่าไร ──────────────────────────────────────────────
        float left = _objective.SecondsRemaining;
        if (left < 0f)
        {
            // phase นี้ไม่มีเวลาจำกัด — เคลียร์ครั้งเดียว ไม่ใช่เขียน "" ทับทุกเฟรม
            if (_lastTimerSec != int.MinValue)
            {
                _lastTimerSec = int.MinValue;
                SetTimerText("");
            }
            return;
        }

        int total = Mathf.FloorToInt(left);
        if (total != _lastTimerSec)            // ข้อความเปลี่ยนวินาทีละครั้งเท่านั้น
        {
            _lastTimerSec = total;
            SetTimerText($"{total / 60}:{total % 60:00}");
        }

        // สีต้องอัปเดตต่อเฟรมเฉพาะตอนกะพริบ — นอกนั้นเขียนแค่ตอนสลับโหมด
        bool urgent = left <= urgentSeconds;
        if (urgent && blinkWhenUrgent)
        {
            // unscaledTime — จอเลือกการ์ด level up หยุด timeScale ไว้
            float a = Mathf.PingPong(Time.unscaledTime * 4f, 1f) * 0.5f + 0.5f;
            SetTimerColor(new Color(urgentColor.r, urgentColor.g, urgentColor.b, a));
        }
        else if (urgent != _wasUrgent)
        {
            SetTimerColor(urgent ? urgentColor : timerNormalColor);
        }
        _wasUrgent = urgent;
    }

    /// <summary>ชื่อสั้นของประเภท quest สำหรับหัวแถว</summary>
    static string QuestLabel(ZoneObjective z)
    {
        if (!z.HasActiveQuest) return "OBJECTIVE";
        return z.ActiveQuestType switch
        {
            ZoneObjective.QuestType.FetchAndDeliver => "DELIVER",
            ZoneObjective.QuestType.Survive         => "SURVIVE",
            ZoneObjective.QuestType.DestroyObjects  => "DESTROY",
            ZoneObjective.QuestType.KillInZone      => "PURGE",
            ZoneObjective.QuestType.SealTheRift     => "SEAL",
            _                                       => "OBJECTIVE",
        };
    }

    // ── Setters — เขียนเฉพาะตอนค่าเปลี่ยน ────────────────────────────────
    void SetHeader(string s, Color c)
    {
        if (headerText == null) return;
        if (_lastHeader != s) { _lastHeader = s; headerText.text = s; }
        if (headerText.color != c) headerText.color = c;
    }

    void SetTask(string s)
    {
        if (taskText == null || _lastTask == s) return;
        _lastTask = s;
        taskText.text = s;
    }

    void SetProgress(string s)
    {
        if (progressText == null || _lastProgress == s) return;
        _lastProgress = s;
        progressText.text = s;
    }

    void SetTimerText(string s)  { if (timerText != null) timerText.text  = s; }
    void SetTimerColor(Color c)  { if (timerText != null) timerText.color = c; }

    void SetFill(float fill01, Color color)
    {
        if (barFill == null) return;

        fill01 = Mathf.Clamp01(fill01);
        if (barFill.color != color) barFill.color = color;
        if (Mathf.Approximately(fill01, _lastFill)) return;
        _lastFill = fill01;

        // รองรับทั้งสองวิธีที่คนจัด UI นิยมใช้ — ไม่ต้องบังคับว่าต้องทำแบบไหน
        if (barFill.type == Image.Type.Filled)
            barFill.fillAmount = fill01;
        else
            barFill.rectTransform.anchorMax = new Vector2(fill01, barFill.rectTransform.anchorMax.y);
    }
}
