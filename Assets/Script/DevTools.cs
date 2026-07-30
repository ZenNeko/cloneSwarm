using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Dev Tool Panel — กด F1 เพื่อเปิด/ปิด
/// ใช้ใน SampleScene โดยไม่ต้องสลับ Scene
///
/// Features:
///   • Force Level Up (เพิ่ม EXP เต็ม threshold)
///   • Add EXP +50
///   • แสดง EXP / Level / Wave / เวลา
///   • Kill All Enemies
///   • Auto-fill allUpgrades จาก Resources
/// </summary>
public class DevTools : MonoBehaviour
{
    [Header("UI References (สร้าง auto ถ้าไม่ assign)")]
    public GameObject panelRoot;

    // ── Auto-build state ──────────────────────────────────────────────────
    private bool       builtUI;
    private TextMeshProUGUI infoText;
    private TextMeshProUGUI vfxText;

    // ─────────────────────────────────────────────────────────────────────
    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) TogglePanel();
        if (panelRoot != null && panelRoot.activeSelf) RefreshInfo();
    }

    void TogglePanel()
    {
        if (panelRoot == null) BuildUI();
        panelRoot.SetActive(!panelRoot.activeSelf);
    }

    // ── Info Refresh ──────────────────────────────────────────────────────
    void RefreshInfo()
    {
        if (infoText == null) return;
        var sem = SharedExperienceManager.Instance;
        var wm  = WaveManager.Instance;

        string expLine   = sem != null
            ? $"EXP: <b>{sem.sharedExp.Value:F0}</b> / {sem.sharedExpToNext.Value:F0}   Level: <b>{sem.sharedLevel.Value}</b>"
            : "SharedExperienceManager - not found";

        string waveLine  = wm != null
            ? $"Wave: <b>{wm.GetCurrentWave()}</b>   HPx{wm.CurrentHealthMultiplier:F2}"
            : "WaveManager - not found";

        string timeLine  = "";
        var gt = FindFirstObjectByType<GameTimeline>();
        if (gt != null)
        {
            float t = gt.GetGameTime();
            timeLine = $"Time: <b>{Mathf.FloorToInt(t / 60):00}:{(int)(t % 60):00}</b>";
        }

        string upgradeManagerLine = "";
        var um = FindFirstObjectByType<UpgradeManager>();
        if (um != null)
            upgradeManagerLine = $"Weapons: <b>{um.allWeapons.Count}</b>  Stats: <b>{um.allStats.Count}</b>";

        infoText.text = $"{expLine}\n{waveLine}\n{timeLine}\n{upgradeManagerLine}";

        if (vfxText != null)
        {
            var pool = NetworkedVFXPool.Instance;
            vfxText.text = pool != null
                ? pool.BuildShortReport(5)
                : "NetworkedVFXPool - not in scene";
        }
    }

    // ── VFX Pool ──────────────────────────────────────────────────────────
    void OnLogVfxReport()
    {
        var pool = NetworkedVFXPool.Instance;
        if (pool == null) { Debug.LogWarning("[DevTools] NetworkedVFXPool ไม่พบ"); return; }
        pool.LogReport();
    }

    void OnResetVfxStats()
    {
        var pool = NetworkedVFXPool.Instance;
        if (pool == null) { Debug.LogWarning("[DevTools] NetworkedVFXPool ไม่พบ"); return; }
        pool.ResetStats();
    }

    // ── Button Callbacks ──────────────────────────────────────────────────
    void OnAddEXP()
    {
        var sem = SharedExperienceManager.Instance;
        if (sem == null) { Debug.LogWarning("[DevTools] SharedExperienceManager ไม่พบ"); return; }
        if (!NetworkManager.Singleton.IsServer) { Debug.LogWarning("[DevTools] ต้องรันในฐานะ Host/Server"); return; }
        sem.AddExp(50);
        Debug.Log("[DevTools] +50 EXP");
    }

    void OnForceLevelUp()
    {
        var sem = SharedExperienceManager.Instance;
        if (sem == null) { Debug.LogWarning("[DevTools] SharedExperienceManager ไม่พบ"); return; }
        if (!NetworkManager.Singleton.IsServer) { Debug.LogWarning("[DevTools] ต้องรันในฐานะ Host/Server"); return; }
        int need = Mathf.Max(1, Mathf.CeilToInt(sem.sharedExpToNext.Value - sem.sharedExp.Value));
        sem.AddExp(need);
        Debug.Log($"[DevTools] Force Level Up — ใส่ {need} EXP");
    }

    void OnKillAllEnemies()
    {
        if (!RequireServer()) return;

        // คัดลอกลิสต์เพื่อป้องกันปัญหาแก้คอลเลกชันขณะกำลังวนซ้ำ
        var enemies = new List<Enemy>(Enemy.ActiveEnemies);
        int count = 0;
        foreach (var e in enemies)
        {
            if (e != null && e.gameObject.activeSelf)
            {
                e.EnemyTakeDamage(999999f);
                count++;
            }
        }
        Debug.Log($"[DevTools] Kill All — {count} enemies");
    }

    void OnAutoFillUpgrades()
    {
        var um = FindFirstObjectByType<UpgradeManager>();
        if (um == null) { Debug.LogWarning("[DevTools] UpgradeManager ไม่พบ"); return; }

        var weapons = Resources.LoadAll<WeaponData>("");
        var stats   = Resources.LoadAll<StatData>("");

        if (weapons.Length > 0) um.allWeapons = new List<WeaponData>(weapons);
        if (stats.Length   > 0) um.allStats   = new List<StatData>(stats);

        Debug.Log($"[DevTools] Auto-fill — Weapons:{weapons.Length}  Stats:{stats.Length}");
    }

    void OnSetTimeScale(float scale)
    {
        GamePause.ResumeScale = scale;
        Debug.Log($"[DevTools] TimeScale → {scale}");
    }

    // ── Boss / Objective Spawn ───────────────────────────────────────────
    void OnSpawnMiniBoss()
    {
        if (!RequireServer()) return;
        var bm = BossManager.Instance;
        if (bm == null) { Debug.LogWarning("[DevTools] BossManager ไม่พบ"); return; }
        bm.DevSpawnMiniBoss();
        Debug.Log("[DevTools] 🟡 Force spawn Mini Boss");
    }

    void OnSpawnMainBoss()
    {
        if (!RequireServer()) return;
        var bm = BossManager.Instance;
        if (bm == null) { Debug.LogWarning("[DevTools] BossManager ไม่พบ"); return; }
        bm.DevSpawnMainBoss();
        Debug.Log("[DevTools] 🔴 Force spawn Main Boss");
    }

    void OnSpawnObjective()
    {
        if (!RequireServer()) return;
        var om = FindFirstObjectByType<ObjectiveManager>();
        if (om == null) { Debug.LogWarning("[DevTools] ObjectiveManager ไม่พบ"); return; }
        om.DevSpawnObjective();
        Debug.Log("[DevTools] 🎯 Force spawn Objective");
    }

    bool RequireServer()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer) return true;
        Debug.LogWarning("[DevTools] ต้องรันในฐานะ Host/Server");
        return false;
    }

    // ── Build UI at Runtime ───────────────────────────────────────────────
    void BuildUI()
    {
        if (builtUI) return;
        builtUI = true;

        // ── Root Canvas ──
        var canvasGO = new GameObject("DevToolsCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode    = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder  = 999;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Panel ──
        panelRoot = new GameObject("DevPanel");
        panelRoot.transform.SetParent(canvasGO.transform, false);
        var panelImage = panelRoot.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.82f);

        var panelRT = panelRoot.GetComponent<RectTransform>();
        panelRT.anchorMin   = new Vector2(0, 0);
        panelRT.anchorMax   = new Vector2(0, 1);
        panelRT.offsetMin   = new Vector2(0,   0);
        panelRT.offsetMax   = new Vector2(280, 0);

        // ── Layout ──
        var layout = panelRoot.AddComponent<VerticalLayoutGroup>();
        layout.padding         = new RectOffset(10, 10, 10, 10);
        layout.spacing         = 6;
        layout.childForceExpandWidth  = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth      = true;
        layout.childControlHeight     = true;

        // ── Title ──
        AddLabel(panelRoot, "DEV TOOLS  (F1)", 16, Color.cyan);

        // ── Info block ──
        infoText = AddLabel(panelRoot, "...", 12, Color.white);
        infoText.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 72);

        // ── Separator ──
        AddLabel(panelRoot, "──────────────────────", 10, new Color(0.5f, 0.5f, 0.5f));

        // ── Buttons ──
        AddButton(panelRoot, "+50 EXP",          Color.green,         OnAddEXP);
        AddButton(panelRoot, "Force Level Up",   new Color(1f,0.8f,0f), OnForceLevelUp);
        AddButton(panelRoot, "Kill All Enemies", new Color(1f,0.3f,0.3f), OnKillAllEnemies);
        AddButton(panelRoot, "Auto-fill Upgrades", new Color(0.4f,0.8f,1f), OnAutoFillUpgrades);

        // ── Spawning ──
        AddLabel(panelRoot, "-- Spawn --", 11, new Color(0.7f,0.7f,0.7f));
        AddButton(panelRoot, "Spawn Mini Boss",  new Color(1f, 0.85f, 0.2f), OnSpawnMiniBoss);
        AddButton(panelRoot, "Spawn Main Boss",  new Color(1f, 0.25f, 0.25f), OnSpawnMainBoss);
        AddButton(panelRoot, "Spawn Objective",  new Color(0.3f, 1f, 0.6f), OnSpawnObjective);

        // ── VFX Pool ──
        AddLabel(panelRoot, "-- VFX Pool (undersized) --", 11, new Color(0.7f,0.7f,0.7f));
        vfxText = AddLabel(panelRoot, "...", 10, Color.white);
        vfxText.textWrappingMode = TextWrappingModes.Normal;
        vfxText.GetComponent<LayoutElement>().preferredHeight = 78;
        AddButton(panelRoot, "Log VFX Report",  new Color(0.8f,0.5f,1f), OnLogVfxReport);
        AddButton(panelRoot, "Reset VFX Stats", new Color(0.5f,0.5f,0.6f), OnResetVfxStats);

        AddLabel(panelRoot, "-- Time Scale --", 11, new Color(0.7f,0.7f,0.7f));

        var row = new GameObject("TimeScaleRow");
        row.transform.SetParent(panelRoot.transform, false);
        var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 4;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        row.AddComponent<LayoutElement>().preferredHeight = 30;

        AddSmallButton(row, "×0.1",  Color.yellow, () => OnSetTimeScale(0.1f));
        AddSmallButton(row, "×0.5",  Color.yellow, () => OnSetTimeScale(0.5f));
        AddSmallButton(row, "×1",    Color.white,  () => OnSetTimeScale(1f));
        AddSmallButton(row, "×3",    Color.cyan,   () => OnSetTimeScale(3f));

        AddLabel(panelRoot, "F1 to toggle", 10, new Color(0.5f,0.5f,0.5f));

        panelRoot.SetActive(false);
    }

    // ── UI Helpers ────────────────────────────────────────────────────────
    TextMeshProUGUI AddLabel(GameObject parent, string text, int size, Color color)
    {
        var go  = new GameObject("Label");
        go.transform.SetParent(parent.transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.color     = color;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = size + 6;
        return tmp;
    }

    void AddButton(GameObject parent, string label, Color col, UnityEngine.Events.UnityAction action)
    {
        var go  = new GameObject("Btn_" + label);
        go.transform.SetParent(parent.transform, false);

        var img = go.AddComponent<Image>();
        img.color = col * 0.7f;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = col;
        colors.pressedColor     = col * 0.5f;
        btn.colors = colors;
        btn.onClick.AddListener(action);

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 28;

        var txt = new GameObject("Text");
        txt.transform.SetParent(go.transform, false);
        var tmp = txt.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 13;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        var rt = txt.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    void AddSmallButton(GameObject parent, string label, Color col, UnityEngine.Events.UnityAction action)
    {
        var go  = new GameObject("Btn_" + label);
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color = col * 0.6f;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        // ต้องมี LayoutElement เหมือน AddButton — HorizontalLayoutGroup ของแถวตั้ง
        // childControlHeight = true ลูกที่ไม่บอก preferred height จะยุบเหลือสูงศูนย์
        // ตัวอักษรยังวาดเห็นเพราะ anchor เต็มพื้นที่ แต่ Image ไม่มีพื้นที่รับคลิก = กดไม่ได้
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 26;
        le.flexibleWidth   = 1;

        var txt = new GameObject("Text");
        txt.transform.SetParent(go.transform, false);
        var tmp = txt.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 12;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        var rt = txt.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
