using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ต่อ WeaponTestScene ให้ใช้งานได้จริง — สร้าง UI ของ harness แล้ว assign ทุกช่องของ
/// WeaponTestManager ให้อัตโนมัติ
///
/// **ทำไมต้องมี:** ซีนมี WeaponTestManager วางอยู่ตั้งแต่แรกแต่ **ไม่ได้ assign อะไรเลยสักช่อง**
/// (ตรวจจาก YAML ดิบ 2026-08-14 — ทุก field เป็น fileID: 0) harness จึงไม่เคยทำงาน
/// ส่วนที่ขาดจริงคือ UI: ไม่มีลิสต์ปุ่มอาวุธ ไม่มีป้าย DPS ไม่มีสไลเดอร์สเตต
/// (UI ที่เห็นในซีนคือ HUD ของเกมปกติที่ก๊อปมาจาก SampleScene ไม่เกี่ยวกับ harness)
///
/// dummy ไม่ต้องสร้าง — TargetDummy.prefab มีอยู่แล้ว ลงทะเบียนใน DefaultNetworkPrefabs แล้ว
/// และวางในซีนไว้ 12 ตัวแล้ว
///
/// **รันซ้ำได้ปลอดภัย** — ลบ root เดิมทิ้งแล้วสร้างใหม่ ไม่ทับ UI ส่วนอื่นของซีน
/// </summary>
public static class WeaponTestSceneSetup
{
    const string ScenePath  = "Assets/GameScenes/WeaponTestScene.unity";
    const string DummyPath  = "Assets/Prefab/Enemy/TargetDummy.prefab";
    const string ButtonPath = "Assets/Prefab/UI/WeaponTestButton.prefab";
    const string RootName   = "WeaponTestHarnessUI";

    // TMP default (LiberationSans) ไม่มี glyph ไทย — ข้อความไทยจะขึ้นเป็นสี่เหลี่ยมทั้งบรรทัด
    // โปรเจกต์มี Sarabun อยู่แล้วและ UI ส่วนอื่นก็ใช้ตัวนี้
    const string FontPath   = "Assets/Prefab/Art Asset/Fnot/Sarabun/Sarabun-Regular SDF.asset";

    static TMP_FontAsset _font;
    static TMP_FontAsset ThaiFont
    {
        get
        {
            if (_font == null) _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (_font == null) Debug.LogError($"[WeaponTestSetup] ไม่พบฟอนต์ {FontPath} — ข้อความไทยจะเป็นสี่เหลี่ยม");
            return _font;
        }
    }

    [MenuItem("Tools/Clone Swarm/Weapon Test/Setup Scene")]
    public static void Setup()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) { Debug.LogError($"[WeaponTestSetup] เปิดซีนไม่ได้: {ScenePath}"); return; }

        var mgr = Object.FindFirstObjectByType<WeaponTestManager>(FindObjectsInactive.Include);
        if (mgr == null)
        {
            Debug.LogError("[WeaponTestSetup] ไม่พบ WeaponTestManager ในซีน");
            return;
        }

        // ── ล้างของเดิมก่อน ให้รันซ้ำได้โดยไม่ซ้อน ──────────────────────────
        var old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);

        var buttonPrefab = EnsureButtonPrefab();

        // ── Canvas ของ harness แยกจาก HUD เกม ────────────────────────────
        var root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas),
                                  typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;            // ทับ HUD เกมไว้ ไม่งั้นกดปุ่มไม่โดน
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // ── คอลัมน์ซ้าย: ลิสต์ปุ่มอาวุธ (scroll ได้ เพราะมี 46 ตัว) ──────────
        var listPanel = Panel(root.transform, "WeaponListPanel",
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(12f, 12f), new Vector2(330f, -12f));

        Label(listPanel.transform, "Title", "WEAPONS", 22,
              new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -40f), new Vector2(-10f, -6f));

        // RectMask2D ไม่ใช้ stencil และไม่ต้องมี Graphic — ตัดเฉพาะตาม rect ตรงๆ
        // เดิมใช้ Mask + Image alpha 0.001 ซึ่ง Mask เขียน stencil ตาม alpha ของกราฟิก
        // พออัลฟาเกือบศูนย์มันเลย mask ลูกทิ้งหมด ปุ่มอาวุธจึงถูกสร้างจริงแต่มองไม่เห็นสักตัว
        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D));
        scrollGo.transform.SetParent(listPanel.transform, false);
        var scrollRt = Stretch(scrollGo, new Vector2(6f, 6f), new Vector2(-6f, -44f));

        var content = new GameObject("Content", typeof(RectTransform),
                                     typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(scrollGo.transform, false);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot     = new Vector2(0.5f, 1f);
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;

        var vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.spacing              = 3f;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = true;

        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var sr = scrollGo.GetComponent<ScrollRect>();
        sr.content    = contentRt;
        sr.viewport   = scrollRt;
        sr.horizontal = false;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 30f;

        // ── แผงขวาบน: DPS + สเตต ─────────────────────────────────────────
        var statPanel = Panel(root.transform, "StatPanel",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-372f, -368f), new Vector2(-12f, -12f));

        var dps   = Label(statPanel.transform, "DpsLabel",   "DPS: 0",   30, new Vector2(0f,1f), new Vector2(1f,1f), new Vector2(10f,-48f),  new Vector2(-10f,-8f));
        var total = Label(statPanel.transform, "TotalLabel", "Total: 0", 22, new Vector2(0f,1f), new Vector2(1f,1f), new Vector2(10f,-80f),  new Vector2(-10f,-50f));
        var stats = Label(statPanel.transform, "StatsLabel", "Dmg x1.0 | Haste +0 | Crit 0%", 18,
                          new Vector2(0f,1f), new Vector2(1f,1f), new Vector2(10f,-112f), new Vector2(-10f,-84f));

        // มีป้ายกำกับทุกตัว — สามแท่งเปล่าเรียงกันไม่มีทางรู้ว่าตัวไหนคุมอะไร
        var dmgS   = MakeSlider(statPanel.transform, "DamageSlider", "Damage", -156f, 1f, 10f,  1f);
        var hasteS = MakeSlider(statPanel.transform, "HasteSlider",  "Haste",  -196f, 0f, 200f, 0f);
        var critS  = MakeSlider(statPanel.transform, "CritSlider",   "Crit",   -236f, 0f, 1f,   0f);

        var respawnBtn = MakeButton(statPanel.transform, "RespawnAllButton", "Respawn Dummies", -288f);
        var resetBtn   = MakeButton(statPanel.transform, "ResetStatsButton", "Reset Stats",     -328f);

        // ── ต่อสายเข้า manager ────────────────────────────────────────────
        var so = new SerializedObject(mgr);
        so.FindProperty("dummyPrefab").objectReferenceValue           = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPath);
        so.FindProperty("weaponButtonContainer").objectReferenceValue = contentRt;
        so.FindProperty("buttonPrefab").objectReferenceValue          = buttonPrefab;
        so.FindProperty("dpsLabel").objectReferenceValue              = dps;
        so.FindProperty("totalDamageLabel").objectReferenceValue      = total;
        so.FindProperty("statsLabel").objectReferenceValue            = stats;
        so.FindProperty("damageSlider").objectReferenceValue          = dmgS;
        so.FindProperty("hasteSlider").objectReferenceValue           = hasteS;
        so.FindProperty("critSlider").objectReferenceValue            = critS;
        so.FindProperty("respawnAllButton").objectReferenceValue      = respawnBtn;
        so.FindProperty("resetStatsButton").objectReferenceValue      = resetBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (so.FindProperty("dummyPrefab").objectReferenceValue == null)
            Debug.LogError($"[WeaponTestSetup] ไม่พบ {DummyPath} — dummy ที่ตายจะไม่ respawn");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[WeaponTestSetup] ✅ ต่อครบแล้ว — กด Play แล้วลิสต์ปุ่มอาวุธจะสร้างเองตอน Start " +
                  "(allWeapons ดึงจากโปรเจกต์อัตโนมัติ ไม่ต้องลาก)");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Builders
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>ปุ่มต้นแบบของลิสต์อาวุธ — ต้องเป็น prefab asset เพราะ manager เก็บเป็น Button reference</summary>
    /// <summary>
    /// สร้างใหม่ทับของเดิมทุกครั้ง — SaveAsPrefabAsset เขียนทับ path เดิมโดย guid ไม่เปลี่ยน
    /// reference ในซีนจึงไม่ขาด · จำเป็นเพราะถ้าคืนของเดิมไปเฉยๆ การแก้ฟอนต์/หน้าตาปุ่ม
    /// จะไม่มีผลกับ prefab ที่สร้างไว้รอบก่อน
    /// </summary>
    static Button EnsureButtonPrefab()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefab/UI"))
            AssetDatabase.CreateFolder("Assets/Prefab", "UI");

        var go = new GameObject("WeaponTestButton", typeof(RectTransform), typeof(Image),
                                typeof(Button), typeof(LayoutElement));
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(300f, 30f);
        go.GetComponent<Image>().color = new Color(0.18f, 0.20f, 0.26f, 0.95f);
        go.GetComponent<LayoutElement>().minHeight = 30f;

        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(go.transform, false);
        Stretch(label, new Vector2(8f, 0f), new Vector2(-8f, 0f));
        var tmp = label.GetComponent<TextMeshProUGUI>();
        if (ThaiFont != null) tmp.font = ThaiFont;
        tmp.text      = "Weapon";
        tmp.fontSize  = 16;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color     = Color.white;

        var saved = PrefabUtility.SaveAsPrefabAsset(go, ButtonPath);
        Object.DestroyImmediate(go);
        Debug.Log($"[WeaponTestSetup] สร้าง {ButtonPath}");
        return saved.GetComponent<Button>();
    }

    static GameObject Panel(Transform parent, string name, Vector2 aMin, Vector2 aMax,
                            Vector2 offMin, Vector2 offMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
        go.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.82f);
        return go;
    }

    static TextMeshProUGUI Label(Transform parent, string name, string text, float size,
                                 Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (ThaiFont != null) tmp.font = ThaiFont;
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color     = Color.white;
        return tmp;
    }

    /// <summary>
    /// สร้าง Slider ด้วยมือ — DefaultControls.CreateSlider ต้องส่ง sprite resources
    /// ซึ่งใน batchmode ไม่มีให้ ประกอบเองสามชิ้น (background / fill / handle) ตรงไปตรงมากว่า
    /// </summary>
    static Slider MakeSlider(Transform parent, string name, string caption, float top,
                             float min, float max, float value)
    {
        Label(parent, name + "Caption", caption, 16,
              new Vector2(0f, 1f), new Vector2(0f, 1f),
              new Vector2(10f, top), new Vector2(88f, top + 26f));

        var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(92f, top);
        rt.offsetMax = new Vector2(-10f, top + 26f);

        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(go.transform, false);
        Stretch(bg, Vector2.zero, Vector2.zero);
        bg.GetComponent<Image>().color = new Color(0.15f, 0.16f, 0.20f, 1f);

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        Stretch(fillArea, new Vector2(2f, 2f), new Vector2(-2f, -2f));

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        Stretch(fill, Vector2.zero, Vector2.zero);
        fill.GetComponent<Image>().color = new Color(0.30f, 0.62f, 0.95f, 1f);

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        Stretch(handleArea, Vector2.zero, Vector2.zero);

        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        var hrt = handle.GetComponent<RectTransform>();
        hrt.sizeDelta = new Vector2(14f, 0f);
        handle.GetComponent<Image>().color = Color.white;

        var slider = go.GetComponent<Slider>();
        slider.fillRect   = fill.GetComponent<RectTransform>();
        slider.handleRect = hrt;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction  = Slider.Direction.LeftToRight;
        slider.minValue   = min;
        slider.maxValue   = max;
        slider.value      = value;
        return slider;
    }

    static Button MakeButton(Transform parent, string name, string text, float top)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(10f, top);
        rt.offsetMax = new Vector2(-10f, top + 32f);
        go.GetComponent<Image>().color = new Color(0.22f, 0.26f, 0.34f, 1f);

        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(go.transform, false);
        Stretch(label, Vector2.zero, Vector2.zero);
        var tmp = label.GetComponent<TextMeshProUGUI>();
        if (ThaiFont != null) tmp.font = ThaiFont;
        tmp.text      = text;
        tmp.fontSize  = 17;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;

        return go.GetComponent<Button>();
    }

    static RectTransform Stretch(GameObject go, Vector2 offMin, Vector2 offMax)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
        return rt;
    }
}
