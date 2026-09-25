using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// ท่าไหนลงตรงไหน ณ เวลา t — ใช้ร่วมกันระหว่างแผนผังสนาม · แถบความปลอดภัย · การลากแก้ในสนาม
    ///
    /// ═══ แยกออกมาจาก ArenaPreview ทำไม ═══
    ///
    /// แถบความปลอดภัยต้องถามคำถามเดียวกับภาพ ("ณ เวลานี้มีวงอะไรอยู่ตรงไหน") ที่เวลาหลายสิบจุด
    /// ถ้าเขียนสูตรเก็บทรงแยกอีกชุด ตัวเลข % ปลอดภัยจะไม่ตรงกับภาพที่เห็น — และตัวเลขที่ไม่ตรง
    /// กับภาพพาไปจูนผิดที่ · ตำแหน่งยังมาจาก <see cref="SpawnAoEActionBase.ResolveWave"/> ตัวเดียวกับ server
    /// </summary>
    public static class BossPatternGeometry
    {
        public const float DefaultArenaRadius = 20f;

        public struct Shape
        {
            public AoEType type;
            public Vector3 world;
            public float   yawDeg;
            public float   radius, innerRadius, lineLength, lineWidth, coneAngle;
            public Color   color;
            public bool    selected;
            public bool    unknownSize;
            public object  clip;                 // TimelineClip ที่ทรงนี้มาจาก
            public SpawnAoEActionBase action;
        }

        public struct Arena
        {
            public Vector3 center;
            public float   radius;
            public bool    square;
            public bool    missing;
            public ArenaDefinition def;
        }

        public static Arena ArenaOf(BossEncounterConfig config)
        {
            var a = config != null ? config.arena : null;
            return new Arena
            {
                def     = a,
                missing = a == null,
                center  = a != null ? a.center : Vector3.zero,
                radius  = a != null && a.radius > 0.01f ? a.radius : DefaultArenaRadius,
                square  = a != null && a.shape == ArenaShape.Square,
            };
        }

        /// <summary>
        /// ผู้เล่นสมมติ 4 คนที่มุมทแยง — พอให้โหมด AllPlayers / Nearest / RandomPlayer
        /// อ่านออกว่า "ลงที่ตัวผู้เล่น" ไม่ใช่ตัวเลขจริงของรอบไหน
        /// </summary>
        public static void FakePlayers(Arena arena, List<Vector3> into)
        {
            into.Clear();
            float pr = arena.radius * 0.55f;
            foreach (var d in new[] { new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1), new Vector2(-1, 1) })
            {
                var n = d.normalized * pr;
                into.Add(arena.center + new Vector3(n.x, 0f, n.y));
            }
        }

        /// <summary>
        /// roll context ปลอมสำหรับพรีวิว — ล็อกด้วย seed ที่กดเปลี่ยนได้ · roll เฉพาะชื่อที่ config นิยามจริง
        /// (ชื่อผิดปล่อยให้ Peek ได้ -1 ไม่งั้น RollContext warn ทุกครั้งที่หน้าต่างรีเฟรช)
        /// </summary>
        public static RollContext BuildRolls(BossEncounterConfig config, int seed)
        {
            if (config?.rolls == null || config.rolls.Length == 0) return null;
            var ctx = new RollContext(seed, config.rolls);
            foreach (var def in config.rolls)
                if (def != null && !string.IsNullOrEmpty(def.rollName)) ctx.Roll(def.rollName);
            return ctx;
        }

        /// <summary>คลิปที่ active ณ เวลา t — ตั้งแต่เริ่มเตือนจนจบท่า (ความยาวจาก GetEditorDuration)</summary>
        public static IEnumerable<BossTimelineAction.TimelineClip> ActiveClips(BossTimelineAction timeline, float t)
        {
            if (timeline?.tracks == null) yield break;
            foreach (var track in timeline.tracks)
            {
                if (track?.clips == null) continue;
                foreach (var clip in track.clips)
                {
                    if (clip?.action == null || clip.action == timeline) continue;
                    float len = Mathf.Max(clip.action.GetEditorDuration(), 0.3f);
                    if (t >= clip.startTime && t < clip.startTime + len) yield return clip;
                }
            }
        }

        /// <summary>เก็บทรงทุกอันที่ active ณ เวลา t ลง into (ล้างก่อน)</summary>
        public static void Collect(BossEncounterConfig config, BossTimelineAction timeline, float t,
                                   int rollSeed, object selectedClip, List<Shape> into,
                                   List<Vector3> players, System.Func<BossAction, Color> colorOf = null)
        {
            into.Clear();
            var arena = ArenaOf(config);
            FakePlayers(arena, players);

            var world = new AoEWorld
            {
                bossPos      = arena.center,
                alivePlayers = players,
                arena        = arena.def,
                rolls        = BuildRolls(config, rollSeed),
            };

            foreach (var clip in ActiveClips(timeline, t))
            {
                // ท่าสุ่มผูก roll — วาดท่าที่ roll เลือก (กด 🎲 roll ใหม่ แล้วสลับให้เห็น)
                BossAction act = clip.action;
                float into_ = t - clip.startTime;
                for (int depth = 0; act is RandomAttackAction ra && depth < 4; depth++)
                {
                    // นัดที่กำลังเล่น ณ playhead — โหมดสลับ/หยิบใหม่ จะเห็นท่าเปลี่ยนไปตามนัด
                    int round = ra.RoundAt(into_);
                    into_ -= ra.actionDelay + round * ra.repeatInterval;
                    act = ra.PickForPreview(world.rolls?.Peek(ra.rollName) ?? -1, round);
                }
                if (act is not SpawnAoEActionBase aoe) continue;
                Color col = colorOf != null ? colorOf(aoe) : Color.white;
                bool sel = selectedClip != null && ReferenceEquals(clip, selectedClip);

                foreach (var (pos, rot) in aoe.ResolveWave(world))
                {
                    var s = ReadShape(aoe);
                    s.world    = pos;
                    s.yawDeg   = rot.eulerAngles.y;
                    s.color    = col;
                    s.selected = sel;
                    s.clip     = clip;
                    s.action   = aoe;
                    into.Add(s);
                }
            }
        }

        // ── อ่านขนาดทรงจากฟิลด์ชื่อเดียวกับที่ TelegraphZone ใช้ ──────────
        // subclass ส่งขนาดให้ TelegraphZone ผ่าน ConfigureTelegraphZone ซึ่งเรียกจาก editor ไม่ได้
        // จึงอ่านฟิลด์ชื่อเดียวกันตรงๆ · เปลี่ยนชื่อฟิลด์แล้วขึ้นป้าย "ไม่รู้ขนาด" แทนการวาดวงขนาด 0 เงียบๆ
        static readonly Dictionary<System.Type, FieldInfo[]> _fieldCache = new();

        public static Shape ReadShape(SpawnAoEActionBase aoe)
        {
            var s = new Shape { type = TypeOf(aoe), coneAngle = 90f };

            var t = aoe.GetType();
            if (!_fieldCache.TryGetValue(t, out var fields))
            {
                fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance)
                          .Where(f => f.FieldType == typeof(float)).ToArray();
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

            s.unknownSize = !sized;
            if (!sized) s.radius = 1.5f;

            // scaleEnd คือขนาดตอนระเบิด ซึ่งเป็นขนาดที่ใช้ตัดสินว่าใครโดน (ADR-003)
            float scale = Mathf.Max(0.01f, aoe.scaleEnd);
            s.radius *= scale; s.innerRadius *= scale; s.lineLength *= scale; s.lineWidth *= scale;
            return s;
        }

        /// <summary>GetAoEType() เป็น protected — อ่านผ่าน reflection ทางเดียวที่เหลือ</summary>
        public static AoEType TypeOf(SpawnAoEActionBase aoe)
        {
            var m = aoe.GetType().GetMethod("GetAoEType",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            return m != null ? (AoEType)m.Invoke(aoe, null) : AoEType.Circle;
        }

        /// <summary>ทิศหน้าในโลก (XZ) ของ yaw — yaw 0 = +Z (เหนือ)</summary>
        public static Vector3 ForwardWorld(float yawDeg)
        {
            float r = yawDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
        }

        /// <summary>จุด p (โลก) อยู่ในพื้นที่อันตรายของทรงนี้ไหม — เรขาคณิตเดียวกับที่วาด</summary>
        public static bool Contains(in Shape s, Vector3 p)
        {
            Vector3 d = p - s.world; d.y = 0f;
            Vector3 fwd  = ForwardWorld(s.yawDeg);
            Vector3 side = new Vector3(fwd.z, 0f, -fwd.x);
            float along = Vector3.Dot(d, fwd), lat = Vector3.Dot(d, side);
            float dist  = d.magnitude;

            switch (s.type)
            {
                case AoEType.Circle: return dist <= s.radius;
                case AoEType.Donut:  return dist <= s.radius && dist >= s.innerRadius;
                case AoEType.Line:   // จุดเกิดคือต้นลำ ลำพาดไปข้างหน้า
                    return along >= 0f && along <= s.lineLength && Mathf.Abs(lat) <= s.lineWidth * 0.5f;
                case AoEType.Cross:
                    return (Mathf.Abs(along) <= s.lineLength * 0.5f && Mathf.Abs(lat) <= s.lineWidth * 0.5f)
                        || (Mathf.Abs(lat) <= s.lineLength * 0.5f && Mathf.Abs(along) <= s.lineWidth * 0.5f);
                case AoEType.Cone:
                    if (dist > s.radius) return false;
                    if (dist < 0.001f) return true;
                    return Vector3.Angle(fwd, d) <= s.coneAngle * 0.5f;
                default: return dist <= s.radius;
            }
        }

        /// <summary>
        /// สัดส่วนพื้นที่สนามที่ไม่มีทรงไหนทับ (0..1) — ตาราง grid×grid ภายในขอบสนาม
        ///
        /// นับ "ทั้งสนาม" ไม่ใช่ "ที่วิ่งไปถึงทันในเวลาเตือน" (ตัดสินแล้ว 2026-09-24 — ง่ายและไม่ต้องรู้
        /// ความเร็วผู้เล่น) · ตัวเลขจึงมองโลกในแง่ดีกว่าจริง: 10% ที่อยู่อีกฝั่งสนามอาจวิ่งไปไม่ทัน
        /// </summary>
        public static float SafeFraction(Arena arena, List<Shape> shapes, int grid = 28)
        {
            if (shapes.Count == 0) return 1f;
            int inside = 0, safe = 0;
            float step = arena.radius * 2f / grid;
            for (int ix = 0; ix < grid; ix++)
            for (int iz = 0; iz < grid; iz++)
            {
                float x = -arena.radius + (ix + 0.5f) * step;
                float z = -arena.radius + (iz + 0.5f) * step;
                if (!arena.square && x * x + z * z > arena.radius * arena.radius) continue;
                inside++;
                var p = arena.center + new Vector3(x, 0f, z);
                bool hit = false;
                for (int i = 0; i < shapes.Count && !hit; i++) hit = Contains(shapes[i], p);
                if (!hit) safe++;
            }
            return inside > 0 ? (float)safe / inside : 1f;
        }
    }
}
