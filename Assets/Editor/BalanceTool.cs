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
/// **เพิ่ม category ใหม่:** เพิ่ม `tree.AddAllAssetsAtPath(...)` ใน BuildMenuTree
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
        tree.AddAllAssetsAtPath("Weapons",    "Assets/Script/Data/WeaponData",     typeof(WeaponData),    includeSubDirectories: true, flattenSubDirectories: false);
        tree.AddAllAssetsAtPath("Abilities",  "Assets/Script/Data/AbilityData",    typeof(AbilityData),   includeSubDirectories: true);
        tree.AddAllAssetsAtPath("Stats",      "Assets/Script/Data/StatData",       typeof(StatData),      includeSubDirectories: true);
        tree.AddAllAssetsAtPath("Characters", "Assets/Script/Data/Characters",     typeof(CharacterData), includeSubDirectories: true);
        tree.AddAllAssetsAtPath("Fusions",    "Assets/Script/Data/FusionRecipes",  typeof(WeaponFusionRecipe), includeSubDirectories: true);

        // ── Enemy / Boss ─────────────────────────────────────────────────
        tree.AddAllAssetsAtPath("MiniBoss Configs", "Assets/Prefab/Enemy/Boss",    typeof(MiniBossConfig), includeSubDirectories: true);
        tree.AddAllAssetsAtPath("Waves",            "Assets/ScriptableObjects/Waves", typeof(WaveConfig),  includeSubDirectories: true);

        return tree;
    }
}
