using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Clip = BossTimelineAction.TimelineClip;

/// <summary>
/// เลือกหลายคลิป + คัดลอก/ตัด/วาง แบบโปรแกรมตัดต่อ
///
///   Ctrl/Shift+คลิก  เพิ่ม/เอาออกจากที่เลือก · Ctrl+A ทั้ง timeline · Esc ยกเลิก
///   ลากคลิปที่เลือกอยู่  ย้ายทั้งกลุ่ม (ระยะห่างคงเดิม)
///   Ctrl+C / Ctrl+X   คัดลอก / ตัด
///   Ctrl+V            วางที่ playhead บนเลนของคลิปที่เลือก (ไม่มี = เลนเดิม) · playhead เลื่อนไปท้ายชุด
///                     กดซ้ำจึงวางต่อกันเป็นแถว
///   Ctrl+D            ก๊อปชุดที่เลือกไปต่อท้ายตัวเอง
///   คลิกขวาพื้นเลน     "วางที่นี่"
///
/// ═══ คลิปบอร์ดเก็บ "ภาพถ่าย" ไม่ใช่ตัวอ้างอิง ═══
///
/// Ctrl+X ลบคลิป และ action ที่เป็น sub-asset ไม่มีใครใช้แล้วถูกทำลายทิ้ง — ถ้าคลิปบอร์ดชี้ของจริง
/// จะวางไม่ได้ · จึงก๊อป action เป็นสำเนาซ่อนตอนคัดลอก (ค่าตอนกด Ctrl+C แบบโปรแกรมตัดต่อ)
/// วางแล้วได้สำเนาใหม่เป็นของบอสปลายทางเสมอ — วางข้ามเฟส ข้ามบอสได้ (คลิปบอร์ดเป็น static)
/// ไม่รอด domain reload (คอมไพล์สคริปต์แล้วคลิปบอร์ดว่าง)
/// </summary>
public partial class BossDesignerWindow
{
    class ClipboardItem
    {
        public BossAction snapshot;
        public float      relTime;
        public int        relTrack;
    }

    static readonly List<ClipboardItem> clipboard = new List<ClipboardItem>();
    static float clipboardSpan;
    static int   clipboardTrack;

    readonly HashSet<Clip> selection = new HashSet<Clip>();
    readonly Dictionary<Clip, VisualElement> clipEls = new Dictionary<Clip, VisualElement>();

    bool IsSelected(Clip clip) => clip != null && (clip == selectedClip || selection.Contains(clip));

    /// <summary>คลิปที่เลือกพร้อมเลน เรียงตามเวลา</summary>
    List<(Clip clip, int track)> SelectedClips()
    {
        var list = new List<(Clip, int)>();
        if (timeline?.tracks == null) return list;
        for (int i = 0; i < timeline.tracks.Count; i++)
        {
            var clips = timeline.tracks[i]?.clips;
            if (clips == null) continue;
            foreach (var c in clips)
                if (c?.action != null && IsSelected(c)) list.Add((c, i));
        }
        return list.OrderBy(x => x.Item1.startTime).ToList();
    }

    /// <summary>
    /// เรียกต้น BuildTimelinePane — โค้ดส่วนอื่นตั้ง selectedClip ตรงๆ (เพิ่ม/ก๊อป/วางท่า)
    /// ถ้า selectedClip ไม่อยู่ในชุด แปลว่าเพิ่งถูกตั้งจากที่อื่น → ชุดเหลือตัวนั้นตัวเดียว
    /// </summary>
    void SyncSelection()
    {
        clipEls.Clear();
        ClearTrackElements();
        if (selectedClip == null) { selection.Clear(); return; }
        if (!selection.Contains(selectedClip)) { selection.Clear(); selection.Add(selectedClip); }

        var alive = new HashSet<Clip>(timeline?.tracks?.Where(t => t?.clips != null).SelectMany(t => t.clips)
                                      ?? Enumerable.Empty<Clip>());
        selection.RemoveWhere(c => !alive.Contains(c));
        if (!selection.Contains(selectedClip)) selectedClip = selection.FirstOrDefault();
    }

    void RefreshSelectionStyles()
    {
        foreach (var kv in clipEls)
            ApplyClipSelectionStyle(kv.Value, ClipColor(kv.Key.action), IsSelected(kv.Key));
    }

    void SelectOnly(Clip clip)
    {
        selection.Clear();
        if (clip != null) selection.Add(clip);
        selectedClip = clip;
        RefreshSelectionStyles();
        BuildInspectorPane();
        RefreshPreview();
    }

    void ToggleSelect(Clip clip)
    {
        if (selection.Contains(clip))
        {
            selection.Remove(clip);
            if (selectedClip == clip) selectedClip = selection.FirstOrDefault();
        }
        else
        {
            selection.Add(clip);
            selectedClip = clip;
        }
        RefreshSelectionStyles();
        BuildInspectorPane();
        RefreshPreview();
    }

    void SelectAllInTimeline()
    {
        if (timeline?.tracks == null) return;
        selection.Clear();
        foreach (var c in timeline.tracks.Where(t => t?.clips != null).SelectMany(t => t.clips))
            if (c?.action != null) selection.Add(c);
        if (!selection.Contains(selectedClip)) selectedClip = selection.FirstOrDefault();
        RefreshSelectionStyles();
        BuildInspectorPane();
        RefreshPreview();
    }

    // ── เลื่อนทั้งกลุ่ม ──
    void NudgeSelection(float step)
    {
        var sel = SelectedClips();
        if (sel.Count == 0) return;
        float min = sel.Min(x => x.clip.startTime);
        step = Mathf.Max(step, -min);   // ตัวแรกชน 0 แล้วทั้งกลุ่มหยุด ระยะห่างไม่เพี้ยน
        Undo.RecordObject(timeline, "Nudge Clips");
        foreach (var (c, _) in sel) c.startTime = Mathf.Max(0f, Snap(c.startTime + step, false));
        EditorUtility.SetDirty(timeline);
        BuildTimelinePane();
        BuildInspectorPane();
        RefreshPreview();
    }

    // ── คลิปบอร์ด ──
    static List<ClipboardItem> Snapshot(List<(Clip clip, int track)> sel, out float t0, out int tr0, out float span)
    {
        t0  = sel.Min(x => x.clip.startTime);
        tr0 = sel.Min(x => x.track);
        span = sel.Max(x => x.clip.startTime + Mathf.Max(x.clip.action.GetEditorDuration(), 0.3f)) - t0;

        var items = new List<ClipboardItem>();
        foreach (var (c, tr) in sel)
        {
            var snap = Object.Instantiate(c.action);
            snap.name = c.action.name;
            snap.hideFlags = HideFlags.HideAndDontSave;
            items.Add(new ClipboardItem { snapshot = snap, relTime = c.startTime - t0, relTrack = tr - tr0 });
        }
        return items;
    }

    static void DisposeSnapshots(List<ClipboardItem> items)
    {
        foreach (var it in items)
            if (it.snapshot != null) Object.DestroyImmediate(it.snapshot);
        items.Clear();
    }

    bool CopySelection()
    {
        var sel = SelectedClips();
        if (sel.Count == 0) return false;
        DisposeSnapshots(clipboard);
        clipboard.AddRange(Snapshot(sel, out _, out clipboardTrack, out clipboardSpan));
        ShowNotification(new GUIContent($"คัดลอก {sel.Count} คลิป"), 1.0);
        return true;
    }

    void CutSelection()
    {
        if (!CopySelection()) return;
        DeleteSelection("Cut Clips");
        ShowNotification(new GUIContent($"ตัด {clipboard.Count} คลิป"), 1.0);
    }

    void PasteClipboard(float atTime, int baseTrack)
    {
        if (clipboard.Count == 0) { ShowNotification(new GUIContent("คลิปบอร์ดว่าง"), 1.0); return; }
        PasteItems(clipboard, atTime, baseTrack, clipboardSpan, "Paste Clips");
    }

    /// <summary>Ctrl+D — ชุดที่เลือกไปต่อท้ายตัวเอง (ไม่แตะคลิปบอร์ด)</summary>
    void DuplicateSelection()
    {
        var sel = SelectedClips();
        if (sel.Count == 0) return;
        var items = Snapshot(sel, out float t0, out int tr0, out float span);
        PasteItems(items, t0 + span, tr0, span, "Duplicate Clips");
        DisposeSnapshots(items);
    }

    void PasteItems(List<ClipboardItem> items, float atTime, int baseTrack, float span, string undoName)
    {
        if (timeline?.tracks == null || timeline.tracks.Count == 0) return;
        Object owner = config != null ? config : (AssetDatabase.Contains(timeline) ? timeline : null);
        if (owner == null) return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.RecordObject(timeline, undoName);

        selection.Clear();
        Clip first = null;
        foreach (var it in items.OrderBy(i => i.relTime))
        {
            if (it.snapshot == null) continue;
            var a = Object.Instantiate(it.snapshot);
            a.hideFlags = HideFlags.None;
            a.name = MakeUniqueSubAssetName(it.snapshot.name);
            Undo.RegisterCreatedObjectUndo(a, undoName);
            AssetDatabase.AddObjectToAsset(a, owner);

            int tr = Mathf.Clamp(baseTrack + it.relTrack, 0, timeline.tracks.Count - 1);
            timeline.tracks[tr].clips ??= new List<Clip>();
            var clip = new Clip { action = a, startTime = Mathf.Max(0f, Snap(atTime + it.relTime, false)) };
            timeline.tracks[tr].clips.Add(clip);
            selection.Add(clip);
            first ??= clip;
        }
        Undo.CollapseUndoOperations(group);
        if (first == null) return;

        selectedClip = first;
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();

        // playhead ไปท้ายชุด — Ctrl+V ซ้ำแล้ววางต่อกันเป็นแถว แบบโปรแกรมตัดต่อ
        SetPlayhead(Mathf.Max(0f, atTime) + span, snap: false);
        BuildTimelinePane();
        BuildInspectorPane();
        RefreshPreview();
    }

    /// <summary>ลบทุกคลิปที่เลือก (Undo ครั้งเดียว) · action ที่เป็นของบอสนี้และไม่มีใครใช้แล้วถูกทำลายด้วย</summary>
    void DeleteSelection(string undoName = "Delete Clips")
    {
        var sel = SelectedClips();
        if (sel.Count == 0) return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.RecordObject(timeline, undoName);
        foreach (var (c, tr) in sel) timeline.tracks[tr].clips.Remove(c);

        bool destroyed = false;
        foreach (var a in sel.Select(x => x.clip.action).Distinct())
        {
            if (IsOwnedSubAsset(a) && !IsReferencedInConfig(a))
            {
                Undo.DestroyObjectImmediate(a);
                destroyed = true;
            }
        }
        Undo.CollapseUndoOperations(group);

        selection.Clear();
        selectedClip = null;
        EditorUtility.SetDirty(timeline);
        if (destroyed) AssetDatabase.SaveAssets();
        BuildTimelinePane();
        BuildInspectorPane();
        RefreshPreview();
    }

    void ShowLaneContextMenu(int trackIdx, float atTime)
    {
        var menu = new GenericMenu();
        if (clipboard.Count > 0)
            menu.AddItem(new GUIContent($"วางที่นี่ ({clipboard.Count} คลิป)  Ctrl+V"), false, () => PasteClipboard(atTime, trackIdx));
        else
            menu.AddDisabledItem(new GUIContent("วางที่นี่ — คลิปบอร์ดว่าง"));
        menu.AddItem(new GUIContent("เพิ่มท่าตรงนี้…"), false, () => ShowAddClipMenu(trackIdx, atTime));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("เลือกทั้งหมด  Ctrl+A"), false, SelectAllInTimeline);
        menu.ShowAsContext();
    }
}
