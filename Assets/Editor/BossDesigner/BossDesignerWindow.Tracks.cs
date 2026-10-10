using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Clip = BossTimelineAction.TimelineClip;

/// <summary>
/// เลนเป้าหมาย · ลากกรอบเลือก · ย้ายคลิปข้ามเลน
///
///   คลิกหัวเลน / พื้นเลน / คลิป  = เลนนั้นเป็น "เลนเป้าหมาย" (แถบฟ้าที่หัวเลน)
///                                  Ctrl+V และคลิกท่าในแผงซ้ายวางลงเลนนี้
///   ลากบนพื้นเลนว่าง              = กรอบเลือก (ข้ามหลายเลนได้ · Ctrl/Shift = เพิ่มจากที่เลือกไว้)
///   ลากคลิปขึ้น/ลง                = ย้ายข้ามเลน (ทั้งชุด · เห็นเงาบนเลนปลายทางระหว่างลาก)
///   Alt+↑ / Alt+↓                 = ย้ายชุดที่เลือกขึ้น/ลงหนึ่งเลน
///
/// ═══ ทำไมลากข้ามเลนใช้เงา ไม่ย้ายตัวคลิปจริง ═══
/// คลิปเป็นลูกของเลน และเลนล่างวาดทับเลนบน (มีพื้นหลังทึบ) · ย้าย top ของคลิปลงไปจะจมใต้เลนถัดไป
/// ย้ายพ่อแม่ระหว่างลากก็ทำ pointer capture หลุด · จึงวาดเงาบนเลนปลายทาง แล้วย้ายจริงตอนปล่อย
/// </summary>
public partial class BossDesignerWindow
{
    int selectedTrack = -1;
    readonly List<VisualElement> trackHeaderEls = new List<VisualElement>();
    readonly List<VisualElement> laneEls        = new List<VisualElement>();
    VisualElement timelineContent;

    static readonly Color TrackAccent = new Color(0.35f, 0.65f, 1f);

    /// <summary>เลนที่ Ctrl+V / แผงท่าสำเร็จรูปจะวางลง</summary>
    int TargetTrack()
    {
        int n = timeline?.tracks?.Count ?? 0;
        if (n == 0) return -1;
        if (selectedTrack >= 0 && selectedTrack < n) return selectedTrack;
        int t = TrackOf(selectedClip);
        return t >= 0 ? t : Mathf.Clamp(clipboardTrack, 0, n - 1);
    }

    void SelectTrack(int i)
    {
        if (selectedTrack == i) return;
        selectedTrack = i;
        RefreshTrackStyles();
    }

    void RefreshTrackStyles()
    {
        for (int i = 0; i < trackHeaderEls.Count; i++)
        {
            bool on = i == selectedTrack;
            var h = trackHeaderEls[i];
            h.style.borderLeftWidth = on ? 3 : 0;
            h.style.borderLeftColor = TrackAccent;
            h.style.backgroundColor = on ? new Color(TrackAccent.r, TrackAccent.g, TrackAccent.b, 0.12f) : Color.clear;
        }
        for (int i = 0; i < laneEls.Count; i++)
            laneEls[i].style.backgroundColor = i == selectedTrack ? Color.Lerp(LaneBg, TrackAccent, 0.06f) : LaneBg;
    }

    void ClearTrackElements()
    {
        trackHeaderEls.Clear();
        laneEls.Clear();
        timelineContent = null;
        marqueePending = marqueeActive = false;
        marqueeEl = null;
    }

    // ── ย้ายข้ามเลน ──────────────────────────────────────────────────────
    int ClampTrackDelta(IEnumerable<int> tracks, int delta)
    {
        var list = tracks.ToList();
        if (list.Count == 0 || timeline?.tracks == null) return 0;
        return Mathf.Clamp(delta, -list.Min(), timeline.tracks.Count - 1 - list.Max());
    }

    void MoveSelectionTracks(int dir)
    {
        var sel = SelectedClips();
        if (sel.Count == 0) return;
        int delta = ClampTrackDelta(sel.Select(x => x.track), dir);
        if (delta == 0) return;

        Undo.RecordObject(timeline, "Move Clips to Track");
        foreach (var (c, tr) in sel)
        {
            timeline.tracks[tr].clips.Remove(c);
            timeline.tracks[tr + delta].clips.Add(c);
        }
        EditorUtility.SetDirty(timeline);
        if (selectedTrack >= 0) selectedTrack = Mathf.Clamp(selectedTrack + delta, 0, timeline.tracks.Count - 1);
        BuildTimelinePane();
    }

    readonly List<VisualElement> dragGhosts = new List<VisualElement>();

    /// <summary>เงาของคลิปบนเลนปลายทาง · delta 0 = ไม่มีเงา ตัวจริงทึบตามปกติ</summary>
    void UpdateDragGhosts(List<(Clip clip, float start, int track)> group, int delta)
    {
        foreach (var g in dragGhosts) g.RemoveFromHierarchy();
        dragGhosts.Clear();

        foreach (var (c, _, tr) in group)
        {
            if (clipEls.TryGetValue(c, out var el)) el.style.opacity = delta == 0 ? 1f : 0.35f;
            if (delta == 0) continue;
            int to = tr + delta;
            if (to < 0 || to >= laneEls.Count) continue;

            var ghost = new VisualElement { pickingMode = PickingMode.Ignore };
            ghost.style.position = Position.Absolute;
            ghost.style.left = c.startTime * pxPerSec;
            ghost.style.top = 4;
            ghost.style.height = TrackH - 9;
            ghost.style.width = el != null ? el.resolvedStyle.width : 24f;
            var col = ClipColor(c.action);
            ghost.style.backgroundColor = new Color(col.r, col.g, col.b, 0.6f);
            SetBorder(ghost, Color.white, 1);
            ghost.style.borderTopLeftRadius = ghost.style.borderTopRightRadius = 3;
            ghost.style.borderBottomLeftRadius = ghost.style.borderBottomRightRadius = 3;
            laneEls[to].Add(ghost);
            dragGhosts.Add(ghost);
        }
    }

    // ── กรอบเลือก ─────────────────────────────────────────────────────────
    bool marqueePending, marqueeActive, marqueeAdditive;
    Vector2 marqueeStart;
    VisualElement marqueeEl;
    HashSet<Clip> marqueeBase;

    /// <summary>
    /// กดบนพื้นเลนว่าง — ยังไม่จับ pointer จนกว่าจะลากเกิน 4px (ไม่งั้นดับเบิลคลิกเพิ่มท่าพัง
    /// เพราะ click event จะไปลงที่ตัวที่จับ pointer แทนเลน)
    /// </summary>
    void BeginMarqueePending(PointerDownEvent e, int trackIdx)
    {
        if (timelineContent == null) return;
        SelectTrack(trackIdx);
        marqueeAdditive = e.ctrlKey || e.commandKey || e.shiftKey;
        marqueeStart = timelineContent.WorldToLocal(e.position);
        marqueePending = true;
        if (!marqueeAdditive && (selection.Count > 0 || selectedClip != null)) SelectOnly(null);
    }

    void HookMarquee(VisualElement content)
    {
        timelineContent = content;

        content.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (!marqueePending && !marqueeActive) return;
            if ((e.pressedButtons & 1) == 0) { EndMarquee(content, e.pointerId); return; }

            Vector2 p = content.WorldToLocal(e.position);
            if (!marqueeActive)
            {
                if ((p - marqueeStart).sqrMagnitude < 16f) return;
                marqueeActive = true;
                marqueeBase = marqueeAdditive ? new HashSet<Clip>(selection) : new HashSet<Clip>();
                marqueeEl = new VisualElement { pickingMode = PickingMode.Ignore };
                marqueeEl.style.position = Position.Absolute;
                marqueeEl.style.backgroundColor = new Color(TrackAccent.r, TrackAccent.g, TrackAccent.b, 0.15f);
                SetBorder(marqueeEl, new Color(TrackAccent.r, TrackAccent.g, TrackAccent.b, 0.9f), 1);
                content.Add(marqueeEl);
                content.CapturePointer(e.pointerId);
            }

            Vector2 min = Vector2.Min(p, marqueeStart), max = Vector2.Max(p, marqueeStart);
            marqueeEl.style.left = min.x;
            marqueeEl.style.top = min.y;
            marqueeEl.style.width = max.x - min.x;
            marqueeEl.style.height = max.y - min.y;

            var rect = new Rect(min, max - min);
            selection.Clear();
            selection.UnionWith(marqueeBase);
            foreach (var kv in clipEls)
                if (rect.Overlaps(content.WorldToLocal(kv.Value.worldBound))) selection.Add(kv.Key);
            if (!selection.Contains(selectedClip))
                selectedClip = SelectedClips().Select(x => x.clip).FirstOrDefault();
            RefreshSelectionStyles();
        });

        content.RegisterCallback<PointerUpEvent>(e => EndMarquee(content, e.pointerId));
    }

    void EndMarquee(VisualElement content, int pointerId)
    {
        bool wasActive = marqueeActive;
        marqueePending = marqueeActive = false;
        if (content.HasPointerCapture(pointerId)) content.ReleasePointer(pointerId);
        marqueeEl?.RemoveFromHierarchy();
        marqueeEl = null;
        if (!wasActive) return;
        BuildInspectorPane();
        RefreshPreview();
    }
}
