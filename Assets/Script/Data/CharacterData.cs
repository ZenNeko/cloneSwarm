using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// ข้อมูลตัวละคร — Assets > Create > LoL Swarm > Character Data
/// </summary>
[CreateAssetMenu(fileName = "Char_New", menuName = "LoL Swarm/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Identity — ห้ามแปล ห้ามเปลี่ยนหลังปล่อยเกม")]
    [Tooltip("รหัสประจำตัวละคร ไม่ใช่ชื่อที่โชว์บนจอ — ใช้เป็นคีย์จริงในสามที่:\n" +
             "  1. บนเน็ตเวิร์ก (LobbyState.characterName, CLAUDE.md ข้อ 10)\n" +
             "  2. คีย์ในไฟล์เซฟ (SaveData / MetaProgression)\n" +
             "  3. dimension ของ analytics\n" +
             "แปลเมื่อไหร่ = host กับ client ส่งคนละสตริง, ของที่ปลดล็อกไว้หาย, " +
             "และข้อมูล analytics ถูกแยกตามภาษา · ชื่อที่โชว์ให้ใส่ displayName ข้างล่าง")]
    public string characterName = "Unnamed";

    [Header("Display — แปลได้ ชี้ไป String Table 'Content'")]
    [Tooltip("ว่าง = ใช้ characterName แทน · ตั้งอัตโนมัติด้วย Tools > Clone Swarm > Localization > 2. Relink")]
    public LocalizedString displayName;
    public LocalizedString description;
    public Sprite portrait;
    public Sprite icon;

    /// <summary>ชื่อที่เอาไปโชว์บนจอ — ตกกลับไปใช้ characterName เมื่อยังไม่ได้ตั้ง displayName
    ///
    /// ทุกที่ที่เอาชื่อไปแสดงต้องอ่านตัวนี้ ห้ามอ่าน characterName ตรงๆ
    /// (จุดที่เทียบค่า/ส่งขึ้นเน็ตเวิร์ก/เขียนลงเซฟ ยังต้องใช้ characterName เหมือนเดิม)</summary>
    public string DisplayName => displayName.IsEmpty ? characterName : displayName.GetLocalizedString();
    public string Description  => description.IsEmpty ? "" : description.GetLocalizedString();
    public string PassiveName  => passiveName.IsEmpty ? "" : passiveName.GetLocalizedString();
    public string PassiveDesc  => passiveDescription.IsEmpty ? "" : passiveDescription.GetLocalizedString();

    [Header("Base Stats")]
    public float baseHealth    = 100f;
    public float baseMoveSpeed = 5f;

    // ── Display-only stats ────────────────────────────────────────────────
    // สี่ค่านี้ยังไม่ถูกต่อสายเข้า gameplay — playermove อ่านแค่ baseHealth/baseMoveSpeed
    // และ PlayerStatManager คืนค่า crit/armor จากการอัปเกรดในรันเท่านั้น (ฐาน = 0)
    // ตอนนี้ใช้แสดงบนแผง stat ของหน้าเลือกตัวละคร เพื่อให้เทียบตัวละครกันได้
    // ถ้าจะให้มีผลจริงในเกม ต้องต่อสายแยกรอบ
    [Header("Display Stats (หน้าเลือกตัวละคร — ยังไม่มีผลในเกม)")]
    [Tooltip("พลังโจมตีฐานที่โชว์บนแผง stat")]
    public float baseAttack     = 0f;
    [Tooltip("ค่าป้องกันฐานที่โชว์บนแผง stat")]
    public float baseDefense    = 0f;
    [Tooltip("โอกาสคริฐาน 0-1 (0.05 = 5%)")]
    [Range(0f, 1f)]
    public float baseCritRate   = 0.05f;
    [Tooltip("ดาเมจคริเพิ่มจากปกติ (0.5 = +50%)")]
    public float baseCritDamage = 0.5f;

    // ─────────────────────────────────────────────────────────────────────
    [Header("Meta Unlock")]
    [Tooltip("ติ๊ก = เล่นได้ตั้งแต่แรก ไม่ต้องซื้อ (ควรมีอย่างน้อย 1 ตัว)")]
    public bool unlockedByDefault = false;
    [Tooltip("ราคาทองที่ต้องจ่ายเพื่อปลดล็อก — ใช้เมื่อ unlockedByDefault ไม่ติ๊ก")]
    public int  unlockCost = 1000;

    [Header("Visual Model")]
    [Tooltip("Prefab โมเดลตัวละคร (mesh + animator + materials)\n" +
             "Spawn เป็น child ของ PlayerVisual.modelHolder บนทุก client\n" +
             "Animator ควรมี params: IsMoving (bool), IsDead (bool), Attack (trigger)")]
    public GameObject characterModelPrefab;

    [Header("Starting Weapon")]
    public WeaponData startingWeapon;

    [Header("Passive Weapons (ไม่นับ Weapon Slot)")]
    [Tooltip("Passive scripts เช่น HunterPassiveWeapon, GunnerPassiveWeapon\n" +
             "Spawn เป็น child player แต่ไม่นับ Weapon Slot และไม่แสดงใน Weapon UI")]
    [UnityEngine.Serialization.FormerlySerializedAs("additionalWeapons")]
    public WeaponData[] passiveWeapons;

    [Header("Abilities (Q / E / R)")]
    [Tooltip("Ability ของ character — ใช้ AbilityData (ไม่ใช่ WeaponData)\n" +
             "บริหารโดย PlayerAbilityManager ไม่นับ Weapon Slot\n" +
             "เช่น Riven: ใส่ Valor (Q) และ Blade of Exile (E) ที่นี่")]
    public AbilityData[] abilities;

    // ─────────────────────────────────────────────────────────────────────
    [Header("Passive — ข้อความแปลได้ ชี้ไป String Table 'Content'")]
    public Sprite          passiveIcon;
    public LocalizedString passiveName;
    public LocalizedString passiveDescription;
}
