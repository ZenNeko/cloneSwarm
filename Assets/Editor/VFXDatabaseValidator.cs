using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ตรวจ VFXDatabase.assets (ADR-006) — id เสถียรมาจาก index ในลิสต์เท่านั้น ดังนั้นช่องว่าง (null)
/// หรือ VFXAsset ตัวเดียวกันถูกลากซ้ำหลาย index จะทำให้ id ผิดเงียบๆ compiler ช่วยไม่ได้
///
/// ใช้สองทาง:
///   1. เปิด VFXDatabase asset ใน Inspector — เห็นคำเตือนทันทีถ้ามีปัญหา
///   2. เมนู Tools/LoL Swarm/Validate VFX Database — ตรวจทุก VFXDatabase ในโปรเจกต์ + log ละเอียด
/// </summary>
[CustomEditor(typeof(VFXDatabase))]
public class VFXDatabaseValidator : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var db = (VFXDatabase)target;
        var issues = Validate(db);

        EditorGUILayout.Space();
        if (issues.Count == 0)
        {
            EditorGUILayout.HelpBox("VFXAsset list (ADR-006) ผ่านการตรวจแล้ว — ไม่มีช่องว่างหรือรายการซ้ำ", MessageType.Info);
            return;
        }

        foreach (var issue in issues)
            EditorGUILayout.HelpBox(issue, MessageType.Error);
    }

    [MenuItem("Tools/LoL Swarm/Validate VFX Database")]
    public static void ValidateAllMenuItem()
    {
        string[] guids = AssetDatabase.FindAssets("t:VFXDatabase");
        if (guids.Length == 0)
        {
            Debug.Log("[VFXDatabaseValidator] ไม่พบ VFXDatabase asset ในโปรเจกต์");
            return;
        }

        int totalIssues = 0;
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var db = AssetDatabase.LoadAssetAtPath<VFXDatabase>(path);
            if (db == null) continue;

            var issues = Validate(db);
            totalIssues += issues.Count;
            foreach (var issue in issues)
                Debug.LogError($"[VFXDatabaseValidator] {path}: {issue}", db);
        }

        if (totalIssues == 0)
            Debug.Log($"[VFXDatabaseValidator] ตรวจ {guids.Length} VFXDatabase — ผ่านหมด ไม่มีช่องว่างหรือรายการซ้ำ");
        else
            Debug.LogWarning($"[VFXDatabaseValidator] พบ {totalIssues} ปัญหารวม — ดู error ด้านบนสำหรับตำแหน่ง");
    }

    /// <summary>คืนรายการปัญหาของ VFXDatabase.assets ตัวเดียว (ช่องว่าง + รายการซ้ำ)</summary>
    static List<string> Validate(VFXDatabase db)
    {
        var issues = new List<string>();
        if (db == null || db.assets == null) return issues;

        for (int i = 0; i < db.assets.Count; i++)
        {
            if (db.assets[i] == null)
                issues.Add($"index {i} เป็นช่องว่าง (null) — id {i} จะไม่มี VFX ให้เล่น");
        }

        var seen = new Dictionary<VFXAsset, List<int>>();
        for (int i = 0; i < db.assets.Count; i++)
        {
            var a = db.assets[i];
            if (a == null) continue;
            if (!seen.TryGetValue(a, out var indices))
            {
                indices = new List<int>();
                seen[a] = indices;
            }
            indices.Add(i);
        }
        foreach (var kv in seen)
        {
            if (kv.Value.Count > 1)
                issues.Add($"VFXAsset '{kv.Key.name}' ถูกลากซ้ำที่ index [{string.Join(", ", kv.Value)}] — เลือกให้เหลือ index เดียว ที่เหลือลบทิ้ง");
        }

        return issues;
    }
}
