using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// FloatingBuffUI — ระบบสร้าง Canvas หน้าต่างแจ้งเตือนบัฟดาเมจลอยเหนือหัวผู้เล่นโดยอัตโนมัติผ่านโค้ด (World Space Billboard)
/// </summary>
public class FloatingBuffUI : MonoBehaviour
{
    private Canvas canvas;
    private Image iconImage;
    private TextMeshProUGUI durationText;
    private float remainingDuration;

    public static void Create(Transform playerTransform, float duration)
    {
        if (playerTransform == null) return;

        // เช็คว่ามี UI บัฟนี้อยู่บนหัวผู้เล่นเดิมอยู่แล้วหรือไม่ ถ้ามีให้รีเฟรชเวลา
        var existing = playerTransform.GetComponentInChildren<FloatingBuffUI>();
        if (existing != null)
        {
            existing.Refresh(duration);
            return;
        }

        // สร้าง GameObject ใหม่ขึ้นมาทำงาน
        GameObject go = new GameObject("FloatingBuffUI");
        go.transform.SetParent(playerTransform);
        // ตั้งตำแหน่งให้ลอยตัวเหนือแถบเลือดของผู้เล่น (~2.2 เมตร)
        go.transform.localPosition = new Vector3(0f, 2.2f, 0f);

        var buffUI = go.AddComponent<FloatingBuffUI>();
        buffUI.Init(duration);
    }

    private void Init(float duration)
    {
        remainingDuration = duration;

        // 1. ตั้งค่า Canvas โหมดพิกัดโลก (World Space)
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        // ตั้งสัดส่วนขนาดพิกัดโลกให้เล็กพอเหมาะ
        var rect = GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1.8f, 0.5f);
        rect.localScale = Vector3.one * 0.4f;

        // 2. สร้างพื้นหลังสีดำโปร่งใส (Background Panel)
        GameObject panelGo = new GameObject("BuffPanel");
        panelGo.transform.SetParent(transform, false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(1.8f, 0.5f);

        var bgImage = panelGo.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.65f); // พื้นหลังกึ่งโปร่งใส

        // 3. สร้างข้อความบอกเวลาบัฟและอิโมจิเบ่งกล้าม
        GameObject textGo = new GameObject("BuffText");
        textGo.transform.SetParent(panelGo.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(1.8f, 0.5f);

        durationText = textGo.AddComponent<TextMeshProUGUI>();
        durationText.fontSize = 0.22f;
        durationText.alignment = TextAlignmentOptions.Center;
        durationText.color = new Color(1f, 0.85f, 0.0f, 1f); // สีเหลืองทอง
        durationText.text = $"💪 {remainingDuration:F1}s";
    }

    public void Refresh(float duration)
    {
        remainingDuration = Mathf.Max(remainingDuration, duration);
    }

    private Camera _cam;

    void Update()
    {
        remainingDuration -= Time.deltaTime;
        if (remainingDuration <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        // หมุนหันหน้าเข้าหาหน้ากล้องหลักตลอดเวลา (Billboard Effect)
        if (_cam == null) _cam = Camera.main;
        if (_cam != null)
        {
            transform.rotation = _cam.transform.rotation;
        }

        // อัปเดตเวลาถอยหลัง
        if (durationText != null)
        {
            durationText.text = $"💪 {remainingDuration:F1}s";
        }
    }
}
