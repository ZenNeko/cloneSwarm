using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// เส้นเวลาของรันต่อระดับความยาก — วาดแบบ IMGUI ตัวเดียว ใช้ทั้ง MapData Inspector และ Balance Tool
    ///
    /// ═══ ทำไม IMGUI ═══
    /// Balance Tool เป็น Odin (IMGUI ล้วน) ไม่ได้เรียก custom editor ของ Unity · รอบแรกเขียนเป็น UI Toolkit
    /// ใน MapDataEditor แล้วมันไม่โผล่ใน Balance Tool · IMGUI ใส่ได้ทั้งสองที่ (Inspector ห่อด้วย IMGUIContainer)
    /// จึงมีโค้ดวาดชุดเดียว ไม่ต้องดูแลสองชุดที่หน้าตาค่อยๆ เพี้ยนจากกัน
    ///
    /// ตอบคำถามเดียว: "ระดับนี้ นาทีไหน ใครออก"
    ///   แถว โซน / มินิบอส / wave / บอสใหญ่ บนแกน 0 → นาทีบอสใหญ่
    ///   ลากหมุด = ย้ายนาที (snap 0.5 · Shift = 0.1) · คลิกขวาหมุด = ลบ / เปลี่ยนตัว · คลิกขวาที่ว่าง = เพิ่ม
    ///   แก้ลิสต์เดียวกับ schedule.cues[].atMinutes — แก้ทางไหนอีกทางตาม
    /// </summary>
    public static class MapTimelineGUI
    {
        const float RowH   = 24f;
        const float LabelW = 72f;
        const string TierKey = "CloneSwarm.MapTimeline.Tier";

        static readonly Color LaneBg    = new Color(0f, 0f, 0f, 0.18f);
        static readonly Color GridCol   = new Color(1f, 1f, 1f, 0.06f);
        static readonly Color RandomCol = new Color(0.45f, 0.45f, 0.5f);
        static readonly Color BadCol    = new Color(1f, 0.3f, 0.2f);
        static readonly Color[] Palette =
        {
            new Color(0.62f, 0.36f, 0.85f), new Color(0.85f, 0.3f, 0.3f), new Color(0.25f, 0.65f, 0.85f),
            new Color(0.9f, 0.6f, 0.2f),    new Color(0.35f, 0.75f, 0.4f), new Color(0.85f, 0.45f, 0.65f),
        };

        static GUIStyle s_marker, s_small, s_title, s_wrap;
        static float s_dragStart, s_accum;

        static void Styles()
        {
            if (s_marker != null) return;
            s_marker = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 10, clipping = TextClipping.Clip,
                padding = new RectOffset(3, 3, 0, 0),
            };
            s_marker.normal.textColor = Color.white;
            s_small = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };
            s_title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            s_wrap  = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
        }

        // ══════════════════════════════════════════════════════════════════
        public static void Draw(MapData map)
        {
            if (map == null) return;
            Styles();
            GUILayout.Label("เส้นเวลาของรัน", s_title);

            var tiers = map.tiers;
            if (tiers == null || tiers.Length == 0)
            {
                EditorGUILayout.HelpBox("ยังไม่มีระดับ — เพิ่มใน tiers", MessageType.None);
                return;
            }

            int idx = Mathf.Clamp(SessionState.GetInt(TierKey, 0), 0, tiers.Length - 1);
            var names = tiers.Select(t => t != null ? t.tier.ToString() : "(ว่าง)").ToArray();
            int picked = GUILayout.Toolbar(idx, names, GUILayout.MaxWidth(80f * names.Length));
            if (picked != idx) { idx = picked; SessionState.SetInt(TierKey, idx); }

            var tc = tiers[idx];
            if (tc == null) return;
            var sch = tc.schedule ?? new TimelineSchedule();

            ProfileLine(map, tc);

            float len = sch.mainBossMinutes > 0f ? sch.mainBossMinutes : SceneBossMinutes();
            if (!sch.HasCues)
                EditorGUILayout.HelpBox("ระดับนี้ยังไม่มีนัดหมาย — ตอนรันใช้ตารางในซีน · คลิกขวาในแถวเพื่อเริ่มตารางของแมพ", MessageType.None);

            AxisRow(len);
            CueRow(map, tc, TimelineCueKind.ZoneObjective, "โซน", len);
            CueRow(map, tc, TimelineCueKind.MiniBoss, "มินิบอส", len);
            WaveRow(sch, len);
            BossRow(len, sch.mainBossMinutes > 0f);
            Legend(map);
            foreach (var w in Warnings(map, tc, len))
                EditorGUILayout.HelpBox(w, MessageType.Warning);
        }

        // ── แถว ───────────────────────────────────────────────────────────
        static Rect Row(string title, float height = RowH)
        {
            Rect r = GUILayoutUtility.GetRect(10f, height, GUILayout.ExpandWidth(true));
            GUI.Label(new Rect(r.x, r.y, LabelW, r.height), title, s_small);
            return new Rect(r.x + LabelW, r.y + 1, Mathf.Max(10f, r.width - LabelW), r.height - 2);
        }

        static float X(Rect lane, float minute, float len)
            => lane.x + Mathf.Clamp01(minute / Mathf.Max(0.01f, len)) * lane.width;

        static void Grid(Rect lane, float len)
        {
            int step = len <= 20f ? 1 : 5;
            for (int m = step; m < len; m += step)
                EditorGUI.DrawRect(new Rect(X(lane, m, len), lane.y, 1, lane.height), GridCol);
        }

        static void AxisRow(float len)
        {
            var lane = Row("นาที", 16f);
            int step = len <= 20f ? 1 : 5;
            var st = new GUIStyle(s_small) { alignment = TextAnchor.MiddleCenter };
            for (int m = 0; m <= len + 0.01f; m += step)
                GUI.Label(new Rect(X(lane, m, len) - 12, lane.y, 24, lane.height), m.ToString(), st);
        }

        /// <summary>หมุดหนึ่งอัน · วางกึ่งกลางที่นาที แต่ไม่ล้นขอบเลน</summary>
        static Rect MarkerRect(Rect lane, float minute, float len, GUIContent content)
        {
            float w = Mathf.Clamp(s_marker.CalcSize(content).x + 4, 22f, 90f);
            float x = Mathf.Clamp(X(lane, minute, len) - w * 0.5f, lane.x, lane.xMax - w);
            return new Rect(x, lane.y + 2, w, lane.height - 4);
        }

        static void Box(Rect r, Color c, bool bad)
        {
            EditorGUI.DrawRect(r, c);
            if (!bad) return;
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 2), BadCol);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2, r.width, 2), BadCol);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 2, r.height), BadCol);
            EditorGUI.DrawRect(new Rect(r.xMax - 2, r.y, 2, r.height), BadCol);
        }

        static void CueRow(MapData map, MapData.TierContent tc, TimelineCueKind kind, string title, float len)
        {
            var lane = Row(title);
            EditorGUI.DrawRect(lane, LaneBg);
            Grid(lane, len);

            var ids = RosterIds(map);
            var cues = tc.schedule?.cues;
            var e = Event.current;

            // หมุดที่ชนกันแตกเป็นสองชั้น (บน/ล่าง) — เดิมทับกันจนอ่านชื่อไม่ออก
            // เรียงตามนาที · ลำดับต้องเหมือนกันทุก event (control id ผูกกับลำดับการเรียก)
            var items = new List<(TimelineCue cue, int k, float minute)>();
            if (cues != null)
                foreach (var cue in cues)
                    if (cue != null && cue.kind == kind && cue.atMinutes != null)
                        for (int k = 0; k < cue.atMinutes.Length; k++) items.Add((cue, k, cue.atMinutes[k]));
            items.Sort((a, b) => a.minute.CompareTo(b.minute));

            var rects = new Rect[items.Count];
            bool overlap = false;
            for (int i = 0; i < items.Count; i++)
            {
                rects[i] = MarkerRect(lane, items[i].minute, len, MarkerContent(items[i].cue, items[i].minute, len, ids));
                if (i > 0 && rects[i].x < rects[i - 1].xMax) overlap = true;
            }
            if (overlap)
            {
                float half = (lane.height - 4) * 0.5f;
                var lastRight = new[] { float.MinValue, float.MinValue };
                for (int i = 0; i < rects.Length; i++)
                {
                    int level = rects[i].x >= lastRight[0] ? 0
                              : rects[i].x >= lastRight[1] ? 1
                              : lastRight[0] <= lastRight[1] ? 0 : 1;
                    rects[i].y = lane.y + 2 + level * half;
                    rects[i].height = half - 1;
                    lastRight[level] = rects[i].xMax;
                }
            }
            for (int i = 0; i < items.Count; i++)
                Marker(map, tc, items[i].cue, items[i].k, lane, len, ids, e, rects[i]);

            // คลิกขวาที่ว่างในแถว = เพิ่มตรงนั้น (หมุดใช้ event ไปแล้วถ้าโดนหมุด)
            if (e.type == EventType.ContextClick && lane.Contains(e.mousePosition))
            {
                float minute = Snap((e.mousePosition.x - lane.x) / lane.width * len, false);
                var menu = new GenericMenu();
                foreach (var v in VariantChoices(map, kind))
                {
                    string variant = v;
                    menu.AddItem(new GUIContent($"เพิ่ม{title}นาที {minute:0.#}/{(variant == "" ? "สุ่ม" : variant)}"), false,
                                 () => Edit(map, "Add Cue Time", () => AddTime(tc, kind, variant, minute)));
                }
                menu.ShowAsContext();
                e.Use();
            }
        }

        static bool IsUnknown(TimelineCue cue, string[] ids)
            => cue.kind == TimelineCueKind.MiniBoss && !string.IsNullOrEmpty(cue.variant) &&
               ids.Length > 0 && !ids.Contains(cue.variant);

        static GUIContent MarkerContent(TimelineCue cue, float minute, float len, string[] ids)
        {
            bool random = string.IsNullOrEmpty(cue.variant);
            return new GUIContent(random ? "สุ่ม" : Short(cue.variant),
                $"{cue.DisplayName}\nนาที {minute:0.#} · {(random ? "สุ่ม" : cue.variant)}" +
                (minute > len ? "\n⚠ หลังบอสใหญ่ — ไม่มีวันเกิด" : "") +
                (IsUnknown(cue, ids) ? "\n⚠ ไม่มีในรายชื่อมินิบอสของแมพ — จะสุ่มแทน" : "") +
                "\nลาก = ย้ายนาที (Shift = ละเอียด) · คลิกขวา = ลบ / เปลี่ยน");
        }

        static void Marker(MapData map, MapData.TierContent tc, TimelineCue cue, int k, Rect lane, float len,
                           string[] ids, Event e, Rect r)
        {
            float minute = cue.atMinutes[k];
            bool random  = string.IsNullOrEmpty(cue.variant);
            bool late    = minute > len;
            bool unknown = IsUnknown(cue, ids);
            var content  = MarkerContent(cue, minute, len, ids);
            int id = GUIUtility.GetControlID(FocusType.Passive, r);

            switch (e.GetTypeForControl(id))
            {
                case EventType.Repaint:
                    Box(r, random ? RandomCol : ColorFor(map, cue.kind, cue.variant), late || unknown);
                    s_marker.Draw(r, content, false, false, false, false);
                    break;

                case EventType.MouseDown:
                    if (e.button == 0 && r.Contains(e.mousePosition))
                    {
                        Undo.RecordObject(map, "Move Cue Time");
                        GUIUtility.hotControl = id;
                        s_dragStart = minute;
                        s_accum = 0f;
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        s_accum += e.delta.x;
                        cue.atMinutes[k] = Mathf.Max(0f, Snap(s_dragStart + s_accum / lane.width * len, e.shift));
                        e.Use();
                        HandleUtility.Repaint();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        if (!Mathf.Approximately(cue.atMinutes[k], s_dragStart))
                        {
                            System.Array.Sort(cue.atMinutes);
                            EditorUtility.SetDirty(map);
                        }
                        e.Use();
                    }
                    break;

                case EventType.ContextClick:
                    if (r.Contains(e.mousePosition))
                    {
                        var menu = new GenericMenu();
                        menu.AddItem(new GUIContent($"ลบนาที {minute:0.#}"), false,
                                     () => Edit(map, "Remove Cue Time", () => RemoveTime(tc, cue, k)));
                        foreach (var v in VariantChoices(map, cue.kind))
                        {
                            string variant = v;
                            if (variant == (cue.variant ?? "")) continue;
                            menu.AddItem(new GUIContent($"เปลี่ยนเป็น/{(variant == "" ? "สุ่ม" : variant)}"), false,
                                         () => Edit(map, "Change Cue Variant", () =>
                                         {
                                             RemoveTime(tc, cue, k);
                                             AddTime(tc, cue.kind, variant, minute);
                                         }));
                        }
                        menu.ShowAsContext();
                        e.Use();
                    }
                    break;
            }
        }

        static void WaveRow(TimelineSchedule sch, float len)
        {
            var lane = Row("wave");
            EditorGUI.DrawRect(lane, LaneBg);
            var phases = (sch.wavePhases ?? new WavePhase[0]).Where(p => p.config != null)
                                                               .OrderBy(p => p.atMinutes).ToList();
            if (phases.Count == 0)
            {
                GUI.Label(new Rect(lane.x + 4, lane.y, lane.width, lane.height), "ใช้ WaveConfig ของซีน", s_small);
                return;
            }
            for (int i = 0; i < phases.Count; i++)
            {
                float from = i == 0 ? 0f : phases[i].atMinutes;   // ช่วงแรกครอบตั้งแต่เริ่มเกมเสมอ
                float to = i + 1 < phases.Count ? phases[i + 1].atMinutes : len;
                var r = new Rect(X(lane, from, len), lane.y + 2, Mathf.Max(2f, X(lane, to, len) - X(lane, from, len)) - 1, lane.height - 4);
                var col = Color.Lerp(new Color(0.2f, 0.45f, 0.35f), new Color(0.55f, 0.35f, 0.2f),
                                     (float)i / Mathf.Max(1, phases.Count - 1));
                EditorGUI.DrawRect(r, col);
                GUI.Label(r, new GUIContent(phases[i].config.name, $"{phases[i].config.name}\nนาที {from:0.#} – {to:0.#}"), s_marker);
            }
        }

        static void BossRow(float len, bool fromMap)
        {
            var lane = Row("บอสใหญ่");
            EditorGUI.DrawRect(lane, LaneBg);
            Grid(lane, len);
            var content = new GUIContent($"บอส · {len:0.#}",
                fromMap ? "นาทีบอสใหญ่ของระดับนี้ (schedule.mainBossMinutes)"
                        : "ระดับนี้ไม่ได้ตั้ง mainBossMinutes — ใช้ค่าในซีน (GameTimeline.mainBossTimeMin)");
            var r = MarkerRect(lane, len, len, content);
            EditorGUI.DrawRect(r, new Color(0.75f, 0.2f, 0.2f));
            GUI.Label(r, content, s_marker);
        }

        static void Legend(MapData map)
        {
            var ids = RosterIds(map);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(LabelW);
            if (ids.Length == 0)
                GUILayout.Label("แมพนี้ยังไม่มีรายชื่อมินิบอส (miniBosses) — ใช้ลิสต์ในซีน", s_small);
            foreach (var id in ids)
            {
                var content = new GUIContent(id);
                var r = GUILayoutUtility.GetRect(content, s_marker, GUILayout.ExpandWidth(false), GUILayout.Height(16));
                r.width += 6;
                EditorGUI.DrawRect(r, ColorFor(map, TimelineCueKind.MiniBoss, id));
                GUI.Label(r, content, s_marker);
                GUILayout.Space(10);
            }
            GUILayout.Label("ลากหมุด = ย้าย · คลิกขวา = เพิ่ม/ลบ/เปลี่ยนตัว", s_small);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        static void ProfileLine(MapData map, MapData.TierContent tc)
        {
            var tier = tc.tier;
            var p = DifficultyProfile.Resolve(map, tier);
            bool real = AssetDatabase.Contains(p);
            bool own  = tc.difficultyOverride != null;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(real
                ? $"{tier} {(own ? $"(เฉพาะแมพนี้: {p.name})" : "(ค่ากลาง)")}: HP×{p.enemyHpMult:0.##} · " +
                  $"บอส ดาเมจ×{p.bossDamageMult:0.##} เตือน×{p.bossWarningMult:0.##} ห่าง×{p.bossIntervalMult:0.##} " +
                  $"enrage {(p.enrageEnabled ? $"×{p.enrageTimeMult:0.##}" : "ปิด")} · exp×{p.expMult:0.##}"
                : $"{tier}: ไม่มี DifficultyProfile — ใช้ค่ากลาง ×1 (Tools > Clone Swarm > Difficulty > Create Profiles)", s_wrap);
            if (real && GUILayout.Button("แก้ตัวคูณ", GUILayout.Width(70)))
            {
                EditorGUIUtility.PingObject(p);
                Selection.activeObject = p;
            }
            if (!own && GUILayout.Button(new GUIContent("ทำเฉพาะแมพนี้",
                    "ก๊อปตัวคูณกลางของระดับนี้เป็นไฟล์ของแมพนี้ แล้วผูกให้ — แก้แล้วไม่กระทบแมพอื่น"), GUILayout.Width(90)))
                EditorApplication.delayCall += () => MakeMapOverride(map, tc, p);   // สร้าง asset กลาง OnGUI = layout รอบนี้พัง
            if (own && GUILayout.Button(new GUIContent("กลับไปค่ากลาง", "เลิกผูกไฟล์เฉพาะแมพ (ไฟล์ไม่ถูกลบ)"), GUILayout.Width(90)))
                EditorApplication.delayCall += () => Edit(map, "Clear Difficulty Override", () => tc.difficultyOverride = null);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>ก๊อปค่ากลางเป็นไฟล์ของแมพ — วางไว้ข้าง MapData (นอก Resources/Difficulty)</summary>
        static void MakeMapOverride(MapData map, MapData.TierContent tc, DifficultyProfile source)
        {
            string dir = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(map)).Replace('\\', '/');
            string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/DifficultyProfile_{map.mapId}_{tc.tier}.asset");
            var copy = ScriptableObject.CreateInstance<DifficultyProfile>();
            if (AssetDatabase.Contains(source)) EditorUtility.CopySerialized(source, copy);
            copy.tier = tc.tier;
            AssetDatabase.CreateAsset(copy, path);
            Edit(map, "Make Difficulty Override", () => tc.difficultyOverride = copy);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(copy);
        }

        // ── ข้อมูล ────────────────────────────────────────────────────────
        static IEnumerable<string> Warnings(MapData map, MapData.TierContent tc, float len)
        {
            var cues = tc.schedule?.cues ?? new TimelineCue[0];
            int late = cues.Where(c => c?.atMinutes != null).Sum(c => c.atMinutes.Count(m => m > len));
            if (late > 0) yield return $"{late} นัดอยู่หลังบอสใหญ่ ({len:0.#} นาที) — ไม่มีวันเกิด";

            var miniTimes = cues.Where(c => c != null && c.kind == TimelineCueKind.MiniBoss && c.atMinutes != null)
                                .SelectMany(c => c.atMinutes).OrderBy(m => m).ToList();
            for (int i = 1; i < miniTimes.Count; i++)
                if (miniTimes[i] - miniTimes[i - 1] < 0.5f)
                {
                    yield return $"มินิบอสสองนัดที่นาที {miniTimes[i]:0.#} ห่างกันไม่ถึงครึ่งนาที — ออกพร้อมกัน (ถ้าตั้งใจก็ได้)";
                    break;
                }
            if (tc.schedule != null && tc.schedule.HasCues && miniTimes.Count == 0) yield return "ระดับนี้ไม่มีมินิบอสเลย";

            var ids = RosterIds(map);
            if (ids.Length > 0)
                foreach (var c in cues.Where(c => c != null && c.kind == TimelineCueKind.MiniBoss &&
                                                  !string.IsNullOrEmpty(c.variant) && !ids.Contains(c.variant)))
                    yield return $"'{c.variant}' ไม่มีในรายชื่อมินิบอส — นัดนี้จะสุ่มแทน";
        }

        static string[] RosterIds(MapData map) => (map.miniBosses ?? new MapData.MiniBossEntry[0])
            .Where(m => m != null && m.prefab != null && !string.IsNullOrEmpty(m.id))
            .Select(m => m.id).Distinct().ToArray();

        /// <summary>ตัวเลือกเพิ่ม/เปลี่ยน: มินิบอส = รายชื่อแมพ · โซน = แบบที่ตารางใช้อยู่ · "" = สุ่ม</summary>
        static List<string> VariantChoices(MapData map, TimelineCueKind kind)
        {
            var list = new List<string>();
            if (kind == TimelineCueKind.MiniBoss) list.AddRange(RosterIds(map));
            foreach (var t in map.tiers ?? new MapData.TierContent[0])
                foreach (var c in t?.schedule?.cues ?? new TimelineCue[0])
                    if (c != null && c.kind == kind && !string.IsNullOrEmpty(c.variant) && !list.Contains(c.variant))
                        list.Add(c.variant);
            list.Add("");
            return list;
        }

        static Color ColorFor(MapData map, TimelineCueKind kind, string variant)
        {
            if (string.IsNullOrEmpty(variant)) return RandomCol;
            if (kind == TimelineCueKind.MiniBoss)
            {
                int i = System.Array.IndexOf(RosterIds(map), variant);
                if (i >= 0) return Palette[i % Palette.Length];
            }
            int h = 0;
            foreach (char ch in variant) h = h * 31 + ch;
            var c = Palette[Mathf.Abs(h) % Palette.Length];
            return kind == TimelineCueKind.ZoneObjective ? Color.Lerp(c, new Color(0.2f, 0.6f, 0.55f), 0.5f) : c;
        }

        static void AddTime(MapData.TierContent tc, TimelineCueKind kind, string variant, float minute)
        {
            tc.schedule ??= new TimelineSchedule();
            var cues = (tc.schedule.cues ?? new TimelineCue[0]).ToList();
            var cue = cues.FirstOrDefault(c => c != null && c.kind == kind && (c.variant ?? "") == variant);
            if (cue == null)
            {
                cue = new TimelineCue
                {
                    kind = kind,
                    variant = variant,
                    label = (kind == TimelineCueKind.MiniBoss ? "มินิบอส " : "โซน ") + (variant == "" ? "สุ่ม" : variant),
                    atMinutes = new float[0],
                };
                cues.Add(cue);
            }
            cue.atMinutes = (cue.atMinutes ?? new float[0]).Append(minute).OrderBy(m => m).ToArray();
            tc.schedule.cues = cues.ToArray();
        }

        /// <summary>ลบนาทีที่ k · นัดที่ไม่เหลือนาทีถูกลบทั้งนัด (ไม่ทิ้งแถวว่างไว้ในลิสต์)</summary>
        static void RemoveTime(MapData.TierContent tc, TimelineCue cue, int k)
        {
            var times = cue.atMinutes.ToList();
            if (k >= 0 && k < times.Count) times.RemoveAt(k);
            cue.atMinutes = times.ToArray();
            if (cue.atMinutes.Length == 0)
                tc.schedule.cues = tc.schedule.cues.Where(c => c != cue).ToArray();
        }

        /// <summary>แก้จากเมนู — callback วิ่งนอก OnGUI จึงต้องสั่งวาดใหม่เอง (ทั้ง Inspector และ Balance Tool)</summary>
        static void Edit(MapData map, string undo, System.Action change)
        {
            Undo.RecordObject(map, undo);
            change();
            EditorUtility.SetDirty(map);
            InternalEditorUtility.RepaintAllViews();
        }

        static float Snap(float minute, bool fine) => Mathf.Round(minute / (fine ? 0.1f : 0.5f)) * (fine ? 0.1f : 0.5f);

        static string Short(string s) => s.Length <= 9 ? s : s.Substring(0, 8) + "…";

        // นาทีบอสใหญ่ของซีนเกม (ถ้าเปิดอยู่) · ไม่งั้น 15 — ถามวินาทีละครั้ง (OnGUI วิ่งหลายรอบต่อเฟรม)
        static float s_sceneBoss = 15f;
        static double s_nextSceneScan;
        static float SceneBossMinutes()
        {
            if (EditorApplication.timeSinceStartup >= s_nextSceneScan)
            {
                s_nextSceneScan = EditorApplication.timeSinceStartup + 1.0;
                var gt = Object.FindAnyObjectByType<GameTimeline>(FindObjectsInactive.Include);
                s_sceneBoss = gt != null && gt.mainBossTimeMin > 0f ? gt.mainBossTimeMin : 15f;
            }
            return s_sceneBoss;
        }
    }
}
