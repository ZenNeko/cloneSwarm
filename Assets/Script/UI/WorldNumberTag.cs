using TMPro;
using UnityEngine;

/// <summary>
/// เลขลำดับ Limit Cut ลอยเหนือหัวผู้เล่น — billboard หันหากล้องเสมอ
///
/// **Setup: ลาก component นี้ลง player prefab ครั้งเดียว จบ**
/// ไม่ต้องสร้าง child หรือ assign ref อะไรทั้งนั้น — มันปั้น TextMeshPro ของตัวเองตอนรัน
/// (ต่างจาก WorldHPBar ที่ต้อง wire Canvas + Image + Text ใน Inspector)
///
/// อ่าน playermove.limitCutNumber ซึ่งเป็น NetworkVariable แบบ Everyone-read
/// **ทุกคนจึงเห็นเลขของทุกคน** — จำเป็นสำหรับกลไกที่ต้องรู้ว่าใครไปก่อนใคร
/// ถ้าเห็นแค่เลขตัวเองกลไกนี้จะเสียความเป็น co-op ไปเลย
/// </summary>
[RequireComponent(typeof(playermove))]
public class WorldNumberTag : MonoBehaviour
{
    [Header("Position")]
    [Tooltip("ความสูงเหนือ pivot ของผู้เล่น (world units)")]
    public float heightOffset = 2.4f;

    [Header("Style")]
    public float fontSize = 6f;
    public Color color    = new Color(1f, 0.95f, 0.3f, 1f);
    [Tooltip("สีขอบตัวอักษร — ช่วยให้อ่านออกบนพื้นสว่าง")]
    public Color outlineColor = new Color(0f, 0f, 0f, 1f);

    playermove   _pm;
    TextMeshPro  _label;
    Camera       _cam;

    void Awake()
    {
        _pm = GetComponent<playermove>();
        BuildLabel();
    }

    void BuildLabel()
    {
        var font = TMP_Settings.defaultFontAsset;
        if (font == null)
        {
            Debug.LogWarning("[WorldNumberTag] TMP_Settings.defaultFontAsset ว่าง — เลข Limit Cut จะไม่โผล่ " +
                             "(Project Settings → TextMesh Pro → Default Font Asset)");
            return;
        }

        var go = new GameObject("LimitCutNumber");
        go.transform.SetParent(transform, worldPositionStays: false);
        go.transform.localPosition = new Vector3(0f, heightOffset, 0f);

        _label = go.AddComponent<TextMeshPro>();
        _label.font              = font;
        _label.fontSize          = fontSize;
        _label.color             = color;
        _label.alignment         = TextAlignmentOptions.Center;
        _label.outlineColor      = outlineColor;
        _label.outlineWidth      = 0.2f;
        _label.raycastTarget     = false;
        _label.text              = "";

        // ไม่ให้ไปบังจอตอนกล้องอยู่ใกล้
        var mr = go.GetComponent<MeshRenderer>();
        if (mr) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        go.SetActive(false);
    }

    void OnEnable()
    {
        if (_pm != null) _pm.limitCutNumber.OnValueChanged += OnNumberChanged;
        Refresh();
    }

    void OnDisable()
    {
        if (_pm != null) _pm.limitCutNumber.OnValueChanged -= OnNumberChanged;
    }

    void OnNumberChanged(int _, int __) => Refresh();

    void Refresh()
    {
        if (_label == null || _pm == null) return;

        int n = _pm.limitCutNumber.Value;
        bool show = n > 0 && !_pm.isDead.Value;

        _label.gameObject.SetActive(show);
        if (show) _label.text = n.ToString();
    }

    void LateUpdate()
    {
        if (_label == null || !_label.gameObject.activeSelf) return;

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        var t = _label.transform;
        t.position = transform.position + Vector3.up * heightOffset;
        t.rotation = _cam.transform.rotation;   // billboard
    }
}
