using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Clip = BossTimelineAction.TimelineClip;

/// <summary>
/// ลากขอบคลิปเพื่อยืด/หดเวลา แบบโปรแกรมตัดต่อ
///
///   ขอบขวา = จุดเริ่มอยู่กับที่ ยืดท้าย
///   ขอบซ้าย = จุดจบอยู่กับที่ (ท่ายังระเบิดเวลาเดิม) เลื่อนจุดเริ่ม — ใช้ "เตือนนานขึ้นโดยไม่เลื่อนจังหวะดาเมจ"
///   snap 0.1s · กด Alt ระหว่างลาก = 0.01s
///
/// ═══ ความยาวคลิปไม่ใช่ฟิลด์ ═══
/// ความยาวที่วาดมาจาก BossAction.GetEditorDuration() ซึ่งคำนวณจากค่าของท่า · การลากขอบจึงแก้ฟิลด์
/// ที่ "เป็นความยาว" ของท่าชนิดนั้น (ดู <see cref="DurationField"/>) · ท่าที่ความยาวมาจากท่าลูก
/// (Combo / Random / timeline ซ้อน) ไม่มีขอบให้ลาก — แก้ที่ท่าลูกแทน
///
/// ═══ ทำไมตรวจระยะขอบบนตัวคลิป ไม่ใช้ element ลูกเป็นมือจับ ═══
/// รอบแรกใช้แถบลูกกว้าง 6px ชิดขอบ · แต่ลูก absolute วางในกรอบ padding (ด้านในเส้นขอบ 1–3px)
/// คลิกโดนเส้นขอบขาวของคลิปที่เลือกอยู่ = โดนตัวคลิป → กลายเป็นลากย้ายแทน · ใช้ไม่ได้จริง
/// ตอนนี้ตัวคลิปดูตำแหน่งเมาส์เอง (ใน <see cref="EdgeZone"/> px จากขอบนอก) ใน TrickleDown
/// ซึ่งวิ่งก่อน handler ลากย้าย แล้ว StopImmediatePropagation กันไม่ให้ตัวลากย้ายทำงานซ้อน
/// </summary>
public partial class BossDesignerWindow
{
    const float MinMechanicSec = 0.2f;
    const float EdgeZone       = 8f;

    /// <summary>ฟิลด์ที่ขอบคลิปแก้ · null = ท่านี้ยืดไม่ได้</summary>
    static (string prop, string label) DurationField(BossAction a) => a switch
    {
        TetherAction       => ("tetherDuration",   "เวลาโซ่"),
        KeepMovingAction   => ("mechanicDuration", "เวลาที่ต้องเดิน"),
        SpawnAoEActionBase => ("warningDuration",  "เวลาเตือน"),
        _                  => (null, null),
    };

    // เคอร์เซอร์ลูกศรซ้ายขวา — UI Toolkit ฝั่ง Editor ตั้ง MouseCursor ได้ผ่าน defaultCursorId (internal)
    static StyleCursor? s_resizeCursor;
    static StyleCursor ResizeCursor()
    {
        if (s_resizeCursor.HasValue) return s_resizeCursor.Value;
        object boxed = new UnityEngine.UIElements.Cursor();
        var p = typeof(UnityEngine.UIElements.Cursor).GetProperty("defaultCursorId",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        p?.SetValue(boxed, (int)MouseCursor.ResizeHorizontal);
        s_resizeCursor = new StyleCursor((UnityEngine.UIElements.Cursor)boxed);
        return s_resizeCursor.Value;
    }

    void AddResizeHandles(VisualElement el, Clip clip)
    {
        var (prop, label) = DurationField(clip.action);
        if (prop == null) return;

        // แถบสว่างบอกตำแหน่งมือจับ — ภาพอย่างเดียว ไม่รับเมาส์
        var gripL = Grip(left: true);
        var gripR = Grip(left: false);
        el.Add(gripL);
        el.Add(gripR);

        int edge = 0;                 // -1 ซ้าย · 1 ขวา · 0 ไม่ได้ลากขอบ
        SerializedObject so = null;
        SerializedProperty sp = null;
        float origVal = 0f, origStart = 0f, accum = 0f;
        string baseTooltip = null;

        int EdgeAt(Vector2 local)
        {
            float w = el.resolvedStyle.width;
            float zone = Mathf.Min(EdgeZone, w * 0.3f);   // คลิปสั้นยังเหลือกลางไว้ลากย้าย
            if (local.x <= zone) return -1;
            if (local.x >= w - zone) return 1;
            return 0;
        }

        void ShowHover(int e)
        {
            gripL.style.backgroundColor = new Color(1f, 1f, 1f, e == -1 ? 0.55f : 0f);
            gripR.style.backgroundColor = new Color(1f, 1f, 1f, e ==  1 ? 0.55f : 0f);
            el.style.cursor = e != 0 ? ResizeCursor() : new StyleCursor(StyleKeyword.Null);
        }

        el.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (edge == 0)
            {
                if (!el.HasPointerCapture(e.pointerId)) ShowHover(EdgeAt(e.localPosition));
                return;
            }
            if (!el.HasPointerCapture(e.pointerId) || sp == null) return;

            accum += e.deltaPosition.x;
            float d = Snap(accum / pxPerSec, e.altKey);
            float val;
            if (edge < 0)
            {
                // เลื่อนจุดเริ่มได้ไม่เกิน 0 และไม่ทำให้ท่าสั้นกว่าขั้นต่ำ
                d = Mathf.Clamp(d, -origStart, origVal - MinMechanicSec);
                val = origVal - d;
                clip.startTime = Mathf.Max(0f, origStart + d);
                el.style.left = clip.startTime * pxPerSec;
            }
            else val = Mathf.Max(MinMechanicSec, origVal + d);

            sp.floatValue = Mathf.Round(val * 100f) / 100f;
            so.ApplyModifiedPropertiesWithoutUndo();
            el.style.width = Mathf.Max(Mathf.Max(clip.action.GetEditorDuration(), 0.3f) * pxPerSec, 24f);
            el.tooltip = $"{label} {sp.floatValue:0.00}s · start {clip.startTime:0.00}s";
            e.StopImmediatePropagation();
        }, TrickleDown.TrickleDown);

        el.RegisterCallback<PointerLeaveEvent>(_ => { if (edge == 0) ShowHover(0); });

        el.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0 || e.ctrlKey || e.commandKey || e.shiftKey) return;
            int at = EdgeAt(e.localPosition);
            if (at == 0) return;   // กลางคลิป → ปล่อยให้ตัวลากย้ายทำงาน

            if (!IsSelected(clip)) SelectOnly(clip);
            SelectTrack(TrackOf(clip));

            Undo.RecordObject(clip.action, "Resize Clip");
            if (at < 0) Undo.RecordObject(timeline, "Resize Clip");
            so = new SerializedObject(clip.action);
            sp = so.FindProperty(prop);
            if (sp == null) return;

            edge = at;
            origVal = sp.floatValue;
            origStart = clip.startTime;
            accum = 0f;
            baseTooltip = el.tooltip;
            el.CapturePointer(e.pointerId);
            e.StopImmediatePropagation();   // ไม่ให้ handler ลากย้ายเริ่มทำงาน
        }, TrickleDown.TrickleDown);

        el.RegisterCallback<PointerUpEvent>(e =>
        {
            if (edge == 0) return;
            int was = edge;
            edge = 0;
            if (el.HasPointerCapture(e.pointerId)) el.ReleasePointer(e.pointerId);
            ShowHover(0);
            e.StopImmediatePropagation();

            if (sp == null || Mathf.Approximately(sp.floatValue, origVal))
            {
                el.tooltip = baseTooltip;
                return;
            }

            EditorUtility.SetDirty(clip.action);
            if (was < 0) EditorUtility.SetDirty(timeline);
            // แถบเตือนในคลิป · ruler · แถบปลอดภัย · การ์ด ต้องตามค่าใหม่
            BuildTimelinePane();
            BuildInspectorPane();
            RefreshPreview();
        }, TrickleDown.TrickleDown);
    }

    static VisualElement Grip(bool left)
    {
        var g = new VisualElement { pickingMode = PickingMode.Ignore };
        g.style.position = Position.Absolute;
        g.style.top = 2;
        g.style.bottom = 2;
        g.style.width = 3;
        if (left) g.style.left = 1; else g.style.right = 1;
        g.style.backgroundColor = new Color(1f, 1f, 1f, 0f);
        g.style.borderTopLeftRadius = g.style.borderTopRightRadius = 1;
        g.style.borderBottomLeftRadius = g.style.borderBottomRightRadius = 1;
        return g;
    }
}
