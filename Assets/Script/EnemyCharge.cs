using System.Collections;
using UnityEngine;

/// <summary>
/// Add-on behaviour: enemy ชาร์จพุ่งเข้าหา player ทุก chargeCooldown วินาที
/// ต้องแนบกับ GameObject ที่มี Enemy.cs อยู่ด้วย
/// Server-only logic
/// </summary>
[RequireComponent(typeof(Enemy))]
public class EnemyCharge : MonoBehaviour
{
    [Header("Charge Config")]
    [Tooltip("เวลารอระหว่างชาร์จแต่ละครั้ง (วินาที)")]
    public float chargeCooldown = 4f;
    [Tooltip("ตัวคูณความเร็วขณะชาร์จ")]
    public float chargeSpeedMult = 4f;
    [Tooltip("ระยะเวลาชาร์จ (วินาที)")]
    public float chargeDuration = 0.4f;

    private Enemy   enemy;
    private bool    isServer;
    private bool    charging;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
    }

    void Start()
    {
        // ตรวจสอบว่าเป็น server จาก Enemy.IsServer (NetworkBehaviour)
        // ใช้ InvokeRepeating หลังจาก spawn ไม่นาน
        isServer = enemy != null && enemy.IsServer;
        if (!isServer) return;

        InvokeRepeating(nameof(TriggerCharge), chargeCooldown, chargeCooldown);
    }

    void TriggerCharge()
    {
        if (!isServer || charging) return;
        StartCoroutine(ChargeSequence());
    }

    IEnumerator ChargeSequence()
    {
        charging = true;
        float originalSpeed = enemy.speed;
        enemy.speed = originalSpeed * chargeSpeedMult;

        yield return new WaitForSeconds(chargeDuration);

        enemy.speed = originalSpeed;
        charging    = false;
    }
}
