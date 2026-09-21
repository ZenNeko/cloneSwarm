using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// ตรวจ BossEncounterConfig ทุกใบโดยไม่ต้องกด Play
    ///
    /// ═══ ตรวจอะไร ═══
    ///
    /// 1. `rollName` ที่ไม่มีนิยาม — ตอนรันจะถูกมองเป็น Variant 2 ตัวเลือกแบบเงียบๆ
    ///    ท่าที่ควรหมุน/พลิกก็แค่หยุดทำงาน · RollIdDrawer กันได้เฉพาะทางที่ผ่าน Inspector
    /// 2. ท่าที่ต้องใช้สนามแต่ config ไม่ได้ผูก arena — ตอนรันจะถอยไปใช้ตำแหน่งบอส
    ///    วงไปโผล่ผิดที่ทั้งชุด
    /// 3. `ResolveWave` คืนจุดเกิดที่ใช้ได้จริงไหม — สูตรเดียวกับที่ ArenaPreview วาด
    ///    และที่เซิร์ฟเวอร์ใช้ยิงจริง · ถ้าตรงนี้พัง พรีวิวจะวาดของที่ไม่ตรงกับเกม
    /// 4. roll ที่นิยามไว้แต่ไม่มีใครอ้าง — ไม่ใช่ความผิด แต่ต้องเห็น
    ///    (BossConfig_01 นิยามไว้ 5 ตัว ไม่มี action ตัวไหนใช้เลยสักตัว และไม่มีใครรู้)
    ///
    /// ═══ ทำไมไม่รวมใน Smoke Test เฉยๆ ═══
    ///
    /// สโมกเทสต์ต้อง Play + โหลดซีน · ข้อพวกนี้อ่านจาก asset ล้วน รันได้ในวินาทีเดียว
    /// ของที่ตรวจเร็วควรตรวจได้เร็ว ไม่งั้นคนจะไม่รัน
    /// </summary>
    public static class BossConfigAudit
    {
        public class Problem
        {
            public string where;
            public string what;
            public bool   blocking;   // false = หมายเหตุให้เห็น ไม่ใช่ข้อผิด
            public override string ToString() => $"{where} — {what}";
        }

        [MenuItem("Tools/Clone Swarm/Audit Boss Configs")]
        public static void RunFromMenu()
        {
            var problems = Collect(out string summary);
            var blocking = problems.Where(p => p.blocking).ToList();

            var sb = new StringBuilder();
            sb.AppendLine(summary);
            foreach (var p in problems)
                sb.AppendLine($"  {(p.blocking ? "✗" : "·")} {p}");

            if (blocking.Count > 0) Debug.LogError(sb.ToString());
            else                    Debug.Log(sb.ToString());
        }

        /// <summary>เข้าทาง batchmode — คืน exit code 1 ถ้ามีข้อผิดจริง</summary>
        public static void RunBatch()
        {
            var problems = Collect(out string summary);
            var blocking = problems.Where(p => p.blocking).ToList();

            Debug.Log(summary);
            foreach (var p in problems)
                Debug.Log($"  {(p.blocking ? "FAIL" : "note")}  {p}");

            EditorApplication.Exit(blocking.Count > 0 ? 1 : 0);
        }

        public static List<Problem> Collect(out string summary)
        {
            var problems = new List<Problem>();
            int configs = 0, actions = 0, waves = 0, rollsDefined = 0, rollsUsed = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:BossEncounterConfig"))
            {
                var cfg = AssetDatabase.LoadAssetAtPath<BossEncounterConfig>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (cfg == null) continue;
                configs++;

                var defined = new HashSet<string>();
                if (cfg.rolls != null)
                    foreach (var d in cfg.rolls)
                        if (d != null && !string.IsNullOrEmpty(d.rollName)) defined.Add(d.rollName);
                rollsDefined += defined.Count;

                var used = new HashSet<string>();
                var reached = new HashSet<BossAction>();
                foreach (var phase in cfg.phases ?? new List<BossPhase>())
                {
                    if (phase == null) continue;
                    Collect(phase.actions, reached, 0);
                    Collect(phase.enrageActions, reached, 0);
                }

                // โลกสมมติแบบเดียวกับที่ ArenaPreview ใช้ — ผู้เล่น 4 คนรอบกลางสนาม
                var world = MakeWorld(cfg);

                foreach (var a in reached)
                {
                    actions++;

                    if (!string.IsNullOrEmpty(a.rollName))
                    {
                        used.Add(a.rollName);
                        if (!defined.Contains(a.rollName))
                            problems.Add(new Problem
                            {
                                where = $"{cfg.name} · {a.name}",
                                what = $"rollName '{a.rollName}' ไม่มีใน Roll Definitions — " +
                                       "ตอนรันจะเป็น Variant 2 ตัวเลือก และ roll เชิงพื้นที่จะไม่ทำงาน",
                                blocking = true,
                            });
                    }

                    if (a is not SpawnAoEActionBase aoe) continue;

                    bool needsArena = aoe.targetingMode == SpawnAoEActionBase.TargetingMode.ArenaAnchor
                                   || (!string.IsNullOrEmpty(aoe.rollName)
                                       && defined.Contains(aoe.rollName)
                                       && IsSpatial(cfg, aoe.rollName));

                    if (needsArena && cfg.arena == null)
                        problems.Add(new Problem
                        {
                            where = $"{cfg.name} · {a.name}",
                            what = "ใช้ ArenaAnchor หรือ roll เชิงพื้นที่ แต่ config ไม่ได้ผูก arena — " +
                                   "ตอนรันจะถอยไปใช้ตำแหน่งบอส วงไปโผล่ผิดที่ทั้งชุด",
                            blocking = true,
                        });

                    // สูตรเดียวกับที่ ArenaPreview วาดและที่เซิร์ฟเวอร์ยิงจริง
                    var wave = aoe.ResolveWave(world);
                    waves++;

                    if (wave.Count == 0)
                        problems.Add(new Problem
                        {
                            where = $"{cfg.name} · {a.name}",
                            what = "ResolveWave ไม่คืนจุดเกิดเลย — ท่านี้จะไม่เกิดอะไรขึ้น",
                            blocking = true,
                        });

                    foreach (var (pos, _) in wave)
                        if (!IsFinite(pos))
                        {
                            problems.Add(new Problem
                            {
                                where = $"{cfg.name} · {a.name}",
                                what = $"จุดเกิดไม่ใช่ตัวเลขที่ใช้ได้ ({pos})",
                                blocking = true,
                            });
                            break;
                        }
                }

                rollsUsed += used.Count(u => defined.Contains(u));

                var orphanRolls = defined.Where(d => !used.Contains(d)).ToList();
                if (orphanRolls.Count > 0)
                    problems.Add(new Problem
                    {
                        where = cfg.name,
                        what = $"นิยาม roll ไว้ {defined.Count} ตัวแต่ไม่มีท่าไหนอ้าง {orphanRolls.Count} ตัว " +
                               $"({string.Join(", ", orphanRolls)}) — แพตเทิร์นจะออกเหมือนเดิมทุกรอบ",
                        blocking = false,
                    });
            }

            summary = $"[Boss Audit] config {configs} ใบ · action {actions} ตัว · " +
                      $"ResolveWave ผ่าน {waves} ครั้ง · roll นิยาม {rollsDefined} / ใช้จริง {rollsUsed} · " +
                      $"พบปัญหา {problems.Count(p => p.blocking)} ข้อ";
            return problems;
        }

        static bool IsSpatial(BossEncounterConfig cfg, string rollName)
        {
            var def = cfg.rolls?.FirstOrDefault(d => d != null && d.rollName == rollName);
            if (def == null) return false;
            return def.kind == RollKind.Anchor || def.kind == RollKind.SnapAngle
                || def.kind == RollKind.MirrorX || def.kind == RollKind.MirrorZ;
        }

        static bool IsFinite(Vector3 v)
            => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
            && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);

        static AoEWorld MakeWorld(BossEncounterConfig cfg)
        {
            Vector3 center = cfg.arena != null ? cfg.arena.center : Vector3.zero;
            float radius = cfg.arena != null && cfg.arena.radius > 0.01f ? cfg.arena.radius : 20f;

            var players = new List<Vector3>();
            foreach (var d in new[] { new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1), new Vector2(-1, 1) })
            {
                var n = d.normalized * (radius * 0.55f);
                players.Add(center + new Vector3(n.x, 0f, n.y));
            }

            RollContext rolls = null;
            if (cfg.rolls != null && cfg.rolls.Length > 0)
            {
                rolls = new RollContext(1, cfg.rolls);
                foreach (var d in cfg.rolls)
                    if (d != null && !string.IsNullOrEmpty(d.rollName)) rolls.Roll(d.rollName);
            }

            return new AoEWorld
            {
                bossPos = center,
                alivePlayers = players,
                arena = cfg.arena,
                rolls = rolls,
            };
        }

        /// <summary>ไล่ท่าทั้งหมดที่เฟสนี้เข้าถึงได้ รวมที่ซ้อนอยู่ใน timeline / combo / pool</summary>
        static void Collect(List<BossAction> list, HashSet<BossAction> into, int depth)
        {
            if (list == null || depth > 8) return;
            foreach (var a in list) Collect(a, into, depth);
        }

        static void Collect(BossAction a, HashSet<BossAction> into, int depth)
        {
            if (a == null || depth > 8 || !into.Add(a)) return;

            switch (a)
            {
                case BossTimelineAction tl when tl.tracks != null:
                    foreach (var track in tl.tracks)
                        if (track?.clips != null)
                            foreach (var c in track.clips)
                                Collect(c?.action, into, depth + 1);
                    break;

                case ComboAction combo when combo.subActions != null:
                    foreach (var e in combo.subActions) Collect(e.action, into, depth + 1);
                    break;

                case RandomAttackAction rnd when rnd.attackPool != null:
                    foreach (var p in rnd.attackPool) Collect(p, into, depth + 1);
                    break;
            }
        }
    }
}
