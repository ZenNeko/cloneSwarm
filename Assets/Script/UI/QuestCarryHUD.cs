using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// แสดง HUD counter ของ FetchItem ที่ local player ถืออยู่
/// (ZoneObjective Type B — FetchAndDeliver)
///
/// Setup:
///   1. วาง component บน GameObject ใน Canvas (HUD)
///   2. ลาก label (TextMeshProUGUI) เข้า field
///   3. (Optional) ลาก panel (parent GameObject) เข้า field — จะถูก hide เมื่อ carry == 0
///   4. (Optional) ใส่ icon (Image) — แสดงคู่กับเลข
///
/// แสดงผลแบบ "★ 3" — hide panel เมื่อ player ไม่ถืออะไร
/// </summary>
public class QuestCarryHUD : MonoBehaviour
{
    [Header("UI Refs")]
    [Tooltip("Panel parent — auto hide เมื่อ player ไม่ถือ item")]
    public GameObject panel;
    [Tooltip("Text แสดงจำนวน — รองรับ format \"★ {0}\"")]
    public TextMeshProUGUI label;
    [Tooltip("Icon (optional)")]
    public Image iconImage;

    [Header("Display")]
    [Tooltip("Format string — {0} = จำนวน")]
    public string format = "★ {0}";

    // ── Internal ──────────────────────────────────────────────────────────
    playermove cachedLocalPlayer;
    int        lastShown = -1;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void OnEnable()
    {
        if (panel) panel.SetActive(false);
    }

    void Update()
    {
        var pm = GetLocalPlayer();
        int n  = pm != null ? pm.carriedQuestItems.Value : 0;

        if (n == lastShown) return;
        lastShown = n;

        if (panel) panel.SetActive(n > 0);
        if (label) label.text = string.Format(format, n);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    /// <summary>หา local player (cache; refresh ถ้า object หายไป — เช่น respawn)</summary>
    playermove GetLocalPlayer()
    {
        if (cachedLocalPlayer != null) return cachedLocalPlayer;

        foreach (var pm in Object.FindObjectsByType<playermove>(FindObjectsSortMode.None))
        {
            if (pm != null && pm.IsOwner) { cachedLocalPlayer = pm; break; }
        }
        return cachedLocalPlayer;
    }
}
