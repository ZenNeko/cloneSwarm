#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Project Automated Auditor — เรียกใช้งานผ่าน Unity CLI (-executeMethod ProjectAutomatedAuditor.PerformFullAudit)
/// </summary>
public static class ProjectAutomatedAuditor
{
    public static void PerformFullAudit()
    {
        Debug.Log("[AutomatedAudit] 🔍 Starting Automated Project Audit via Unity CLI...");
        int issueCount = 0;

        // 1. Audit Network Prefabs for missing NetworkObject
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("Network") || path.Contains("Projectile") || path.Contains("Enemy") || path.Contains("Boss"))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.GetComponent<Unity.Netcode.NetworkObject>() == null)
                {
                    Debug.LogWarning($"[AutomatedAudit] ⚠️ Network candidate missing NetworkObject: {path}");
                    issueCount++;
                }
            }
        }

        // 2. Audit ScriptableObjects for null critical fields
        string[] soGuids = AssetDatabase.FindAssets("t:WeaponData", new[] { "Assets" });
        foreach (var guid in soGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (data != null && string.IsNullOrEmpty(data.weaponName))
            {
                Debug.LogWarning($"[AutomatedAudit] ⚠️ WeaponData with empty weaponName: {path}");
                issueCount++;
            }
        }

        Debug.Log($"[AutomatedAudit] ✅ Audit Completed. Total Issues Found: {issueCount}");
    }
}
#endif
