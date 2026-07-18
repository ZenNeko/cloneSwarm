using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// คอมโพเนนต์เสาบัฟโซนที่ประมวลผลบนวัตถุเสาโดยตรง (Prefab-friendly)
/// ทำหน้าที่จับการสัมผัส (OnTriggerEnter/Exit) และประมวลผลการฮีลตรงๆ บน Server
/// </summary>
public class SupportArenaPillar : MonoBehaviour
{
    private SupportArenaWeapon weapon;
    private float dmgBuff;
    private float moveBuff;
    private float hasteBuff;
    private float regenBuff; // เก็บไว้เผื่อความเข้ากันได้
    private bool evo;
    private float shieldTickInterval;
    private float radius;
    private float healAmount;

    private readonly HashSet<playermove> buffedPlayers = new();
    private readonly Dictionary<playermove, int> playerColliderCounts = new(); // ระบบป้องกันบักคอลไลเดอร์ซ้อน (Compound Colliders)
    private readonly Dictionary<playermove, float> lastShieldTime = new();

    private float healTimer = 0f;
    private float shieldTimer = 0f;

    public void Init(SupportArenaWeapon srcWeapon, float dmg, float move, float haste, float regen, bool isEvo, float tickInterval, float rad)
    {
        weapon             = srcWeapon;
        dmgBuff            = dmg;
        moveBuff           = move;
        hasteBuff          = haste;
        regenBuff          = regen;
        evo                = isEvo;
        shieldTickInterval = tickInterval;
        radius             = rad;

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
        bool isServer = weapon != null && weapon.manager != null && weapon.manager.IsServer;
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
                    Debug.Log($"[SupportArena] Server healed player {pm.name} for {healAmount} HP (Current={pm.netHealth.Value}/{pm.maxHealth})");
                }
            }
        }

        // 2. ดำเนินการชาร์จเกราะป้องกัน (Evo Shield Fill) เฉพาะคนที่มี HP เต็มจริงในวง
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
                        pm.AddShield(pm.maxHealth);
                        Debug.Log($"[SupportArena] Server added shield to player {pm.name} for {pm.maxHealth} HP (Full HP reached)");
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

        bool isServer = weapon != null && weapon.manager != null && weapon.manager.IsServer;

        if (pm.IsOwner || isServer)
        {
            // เพิ่มตัวนับคอลไลเดอร์ของตัวละครคนนี้
            if (!playerColliderCounts.TryGetValue(pm, out int count))
            {
                count = 0;
            }
            playerColliderCounts[pm] = count + 1;

            // แอดบัฟดาเมจ ความเร็ว และคูลเดอร์สกิลเมื่ออยู่ในโซน (เฉพาะเมื่อเป็นการก้าวเข้าตัวแรกสุด)
            if (!buffedPlayers.Contains(pm))
            {
                if (weapon != null)
                {
                    weapon.ApplyBuffs(pm, dmgBuff, moveBuff, hasteBuff, regenBuff);
                }
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
                    if (weapon != null)
                    {
                        weapon.RemoveBuffs(pm, dmgBuff, moveBuff, hasteBuff, regenBuff);
                    }
                    buffedPlayers.Remove(pm);
                    lastShieldTime.Remove(pm);
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
            if (pm != null && weapon != null)
            {
                weapon.RemoveBuffs(pm, dmgBuff, moveBuff, hasteBuff, regenBuff);
            }
        }
        buffedPlayers.Clear();
        playerColliderCounts.Clear();
        lastShieldTime.Clear();
    }
}
