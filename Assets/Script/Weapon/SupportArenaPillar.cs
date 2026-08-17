using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// คอมโพเนนต์เสาบัฟโซนที่ประมวลผลบนวัตถุเสาโดยตรง (Prefab-friendly)
/// ทำหน้าที่จับการสัมผัส (OnTriggerEnter/Exit) และประมวลผลการฮีลตรงๆ บน Server
///
/// หมายเหตุ (ADR-008 D4): เสาไม่ถือ reference ไปยัง SupportArenaWeapon อีกต่อไป
/// อาวุธเป็นคอมโพเนนต์บนลูกของผู้เล่นและอาจถูก Destroy ทิ้งระหว่างเสายังไม่หมดอายุ
/// (เช่นตอนอัปเกรดเป็น Super ผ่าน PlayerWeaponManager.ReplaceWeapon) เสาจึงต้องพกค่าที่ต้องใช้ทั้งหมด
/// มาเองตั้งแต่ Init และ apply/remove บัฟด้วยตัวเอง เพื่อไม่ให้ค้างบัฟถาวรถ้าอาวุธตายก่อน
/// </summary>
public class SupportArenaPillar : MonoBehaviour
{
    private float dmgBuff;
    private float moveBuff;
    private float hasteBuff;
    private float regenBuff;
    private bool evo;
    private float shieldTickInterval;
    private float radius;
    private float healAmount;
    private bool isServer;

    private readonly HashSet<playermove> buffedPlayers = new();
    private readonly Dictionary<playermove, int> playerColliderCounts = new(); // ระบบป้องกันบักคอลไลเดอร์ซ้อน (Compound Colliders)

    private float healTimer = 0f;
    private float shieldTimer = 0f;

    /// <summary>
    /// isServerAuthority ถูกส่งเข้ามาตรงๆ จากผู้เรียก (แทนการอ่านผ่าน weapon.manager.IsServer)
    /// เพราะเสาต้องรอดแม้ NetworkBehaviour ต้นทางจะถูกทำลายไปแล้วก็ตาม
    /// </summary>
    public void Init(float dmg, float move, float haste, float regen, bool isEvo, float tickInterval, float rad, bool isServerAuthority)
    {
        dmgBuff            = dmg;
        moveBuff           = move;
        hasteBuff          = haste;
        regenBuff          = regen;
        evo                = isEvo;
        shieldTickInterval = tickInterval;
        radius             = rad;
        isServer           = isServerAuthority;

        // 1. ตรวจเช็คว่ามี SphereCollider หรือไม่
        var col = GetComponent<SphereCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<SphereCollider>();
        }
        col.isTrigger = true;

        // หารล้างสเกลของโมเดลแม่เพื่อให้ได้รัศมีจริงในโลกเกมตามค่าของ WD
        float parentScale = transform.lossyScale.x;
        if (parentScale <= 0f) parentScale = 1f;
        col.radius = radius / parentScale;

        // 2. ตรวจเช็คว่ามี Rigidbody หรือไม่
        var rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity  = false;

        // 3. ปรับขนาดวงแหวนบ่งชี้อาณาเขตขอบเขตระนาบ Y
        var zoneTrans = transform.Find("Zone");
        if (zoneTrans != null)
        {
            zoneTrans.localScale = new Vector3(radius * 2f, zoneTrans.localScale.y, radius * 2f);
        }
    }

    public void SetHealAmount(float amount)
    {
        healAmount = amount;
    }

    private void Update()
    {
        if (!isServer) return; // ทำการฮีล/แอดเกราะบน Server เท่านั้นเพื่อความเสถียรสูงสุดและป้องกันแล็ก

        // 1. วนตรวจและฟื้นฟูเลือดผู้เล่นทุกคนในวงตรงๆ (ฮีลทุกๆ 1 วินาทีคงที่)
        healTimer += Time.deltaTime;
        if (healTimer >= 1.0f)
        {
            healTimer = 0f;
            foreach (var pm in buffedPlayers)
            {
                if (pm != null && !pm.isDead.Value && pm.netHealth.Value < pm.maxHealth)
                {
                    pm.Heal(healAmount);
                }
            }
        }

        // 2. ดำเนินการชาร์จเกราะป้องกัน (Evo Shield Fill) เฉพาะคนที่มี HP เต็มจริงในวง
        //
        // ADR-008 D5 — ใช้ RefreshShield ไม่ใช่ AddShield
        // ของเดิมเรียก AddShield ทุกทิก ซึ่งเติมชั้นใหม่ทับซ้อนไปเรื่อยๆ แล้วยังรีเซ็ตนาฬิกาสลาย
        // ของยอดสะสมทั้งก้อนด้วย ผลคือยืนในวงนานๆ แล้วโล่ลู่เข้า 3 เท่าของเลือดสูงสุดและไม่มีวันสลาย
        // ตอนนี้เป็นชั้นเดียวที่ถูกต่ออายุเรื่อยๆ ขณะยังยืนอยู่ในวง และสลายตามปกติเมื่อเดินออก
        // (โมเดลเดียวกับโล่ของ TFT — ดูตารางเทียบใน ADR-008)
        //
        // ขนาดยังเป็น pm.maxHealth เท่าเดิม ยังไม่แตะ — เป็นข้อบาลานซ์ที่เจ้าของยังไม่ได้ตัดสิน
        if (evo)
        {
            shieldTimer += Time.deltaTime;
            if (shieldTimer >= shieldTickInterval)
            {
                shieldTimer = 0f;
                foreach (var pm in buffedPlayers)
                {
                    if (pm != null && !pm.isDead.Value && pm.netHealth.Value >= pm.maxHealth)
                    {
                        pm.RefreshShield(ShieldSourceId.SupportArena, pm.maxHealth);
                    }
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // ตรวจหา playermove จาก Collider นั้นๆ หรือจาก Parent Object (เพื่อรองรับคอลไลเดอร์ย่อยในโมเดล)
        var pm = other.GetComponent<playermove>() ?? other.GetComponentInParent<playermove>();
        if (pm == null || pm.isDead.Value) return;

        if (pm.IsOwner || isServer)
        {
            // เพิ่มตัวนับคอลไลเดอร์ของตัวละครคนนี้
            if (!playerColliderCounts.TryGetValue(pm, out int count))
            {
                count = 0;
            }
            playerColliderCounts[pm] = count + 1;

            // แอดบัฟดาเมจ ความเร็ว และคูลเดอร์สกิลเมื่ออยู่ในโซน (เฉพาะเมื่อเป็นการก้าวเข้าตัวแรกสุด)
            // การ apply บัฟกับการบันทึกลง buffedPlayers ต้องอยู่ในบล็อกเดียวกันเสมอ ห้ามแยกกัน
            // ไม่งั้นจะเกิดเคสที่ถูกนับว่าได้บัฟแล้วทั้งที่ไม่เคยได้จริง (บั๊กเดิมของ ADR-008)
            if (!buffedPlayers.Contains(pm))
            {
                ApplyBuffsToPlayer(pm);
                buffedPlayers.Add(pm);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var pm = other.GetComponent<playermove>() ?? other.GetComponentInParent<playermove>();
        if (pm == null) return;

        if (playerColliderCounts.TryGetValue(pm, out int count))
        {
            count--;
            if (count <= 0)
            {
                // เมื่อคอลไลเดอร์ทุกตัวของตัวละครหลุดออกจากวงไปแล้วจริงๆ ค่อยลบออกและล้างบัฟ
                playerColliderCounts.Remove(pm);
                if (buffedPlayers.Contains(pm))
                {
                    RemoveBuffsFromPlayer(pm);
                    buffedPlayers.Remove(pm);
                }
            }
            else
            {
                playerColliderCounts[pm] = count;
            }
        }
    }

    private void OnDestroy()
    {
        // ล้างบัฟผู้เล่นทุกคนคืนสู่ปกติเมื่อเสาหมดอายุ
        foreach (var pm in buffedPlayers)
        {
            if (pm != null)
            {
                RemoveBuffsFromPlayer(pm);
            }
        }
        buffedPlayers.Clear();
        playerColliderCounts.Clear();
    }

    private void ApplyBuffsToPlayer(playermove pm)
    {
        if (pm == null) return;

        pm.tempMoveSpeedBonus     += moveBuff;
        // playermove.Update() รวมค่านี้กับ healthRegenPerSecond แล้วฟื้นเลือดจริงฝั่ง server
        // สำเนาฝั่ง owner ก็ถูกบวกด้วยแต่ไม่มีผล เพราะการฟื้นเลือดถูก gate ด้วย IsServer อยู่แล้ว
        pm.tempHealthRegenBonus   += regenBuff;

        var sm = pm.GetComponent<PlayerStatManager>();
        if (sm != null)
        {
            sm.tempDamageBonusMult += dmgBuff;
            sm.tempAbilityHaste   += hasteBuff;
        }
    }

    private void RemoveBuffsFromPlayer(playermove pm)
    {
        if (pm == null) return;

        pm.tempMoveSpeedBonus     -= moveBuff;
        pm.tempHealthRegenBonus   -= regenBuff;

        var sm = pm.GetComponent<PlayerStatManager>();
        if (sm != null)
        {
            sm.tempDamageBonusMult -= dmgBuff;
            sm.tempAbilityHaste   -= hasteBuff;
        }
    }
}
