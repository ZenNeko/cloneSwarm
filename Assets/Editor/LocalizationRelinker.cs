using System;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

/// <summary>
/// ผูกฟิลด์ LocalizedString ของ asset เข้ากับ entry ใน String Table ให้อัตโนมัติ
///
/// ── ทำไมต้องมี ────────────────────────────────────────────────────────────
/// พอเปลี่ยน `public string description` เป็น `public LocalizedString description`
/// ฟิลด์จะว่างทั้งหมด ต้องไปเลือก table + key ให้ทีละ asset ใน Inspector
/// รวมทุกชนิดแล้วเกิน 170 ช่อง
///
/// key ถูกสร้างจากฟิลด์ identity ด้วยกฎ Slug เดียวกับ LocalizationHarvester
/// (แก้กฎเมื่อไหร่ต้องแก้ทั้งสองไฟล์พร้อมกัน) จึงคำนวณย้อนได้โดยไม่ต้องเดา
///
/// ── ปลอดภัยแค่ไหน ─────────────────────────────────────────────────────────
/// เขียนเฉพาะช่องที่ยังว่าง — ที่ผูกไว้แล้วไม่แตะ
/// key ที่ไม่มีใน table จะข้ามเฉยๆ ไม่สร้าง entry ใหม่ให้มั่ว (ต้องรัน Harvest ก่อน)
/// </summary>
public static class LocalizationRelinker
{
    const string ContentTable = "Content";

    [MenuItem("Tools/Clone Swarm/Localization/2. Relink LocalizedString fields")]
    public static void Relink()
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(ContentTable);
        if (collection == null)
        {
            Debug.LogError($"[Relink] ไม่พบ String Table Collection '{ContentTable}' — รัน Harvest ก่อน");
            return;
        }

        var shared = collection.SharedData;
        var stats  = new Stats();

        LinkAll<WeaponData>("weapon", a => Slug(a.weaponName, a.name), a => new[]
        {
            ("name", a.displayName), ("desc", a.description),
        }, shared, stats);

        LinkAll<AbilityData>("ability", a => Slug(a.abilityName, a.name), a => new[]
        {
            ("name", a.displayName), ("desc", a.description),
        }, shared, stats);

        LinkAll<CharacterData>("character", a => Slug(a.characterName, a.name), a => new[]
        {
            ("name", a.displayName),             ("desc", a.description),
            ("passiveName", a.passiveName),      ("passiveDesc", a.passiveDescription),
        }, shared, stats);

        LinkAll<MapData>("map", a => Slug(a.mapId, a.name), a => new[]
        {
            ("name", a.displayName), ("desc", a.description),
        }, shared, stats);

        LinkAll<StatData>("stat", a => Slug(a.statName, a.name), a => new[]
        {
            ("name", a.displayName), ("desc", a.description),
        }, shared, stats);

        LinkAll<CloneSwarm.Meta.TalentData>("talent", a => Slug(a.talentId, a.name), a => new[]
        {
            ("name", a.displayName), ("desc", a.description),
        }, shared, stats);

        AssetDatabase.SaveAssets();
        Debug.Log($"[Relink] ผูกให้ {stats.Linked} ช่อง · ผูกไว้อยู่แล้ว {stats.Already} · " +
                  $"ไม่มี key ใน table {stats.Missing}");
    }

    class Stats { public int Linked, Already, Missing; }

    /// <summary>
    /// ผูกทุกฟิลด์ LocalizedString ของ asset ชนิดหนึ่ง
    ///
    /// "ไม่มี key ใน table" เป็นเรื่องปกติ ไม่ใช่ error — Harvest ข้ามฟิลด์ที่ว่างไว้แต่แรก
    /// (เช่น passiveName ของตัวละครที่ไม่มี passive) จึงไม่มี entry ให้ผูก
    /// นับรวมไว้เฉยๆ ไม่เตือนรายตัวให้รก Console
    /// </summary>
    static void LinkAll<T>(string category,
                           Func<T, string> keyOf,
                           Func<T, (string suffix, LocalizedString field)[]> fieldsOf,
                           SharedTableData shared,
                           Stats stats) where T : ScriptableObject
    {
        foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
        {
            string path  = AssetDatabase.GUIDToAssetPath(guid);
            var    asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) continue;

            bool dirty = false;
            foreach (var (suffix, field) in fieldsOf(asset))
            {
                if (field == null)  continue;
                if (!field.IsEmpty) { stats.Already++; continue; }

                string key = category + "." + keyOf(asset) + "." + suffix;
                if (shared.GetEntry(key) == null) { stats.Missing++; continue; }

                field.SetReference(ContentTable, key);
                stats.Linked++;
                dirty = true;
            }
            if (dirty) EditorUtility.SetDirty(asset);
        }
    }

    /// <summary>ต้องเหมือน LocalizationHarvester.Slug เป๊ะ — ไม่งั้น key ที่คำนวณจะไม่ตรงกับที่ harvest ไว้</summary>
    static string Slug(string preferred, string fallback)
    {
        string src = string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
        var sb = new System.Text.StringBuilder(src.Length);
        foreach (var c in src.Trim())
        {
            if (char.IsLetterOrDigit(c) && c < 128) sb.Append(c);
            else if (c == '_' || c == '-')          sb.Append(c);
            else if (char.IsWhiteSpace(c))          sb.Append('_');
        }
        return sb.Length > 0 ? sb.ToString() : "unnamed";
    }
}
