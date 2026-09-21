using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// แผนผังสนามมองจากด้านบน — วาดว่าท่าที่ active ณ playhead ลงตรงไหนบ้าง
    ///
    /// ═══ ทำไมต้องมี ═══
    ///
    /// Boss Designer เดิมเห็นแค่แกน **เวลา** · แกนที่ทำให้แพตเทิร์นเป็นแพตเทิร์นจริงๆ คือ
    /// **ตำแหน่ง** กับ **ความสุ่ม** ซึ่งมองไม่เห็นเลยจนกว่าจะกด Play
    /// ผลคือ targetingMode / arenaAnchor / arenaDistanceScale / roll ถูกตั้งแบบเดาแล้วลอง
    /// และระบบ roll ทั้งระบบไม่มี content ตัวไหนใช้เลยสักตัว
    ///
    /// ═══ ใช้สูตรเดียวกับตอนยิงจริง ═══
    ///
    /// ตำแหน่งมาจาก <see cref="SpawnAoEActionBase.ResolveWave"/> ตัวเดียวกับที่เซิร์ฟเวอร์เรียก
    /// ต่างกันแค่ <see cref="AoEWorld"/> ที่ป้อนเข้าไป (ผู้เล่นสมมติ 4 คนแทนของจริง)
    /// ถ้าเขียนสูตรวาดแยกอีกชุด ภาพจะเพี้ยนจากของจริงทันทีที่ใครแก้ข้างเดียว —
    /// และเรนเดอร์ที่โกหกพาไปแก้ผิดที่
    ///
    /// ═══ ขนาดของทรงอ่านด้วย reflection ═══
    ///
    /// ทรงแต่ละแบบเก็บขนาดไว้ในฟิลด์ของ subclass (radius / lineLength / …) ซึ่ง subclass
    /// ส่งต่อให้ TelegraphZone ผ่าน ConfigureTelegraphZone ที่เรียกจาก editor ไม่ได้
    /// จึงอ่านฟิลด์ **ชื่อเดียวกับที่ TelegraphZone ใช้** ตรงๆ แทนการให้ subclass ประกาศซ้ำ
    ///
    /// จุดอ่อน: เปลี่ยนชื่อฟิลด์แล้วพรีวิวจะอ่านไม่เจอ · จึงขึ้นป้าย "ไม่รู้ขนาด" บนจอ
    /// แทนที่จะวาดวงขนาด 0 เงียบๆ — ถ้าเห็นป้ายนี้บ่อย แปลว่าถึงเวลายก AoEShape
    /// ขึ้นเป็น struct ที่ทั้งสองทางใช้ร่วมกัน
    /// </summary>
    public class ArenaPreview : VisualElement
    {
        const float Margin = 14f;
        const float DefaultRadius = 20f;

        BossEncounterConfig _config;
        BossTimelineAction  _timeline;
        float               _playhead;
        object              _selected;     // TimelineClip ที่เลือกอยู่ (ไฮไลต์)
        int                 _rollSeed = 1;

        readonly Label _header = new Label();
        readonly Label _footer = new Label();
        readonly VisualElement _canvas = new VisualElement();

        /// <summary>ทรงหนึ่งใบที่จะวาด — แปลงเป็นพิกัดจอแล้ว</summary>
        struct Shape
        {
            public AoEType type;
            public Vector3 world;
            public float   yawDeg;
            public float   radius, innerRadius, lineLength, lineWidth, coneAngle;
            public Color   color;
            public bool    selected;
            public bool    unknownSize;
        }

        float _lastWidth = -1f;

        readonly List<Shape>  _shapes  = new();
        readonly List<Vector3> _players = new();
        Vector3 _arenaCenter;
        float   _arenaRadius = DefaultRadius;
        bool    _arenaIsSquare;
        bool    _arenaMissing;

        public ArenaPreview()
        {
            style.flexShrink = 0;
            style.marginBottom = 6;

            _header.style.fontSize = 10;
            _header.style.opacity = 0.75f;
            _header.style.whiteSpace = WhiteSpace.Normal;
            Add(_header);

            _canvas.style.flexGrow = 0;
            _canvas.style.flexShrink = 0;
            _canvas.generateVisualContent += OnPaint;
            Add(_canvas);

            _footer.style.fontSize = 10;
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
            Rebuild();
        }

        // ══════════════════════════════════════════════════════════════════
        // ประกอบข้อมูล
        // ══════════════════════════════════════════════════════════════════
        void Rebuild()
        {
            _shapes.Clear();
            _players.Clear();

            var arena = _config != null ? _config.arena : null;
            _arenaMissing  = arena == null;
            _arenaCenter   = arena != null ? arena.center : Vector3.zero;
            _arenaRadius   = arena != null && arena.radius > 0.01f ? arena.radius : DefaultRadius;
            _arenaIsSquare = arena != null && arena.shape == ArenaShape.Square;

            // ผู้เล่นสมมติ 4 คนที่มุมทแยง — พอให้โหมด AllPlayers / Nearest / RandomPlayer
            // วาดออกมาอ่านได้ว่า "ลงที่ตัวผู้เล่น" ไม่ใช่ตัวเลขจริงของรอบไหน
            float pr = _arenaRadius * 0.55f;
            foreach (var d in new[] { new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1), new Vector2(-1, 1) })
            {
                var n = d.normalized * pr;
                _players.Add(_arenaCenter + new Vector3(n.x, 0f, n.y));
            }

            var world = new AoEWorld
            {
                bossPos      = _arenaCenter,
                alivePlayers = _players,
                arena        = arena,
                rolls        = BuildRolls(),
            };

            foreach (var (clip, action) in ActiveClips())
            {
                if (action is not SpawnAoEActionBase aoe) continue;

                Color col = BossDesignerWindow.ClipColorOf(action);
                bool sel = _selected != null && ReferenceEquals(clip, _selected);

                foreach (var (pos, rot) in aoe.ResolveWave(world))
                {
                    var s = ReadShape(aoe);
                    s.world    = pos;
                    s.yawDeg   = rot.eulerAngles.y;
                    s.color    = col;
                    s.selected = sel;
                    _shapes.Add(s);
                }
            }

            UpdateLabels();
            _canvas.MarkDirtyRepaint();
        }

        /// <summary>
        /// roll context ปลอมสำหรับพรีวิว — ล็อกด้วย seed ที่กดเปลี่ยนได้
        ///
        /// roll เฉพาะชื่อที่ config นิยามไว้จริง · ชื่อที่พิมพ์ผิดปล่อยให้ Peek ได้ -1
        /// ไม่งั้น RollContext จะ warn ทุกครั้งที่หน้าต่างรีเฟรช ท่วม Console
        /// (คำเตือนเรื่องชื่อผิดเป็นหน้าที่ของ RollIdDrawer ในช่องนั้นอยู่แล้ว)
        /// </summary>
        RollContext BuildRolls()
        {
            if (_config?.rolls == null || _config.rolls.Length == 0) return null;

            var ctx = new RollContext(_rollSeed, _config.rolls);
            foreach (var def in _config.rolls)
                if (def != null && !string.IsNullOrEmpty(def.rollName))
                    ctx.Roll(def.rollName);
            return ctx;
        }

        IEnumerable<(object clip, BossAction action)> ActiveClips()
        {
            if (_timeline?.tracks == null) yield break;

            foreach (var track in _timeline.tracks)
            {
                if (track?.clips == null) continue;
                foreach (var clip in track.clips)
                {
                    if (clip?.action == null || clip.action == _timeline) continue;

                    float len = Mathf.Max(clip.action.GetEditorDuration(), 0.3f);
                    if (_playhead >= clip.startTime && _playhead < clip.startTime + len)
                        yield return (clip, clip.action);
                }
            }
        }

        // ── อ่านขนาดทรงจากฟิลด์ชื่อเดียวกับที่ TelegraphZone ใช้ ──────────
        static readonly Dictionary<System.Type, FieldInfo[]> _fieldCache = new();

        static Shape ReadShape(SpawnAoEActionBase aoe)
        {
            var s = new Shape { type = TypeOf(aoe), coneAngle = 90f };

            var t = aoe.GetType();
            if (!_fieldCache.TryGetValue(t, out var fields))
            {
                fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance)
                          .Where(f => f.FieldType == typeof(float))
                          .ToArray();
                _fieldCache[t] = fields;
            }

            bool sized = false;
            foreach (var f in fields)
            {
                float v = (float)f.GetValue(aoe);
                switch (f.Name)
                {
                    case "radius":      s.radius      = v; sized = true; break;
                    case "innerRadius": s.innerRadius = v; break;
                    case "lineLength":  s.lineLength  = v; sized = true; break;
                    case "lineWidth":   s.lineWidth   = v; break;
                    case "coneAngle":   s.coneAngle   = v; break;
                }
            }

            // ทรงที่อ่านขนาดไม่ได้ต้องเห็นว่าอ่านไม่ได้ ไม่ใช่วาดวงขนาด 0 แล้วเงียบ
            s.unknownSize = !sized;
            if (!sized) s.radius = 1.5f;

            // scaleEnd คือขนาดตอนระเบิด ซึ่งเป็นขนาดที่ใช้ตัดสินว่าใครโดน (ดู ADR-003)
            float scale = Mathf.Max(0.01f, aoe.scaleEnd);
            s.radius *= scale;
            s.innerRadius *= scale;
            s.lineLength *= scale;
            s.lineWidth *= scale;
            return s;
        }

        /// <summary>GetAoEType() เป็น protected — อ่านผ่าน reflection ทางเดียวที่เหลือ</summary>
        static AoEType TypeOf(SpawnAoEActionBase aoe)
        {
            var m = aoe.GetType().GetMethod("GetAoEType",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            return m != null ? (AoEType)m.Invoke(aoe, null) : AoEType.Circle;
        }

        void UpdateLabels()
        {
            string shape = _arenaIsSquare ? "Square" : "Circle";
            _header.text = _arenaMissing
                ? $"⚠ ไม่ได้ผูก arena — วาดด้วยค่าสมมติ {shape} r={_arenaRadius:0.#} · ท่าที่ใช้ ArenaAnchor จะเพี้ยนตอนรันจริง"
                : $"{_config.arena.name} · {shape} r={_arenaRadius:0.#} · t = {_playhead:0.0}s";

            var names = ActiveClips().Select(x => x.action.name).Distinct().ToList();
            int unknown = _shapes.Count(s => s.unknownSize);

            _footer.text = names.Count == 0
                ? "ไม่มีท่าที่ active ที่เวลานี้ — ลากไม้บรรทัดไปที่คลิป"
                : $"active: {string.Join(" · ", names)}"
                  + (unknown > 0 ? $"\n⚠ {unknown} ทรงอ่านขนาดไม่ได้ (วาดเป็นจุด) — ดูหมายเหตุใน ArenaPreview" : "");
        }

        // ══════════════════════════════════════════════════════════════════
        // วาด
        // ══════════════════════════════════════════════════════════════════
        Vector2 ToLocal(Vector3 w, float size)
        {
            float usable = size - Margin * 2f;
            float s = usable / (_arenaRadius * 2f);
            float mid = size * 0.5f;
            // UI Toolkit แกน y ชี้ลง · โลกแกน z ชี้เหนือ — กลับทิศให้เหนืออยู่บน
            return new Vector2(mid + (w.x - _arenaCenter.x) * s,
                               mid - (w.z - _arenaCenter.z) * s);
        }

        float ToPx(float meters, float size) => meters * ((size - Margin * 2f) / (_arenaRadius * 2f));

        void OnPaint(MeshGenerationContext ctx)
        {
            float size = _canvas.resolvedStyle.width;
            if (size <= 1f || float.IsNaN(size)) return;

            var p = ctx.painter2D;
            var mid = new Vector2(size * 0.5f, size * 0.5f);
            float r = ToPx(_arenaRadius, size);

            // ── พื้นสนาม ───────────────────────────────────────────────────
            p.fillColor = new Color(1f, 1f, 1f, 0.04f);
            p.strokeColor = _arenaMissing ? new Color(1f, 0.7f, 0.3f, 0.5f) : new Color(1f, 1f, 1f, 0.3f);
            p.lineWidth = 1.5f;
            p.BeginPath();
            if (_arenaIsSquare) Rect(p, mid, r * 2f, r * 2f, 0f);
            else                p.Arc(mid, r, Angle.Degrees(0), Angle.Degrees(360));
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
            if (_arenaIsSquare) Rect(p, mid, r, r, 0f);
            else                p.Arc(mid, r * 0.5f, Angle.Degrees(0), Angle.Degrees(360));
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

            // ── ทรงของท่า ──────────────────────────────────────────────────
            foreach (var s in _shapes) DrawShape(p, s, size);

            // ── บอสอยู่กลางสนาม ───────────────────────────────────────────
            p.fillColor = new Color(1f, 1f, 1f, 0.9f);
            p.BeginPath();
            p.Arc(ToLocal(_arenaCenter, size), 5f, Angle.Degrees(0), Angle.Degrees(360));
            p.ClosePath();
            p.Fill();
        }

        void DrawShape(Painter2D p, Shape s, float size)
        {
            Vector2 c = ToLocal(s.world, size);
            Color fill = s.color; fill.a = s.selected ? 0.55f : 0.3f;
            Color line = s.color; line.a = 0.95f;

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
