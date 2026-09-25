using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Clip = BossTimelineAction.TimelineClip;

/// <summary>
/// "รวมเป็นท่าสุ่ม 🎲" — เลือกหลายคลิป แล้วให้บอสหยิบหนึ่งท่าต่อไฟต์ตามค่า roll
///
/// สร้าง RandomAttackAction (sub-asset) ที่มีท่าที่เลือกอยู่ในกอง + roll แบบ Variant ช่องเท่าจำนวนท่า
/// วางแทนคลิปเดิมที่เวลาของคลิปแรกสุด · ท่าในกองเริ่มพร้อมกันเสมอ (ระยะห่างเดิมระหว่างคลิปหายไป)
///
/// อยากให้สองกองออกช่องเดียวกัน (วงไล่↔วง · โดนัทไล่↔โดนัท): การ์ดของกองที่สอง → สุ่ม → "ร่วมกับ: roll ของกองแรก"
/// ลำดับในกองสำคัญ — ช่อง 0 ของทุกกองออกพร้อมกัน
/// </summary>
public partial class BossDesignerWindow
{
    void MergeSelectionToRandom()
    {
        if (config == null || timeline == null) return;
        var sel = SelectedClips();
        if (sel.Count < 2) return;

        var pool = sel.Select(x => x.clip.action).Where(a => a != null).Distinct().ToList();
        if (pool.Count < 2)
        {
            ShowNotification(new GUIContent("คลิปที่เลือกใช้ท่าเดียวกัน — ไม่มีอะไรให้สุ่ม"), 1.5);
            return;
        }

        float t0 = sel.Min(x => x.clip.startTime);
        int   tr = sel.First(x => Mathf.Approximately(x.clip.startTime, t0)).track;
        bool  spread = sel.Any(x => !Mathf.Approximately(x.clip.startTime, t0));

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        var ra = ScriptableObject.CreateInstance<RandomAttackAction>();
        ra.name = MakeUniqueSubAssetName("สุ่ม_" + string.Join("_", pool.Select(a => a.name)));
        ra.attackPool = pool;
        ra.attackCount = 1;
        ra.delayBetweenPicks = 0f;
        Undo.RegisterCreatedObjectUndo(ra, "Merge to Random");
        AssetDatabase.AddObjectToAsset(ra, config);

        ra.rollName = CreateRoll(ra, ("สลับท่าในกอง", RollKind.Variant, pool.Count));

        Undo.RecordObject(timeline, "Merge to Random");
        foreach (var (c, t) in sel) timeline.tracks[t].clips.Remove(c);
        var clip = new Clip { action = ra, startTime = t0 };
        timeline.tracks[tr].clips.Add(clip);
        Undo.CollapseUndoOperations(group);

        EditorUtility.SetDirty(timeline);
        EditorUtility.SetDirty(ra);
        AssetDatabase.SaveAssets();

        selection.Clear();
        selectedClip = clip;
        RefreshAudit();
        UpdateToolbarStatus();
        BuildTimelinePane();
        BuildInspectorPane();
        RefreshPreview();

        // ท่าในกองซ้ำเท่ากันหมด → ย้ายการซ้ำมาที่กอง (ผลเหมือนเดิมทุกอย่างในโหมด "หยิบครั้งเดียว")
        // จะได้เปลี่ยนเป็นสลับ/หยิบใหม่ทุกนัดได้ทันทีจากการ์ด
        var aoes = pool.OfType<SpawnAoEActionBase>().ToList();
        bool hoisted = aoes.Count == pool.Count && aoes[0].repeatCount > 1 &&
                       aoes.All(a => a.repeatCount == aoes[0].repeatCount &&
                                     Mathf.Approximately(a.repeatInterval, aoes[0].repeatInterval));
        if (hoisted) HoistRepeat(ra);

        if (spread || hoisted)
            ShowNotification(new GUIContent(
                (spread ? $"รวม {pool.Count} ท่า — เริ่มพร้อมกันที่ {t0:0.0}s" : $"รวม {pool.Count} ท่า") +
                (hoisted ? $"\nย้ายการซ้ำ ×{ra.repeatCount} มาไว้ที่ท่าสุ่ม" : "")), 2.5);
    }

    /// <summary>
    /// ย้ายการยิงซ้ำจากท่าในกองมาไว้ที่กอง — กองคุมว่าแต่ละนัดหยิบยังไง (ครั้งเดียว / ใหม่ / สลับ)
    /// ท่าในกองที่มีที่อื่นใช้อยู่ (เฟสอื่น · กองอื่น) ถูกก๊อปก่อนแก้ ไม่งั้นที่อื่นเหลือยิงนัดเดียวเงียบๆ
    /// </summary>
    void HoistRepeat(RandomAttackAction ra)
    {
        if (ra?.attackPool == null || config == null) return;
        var kids = ra.attackPool.OfType<SpawnAoEActionBase>().Where(a => a.repeatCount > 1).ToList();
        if (kids.Count == 0) return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.RecordObject(ra, "Hoist Repeat");
        ra.repeatCount    = kids.Max(a => a.repeatCount);
        ra.repeatInterval = kids[0].repeatInterval;

        for (int i = 0; i < ra.attackPool.Count; i++)
        {
            if (ra.attackPool[i] is not SpawnAoEActionBase a || a.repeatCount <= 1) continue;
            var target = a;
            if (!IsOwnedSubAsset(a) || IsReferencedInConfig(a, ignore: ra))
            {
                target = Instantiate(a);
                target.name = MakeUniqueSubAssetName(a.name);
                Undo.RegisterCreatedObjectUndo(target, "Hoist Repeat");
                AssetDatabase.AddObjectToAsset(target, config);
                ra.attackPool[i] = target;
            }
            Undo.RecordObject(target, "Hoist Repeat");
            target.repeatCount = 1;
            EditorUtility.SetDirty(target);
        }
        Undo.CollapseUndoOperations(group);

        EditorUtility.SetDirty(ra);
        AssetDatabase.SaveAssets();
        BuildTimelinePane();
        BuildInspectorPane();
        RefreshPreview();
    }

    /// <summary>ย้อน "รวมเป็นท่าสุ่ม" — ท่าในกองกลับเป็นคลิปเรียงลงเลนถัดๆ ไป เวลาเดียวกัน</summary>
    void UnpackRandom(Clip clip, int trackIdx)
    {
        if (clip?.action is not RandomAttackAction ra || timeline == null) return;
        var pool = (ra.attackPool ?? new List<BossAction>()).Where(a => a != null).ToList();
        if (pool.Count == 0) return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.RecordObject(timeline, "Unpack Random");

        timeline.tracks[trackIdx].clips.Remove(clip);
        selection.Clear();
        for (int i = 0; i < pool.Count; i++)
        {
            // เลนละท่า — ทับกันบนเลนเดียวแล้วคลิกแยกไม่ได้ · เลนไม่พอสร้างเพิ่ม
            int tr = trackIdx + i;
            while (tr >= timeline.tracks.Count)
                timeline.tracks.Add(new BossTimelineAction.TimelineTrack { trackName = $"Track {timeline.tracks.Count + 1}" });
            timeline.tracks[tr].clips ??= new List<Clip>();
            var c = new Clip { action = pool[i], startTime = clip.startTime };
            timeline.tracks[tr].clips.Add(c);
            selection.Add(c);
        }
        selectedClip = selection.First();

        if (IsOwnedSubAsset(ra) && !IsReferencedInConfig(ra))
            Undo.DestroyObjectImmediate(ra);
        Undo.CollapseUndoOperations(group);

        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();
        BuildTimelinePane();
        BuildInspectorPane();
        RefreshPreview();
    }
}
