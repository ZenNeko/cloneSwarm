using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// HUD แสดงเวลาเกมแบบนับขึ้น (00:00 → 15:00)
/// - timerLabel     : "05:32"  (นับขึ้นตลอด)
/// - announcementLabel : แสดงเฉพาะ "MAIN BOSS!" เท่านั้น
/// </summary>
public class WaveHUD : MonoBehaviour
{
    [Header("Game Clock")]
    [Tooltip("แสดงเวลาเกมที่ผ่านไป เช่น '05:32'")]
    public TextMeshProUGUI timerLabel;

    [Header("Announcement Banner")]
    [Tooltip("Label ใหญ่สำหรับ 'MAIN BOSS!' — ใส่ไว้ตรงกลางจอ")]
    public TextMeshProUGUI announcementLabel;
    [Tooltip("วินาทีที่ announcement แสดงก่อนจาง")]
    public float announcementDuration = 2.5f;

    // ── State ─────────────────────────────────────────────────────────────
    private float localElapsed;   // smooth count-up บน client

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Start()
    {
        if (announcementLabel) announcementLabel.gameObject.SetActive(false);
        StartCoroutine(WaitAndSubscribe());
    }

    IEnumerator WaitAndSubscribe()
    {
        // รอ GameTimeline พร้อม
        while (GameTimeline.Instance == null) yield return null;

        localElapsed = GameTimeline.Instance.gameTime.Value;
        GameTimeline.Instance.gameTime.OnValueChanged += OnElapsedChanged;
    }

    void OnEnable()
    {
        GameTimeline.OnMainBossTime += OnMainBossPhase;
    }

    void OnDisable()
    {
        GameTimeline.OnMainBossTime -= OnMainBossPhase;

        if (GameTimeline.Instance != null)
            GameTimeline.Instance.gameTime.OnValueChanged -= OnElapsedChanged;
    }

    // ── Update: smooth count-up (interpolate ระหว่าง server sync) ─────────
    void Update()
    {
        localElapsed += Time.deltaTime;
        UpdateTimerLabel(localElapsed);
    }

    // ── NetworkVariable Callback ──────────────────────────────────────────
    void OnElapsedChanged(float _, float v)
    {
        localElapsed = v;   // snap ให้ตรงกับ server
    }

    // ── Main Boss Event ───────────────────────────────────────────────────
    void OnMainBossPhase()
    {
        ShowAnnouncement("MAIN BOSS!", Color.red);
    }

    // ── UI Helpers ────────────────────────────────────────────────────────
    void UpdateTimerLabel(float elapsed)
    {
        if (timerLabel == null) return;
        int mins = Mathf.FloorToInt(elapsed / 60f);
        int secs = Mathf.FloorToInt(elapsed % 60f);
        timerLabel.text = $"{mins:00}:{secs:00}";
    }

    void ShowAnnouncement(string text, Color color)
    {
        if (announcementLabel == null) return;
        StopCoroutine(nameof(FadeAnnouncement));
        StartCoroutine(nameof(FadeAnnouncement), (text, color));
    }

    IEnumerator FadeAnnouncement(object args)
    {
        var (text, color) = ((string, Color))args;

        announcementLabel.text  = text;
        announcementLabel.color = color;
        announcementLabel.gameObject.SetActive(true);

        var c = announcementLabel.color;
        c.a = 1f;
        announcementLabel.color = c;

        yield return new WaitForSecondsRealtime(announcementDuration * 0.6f);

        float elapsed    = 0f;
        float fadeDuration = announcementDuration * 0.4f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            announcementLabel.color = c;
            yield return null;
        }

        announcementLabel.gameObject.SetActive(false);
    }
}
