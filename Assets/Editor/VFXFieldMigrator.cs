using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ADR-006 Action Item 5 — migrate ค่า string key เดิม (detonateVfxKey / phaseVfxName / vfxKeyOnTrigger)
/// เป็นการอ้างอิง VFXAsset ตรงๆ หลังจาก field ใน C# ถูกเปลี่ยนชื่อ/ชนิดไปแล้ว (ดู Action Item 3-4 —
/// SpawnAoEActionBase.detonateVfx / BossPhase.phaseVfx / TriggerAugment.vfxOnTrigger)
///
/// ปัญหาที่ต้องแก้: พอเปลี่ยนชนิด field ใน C# แล้ว Unity deserialize ค่า string เดิมผ่าน field เดิมไม่ได้อีก
/// (ชื่อ field เปลี่ยน ชนิดก็เปลี่ยนจาก string เป็น object reference) แต่ตัวไฟล์ .asset บนดิสก์
/// **ยังมีข้อความ YAML เดิมค้างอยู่แบบข้อความล้วน** จนกว่าจะมีอะไรมา SaveAssets ทับ (Unity ไม่เขียน
/// ไฟล์ใหม่เองแค่เพราะ compile ผ่าน หรือแค่เปิด Editor) — สคริปต์นี้จึงอ่าน "ข้อความดิบ" ของไฟล์ .asset
/// ตรงๆ ด้วย regex (บายพาส deserializer ของ Unity ไปเลย) เพื่อดึงค่าเก่าออกมาได้แม้ field เดิมจะหายไป
/// จากคลาสแล้ว จากนั้นค่อยเขียนกลับผ่าน field ใหม่ตามปกติ
///
/// **ลำดับที่ต้องรัน (สำคัญ):**
///   1. เปิดโปรเจกต์ใน Unity ครั้งแรกหลังโค้ดชุดนี้ compile ผ่าน (ห้ามเปิด asset ที่เกี่ยวข้องใน
///      Inspector แล้วกด save ก่อน — การ save จะเขียนทับข้อความ YAML เดิมด้วยค่า default ของ field ใหม่
///      ทำให้ข้อมูลเดิมหายจริง ไม่ใช่แค่ compile ไม่ผ่าน)
///   2. Tools/LoL Swarm/ADR-006/Step 0 - Generate VFXAssets From Legacy Entries  (ต้องรันก่อนเสมอ —
///      ตัวนี้สร้าง VFXDatabase.assets ให้ id ตรงกับ entries ก่อน ไม่งั้น migrator หา VFXAsset ให้จับคู่ไม่ได้)
///   3. Tools/LoL Swarm/ADR-006/Step 5 - Migrate VFX String Fields To VFXAsset Refs  (ตัวนี้)
///   4. อ่านสรุปผลใน Console — ถ้ามี "resolve ไม่ได้" ต้องตามไปดูตำแหน่ง asset ที่ error บอกแล้วแก้เอง
///      (พิมพ์ผิด/key ถูกลบไปจาก VFXDatabase.entries แล้ว) ก่อนจะลบ .asset สำรอง/commit
/// </summary>
public static class VFXFieldMigrator
{
    private const string DatabasePath = "Assets/Script/Data/VFXDatabase.asset";

    [MenuItem("Tools/LoL Swarm/ADR-006/Step 5 - Migrate VFX String Fields To VFXAsset Refs")]
    public static void Migrate()
    {
        var db = AssetDatabase.LoadAssetAtPath<VFXDatabase>(DatabasePath);
        if (db == null)
        {
            Debug.LogError($"[VFXFieldMigrator] ไม่พบ VFXDatabase ที่ {DatabasePath} — ยกเลิก");
            return;
        }
        if (db.assets == null || db.assets.Count == 0)
        {
            Debug.LogError("[VFXFieldMigrator] VFXDatabase.assets ว่าง — ต้องรัน " +
                            "'Tools/LoL Swarm/ADR-006/Step 0 - Generate VFXAssets...' ก่อน แล้วค่อยรันตัวนี้ — ยกเลิก");
            return;
        }

        // key (legacy) -> VFXAsset โดยอิงลำดับเดียวกับ entries (id เสถียร = index) — ดู VFXAssetGenerator
        var keyToAsset = new Dictionary<string, VFXAsset>();
        for (int i = 0; i < db.entries.Count && i < db.assets.Count; i++)
        {
            var e = db.entries[i];
            if (e == null || string.IsNullOrEmpty(e.key)) continue;
            if (db.assets[i] == null)
            {
                Debug.LogWarning($"[VFXFieldMigrator] VFXDatabase.assets[{i}] ('{e.key}') ยังว่าง — " +
                                  "key นี้จะ resolve ไม่ได้จนกว่าจะรัน Step 0 ใหม่หรือเติมช่องนี้เอง");
                continue;
            }
            keyToAsset[e.key] = db.assets[i];
        }

        // รวบรวมทุก asset path ที่อาจมีอ็อบเจ็กต์ชนิดที่เกี่ยวข้องฝังอยู่ — ไฟล์เดียวมีได้หลายอ็อบเจ็กต์
        // (Boss Designer สร้าง sub-asset ฝังในไฟล์เดียวกัน เช่น MiniBossConfig.asset มีทั้ง
        // MiniBossConfig + CrossAoEAction + BossTimelineAction อยู่ในไฟล์เดียว) ต้องกวาดทุกชนิดรวมกัน
        // แล้ว dedupe ด้วย path ไม่งั้นจะเปิด/เขียนไฟล์เดียวกันซ้ำหลายรอบ
        var targetPaths = new HashSet<string>();
        foreach (var t in new[] { "BossAction", "BossEncounterConfig", "TriggerAugment" })
            foreach (var guid in AssetDatabase.FindAssets($"t:{t}"))
                targetPaths.Add(AssetDatabase.GUIDToAssetPath(guid));

        int resolved = 0, unresolved = 0, empty = 0;
        var touchedObjects = new List<UnityEngine.Object>();

        foreach (var path in targetPaths)
        {
            string rawText;
            try { rawText = File.ReadAllText(path); }
            catch (Exception ex)
            {
                Debug.LogError($"[VFXFieldMigrator] อ่านไฟล์ไม่ได้: {path} — {ex.Message}");
                continue;
            }

            Dictionary<long, string> blocks = SplitYamlBlocks(rawText);
            UnityEngine.Object[] objs = AssetDatabase.LoadAllAssetsAtPath(path);

            foreach (var obj in objs)
            {
                if (obj == null) continue;

                if (obj is SpawnAoEActionBase spawnAction)
                {
                    if (!TryGetBlock(obj, blocks, out string block)) continue;

                    string legacy = ExtractSingleField(block, "detonateVfxKey");
                    bool changed = ApplyResolution(
                        path, "detonateVfxKey", legacy,
                        () => spawnAction.detonateVfx != null,
                        v => spawnAction.detonateVfx = v,
                        keyToAsset, ref resolved, ref unresolved, ref empty);
                    if (changed) touchedObjects.Add(obj);
                }
                else if (obj is BossEncounterConfig bec) // ครอบ MiniBossConfig ด้วย (subclass)
                {
                    if (!TryGetBlock(obj, blocks, out string block)) continue;

                    List<string> legacyList = ExtractRepeatedField(block, "phaseVfxName");
                    bool anyChanged = false;
                    for (int i = 0; i < bec.phases.Count; i++)
                    {
                        string legacy = i < legacyList.Count ? legacyList[i] : null;
                        int idx = i; // capture ให้ closure ปลอดภัย
                        bool changed = ApplyResolution(
                            path, $"phases[{idx}].phaseVfxName", legacy,
                            () => bec.phases[idx].phaseVfx != null,
                            v => bec.phases[idx].phaseVfx = v,
                            keyToAsset, ref resolved, ref unresolved, ref empty);
                        anyChanged |= changed;
                    }
                    if (anyChanged) touchedObjects.Add(obj);
                }
                else if (obj is TriggerAugment ta)
                {
                    if (!TryGetBlock(obj, blocks, out string block)) continue;

                    string legacy = ExtractSingleField(block, "vfxKeyOnTrigger");
                    bool changed = ApplyResolution(
                        path, "vfxKeyOnTrigger", legacy,
                        () => ta.vfxOnTrigger != null,
                        v => ta.vfxOnTrigger = v,
                        keyToAsset, ref resolved, ref unresolved, ref empty);
                    if (changed) touchedObjects.Add(obj);
                }
            }
        }

        foreach (var obj in touchedObjects)
            EditorUtility.SetDirty(obj);

        if (touchedObjects.Count > 0)
            AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[VFXFieldMigrator] เสร็จ — resolved {resolved} · unresolved {unresolved} · empty {empty} · " +
                  $"แก้ไข {touchedObjects.Count} object(s) จาก {targetPaths.Count} ไฟล์ที่ตรวจ");
    }

    private static bool TryGetBlock(UnityEngine.Object obj, Dictionary<long, string> blocks, out string block)
    {
        block = null;
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out _, out long localId))
            return false;
        return blocks.TryGetValue(localId, out block);
    }

    /// <summary>
    /// ตัดสินใจว่าจะเซ็ต field ใหม่หรือไม่ + นับสถิติ
    /// legacyValue == null  → ไม่เจอ field เดิมใน raw YAML เลย (ไฟล์อาจถูก save ทับไปแล้วก่อนรันตัวนี้)
    ///                         ถ้า field ใหม่มีค่าอยู่แล้ว (currentHasValue) ถือว่า migrate ไปแล้ว/ตั้งเองแล้ว
    ///                         ไม่ error — แต่ถ้าไม่มีค่าด้วย ให้เตือน (ไม่ error เพราะไม่มีหลักฐานว่าข้อมูลหาย
    ///                         จริงหรือเป็นของใหม่ที่ไม่เคยมีค่ามาก่อน)
    /// legacyValue == ""/"None" → เดิมว่างจริง — นับ empty เฉยๆ
    /// legacyValue = อย่างอื่น  → หาใน keyToAsset ถ้าเจอ resolve ถ้าไม่เจอ LogError (นี่คือ typo/key หาย)
    /// </summary>
    private static bool ApplyResolution(
        string path, string fieldLabel, string legacyValue,
        Func<bool> currentHasValue, Action<VFXAsset> setter,
        Dictionary<string, VFXAsset> keyToAsset,
        ref int resolved, ref int unresolved, ref int empty)
    {
        if (legacyValue == null)
        {
            empty++;
            if (!currentHasValue())
            {
                Debug.LogWarning($"[VFXFieldMigrator] {path} · {fieldLabel}: ไม่พบ field เดิมใน raw YAML และ " +
                                  "field ใหม่ก็ยังว่าง — อาจเป็น object ที่สร้างหลัง migrate ไปแล้ว หรือไฟล์ถูก " +
                                  "save ทับก่อนรัน migrator (ตรวจสอบด้วยตาถ้าเคยตั้งค่าไว้)");
            }
            return false;
        }

        string trimmed = legacyValue.Trim('"');
        if (string.IsNullOrEmpty(trimmed) || trimmed == "None")
        {
            empty++;
            return false;
        }

        if (keyToAsset.TryGetValue(trimmed, out var asset) && asset != null)
        {
            setter(asset);
            resolved++;
            return true;
        }

        Debug.LogError($"[VFXFieldMigrator] resolve ไม่ได้: {path} · {fieldLabel} = '{trimmed}' — " +
                        "ไม่พบ key นี้ใน VFXDatabase.entries (พิมพ์ผิด หรือ key ถูกลบไปแล้ว?)");
        unresolved++;
        return false;
    }

    // ── YAML raw parsing — บายพาส Unity deserializer เพราะ field เดิมถูกเปลี่ยนชนิด/ชื่อไปแล้ว ──────

    /// <summary>แยกไฟล์ .asset (multi-document YAML) ออกเป็นบล็อกต่ออ็อบเจ็กต์ ด้วย fileID ที่ Unity
    /// เขียนกำกับไว้ที่หัวแต่ละ document ("--- !u!114 &&lt;id&gt;") — คนละอ็อบเจ็กต์ในไฟล์เดียวกัน
    /// (เช่น sub-asset ที่ Boss Designer ฝังไว้) จึงไม่ปนกัน</summary>
    private static Dictionary<long, string> SplitYamlBlocks(string text)
    {
        var result = new Dictionary<long, string>();
        var matches = Regex.Matches(text, @"^--- !u!\d+ &(-?\d+)\s*$", RegexOptions.Multiline);
        for (int i = 0; i < matches.Count; i++)
        {
            if (!long.TryParse(matches[i].Groups[1].Value, out long id)) continue;
            int start = matches[i].Index + matches[i].Length;
            int end = (i + 1 < matches.Count) ? matches[i + 1].Index : text.Length;
            result[id] = text.Substring(start, end - start);
        }
        return result;
    }

    /// <summary>ดึงค่า field ระดับบนสุดของอ็อบเจ็กต์ (indent 2 ช่อง) — คืน null ถ้าไม่เจอ field นี้เลย
    /// (ต่างจากคืน "" ซึ่งแปลว่าเจอ field แต่ค่าว่าง)</summary>
    private static string ExtractSingleField(string block, string fieldName)
    {
        var m = Regex.Match(block, $@"^  {Regex.Escape(fieldName)}: ?(.*)$", RegexOptions.Multiline);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    /// <summary>ดึงค่า field ที่ซ้ำได้หลายครั้งในบล็อกเดียว (เช่น phaseVfxName ในลิสต์ phases[]) —
    /// เรียงตามลำดับที่เจอใน YAML ซึ่งตรงกับลำดับ index ใน List&lt;BossPhase&gt; เสมอ (YAML sequence
    /// รักษาลำดับ)</summary>
    private static List<string> ExtractRepeatedField(string block, string fieldName)
    {
        var list = new List<string>();
        foreach (Match m in Regex.Matches(block, $@"^\s+{Regex.Escape(fieldName)}: ?(.*)$", RegexOptions.Multiline))
            list.Add(m.Groups[1].Value.Trim());
        return list;
    }
}
