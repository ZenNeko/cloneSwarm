using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// จัดการ CHARGE resource ของ Riven
/// — เดินสะสม CHARGE (chargePerUnit ต่อ 1 unit ที่เดิน)
/// — เมื่อ CHARGE ครบ 100 → ยิง BunnyHopWeapon ทันที
/// — ทำงานบน Owner เท่านั้น
/// </summary>
public class ChargeManager : MonoBehaviour, IHUDPassiveBar
{
    [Header("Charge Settings")]
    [Tooltip("CHARGE ที่ได้ต่อ 1 unit ที่เดิน — default 5 = ต้องเดิน 20 units ต่อ 1 fire")]
    public float chargePerUnit = 5f;

    public const float MaxCharge = 100f;

    // ── State ─────────────────────────────────────────────────────────────
    public float CurrentCharge  { get; private set; }
    public int   FireCount      { get; private set; }   // ใช้ตรวจ every-2nd-cast

    /// <summary>Blade of Exile เปิดอยู่ → charge rate ×2</summary>
    public bool IsExileActive   { get; set; }

    // ── IHUDPassiveBar ────────────────────────────────────────────────────
    /// <summary>active เฉพาะเมื่อ Riven (BunnyHopWeapon) อยู่ใน children</summary>
    public bool  IsActivePassive   => GetComponentInChildren<BunnyHopWeapon>() != null;
    public float NormalizedValue   => CurrentCharge / MaxCharge;
    public bool  IsTriggered       => CurrentCharge >= MaxCharge;
    public string BarText         => IsTriggered ? "READY!"
                                   : $"{Mathf.RoundToInt(NormalizedValue * 100)}";
    public UnityEngine.Color BarColor       => IsExileActive
        ? new UnityEngine.Color(1f, 0.55f, 0.1f)   // ส้ม (Exile)
        : new UnityEngine.Color(0.2f, 0.8f, 1f);    // ฟ้า (ปกติ)
    public UnityEngine.Color TriggeredColor => new UnityEngine.Color(1f, 0.9f, 0f); // ทอง

    // ── Events ────────────────────────────────────────────────────────────
    public static event Action<float> OnChargeChanged;  // normalized 0–1

    // ── Internal ──────────────────────────────────────────────────────────
    private Vector3            lastPos;
    private NetworkBehaviour   owner;   // PlayerWeaponManager
    private BunnyHopWeapon     weapon;
    private playermove         move;    // อ่านธง isDashing / isKnockedBack

    // ── Init ──────────────────────────────────────────────────────────────
    void Awake()
    {
        owner = GetComponent<NetworkBehaviour>();
        move  = GetComponent<playermove>();
    }

    void Start()
    {
        lastPos = transform.position;
        // หา BunnyHopWeapon ใน children (อาจยังไม่ Spawn — หาใหม่ทุก 1 วิ)
        InvokeRepeating(nameof(FindWeapon), 0.5f, 1f);
    }

    void FindWeapon() => weapon = GetComponentInChildren<BunnyHopWeapon>();

    // ── Update ────────────────────────────────────────────────────────────
    void Update()
    {
        // ทำงานเฉพาะ Owner
        if (owner != null && !owner.IsOwner) return;

        // CHARGE ต้องมาจาก "การเดิน" เท่านั้น — ระยะที่ระบบอื่นพาตัวไปไม่นับ
        //
        // dash 6 units × rate 20 (ตอน Exile) = 120 charge ซึ่งเกิน MaxCharge 100 ในตัวมันเอง
        // → dash เติมหลอดให้ dash ครั้งถัดไปเต็มพอดี กลายเป็นวนไม่รู้จบทันทีที่กด E
        // knockback ก็เข้าเงื่อนไขเดียวกัน — ระยะที่ผู้เล่นไม่ได้เดินเอง
        //
        // ยังไม่ครอบการวาร์ป: playermove.Respawn เซ็ต transform.position ตรงๆ โดยไม่ตั้งธง
        // ระยะข้ามแมพก้อนเดียวจึงยังเติมหลอดเต็มและยิง BunnyHop ทันทีที่ฟื้น
        //
        // ต้องอัปเดต lastPos ก่อน return ด้วย ไม่งั้นระยะที่ข้ามไปจะถูกนับรวบเป็นก้อน
        // ในเฟรมแรกที่ธงถูกปลด ซึ่งให้ผลเท่ากับไม่ได้กันเลย
        if (move != null && (move.isDashing || move.isKnockedBack))
        {
            lastPos = transform.position;
            return;
        }

        float dist = Vector3.Distance(transform.position, lastPos);
        lastPos    = transform.position;

        if (dist > 0.001f)
        {
            float rate  = chargePerUnit * (IsExileActive ? 2f : 1f);
            CurrentCharge += dist * rate;
            OnChargeChanged?.Invoke(Mathf.Clamp01(CurrentCharge / MaxCharge));
        }

        if (CurrentCharge < MaxCharge) return;

        // เช็กด้วย != ของ Unity ไม่ใช่ ?. ของ C#
        // ?. เทียบ reference null ล้วน ส่วน object ที่ถูก Destroy แล้วไม่ใช่ reference null
        // ReplaceWeapon (อัปเป็น Super) ทำลายอาวุธเก่าทิ้ง แล้ว FindWeapon โพลทุก 1 วินาที
        // จึงมีหน้าต่างที่ weapon ชี้ของที่ตายแล้ว — ?. จะเรียกเมธอดต่อแล้วไปตายที่ transform ข้างใน
        if (weapon != null)
        {
            // ล้างหลอดเฉพาะตอนที่ยิงออกไปจริง — ของเดิมล้างก่อนเช็ก weapon
            // ช่วง 0.5 วิแรกหลัง spawn และหน้าต่างตอนอัปเป็น Super หา weapon ไม่เจอ
            // หลอดที่เต็มจะถูกทิ้งเปล่าโดยไม่มีอะไรฟ้อง ผู้เล่นเดินครบระยะแล้วไม่มีอะไรเกิดขึ้น
            CurrentCharge = 0f;
            FireCount++;
            OnChargeChanged?.Invoke(0f);
            weapon.FireOnCharge(FireCount);
            return;
        }

        // ยังหาอาวุธไม่เจอ — คาหลอดไว้เต็ม รอ FindWeapon รอบถัดไป (InvokeRepeating ทุก 1 วิ)
        // ตั้งใจไม่เรียก FindWeapon ตรงนี้: ตัวละครที่ไม่มี BunnyHop เลยจะเข้าเงื่อนไขนี้ทุกเฟรม
        // (ChargeManager อยู่บน player prefab ที่ทุกตัวละครใช้ร่วมกัน) กลายเป็น
        // GetComponentInChildren รายเฟรมตลอดทั้งรัน
        //
        // clamp ด้วยเหตุผลเดียวกัน — กันค่าโตไม่จำกัดจากการเดินสะสมตลอด 15 นาที
        CurrentCharge = MaxCharge;
    }
}
