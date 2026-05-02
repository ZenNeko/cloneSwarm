using UnityEngine;

/// <summary>
/// Singleton registry — เก็บ EliteModifierDef ทั้งหมดที่มีในเกม
///
/// ใช้สำหรับ network sync: แทนที่จะส่ง def reference ผ่าน RPC (ทำไม่ได้)
/// ส่งเป็น index แล้ว client ดึง def จาก registry นี้
///
/// Setup:
///   วาง EliteRegistry GameObject ใน scene
///   ลาก EliteModifierDef assets ทั้งหมดใส่ allDefs[]
/// </summary>
public class EliteRegistry : MonoBehaviour
{
    public static EliteRegistry Instance { get; private set; }

    [Header("Defs (ลำดับนี้ใช้เป็น index สำหรับ network sync)")]
    public EliteModifierDef[] allDefs;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public EliteModifierDef GetById(int index)
    {
        if (allDefs == null || index < 0 || index >= allDefs.Length) return null;
        return allDefs[index];
    }

    public int GetIndex(EliteModifierDef def)
    {
        if (allDefs == null || def == null) return -1;
        for (int i = 0; i < allDefs.Length; i++)
            if (allDefs[i] == def) return i;
        return -1;
    }

    public int Count => allDefs != null ? allDefs.Length : 0;
}
