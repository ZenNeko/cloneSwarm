using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

/// <summary>
/// เก็บเกี่ยวข้อความที่อยู่ใน ScriptableObject ทั้งโปรเจกต์เข้า String Table
///
/// **สถานะ: migrate เสร็จแล้วทั้ง 6 ชนิด** — ฟิลด์ข้อความทั้งหมดเป็น LocalizedString ไปแล้ว
/// จึงอ่านกลับมาเป็น string ไม่ได้อีก และไม่ต้องอ่าน (ค่าอยู่ใน table เรียบร้อย)
/// เหลือเก็บเกี่ยวได้เฉพาะฟิลด์ identity ที่ยังเป็น string ซึ่งใช้เป็นค่าตั้งต้นฝั่งอังกฤษ
/// เก็บไฟล์นี้ไว้เผื่อเพิ่มชนิดใหม่ในอนาคต — รันก่อนแปลงชนิดฟิลด์เสมอ
///
/// ── ทำไมต้องมีขั้นนี้ ──────────────────────────────────────────────────────
/// ขั้นถัดไปของการทำ localization คือเปลี่ยนชนิดฟิลด์
///     public string description;  →  public LocalizedString description;
/// ซึ่ง Unity serializer จะ **ทิ้งค่าเดิมทั้งหมด** เพราะชนิดไม่ตรงกัน
/// คำอธิบายไทยใน 88 asset จะหายเกลี้ยงโดยไม่มีใครทันสังเกต
///
/// ตัวนี้จึงต้องรัน **ก่อน** เปลี่ยนชนิดฟิลด์เสมอ — อ่านค่าเดิมออกมาใส่ table ให้ครบก่อน
///
/// ── ปลอดภัยแค่ไหน ─────────────────────────────────────────────────────────
/// อ่านอย่างเดียว ไม่แตะ asset ต้นทางเลย · เขียนลง String Table เท่านั้น
/// รันซ้ำได้ (idempotent) — key ที่มีอยู่แล้วจะถูกข้าม ไม่เขียนทับคำแปลที่คนแก้ไปแล้ว
/// ถ้าอยากเขียนทับให้เปิด overwriteExisting
///
/// ── ภาษาปลายทาง ───────────────────────────────────────────────────────────
/// ดูจากตัวอักษรในข้อความเอง — มีอักษรไทยลงคอลัมน์ th-TH ไม่มีลงคอลัมน์ en
/// ข้อความที่ปนสองภาษา (ในโปรเจกต์นี้มี 52 จุด เช่น "ฟิวชัน Blunderbuss + Splitter Bomb")
/// นับเป็นไทย เพราะโครงประโยคเป็นไทย · คอลัมน์อีกฝั่งเว้นว่างไว้ให้คนแปลเติม
/// </summary>
public static class LocalizationHarvester
{
    const string ContentTable = "Content";

    [MenuItem("Tools/Clone Swarm/Localization/1. Harvest ScriptableObject Text")]
    public static void Harvest() => Run(overwriteExisting: false);

    [MenuItem("Tools/Clone Swarm/Localization/1b. Harvest (เขียนทับของเดิม)")]
    public static void HarvestOverwrite()
    {
        if (!EditorUtility.DisplayDialog("เขียนทับคำแปลที่มีอยู่?",
                "โหมดนี้จะเขียนทับค่าใน String Table ที่มีอยู่แล้ว รวมถึงคำแปลที่คนแก้ไปด้วยมือ\n\n" +
                "ปกติควรใช้ตัวที่ 1 (ไม่เขียนทับ)",
                "เขียนทับ", "ยกเลิก"))
            return;
        Run(overwriteExisting: true);
    }

    // ══════════════════════════════════════════════════════════════════════
    /// <summary>ฟิลด์ข้อความหนึ่งช่องที่เก็บเกี่ยวได้</summary>
    readonly struct Field
    {
        public readonly string KeySuffix;   // "name" / "desc" / "passiveName"
        public readonly string Value;

        public Field(string keySuffix, string value) { KeySuffix = keySuffix; Value = value; }
    }

    static void Run(bool overwriteExisting)
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(ContentTable);
        if (collection == null)
        {
            Debug.LogError($"[Harvest] ไม่พบ String Table Collection ชื่อ '{ContentTable}' — " +
                           "สร้างก่อนที่ Window > Asset Management > Localization Tables");
            return;
        }

        var tables = collection.StringTables.ToList();
        var enTable = FindTable(tables, "en");
        var thTable = FindTable(tables, "th");
        if (enTable == null || thTable == null)
        {
            Debug.LogError($"[Harvest] '{ContentTable}' ต้องมีทั้ง locale en และ th — " +
                           $"ตอนนี้มี: {string.Join(", ", tables.Select(t => t.LocaleIdentifier.Code))}");
            return;
        }

        int added = 0, skipped = 0, empty = 0;
        var log = new StringBuilder();

        foreach (var (assetPath, keyPrefix, fields) in CollectAll())
        {
            foreach (var f in fields)
            {
                if (string.IsNullOrWhiteSpace(f.Value)) { empty++; continue; }

                string key = keyPrefix + "." + f.KeySuffix;
                var target = ContainsThai(f.Value) ? thTable : enTable;

                var shared = collection.SharedData.GetEntry(key) ?? collection.SharedData.AddKey(key);
                var existing = target.GetEntry(shared.Id);

                if (existing != null && !string.IsNullOrEmpty(existing.Value) && !overwriteExisting)
                {
                    skipped++;
                    continue;
                }

                target.AddEntry(shared.Id, f.Value);
                added++;
                log.AppendLine($"  {target.LocaleIdentifier.Code,-6} {key,-52} {Trim(f.Value)}");
            }
        }

        EditorUtility.SetDirty(collection.SharedData);
        foreach (var t in tables) EditorUtility.SetDirty(t);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Harvest] เขียนลง '{ContentTable}' {added} ค่า · ข้ามที่มีอยู่แล้ว {skipped} · " +
                  $"ฟิลด์ว่าง {empty}\n{log}");
    }

    // ══════════════════════════════════════════════════════════════════════
    // เก็บเกี่ยวทีละชนิด — เพิ่มชนิดใหม่ = เพิ่มบล็อกเดียวตรงนี้
    // ══════════════════════════════════════════════════════════════════════
    static IEnumerable<(string path, string keyPrefix, List<Field> fields)> CollectAll()
    {
        foreach (var r in Collect<WeaponData>("weapon", w => Slug(w.weaponName, w.name), w => new List<Field>
        {
            // displayName ว่างทั้ง 46 ตัวเป็นปกติ (DisplayName ตกกลับไปใช้ weaponName)
            // แต่ table ต้องมีชื่อให้แปล จึงใช้ weaponName เป็นค่าตั้งต้นฝั่งอังกฤษ
            new("name", w.weaponName),
            // desc ของ WeaponData ถูกย้ายเป็น LocalizedString ไปแล้ว (migrate เสร็จ)
            // อ่านกลับมาเป็น string ไม่ได้อีก และไม่ต้องอ่าน — ค่าอยู่ใน table เรียบร้อยแล้ว
            // ชนิดอื่นที่ยังเป็น string อยู่ ยังเก็บเกี่ยวได้ตามปกติ
        })) yield return r;

        foreach (var r in Collect<AbilityData>("ability", a => Slug(a.abilityName, a.name), a => new List<Field>
        {
            new("name", a.abilityName),
        })) yield return r;

        foreach (var r in Collect<CharacterData>("character", c => Slug(c.characterName, c.name), c => new List<Field>
        {
            new("name",        c.characterName),
        })) yield return r;

        foreach (var r in Collect<MapData>("map", m => Slug(m.mapId, m.name), m => new List<Field>
        {
            new("name", m.mapId),
        })) yield return r;

        foreach (var r in Collect<StatData>("stat", s => Slug(s.statName, s.name), s => new List<Field>
        {
            new("name", s.statName),
        })) yield return r;

        foreach (var r in Collect<CloneSwarm.Meta.TalentData>("talent", t => Slug(t.talentId, t.name), t => new List<Field>
        {
            new("name", t.talentName),
        })) yield return r;
    }

    static IEnumerable<(string, string, List<Field>)> Collect<T>(
        string category, Func<T, string> keyOf, Func<T, List<Field>> fieldsOf) where T : ScriptableObject
    {
        foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) continue;

            yield return (path, category + "." + keyOf(asset), fieldsOf(asset));
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    static StringTable FindTable(List<StringTable> tables, string codePrefix) =>
        tables.FirstOrDefault(t => t.LocaleIdentifier.Code.StartsWith(codePrefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>ใช้ค่าแรกถ้ามี ไม่งั้นใช้ตัวสำรอง — ชื่อที่โชว์ยังว่างอยู่เกือบทุก asset
    /// จึงต้องเอาฟิลด์ identity มาเป็นค่าตั้งต้นฝั่งอังกฤษ ไม่งั้น table จะไม่มีชื่อให้แปลเลย
    /// (ค่าใน table เป็นคนละชุดกับ identity แล้ว แปลได้อิสระ ไม่กระทบ key)</summary>
    static string Prefer(string first, string fallback) =>
        string.IsNullOrWhiteSpace(first) ? fallback : first;

    /// <summary>มีอักษรไทยอยู่ในข้อความไหม (U+0E00–U+0E7F)</summary>
    static bool ContainsThai(string s)
    {
        foreach (var c in s)
            if (c >= '฀' && c <= '๿') return true;
        return false;
    }

    /// <summary>ทำ key ให้เสถียร — อิงฟิลด์ identity ก่อน ตกกลับไปใช้ชื่อไฟล์
    /// ตัดช่องว่างและอักขระแปลกออก เพราะค่าจริงในโปรเจกต์มีทั้ง "GunnerWeapon " ที่มีช่องว่างท้าย
    /// และ "splitter bomb" ที่มีช่องว่างกลาง</summary>
    static string Slug(string preferred, string fallback)
    {
        string src = string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
        var sb = new StringBuilder(src.Length);
        foreach (var c in src.Trim())
        {
            if (char.IsLetterOrDigit(c) && c < 128) sb.Append(c);
            else if (c == '_' || c == '-')          sb.Append(c);
            else if (char.IsWhiteSpace(c))          sb.Append('_');
        }
        return sb.Length > 0 ? sb.ToString() : "unnamed";
    }

    static string Trim(string s)
    {
        s = s.Replace("\n", " ").Replace("\r", "");
        return s.Length <= 60 ? s : s.Substring(0, 57) + "...";
    }
}
