using System;
using CloneSwarm.Meta;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Balance Tool — Odin-powered editor สำหรับแก้ ScriptableObject ทุกประเภทในที่เดียว
///
/// **เปิดจาก:** Tools → Balance Tool
///
/// **Features (ฟรีจาก Odin):**
///   • Tree view ซ้าย + Inspector ขวา
///   • Search box (พิมพ์ filter ทันที)
///   • Multi-select (Ctrl+click) — แก้หลายตัวพร้อมกัน
///   • Favorites + recent
///   • รองรับ undo, prefab override visualization, asset preview
///
/// **เพิ่ม category ใหม่:** เพิ่มบรรทัด `Add(...)` ใน BuildMenuTree
///
/// ── ทำไมต้องมี Add() ห่อ AddAllAssetsAtPath ───────────────────────────────
/// `AddAllAssetsAtPath` เจอโฟลเดอร์ที่ไม่มีอยู่จริงแล้ว**เงียบ** ได้หมวดว่างเปล่ามาแทน
/// ซึ่งหน้าตาเหมือน "ยังไม่มี asset ประเภทนี้" ไม่เหมือน "เครื่องมือพัง"
///
/// เคยเกิดขึ้นจริง: path 5 ใน 7 เส้นชี้ `Assets/Script/Data/...` ค้างไว้หลังย้าย asset
/// ไป `Assets/ScriptableObjects/` — เครื่องมือเปิดได้ปกติแต่ซ่อน asset 73 ตัวอยู่หลายเดือน
/// โดยไม่มีอะไรเตือน (พบ 2026-08-13) · Add() แปลงความเงียบนั้นเป็น LogError
/// </summary>
public class BalanceTool : OdinMenuEditorWindow
{
    [MenuItem("Tools/Balance Tool")]
    static void Open()
    {
        var w = GetWindow<BalanceTool>("Balance Tool");
        w.minSize = new Vector2(600, 500);
    }

    protected override OdinMenuTree BuildMenuTree()
    {
        var tree = new OdinMenuTree(supportsMultiSelect: true)
        {
            Config = { DrawSearchToolbar = true }
        };

        // ── Combat / Player ─────────────────────────────────────────────
        // WeaponData แยกโฟลเดอร์ย่อย Hero/Super/Fusion/Passive — ไม่ flatten เพื่อให้เห็นกลุ่ม
        Add(tree, "Weapons",    "Assets/ScriptableObjects/WeaponData",    typeof(WeaponData), flatten: false);
        Add(tree, "Abilities",  "Assets/ScriptableObjects/AbilityData",   typeof(AbilityData));
        Add(tree, "Stats",      "Assets/ScriptableObjects/StatData",      typeof(StatData));
        Add(tree, "Characters", "Assets/ScriptableObjects/Characters",    typeof(CharacterData));
        Add(tree, "Fusions",    "Assets/ScriptableObjects/FusionRecipes", typeof(WeaponFusionRecipe));

        // ── Enemy / Boss ─────────────────────────────────────────────────
        // กรองด้วย BossEncounterConfig (คลาสแม่) ไม่ใช่ MiniBossConfig
        // MiniBossConfig เป็น alias legacy ที่สืบทอดมา — กรองด้วยตัวลูกจะเห็นเฉพาะ asset เก่า
        // และมองไม่เห็น BossEncounterConfig ที่ MiniBossConfig.cs เองบอกให้ใช้แทน
        // (บอสหลักซึ่งเป็นเงื่อนไขชนะของเกมหายไปทั้งตัวเพราะเรื่องนี้)
        Add(tree, "Boss Encounters", "Assets/Prefab/Enemy/Boss",           typeof(BossEncounterConfig));
        Add(tree, "Boss Encounters", "Assets/ScriptableObjects/BossAction", typeof(BossEncounterConfig));
        Add(tree, "Boss Actions",    "Assets/ScriptableObjects/BossAction", typeof(BossAction));
        Add(tree, "Waves",           "Assets/ScriptableObjects/Waves",      typeof(WaveConfig));

        // ── Meta / progression ───────────────────────────────────────────
        // ราคา talent กับสูตรจ่ายทองเป็นตัวเลขบาลานซ์เต็มตัว แต่เดิมไม่เคยอยู่ในเครื่องมือนี้
        Add(tree, "Talents",     "Assets/ScriptableObjects/Talent",    typeof(TalentData));
        Add(tree, "Meta DB",     "Assets/ScriptableObjects/Resources", typeof(MetaDatabase));

        // ── Presentation ─────────────────────────────────────────────────
        Add(tree, "VFX Assets",  "Assets/ScriptableObjects/VFX",       typeof(VFXAsset));

        // ยังไม่ใส่ Augments — ตอนนี้ยังไม่มี AugmentData asset สักตัวในโปรเจกต์
        // MetaSetupTools สร้างลง Assets/Script/Data/Augment/Definitions
        // พอสร้างแล้วให้เพิ่มบรรทัด Add(...) ตรงนี้

        return tree;
    }

    /// <summary>
    /// ห่อ AddAllAssetsAtPath ให้ "หาไม่เจอ" ดังขึ้นมา แทนที่จะได้หมวดว่างแบบเงียบๆ
    ///
    /// LogError = path ผิด (เกือบทุกครั้งคือ asset ถูกย้ายแล้วลืมแก้ตรงนี้)
    /// LogWarning = โฟลเดอร์มีจริงแต่ไม่มี asset ชนิดนั้นเลย — อาจตั้งใจ หรืออาจกรองผิดชนิด
    /// </summary>
    static void Add(OdinMenuTree tree, string label, string path, Type type, bool flatten = false)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            Debug.LogError($"[BalanceTool] '{label}' ชี้โฟลเดอร์ที่ไม่มีอยู่: {path}\n" +
                           "asset ถูกย้ายแล้วแต่ยังไม่ได้แก้ path ใน BalanceTool.BuildMenuTree");
            return;
        }

        int found = AssetDatabase.FindAssets($"t:{type.Name}", new[] { path }).Length;
        if (found == 0)
        {
            Debug.LogWarning($"[BalanceTool] '{label}' ({path}) ไม่มี {type.Name} สักตัว — " +
                             "ถ้าคาดว่าควรมี ให้เช็คว่ากรองถูกชนิดหรือเปล่า " +
                             "(กรองด้วยคลาสลูกจะมองไม่เห็น asset ที่เป็นคลาสแม่)");
            return;
        }

        tree.AddAllAssetsAtPath(label, path, type, includeSubDirectories: true, flattenSubDirectories: flatten);
    }
}
