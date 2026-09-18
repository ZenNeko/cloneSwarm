using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

/// <summary>
/// เติมข้อความ "ประกาศกลางจอ" ทั้งหมดลง String Table ชื่อ Announcements
///
/// ── ทำไมไม่ใช่ LocalizationHarvester ──────────────────────────────────────
/// Harvest อ่านค่าจาก ScriptableObject ที่มีอยู่จริงในโปรเจกต์ · ประกาศกลางจอไม่มี asset
/// สักตัว มันเป็น literal ที่ฝังอยู่ในโค้ดเจ็ดไฟล์ จึงเก็บเกี่ยวแบบนั้นไม่ได้
/// ตัวนี้จึง **พก** ข้อความมาเองเป็นข้อมูลในไฟล์ (ดู <see cref="Rows"/>)
///
/// ── เพิ่มประกาศใหม่ทำยังไง ────────────────────────────────────────────────
/// 1. เพิ่มหนึ่งแถวใน Rows (key · en · th-TH)
/// 2. เรียก GameHUD.ShowAnnouncementKey("key ที่เพิ่งเพิ่ม", สี) ในโค้ด
/// 3. รันเมนู 3. Build Announcement Table
/// Rows เป็นแหล่งเดียวของรายการ key — ห้ามมีลิสต์ที่สองที่ไหนอีก
///
/// ── ปลอดภัยแค่ไหน ─────────────────────────────────────────────────────────
/// กติกาเดียวกับ Harvest — รันซ้ำได้ ช่องที่มีค่าอยู่แล้วจะถูกข้าม ไม่เขียนทับคำแปลที่คนแก้
/// อยากเขียนทับต้องเลือกเมนู 3b โดยเจตนา
///
/// ── ทำไมเช็คว่ามี key ไหนในโค้ดที่ไม่มีในตาราง ───────────────────────────
/// ความพลาดที่จะเกิดจริงคือ "เพิ่มจุดเรียกแล้วลืมเพิ่มแถว" · ตอนรันเกมมันจะกลายเป็น
/// ข้อความ key ดิบๆ กลางจอกลางบอส ซึ่งเจอตอนสายไปแล้ว · สแกนโค้ดตอน build ตาราง
/// แล้วเตือนทันทีถูกกว่ามาก
/// </summary>
public static class AnnouncementTableBuilder
{
    const string AnnouncementsTable = "Announcements";

    [MenuItem("Tools/Clone Swarm/Localization/3. Build Announcement Table")]
    public static void Build() => Run(overwriteExisting: false);

    [MenuItem("Tools/Clone Swarm/Localization/3b. Build Announcement Table (เขียนทับของเดิม)")]
    public static void BuildOverwrite()
    {
        if (!EditorUtility.DisplayDialog("เขียนทับคำแปลที่มีอยู่?",
                "โหมดนี้จะเขียนทับค่าใน Announcements ที่มีอยู่แล้ว รวมถึงคำแปลที่คนแก้ไปด้วยมือ\n\n" +
                "ปกติควรใช้ตัวที่ 3 (ไม่เขียนทับ)",
                "เขียนทับ", "ยกเลิก"))
            return;
        Run(overwriteExisting: true);
    }

    // ══════════════════════════════════════════════════════════════════════
    /// <summary>ประกาศหนึ่งข้อความ — key กับค่าของทั้งสอง locale อยู่บรรทัดเดียวกัน
    /// เพื่อให้เห็นพร้อมกันว่าคู่ไหนแปลไม่ตรงกัน (ตาราง Unity แยกไฟล์ต่อ locale จึงเทียบยาก)</summary>
    readonly struct Row
    {
        public readonly string Key;
        public readonly string En;
        public readonly string Th;

        public Row(string key, string en, string th) { Key = key; En = en; Th = th; }
    }

    // ══════════════════════════════════════════════════════════════════════
    // รายการ key ทั้งหมด — แหล่งเดียวของความจริง
    //
    // กฎการตั้งชื่อ: announce.<ระบบที่ยิงประกาศ>.<เหตุการณ์>
    //   อิงระบบที่ยิง ไม่ใช่หน้าจอที่ไปโผล่ เพราะทุกอันโผล่ที่เดียวกันหมด
    //   สิ่งที่คนหาคือ "ข้อความของ tether อยู่ไหน" ไม่ใช่ "ข้อความกลางจออันที่สาม"
    //   ชั้นแรกเป็น announce. ให้เข้าชุดกับ weapon. / ability. / map. ในตาราง Content
    //
    // `{0}` คือค่าที่โค้ดส่งมาตอนรัน — ย้ายตำแหน่งได้อิสระต่อ locale แต่ **ห้ามหาย**
    // ไม่งั้น string.Format จะทิ้งตัวเลขนั้นไปเงียบๆ
    //
    // ห้ามใส่ emoji หรือสัญลักษณ์อย่าง ★ ⚠ ⚡ ✓ — ไม่มีฟอนต์ในโปรเจกต์ที่มี code point
    // พวกนั้น มันขึ้นจอเป็นกล่องสี่เหลี่ยม · 🔗 กับ ⛓ ที่เคยนำหน้าข้อความ tether
    // ถูกตัดทิ้งตอนย้ายเข้าตารางด้วยเหตุนี้ (กติกาข้อ 5 ของ skill thai-text)
    //
    // em dash (—) เก็บไว้ทั้งสองฝั่ง เพราะเป็นตัวที่ข้อความเดิมใช้อยู่แล้วและขึ้นจอได้ ·
    // การย้ายเข้าตารางไม่ควรเปลี่ยนหน้าตาที่ผู้เล่นเห็นไปด้วยโดยไม่ตั้งใจ
    // ══════════════════════════════════════════════════════════════════════
    static readonly Row[] Rows =
    {
        // ── Boss ──────────────────────────────────────────────────────────
        new("announce.boss.main",      "MAIN BOSS!",      "บอสใหญ่มาแล้ว!"),
        new("announce.boss.enrage_in", "ENRAGE IN {0}s!", "อีก {0} วินาทีบอสจะคลั่ง!"),

        // ── Tether (BossTether) ───────────────────────────────────────────
        new("announce.tether.close",        "TETHER! Stay close together!",      "TETHER! อยู่ใกล้กันไว้!"),
        new("announce.tether.close_pillar", "TETHER! Stay close to the pillar!", "TETHER! อยู่ใกล้เสาไว้!"),
        new("announce.tether.far",          "TETHER! Run away from each other!", "TETHER! วิ่งออกจากกัน!"),
        new("announce.tether.far_pillar",   "TETHER! Run away from the pillar!", "TETHER! วิ่งออกจากเสา!"),
        new("announce.tether.leash",        "LEASH! Do not leave the ring!",     "LEASH! ห้ามออกนอกวง!"),
        new("announce.tether.broken",       "TETHER BROKEN!",                    "สายขาดแล้ว!"),
        new("announce.tether.exploded",     "TETHER EXPLODED!",                  "สายระเบิด!"),

        // ── Limit Cut (LimitCutAction) ────────────────────────────────────
        new("announce.limitcut.start", "LIMIT CUT — watch your number, step out in order!",
                                       "LIMIT CUT — ดูเลขแล้วออกตามลำดับ!"),

        // ── Floor Hazard ──────────────────────────────────────────────────
        new("announce.floorhazard.warn",     "FLOOR HAZARD! Get into a Safe Zone!", "พื้นจะระเบิด! วิ่งเข้า Safe Zone!"),
        new("announce.floorhazard.detonate", "FLOOR EXPLODES!",                     "พื้นระเบิด!"),

        // ── Telegraph / AoE ───────────────────────────────────────────────
        new("announce.telegraph.targeted",    "TARGETED — RUN AWAY!",        "โดนล็อกเป้า — วิ่งหนี!"),
        new("announce.telegraph.locked_in",   "LOCKED IN!",                  "ล็อกเป้าแล้ว!"),
        new("announce.telegraph.color_match", "Stand in the {0} circle!",    "เข้าไปยืนในวง{0}!"),

        // ชื่อสีของ ColorMatch — แยก key เพราะมันถูกยัดเข้า {0} ของประโยคข้างบน
        // ฝั่งไทยขึ้นต้นด้วย "สี" เพื่อให้ต่อกับ "วง" แล้วอ่านเป็น "วงสีแดง" ได้พอดี
        new("announce.color.red",    "RED",    "สีแดง"),
        new("announce.color.blue",   "BLUE",   "สีน้ำเงิน"),
        new("announce.color.green",  "GREEN",  "สีเขียว"),
        new("announce.color.yellow", "YELLOW", "สีเหลือง"),

        // ── Zone Objective ────────────────────────────────────────────────
        new("announce.objective.activate", "ZONE OBJECTIVE — Stand inside to activate!",
                                           "ภารกิจโซน — เข้าไปยืนเพื่อเปิดใช้งาน!"),
        new("announce.objective.complete", "OBJECTIVE COMPLETE!  +EXP  +HEAL  +ORB",
                                           "ภารกิจสำเร็จ!  +EXP  +HEAL  +ORB"),
        new("announce.objective.expired",  "OBJECTIVE EXPIRED", "ภารกิจหมดเวลา"),

        new("announce.objective.quest.fetch",   "QUEST: Deliver {0} items!",
                                                "ภารกิจ: ส่งของให้ครบ {0} ชิ้น!"),
        new("announce.objective.quest.survive", "QUEST: Survive {0}s in the zone!",
                                                "ภารกิจ: อยู่รอดในโซนให้ได้ {0} วินาที!"),
        new("announce.objective.quest.destroy", "QUEST: Destroy {0} objects!",
                                                "ภารกิจ: ทำลายเป้าหมายให้ครบ {0} ชิ้น!"),
        new("announce.objective.quest.kill",    "QUEST: Kill {0} enemies inside the zone!",
                                                "ภารกิจ: กำจัดศัตรูในโซนให้ครบ {0} ตัว!"),
        new("announce.objective.quest.seal",    "QUEST: Seal the rift — hold the zone {0}s!",
                                                "ภารกิจ: ปิดรอยแยก — ยึดโซนไว้ {0} วินาที!"),
        new("announce.objective.quest.default", "QUEST STARTED", "เริ่มภารกิจแล้ว"),
    };

    // ══════════════════════════════════════════════════════════════════════
    static void Run(bool overwriteExisting)
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(AnnouncementsTable);
        if (collection == null)
        {
            Debug.LogError($"[Announce] ไม่พบ String Table Collection ชื่อ '{AnnouncementsTable}' — " +
                           "สร้างก่อนที่ Window > Asset Management > Localization Tables");
            return;
        }

        var tables  = collection.StringTables.ToList();
        var enTable = FindTable(tables, "en");
        var thTable = FindTable(tables, "th");
        if (enTable == null || thTable == null)
        {
            Debug.LogError($"[Announce] '{AnnouncementsTable}' ต้องมีทั้ง locale en และ th — " +
                           $"ตอนนี้มี: {string.Join(", ", tables.Select(t => t.LocaleIdentifier.Code))}");
            return;
        }

        int added = 0, skipped = 0;
        var log = new StringBuilder();

        foreach (var row in Rows)
        {
            var shared = collection.SharedData.GetEntry(row.Key) ?? collection.SharedData.AddKey(row.Key);

            Write(enTable, shared.Id, row.Key, row.En, overwriteExisting, ref added, ref skipped, log);
            Write(thTable, shared.Id, row.Key, row.Th, overwriteExisting, ref added, ref skipped, log);
        }

        EditorUtility.SetDirty(collection.SharedData);
        foreach (var t in tables) EditorUtility.SetDirty(t);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Announce] เขียนลง '{AnnouncementsTable}' {added} ค่า · ข้ามที่มีอยู่แล้ว {skipped} · " +
                  $"ทั้งหมด {Rows.Length} key\n{log}");

        WarnAboutKeysUsedInCodeButMissingHere();
    }

    static void Write(StringTable table, long id, string key, string value, bool overwriteExisting,
                      ref int added, ref int skipped, StringBuilder log)
    {
        var existing = table.GetEntry(id);
        if (existing != null && !string.IsNullOrEmpty(existing.Value) && !overwriteExisting)
        {
            skipped++;
            return;
        }

        table.AddEntry(id, value);
        added++;
        log.AppendLine($"  {table.LocaleIdentifier.Code,-6} {key,-38} {Trim(value)}");
    }

    // ══════════════════════════════════════════════════════════════════════
    /// <summary>
    /// เทียบ key ที่โค้ดเรียกจริงกับ Rows — เจอที่ขาดแล้วเตือนพร้อมบอกว่าต้องทำอะไร
    ///
    /// จับเฉพาะ literal ที่ขึ้นต้นด้วย `announce.` และ **ไม่จบด้วยจุด** ·
    /// ที่ต้องกันตัวจบด้วยจุดเพราะ TelegraphZone ประกอบ key ของชื่อสีจากชิ้นส่วน
    /// (`"announce.color." + colorName`) ซึ่งชิ้นแรกไม่ใช่ key เต็ม ไม่งั้นจะเตือนผิดทุกรอบ
    ///
    /// ทางกลับกัน key ที่อยู่ใน Rows แต่ไม่มีใครเรียก **ไม่เตือน** — ของที่เตรียมไว้ล่วงหน้า
    /// กับของที่ประกอบชื่อตอนรัน (announce.color.*) แยกจากกันไม่ได้ด้วยการอ่านโค้ด
    /// </summary>
    static void WarnAboutKeysUsedInCodeButMissingHere()
    {
        const string scriptRoot = "Assets/Script";
        if (!Directory.Exists(scriptRoot)) return;

        var known   = new HashSet<string>(Rows.Select(r => r.Key));
        var pattern = new Regex(@"""(announce\.[A-Za-z0-9_.]*[A-Za-z0-9_])""");
        var missing = new SortedDictionary<string, string>();

        foreach (var file in Directory.GetFiles(scriptRoot, "*.cs", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("//")) continue;   // คอมเมนต์ไม่ได้เรียกจริง

                foreach (Match m in pattern.Matches(lines[i]))
                {
                    string key = m.Groups[1].Value;
                    if (known.Contains(key) || missing.ContainsKey(key)) continue;
                    missing[key] = $"{file.Replace('\\', '/')}:{i + 1}";
                }
            }
        }

        if (missing.Count == 0) return;

        var sb = new StringBuilder();
        foreach (var kv in missing) sb.AppendLine($"  {kv.Key,-38} {kv.Value}");

        Debug.LogWarning($"[Announce] โค้ดเรียก key ที่ไม่มีใน Rows {missing.Count} ตัว — " +
                         "ตอนรันจะโชว์ตัว key ดิบกลางจอ · เพิ่มแถวใน AnnouncementTableBuilder.Rows " +
                         $"แล้วรันเมนูนี้อีกรอบ\n{sb}");
    }

    // ══════════════════════════════════════════════════════════════════════
    static StringTable FindTable(List<StringTable> tables, string codePrefix) =>
        tables.FirstOrDefault(t => t.LocaleIdentifier.Code.StartsWith(codePrefix, StringComparison.OrdinalIgnoreCase));

    static string Trim(string s)
    {
        s = s.Replace("\n", " ").Replace("\r", "");
        return s.Length <= 60 ? s : s.Substring(0, 57) + "...";
    }
}
