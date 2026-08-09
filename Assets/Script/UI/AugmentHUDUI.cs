using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// แถบไอคอน Augment ที่ผู้เล่นเก็บได้ใน run นี้ (มุมจอ)
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
    [Tooltip("แสดงกรอบสีตาม rarity ของ augment")]
    public bool  tintByRarity = true;

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
        if (tintByRarity) img.color = a.RarityColor;

        // ไม่มีไอคอน → ยังเห็นสีประจำ rarity เป็น placeholder
        spawned.Add(img);
    }
}
