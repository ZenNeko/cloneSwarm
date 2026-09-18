using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// แถบไอคอน Augment ที่ผู้เล่นเก็บได้ใน run นี้ (มุมจอ)
///
/// ═══ **ตั้งใจไม่วางลงซีน** — augment ใช้ทางของการ์ดแทน ═══
///
/// augment ไม่ใช่ระบบแยก มันคือ **การ์ดใบหนึ่ง** ที่ได้มาจากทางเฉพาะ (เก็บ orb ที่
/// ตั้ง reward = Augment) · ตอนเลือกใช้จอ `LevelUpUI` ใบเดียว
/// กับการ์ดอื่น ตอนถืออยู่ใช้แถว PASSIVES ของ `BuildStripUI` ใบเดียวกับสเตตัส
///
/// ถ้าแยก HUD ของตัวเองออกมา ระบบที่ทำงานกับ "การ์ดทั้งกอง" ในอนาคต — reroll ·
/// banish · ล็อกใบ — จะต้องเขียนทางแยกให้ augment ทุกครั้ง แล้วสองทางนั้นจะค่อยๆ
/// เพี้ยนออกจากกัน · เคยเกิดมาแล้วกับแถบ build ที่มีสองอัน
///
/// เก็บไฟล์ไว้เพราะมันไม่ผิด แค่ไม่ใช่ทางที่เลือก — ถ้าวันหนึ่งต้องการแถบไอคอนจริงๆ
/// (เช่นโชว์ augment ของ **เพื่อนร่วมทีม** ซึ่งทางการ์ดให้ไม่ได้) ตัวนี้พร้อมใช้
///
/// Setup:
///   1. สร้าง empty GameObject ใต้ Canvas ของ gameplay scene ชื่อ AugmentBar
///   2. ใส่ Horizontal Layout Group + component นี้
///   3. iconTemplate → Image prefab เล็กๆ (SetActive = false ไว้)
///
/// ไม่ต้อง assign อะไรเพิ่ม — subscribe static event ของ PlayerAugmentManager เอง
/// </summary>
public class AugmentHUDUI : MonoBehaviour
{
    [Header("Icons")]
    [Tooltip("Image template สำหรับไอคอน 1 อัน — SetActive = false ไว้")]
    public Image iconTemplate;
    [Tooltip("สีพื้นของไอคอน — augment ไม่มีระดับแล้ว จึงเป็นสีเดียวทั้งหมด")]
    public Color augmentColor = new Color(0.70f, 0.35f, 0.95f);

    readonly List<Image> spawned = new();

    void Awake()
    {
        if (iconTemplate != null) iconTemplate.gameObject.SetActive(false);
    }

    void OnEnable()  => PlayerAugmentManager.OnAugmentAcquired += HandleAcquired;
    void OnDisable() => PlayerAugmentManager.OnAugmentAcquired -= HandleAcquired;

    void HandleAcquired(AugmentData a)
    {
        if (a == null || iconTemplate == null) return;

        var img = Instantiate(iconTemplate, iconTemplate.transform.parent);
        img.gameObject.SetActive(true);
        img.name    = $"Aug_{a.augmentId}";
        img.sprite  = a.icon;
        img.enabled = true;
        img.color = augmentColor;

        // ไม่มีไอคอน → ยังเห็นสีประจำ augment เป็น placeholder
        spawned.Add(img);
    }
}
