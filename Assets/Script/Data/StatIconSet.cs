using System;
using UnityEngine;

/// <summary>
/// ไอคอนประจำ <see cref="StatType"/> — **แหล่งเดียวของทั้งเกม**
///
/// ═══ ทำไมต้องมี ═══
///
/// "ดาเมจ" โผล่อยู่สามที่ที่คนละระบบถือข้อมูลกันเอง —
///   `StatData` (การ์ดอัปเกรดตอนเล่น) · `TalentData` (ร้านอัปเกรดถาวร) · แผงสเตตัสของผู้เล่น
/// แต่ละที่เคยมีช่อง `icon` ของตัวเอง แปลว่าต้องลากรูปเดียวกันใส่สามรอบ
/// และพอเปลี่ยนรูปทีหลังก็ต้องจำให้ครบทั้งสามที่ · ลืมที่ใดที่หนึ่ง ผู้เล่นจะเห็น
/// ไอคอนคนละแบบสำหรับของอย่างเดียวกัน ซึ่งอ่านเหมือนเป็นคนละของ
///
/// ที่นี่เก็บรูปไว้ชุดเดียว คีย์ด้วยชนิดสเตตัส · asset ไหนอยากใช้รูปของตัวเองก็ยังใส่
/// ช่อง `icon` ของมันได้ ตัวนั้นชนะเสมอ (ดู `TalentData.Icon` / `StatData.Icon`)
/// ใช้กับ talent ที่ไม่ใช่สเตตัสอย่าง Gold Find กับ Second Chance ซึ่งไม่มี StatType ให้อ้าง
///
/// ═══ วางไว้ไหน ═══
///
/// ต้องอยู่ที่ <c>Assets/ScriptableObjects/Resources/StatIcons.asset</c> เป๊ะ —
/// โหลดผ่าน <c>Resources.Load</c> จึงไม่ต้องมีใครลาก reference ให้ และใช้ได้จาก
/// ทั้งฝั่งเกมและฝั่ง meta โดยไม่ต้องพึ่ง singleton ที่ต้อง spawn ก่อน
///
/// ไม่มีไฟล์นี้ = ทุกอย่างยังทำงานปกติ แค่ตกไปใช้ `icon` ของแต่ละ asset เหมือนเดิม
/// </summary>
[CreateAssetMenu(fileName = "StatIcons", menuName = "LoL Swarm/Stat Icon Set")]
public class StatIconSet : ScriptableObject
{
    /// <summary>ชื่อไฟล์ใต้ Resources — เปลี่ยนแล้วต้องย้ายไฟล์ตาม</summary>
    public const string ResourceName = "StatIcons";

    [Serializable]
    public class Entry
    {
        public StatType type;
        public Sprite   icon;
        [Tooltip("สีพื้นของช่องเมื่อยังไม่มีรูป — ไม่ได้ย้อมตัวรูป")]
        public Color    placeholderTint = Color.white;
    }

    [Tooltip("หนึ่งแถวต่อหนึ่ง StatType — ปล่อยรูปว่างได้ ระบบจะตกไปใช้ icon ของ asset นั้นแทน")]
    public Entry[] entries = Array.Empty<Entry>();

    // ── การเข้าถึง ────────────────────────────────────────────────────────
    private static StatIconSet cached;
    private static bool        searched;

    /// <summary>
    /// คืน null ได้ — ยังไม่ได้สร้าง asset ก็ยังเล่นเกมได้ ทุกที่ตกไปใช้ icon เดิม
    /// เตือนครั้งเดียวพอ ไม่งั้นจะท่วม console ทุกครั้งที่วาดการ์ดใหม่
    /// </summary>
    public static StatIconSet Instance
    {
        get
        {
            if (cached != null) return cached;
            if (searched) return null;

            searched = true;
            cached = Resources.Load<StatIconSet>(ResourceName);
            if (cached == null)
                Debug.LogWarning(
                    $"[StatIcons] ไม่พบ Resources/{ResourceName} — ไอคอนสเตตัสจะใช้ของที่ใส่ไว้ใน " +
                    "แต่ละ asset แทน · สร้างด้วย Assets > Create > LoL Swarm > Stat Icon Set " +
                    "แล้ววางที่ Assets/ScriptableObjects/Resources/");
            return cached;
        }
    }

    /// <summary>รูปประจำสเตตัสนี้ — null เมื่อยังไม่ได้ใส่ หรือยังไม่มี asset</summary>
    public static Sprite For(StatType type)
    {
        var set = Instance;
        if (set == null || set.entries == null) return null;

        foreach (var e in set.entries)
            if (e != null && e.type == type) return e.icon;
        return null;
    }

    /// <summary>สีพื้นที่ใช้แทนรูปเมื่อยังไม่มีรูป — ขาวเมื่อไม่ได้ตั้ง</summary>
    public static Color PlaceholderTint(StatType type)
    {
        var set = Instance;
        if (set == null || set.entries == null) return Color.white;

        foreach (var e in set.entries)
            if (e != null && e.type == type) return e.placeholderTint;
        return Color.white;
    }

#if UNITY_EDITOR
    /// <summary>เติมแถวให้ครบทุก StatType โดยไม่ทับรูปที่ใส่ไว้แล้ว</summary>
    [ContextMenu("เติมแถวให้ครบทุก StatType")]
    private void FillMissingTypes()
    {
        var list = new System.Collections.Generic.List<Entry>(entries ?? Array.Empty<Entry>());
        foreach (StatType t in Enum.GetValues(typeof(StatType)))
            if (!list.Exists(e => e != null && e.type == t))
                list.Add(new Entry { type = t });

        entries = list.ToArray();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
