using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// ย้ายตาราง start+interval เดิมของ GameTimeline มาเป็นนัดหมายตามเวลา
    /// เมนู: Tools > Clone Swarm > Migrate Timeline Schedule
    ///
    /// ═══ ทำไมต้องมีตัวนี้ ═══
    ///
    /// ช่อง objectiveStartMin / miniBossIntervalMin ถูกถอดออกจากคลาสแล้ว ค่าที่
    /// เจ้าของโปรเจกต์จูนไว้ในซีน (1/2 กับ 2/3 ไม่ใช่ค่าเริ่มต้น 2/2 กับ 5/5)
    /// จะหายไปเงียบๆ ตอน Unity เซฟซีนครั้งถัดไป แล้วเกมจะไม่มีโซนกับมินิบอสเลย
    /// ทั้งรันโดยไม่มีอาการอื่น
    ///
    /// ═══ อ่านจากไฟล์ดิบ ไม่ใช่จาก component ═══
    ///
    /// ช่องพวกนั้นไม่มีอยู่ในคลาสแล้ว อ่านผ่าน component ไม่ได้ · แต่ค่ายังอยู่ใน
    /// ไฟล์ .unity บนดิสก์จนกว่า Unity จะเซฟทับ — จึงต้องรัน **ก่อน** เซฟซีนอื่นใด
    ///
    /// ═══ ตัดหางที่เลยบอสใหญ่ทิ้ง ═══
    ///
    /// ของเดิมยิงต่อไปเรื่อยๆ แม้บอสใหญ่ออกแล้ว — มินิบอสโผล่กลางไฟต์บอสใหญ่
    /// เป็นผลข้างเคียงของตาราง ไม่ใช่สิ่งที่ใครออกแบบ · ย้ายมาเป็นนัดหมายแล้ว
    /// เวลาทุกช่องมองเห็นได้ ของแบบนี้จึงต้องเป็นการเลือก ไม่ใช่ของแถม
    /// </summary>
    public static class P3RTimelineMigrator
    {
        private const string ScenePath = "Assets/GameScenes/SampleScene.unity";

        /// <summary>
        /// ค่าที่อ่านได้จาก SampleScene.unity ตอนถอดช่องเหล่านี้ออก (2026-09-17)
        ///
        /// **ไม่ใช่ค่าเริ่มต้นของคลาส** (2/2 กับ 5/5) — เป็นค่าที่เจ้าของโปรเจกต์
        /// จูนไว้เอง · จดไว้ตรงนี้เพราะแหล่งเดียวที่มีคือไฟล์ซีนบนดิสก์ ซึ่งหายไป
        /// ทันทีที่ใครกด Ctrl+S ในเอดิเตอร์ · ถ้าอ่านไฟล์ไม่เจอแล้ว ยังกู้ได้จากนี่
        /// </summary>
        private const float FallbackObjStart = 1f, FallbackObjEvery = 2f;
        private const float FallbackMbStart  = 2f, FallbackMbEvery  = 3f;

        [MenuItem("Tools/Clone Swarm/Migrate Timeline Schedule")]
        public static void Migrate()
        {
            var log = new StringBuilder("[ย้ายตารางเวลา]\n");

            string raw = File.ReadAllText(ScenePath);
            float objStart = ReadKey(raw, "objectiveStartMin");
            float objEvery = ReadKey(raw, "objectiveIntervalMin");
            float mbStart  = ReadKey(raw, "miniBossStartMin");
            float mbEvery  = ReadKey(raw, "miniBossIntervalMin");
            float bossAt   = ReadKey(raw, "mainBossTimeMin");

            if (float.IsNaN(bossAt)) bossAt = 15f;

            if (float.IsNaN(objStart) && float.IsNaN(mbStart))
            {
                objStart = FallbackObjStart; objEvery = FallbackObjEvery;
                mbStart  = FallbackMbStart;  mbEvery  = FallbackMbEvery;
                log.AppendLine("  ไฟล์ซีนไม่มีช่องตารางเดิมแล้ว (Unity เซฟทับไป) — " +
                               "ใช้ค่าที่จดไว้ตอนถอดช่องออก");
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var gt = scene.GetRootGameObjects()
                          .SelectMany(r => r.GetComponentsInChildren<GameTimeline>(true))
                          .FirstOrDefault();
            if (gt == null) { Debug.LogError("[ย้ายตารางเวลา] หา GameTimeline ไม่เจอในซีน"); return; }

            var list = gt.cues != null ? gt.cues.Where(c => c != null).ToList() : new List<TimelineCue>();

            AddExpanded(list, TimelineCueKind.ZoneObjective, "โซนปกติ", objStart, objEvery, bossAt, log);
            AddExpanded(list, TimelineCueKind.MiniBoss,      "มินิบอส", mbStart,  mbEvery,  bossAt, log);

            gt.cues = list.ToArray();
            EditorUtility.SetDirty(gt);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            log.AppendLine($"  บันทึก {ScenePath}");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// กาง start+interval ออกเป็นรายนาที แล้วเติมเฉพาะเวลาที่ยังไม่มีใครจอง
        ///
        /// เวลาที่ชนกับนัดหมายเดิมถูกข้าม — โซน augment จองนาที 3, 8, 12 ไว้แล้ว
        /// ถ้าเติมทับจะได้สองโซนพร้อมกันที่นาทีเดียว ซึ่งของเดิมไม่เคยเป็น
        /// </summary>
        private static void AddExpanded(List<TimelineCue> list, TimelineCueKind kind, string label,
                                        float startMin, float everyMin, float bossAt, StringBuilder log)
        {
            if (float.IsNaN(startMin) || startMin < 0f)
            {
                log.AppendLine($"  {label}: ตารางเดิมปิดอยู่ — ไม่ย้าย");
                return;
            }

            var taken = new HashSet<float>(
                list.Where(c => c.kind == kind && c.atMinutes != null)
                    .SelectMany(c => c.atMinutes));

            var times = new List<float>();
            var clash = new List<float>();

            for (float t = startMin; t < bossAt; t += (everyMin > 0f ? everyMin : bossAt))
            {
                if (taken.Contains(t)) { clash.Add(t); continue; }
                times.Add(t);
                if (everyMin <= 0f) break;       // interval 0 = ครั้งเดียวจบ
            }

            if (times.Count == 0)
            {
                log.AppendLine($"  {label}: ไม่มีเวลาให้ย้าย");
                return;
            }

            list.Add(new TimelineCue
            {
                label     = label,
                kind      = kind,
                atMinutes = times.ToArray(),
                variant   = "",
            });

            log.AppendLine($"  {label} (เดิม: เริ่ม {startMin} ทุก {everyMin} นาที) → " +
                           string.Join(", ", times.Select(t => t.ToString("0.#"))));
            if (clash.Count > 0)
                log.AppendLine($"    ข้ามนาที {string.Join(", ", clash.Select(t => t.ToString("0.#")))} — มีนัดหมายอยู่แล้ว");
        }

        /// <summary>อ่านค่าตัวเลขของคีย์ระดับ component จากไฟล์ซีนดิบ — NaN เมื่อไม่เจอ</summary>
        private static float ReadKey(string raw, string key)
        {
            var m = Regex.Match(raw, @"^\s{2}" + key + @":\s*(-?[0-9.]+)\s*$", RegexOptions.Multiline);
            return m.Success
                 ? float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)
                 : float.NaN;
        }
    }
}
