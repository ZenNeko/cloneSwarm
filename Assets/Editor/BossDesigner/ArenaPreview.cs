using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Shape = CloneSwarm.EditorTools.BossPatternGeometry.Shape;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// แผนผังสนามมองจากด้านบน — วาดว่าท่าที่ active ณ playhead ลงตรงไหนบ้าง · และลากแก้ได้
    ///
    /// ═══ ทำไมต้องมี ═══
    ///
    /// Boss Designer เดิมเห็นแค่แกน **เวลา** · แกนที่ทำให้แพตเทิร์นเป็นแพตเทิร์นจริงๆ คือ
    /// **ตำแหน่ง** กับ **ความสุ่ม** ซึ่งมองไม่เห็นเลยจนกว่าจะกด Play
    ///
    /// ═══ ใช้สูตรเดียวกับตอนยิงจริง ═══
    ///
    /// ทรงทั้งหมดมาจาก <see cref="BossPatternGeometry"/> ซึ่งเรียก
    /// <see cref="SpawnAoEActionBase.ResolveWave"/> ตัวเดียวกับที่เซิร์ฟเวอร์ใช้ · แถบความปลอดภัยใต้
    /// timeline ก็ใช้ชุดเดียวกัน ตัวเลข % กับภาพจึงตรงกันเสมอ
    ///
    /// ═══ ลากแก้ในสนาม (2026-09-24) ═══
    ///
    /// ทรงของคลิปที่เลือกอยู่ (ขอบหนา):
    ///   ลากตัวทรง → ย้าย · โหมด "จุดในสนาม" ดูดเข้าจุดที่ใกล้สุด + ตั้งระยะจากกลาง ·
    ///                       โหมดอื่นแก้ targetOffset (ปัด 0.5m)
    ///   ลากขอบ    → ขนาด (รัศมี · ความยาวเส้น/กากบาท · รัศมีนอก/ในของโดนัท) ปัด 0.5m
    /// แก้ผ่าน SerializedObject → มี Undo · ยิง <see cref="Edited"/> ให้หน้าต่างรีเฟรชการ์ด
    /// </summary>
    public class ArenaPreview : VisualElement
    {
        const float Margin = 14f;
        const float EdgeGrabPx = 7f;
        const float SnapMeters = 0.5f;

        BossEncounterConfig _config;
        BossTimelineAction  _timeline;
        float               _playhead;
        object              _selected;     // TimelineClip ที่เลือกอยู่ (ไฮไลต์ + ลากแก้ได้)
        int                 _rollSeed = 1;

        /// <summary>ค่าของท่าถูกแก้จากการลากในสนาม — หน้าต่างรีเฟรชการ์ด/timeline</summary>
        public System.Action Edited;

        readonly Label _header = new Label();
        readonly Label _footer = new Label();
        readonly VisualElement _canvas = new VisualElement();

        float _lastWidth = -1f;

        readonly List<Shape>   _shapes  = new();
        readonly List<Vector3> _players = new();
        BossPatternGeometry.Arena _arena;
        float _safe = 1f;

        // ── สถานะการลาก ──
        enum Drag { None, Move, Resize, Rotate }
        Drag    _drag;
        Shape   _dragShape;
        Vector3 _dragStartWorld;
        SerializedObject _dragSo;
        float _rotStartStored, _rotStartShown, _rotStartPointer;

        const float KnobGapPx = 16f;   // จุดจับหมุนอยู่เลยปลายทรงออกไปเท่านี้
        const float KnobR     = 6f;

        public ArenaPreview()
        {
            style.flexShrink = 0;
            style.marginBottom = 6;

            _header.style.fontSize = BossDesignerWindow.Fs(10);
            _header.style.opacity = 0.75f;
            _header.style.whiteSpace = WhiteSpace.Normal;
            Add(_header);

            _canvas.style.flexGrow = 0;
            _canvas.style.flexShrink = 0;
            _canvas.generateVisualContent += OnPaint;
            _canvas.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _canvas.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _canvas.RegisterCallback<PointerUpEvent>(OnPointerUp);
            Add(_canvas);

            _footer.style.fontSize = BossDesignerWindow.Fs(10);
            _footer.style.opacity = 0.7f;
            _footer.style.whiteSpace = WhiteSpace.Normal;
            _footer.style.marginTop = 2;
            Add(_footer);

            // แผงขวากว้างคงที่ แต่ผู้ใช้ย่อหน้าต่างได้ — ผูกสูงตามกว้างให้เป็นจัตุรัสเสมอ
            RegisterCallback<GeometryChangedEvent>(_ =>
            {
                float w = resolvedStyle.width;
                if (w > 1f && !Mathf.Approximately(_lastWidth, w))
                {
                    _lastWidth = w;
                    _canvas.style.height = w;
                    _canvas.MarkDirtyRepaint();
                }
            });
        }

        public int RollSeed
        {
            get => _rollSeed;
            set { _rollSeed = value; }
        }

        public void SetContext(BossEncounterConfig config, BossTimelineAction timeline,
                               float playhead, object selectedClip)
        {
            _config   = config;
            _timeline = timeline;
            _playhead = playhead;
            _selected = selectedClip;
            if (_drag == Drag.None) Rebuild();   // ระหว่างลาก Rebuild ทำเองทุก move อยู่แล้ว
        }

        void Rebuild()
        {
            _arena = BossPatternGeometry.ArenaOf(_config);
            BossPatternGeometry.Collect(_config, _timeline, _playhead, _rollSeed, _selected,
                                        _shapes, _players, BossDesignerWindow.ClipColorOf);
            _safe = BossPatternGeometry.SafeFraction(_arena, _shapes);
            UpdateLabels();
            _canvas.MarkDirtyRepaint();
        }

        void UpdateLabels()
        {
            string shape = _arena.square ? "Square" : "Circle";
            _header.text = _arena.missing
                ? $"⚠ ไม่ได้ผูก arena — วาดด้วยค่าสมมติ {shape} r={_arena.radius:0.#} · ท่าที่ใช้จุดในสนามจะเพี้ยนตอนรันจริง"
                : $"{_config.arena.name} · {shape} r={_arena.radius:0.#} · t = {_playhead:0.0}s";

            var names = BossPatternGeometry.ActiveClips(_timeline, _playhead)
                                           .Select(c => c.action.name).Distinct().ToList();
            int unknown = _shapes.Count(s => s.unknownSize);

            string safe = _shapes.Count == 0 ? ""
                : _safe <= 0.001f ? "\n⛔ ไม่มีที่ให้หลบเลยที่เวลานี้"
                : _safe < 0.1f    ? $"\n⚠ ที่ปลอดภัยเหลือ {_safe:P0} ของสนาม — แทบไม่มีที่หลบ"
                : $"\nปลอดภัย {_safe:P0} ของสนาม";

            _footer.text = (names.Count == 0
                    ? "ไม่มีท่าที่ active ที่เวลานี้ — ลากไม้บรรทัดไปที่คลิป หรือกด ▶"
                    : $"active: {string.Join(" · ", names)}")
                + safe
                + (unknown > 0 ? $"\n⚠ {unknown} ทรงอ่านขนาดไม่ได้ (วาดเป็นจุด)" : "")
                + (_selected != null && _shapes.Any(s => s.selected) ? "\nลากทรงที่เลือกเพื่อย้าย · ลากขอบเพื่อปรับขนาด" : "");
        }

        // ══════════════════════════════════════════════════════════════════
        // แปลงพิกัด
        // ══════════════════════════════════════════════════════════════════
        float Size => _canvas.resolvedStyle.width;

        Vector2 ToLocal(Vector3 w, float size)
        {
            float s = (size - Margin * 2f) / (_arena.radius * 2f);
            float mid = size * 0.5f;
            // UI Toolkit แกน y ชี้ลง · โลกแกน z ชี้เหนือ — กลับทิศให้เหนืออยู่บน
            return new Vector2(mid + (w.x - _arena.center.x) * s, mid - (w.z - _arena.center.z) * s);
        }

        Vector3 ToWorld(Vector2 local, float size)
        {
            float s = (size - Margin * 2f) / (_arena.radius * 2f);
            float mid = size * 0.5f;
            return _arena.center + new Vector3((local.x - mid) / s, 0f, (mid - local.y) / s);
        }

        float ToPx(float meters, float size) => meters * ((size - Margin * 2f) / (_arena.radius * 2f));

        static float SnapM(float v) => Mathf.Round(v / SnapMeters) * SnapMeters;

        // ══════════════════════════════════════════════════════════════════
        // ลากแก้
        // ══════════════════════════════════════════════════════════════════
        void OnPointerDown(PointerDownEvent e)
        {
            if (e.button != 0 || _selected == null) return;
            float size = Size;
            if (size <= 1f) return;

            Vector3 w = ToWorld(e.localPosition, size);
            float grabM = EdgeGrabPx / Mathf.Max(0.001f, ToPx(1f, size));

            // จุดจับหมุนก่อน — อยู่นอกตัวทรง ไม่ชนกับขอบ/ตัว
            foreach (var s in _shapes.Where(s => s.selected && Rotatable(s)))
            {
                if (Vector2.Distance(e.localPosition, KnobPos(s, size)) <= KnobR + 4f)
                {
                    BeginRotate(s, w, e);
                    return;
                }
            }

            // ขอบก่อนตัว — ทรงเล็กจะได้ยังจับขอบได้
            foreach (var s in _shapes.Where(s => s.selected && !s.unknownSize))
            {
                if (NearEdge(s, w, grabM)) { Begin(Drag.Resize, s, w, e); return; }
            }
            foreach (var s in _shapes.Where(s => s.selected))
            {
                if (BossPatternGeometry.Contains(s, w)) { Begin(Drag.Move, s, w, e); return; }
            }
        }

        void Begin(Drag kind, Shape s, Vector3 w, PointerDownEvent e)
        {
            _drag = kind;
            _dragShape = s;
            _dragStartWorld = w;
            _dragSo = new SerializedObject(s.action);
            Undo.RecordObject(s.action, kind == Drag.Move ? "Move AoE in Arena" : "Resize AoE in Arena");
            _canvas.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        void OnPointerMove(PointerMoveEvent e)
        {
            if (_drag == Drag.None || !_canvas.HasPointerCapture(e.pointerId)) return;
            Vector3 w = ToWorld(e.localPosition, Size);
            _dragSo.Update();

            if (_drag == Drag.Move)        ApplyMove(w);
            else if (_drag == Drag.Rotate) ApplyRotate(w, e.altKey);
            else                           ApplyResize(w);

            _dragSo.ApplyModifiedPropertiesWithoutUndo();   // Undo บันทึกไว้แล้วใน Begin
            EditorUtility.SetDirty(_dragShape.action);
            Rebuild();
        }

        void OnPointerUp(PointerUpEvent e)
        {
            if (_drag == Drag.None) return;
            if (_canvas.HasPointerCapture(e.pointerId)) _canvas.ReleasePointer(e.pointerId);
            _drag = Drag.None;
            _dragSo = null;
            Edited?.Invoke();
        }

        void ApplyMove(Vector3 cursor)
        {
            var mode = _dragSo.FindProperty("targetingMode");
            if (mode != null && mode.enumValueIndex == (int)SpawnAoEActionBase.TargetingMode.ArenaAnchor)
            {
                // ดูดเข้าจุดในสนามที่ใกล้สุด (กลาง · 4 ทิศ · 4 ทแยง) แล้วตั้งระยะจากกลางตามที่ลาก
                Vector3 rel = cursor - _arena.center; rel.y = 0f;
                float scale = Mathf.Clamp(Mathf.Round(rel.magnitude / _arena.radius * 20f) / 20f, 0f, 1.5f);

                ArenaAnchor best = ArenaAnchor.Center;
                if (scale >= 0.1f)
                {
                    float bestDot = -2f;
                    foreach (var a in new[] { ArenaAnchor.N, ArenaAnchor.NE, ArenaAnchor.E, ArenaAnchor.SE,
                                              ArenaAnchor.S, ArenaAnchor.SW, ArenaAnchor.W, ArenaAnchor.NW })
                    {
                        Vector3 dir = ArenaAnchors.Resolve(_arena.def, a, 1f) - _arena.center; dir.y = 0f;
                        float dot = Vector3.Dot(dir.normalized, rel.normalized);
                        if (dot > bestDot) { bestDot = dot; best = a; }
                    }
                }
                _dragSo.FindProperty("arenaAnchor").enumValueIndex = (int)best;
                _dragSo.FindProperty("arenaDistanceScale").floatValue = best == ArenaAnchor.Center ? 1f : scale;
                return;
            }

            // โหมดอื่น: เลื่อน targetOffset ตามระยะที่ลาก (โหมดผู้เล่น = เลื่อนจากตัวผู้เล่นทุกคนเท่ากัน)
            var off = _dragSo.FindProperty("targetOffset");
            Vector3 delta = cursor - _dragStartWorld;
            _dragStartWorld = cursor;
            Vector3 v = off.vector3Value + new Vector3(delta.x, 0f, delta.z);
            off.vector3Value = new Vector3(SnapM(v.x), v.y, SnapM(v.z));
            // ปัดแล้วส่วนที่ปัดทิ้งต้องไม่หาย — เก็บจุดเริ่มเทียบกับค่าที่ปัดแล้ว
            _dragStartWorld -= (v - off.vector3Value);
        }

        // ── หมุน ──────────────────────────────────────────────────────────
        static bool Rotatable(in Shape s) =>
            !s.unknownSize && (s.type == AoEType.Line || s.type == AoEType.Cone || s.type == AoEType.Cross);

        /// <summary>ระยะ (เมตร) จากจุดเกิดถึงปลายทรงทางด้านหน้า</summary>
        static float ReachM(in Shape s) => s.type switch
        {
            AoEType.Line  => s.lineLength,
            AoEType.Cross => s.lineLength * 0.5f,
            _             => s.radius,
        };

        Vector2 KnobPos(in Shape s, float size)
        {
            Vector2 c = ToLocal(s.world, size);
            return c + Forward(s.yawDeg) * (ToPx(ReachM(s), size) + KnobGapPx);
        }

        static float YawOf(Vector3 d) => Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;   // 0 = +Z · ตามเข็ม

        /// <summary>
        /// เริ่มหมุน · Line/Cone ที่หันหาผู้เล่นอยู่ถูกเปลี่ยนเป็น "มุมคงที่" ที่มุมที่เห็นตอนนี้
        /// แล้วคิดมุมใหม่เป็น "ส่วนต่าง" จากตอนเริ่ม — มุมที่เห็นคือมุมที่เก็บ + roll (หมุน/พลิก)
        /// ถ้าเอามุมเมาส์ไปใส่ตรงๆ ท่าที่มี roll จะกระโดดหนีเมาส์เท่ากับค่า roll
        /// </summary>
        void BeginRotate(Shape s, Vector3 w, PointerDownEvent e)
        {
            Begin(Drag.Rotate, s, w, e);
            Undo.RecordObject(s.action, "Rotate AoE in Arena");
            var aim = _dragSo.FindProperty("aimMode");
            var ang = _dragSo.FindProperty("angleDegrees");
            if (ang == null) { _drag = Drag.None; return; }

            if (aim != null && s.type != AoEType.Cross &&
                aim.enumValueIndex == (int)SpawnAoEActionBase.AimMode.NearestPlayer)
            {
                aim.enumValueIndex = (int)SpawnAoEActionBase.AimMode.FixedAngle;
                ang.floatValue = Mathf.Round(s.yawDeg);
                _dragSo.ApplyModifiedPropertiesWithoutUndo();
                Rebuild();
                // มุมที่เห็นหลังเปลี่ยนโหมด (roll มีผลแล้ว)
                foreach (var sh in _shapes)
                    if (sh.action == s.action) { s = sh; break; }
                _dragShape = s;
            }

            _rotStartStored  = ang.floatValue;
            _rotStartShown   = s.yawDeg;
            _rotStartPointer = YawOf(w - s.world);
        }

        void ApplyRotate(Vector3 cursor, bool fine)
        {
            var ang = _dragSo.FindProperty("angleDegrees");
            if (ang == null) return;
            // ใช้ส่วนต่างจากตำแหน่งเมาส์ตอนเริ่ม — คลิกโดนจุดจับเยื้องนิดหน่อยก็ไม่กระตุก
            float delta = Mathf.DeltaAngle(_rotStartPointer, YawOf(cursor - _dragShape.world));
            float step  = fine ? 1f : 15f;
            float shown = Mathf.Round((_rotStartShown + delta) / step) * step;
            float v = _rotStartStored + Mathf.DeltaAngle(_rotStartShown, shown);
            ang.floatValue = Mathf.Repeat(v + 180f, 360f) - 180f;   // เก็บเป็น -180..180 อ่านง่าย
        }

        void DrawKnob(Painter2D p, in Shape s, float size)
        {
            Vector2 c    = ToLocal(s.world, size);
            Vector2 tip  = c + Forward(s.yawDeg) * ToPx(ReachM(s), size);
            Vector2 knob = KnobPos(s, size);

            p.strokeColor = new Color(1f, 1f, 1f, 0.8f);
            p.lineWidth = 1.5f;
            p.BeginPath();
            p.MoveTo(tip);
            p.LineTo(knob);
            p.Stroke();

            p.fillColor = new Color(0.35f, 0.65f, 1f, 0.95f);
            p.BeginPath();
            p.Arc(knob, KnobR, Angle.Degrees(0), Angle.Degrees(360));
            p.ClosePath();
            p.Fill(); p.Stroke();
        }

        void ApplyResize(Vector3 cursor)
        {
            var s = _dragShape;
            Vector3 d = cursor - s.world; d.y = 0f;
            float dist = d.magnitude;
            float scale = Mathf.Max(0.01f, s.action.scaleEnd);   // ทรงที่วาดคูณ scaleEnd ไว้ — ค่าที่เก็บต้องหารกลับ
            Vector3 fwd = BossPatternGeometry.ForwardWorld(s.yawDeg);

            void Set(string field, float meters)
            {
                var p = _dragSo.FindProperty(field);
                if (p != null) p.floatValue = Mathf.Max(SnapMeters, SnapM(meters / scale));
            }

            switch (s.type)
            {
                case AoEType.Circle:
                case AoEType.Cone:
                    Set("radius", dist); break;
                case AoEType.Donut:
                    // จับใกล้วงในกว่า = แก้วงใน
                    if (Mathf.Abs(dist - s.innerRadius) < Mathf.Abs(dist - s.radius))
                        Set("innerRadius", Mathf.Min(dist, s.radius - SnapMeters));
                    else Set("radius", Mathf.Max(dist, s.innerRadius + SnapMeters));
                    break;
                case AoEType.Line:
                    Set("lineLength", Vector3.Dot(d, fwd)); break;
                case AoEType.Cross:
                    Set("lineLength", 2f * Mathf.Max(Mathf.Abs(Vector3.Dot(d, fwd)),
                                                     Mathf.Abs(Vector3.Dot(d, new Vector3(fwd.z, 0, -fwd.x)))));
                    break;
            }
        }

        /// <summary>จุดใกล้ขอบที่ลากปรับขนาดได้ไหม (ระยะเป็นเมตร)</summary>
        static bool NearEdge(in Shape s, Vector3 p, float tol)
        {
            Vector3 d = p - s.world; d.y = 0f;
            float dist = d.magnitude;
            Vector3 fwd = BossPatternGeometry.ForwardWorld(s.yawDeg);
            switch (s.type)
            {
                case AoEType.Circle:
                case AoEType.Cone:  return Mathf.Abs(dist - s.radius) <= tol;
                case AoEType.Donut: return Mathf.Abs(dist - s.radius) <= tol || Mathf.Abs(dist - s.innerRadius) <= tol;
                case AoEType.Line:
                {   // ปลายลำ
                    Vector3 tip = s.world + fwd * s.lineLength;
                    Vector3 dt = p - tip; dt.y = 0f;
                    return dt.magnitude <= Mathf.Max(tol, s.lineWidth * 0.5f);
                }
                case AoEType.Cross:
                {
                    float h = s.lineLength * 0.5f;
                    Vector3 side = new Vector3(fwd.z, 0f, -fwd.x);
                    foreach (var tip in new[] { s.world + fwd * h, s.world - fwd * h, s.world + side * h, s.world - side * h })
                    {
                        Vector3 dt = p - tip; dt.y = 0f;
                        if (dt.magnitude <= Mathf.Max(tol, s.lineWidth * 0.5f)) return true;
                    }
                    return false;
                }
            }
            return false;
        }

        // ══════════════════════════════════════════════════════════════════
        // วาด
        // ══════════════════════════════════════════════════════════════════
        void OnPaint(MeshGenerationContext ctx)
        {
            float size = Size;
            if (size <= 1f || float.IsNaN(size)) return;

            var p = ctx.painter2D;
            var mid = new Vector2(size * 0.5f, size * 0.5f);
            float r = ToPx(_arena.radius, size);

            // ── พื้นสนาม — ไม่มีที่หลบ = พื้นแดงจางๆ ให้เห็นทันทีโดยไม่ต้องอ่านตัวเลข ──
            p.fillColor = _shapes.Count > 0 && _safe <= 0.001f ? new Color(1f, 0.2f, 0.2f, 0.12f)
                                                                : new Color(1f, 1f, 1f, 0.04f);
            p.strokeColor = _arena.missing ? new Color(1f, 0.7f, 0.3f, 0.5f) : new Color(1f, 1f, 1f, 0.3f);
            p.lineWidth = 1.5f;
            p.BeginPath();
            if (_arena.square) Rect(p, mid, r * 2f, r * 2f, 0f);
            else               p.Arc(mid, r, Angle.Degrees(0), Angle.Degrees(360));
            p.ClosePath();
            p.Fill();
            p.Stroke();

            // ── เข็มทิศ: เส้นแกน + ครึ่งรัศมี ให้อ่าน anchor กับ distanceScale ออก ──
            p.strokeColor = new Color(1f, 1f, 1f, 0.08f);
            p.lineWidth = 1f;
            p.BeginPath();
            p.MoveTo(new Vector2(mid.x - r, mid.y)); p.LineTo(new Vector2(mid.x + r, mid.y));
            p.MoveTo(new Vector2(mid.x, mid.y - r)); p.LineTo(new Vector2(mid.x, mid.y + r));
            p.Stroke();

            p.BeginPath();
            if (_arena.square) Rect(p, mid, r, r, 0f);
            else               p.Arc(mid, r * 0.5f, Angle.Degrees(0), Angle.Degrees(360));
            p.ClosePath();
            p.Stroke();

            // ── ผู้เล่นสมมติ ───────────────────────────────────────────────
            p.fillColor = new Color(0.45f, 0.8f, 1f, 0.85f);
            foreach (var pl in _players)
            {
                p.BeginPath();
                p.Arc(ToLocal(pl, size), 4f, Angle.Degrees(0), Angle.Degrees(360));
                p.ClosePath();
                p.Fill();
            }

            // ── ทรงของท่า — ที่เลือกวาดทีหลังให้อยู่บนสุด ──────────────────
            foreach (var s in _shapes) if (!s.selected) DrawShape(p, s, size);
            foreach (var s in _shapes) if (s.selected)  DrawShape(p, s, size);
            // จุดจับหมุน (ฟ้า) ของ Line / Cone / Cross ที่เลือก — ลากรอบจุดเกิด · snap 15° · Alt = 1°
            foreach (var s in _shapes) if (s.selected && Rotatable(s)) DrawKnob(p, s, size);

            // ── บอสอยู่กลางสนาม ───────────────────────────────────────────
            p.fillColor = new Color(1f, 1f, 1f, 0.9f);
            p.BeginPath();
            p.Arc(ToLocal(_arena.center, size), 5f, Angle.Degrees(0), Angle.Degrees(360));
            p.ClosePath();
            p.Fill();
        }

        void DrawShape(Painter2D p, Shape s, float size)
        {
            Vector2 c = ToLocal(s.world, size);
            Color fill = s.color; fill.a = s.selected ? 0.55f : 0.3f;
            Color line = s.selected ? Color.white : s.color; line.a = 0.95f;

            p.fillColor = fill;
            p.strokeColor = s.unknownSize ? new Color(1f, 0.75f, 0.3f) : line;
            p.lineWidth = s.selected ? 2.5f : 1.2f;

            switch (s.type)
            {
                case AoEType.Circle:
                    p.BeginPath();
                    p.Arc(c, Mathf.Max(2f, ToPx(s.radius, size)), Angle.Degrees(0), Angle.Degrees(360));
                    p.ClosePath();
                    p.Fill(); p.Stroke();
                    break;

                case AoEType.Donut:
                    // วงนอกตามเข็ม + วงในทวนเข็ม แล้ว fill แบบ OddEven = ได้รูรูปโดนัทจริง
                    p.BeginPath();
                    p.Arc(c, Mathf.Max(2f, ToPx(s.radius, size)), Angle.Degrees(0), Angle.Degrees(360));
                    p.ClosePath();
                    p.Arc(c, Mathf.Max(1f, ToPx(s.innerRadius, size)), Angle.Degrees(360), Angle.Degrees(0),
                          ArcDirection.CounterClockwise);
                    p.ClosePath();
                    p.Fill(FillRule.OddEven); p.Stroke();
                    break;

                case AoEType.Line:
                    // จุดเกิดคือ "ต้นลำ" — ลำพาดไปข้างหน้าตามทิศที่หัน ไม่ได้อยู่กลางลำ
                    p.BeginPath();
                    Rect(p, c + Forward(s.yawDeg) * ToPx(s.lineLength, size) * 0.5f,
                         ToPx(s.lineWidth, size), ToPx(s.lineLength, size), s.yawDeg);
                    p.ClosePath();
                    p.Fill(); p.Stroke();
                    break;

                case AoEType.Cross:
                    p.BeginPath();
                    Rect(p, c, ToPx(s.lineWidth, size), ToPx(s.lineLength, size), s.yawDeg);
                    p.ClosePath();
                    Rect(p, c, ToPx(s.lineLength, size), ToPx(s.lineWidth, size), s.yawDeg);
                    p.ClosePath();
                    p.Fill(); p.Stroke();
                    break;

                case AoEType.Cone:
                {
                    float rad = Mathf.Max(2f, ToPx(s.radius, size));
                    // yaw 0 = เหนือ = ขึ้นบนจอ = -90° ในระบบมุมของ Painter2D
                    float mid = s.yawDeg - 90f;
                    p.BeginPath();
                    p.MoveTo(c);
                    p.Arc(c, rad, Angle.Degrees(mid - s.coneAngle * 0.5f), Angle.Degrees(mid + s.coneAngle * 0.5f));
                    p.ClosePath();
                    p.Fill(); p.Stroke();
                    break;
                }

                default:
                    p.BeginPath();
                    p.Arc(c, 4f, Angle.Degrees(0), Angle.Degrees(360));
                    p.ClosePath();
                    p.Fill(); p.Stroke();
                    break;
            }
        }

        static Vector2 Forward(float yawDeg)
        {
            float rad = yawDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad));   // เหนือ = ขึ้นบนจอ
        }

        /// <summary>
        /// สี่เหลี่ยมหมุนรอบจุดกลางของตัวเอง · local +y = ทิศที่หัน (ตรงกับ Forward)
        /// แกน y ของจอชี้ลง สูตรหมุนจึงไม่ใช่ตัวมาตรฐาน
        /// </summary>
        static void Rect(Painter2D p, Vector2 center, float w, float h, float yawDeg)
        {
            float rad = yawDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            Vector2 R(float x, float y) => center + new Vector2(x * cos + y * sin, x * sin - y * cos);

            float hw = Mathf.Max(1f, w) * 0.5f, hh = Mathf.Max(1f, h) * 0.5f;
            p.MoveTo(R(-hw, -hh));
            p.LineTo(R( hw, -hh));
            p.LineTo(R( hw,  hh));
            p.LineTo(R(-hw,  hh));
        }
    }
}
