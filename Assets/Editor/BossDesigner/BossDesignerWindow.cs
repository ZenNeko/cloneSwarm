using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Boss Designer — หน้าต่างออกแบบ encounter ของบอสแบบเห็นภาพ
///
/// ครึ่งบน: encounter graph — เฟสเรียงเป็น node พร้อมเงื่อนไขเปลี่ยนเฟส (HP%) และ enrage
/// ครึ่งล่าง: timeline editor ของ BossTimelineAction — ลากคลิปเปลี่ยนเวลาเริ่ม (snap 0.1s,
///           กด Shift ขณะลากเพื่อปิด snap), คลิกคลิปเพื่อแก้ค่าใน Inspector,
///           ลาก BossAction asset จาก Project มาวางบนเลนได้เลย
///
/// เปิดผ่าน Tools → Boss Designer หรือดับเบิลคลิก BossEncounterConfig / BossTimelineAction
/// </summary>
public partial class BossDesignerWindow : EditorWindow
{
    // ── Layout ────────────────────────────────────────────────────────────
    const float TrackHeaderW = 150f;
    const float TrackH       = 38f;
    const float RulerH       = 26f;
    const float SnapStep     = 0.1f;
    const float MinPxPerSec  = 5f;
    const float MaxPxPerSec  = 400f;
    const float ZoomStep     = 1.12f;   // ต่อ 1 คลิกล้อ

    // ── State (serialize เพื่อรอด domain reload) ──────────────────────────
    [SerializeField] BossEncounterConfig config;
    [SerializeField] BossTimelineAction  timeline;
    [SerializeField] int   phaseIndex = -1;
    [SerializeField] bool  editingEnrage;
    [SerializeField] float pxPerSec = 50f;
    [SerializeField] float playhead;

    BossTimelineAction.TimelineClip selectedClip;

    /// <summary>ตำแหน่ง scroll แนวนอนที่ต้องเรียกคืนหลัง rebuild · &lt;0 = ให้จำตำแหน่งปัจจุบันเอง</summary>
    float pendingScrollX = -1f;
    ScrollView timelineScroll;
    /// <summary>element ของคลิปที่เลือกอยู่ — ใช้ล้างไฮไลต์ตัวเก่าโดยไม่ต้อง rebuild ทั้งเลน</summary>

    // ── UI refs ───────────────────────────────────────────────────────────
    ObjectField   configField;
    Button        auditButton;
    Button        testButton;
    Button        variantButton;
    Button        embedButton;
    Label         usageLabel;

    /// <summary>ผล BossConfigAudit เฉพาะ config ที่เปิดอยู่ · คำนวณตอนเปลี่ยน config / กดตรวจ ไม่ใช่ทุก rebuild</summary>
    readonly List<CloneSwarm.EditorTools.BossConfigAudit.Problem> auditProblems = new();
    VisualElement graphPane;
    VisualElement timelinePane;
    VisualElement inspectorPane;

    /// <summary>แผนผังสนามที่ playhead — แกนที่เส้นเวลาบอกไม่ได้ว่าท่าลงตรงไหน</summary>
    CloneSwarm.EditorTools.ArenaPreview arenaPreview;
    CloneSwarm.EditorTools.BossPalette  palette;

    // ── ตัวเลขประมาณเวลาเล่นต่อเฟส (จำต่อเครื่องใน EditorPrefs) ──
    const string PrefBossHp  = "CloneSwarm.BossDesigner.BossHp";
    const string PrefTeamDps = "CloneSwarm.BossDesigner.TeamDps";
    const string PrefGod     = "CloneSwarm.BossDesigner.TestGod";

    /// <summary>seed ของ roll ในพรีวิว — กดปุ่มเพื่อดูแพตเทิร์นแบบอื่นของ roll เดียวกัน</summary>
    [SerializeField] int previewRollSeed = 1;

    // ── เล่นพรีวิว ────────────────────────────────────────────────────────
    // เดิมต้องลากไม้บรรทัดทีละจุดถึงจะเห็นว่าท่าลงตรงไหน · ▶ เดิน playhead ตามเวลาจริง
    // ด้วย EditorApplication.update (ไม่ต้องเข้า Play Mode) แล้วแผนผังสนามตามทุกเฟรม
    [SerializeField] float playSpeed = 1f;
    [SerializeField] bool  playLoop  = true;
    bool   isPlaying;
    double lastTick;
    static readonly float[] PlaySpeeds = { 0.5f, 1f, 2f };

    static readonly Color NodeBg       = new Color(0.22f, 0.22f, 0.22f);
    static readonly Color NodeSelected = new Color(0.95f, 0.55f, 0.25f);
    static readonly Color LaneBg       = new Color(0.16f, 0.16f, 0.16f);
    static readonly Color GridLine     = new Color(1f, 1f, 1f, 0.06f);
    static readonly Color PlayheadCol  = new Color(0.95f, 0.35f, 0.2f);

    // ── Entry points ──────────────────────────────────────────────────────
    [MenuItem("Tools/Boss Designer")]
    public static void Open() => GetWindow<BossDesignerWindow>("Boss Designer");

    [OnOpenAsset]
    static bool OnOpenAsset(EntityId entityId, int line)
    {
        // Unity 6000.7 เลิกใช้ instance id แบบ int — callback ต้องรับ EntityId ตรงๆ
        var obj = EditorUtility.EntityIdToObject(entityId);
        if (obj is BossEncounterConfig cfg)
        {
            var w = GetWindow<BossDesignerWindow>("Boss Designer");
            w.SetConfig(cfg);
            return true;
        }
        if (obj is BossTimelineAction tl)
        {
            var w = GetWindow<BossDesignerWindow>("Boss Designer");
            w.SetStandaloneTimeline(tl);
            return true;
        }
        return false;
    }

    // ── ขนาดตัวอักษร ── ทั้งหน้าต่างคูณด้วยค่านี้ (A− / A+ บน toolbar · จำใน EditorPrefs)
    const string PrefFontScale = "CloneSwarm.BossDesigner.FontScale";
    static float? _fontScale;
    public static float FontScale
    {
        get => _fontScale ??= EditorPrefs.GetFloat(PrefFontScale, 1.25f);
        set { _fontScale = Mathf.Clamp(Mathf.Round(value * 20f) / 20f, 0.8f, 2f); EditorPrefs.SetFloat(PrefFontScale, _fontScale.Value); }
    }
    public static int Fs(float px) => Mathf.RoundToInt(px * FontScale);

    void ChangeFontScale(float delta)
    {
        FontScale += delta;
        CreateGUI();   // สร้าง UI ใหม่ทั้งหน้า — สถานะ (config/เฟส/คลิปที่เลือก) อยู่ใน field ไม่หาย
    }

    void OnEnable()
    {
        Undo.undoRedoPerformed   += OnUndoRedo;
        EditorApplication.update += TickPlayback;
    }

    void OnDisable()
    {
        Undo.undoRedoPerformed   -= OnUndoRedo;
        EditorApplication.update -= TickPlayback;
        isPlaying = false;
    }

    // ── Playback ──────────────────────────────────────────────────────────
    void TogglePlay()
    {
        if (timeline == null) return;
        isPlaying = !isPlaying;
        lastTick  = EditorApplication.timeSinceStartup;

        // กด ▶ ตอน playhead อยู่ท้ายเส้นแล้ว = เริ่มใหม่จากต้น ไม่ใช่เล่นแล้วหยุดทันที
        if (isPlaying && playhead >= timeline.GetTimelineDuration() - 0.01f) SetPlayhead(0f, snap: false);
        BuildTimelinePane();   // ปุ่ม ▶/⏸ เปลี่ยนหน้าตา
    }

    void TickPlayback()
    {
        if (!isPlaying) return;
        if (timeline == null) { isPlaying = false; return; }

        double now = EditorApplication.timeSinceStartup;
        float  dt  = (float)(now - lastTick) * playSpeed;
        lastTick = now;

        float end = Mathf.Max(timeline.GetTimelineDuration(), 0.1f);
        float t   = playhead + dt;
        if (t >= end)
        {
            if (playLoop) t %= end;
            else
            {
                isPlaying = false;
                SetPlayhead(end, snap: false);
                BuildTimelinePane();
                return;
            }
        }
        SetPlayhead(t, snap: false);
    }
    void OnUndoRedo() { RefreshAudit(); RebuildAll(); }

    public void SetConfig(BossEncounterConfig cfg)
    {
        config = cfg;
        phaseIndex = (cfg != null && cfg.phases != null && cfg.phases.Count > 0) ? 0 : -1;
        editingEnrage = false;
        timeline = ResolvePhaseTimeline();
        RefreshAudit();
        RebuildAll();
    }

    public void SetStandaloneTimeline(BossTimelineAction tl)
    {
        config = null;
        phaseIndex = -1;
        editingEnrage = false;
        timeline = tl;
        RebuildAll();
    }

    // ── Root UI ───────────────────────────────────────────────────────────
    public void CreateGUI()
    {
        var root = rootVisualElement;
        root.Clear();
        root.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);   // CreateGUI ถูกเรียกซ้ำตอนเปลี่ยนขนาดอักษร
        // ป้าย/ช่องที่ไม่ได้ตั้งขนาดเองรับค่านี้ต่อจาก root (ค่าปกติของ Editor = 12)
        root.style.fontSize = Fs(12);

        var toolbar = new VisualElement();
        toolbar.style.flexDirection   = FlexDirection.Row;
        toolbar.style.alignItems      = Align.Center;
        toolbar.style.paddingLeft     = 6;
        toolbar.style.paddingRight    = 6;
        toolbar.style.paddingTop      = 4;
        toolbar.style.paddingBottom   = 4;
        toolbar.style.borderBottomWidth = 1;
        toolbar.style.borderBottomColor = GridLine;

        configField = new ObjectField("Encounter Config")
        {
            objectType = typeof(BossEncounterConfig),
            allowSceneObjects = false,
            value = config,
        };
        configField.style.flexGrow = 1;
        configField.RegisterValueChangedCallback(evt => SetConfig(evt.newValue as BossEncounterConfig));
        toolbar.Add(configField);

        // ใช้โดยแมพ/ระดับไหน — แก้ config ตัวเดียวกระทบทุกที่ที่อ้าง (สำคัญเมื่อเริ่มมี Savage/Epic)
        usageLabel = new Label();
        usageLabel.style.marginLeft = 8;
        usageLabel.style.fontSize = Fs(10);
        usageLabel.style.opacity = 0.7f;
        toolbar.Add(usageLabel);

        // ผล BossConfigAudit ของ config นี้ — เดิมต้องไปรันจากเมนูแยก เลยไม่มีใครรันจนกว่าจะพัง
        auditButton = new Button(ShowAuditDetails);
        auditButton.style.marginLeft = 8;
        toolbar.Add(auditButton);

        // ── ทดสอบในเกม — เข้า Play Mode แล้วเกิดบอสด้วย config นี้ที่เฟสที่เลือก ──
        testButton = new Button(TestInGame);
        testButton.style.marginLeft = 8;
        testButton.tooltip = "เข้า Play Mode จาก MenuScene · เริ่ม host แล้วเข้าเกมแบบปุ่มเริ่มรัน · รอเกมเริ่ม · เกิดบอสด้วย config นี้ แล้วข้ามไปเฟสที่เลือก";
        toolbar.Add(testButton);

        var god = new Toggle("อมตะ") { value = EditorPrefs.GetBool(PrefGod, true) };
        god.tooltip = "ผู้เล่นไม่รับดาเมจระหว่างทดสอบ — ดูท่าได้นานโดยไม่ตาย";
        god.labelElement.style.minWidth = 30;
        god.RegisterValueChangedCallback(e => EditorPrefs.SetBool(PrefGod, e.newValue));
        toolbar.Add(god);

        variantButton = new Button(() =>
            CloneSwarm.EditorTools.BossDifficultyVariant.Open(config, created =>
            {
                RefreshAudit();
                SetConfig(created);
            })) { text = "สร้างเวอร์ชันความยาก…" };
        variantButton.tooltip = "ก๊อป config ทั้งชุด (รวม action ที่เป็นไฟล์แยก) + ตัวคูณ แล้วผูกเข้า MapData";
        variantButton.style.marginLeft = 4;
        toolbar.Add(variantButton);

        // โผล่เฉพาะเมื่อ config ยังมีคลิปชี้ไฟล์ท่านอกบอส (ของรุ่นแรก) — กดครั้งเดียวให้เป็นสำเนาทั้งหมด
        embedButton = new Button(EmbedExternal);
        embedButton.style.marginLeft = 4;
        embedButton.tooltip = "คลิปที่ชี้ไฟล์ท่านอกบอสนี้ แก้ในการ์ดแล้วไฟล์นั้นเปลี่ยน (บอสอื่นที่ใช้ไฟล์เดียวกันเปลี่ยนตาม)\n" +
                              "กดเพื่อก๊อปทุกตัวเข้ามาเป็นของบอสนี้ · คลิปที่แชร์ท่ากันภายในบอสยังแชร์กันเหมือนเดิม · ไฟล์ต้นทางไม่ถูกลบ";
        toolbar.Add(embedButton);

        toolbar.Add(BuildViewTierField());

        var fontDown = new Button(() => ChangeFontScale(-0.1f)) { text = "A−", tooltip = "ตัวอักษรเล็กลง" };
        var fontUp   = new Button(() => ChangeFontScale(+0.1f)) { text = "A+", tooltip = $"ตัวอักษรใหญ่ขึ้น (ตอนนี้ ×{FontScale:0.##})" };
        fontDown.style.marginLeft = 8;
        toolbar.Add(fontDown);
        toolbar.Add(fontUp);

        root.Add(toolbar);

        graphPane = new VisualElement();
        graphPane.style.borderBottomWidth = 1;
        graphPane.style.borderBottomColor = GridLine;
        // กันไม่ให้ bodyRow ที่ flexGrow=1 บีบแถวเฟสจนป้ายชื่อซ้อนกัน
        graphPane.style.flexShrink = 0;
        root.Add(graphPane);

        // timeline ซ้าย · inspector ของคลิปที่เลือกอยู่ขวา — จะได้ปรับค่าจบในหน้าต่างเดียว
        // ไม่ต้องเด้งไปมองหน้าต่าง Inspector ข้างนอก
        var bodyRow = new VisualElement();
        bodyRow.style.flexDirection = FlexDirection.Row;
        bodyRow.style.flexGrow = 1;

        // ท่าสำเร็จรูปเรียงตามสิ่งที่ผู้เล่นต้องทำ — แทนเมนู New/ชื่อคลาส
        palette = new CloneSwarm.EditorTools.BossPalette(preset =>
        {
            AddPresetClip(preset, Mathf.Max(0, TargetTrack()), playhead);
        });
        bodyRow.Add(palette);

        timelinePane = new VisualElement();
        timelinePane.style.flexGrow = 1;
        timelinePane.style.flexShrink = 1;
        timelinePane.style.minWidth = 0;   // ยอมให้หดได้ ไม่งั้นมันดันแผงขวาจนแบน
        bodyRow.Add(timelinePane);

        // พรีวิวอยู่นอก inspectorPane เพราะ BuildInspectorPane Clear() ทุกครั้งที่เลือกคลิป
        // ถ้าอยู่ข้างในจะถูกสร้างใหม่ทั้งตัวทุกคลิก เสียสถานะและกะพริบ
        var rightCol = new VisualElement();
        rightCol.style.width = Mathf.Round(330 * Mathf.Max(1f, FontScale));   // การ์ดแก้ด่วนกว้างตามตัวอักษร
        rightCol.style.minWidth = 260;
        rightCol.style.flexShrink = 0;
        rightCol.style.borderLeftWidth = 1;
        rightCol.style.borderLeftColor = GridLine;
        rightCol.style.paddingLeft = 4;
        rightCol.style.paddingRight = 4;

        arenaPreview = new CloneSwarm.EditorTools.ArenaPreview { RollSeed = previewRollSeed };
        // ลากแก้ในสนามแล้ว การ์ดกับความยาวคลิป/แถบความปลอดภัยต้องตาม
        arenaPreview.Edited = () => { BuildTimelinePane(); BuildInspectorPane(); };
        rightCol.Add(arenaPreview);

        var reroll = new Button(() =>
        {
            previewRollSeed++;
            arenaPreview.RollSeed = previewRollSeed;
            RefreshPreview();
            BuildTimelinePane();   // แถบความปลอดภัยคิดด้วย seed เดียวกัน
        })
        { text = "🎲 roll ใหม่" };
        reroll.tooltip = "สุ่มค่า roll ชุดใหม่ในพรีวิว — ดูว่าแพตเทิร์นเดียวกันออกมาได้กี่หน้าตา";
        reroll.style.marginBottom = 4;
        rightCol.Add(reroll);

        inspectorPane = new VisualElement();
        inspectorPane.style.flexGrow = 1;
        rightCol.Add(inspectorPane);
        bodyRow.Add(rightCol);

        root.Add(bodyRow);

        // ── คีย์ลัด ──
        // root รับคีย์ได้เมื่อมีโฟกัส · คลิกที่ timeline/กราฟเฟสแล้วคืนโฟกัสให้ root
        // (ไม่ดักทั้งหน้าต่าง ไม่งั้นคลิกช่องพิมพ์ในแผงขวาแล้วโฟกัสถูกแย่ง)
        root.focusable = true;
        root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        timelinePane.RegisterCallback<PointerDownEvent>(_ => root.Focus(), TrickleDown.TrickleDown);
        graphPane.RegisterCallback<PointerDownEvent>(_ => root.Focus(), TrickleDown.TrickleDown);

        // config รอด domain reload มาได้ (serialize) แต่ผลตรวจไม่รอด — ตรวจใหม่ตอนสร้าง UI
        RefreshAudit();
        RebuildAll();
    }

    /// <summary>
    /// Space เล่น/หยุด · ←/→ เลื่อนคลิป 0.1s (Shift = 1s) · Ctrl+D ก๊อป · Delete ลบ ·
    /// 1–9 เลือกเฟส · F ซูมพอดี · Home กลับต้นเส้น
    /// </summary>
    void OnKeyDown(KeyDownEvent e)
    {
        if (IsTyping(e.target as VisualElement)) return;

        bool ctrl = e.ctrlKey || e.commandKey;
        int  trackIdx = TrackOf(selectedClip);
        bool handled = true;

        switch (e.keyCode)
        {
            case KeyCode.Space: TogglePlay(); break;
            case KeyCode.Home:  SetPlayhead(0f); break;
            case KeyCode.F:     FitZoomToContent(); break;

            case KeyCode.UpArrow when e.altKey:
            case KeyCode.DownArrow when e.altKey:
                if (trackIdx < 0) { handled = false; break; }
                MoveSelectionTracks(e.keyCode == KeyCode.UpArrow ? -1 : 1);
                break;

            case KeyCode.LeftArrow:
            case KeyCode.RightArrow:
                if (trackIdx < 0) { handled = false; break; }
                NudgeSelection((e.shiftKey ? 1f : SnapStep) * (e.keyCode == KeyCode.LeftArrow ? -1f : 1f));
                break;

            case KeyCode.D when ctrl:
                if (trackIdx < 0) { handled = false; break; }
                DuplicateSelection();
                break;

            case KeyCode.C when ctrl: handled = CopySelection(); break;
            case KeyCode.X when ctrl:
                if (trackIdx < 0) { handled = false; break; }
                CutSelection();
                break;
            case KeyCode.V when ctrl:
                if (timeline == null) { handled = false; break; }
                PasteClipboard(playhead, TargetTrack());
                break;
            case KeyCode.A when ctrl: SelectAllInTimeline(); break;
            case KeyCode.Escape:
                if (selection.Count == 0 && selectedClip == null) { handled = false; break; }
                SelectOnly(null);
                break;

            case KeyCode.Delete:
            case KeyCode.Backspace:
                if (trackIdx < 0) { handled = false; break; }
                DeleteSelection();
                break;

            default:
                // 1–9 เลือกเฟส
                int n = e.keyCode - KeyCode.Alpha1;
                if (!ctrl && n >= 0 && n < 9 && config?.phases != null && n < config.phases.Count)
                    SelectPhase(n, enrage: false);
                else handled = false;
                break;
        }

        if (handled) e.StopPropagation();
    }

    /// <summary>กำลังพิมพ์ในช่องข้อความ/ตัวเลขอยู่ — คีย์ต้องเป็นของช่องนั้น ไม่ใช่คีย์ลัด</summary>
    static bool IsTyping(VisualElement ve)
    {
        for (; ve != null; ve = ve.parent)
        {
            if (ve is IMGUIContainer) return true;   // PropertyDrawer แบบ IMGUI (เช่น RollIdDrawer)
            string n = ve.GetType().Name;
            if (n.Contains("TextInput") || n.Contains("TextField") || n.EndsWith("Field`1") ||
                ve is UnityEngine.UIElements.FloatField || ve is IntegerField || ve is TextField)
                return true;
        }
        return false;
    }

    void RebuildAll()
    {
        if (graphPane == null || timelinePane == null) return;
        if (configField != null) configField.SetValueWithoutNotify(config);
        UpdateToolbarStatus();
        BuildGraphPane();
        BuildTimelinePane();
        BuildInspectorPane();
        RefreshPreview();
    }

    // ── Audit + usage ─────────────────────────────────────────────────────
    void RefreshAudit()
    {
        auditProblems.Clear();
        if (config == null) return;

        // Collect ไล่ทุก config ในโปรเจกต์ — กรองเอาเฉพาะของตัวนี้ (ทุก Problem ขึ้นต้นด้วยชื่อ config)
        string prefix = config.name + " ·";
        foreach (var p in CloneSwarm.EditorTools.BossConfigAudit.Collect(out _))
            if (p.where != null && (p.where == config.name || p.where.StartsWith(prefix)))
                auditProblems.Add(p);
    }

    void UpdateToolbarStatus()
    {
        if (auditButton != null)
        {
            auditButton.style.display = config != null ? DisplayStyle.Flex : DisplayStyle.None;
            int blocking = auditProblems.Count(p => p.blocking);
            int notes    = auditProblems.Count - blocking;
            auditButton.text = blocking > 0 ? $"⚠ {blocking} ปัญหา"
                             : notes > 0    ? $"✓ ผ่าน · {notes} หมายเหตุ"
                             : "✓ ผ่าน";
            auditButton.style.color = blocking > 0 ? new Color(1f, 0.55f, 0.35f) : new Color(0.55f, 0.9f, 0.65f);
            auditButton.tooltip = "ผลตรวจ config นี้ (BossConfigAudit) — คลิกเพื่อดูรายละเอียดและตรวจใหม่";
        }

        if (usageLabel != null) usageLabel.text = config != null ? DescribeUsage(config) : "";

        var show = config != null ? DisplayStyle.Flex : DisplayStyle.None;
        if (variantButton != null) variantButton.style.display = show;
        if (embedButton != null)
        {
            int ext = CloneSwarm.EditorTools.BossActionEmbed.CountExternal(config);
            embedButton.style.display = ext > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            embedButton.text = $"🔗 {ext} ท่าชี้ไฟล์นอก → แปลงเป็นของบอสนี้";
        }
        if (testButton != null)
        {
            testButton.style.display = show;
            testButton.text = $"▶ ทดสอบในเกม: เฟส {Mathf.Max(0, phaseIndex) + 1}";
            testButton.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
        }
    }

    /// <summary>
    /// เข้า Play Mode แล้วเกิดบอสด้วย config นี้ที่เฟสที่เลือกอยู่ — ฝั่งเกมคือ BossTestRunner
    /// ส่งคำขอผ่าน SessionState (อยู่รอดข้าม domain reload ตอนเข้า Play) และใช้ครั้งเดียว
    /// </summary>
    void EmbedExternal()
    {
        if (config == null) return;
        var log = new System.Text.StringBuilder();
        int n = CloneSwarm.EditorTools.BossActionEmbed.EmbedExternalActions(config, log, undo: true);
        Debug.Log($"[BossDesigner] แปลง {n} ท่าเป็นของ {config.name}\n{log}", config);
        RefreshAudit();
        RebuildAll();
    }

    void TestInGame()
    {
        if (config == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        AssetDatabase.SaveAssets();   // การ์ด/timeline ที่เพิ่งแก้ต้องอยู่บนดิสก์ก่อนเกมโหลด

        // เริ่มจากเมนู — NetworkManager อยู่ที่นั่น และ player ถูก spawn หลัง NGO โหลดซีนเกมเท่านั้น
        const string Scene = BossTestRunner.MenuScenePath;
        if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path != Scene)
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Scene);

        SessionState.SetString(BossTestRunner.KeyConfig, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(config)));
        SessionState.SetInt(BossTestRunner.KeyPhase, Mathf.Max(0, phaseIndex));
        SessionState.SetBool(BossTestRunner.KeyGod, EditorPrefs.GetBool(PrefGod, true));
        SessionState.SetInt(BossTestRunner.KeyTier, ViewTier.HasValue ? (int)ViewTier.Value : -1);
        EditorApplication.EnterPlaymode();
    }

    void ShowAuditDetails()
    {
        RefreshAudit();
        UpdateToolbarStatus();
        BuildGraphPane();

        string body = auditProblems.Count == 0
            ? "ไม่พบปัญหาใน config นี้"
            : string.Join("\n\n", auditProblems.Select(p => $"{(p.blocking ? "⚠" : "·")} {p.where}\n   {p.what}"));
        EditorUtility.DisplayDialog($"ตรวจ {config?.name}", body, "OK");
    }

    /// <summary>แมพ/ระดับที่ชี้มาที่ config นี้ — ไม่นับบอสที่ใช้ config จาก prefab ตรงๆ</summary>
    static string DescribeUsage(BossEncounterConfig cfg)
    {
        var uses = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:MapData"))
        {
            var map = AssetDatabase.LoadAssetAtPath<MapData>(AssetDatabase.GUIDToAssetPath(guid));
            if (map?.tiers == null) continue;
            foreach (var t in map.tiers)
            {
                if (t == null) continue;
                if (t.mainBossConfig == cfg) uses.Add($"{map.mapId}/{t.tier} (บอสใหญ่)");
                if (t.miniBossConfig == cfg) uses.Add($"{map.mapId}/{t.tier} (มินิ)");
            }
        }
        return uses.Count > 0 ? "ใช้โดย: " + string.Join(", ", uses)
                              : "ไม่มีแมพไหนอ้าง — ใช้ผ่าน prefab บอสเท่านั้น";
    }

    /// <summary>
    /// เกณฑ์ HP ของเฟสนี้ใช้ได้ไหม — สูตรเดียวกับ BossConfigAudit.CheckPhaseThresholds
    /// (BossController เปลี่ยนเฟสเมื่อ HP ≤ ค่าของเฟสปัจจุบัน · เฟสสุดท้ายไม่ใช้ค่านี้)
    /// </summary>
    string ThresholdProblem(int idx)
    {
        if (config?.phases == null || idx >= config.phases.Count - 1) return null;
        float t    = config.phases[idx].transitionHealthPct;
        float prev = idx > 0 ? config.phases[idx - 1].transitionHealthPct : 1f;
        if (t <= 0f)   return $"HP ≤ 0% — บอสตายก่อน Phase {idx + 2} จะมา";
        if (t >= prev) return $"ไม่ต่ำกว่าเฟสก่อน ({prev:P0}) — จะข้าม Phase {idx + 1} ทันที";
        return null;
    }

    /// <summary>ป้อน context ปัจจุบันให้แผนผังสนาม — เรียกทุกครั้งที่เฟส/playhead/คลิปเปลี่ยน</summary>
    void RefreshPreview()
        => arenaPreview?.SetContext(config, timeline, playhead, selectedClip);

    // ══════════════════════════════════════════════════════════════════════
    // Inspector ของคลิปที่เลือก (แผงขวา)
    // ══════════════════════════════════════════════════════════════════════
    /// <summary>ซูมให้ timeline ทั้งเส้นพอดีความกว้างที่มี</summary>
    void FitZoomToContent()
    {
        if (timeline == null || timelinePane == null) return;

        float avail = timelinePane.resolvedStyle.width - TrackHeaderW - 24f;
        if (avail <= 1f || float.IsNaN(avail)) return;

        float contentSec = Mathf.Max(timeline.GetTimelineDuration() + 10f, 30f);
        pxPerSec = Mathf.Clamp(avail / contentSec, MinPxPerSec, MaxPxPerSec);
        pendingScrollX = 0f;
        BuildTimelinePane();
    }

    void BuildInspectorPane()
    {
        if (inspectorPane == null) return;
        inspectorPane.Clear();

        // ไม่ได้เลือกคลิป แต่เลือกเฟสอยู่ → แก้ค่าของเฟสที่นี่ ไม่ต้องเด้งไป Inspector ของ config
        if (selectedClip?.action == null && config != null && phaseIndex >= 0 && phaseIndex < (config.phases?.Count ?? 0))
        {
            BuildPhaseInspector();
            return;
        }

        if (selectedClip?.action == null)
        {
            var hint = new Label("เลือกคลิปเพื่อแก้ค่าที่นี่");
            hint.style.marginTop = 8;
            hint.style.unityFontStyleAndWeight = FontStyle.Italic;
            hint.style.color = new Color(1f, 1f, 1f, 0.45f);
            hint.style.whiteSpace = WhiteSpace.Normal;
            inspectorPane.Add(hint);
            return;
        }

        var action = selectedClip.action;

        var title = new Label($"{action.GetType().Name}\n{action.name}");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.marginTop = 6;
        title.style.marginBottom = 2;
        title.style.whiteSpace = WhiteSpace.Normal;
        inspectorPane.Add(title);

        // เตือนตั้งแต่แรกว่าแก้แล้วกระทบใครบ้าง — เป็นกับดักหลักของ ScriptableObject ที่ใช้ร่วมกัน
        int  refs       = CountClipsUsing(action);
        bool ownedHere  = IsOwnedSubAsset(action);
        bool sharedHere = refs > 1;

        string msg;
        Color  msgCol;
        if (!ownedHere)
        {
            // ไฟล์ asset แยก — อาจถูกอ้างจาก timeline อื่น เฟสอื่น หรือบอสตัวอื่นที่เรามองไม่เห็นจากตรงนี้
            msg = sharedHere
                ? $"⚠ ไฟล์ asset แยก · ใช้ {refs} คลิปใน timeline นี้ และอาจถูกใช้ที่อื่นด้วย\nกด Make Unique ถ้าอยากแก้เฉพาะคลิปนี้"
                : "⚠ ไฟล์ asset แยก — อาจถูกใช้ที่อื่นด้วย\nกด Make Unique ถ้าอยากแก้เฉพาะคลิปนี้";
            msgCol = new Color(1f, 0.75f, 0.3f);
        }
        else if (sharedHere)
        {
            msg = $"⚠ ฝังอยู่ใน config นี้ แต่ใช้ร่วม {refs} คลิป — แก้แล้วเปลี่ยนทุกคลิป";
            msgCol = new Color(1f, 0.75f, 0.3f);
        }
        else
        {
            msg = "✓ เป็นของคลิปนี้ตัวเดียว แก้ได้อิสระ";
            msgCol = new Color(0.5f, 0.9f, 0.6f);
        }

        var badge = new Label(msg);
        badge.style.whiteSpace = WhiteSpace.Normal;
        badge.style.fontSize = Fs(10);
        badge.style.marginBottom = 4;
        badge.style.color = msgCol;
        inspectorPane.Add(badge);

        // FloatField อยู่ใน UnityEngine.UIElements — ไม่มีตัวคู่ใน UnityEditor.UIElements
        var startField = new UnityEngine.UIElements.FloatField("Start Time") { value = selectedClip.startTime };
        startField.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(timeline, "Edit Clip Start");
            selectedClip.startTime = Mathf.Max(0f, evt.newValue);
            EditorUtility.SetDirty(timeline);
            BuildTimelinePane();
        });
        inspectorPane.Add(startField);
        inspectorPane.Add(BuildClipTierRow(selectedClip));

        var scroll = new ScrollView();
        scroll.style.flexGrow = 1;

        // การ์ดแก้ด่วน — 5–6 ช่องที่แก้ทุกครั้ง พร้อมหน่วยที่คนเข้าใจ
        // Inspector เต็ม (~48 ช่อง) ถอยไปอยู่หลัง "ขั้นสูง" · ท่าที่ไม่มีการ์ดเปิด "ขั้นสูง" ให้เอง
        bool hasCard = BuildQuickCard(action, scroll);

        const string AdvancedPref = "CloneSwarm.BossDesigner.AdvancedOpen";
        var advanced = new Foldout { text = "ขั้นสูง — ทุกช่อง", value = !hasCard || EditorPrefs.GetBool(AdvancedPref, false) };
        advanced.style.marginTop = 6;
        if (hasCard) advanced.RegisterValueChangedCallback(e => EditorPrefs.SetBool(AdvancedPref, e.newValue));

        // แก้ radius / targetingMode / arenaDistanceScale แล้วเห็นผลในสนามทันที
        // — วงป้อนกลับที่สั้นที่สุดคือเหตุผลทั้งหมดที่พรีวิวอยู่ติดกับช่องแก้ค่า
        var inspector = new InspectorElement(action);
        inspector.TrackSerializedObjectValue(new SerializedObject(action), _ => RefreshPreview());
        advanced.Add(inspector);
        scroll.Add(advanced);

        inspectorPane.Add(scroll);
    }

    // ══════════════════════════════════════════════════════════════════════
    // การ์ดแก้ด่วน
    // ══════════════════════════════════════════════════════════════════════

    // ลำดับตรงกับ SpawnAoEActionBase.TargetingMode — enumValueIndex ใช้ index นี้ตรงๆ
    static readonly List<string> TargetLabels = new()
    {
        "ที่ตัวบอส", "สุ่มผู้เล่นหนึ่งคน", "ใต้ผู้เล่นทุกคน", "ผู้เล่นใกล้บอสที่สุด", "พิกัดตายตัว", "จุดในสนาม",
    };

    // ลำดับตรงกับ RandomAttackAction.RepeatPick
    static readonly List<string> RepeatPickLabels = new() { "หยิบครั้งเดียวใช้ทุกนัด", "หยิบใหม่ทุกนัด", "สลับตามลำดับในกอง" };

    // ลำดับตรงกับ SpawnAoEActionBase.AimMode
    static readonly List<string> AimLabels = new() { "หันหาผู้เล่นใกล้สุด", "มุมคงที่" };

    /// <summary>ชนิดทรงของท่า — GetAoEType เป็น protected จึงอ่านผ่าน reflection (แบบเดียวกับ BossPatternGeometry)</summary>
    static AoEType GetAoETypeOf(BossAction a)
    {
        var m = a?.GetType().GetMethod("GetAoEType",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        return m != null ? (AoEType)m.Invoke(a, null) : AoEType.Circle;
    }

    /// <summary>ช่องขนาดของแต่ละทรง — ชื่อเดียวกับที่ ArenaPreview/TelegraphZone อ่าน</summary>
    static readonly (string field, string label)[] SizeFields =
    {
        ("radius", "รัศมี"), ("innerRadius", "รัศมีใน (ที่ปลอดภัย)"),
        ("lineLength", "ความยาว"), ("lineWidth", "ความกว้าง"), ("coneAngle", "มุมพัด"),
    };

    /// <summary>คืน false ถ้าท่านี้ไม่มีการ์ด (ให้เปิด "ขั้นสูง" แทน)</summary>
    bool BuildQuickCard(BossAction action, VisualElement into)
    {
        var so   = new SerializedObject(action);
        var card = new VisualElement();
        card.style.backgroundColor = new Color(1f, 1f, 1f, 0.04f);
        card.style.paddingLeft = card.style.paddingRight = 6;
        card.style.paddingTop = card.style.paddingBottom = 4;
        card.style.marginTop = 4;
        card.style.borderTopLeftRadius = card.style.borderTopRightRadius = 4;
        card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 4;

        if (action is SpawnAoEActionBase)
        {
            // ── ที่ไหน — คำแทนชื่อ enum ──
            var mode = so.FindProperty("targetingMode");
            var where = new PopupField<string>("ที่ไหน", TargetLabels,
                Mathf.Clamp(mode.enumValueIndex, 0, TargetLabels.Count - 1));
            where.RegisterValueChangedCallback(e =>
            {
                so.Update();
                mode.enumValueIndex = TargetLabels.IndexOf(e.newValue);
                so.ApplyModifiedProperties();
                RefreshPreview();
                // ช่องจุดในสนามโผล่/หายตามโหมด — สร้างการ์ดใหม่หลังจบ event นี้
                inspectorPane.schedule.Execute(BuildInspectorPane);
            });
            card.Add(where);

            if (mode.enumValueIndex == (int)SpawnAoEActionBase.TargetingMode.ArenaAnchor)
            {
                card.Add(new PropertyField(so.FindProperty("arenaAnchor"), "จุดในสนาม"));
                card.Add(new PropertyField(so.FindProperty("arenaDistanceScale"), "ห่างจากกลาง (1 = ขอบ)"));
                if (config != null && config.arena == null)
                    card.Add(Note("⚠ config นี้ยังไม่ได้ผูก arena — ตอนรันวงจะไปโผล่ที่ตัวบอส", 1f, new Color(1f, 0.55f, 0.35f)));
            }

            // ── ขนาด — เมตร + % ของสนาม ──
            foreach (var (field, label) in SizeFields)
            {
                var p = so.FindProperty(field);
                if (p == null) continue;
                card.Add(new PropertyField(p, label));
                if (field != "coneAngle") card.Add(SizeHint(p));
            }

            // ── ทิศ — Line/Cone หันหาผู้เล่นหรือมุมคงที่ · Cross มุมอย่างเดียว ──
            var aoeType = GetAoETypeOf(action);
            if (aoeType == AoEType.Line || aoeType == AoEType.Cone)
            {
                var aim = so.FindProperty("aimMode");
                var aimField = new PopupField<string>("ทิศ", AimLabels, Mathf.Clamp(aim.enumValueIndex, 0, AimLabels.Count - 1));
                aimField.RegisterValueChangedCallback(e =>
                {
                    so.Update();
                    aim.enumValueIndex = AimLabels.IndexOf(e.newValue);
                    so.ApplyModifiedProperties();
                    RefreshPreview();
                    inspectorPane.schedule.Execute(BuildInspectorPane);   // ช่องมุมโผล่/หาย
                });
                card.Add(aimField);
                if (aim.enumValueIndex == (int)SpawnAoEActionBase.AimMode.FixedAngle)
                    card.Add(new PropertyField(so.FindProperty("angleDegrees"), "มุม (° · 0 = เหนือ · ตามเข็ม)"));
            }
            else if (aoeType == AoEType.Cross)
                card.Add(new PropertyField(so.FindProperty("angleDegrees"), "มุม (° · 0 = + · 45 = ×)"));

            card.Add(new PropertyField(so.FindProperty("warningDuration"), "เตือนนาน (วินาที)"));
            var dmg = so.FindProperty("damage");
            card.Add(new PropertyField(dmg, "ดาเมจ"));
            card.Add(DamageHint(dmg));

            var repeat = so.FindProperty("repeatCount");
            card.Add(new PropertyField(repeat, "ซ้ำ (ครั้ง)"));
            if (repeat.intValue > 1)
                card.Add(new PropertyField(so.FindProperty("repeatInterval"), "ห่างกันทุก (วินาที)"));
        }
        else if (action is TetherAction)
        {
            card.Add(new PropertyField(so.FindProperty("mode"), "แบบโซ่"));
            card.Add(new PropertyField(so.FindProperty("tetherDistance"), "ระยะ"));
            card.Add(new PropertyField(so.FindProperty("tetherDuration"), "นาน (วินาที)"));
            card.Add(new PropertyField(so.FindProperty("tetherFailDamage"), "ดาเมจเมื่อพลาด"));
        }
        else if (action is RandomAttackAction ra)
        {
            var names = (ra.attackPool ?? new List<BossAction>()).Where(a => a != null).Select(a => a.name).ToList();
            card.Add(Note($"ในกอง ({names.Count}): {string.Join(" | ", names)}", 0.85f));
            card.Add(new PropertyField(so.FindProperty("attackCount"), "หยิบกี่ท่า"));
            if (ra.attackCount > 1)
                card.Add(new PropertyField(so.FindProperty("delayBetweenPicks"), "ห่างกัน (วินาที)"));

            // ── ซ้ำ + แต่ละนัดหยิบยังไง ──
            var repeatP = so.FindProperty("repeatCount");
            card.Add(new PropertyField(repeatP, "ซ้ำ (นัด)"));
            if (repeatP.intValue > 1)
            {
                card.Add(new PropertyField(so.FindProperty("repeatInterval"), "ห่างกันทุก (วินาที)"));
                var pickP = so.FindProperty("repeatPick");
                var pick = new PopupField<string>("แต่ละนัด", RepeatPickLabels,
                    Mathf.Clamp(pickP.enumValueIndex, 0, RepeatPickLabels.Count - 1));
                pick.RegisterValueChangedCallback(e =>
                {
                    so.Update();
                    pickP.enumValueIndex = RepeatPickLabels.IndexOf(e.newValue);
                    so.ApplyModifiedProperties();
                    RefreshPreview();
                    BuildTimelinePane();   // แถบปลอดภัยคิดตามนัด
                });
                card.Add(pick);
            }

            var selfRepeating = (ra.attackPool ?? new List<BossAction>())
                .OfType<SpawnAoEActionBase>().Where(a => a.repeatCount > 1).ToList();
            if (selfRepeating.Count > 0)
            {
                int inner = selfRepeating.Max(a => a.repeatCount);
                card.Add(Note($"⚠ ท่าในกองซ้ำเองอยู่แล้ว (×{inner}) — ทุกนัดของกองจะเป็นท่าเดียวกันทั้งชุด" +
                              (ra.repeatCount > 1 ? $" · รวม {ra.repeatCount * inner} นัด" : ""),
                              0.9f, new Color(1f, 0.75f, 0.35f)));
                var hoist = new Button(() => HoistRepeat(ra)) { text = "ย้ายการซ้ำมาไว้ที่ท่าสุ่ม" };
                hoist.tooltip = "กองซ้ำแทน · ท่าในกองเหลือยิงครั้งเดียว — แล้วเลือก \"แต่ละนัด\" ให้สลับหรือหยิบใหม่ได้\n" +
                                "ท่าในกองที่เฟส/คลิปอื่นใช้อยู่จะถูกก๊อปก่อน ของที่อื่นไม่เปลี่ยน";
                card.Add(hoist);
            }
            card.Add(Note("แก้ท่าในกอง: \"แยกท่าสุ่มกลับเป็นคลิป\" (คลิกขวา) หรือ \"ขั้นสูง\" ด้านล่าง", 0.55f));
        }
        else return false;

        // ช่องร่วมของทุกท่า — ความสุ่มเลือกเป็นคำ ระบบสร้าง Roll Definition ให้เอง
        card.Add(BuildRollChooser(action, so));

        card.Bind(so);
        card.TrackSerializedObjectValue(so, _ =>
        {
            RefreshPreview();
            // จำนวนซ้ำข้าม 1 → ช่อง "ห่างกันทุก" ต้องโผล่/หาย
            if ((repeatShown(card)) != (so.FindProperty("repeatCount")?.intValue > 1))
                inspectorPane.schedule.Execute(BuildInspectorPane);
        });
        into.Add(card);
        return true;

        static bool repeatShown(VisualElement c) =>
            c.Query<PropertyField>().Where(f => f.bindingPath == "repeatInterval").ToList().Count > 0;
    }

    // ── ความสุ่มในช่องเดียว ─────────────────────────────────────────────
    //
    // เดิมต้องไปเพิ่ม Roll Definition ใน config ก่อน แล้วกลับมาเลือกชื่อ — ระบบ roll ทั้งระบบ
    // จึงไม่มีท่าไหนใช้เลย · ตอนนี้เลือกเป็นคำ แล้วสร้าง definition ใน config ให้เอง
    // ("ใช้ค่าสุ่มร่วมกับ…" = ท่าหลายท่าหมุน/พลิกไปพร้อมกันด้วย roll ตัวเดียว)
    static readonly (string label, RollKind kind, int options)[] RollPresets =
    {
        ("พลิกซ้าย-ขวา",   RollKind.MirrorX,   2),
        ("พลิกหน้า-หลัง",  RollKind.MirrorZ,   2),
        ("หมุนทีละ 90°",   RollKind.SnapAngle, 4),
        ("หมุนทีละ 45°",   RollKind.SnapAngle, 8),
        ("เลือกมุมสนาม",   RollKind.Anchor,    4),
        ("เลือกผู้เล่น",    RollKind.Target,    4),
        ("สลับลำดับ",      RollKind.Order,     4),
    };

    VisualElement BuildRollChooser(BossAction action, SerializedObject so)
    {
        var box = new VisualElement();
        var rollProp = so.FindProperty("rollName");
        string cur = rollProp.stringValue ?? "";

        // ท่าสุ่มใช้ roll แบบ Variant (เลือกช่องในกอง) อย่างเดียว · ท่าอื่นใช้ roll เชิงพื้นที่ — Variant ไม่มีผลกับมัน
        bool isPool = action is RandomAttackAction;
        int poolCount = (action as RandomAttackAction)?.attackPool?.Count(a => a != null) ?? 0;
        var presets = isPool
            ? new[] { ("สลับท่าในกอง", RollKind.Variant, Mathf.Max(2, poolCount)) }
            : RollPresets;

        var choices = new List<string> { isPool ? "ไม่ผูก roll (สุ่มใหม่ทุกครั้ง)" : "ไม่สุ่ม" };
        choices.AddRange(presets.Select(r => r.Item1));
        var existing = (config?.rolls ?? new RollDefinition[0])
            .Where(r => r != null && !string.IsNullOrEmpty(r.rollName))
            .Where(r => isPool ? r.kind == RollKind.Variant : r.kind != RollKind.Variant || r.rollName == cur)
            .ToList();
        foreach (var r in existing) choices.Add($"ร่วมกับ: {r.rollName} ({r.kind} ×{r.optionCount})");

        int idx = 0;
        if (!string.IsNullOrEmpty(cur))
        {
            int e = existing.FindIndex(r => r.rollName == cur);
            idx = e >= 0 ? 1 + presets.Length + e : -1;
            if (idx < 0) { choices.Add($"'{cur}' (ไม่มีนิยาม)"); idx = choices.Count - 1; }
        }

        var field = new PopupField<string>("สุ่ม", choices, idx);
        field.tooltip = "แบบใหม่ = สร้าง Roll Definition ใน config ให้ · \"ร่วมกับ\" = หมุน/พลิกไปพร้อมท่าอื่นที่ใช้ roll ตัวเดียวกัน";
        field.RegisterValueChangedCallback(ev =>
        {
            int i = choices.IndexOf(ev.newValue);
            string name;
            if (i == 0) name = "";
            else if (i <= presets.Length) name = CreateRoll(action, presets[i - 1]);
            else if (i - 1 - presets.Length < existing.Count) name = existing[i - 1 - presets.Length].rollName;
            else return;   // ค่าผิดเดิม — เลือกแล้วไม่เปลี่ยน

            so.Update();
            rollProp.stringValue = name;
            so.ApplyModifiedProperties();
            RefreshAudit();
            UpdateToolbarStatus();
            inspectorPane.schedule.Execute(BuildInspectorPane);
            BuildTimelinePane();   // ป้าย 🎲 บนคลิป
        });
        box.Add(field);

        if (config == null)
            box.Add(Note("เปิดผ่าน Encounter Config เพื่อสร้าง roll ใหม่ได้", 0.55f));
        else if (!string.IsNullOrEmpty(cur))
        {
            box.Add(Note("กด 🎲 roll ใหม่ ใต้แผนผังสนามเพื่อดูแบบอื่น", 0.55f));
            var def = config.rolls?.FirstOrDefault(r => r?.rollName == cur);
            if (isPool && def != null && def.optionCount != poolCount)
                box.Add(Note($"⚠ roll มี {def.optionCount} ช่อง แต่กองมี {poolCount} ท่า — ช่องเกินวนกลับไปท่าแรก", 0.9f, new Color(1f, 0.75f, 0.35f)));
        }
        else if (isPool)
            box.Add(Note("ผูก roll = ท่าที่ออกล็อกต่อไฟต์ · ใช้ roll เดียวกับอีกกอง = ออกช่องเดียวกัน", 0.55f));
        return box;
    }

    /// <summary>สร้าง Roll Definition ใหม่ใน config ตั้งชื่อตามท่า · คืนชื่อที่สร้าง</summary>
    string CreateRoll(BossAction action, (string label, RollKind kind, int options) preset)
    {
        if (config == null) return "";
        Undo.RecordObject(config, "Add Roll");

        var list = (config.rolls ?? new RollDefinition[0]).ToList();
        string baseName = $"{action.name}_{preset.kind}".Replace(' ', '_');
        string name = baseName;
        for (int n = 2; list.Any(r => r?.rollName == name); n++) name = $"{baseName}_{n}";

        list.Add(new RollDefinition { rollName = name, kind = preset.kind, optionCount = preset.options });
        config.rolls = list.ToArray();
        EditorUtility.SetDirty(config);

        // roll แบบเลือกมุมสนามอ่านจาก anchorChoices — ว่างอยู่ใส่ 4 ทิศให้ก่อน ไม่งั้นสุ่มไม่มีอะไรให้เลือก
        if (preset.kind == RollKind.Anchor && action is SpawnAoEActionBase aoe
            && (aoe.anchorChoices == null || aoe.anchorChoices.Length == 0))
        {
            Undo.RecordObject(aoe, "Set Anchor Choices");
            aoe.anchorChoices = new[] { ArenaAnchor.N, ArenaAnchor.E, ArenaAnchor.S, ArenaAnchor.W };
            if (aoe.targetingMode != SpawnAoEActionBase.TargetingMode.ArenaAnchor)
                aoe.targetingMode = SpawnAoEActionBase.TargetingMode.ArenaAnchor;
            EditorUtility.SetDirty(aoe);
        }
        return name;
    }

    /// <summary>"= 20% ของ HP ผู้เล่น" — เทียบกับตัวละครที่ HP พื้นฐานต่ำสุด (คนที่ตายง่ายสุด)</summary>
    static float MinPlayerHp()
    {
        float min = float.MaxValue;
        foreach (var g in AssetDatabase.FindAssets("t:CharacterData"))
        {
            var c = AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(g));
            if (c != null && c.baseHealth > 0f) min = Mathf.Min(min, c.baseHealth);
        }
        return min == float.MaxValue ? 100f : min;
    }

    Label DamageHint(SerializedProperty p)
    {
        var hint = Note("", 0.55f);
        hint.style.marginTop = -2;
        float hp = MinPlayerHp();
        void Update(SerializedProperty prop) =>
            hint.text = $"= {prop.floatValue / hp:P0} ของ HP พื้นฐาน ({hp:0}) ของตัวละครที่ HP น้อยสุด · ก่อนเกราะ";
        Update(p);
        hint.TrackPropertyValue(p, Update);
        return hint;
    }

    /// <summary>"= 25% ของรัศมีสนาม" ใต้ช่องขนาด — ตัวเลขเมตรเฉยๆ บอกไม่ได้ว่าวงใหญ่แค่ไหนในสนาม</summary>
    Label SizeHint(SerializedProperty p)
    {
        var hint = Note("", 0.55f);
        hint.style.marginTop = -2;

        void Update(SerializedProperty prop)
        {
            float arena = config?.arena != null ? config.arena.radius : 0f;
            hint.text = arena > 0f
                ? $"= {prop.floatValue / arena:P0} ของรัศมีสนาม ({arena:0.#}m)"
                : "ผูก arena ใน config เพื่อเทียบกับขนาดสนาม";
        }

        Update(p);
        hint.TrackPropertyValue(p, Update);
        return hint;
    }

    /// <summary>
    /// ค่าของเฟสที่เลือก — เดิมต้องกด "Select Config in Inspector" แล้วไล่หาเฟสใน list
    /// และสร้าง enrage จากหน้าต่างไม่ได้เลย เพราะป้าย enrage โผล่เฉพาะเมื่อ enrageTime > 0 อยู่แล้ว
    /// </summary>
    void BuildPhaseInspector()
    {
        bool isLast = phaseIndex == config.phases.Count - 1;

        var title = new Label($"Phase {phaseIndex + 1}  ·  ค่าของเฟส");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.marginTop = 6;
        title.style.marginBottom = 4;
        inspectorPane.Add(title);

        var so    = new SerializedObject(config);
        var phase = so.FindProperty("phases").GetArrayElementAtIndex(phaseIndex);

        var scroll = new ScrollView();
        scroll.style.flexGrow = 1;

        string[] fields =
        {
            "transitionHealthPct", "invincibilityDuration", "attackInterval", "cameraShakeMagnitude",
            "announcement", "announcementColor", "phaseVfx", "enrageTime",
        };

        foreach (var name in fields)
        {
            var prop = phase.FindPropertyRelative(name);
            if (prop == null) continue;

            var field = new PropertyField(prop);
            scroll.Add(field);

            if (name == "transitionHealthPct")
            {
                if (isLast)
                {
                    field.SetEnabled(false);
                    scroll.Add(Note("เฟสสุดท้าย — ค่านี้ไม่ถูกใช้ (ไม่มีเฟสถัดไปให้เปลี่ยนไป)", 0.55f));
                }
                else
                {
                    string problem = ThresholdProblem(phaseIndex);
                    scroll.Add(problem != null
                        ? Note($"⚠ {problem}", 1f, new Color(1f, 0.55f, 0.35f))
                        : Note($"เปลี่ยนไป Phase {phaseIndex + 2} เมื่อ HP ≤ ค่านี้", 0.55f));
                }
            }

            if (name == "enrageTime")
            {
                // BossController ใช้ตัวจับเวลาแยกแล้ว (2026-09-24) — enrage ตัด timeline ที่เล่นอยู่
                // ตรงเวลานี้ · telegraph ที่วางไปแล้วยังระเบิดตามที่เตือน · ท่าที่ยังไม่ลงมือถูกทิ้ง
                scroll.Add(Note("-1 = ไม่มี enrage · นับจากตอนเข้าเฟส (หลังอมตะจบ) · ถึงเวลาแล้วตัดท่าปกติทันที " +
                                "วงที่เตือนไปแล้วยังระเบิดตามเดิม · ไม่มีท่า enrage = ลูปปกติเดินต่อ", 0.6f));
                if (config.phases[phaseIndex].enrageTime > 0f)
                {
                    var goEnrage = new Button(() => SelectPhase(phaseIndex, enrage: true)) { text = "แก้ท่า enrage →" };
                    goEnrage.style.alignSelf = Align.FlexStart;
                    scroll.Add(goEnrage);
                }
            }
        }

        scroll.Bind(so);
        // เกณฑ์ HP กับ enrage แสดงบนกราฟ — แก้แล้วให้กราฟตามทันที
        scroll.TrackSerializedObjectValue(so, _ => BuildGraphPane());

        inspectorPane.Add(scroll);
    }

    static Label Note(string text, float opacity, Color? color = null)
    {
        var l = new Label(text);
        l.style.whiteSpace = WhiteSpace.Normal;
        l.style.fontSize = Fs(10);
        l.style.opacity = opacity;
        l.style.marginBottom = 4;
        if (color.HasValue) l.style.color = color.Value;
        return l;
    }

    // ══════════════════════════════════════════════════════════════════════
    // Encounter Graph (ครึ่งบน)
    // ══════════════════════════════════════════════════════════════════════
    void BuildGraphPane()
    {
        graphPane.Clear();

        if (config == null)
        {
            if (timeline != null)
            {
                graphPane.Add(InfoLabel($"แก้ไข timeline เดี่ยว: {timeline.name}  (เลือก Encounter Config ด้านบนเพื่อเห็นภาพรวมเฟส)"));
            }
            else
            {
                graphPane.Add(InfoLabel("เลือก BossEncounterConfig ด้านบน หรือดับเบิลคลิก asset (Config / TimelineAction) เพื่อเริ่ม"));
            }
            return;
        }

        var scroll = new ScrollView(ScrollViewMode.Horizontal);
        scroll.style.paddingTop = 8;
        scroll.style.paddingBottom = 8;

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems    = Align.Center;
        row.style.paddingLeft   = 8;
        row.style.paddingRight  = 8;

        if (config.phases == null) config.phases = new List<BossPhase>();

        for (int i = 0; i < config.phases.Count; i++)
        {
            int idx = i;
            var phase = config.phases[i];
            row.Add(BuildPhaseNode(phase, idx));

            if (i < config.phases.Count - 1)
            {
                string problem = ThresholdProblem(i);
                var chip = ConditionChip($"HP ≤ {phase.transitionHealthPct:P0}");
                if (problem != null)
                {
                    chip.text += "  ⚠";
                    chip.tooltip = problem;
                    chip.style.color = new Color(1f, 0.6f, 0.4f);
                    chip.style.backgroundColor = new Color(0.7f, 0.25f, 0.1f, 0.35f);
                }
                row.Add(chip);
                row.Add(ArrowLabel());
            }
        }

        var addBtn = new Button(AddPhase) { text = "+ Phase" };
        addBtn.style.marginLeft = 10;
        row.Add(addBtn);

        // ── ตัวเลขสมมติสำหรับประมาณเวลาเล่นต่อเฟส (จำต่อเครื่อง) ──
        // config ไม่รู้ HP บอส (อยู่บน prefab) และ DPS ทีมขึ้นกับบิลด์ — ให้คนออกแบบตั้งสมมติฐานเอง
        var hp = new UnityEngine.UIElements.FloatField("HP บอส") { value = EditorPrefs.GetFloat(PrefBossHp, 20000f) };
        var dps = new UnityEngine.UIElements.FloatField("DPS ทีม") { value = EditorPrefs.GetFloat(PrefTeamDps, 400f) };
        foreach (var f in new[] { hp, dps })
        {
            f.style.width = Mathf.Round(130 * Mathf.Max(1f, FontScale));
            f.style.marginLeft = 12;
            f.labelElement.style.minWidth = 50;
            f.isDelayed = true;
        }
        hp.RegisterValueChangedCallback(e => { EditorPrefs.SetFloat(PrefBossHp, Mathf.Max(1f, e.newValue)); BuildGraphPane(); });
        dps.RegisterValueChangedCallback(e => { EditorPrefs.SetFloat(PrefTeamDps, Mathf.Max(1f, e.newValue)); BuildGraphPane(); });
        row.Add(hp);
        row.Add(dps);

        scroll.Add(row);
        graphPane.Add(scroll);
    }

    VisualElement BuildPhaseNode(BossPhase phase, int idx)
    {
        bool isSelected = idx == phaseIndex;
        var node = new VisualElement();
        node.style.backgroundColor = NodeBg;
        node.style.borderTopLeftRadius = node.style.borderTopRightRadius = 6;
        node.style.borderBottomLeftRadius = node.style.borderBottomRightRadius = 6;
        SetBorder(node, isSelected && !editingEnrage ? NodeSelected : new Color(1f, 1f, 1f, 0.18f), isSelected && !editingEnrage ? 2 : 1);
        node.style.paddingLeft = node.style.paddingRight = 10;
        node.style.paddingTop = node.style.paddingBottom = 6;
        node.style.marginLeft = node.style.marginRight = 4;
        node.style.minWidth = 130;

        var title = new Label($"Phase {idx + 1}");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        node.Add(title);

        var tl = GetListTimeline(phase.actions);
        string sub = tl != null
            ? $"⏱ {tl.name}  ·  {CountClips(tl)} clips / {tl.GetTimelineDuration():0.#}s"
            : $"{(phase.actions?.Count(a => a != null) ?? 0)} actions (legacy list)";
        var subLabel = new Label(sub);
        subLabel.style.fontSize = Fs(10);
        subLabel.style.opacity = 0.7f;
        node.Add(subLabel);

        string est = EstimatePhase(idx, tl);
        if (est != null)
        {
            var estLabel = new Label(est);
            estLabel.style.fontSize = Fs(10);
            estLabel.style.opacity = 0.55f;
            estLabel.tooltip = "ประมาณจาก HP บอส × ช่วง HP ของเฟส ÷ DPS ทีม (ตั้งที่ท้ายแถวเฟส) · ไม่นับช่วงอมตะ";
            node.Add(estLabel);
        }

        // Enrage chip — คลิกเพื่อแก้ enrage timeline ของเฟสนี้
        if (phase.enrageTime > 0f)
        {
            bool enrageSelected = isSelected && editingEnrage;
            var enrage = new Label($"enrage {phase.enrageTime:0}s →");
            enrage.style.fontSize = Fs(10);
            enrage.style.marginTop = 3;
            enrage.style.color = enrageSelected ? Color.white : new Color(1f, 0.55f, 0.35f);
            enrage.style.backgroundColor = enrageSelected ? new Color(0.6f, 0.25f, 0.1f) : new Color(1f, 0.4f, 0.2f, 0.12f);
            enrage.style.paddingLeft = enrage.style.paddingRight = 5;
            enrage.style.borderTopLeftRadius = enrage.style.borderTopRightRadius = 8;
            enrage.style.borderBottomLeftRadius = enrage.style.borderBottomRightRadius = 8;
            enrage.style.alignSelf = Align.FlexStart;
            enrage.RegisterCallback<ClickEvent>(e =>
            {
                SelectPhase(idx, enrage: true);
                e.StopPropagation();
            });
            node.Add(enrage);
        }

        node.RegisterCallback<ClickEvent>(e => SelectPhase(idx, enrage: false));
        node.RegisterCallback<ContextClickEvent>(e =>
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Select Config in Inspector"), false, () => Selection.activeObject = config);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent($"Delete Phase {idx + 1}"), false, () => DeletePhase(idx));
            menu.ShowAsContext();
            e.StopPropagation();
        });

        return node;
    }

    /// <summary>
    /// "~24s · ~2 รอบ" — เฟสนี้อยู่นานแค่ไหน และ timeline วนได้กี่รอบ
    /// ช่วง HP ของเฟส = เกณฑ์ของเฟสก่อน − เกณฑ์ของเฟสนี้ (เฟสสุดท้าย = ถึง 0)
    /// </summary>
    string EstimatePhase(int idx, BossTimelineAction tl)
    {
        if (config?.phases == null || idx >= config.phases.Count) return null;
        float bossHp  = EditorPrefs.GetFloat(PrefBossHp, 20000f);
        float teamDps = Mathf.Max(1f, EditorPrefs.GetFloat(PrefTeamDps, 400f));

        float top    = idx > 0 ? config.phases[idx - 1].transitionHealthPct : 1f;
        float bottom = idx < config.phases.Count - 1 ? config.phases[idx].transitionHealthPct : 0f;
        float frac   = Mathf.Max(0f, top - bottom);
        float secs   = frac * bossHp / teamDps;

        if (tl == null) return $"~{secs:0}s ({frac:P0} HP)";

        var ph = config.phases[idx];
        float gap = tl.cooldownAfter > 0f ? tl.cooldownAfter
                  : ph.attackInterval > 0f ? ph.attackInterval : config.attackInterval;
        float loop = Mathf.Max(0.1f, tl.GetTimelineDuration() + gap);
        return $"~{secs:0}s · ~{secs / loop:0.#} รอบ ({frac:P0} HP)";
    }

    void SelectPhase(int idx, bool enrage)
    {
        phaseIndex = idx;
        editingEnrage = enrage;
        selectedClip = null;
        pendingScrollX = 0f;   // คนละ timeline แล้ว เริ่มดูจากต้นเส้น
        timeline = ResolvePhaseTimeline();
        RebuildAll();
    }

    void AddPhase()
    {
        Undo.RecordObject(config, "Add Boss Phase");

        // เฟสสุดท้ายเดิมมักตั้ง 0% ไว้ (ค่านี้ไม่ถูกใช้ตอนเป็นเฟสสุดท้าย) · พอต่อเฟสใหม่ท้าย
        // มันกลายเป็นเกณฑ์จริง "HP ≤ 0%" → บอสตายก่อน เฟสใหม่ไม่มีวันมา
        // ตั้งให้ครึ่งทางระหว่างเกณฑ์ของเฟสก่อนหน้ากับ 0 (ปัด 5%) แล้วค่อยปรับเองใน Phase panel
        int n = config.phases.Count;
        if (n > 0)
        {
            var last = config.phases[n - 1];
            float prev = n >= 2 ? config.phases[n - 2].transitionHealthPct : 1f;
            if (last.transitionHealthPct <= 0f || last.transitionHealthPct >= prev)
            {
                float t = Mathf.Max(0.05f, Mathf.Round(prev * 0.5f * 20f) / 20f);
                Debug.Log($"[BossDesigner] Phase {n} เปลี่ยนเฟสที่ {last.transitionHealthPct:P0} → ตั้งเป็น {t:P0} " +
                          $"ไม่งั้น Phase {n + 1} ที่เพิ่งเพิ่มจะไปไม่ถึง", config);
                last.transitionHealthPct = t;
            }
        }

        config.phases.Add(new BossPhase
        {
            transitionHealthPct = 0f,
            invincibilityDuration = 1.5f,
            cameraShakeMagnitude = 0.4f,
            actions = new List<BossAction>(),
        });
        EditorUtility.SetDirty(config);
        SelectPhase(config.phases.Count - 1, enrage: false);
    }

    void DeletePhase(int idx)
    {
        if (!EditorUtility.DisplayDialog("Delete Phase",
            $"ลบ Phase {idx + 1} ออกจาก {config.name}?\n(BossAction assets ที่อ้างถึงจะไม่ถูกลบ)", "Delete", "Cancel"))
            return;

        Undo.RecordObject(config, "Delete Boss Phase");
        config.phases.RemoveAt(idx);
        EditorUtility.SetDirty(config);
        phaseIndex = Mathf.Clamp(phaseIndex, -1, config.phases.Count - 1);
        editingEnrage = false;
        timeline = ResolvePhaseTimeline();
        RebuildAll();
    }

    // ── Phase → timeline helpers ─────────────────────────────────────────
    List<BossAction> CurrentActionList()
    {
        if (config == null || phaseIndex < 0 || phaseIndex >= (config.phases?.Count ?? 0)) return null;
        var phase = config.phases[phaseIndex];
        if (editingEnrage)
        {
            if (phase.enrageActions == null) phase.enrageActions = new List<BossAction>();
            return phase.enrageActions;
        }
        if (phase.actions == null) phase.actions = new List<BossAction>();
        return phase.actions;
    }

    BossTimelineAction ResolvePhaseTimeline()
    {
        if (config == null) return timeline; // standalone mode
        return GetListTimeline(CurrentActionList());
    }

    static BossTimelineAction GetListTimeline(List<BossAction> list)
    {
        if (list == null) return null;
        var nonNull = list.Where(a => a != null).ToList();
        return nonNull.Count == 1 ? nonNull[0] as BossTimelineAction : null;
    }

    static int CountClips(BossTimelineAction tl) =>
        tl.tracks?.Sum(t => t?.clips?.Count(c => c?.action != null) ?? 0) ?? 0;

    // ══════════════════════════════════════════════════════════════════════
    // Timeline Editor (ครึ่งล่าง)
    // ══════════════════════════════════════════════════════════════════════
    void BuildTimelinePane()
    {
        // rebuild สร้าง ScrollView ใหม่ทุกครั้ง ตำแหน่ง scroll เลยหายทุกครั้ง
        // ถ้าไม่มีใครสั่งตำแหน่งไว้ ให้จำของเดิมไว้เอง ไม่งั้นแค่คลิกคลิปภาพก็เด้งไปต้นเส้น
        if (pendingScrollX < 0f && timelineScroll != null)
            pendingScrollX = timelineScroll.scrollOffset.x;

        timelineScroll = null;
        timelinePane.Clear();

        if (config != null && phaseIndex >= 0)
        {
            timeline = ResolvePhaseTimeline();
            if (timeline == null)
            {
                BuildLegacyListView();
                return;
            }
        }

        if (timeline == null)
        {
            timelinePane.Add(InfoLabel("ยังไม่มี timeline ให้แก้ — เลือกเฟสด้านบน หรือเปิด BossTimelineAction asset"));
            return;
        }

        SyncSelection();
        BuildTimelineToolbar();
        BuildTimelineBody();
    }

    // แสดงเฟสแบบเก่า (action list) + ปุ่มแปลงเป็น timeline
    void BuildLegacyListView()
    {
        var list = CurrentActionList();
        var box = new VisualElement();
        box.style.paddingLeft = box.style.paddingRight = 12;
        box.style.paddingTop = 12;

        string listName = editingEnrage ? "enrageActions" : "actions";
        box.Add(InfoLabel(list.Count(a => a != null) == 0
            ? $"Phase {phaseIndex + 1} ({listName}) ยังว่างอยู่"
            : $"Phase {phaseIndex + 1} ({listName}) เป็น list แบบเก่า — บอสวนยิงทีละท่าเว้นด้วย attackInterval"));

        if (list.Count(a => a != null) > 0)
        {
            var chipRow = new VisualElement();
            chipRow.style.flexDirection = FlexDirection.Row;
            chipRow.style.flexWrap = Wrap.Wrap;
            chipRow.style.marginTop = 6;
            int order = 1;
            foreach (var a in list.Where(a => a != null))
                chipRow.Add(ConditionChip($"{order++}. {a.name} ({a.GetEditorDuration():0.#}s)"));
            box.Add(chipRow);
        }

        var convert = new Button(ConvertCurrentListToTimeline)
        {
            text = list.Count(a => a != null) == 0 ? "สร้าง Timeline เปล่า" : "แปลงเป็น Timeline",
        };
        convert.style.marginTop = 10;
        convert.style.alignSelf = Align.FlexStart;
        box.Add(convert);

        var hint = new Label("การแปลงจะวางท่าเดิมเรียงต่อกันบน track เดียว (เว้นระยะตาม interval เดิม) — ลากปรับได้ทีหลัง");
        hint.style.fontSize = Fs(10);
        hint.style.opacity = 0.6f;
        hint.style.marginTop = 4;
        box.Add(hint);

        timelinePane.Add(box);
    }

    void ConvertCurrentListToTimeline()
    {
        var list = CurrentActionList();
        var phase = config.phases[phaseIndex];

        var tl = CreateInstance<BossTimelineAction>();
        tl.name = $"Phase{phaseIndex + 1}{(editingEnrage ? "_Enrage" : "")}_Timeline";
        var track = new BossTimelineAction.TimelineTrack { trackName = "Main" };

        float interval = phase.attackInterval > 0f ? phase.attackInterval
                       : (config.attackInterval > 0f ? config.attackInterval : 3f);
        float t = 0f;
        foreach (var a in list.Where(a => a != null))
        {
            track.clips.Add(new BossTimelineAction.TimelineClip { action = a, startTime = t });
            float cd = a.cooldownAfter > 0f ? a.cooldownAfter : interval;
            t += a.GetEditorDuration() + cd;
        }
        tl.tracks.Add(track);

        // เก็บ timeline เป็น sub-asset ใน config เดียวกัน จะได้ไม่หลงหาย
        Undo.RegisterCreatedObjectUndo(tl, "Create Boss Timeline");
        AssetDatabase.AddObjectToAsset(tl, config);

        Undo.RecordObject(config, "Convert Phase to Timeline");
        list.Clear();
        list.Add(tl);
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();

        timeline = tl;
        RebuildAll();
    }

    void BuildTimelineToolbar()
    {
        var bar = new VisualElement();
        bar.style.flexDirection = FlexDirection.Row;
        bar.style.alignItems = Align.Center;
        bar.style.paddingLeft = bar.style.paddingRight = 8;
        bar.style.paddingTop = bar.style.paddingBottom = 4;
        bar.style.borderBottomWidth = 1;
        bar.style.borderBottomColor = GridLine;

        string ctx = config != null && phaseIndex >= 0
            ? $"Phase {phaseIndex + 1}{(editingEnrage ? " (Enrage)" : "")} — "
            : "";
        var name = new Label($"{ctx}{timeline.name}");
        name.style.unityFontStyleAndWeight = FontStyle.Bold;
        bar.Add(name);

        var dur = new Label($"   ⏱ {timeline.GetTimelineDuration():0.0}s");
        dur.style.opacity = 0.7f;
        bar.Add(dur);

        var spacer = new VisualElement();
        spacer.style.flexGrow = 1;
        bar.Add(spacer);

        // ── ▶ เล่นพรีวิว ──
        var play = new Button(TogglePlay) { text = isPlaying ? "⏸" : "▶" };
        play.tooltip = "เล่น/หยุดพรีวิว (Space) — แผนผังสนามตาม playhead";
        play.style.minWidth = 28;
        bar.Add(play);

        var loop = new Button(() => { playLoop = !playLoop; BuildTimelinePane(); }) { text = "⟲" };
        loop.tooltip = playLoop ? "วนซ้ำ: เปิด" : "วนซ้ำ: ปิด";
        loop.style.opacity = playLoop ? 1f : 0.4f;
        bar.Add(loop);

        var speed = new Button(() =>
        {
            int i = System.Array.IndexOf(PlaySpeeds, playSpeed);
            playSpeed = PlaySpeeds[(i + 1) % PlaySpeeds.Length];
            BuildTimelinePane();
        }) { text = $"{playSpeed:0.#}×" };
        speed.tooltip = "ความเร็วพรีวิว";
        speed.style.marginRight = 8;
        bar.Add(speed);

        var playheadLabel = new Label($"t = {playhead:0.0}s");
        playheadLabel.name = "playhead-label";
        playheadLabel.style.marginRight = 12;
        playheadLabel.style.opacity = 0.8f;
        bar.Add(playheadLabel);

        var zoom = new Slider(MinPxPerSec, MaxPxPerSec) { value = pxPerSec };
        zoom.style.width = 120;
        zoom.tooltip = "Zoom (px ต่อวินาที) — หรือ Ctrl+ล้อเมาส์บน timeline · ล้อเปล่า = เลื่อนแนวนอน";
        zoom.RegisterValueChangedCallback(e =>
        {
            pxPerSec = Mathf.Clamp(e.newValue, MinPxPerSec, MaxPxPerSec);
            BuildTimelinePane();
        });
        bar.Add(zoom);

        var fit = new Button(FitZoomToContent) { text = "Fit" };
        fit.tooltip = "ซูมให้เห็น timeline ทั้งเส้นพอดีหน้าต่าง";
        fit.style.marginLeft = 4;
        fit.style.marginRight = 8;
        bar.Add(fit);

        if (config != null)
        {
            var clean = new Button(CleanUnusedActions) { text = "Clean Unused" };
            clean.tooltip = "ลบ action ที่ฝังอยู่ใน config นี้แต่ไม่มีคลิปหรือเฟสไหนอ้างถึงแล้ว";
            clean.style.marginRight = 6;
            bar.Add(clean);
        }

        var addTrack = new Button(() =>
        {
            Undo.RecordObject(timeline, "Add Track");
            timeline.tracks.Add(new BossTimelineAction.TimelineTrack { trackName = $"Track {timeline.tracks.Count + 1}" });
            EditorUtility.SetDirty(timeline);
            BuildTimelinePane();
        }) { text = "+ Track" };
        bar.Add(addTrack);

        var inspect = new Button(() => Selection.activeObject = timeline) { text = "Inspector" };
        bar.Add(inspect);

        timelinePane.Add(bar);
    }

    void BuildTimelineBody()
    {
        if (timeline.tracks == null) timeline.tracks = new List<BossTimelineAction.TimelineTrack>();
        if (timeline.tracks.Count == 0)
            timeline.tracks.Add(new BossTimelineAction.TimelineTrack { trackName = "Main" });

        float contentSec = Mathf.Max(timeline.GetTimelineDuration() + 10f, 30f);
        float contentW   = contentSec * pxPerSec;

        var body = new VisualElement();
        body.style.flexDirection = FlexDirection.Row;
        body.style.flexGrow = 1;

        // ── คอลัมน์ header ของ track (ฝั่งซ้าย ไม่เลื่อนตามแนวนอน) ──
        var headerCol = new VisualElement();
        headerCol.style.width = TrackHeaderW;
        headerCol.style.flexShrink = 0;
        headerCol.style.borderRightWidth = 1;
        headerCol.style.borderRightColor = GridLine;

        var rulerSpacer = new VisualElement();
        rulerSpacer.style.height = RulerH;
        rulerSpacer.style.borderBottomWidth = 1;
        rulerSpacer.style.borderBottomColor = GridLine;
        headerCol.Add(rulerSpacer);

        for (int i = 0; i < timeline.tracks.Count; i++)
            headerCol.Add(BuildTrackHeader(i));

        var safetyHeader = new Label("ปลอดภัย");
        safetyHeader.tooltip = "% พื้นที่สนามที่ไม่มีวงทับ ณ แต่ละช่วงเวลา · เขียว = หลบสบาย · เหลือง < 30% · แดง < 10% · แดงเข้ม = ไม่มีที่หลบ";
        safetyHeader.style.height = SafetyRowH;
        safetyHeader.style.fontSize = Fs(10);
        safetyHeader.style.opacity = 0.7f;
        safetyHeader.style.paddingLeft = 6;
        safetyHeader.style.unityTextAlign = TextAnchor.MiddleLeft;
        headerCol.Add(safetyHeader);

        body.Add(headerCol);

        // ── ฝั่งขวา: ruler + lanes ใน ScrollView แนวนอน ──
        var scroll = new ScrollView(ScrollViewMode.Horizontal);
        scroll.style.flexGrow = 1;
        timelineScroll = scroll;

        var content = new VisualElement();
        content.style.width = contentW;
        content.style.flexShrink = 0;

        HookMarquee(content);
        content.Add(BuildRuler(contentSec));

        for (int i = 0; i < timeline.tracks.Count; i++)
            content.Add(BuildLane(i, contentSec));

        content.Add(BuildSafetyRow());

        // playhead line ทับทุกเลน
        var ph = new VisualElement { name = "playhead-line" };
        ph.style.position = Position.Absolute;
        ph.style.top = 0;
        ph.style.bottom = 0;
        ph.style.left = playhead * pxPerSec;
        ph.style.width = 2;
        ph.style.backgroundColor = PlayheadCol;
        ph.pickingMode = PickingMode.Ignore;
        content.Add(ph);

        scroll.Add(content);

        // ── ล้อเมาส์ ─────────────────────────────────────────────────────
        // ล้อเปล่า = เลื่อนแนวนอน · Ctrl (หรือ Cmd) + ล้อ = ซูม
        scroll.RegisterCallback<WheelEvent>(e =>
        {
            if (!(e.ctrlKey || e.commandKey))
            {
                // จัดการเองแทนที่จะพึ่ง ScrollView map ล้อแนวตั้ง → เลื่อนแนวนอนให้
                // จะได้ระยะเลื่อนคงที่เป็นพิกเซลไม่ว่าซูมอยู่ระดับไหน
                scroll.scrollOffset = new Vector2(
                    Mathf.Max(0f, scroll.scrollOffset.x + e.delta.y * 50f),
                    scroll.scrollOffset.y);
                e.StopPropagation();
                return;
            }

            float before = pxPerSec;
            float after  = Mathf.Clamp(before * Mathf.Pow(ZoomStep, -e.delta.y), MinPxPerSec, MaxPxPerSec);
            if (Mathf.Approximately(before, after)) { e.StopPropagation(); return; }

            // ตรึงเวลาที่อยู่ใต้เคอร์เซอร์ให้ค้างที่เดิม ไม่งั้นซูมแล้วภาพไถลหนีมือ
            float localX       = scroll.WorldToLocal(e.mousePosition).x;
            float timeAtCursor = (scroll.scrollOffset.x + localX) / before;

            pxPerSec       = after;
            pendingScrollX = Mathf.Max(0f, timeAtCursor * after - localX);

            e.StopPropagation();
            // สร้างใหม่ทีหลัง — rebuild ต้นไม้ระหว่างที่ยัง dispatch event บนต้นไม้เดิมอยู่ไม่ปลอดภัย
            scroll.schedule.Execute(BuildTimelinePane);
        });

        // เรียกคืนตำแหน่ง scroll หลัง layout รู้ขนาดจริงแล้ว
        if (pendingScrollX >= 0f)
        {
            float restore = pendingScrollX;
            pendingScrollX = -1f;
            content.RegisterCallback<GeometryChangedEvent>(OnceRestoreScroll);

            void OnceRestoreScroll(GeometryChangedEvent _)
            {
                content.UnregisterCallback<GeometryChangedEvent>(OnceRestoreScroll);
                scroll.scrollOffset = new Vector2(restore, scroll.scrollOffset.y);
            }
        }

        body.Add(scroll);
        timelinePane.Add(body);
        RefreshTrackStyles();
    }

    // ── แถบความปลอดภัย ────────────────────────────────────────────────────
    const float SafetyRowH  = 16f;
    const float SafetyStep  = 0.25f;   // วินาทีต่อช่อง — ละเอียดพอเห็นจังหวะ ไม่หนักตอน rebuild

    /// <summary>
    /// % พื้นที่ที่ไม่มีวงทับตลอดเส้นเวลา — ถามคำถามเดียวกับแผนผังสนาม (BossPatternGeometry)
    /// ตัวเลขจึงตรงกับภาพเสมอ · roll ใช้ seed เดียวกับพรีวิว (กด 🎲 แล้วแถบเปลี่ยนตาม)
    /// </summary>
    VisualElement BuildSafetyRow()
    {
        var row = new VisualElement();
        row.style.height = SafetyRowH;
        row.style.flexShrink = 0;
        row.style.backgroundColor = new Color(0f, 0f, 0f, 0.15f);
        row.pickingMode = PickingMode.Ignore;

        float end = timeline.GetTimelineDuration();
        if (end <= 0f) return row;

        var shapes  = new List<CloneSwarm.EditorTools.BossPatternGeometry.Shape>();
        var players = new List<Vector3>();
        var arena   = CloneSwarm.EditorTools.BossPatternGeometry.ArenaOf(config);
        float worst = 1f, worstAt = 0f;

        for (float t = 0f; t < end; t += SafetyStep)
        {
            CloneSwarm.EditorTools.BossPatternGeometry.Collect(config, timeline, t + SafetyStep * 0.5f,
                                                               previewRollSeed, null, shapes, players);
            if (shapes.Count == 0) continue;
            float safe = CloneSwarm.EditorTools.BossPatternGeometry.SafeFraction(arena, shapes, 20);
            if (safe < worst) { worst = safe; worstAt = t; }

            var cell = new VisualElement();
            cell.style.position = Position.Absolute;
            cell.style.left = t * pxPerSec;
            cell.style.width = Mathf.Max(1f, SafetyStep * pxPerSec);
            cell.style.top = 2; cell.style.bottom = 2;
            cell.style.backgroundColor =
                safe <= 0.001f ? new Color(0.75f, 0.05f, 0.05f) :
                safe < 0.10f   ? new Color(0.95f, 0.3f, 0.2f) :
                safe < 0.30f   ? new Color(0.95f, 0.75f, 0.2f) :
                                 new Color(0.3f, 0.75f, 0.4f, 0.8f);
            cell.tooltip = $"t = {t:0.00}s · ปลอดภัย {safe:P0}";
            cell.pickingMode = PickingMode.Position;
            row.Add(cell);
        }

        row.tooltip = worst < 1f ? $"แย่สุดที่ t = {worstAt:0.0}s · ปลอดภัย {worst:P0}" : "ไม่มีวงบนเส้นเวลานี้";
        return row;
    }

    VisualElement BuildTrackHeader(int trackIdx)
    {
        var track = timeline.tracks[trackIdx];
        var header = new VisualElement();
        header.style.height = TrackH;
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.paddingLeft = 6;
        header.style.borderBottomWidth = 1;
        header.style.borderBottomColor = GridLine;
        trackHeaderEls.Add(header);
        // คลิกตรงไหนก็ได้บนหัวเลน (รวมช่องชื่อ) = เลนเป้าหมายของ Ctrl+V / แผงท่าสำเร็จรูป
        header.RegisterCallback<PointerDownEvent>(_ => SelectTrack(trackIdx), TrickleDown.TrickleDown);

        var nameField = new TextField { value = track.trackName, isDelayed = true };
        nameField.style.flexGrow = 1;
        nameField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(timeline, "Rename Track");
            track.trackName = e.newValue;
            EditorUtility.SetDirty(timeline);
        });
        header.Add(nameField);

        var addClip = new Button(() => ShowAddClipMenu(trackIdx, playhead)) { text = "+" };
        addClip.tooltip = "เพิ่มคลิปที่ตำแหน่ง playhead";
        header.Add(addClip);

        header.RegisterCallback<ContextClickEvent>(e =>
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("เพิ่มท่าที่ playhead…"), false, () => ShowAddClipMenu(trackIdx, playhead));
            menu.AddSeparator("");
            if (timeline.tracks.Count > 1)
                menu.AddItem(new GUIContent("Delete Track"), false, () =>
                {
                    Undo.RecordObject(timeline, "Delete Track");
                    timeline.tracks.RemoveAt(trackIdx);
                    EditorUtility.SetDirty(timeline);
                    BuildTimelinePane();
                });
            else
                menu.AddDisabledItem(new GUIContent("Delete Track"));
            menu.ShowAsContext();
        });

        return header;
    }

    VisualElement BuildRuler(float contentSec)
    {
        var ruler = new VisualElement();
        ruler.style.height = RulerH;
        ruler.style.borderBottomWidth = 1;
        ruler.style.borderBottomColor = GridLine;
        ruler.style.flexShrink = 0;

        int labelStep = pxPerSec >= 50f ? 1 : pxPerSec >= 20f ? 5 : 10;
        for (int s = 0; s <= Mathf.CeilToInt(contentSec); s++)
        {
            bool major = s % labelStep == 0;
            var tick = new VisualElement();
            tick.style.position = Position.Absolute;
            tick.style.left = s * pxPerSec;
            tick.style.bottom = 0;
            tick.style.width = 1;
            tick.style.height = major ? 10 : 5;
            tick.style.backgroundColor = new Color(1f, 1f, 1f, major ? 0.4f : 0.15f);
            tick.pickingMode = PickingMode.Ignore;
            ruler.Add(tick);

            if (major)
            {
                var lbl = new Label($"{s}s");
                lbl.style.position = Position.Absolute;
                lbl.style.left = s * pxPerSec + 3;
                lbl.style.top = 0;
                lbl.style.fontSize = Fs(9);
                lbl.style.opacity = 0.6f;
                lbl.pickingMode = PickingMode.Ignore;
                ruler.Add(lbl);
            }
        }

        // ลากบน ruler เพื่อเลื่อน playhead
        ruler.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0) return;
            ruler.CapturePointer(e.pointerId);
            SetPlayhead(e.localPosition.x / pxPerSec);
        });
        ruler.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (!ruler.HasPointerCapture(e.pointerId)) return;
            SetPlayhead(e.localPosition.x / pxPerSec);
        });
        ruler.RegisterCallback<PointerUpEvent>(e =>
        {
            if (ruler.HasPointerCapture(e.pointerId)) ruler.ReleasePointer(e.pointerId);
        });

        return ruler;
    }

    void SetPlayhead(float t, bool snap = true)
    {
        // ลากเอง snap 0.1s ให้อ่านง่าย · ตอนเล่นพรีวิวต้องไม่ snap ไม่งั้นภาพกระตุกเป็นขั้น
        playhead = Mathf.Max(0f, snap ? Mathf.Round(t / SnapStep) * SnapStep : t);
        var line = timelinePane.Q<VisualElement>("playhead-line");
        if (line != null) line.style.left = playhead * pxPerSec;
        var lbl = timelinePane.Q<Label>("playhead-label");
        if (lbl != null) lbl.text = $"t = {playhead:0.0}s";

        // ลากไม้บรรทัดคือทางหลักที่คนใช้ดูว่า "ตอนนี้มีอะไรอยู่ในสนาม" — ต้องตามทันที
        RefreshPreview();
    }

    VisualElement BuildLane(int trackIdx, float contentSec)
    {
        var track = timeline.tracks[trackIdx];
        var lane = new VisualElement();
        lane.style.height = TrackH;
        lane.style.backgroundColor = LaneBg;
        lane.style.borderBottomWidth = 1;
        lane.style.borderBottomColor = GridLine;
        lane.style.flexShrink = 0;
        laneEls.Add(lane);

        int gridStep = pxPerSec >= 50f ? 1 : pxPerSec >= 20f ? 5 : 10;
        for (int s = gridStep; s <= Mathf.CeilToInt(contentSec); s += gridStep)
        {
            var line = new VisualElement();
            line.style.position = Position.Absolute;
            line.style.left = s * pxPerSec;
            line.style.top = 0;
            line.style.bottom = 0;
            line.style.width = 1;
            line.style.backgroundColor = GridLine;
            line.pickingMode = PickingMode.Ignore;
            lane.Add(line);
        }

        if (track.clips == null) track.clips = new List<BossTimelineAction.TimelineClip>();
        foreach (var clip in track.clips)
        {
            if (clip?.action == null) continue;
            lane.Add(BuildClipElement(clip, trackIdx));
        }

        // คลิกพื้นเลนว่าง = ยกเลิกการเลือก + เลือกเลน · ลาก = กรอบเลือก · คลิกขวา = วางที่นี่
        lane.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button == 0 && e.target == lane) BeginMarqueePending(e, trackIdx);
        });
        lane.RegisterCallback<ContextClickEvent>(e =>
        {
            if (e.target != lane) return;
            SelectTrack(trackIdx);
            ShowLaneContextMenu(trackIdx, Snap(e.localMousePosition.x / pxPerSec, false));
            e.StopPropagation();
        });

        // ดับเบิลคลิกพื้นเลนว่าง = เพิ่มคลิปตรงนั้น
        lane.RegisterCallback<ClickEvent>(e =>
        {
            if (e.clickCount == 2 && e.target == lane)
                ShowAddClipMenu(trackIdx, Snap(e.localPosition.x / pxPerSec, false));
        });

        // รองรับลาก BossAction asset จาก Project มาวางบนเลน
        lane.RegisterCallback<DragUpdatedEvent>(e =>
        {
            if (DragAndDrop.objectReferences.OfType<BossAction>().Any(a => a != timeline))
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        });
        lane.RegisterCallback<DragPerformEvent>(e =>
        {
            var dropped = DragAndDrop.objectReferences.OfType<BossAction>().Where(a => a != timeline).ToList();
            if (dropped.Count == 0) return;
            DragAndDrop.AcceptDrag();
            float t = Snap(e.localMousePosition.x / pxPerSec, false);
            foreach (var a in dropped)
            {
                // ท่าสำเร็จรูปได้สำเนาเสมอ · asset อื่นที่ลากจาก Project = ตั้งใจแชร์ไฟล์นั้น
                if (CloneSwarm.EditorTools.BossPalette.IsPreset(a))
                    AddPresetClip(a, trackIdx, t);
                else
                {
                    Undo.RecordObject(timeline, "Add Timeline Clip");
                    track.clips.Add(new BossTimelineAction.TimelineClip { action = a, startTime = t });
                    EditorUtility.SetDirty(timeline);
                }
                t += 0.5f;
            }
            BuildTimelinePane();
        });

        return lane;
    }

    /// <summary>
    /// ไฮไลต์คลิปที่เลือก — ขอบขาวอย่างเดียวแยกไม่ออกเวลาคลิปเรียงติดกัน
    /// เลยเพิ่มการทำพื้นหลังสว่างขึ้นกับขอบหนาขึ้นด้วย
    /// </summary>
    static void ApplyClipSelectionStyle(VisualElement el, Color baseCol, bool selected)
    {
        if (el == null) return;

        el.style.backgroundColor = selected
            ? Color.Lerp(baseCol, Color.white, 0.35f)
            : baseCol;
        SetBorder(el,
            selected ? Color.white : new Color(0f, 0f, 0f, 0.5f),
            selected ? 3 : 1);
    }

    VisualElement BuildClipElement(BossTimelineAction.TimelineClip clip, int trackIdx)
    {
        float dur = Mathf.Max(clip.action.GetEditorDuration(), 0.3f);

        var el = new VisualElement();
        el.style.position = Position.Absolute;
        el.style.left = clip.startTime * pxPerSec;
        el.style.top = 4;
        el.style.height = TrackH - 9;
        el.style.width = Mathf.Max(dur * pxPerSec, 24f);
        el.style.borderTopLeftRadius = el.style.borderTopRightRadius = 3;
        el.style.borderBottomLeftRadius = el.style.borderBottomRightRadius = 3;

        Color baseCol = ClipColor(clip.action);
        ApplyClipSelectionStyle(el, baseCol, IsSelected(clip));
        clipEls[clip] = el;

        // ส่วน telegraph (สีอ่อนกว่า) สำหรับท่า AoE — เห็นชัดว่า warning นานแค่ไหน
        if (clip.action is SpawnAoEActionBase aoe && dur > 0f)
        {
            float frac = Mathf.Clamp01((aoe.actionDelay + aoe.warningDuration) / dur);
            var warn = new VisualElement();
            warn.style.position = Position.Absolute;
            warn.style.left = 0; warn.style.top = 0; warn.style.bottom = 0;
            warn.style.width = new Length(frac * 100f, LengthUnit.Percent);
            warn.style.backgroundColor = new Color(1f, 1f, 1f, 0.18f);
            warn.pickingMode = PickingMode.Ignore;
            el.Add(warn);
        }

        // roll ที่ท่านี้ใช้ — เดิมมองไม่เห็นจาก timeline เลย ระบบ roll จึงไม่มีใครใช้
        string roll = clip.action.rollName;
        string text = clip.action is RandomAttackAction pool
            ? $"🎲 {string.Join(" | ", (pool.attackPool ?? new List<BossAction>()).Where(a => a != null).Select(a => a.name))}"
            : string.IsNullOrEmpty(roll) ? clip.action.name : $"🎲{roll} · {clip.action.name}";
        var lbl = new Label(TierBadge(clip) + text);
        if (!ClipVisibleInView(clip)) el.style.opacity = 0.3f;   // ไม่ออกในระดับที่ดูอยู่
        lbl.style.fontSize = Fs(10);
        lbl.style.color = Color.white;
        lbl.style.marginLeft = 4;
        lbl.style.marginTop = 2;
        lbl.style.overflow = Overflow.Hidden;
        lbl.style.textOverflow = TextOverflow.Ellipsis;
        lbl.style.whiteSpace = WhiteSpace.NoWrap;
        lbl.pickingMode = PickingMode.Ignore;
        el.Add(lbl);
        AddResizeHandles(el, clip);   // ขอบซ้าย/ขวา ยืดหดเวลา (BossDesignerWindow.Resize.cs)

        el.tooltip = $"{clip.action.GetType().Name}\nstart {clip.startTime:0.0}s · ยาว ~{dur:0.0}s" +
                     (string.IsNullOrEmpty(roll) ? "" : $"\nroll: {roll}");

        // ── ลากเลื่อนเวลา (ทั้งกลุ่มที่เลือก) ──
        float dragStartT = 0f;
        float accum = 0f, accumY = 0f;
        int trackDelta = 0;
        List<(BossTimelineAction.TimelineClip clip, float start, int track)> group = null;
        el.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0) return;
            SelectTrack(trackIdx);

            // Ctrl/Shift+คลิก = เพิ่ม/เอาออกจากชุด ไม่ลาก
            if (e.ctrlKey || e.commandKey || e.shiftKey)
            {
                ToggleSelect(clip);
                e.StopPropagation();
                return;
            }

            // คลิกตัวที่อยู่ในชุดแล้ว = เตรียมลากทั้งชุด · ตัวนอกชุด = เลือกตัวเดียว
            if (!IsSelected(clip)) SelectOnly(clip);
            else if (selectedClip != clip)
            {
                selectedClip = clip;
                BuildInspectorPane();
                RefreshPreview();
            }

            Undo.RecordObject(timeline, "Move Timeline Clip");
            group = SelectedClips().Select(x => (x.clip, x.clip.startTime, x.track)).ToList();
            dragStartT = clip.startTime;
            accum = accumY = 0f;
            trackDelta = 0;
            el.CapturePointer(e.pointerId);
            el.BringToFront();
            e.StopPropagation();
        });
        el.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (!el.HasPointerCapture(e.pointerId) || group == null) return;
            accum += e.deltaPosition.x;
            float delta = Snap(dragStartT + accum / pxPerSec, e.altKey) - dragStartT;
            delta = Mathf.Max(delta, -group.Min(g => g.start));   // ตัวแรกชน 0 แล้วทั้งกลุ่มหยุด
            foreach (var (c, start, _) in group)
            {
                c.startTime = Mathf.Max(0f, start + delta);
                if (clipEls.TryGetValue(c, out var ce)) ce.style.left = c.startTime * pxPerSec;
            }

            // ขึ้น/ลงเลน — ปัดตามความสูงเลน · ทั้งชุดต้องอยู่ในช่วงเลนที่มี
            accumY += e.deltaPosition.y;
            trackDelta = ClampTrackDelta(group.Select(g => g.track), Mathf.RoundToInt(accumY / TrackH));
            UpdateDragGhosts(group, trackDelta);
            el.tooltip = $"{clip.action.GetType().Name}\nstart {clip.startTime:0.0}s · ยาว ~{dur:0.0}s";
        });
        el.RegisterCallback<PointerUpEvent>(e =>
        {
            if (!el.HasPointerCapture(e.pointerId)) return;
            el.ReleasePointer(e.pointerId);

            UpdateDragGhosts(group, 0);

            // แค่คลิกไม่ได้ลาก — คลิกตัวในชุดหลายตัว = เหลือตัวนั้นตัวเดียว (แบบโปรแกรมตัดต่อ)
            if (Mathf.Approximately(clip.startTime, dragStartT) && trackDelta == 0)
            {
                if (selection.Count > 1) SelectOnly(clip);
                return;
            }

            if (trackDelta != 0)
            {
                foreach (var (c, _, tr) in group)
                {
                    timeline.tracks[tr].clips.Remove(c);
                    timeline.tracks[tr + trackDelta].clips.Add(c);
                }
                SelectTrack(trackIdx + trackDelta);
            }

            EditorUtility.SetDirty(timeline);
            BuildTimelinePane(); // รีเฟรช duration/ความยาว ruler
        });

        el.RegisterCallback<ContextClickEvent>(e =>
        {
            ShowClipContextMenu(clip, trackIdx);
            e.StopPropagation();
        });

        return el;
    }

    void ShowClipContextMenu(BossTimelineAction.TimelineClip clip, int trackIdx)
    {
        var menu = new GenericMenu();
        if (!IsSelected(clip)) SelectOnly(clip);
        int n = SelectedClips().Count;
        string many = n > 1 ? $" {n} คลิป" : "";
        menu.AddItem(new GUIContent($"คัดลอก{many}  Ctrl+C"), false, () => CopySelection());
        menu.AddItem(new GUIContent($"ตัด{many}  Ctrl+X"), false, CutSelection);
        if (clipboard.Count > 0)
            menu.AddItem(new GUIContent($"วางที่ playhead ({clipboard.Count} คลิป)  Ctrl+V"), false, () => PasteClipboard(playhead, trackIdx));
        else
            menu.AddDisabledItem(new GUIContent("วาง — คลิปบอร์ดว่าง"));
        if (n > 1)
        {
            if (config != null)
                menu.AddItem(new GUIContent($"รวมเป็นท่าสุ่ม 🎲 ({n} ท่า)"), false, MergeSelectionToRandom);
            else
                menu.AddDisabledItem(new GUIContent("รวมเป็นท่าสุ่ม — ต้องเปิดผ่าน Encounter Config"));
        }
        if (clip.action is RandomAttackAction)
            menu.AddItem(new GUIContent("แยกท่าสุ่มกลับเป็นคลิป"), false, () => UnpackRandom(clip, trackIdx));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Ping Asset"), false, () => EditorGUIUtility.PingObject(clip.action));

        // ก๊อปแล้วได้ action ของตัวเองเป็นค่าเริ่มต้น (Ctrl+D) — เดิมก๊อปได้คลิปที่ชี้ action เดิม
        // แก้ตัวหนึ่งแล้วอีกตัวเปลี่ยนตาม ซึ่งเป็นกับดักหลักของ ScriptableObject ที่ใช้ร่วมกัน
        // แชร์ต้องตั้งใจเลือกเอง
        menu.AddItem(new GUIContent("Duplicate Clip  (Ctrl+D)"), false, () => DuplicateClip(clip, trackIdx, unique: true));
        menu.AddItem(new GUIContent("Duplicate Clip — แชร์ action เดิม 🔗"), false, () => DuplicateClip(clip, trackIdx, unique: false));

        // ตัวที่ให้ action อิสระจริงๆ
        if (config != null && clip.action != null)
        {
            menu.AddItem(new GUIContent("Make Unique"), false, () => MakeClipActionUnique(clip));
        }
        else
        {
            menu.AddDisabledItem(new GUIContent("Make Unique  (ต้องเปิดผ่าน Encounter Config)"));
        }

        if (config != null && IsOwnedSubAsset(clip.action))
            menu.AddItem(new GUIContent("Extract to Asset File"), false, () => ExtractToAssetFile(clip.action));

        menu.AddItem(new GUIContent("บันทึกเป็นท่าสำเร็จรูป…"), false, () =>
        {
            if (CloneSwarm.EditorTools.BossPalette.SaveAsPreset(clip.action)) palette?.Refresh();
        });

        for (int i = 0; i < timeline.tracks.Count; i++)
        {
            if (i == trackIdx) continue;
            int target = i;
            menu.AddItem(new GUIContent($"Move to Track/{timeline.tracks[i].trackName}"), false, () =>
            {
                Undo.RecordObject(timeline, "Move Clip to Track");
                timeline.tracks[trackIdx].clips.Remove(clip);
                timeline.tracks[target].clips.Add(clip);
                EditorUtility.SetDirty(timeline);
                BuildTimelinePane();
            });
        }

        menu.AddSeparator("");
        menu.AddItem(new GUIContent(n > 1 ? $"ลบ {n} คลิป  Delete" : "Delete Clip"), false, () => DeleteSelection());
        menu.ShowAsContext();
    }

    /// <summary>
    /// ลบคลิป · ถ้า action เป็น sub-asset ของ config นี้และไม่มีคลิปอื่นใช้แล้ว ให้ทำลายทิ้งด้วย
    ///
    /// ต่างจากการลบ GameObject ตรงที่ sub-asset **ไม่มีใครเป็นเจ้าของเชิงโครงสร้าง**
    /// ลบคลิปเฉยๆ แล้วมันจะค้างอยู่ใน config ตลอดไปแบบมองไม่เห็น สร้าง-ลบสิบรอบ config บวม
    /// </summary>
    void DeleteClip(BossTimelineAction.TimelineClip clip, int trackIdx)
    {
        var action = clip.action;

        Undo.RecordObject(timeline, "Delete Timeline Clip");
        timeline.tracks[trackIdx].clips.Remove(clip);
        if (selectedClip == clip) selectedClip = null;
        EditorUtility.SetDirty(timeline);

        // เช็คหลังลบคลิปแล้ว — เหลือ 0 การอ้างอิงถึงค่อยทำลาย
        if (IsOwnedSubAsset(action) && !IsReferencedInConfig(action))
        {
            Undo.DestroyObjectImmediate(action);
            AssetDatabase.SaveAssets();
        }

        BuildTimelinePane();
        BuildInspectorPane();
    }

    /// <summary>
    /// ก๊อปคลิปไปวางต่อท้ายตัวเดิม · unique = action ก๊อปเป็น sub-asset ของ config นี้
    /// (ต้องเปิดผ่าน config — timeline เดี่ยวไม่มีที่ฝัง จึงแชร์ action เดิมแทน)
    /// </summary>
    void DuplicateClip(BossTimelineAction.TimelineClip clip, int trackIdx, bool unique)
    {
        if (clip?.action == null || timeline == null) return;

        BossAction action = clip.action;
        if (unique && config != null)
        {
            action = Instantiate(clip.action);
            action.name = MakeUniqueSubAssetName(clip.action.name);
            Undo.RegisterCreatedObjectUndo(action, "Duplicate Clip");
            AssetDatabase.AddObjectToAsset(action, config);
        }

        Undo.RecordObject(timeline, "Duplicate Clip");
        var copy = new BossTimelineAction.TimelineClip
        {
            action    = action,
            startTime = Snap(clip.startTime + Mathf.Max(clip.action.GetEditorDuration(), 0.5f), false),
        };
        timeline.tracks[trackIdx].clips.Add(copy);
        EditorUtility.SetDirty(timeline);
        if (action != clip.action) AssetDatabase.SaveAssets();

        selectedClip = copy;
        BuildTimelinePane();
        BuildInspectorPane();
        RefreshPreview();
    }

    /// <summary>คลิปที่เลือกอยู่บนเลนไหน · -1 ถ้าไม่พบ</summary>
    int TrackOf(BossTimelineAction.TimelineClip clip)
    {
        if (clip == null || timeline?.tracks == null) return -1;
        for (int i = 0; i < timeline.tracks.Count; i++)
            if (timeline.tracks[i]?.clips != null && timeline.tracks[i].clips.Contains(clip)) return i;
        return -1;
    }

    /// <summary>ก๊อป action ของคลิปนี้เป็น sub-asset ใหม่ แล้วให้คลิปชี้ไปตัวก๊อป</summary>
    void MakeClipActionUnique(BossTimelineAction.TimelineClip clip)
    {
        if (config == null || clip.action == null) return;

        var copy = Instantiate(clip.action);
        copy.name = MakeUniqueSubAssetName(clip.action.name);
        Undo.RegisterCreatedObjectUndo(copy, "Make Clip Action Unique");
        AssetDatabase.AddObjectToAsset(copy, config);

        Undo.RecordObject(timeline, "Make Clip Action Unique");
        clip.action = copy;
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();

        selectedClip = clip;
        BuildTimelinePane();
        BuildInspectorPane();
    }

    /// <summary>สร้าง action ใหม่เป็น sub-asset แล้วเพิ่มเป็นคลิปทันที — เหมือนกด GameObject → Cube</summary>
    void CreateActionAndAddClip(System.Type type, int trackIdx, float atTime)
    {
        if (config == null || timeline == null) return;

        var action = ScriptableObject.CreateInstance(type) as BossAction;
        if (action == null) return;

        action.name = MakeUniqueSubAssetName(type.Name);
        Undo.RegisterCreatedObjectUndo(action, "Create Boss Action");
        AssetDatabase.AddObjectToAsset(action, config);

        Undo.RecordObject(timeline, "Add Timeline Clip");
        var clip = new BossTimelineAction.TimelineClip
        {
            action = action,
            startTime = Mathf.Max(0f, atTime),
        };
        timeline.tracks[trackIdx].clips.Add(clip);
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();

        selectedClip = clip;
        BuildTimelinePane();
        BuildInspectorPane();
    }

    /// <summary>
    /// วางท่าสำเร็จรูปเป็นคลิป — ได้ **สำเนา** ฝังใน config เสมอ (แก้แล้วไม่ไปเปลี่ยนท่าสำเร็จรูป
    /// หรือบอสตัวอื่น) · timeline เดี่ยวไม่มี config ให้ฝัง จึงเตือนแล้วไม่ทำ
    /// </summary>
    void AddPresetClip(BossAction preset, int trackIdx, float atTime)
    {
        if (preset == null || timeline == null) return;
        if (config == null)
        {
            EditorUtility.DisplayDialog("ท่าสำเร็จรูป", "เปิดผ่าน Encounter Config ก่อน — ท่าต้องฝังเป็นของบอสตัวนั้น", "OK");
            return;
        }
        if (timeline.tracks == null || timeline.tracks.Count == 0) return;
        trackIdx = Mathf.Clamp(trackIdx, 0, timeline.tracks.Count - 1);

        var copy = Instantiate(preset);
        copy.name = MakeUniqueSubAssetName(preset.name);
        Undo.RegisterCreatedObjectUndo(copy, "Add Preset");
        AssetDatabase.AddObjectToAsset(copy, config);

        Undo.RecordObject(timeline, "Add Preset");
        var clip = new BossTimelineAction.TimelineClip { action = copy, startTime = Mathf.Max(0f, Snap(atTime, false)) };
        timeline.tracks[trackIdx].clips.Add(clip);
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();

        selectedClip = clip;
        BuildTimelinePane();
        BuildInspectorPane();
        RefreshPreview();
    }

    /// <summary>ดึง sub-asset ออกมาเป็นไฟล์เดี่ยว — ใช้เมื่ออยากเอาท่านี้ไปใช้กับบอสตัวอื่น</summary>
    void ExtractToAssetFile(BossAction action)
    {
        if (action == null) return;

        string dir  = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(config));
        string path = EditorUtility.SaveFilePanelInProject(
            "Extract Boss Action", action.name, "asset",
            "เลือกที่เก็บ action ที่ดึงออกมา", dir);
        if (string.IsNullOrEmpty(path)) return;

        var copy = Instantiate(action);
        copy.name = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(copy, path);

        // ชี้ทุกคลิปที่ใช้ตัวเดิมไปที่ไฟล์ใหม่ แล้วทิ้ง sub-asset
        Undo.RecordObject(timeline, "Extract Boss Action");
        foreach (var track in timeline.tracks)
        {
            if (track?.clips == null) continue;
            foreach (var c in track.clips)
                if (c != null && c.action == action) c.action = copy;
        }
        EditorUtility.SetDirty(timeline);

        if (IsOwnedSubAsset(action) && !IsReferencedByPhases(action))
            Undo.DestroyObjectImmediate(action);

        AssetDatabase.SaveAssets();
        RebuildAll();
    }

    // ── Sub-asset helpers ─────────────────────────────────────────────────
    /// <summary>true ถ้า action ตัวนี้เป็น sub-asset ที่ฝังอยู่ใน config ที่เปิดอยู่</summary>
    bool IsOwnedSubAsset(Object action)
    {
        if (action == null || config == null) return false;
        if (!AssetDatabase.IsSubAsset(action)) return false;
        return AssetDatabase.GetAssetPath(action) == AssetDatabase.GetAssetPath(config);
    }

    int CountClipsUsing(BossAction action)
    {
        if (action == null || timeline?.tracks == null) return 0;
        int n = 0;
        foreach (var track in timeline.tracks)
        {
            if (track?.clips == null) continue;
            foreach (var c in track.clips)
                if (c != null && c.action == action) n++;
        }
        return n;
    }

    /// <summary>
    /// มีอะไรในไฟล์ config อ้าง action นี้อยู่ไหม — timeline ทุกเฟส · enrage · กองท่าสุ่ม · คอมโบ · เฟส
    ///
    /// เดิมนับแค่คลิปใน timeline ที่เปิดอยู่ + รายการของเฟส · ท่าเดียวกันที่ใช้ในเฟสอื่น (หลังแปลงคลิป
    /// เป็นของบอส ท่าที่เคยชี้ไฟล์เดียวกันกลายเป็น sub-asset ตัวเดียวที่หลายเฟสแชร์) หรืออยู่ในกองท่าสุ่ม
    /// ถูกทำลายทิ้งตอนลบคลิป แล้วเฟสอื่นเหลือคลิปว่าง
    /// </summary>
    bool IsReferencedInConfig(BossAction action, Object ignore = null)
    {
        if (action == null) return false;
        if (config == null) return CountClipsUsing(action) > 0;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(config)))
        {
            if (o == null || o == action || o == ignore) continue;
            var it = new SerializedObject(o).GetIterator();
            while (it.Next(true))
                if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == action)
                    return true;
        }
        return false;
    }

    /// <summary>เช็คว่ามีเฟสไหนอ้าง action นี้ตรงๆ อยู่ไหม — กันลบของที่ยังมีคนใช้</summary>
    bool IsReferencedByPhases(BossAction action)
    {
        if (action == null || config?.phases == null) return false;
        foreach (var phase in config.phases)
        {
            if (phase == null) continue;
            if (phase.actions != null && phase.actions.Contains(action)) return true;
            if (phase.enrageActions != null && phase.enrageActions.Contains(action)) return true;
        }
        return false;
    }

    string MakeUniqueSubAssetName(string baseName)
    {
        if (config == null) return baseName;

        var existing = new HashSet<string>();
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(config)))
            if (o != null) existing.Add(o.name);

        if (!existing.Contains(baseName)) return baseName;
        for (int i = 2; i < 999; i++)
            if (!existing.Contains($"{baseName}_{i}")) return $"{baseName}_{i}";
        return baseName;
    }

    /// <summary>ล้าง sub-asset ที่ไม่มีคลิปหรือเฟสไหนอ้างถึงแล้ว</summary>
    void CleanUnusedActions()
    {
        if (config == null) return;

        string path = AssetDatabase.GetAssetPath(config);
        var orphans = new List<BossAction>();
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (o is not BossAction a) continue;
            if (a == timeline || a is BossTimelineAction) continue;   // timeline เป็นตัวถือคลิป ไม่ใช่ของกำพร้า
            if (!AssetDatabase.IsSubAsset(a)) continue;
            if (!IsReferencedInConfig(a)) orphans.Add(a);
        }

        if (orphans.Count == 0)
        {
            EditorUtility.DisplayDialog("Clean Unused Actions", "ไม่มี action กำพร้าใน config นี้", "OK");
            return;
        }

        string names = string.Join("\n", orphans.ConvertAll(a => "• " + a.name));
        if (!EditorUtility.DisplayDialog("Clean Unused Actions",
                $"จะลบ action ที่ไม่มีใครอ้างถึง {orphans.Count} ตัว:\n\n{names}\n\nลบเลยไหม?",
                "ลบ", "ยกเลิก"))
            return;

        foreach (var a in orphans) Undo.DestroyObjectImmediate(a);
        AssetDatabase.SaveAssets();
        RebuildAll();
    }

    /// <summary>
    /// เมนู "+" / ดับเบิลคลิกเลน — **ลำดับเดียวกับแผงท่าสำเร็จรูป**
    ///
    /// เดิมเมนูนี้เรียงตามชื่อคลาส (New/CircleAoEAction, CircleAoEAction/AoE_Boss_…) ขณะที่แผงซ้าย
    /// เรียงตามสิ่งที่ผู้เล่นต้องทำ · สองคลังหน้าตาต่างกันแถมให้ผลต่างกัน (เมนูชี้ไฟล์เดิม
    /// แผงได้สำเนา) คนใช้ต้องจำว่าอันไหนเป็นอันไหน
    ///
    /// ตอนนี้ทางหลักมีทางเดียว: **เลือกท่า → ได้สำเนาของบอสนี้ → ปรับในการ์ด**
    ///   • หมวดท่าสำเร็จรูป (เหมือนแผงซ้าย)
    ///   • "ท่าในบอสนี้" — ก๊อปท่าที่ปรับไว้แล้วในบอสตัวนี้มาใช้อีก
    ///   • "ขั้นสูง" — ท่าเปล่าตามชนิด · ชี้ไฟล์ท่าในโปรเจกต์ตรงๆ (แชร์ แก้ที่เดียวเปลี่ยนทุกที่)
    /// </summary>
    void ShowAddClipMenu(int trackIdx, float atTime)
    {
        var menu = new GenericMenu();

        if (config == null)
            menu.AddDisabledItem(new GUIContent("ต้องเปิดผ่าน Encounter Config ก่อน จึงใช้ท่าสำเร็จรูปได้"));
        else
        {
            // ── ท่าสำเร็จรูป ──
            bool any = false;
            foreach (var (cat, preset) in CloneSwarm.EditorTools.BossPalette.AllPresets())
            {
                var p = preset;
                menu.AddItem(new GUIContent($"{cat}/{p.name}"), false, () => AddPresetClip(p, trackIdx, atTime));
                any = true;
            }
            if (!any)
                menu.AddDisabledItem(new GUIContent("ยังไม่มีท่าสำเร็จรูป — Tools > Clone Swarm > Boss > Create Default Presets"));

            // ── ท่าในบอสนี้ (ก๊อป) ──
            var own = ActionsUsedByConfig().OrderBy(x => x.name).ToList();
            if (own.Count > 0)
            {
                menu.AddSeparator("");
                foreach (var a in own)
                {
                    var action = a;
                    menu.AddItem(new GUIContent($"ท่าในบอสนี้ (ก๊อป)/{a.name}"), false,
                                 () => AddPresetClip(action, trackIdx, atTime));
                }
            }
        }

        menu.AddSeparator("");

        // ── ขั้นสูง: ท่าเปล่าตามชนิด ──
        if (config != null)
        {
            foreach (var t in TypeCache.GetTypesDerivedFrom<BossAction>()
                         .Where(t => !t.IsAbstract).OrderBy(t => ThaiTypeName(t)))
            {
                var type = t;
                menu.AddItem(new GUIContent($"ขั้นสูง/ท่าเปล่าตามชนิด/{ThaiTypeName(type)}"), false,
                             () => CreateActionAndAddClip(type, trackIdx, atTime));
            }
        }

        // ── ขั้นสูง: ชี้ไฟล์ท่าในโปรเจกต์ตรงๆ (แชร์) ──
        // เฉพาะไฟล์หลัก — sub-asset ของ config ต่างๆ ไม่อยู่ตรงนี้ (ท่าในบอสนี้อยู่ข้างบนแล้ว)
        // ท่าสำเร็จรูปก็ไม่อยู่ตรงนี้ — ถ้าชี้ตรงๆ แก้ในบอสแล้วท่าสำเร็จรูปจะเปลี่ยนตาม
        var shared = AssetDatabase.FindAssets("t:BossAction")
            .Select(AssetDatabase.GUIDToAssetPath).Distinct()
            .Select(AssetDatabase.LoadMainAssetAtPath).OfType<BossAction>()
            .Where(a => a != timeline && a is not BossTimelineAction)
            .Where(a => !CloneSwarm.EditorTools.BossPalette.IsPreset(a))
            .OrderBy(a => ThaiTypeName(a.GetType())).ThenBy(a => a.name)
            .ToList();
        foreach (var a in shared)
        {
            var action = a;
            menu.AddItem(new GUIContent($"ขั้นสูง/ไฟล์ท่าในโปรเจกต์ (แชร์ 🔗)/{ThaiTypeName(a.GetType())}/{a.name}"), false, () =>
            {
                Undo.RecordObject(timeline, "Add Timeline Clip");
                timeline.tracks[trackIdx].clips.Add(new BossTimelineAction.TimelineClip
                {
                    action = action,
                    startTime = Mathf.Max(0f, atTime),
                });
                EditorUtility.SetDirty(timeline);
                BuildTimelinePane();
            });
        }
        menu.ShowAsContext();
    }

    /// <summary>ท่าที่คลิปใน timeline ของ config นี้ใช้อยู่ (ทุกเฟส + enrage) — ไม่นับตัว timeline เอง</summary>
    IEnumerable<BossAction> ActionsUsedByConfig()
    {
        if (config == null) return Enumerable.Empty<BossAction>();
        var timelines = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(config))
            .OfType<BossTimelineAction>().ToList();
        foreach (var ph in config.phases ?? new List<BossPhase>())
        {
            if (ph == null) continue;
            if (ph.actions != null) timelines.AddRange(ph.actions.OfType<BossTimelineAction>());
            if (ph.enrageActions != null) timelines.AddRange(ph.enrageActions.OfType<BossTimelineAction>());
        }
        return timelines.Distinct()
            .SelectMany(tl => tl.tracks ?? new List<BossTimelineAction.TimelineTrack>())
            .Where(tr => tr?.clips != null)
            .SelectMany(tr => tr.clips)
            .Select(c => c?.action)
            .Where(a => a != null && a is not BossTimelineAction)
            .Distinct();
    }

    /// <summary>ชื่อชนิดท่าที่คนออกแบบอ่านรู้เรื่อง · ชื่อคลาสยังอยู่ในวงเล็บให้ค้นโค้ดได้</summary>
    static string ThaiTypeName(System.Type t)
    {
        string th = t.Name switch
        {
            nameof(CircleAoEAction)     => "วง",
            nameof(DonutAoEAction)      => "โดนัท",
            nameof(LineAoEAction)       => "เส้น",
            nameof(CrossAoEAction)      => "กากบาท",
            nameof(ConeAoEAction)       => "พัด",
            nameof(ColorMatchAoEAction) => "จับคู่สี",
            nameof(TetherAction)        => "โซ่",
            nameof(LimitCutAction)      => "Limit Cut",
            nameof(KeepMovingAction)    => "ห้ามหยุดเดิน",
            nameof(RandomAttackAction)  => "สุ่มจากหลายท่า",
            nameof(ComboAction)         => "คอมโบ",
            nameof(BossTimelineAction)  => "timeline ซ้อน",
            _                           => null,
        };
        return th != null ? $"{th} ({t.Name})" : t.Name;
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    static float Snap(float t, bool fine) => Mathf.Round(t / (fine ? 0.01f : SnapStep)) * (fine ? 0.01f : SnapStep);

    /// <summary>สีเดียวกับคลิปบนเส้นเวลา — ArenaPreview ใช้ให้รูปในสนามกับคลิปตรงกัน</summary>
    internal static Color ClipColorOf(BossAction action) => ClipColor(action);

    static Color ClipColor(BossAction action) => action switch
    {
        TetherAction         => new Color(0.45f, 0.35f, 0.75f),
        BossTimelineAction   => new Color(0.2f, 0.45f, 0.7f),
        ComboAction          => new Color(0.2f, 0.5f, 0.55f),
        RandomAttackAction   => new Color(0.3f, 0.55f, 0.3f),
        SpawnAoEActionBase   => new Color(0.75f, 0.4f, 0.2f),
        _                    => new Color(0.4f, 0.4f, 0.4f),
    };

    static void SetBorder(VisualElement el, Color color, float width)
    {
        el.style.borderTopColor = el.style.borderBottomColor = el.style.borderLeftColor = el.style.borderRightColor = color;
        el.style.borderTopWidth = el.style.borderBottomWidth = el.style.borderLeftWidth = el.style.borderRightWidth = width;
    }

    static Label InfoLabel(string text)
    {
        var lbl = new Label(text);
        lbl.style.opacity = 0.75f;
        lbl.style.paddingLeft = 10;
        lbl.style.paddingTop = 8;
        lbl.style.paddingBottom = 8;
        lbl.style.whiteSpace = WhiteSpace.Normal;
        return lbl;
    }

    static Label ConditionChip(string text)
    {
        var chip = new Label(text);
        chip.style.fontSize = Fs(10);
        chip.style.color = new Color(0.55f, 0.85f, 0.7f);
        chip.style.backgroundColor = new Color(0.2f, 0.5f, 0.35f, 0.25f);
        chip.style.paddingLeft = chip.style.paddingRight = 6;
        chip.style.paddingTop = chip.style.paddingBottom = 2;
        chip.style.marginLeft = chip.style.marginRight = 2;
        chip.style.marginBottom = 2;
        chip.style.borderTopLeftRadius = chip.style.borderTopRightRadius = 8;
        chip.style.borderBottomLeftRadius = chip.style.borderBottomRightRadius = 8;
        return chip;
    }

    static Label ArrowLabel()
    {
        var lbl = new Label("→");
        lbl.style.fontSize = Fs(14);
        lbl.style.opacity = 0.5f;
        lbl.style.marginRight = 2;
        return lbl;
    }
}
