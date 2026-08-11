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
public class BossDesignerWindow : EditorWindow
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
    VisualElement selectedClipEl;

    // ── UI refs ───────────────────────────────────────────────────────────
    ObjectField   configField;
    VisualElement graphPane;
    VisualElement timelinePane;
    VisualElement inspectorPane;

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

    void OnEnable()  => Undo.undoRedoPerformed += OnUndoRedo;
    void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;
    void OnUndoRedo() { RebuildAll(); }

    public void SetConfig(BossEncounterConfig cfg)
    {
        config = cfg;
        phaseIndex = (cfg != null && cfg.phases != null && cfg.phases.Count > 0) ? 0 : -1;
        editingEnrage = false;
        timeline = ResolvePhaseTimeline();
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

        timelinePane = new VisualElement();
        timelinePane.style.flexGrow = 1;
        timelinePane.style.flexShrink = 1;
        timelinePane.style.minWidth = 0;   // ยอมให้หดได้ ไม่งั้นมันดันแผงขวาจนแบน
        bodyRow.Add(timelinePane);

        inspectorPane = new VisualElement();
        inspectorPane.style.width = 330;
        inspectorPane.style.minWidth = 260;
        inspectorPane.style.flexShrink = 0;   // ห้ามหด — ตัวที่ทำให้แผงเหลือ 30px
        inspectorPane.style.borderLeftWidth = 1;
        inspectorPane.style.borderLeftColor = GridLine;
        inspectorPane.style.paddingLeft = 4;
        inspectorPane.style.paddingRight = 4;
        bodyRow.Add(inspectorPane);

        root.Add(bodyRow);

        RebuildAll();
    }

    void RebuildAll()
    {
        if (graphPane == null || timelinePane == null) return;
        if (configField != null) configField.SetValueWithoutNotify(config);
        BuildGraphPane();
        BuildTimelinePane();
        BuildInspectorPane();
    }

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
        badge.style.fontSize = 10;
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

        var scroll = new ScrollView();
        scroll.style.flexGrow = 1;
        scroll.Add(new InspectorElement(action));
        inspectorPane.Add(scroll);
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
                row.Add(ConditionChip($"HP ≤ {phase.transitionHealthPct:P0}"));
                row.Add(ArrowLabel());
            }
        }

        var addBtn = new Button(AddPhase) { text = "+ Phase" };
        addBtn.style.marginLeft = 10;
        row.Add(addBtn);

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
        subLabel.style.fontSize = 10;
        subLabel.style.opacity = 0.7f;
        node.Add(subLabel);

        // Enrage chip — คลิกเพื่อแก้ enrage timeline ของเฟสนี้
        if (phase.enrageTime > 0f)
        {
            bool enrageSelected = isSelected && editingEnrage;
            var enrage = new Label($"enrage {phase.enrageTime:0}s →");
            enrage.style.fontSize = 10;
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
        hint.style.fontSize = 10;
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

        body.Add(headerCol);

        // ── ฝั่งขวา: ruler + lanes ใน ScrollView แนวนอน ──
        var scroll = new ScrollView(ScrollViewMode.Horizontal);
        scroll.style.flexGrow = 1;
        timelineScroll = scroll;

        var content = new VisualElement();
        content.style.width = contentW;
        content.style.flexShrink = 0;

        content.Add(BuildRuler(contentSec));

        for (int i = 0; i < timeline.tracks.Count; i++)
            content.Add(BuildLane(i, contentSec));

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
            menu.AddItem(new GUIContent("Add Clip at Playhead"), false, () => ShowAddClipMenu(trackIdx, playhead));
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
                lbl.style.fontSize = 9;
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

    void SetPlayhead(float t)
    {
        playhead = Mathf.Max(0f, Mathf.Round(t / SnapStep) * SnapStep);
        var line = timelinePane.Q<VisualElement>("playhead-line");
        if (line != null) line.style.left = playhead * pxPerSec;
        var lbl = timelinePane.Q<Label>("playhead-label");
        if (lbl != null) lbl.text = $"t = {playhead:0.0}s";
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
            Undo.RecordObject(timeline, "Add Timeline Clip");
            float t = Snap(e.localMousePosition.x / pxPerSec, false);
            foreach (var a in dropped)
            {
                track.clips.Add(new BossTimelineAction.TimelineClip { action = a, startTime = t });
                t += 0.5f;
            }
            EditorUtility.SetDirty(timeline);
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
        ApplyClipSelectionStyle(el, baseCol, clip == selectedClip);
        if (clip == selectedClip) selectedClipEl = el;

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

        var lbl = new Label(clip.action.name);
        lbl.style.fontSize = 10;
        lbl.style.color = Color.white;
        lbl.style.marginLeft = 4;
        lbl.style.marginTop = 2;
        lbl.style.overflow = Overflow.Hidden;
        lbl.style.textOverflow = TextOverflow.Ellipsis;
        lbl.style.whiteSpace = WhiteSpace.NoWrap;
        lbl.pickingMode = PickingMode.Ignore;
        el.Add(lbl);

        el.tooltip = $"{clip.action.GetType().Name}\nstart {clip.startTime:0.0}s · ยาว ~{dur:0.0}s";

        // ── ลากเลื่อนเวลา ──
        float dragStartT = 0f;
        float accum = 0f;
        el.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0) return;

            // ล้างไฮไลต์ตัวเก่าก่อน ไม่งั้นขอบขาวค้างสะสมทุกตัวที่เคยคลิก
            if (selectedClipEl != null && selectedClipEl != el)
                ApplyClipSelectionStyle(selectedClipEl, ClipColor(selectedClip?.action), false);

            selectedClip   = clip;
            selectedClipEl = el;
            ApplyClipSelectionStyle(el, baseCol, true);
            BuildInspectorPane();

            Undo.RecordObject(timeline, "Move Timeline Clip");
            dragStartT = clip.startTime;
            accum = 0f;
            el.CapturePointer(e.pointerId);
            el.BringToFront();
            e.StopPropagation();
        });
        el.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (!el.HasPointerCapture(e.pointerId)) return;
            accum += e.deltaPosition.x;
            clip.startTime = Mathf.Max(0f, Snap(dragStartT + accum / pxPerSec, e.shiftKey));
            el.style.left = clip.startTime * pxPerSec;
            el.tooltip = $"{clip.action.GetType().Name}\nstart {clip.startTime:0.0}s · ยาว ~{dur:0.0}s";
        });
        el.RegisterCallback<PointerUpEvent>(e =>
        {
            if (!el.HasPointerCapture(e.pointerId)) return;
            el.ReleasePointer(e.pointerId);

            // แค่คลิกเลือกไม่ได้ลาก — ไม่มีอะไรให้รีเฟรช ข้าม rebuild ไปเลย
            if (Mathf.Approximately(clip.startTime, dragStartT)) return;

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
        menu.AddItem(new GUIContent("Ping Asset"), false, () => EditorGUIUtility.PingObject(clip.action));

        // ชื่อเดิมคือ "Duplicate (+0.5s)" ซึ่งชวนเข้าใจว่าได้ action ตัวใหม่
        // จริงๆ ได้แค่คลิปที่ชี้ไป action เดิม — เปลี่ยนชื่อให้ตรงกับที่มันทำ
        menu.AddItem(new GUIContent("Duplicate Clip (same action)"), false, () =>
        {
            Undo.RecordObject(timeline, "Duplicate Timeline Clip");
            timeline.tracks[trackIdx].clips.Add(new BossTimelineAction.TimelineClip
            {
                action = clip.action,
                startTime = clip.startTime + 0.5f,
            });
            EditorUtility.SetDirty(timeline);
            BuildTimelinePane();
        });

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
        menu.AddItem(new GUIContent("Delete Clip"), false, () => DeleteClip(clip, trackIdx));
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
        if (IsOwnedSubAsset(action) && CountClipsUsing(action) == 0 && !IsReferencedByPhases(action))
        {
            Undo.DestroyObjectImmediate(action);
            AssetDatabase.SaveAssets();
        }

        BuildTimelinePane();
        BuildInspectorPane();
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
            if (CountClipsUsing(a) == 0 && !IsReferencedByPhases(a)) orphans.Add(a);
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

    void ShowAddClipMenu(int trackIdx, float atTime)
    {
        var menu = new GenericMenu();

        // ── สร้างใหม่ตรงนี้เลย ─────────────────────────────────────────────
        // เดิมเมนูนี้บอกให้ไปสร้างที่ Project window ก่อนแล้วค่อยกลับมา
        if (config != null)
        {
            var types = TypeCache.GetTypesDerivedFrom<BossAction>()
                .Where(t => !t.IsAbstract)
                .OrderBy(t => t.Name);
            foreach (var t in types)
            {
                var type = t;
                menu.AddItem(new GUIContent($"New/{type.Name}"), false,
                             () => CreateActionAndAddClip(type, trackIdx, atTime));
            }
            menu.AddSeparator("");
        }

        // ── asset ที่มีอยู่แล้ว ───────────────────────────────────────────
        // LoadAllAssetsAtPath คืน sub-asset มาด้วย — ต้องกรอง sub-asset ของ config อื่นออก
        // ไม่งั้นท่าที่สร้างในบอส A จะไปโผล่ในเมนูของบอสทุกตัว
        string configPath = config != null ? AssetDatabase.GetAssetPath(config) : null;
        var guids = AssetDatabase.FindAssets("t:BossAction");
        var actions = guids
            .SelectMany(g => AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(g)))
            .OfType<BossAction>()
            .Where(a => a != timeline)
            .Where(a => !AssetDatabase.IsSubAsset(a)
                        || AssetDatabase.GetAssetPath(a) == configPath)
            .Distinct()
            .OrderBy(a => a.GetType().Name).ThenBy(a => a.name)
            .ToList();

        if (actions.Count == 0)
        {
            if (config == null)
                menu.AddDisabledItem(new GUIContent("ไม่พบ BossAction asset — เปิดผ่าน Encounter Config เพื่อสร้างใหม่ได้ที่นี่"));
        }
        else
        {
            foreach (var a in actions)
            {
                var action = a;
                menu.AddItem(new GUIContent($"{a.GetType().Name}/{a.name}"), false, () =>
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
        }
        menu.ShowAsContext();
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    static float Snap(float t, bool fine) => Mathf.Round(t / (fine ? 0.01f : SnapStep)) * (fine ? 0.01f : SnapStep);

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
        chip.style.fontSize = 10;
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
        lbl.style.fontSize = 14;
        lbl.style.opacity = 0.5f;
        lbl.style.marginRight = 2;
        return lbl;
    }
}
