using UnityEngine;

[CreateAssetMenu(fileName = "Status_New", menuName = "LoL Swarm/Status Effect")]
public class StatusEffectData : ScriptableObject
{
    [Header("Identity")]
    public string statusId    = "vuln";      // ตัวระบุบนสาย ห้ามซ้ำ
    public string displayName = "Vulnerability";
    public Sprite icon;

    [Header("Timing")]
    public float duration  = 30f;
    public int   maxStacks = 5;
    [Tooltip("ลด stack ทีละ 1 ทุกกี่วินาที — 0 = ไม่ decay ทีละชั้น หมดอายุพร้อมกันทั้งก้อน")]
    public float decayInterval = 0f;

    [Header("Effect")]
    [Tooltip("ตัวคูณดาเมจที่ได้รับ ต่อ 1 stack — 1.1 = แต่ละ stack เพิ่ม 10%")]
    public float damageTakenMultPerStack = 1f;
    [Tooltip("ยิง action นี้ตอนหมดอายุ (Spell-in-Waiting) — ว่างได้")]
    public BossAction onExpire;
}
