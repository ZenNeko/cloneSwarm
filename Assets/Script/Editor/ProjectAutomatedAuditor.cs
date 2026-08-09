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
        // เดิมเช็คจากชื่อ path ("Network"/"Projectile"/"Enemy"/"Boss") ซึ่งจับ asset pack
        // ของคนอื่นทั้งกอง — 229 คำเตือน ไม่มีอันไหนจริงสักอัน
        // บั๊กจริงหน้าตาแบบนี้: มีสคริปต์ NetworkBehaviour อยู่ แต่หา NetworkObject
        // บนตัวเองหรือ parent ไม่เจอ → NGO ปฏิเสธ spawn เงียบๆ
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            foreach (var nb in prefab.GetComponentsInChildren<Unity.Netcode.NetworkBehaviour>(true))
            {
                if (nb.GetComponentInParent<Unity.Netcode.NetworkObject>(true) != null) continue;

                Debug.LogWarning($"[AutomatedAudit] ⚠️ {nb.GetType().Name} on prefab but NetworkObject is missing on it or any parent: {path}");
                issueCount++;
                break;   // รายงาน prefab ละครั้งพอ
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
