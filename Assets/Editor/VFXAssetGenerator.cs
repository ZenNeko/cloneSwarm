using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ADR-006 ขั้นเตรียม (ก่อน Action Item 5) — สร้าง VFXAsset ทีละตัวจาก VFXDatabase.entries (legacy)
/// แล้วต่อท้ายเข้า VFXDatabase.assets "ตามลำดับเดียวกับ entries" เพื่อให้ id (= index ในลิสต์) นิ่ง
///
/// id เป็นสัญญาเครือข่าย — TelegraphInit.detonateVfxId ฯลฯ อ้างอิงมันตรงๆ ข้าม client/server
/// ห้ามให้ id ขยับความหมายหลังใช้งานจริงแล้ว จึงต่อท้ายลิสต์เท่านั้น ไม่แทรก/ไม่เรียงใหม่
///
/// Idempotent — รันซ้ำได้กี่ครั้งก็ได้โดยไม่สร้างไฟล์ซ้ำและไม่ขยับ id เดิม:
///   • ช่อง assets[i] มีของอยู่แล้ว → ข้าม ไม่แตะ
///   • ไฟล์ .asset ปลายทางมีอยู่แล้ว (เช่นเคย generate ไว้ แต่ช่องใน assets ถูกล้างออกด้วยมือ)
///     → โหลดตัวเดิมกลับมาใช้ ไม่สร้างไฟล์ใหม่ซ้อน
/// </summary>
public static class VFXAssetGenerator
{
    private const string TargetFolder = "Assets/ScriptableObjects/VFX";
    private const string DatabasePath = "Assets/Script/Data/VFXDatabase.asset";

    [MenuItem("Tools/LoL Swarm/ADR-006/Step 0 - Generate VFXAssets From Legacy Entries")]
    public static void Generate()
    {
        var db = AssetDatabase.LoadAssetAtPath<VFXDatabase>(DatabasePath);
        if (db == null)
        {
            Debug.LogError($"[VFXAssetGenerator] ไม่พบ VFXDatabase ที่ {DatabasePath}");
            return;
        }

        if (db.entries == null || db.entries.Count == 0)
        {
            Debug.LogWarning("[VFXAssetGenerator] VFXDatabase.entries ว่าง — ไม่มีอะไรให้ generate");
            return;
        }

        EnsureFolder(TargetFolder);

        db.assets ??= new List<VFXAsset>();

        int created = 0, reused = 0, skipped = 0;

        for (int i = 0; i < db.entries.Count; i++)
        {
            var entry = db.entries[i];
            if (entry == null || string.IsNullOrEmpty(entry.key))
            {
                Debug.LogWarning($"[VFXAssetGenerator] entries[{i}] ว่าง/ไม่มี key — ข้าม (id {i} จะไม่มี VFXAsset ให้จับคู่)");
                continue;
            }

            // ช่องนี้มีของแล้ว (รันซ้ำ) — ข้ามไปเลย ห้ามสร้างทับ เพราะ id ต้องนิ่ง
            if (i < db.assets.Count && db.assets[i] != null)
            {
                skipped++;
                continue;
            }

            string assetPath = $"{TargetFolder}/{entry.key}.asset";
            VFXAsset asset = AssetDatabase.LoadAssetAtPath<VFXAsset>(assetPath);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<VFXAsset>();
                asset.prefab = entry.prefab;
                asset.poolSize = entry.poolSize;
                asset.designedRadius = entry.designedRadius;
                asset.fixedDuration = entry.fixedDuration;
                asset.category = GuessCategory(entry.key);

                AssetDatabase.CreateAsset(asset, assetPath);
                created++;
            }
            else
            {
                reused++;
            }

            // ต่อท้ายลิสต์ให้ index ตรงกับ i เสมอ — เติมช่องว่างถ้า assets สั้นกว่า i อยู่ (ไม่ควรเกิดปกติ
            // แต่กันไว้เผื่อ entries ถูกแทรกตัวใหม่ก่อนตัวท้ายๆ ระหว่างสอง run)
            while (db.assets.Count < i) db.assets.Add(null);
            if (db.assets.Count == i) db.assets.Add(asset);
            else db.assets[i] = asset;
        }

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[VFXAssetGenerator] เสร็จ — สร้างใหม่ {created} · ใช้ไฟล์เดิม {reused} · ข้าม (มี id นี้แล้ว) {skipped} " +
                  $"· รวม {db.assets.Count} VFXAsset ใน VFXDatabase.assets\n" +
                  $"หมายเหตุ: category เป็นการเดาจากชื่อ key เท่านั้น — designer ต้องมารีวิว/แก้ทีหลัง");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, leaf);
    }

    /// <summary>เดาหมวดหมู่จากชื่อ key — ตัวเดียวเท่าที่มีตอนนี้คือ 23 key ที่รู้จักอยู่แล้วในเกม
    /// key ใหม่ในอนาคตที่ไม่ตรงไหนเลยจะตกไปที่ Weapon (ค่า default ของ VFXAsset)</summary>
    private static VFXCategory GuessCategory(string key)
    {
        switch (key)
        {
            case "EnemyDeath":
                return VFXCategory.Enemy;

            case "OrbPickup":
                return VFXCategory.Environment;

            case "MeteorAoE":
            case "O_AoE_RadiantAura":
            case "PhaseShockwave":
            case "Stormcaller_AOE":
            case "AOE_ThunderRail":
            case "BigAOEBome":
            case "BigAOEBome_chargeTime":
                return VFXCategory.Boss;

            case "HitEffect":
            case "CritHitEffect":
            case "GrenadeExplosion":
            case "OrbiterHit":
            case "SlashHit":
            case "SlashHit2":
            case "VortexFlame":
            case "VortexFlame2":
            case "DashTrail":
            case "Beam_Laser":
            case "Beam_Railgun":
            case "Beam_Lightning":
            case "VFX_Lance":
            case "Beam_ThunderRail":
                return VFXCategory.Weapon;
        }

        // fallback แบบหยาบสำหรับ key ที่เพิ่มเข้ามาทีหลัง — ยังต้องให้ designer รีวิวอยู่ดี
        string k = key.ToLowerInvariant();
        if (k.Contains("boss") || k.Contains("shockwave") || (k.Contains("aoe") && !k.Contains("orb")))
            return VFXCategory.Boss;
        if (k.Contains("death") || k.Contains("enemy"))
            return VFXCategory.Enemy;
        if (k.Contains("pickup"))
            return VFXCategory.Environment;
        if (k.Contains("ui") || k.Contains("hud"))
            return VFXCategory.UI;

        return VFXCategory.Weapon;
    }
}
