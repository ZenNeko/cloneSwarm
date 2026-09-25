using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Clip = BossTimelineAction.TimelineClip;

/// <summary>
/// ระดับความยากใน Boss Designer — ท่าต่อยอดแบบ Rabbit and Steel ในไฟล์บอสไฟล์เดียว
///
///   toolbar "ระดับ"   ดูแบบระดับไหน · คลิปที่ไม่ออกในระดับนั้นจาง · แผนผัง/แถบปลอดภัย/ทดสอบในเกม ตาม
///   ป้ายบนคลิป        [H+] = ตั้งแต่ Hard · [≤N] = ถึง Normal · [H–S] = Hard ถึง Savage
///   แผงขวา            "ออกเฉพาะระดับ" + ตั้งแต่ / ถึง
///
/// ตัวเลข (HP · ดาเมจ · เวลาเตือน · enrage) ไม่ได้อยู่ที่นี่ — อยู่ใน DifficultyProfile (Resources/Difficulty)
/// การ์ดและพรีวิวแสดงค่าฐาน (Normal) · ตอนเล่นจริงคูณตามระดับ
/// </summary>
public partial class BossDesignerWindow
{
    const string PrefViewTier = "CloneSwarm.BossDesigner.ViewTier";   // -1 = ทุกระดับ

    static readonly string[] TierShort = { "E", "N", "H", "S", "X" };   // Easy Normal Hard Savage Epic(X)

    /// <summary>ระดับที่กำลังดู · null = ดูทุกคลิป</summary>
    static DifficultyTier? ViewTier
    {
        get { int v = EditorPrefs.GetInt(PrefViewTier, -1); return v < 0 ? null : (DifficultyTier)v; }
        set
        {
            EditorPrefs.SetInt(PrefViewTier, value.HasValue ? (int)value.Value : -1);
            CloneSwarm.EditorTools.BossPatternGeometry.ViewTier = value;
        }
    }

    VisualElement BuildViewTierField()
    {
        CloneSwarm.EditorTools.BossPatternGeometry.ViewTier = ViewTier;

        var choices = new System.Collections.Generic.List<string> { "ทุกระดับ" };
        choices.AddRange(System.Enum.GetNames(typeof(DifficultyTier)));
        int idx = ViewTier.HasValue ? (int)ViewTier.Value + 1 : 0;

        var field = new PopupField<string>("ระดับ", choices, idx);
        field.tooltip = "ดูบอสแบบระดับไหน — คลิปที่ตั้ง \"ออกเฉพาะระดับ\" ไว้แล้วไม่ออกในระดับนี้จะจาง\n" +
                        "แผนผัง · แถบปลอดภัย · ▶ ทดสอบในเกม ใช้ระดับนี้\n" +
                        "ตัวคูณ HP/ดาเมจ/เวลาเตือนของระดับอยู่ใน Resources/Difficulty/DifficultyProfile_*";
        field.labelElement.style.minWidth = 34;
        field.style.marginLeft = 8;
        field.RegisterValueChangedCallback(e =>
        {
            int i = choices.IndexOf(e.newValue);
            ViewTier = i <= 0 ? (DifficultyTier?)null : (DifficultyTier)(i - 1);
            BuildTimelinePane();
            RefreshPreview();
        });
        return field;
    }

    static bool ClipVisibleInView(Clip clip) => !ViewTier.HasValue || clip.ActiveIn(ViewTier.Value);

    /// <summary>ป้ายช่วงระดับ · "" = ทุกระดับ</summary>
    static string TierBadge(Clip clip)
    {
        if (!clip.limitTiers) return "";
        int lo = (int)clip.minTier, hi = (int)clip.maxTier;
        int last = TierShort.Length - 1;
        if (lo > hi) return "[✕] ";
        if (lo == 0 && hi == last) return "";
        if (hi == last) return $"[{TierShort[lo]}+] ";
        if (lo == 0) return $"[≤{TierShort[hi]}] ";
        return lo == hi ? $"[{TierShort[lo]}] " : $"[{TierShort[lo]}–{TierShort[hi]}] ";
    }

    /// <summary>แผงขวา: ออกเฉพาะระดับ — แก้ที่ timeline (คลิปไม่ใช่ Object) จึงใช้ RecordObject ตรงๆ</summary>
    VisualElement BuildClipTierRow(Clip clip)
    {
        var box = new VisualElement();
        var names = new System.Collections.Generic.List<string>(System.Enum.GetNames(typeof(DifficultyTier)));

        var limit = new Toggle("ออกเฉพาะระดับ") { value = clip.limitTiers };
        limit.tooltip = "ปิด = ออกทุกระดับ · เปิด = ออกเฉพาะช่วงที่ตั้ง\n" +
                        "ท่าต่อยอด: วางคลิปเพิ่มแล้วตั้ง \"ตั้งแต่ Hard\"\n" +
                        "เปลี่ยนท่าเดิมเป็นแบบยาก: คลิปเดิม \"ถึง Hard\" + คลิปใหม่ \"ตั้งแต่ Savage\" ที่เวลาเดียวกัน";
        box.Add(limit);

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        var from = new PopupField<string>("ตั้งแต่", names, (int)clip.minTier);
        var to   = new PopupField<string>("ถึง", names, (int)clip.maxTier);
        from.style.flexGrow = to.style.flexGrow = 1;
        from.labelElement.style.minWidth = 40;
        to.labelElement.style.minWidth = 24;
        row.Add(from);
        row.Add(to);
        row.style.display = clip.limitTiers ? DisplayStyle.Flex : DisplayStyle.None;
        box.Add(row);

        void Apply(System.Action change, string undo)
        {
            Undo.RecordObject(timeline, undo);
            change();
            EditorUtility.SetDirty(timeline);
            BuildTimelinePane();
            RefreshPreview();
        }

        limit.RegisterValueChangedCallback(e =>
        {
            Apply(() => clip.limitTiers = e.newValue, "Clip Tier Range");
            row.style.display = e.newValue ? DisplayStyle.Flex : DisplayStyle.None;
        });
        from.RegisterValueChangedCallback(e => Apply(() =>
        {
            clip.minTier = (DifficultyTier)names.IndexOf(e.newValue);
            if (clip.maxTier < clip.minTier) { clip.maxTier = clip.minTier; to.SetValueWithoutNotify(names[(int)clip.maxTier]); }
        }, "Clip Tier Range"));
        to.RegisterValueChangedCallback(e => Apply(() =>
        {
            clip.maxTier = (DifficultyTier)names.IndexOf(e.newValue);
            if (clip.minTier > clip.maxTier) { clip.minTier = clip.maxTier; from.SetValueWithoutNotify(names[(int)clip.minTier]); }
        }, "Clip Tier Range"));
        return box;
    }
}
