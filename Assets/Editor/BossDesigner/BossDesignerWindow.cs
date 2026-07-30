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

    // ── State (serialize เพื่อรอด domain reload) ──────────────────────────
    [SerializeField] BossEncounterConfig config;
    [SerializeField] BossTimelineAction  timeline;
    [SerializeField] int   phaseIndex = -1;
    [SerializeField] bool  editingEnrage;
    [SerializeField] float pxPerSec = 50f;
    [SerializeField] float playhead;

    BossTimelineAction.TimelineClip selectedClip;

    // ── UI refs ───────────────────────────────────────────────────────────
    ObjectField   configField;
    VisualElement graphPane;
    VisualElement timelinePane;

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
        root.Add(graphPane);

        timelinePane = new VisualElement();
        timelinePane.style.flexGrow = 1;
        root.Add(timelinePane);

        RebuildAll();
    }

    void RebuildAll()
    {
        if (graphPane == null || timelinePane == null) return;
        if (configField != null) configField.SetValueWithoutNotify(config);
        BuildGraphPane();
        BuildTimelinePane();
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

        var zoom = new Slider(10f, 200f) { value = pxPerSec };
        zoom.style.width = 120;
        zoom.style.marginRight = 8;
        zoom.tooltip = "Zoom (px ต่อวินาที)";
        zoom.RegisterValueChangedCallback(e => { pxPerSec = e.newValue; BuildTimelinePane(); });
        bar.Add(zoom);

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
        el.style.backgroundColor = baseCol;
        SetBorder(el, clip == selectedClip ? Color.white : new Color(0f, 0f, 0f, 0.5f), clip == selectedClip ? 2 : 1);

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
            selectedClip = clip;
            Selection.activeObject = clip.action;
            Undo.RecordObject(timeline, "Move Timeline Clip");
            dragStartT = clip.startTime;
            accum = 0f;
            el.CapturePointer(e.pointerId);
            el.BringToFront();
            SetBorder(el, Color.white, 2);
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
        menu.AddItem(new GUIContent("Edit in Inspector"), false, () => Selection.activeObject = clip.action);
        menu.AddItem(new GUIContent("Duplicate (+0.5s)"), false, () =>
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
        menu.AddItem(new GUIContent("Delete Clip"), false, () =>
        {
            Undo.RecordObject(timeline, "Delete Timeline Clip");
            timeline.tracks[trackIdx].clips.Remove(clip);
            if (selectedClip == clip) selectedClip = null;
            EditorUtility.SetDirty(timeline);
            BuildTimelinePane();
        });
        menu.ShowAsContext();
    }

    void ShowAddClipMenu(int trackIdx, float atTime)
    {
        var menu = new GenericMenu();
        var guids = AssetDatabase.FindAssets("t:BossAction");
        var actions = guids
            .SelectMany(g => AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(g)))
            .OfType<BossAction>()
            .Where(a => a != timeline)
            .Distinct()
            .OrderBy(a => a.GetType().Name).ThenBy(a => a.name)
            .ToList();

        if (actions.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("ไม่พบ BossAction asset — สร้างผ่าน Create → Boss → Actions ก่อน"));
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
