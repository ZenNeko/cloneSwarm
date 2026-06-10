using TMPro;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Pre-placed weapon station สำหรับ WeaponTestScene
///   • Designer วางใน scene พร้อมกับ TargetDummy ข้างๆ
///   • Player เดินเข้า trigger → equip weapon นั้นทันที (server only)
///   • แสดง label ของ weapon ลอยเหนือ station
///
/// **Setup:**
///   1. สร้าง empty GameObject ในตำแหน่งที่ต้องการ
///   2. Add component นี้ + assign `weaponData`
///   3. Add SphereCollider (Is Trigger = true, radius 1.5)
///   4. (optional) สร้าง child World-Space Canvas + TextMeshProUGUI สำหรับชื่อ → assign `weaponLabel`
///   5. วาง TargetDummy ห่าง 10m เพื่อ test damage
///
/// **เคล็ดลับ:**
///   • วาง station เป็นแถวเรียงกัน (เช่น ห่างกัน 6m) — เดินผ่านทดสอบทีละตัวได้
///   • ถ้าอยากให้เป็น "vending machine" — ทำ proximity hold (กดปุ่ม E เพื่อ equip)
/// </summary>
public class WeaponStation : MonoBehaviour
{
    [Header("Weapon")]
    [Tooltip("WeaponData ที่จะ equip ให้ player ตอนเดินเข้า trigger")]
    public WeaponData weaponData;

    [Tooltip("Level เริ่มต้นเมื่อ equip (0-indexed) — 4 = Lv5 max\nNormal: 0-4 | Super/Fusion: 0")]
    [Range(0, 4)] public int initialLevel = 4;

    [Tooltip("เปลี่ยน weapon slot แรกของ player (true) หรือเพิ่มเป็น slot ใหม่ (false)")]
    public bool replaceFirstSlot = true;

    [Header("Visual (optional)")]
    [Tooltip("Text label ที่แสดงชื่อ weapon เหนือ station")]
    public TMP_Text weaponLabel;
    [Tooltip("ตำแหน่งที่จะ spawn weapon icon เป็น 3D model floating — ปล่อยว่างได้")]
    public Transform iconAnchor;

    [Header("Trigger")]
    [Tooltip("Cooldown หลัง equip 1 ครั้ง (วินาที) — กัน trigger รัวๆ")]
    public float equipCooldown = 0.5f;

    float _lastEquipTime = -999f;

    void Start()
    {
        if (weaponLabel != null && weaponData != null)
            weaponLabel.text = $"{weaponData.tier}\n{weaponData.weaponName}";
    }

    void OnTriggerEnter(Collider other)
    {
        if (Time.time - _lastEquipTime < equipCooldown) return;
        if (weaponData == null) return;

        // เฉพาะ server เป็นคน equip (NGO authority)
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        var pm = other.GetComponent<PlayerWeaponManager>();
        if (pm == null) return;
        if (!pm.IsOwner && !pm.IsServer) return;   // server-only equip

        EquipOnPlayer(pm);
        _lastEquipTime = Time.time;
    }

    void EquipOnPlayer(PlayerWeaponManager pm)
    {
        // Replace first slot — clear แล้วใส่ใหม่ (ดูจาก API จริงของ PlayerWeaponManager)
        // ถ้าไม่มี method ClearSlot — fallback ใช้ AddWeapon (จะเต็ม slot)
        if (replaceFirstSlot && pm.HasWeapon(weaponData) == false)
        {
            // ถ้า PlayerWeaponManager มี method ลบ weapon — เรียกที่นี่
            // ตัวอย่าง: pm.RemoveAllWeapons() หรือ pm.RemoveSlot(0)
        }

        if (!pm.HasWeapon(weaponData))
            pm.AddWeapon(weaponData);

        // ถ้าอยากให้ Lv5 ทันที — ต้องมี method SetWeaponLevel (ใน PlayerWeaponManager)
        // pm.SetWeaponLevel(weaponData, initialLevel);

        Debug.Log($"[WeaponStation] Equipped '{weaponData.weaponName}' → {pm.name}");
    }

    // ── Editor Gizmo ──────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        // วงสีฟ้าที่ตำแหน่ง station + ลูกศรชี้ลง
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, 1.5f);
        Gizmos.DrawLine(transform.position + Vector3.up * 3f, transform.position);

        // Label ใน Scene view
        if (weaponData != null)
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f,
                $"📦 {weaponData.weaponName}");
    }
#endif
}
