using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// จัดการ CHARGE resource ของ Riven
/// — เดินสะสม CHARGE (chargePerUnit ต่อ 1 unit ที่เดิน)
/// — เมื่อ CHARGE ครบ 100 → ยิง BunnyHopWeapon ทันที
/// — ทำงานบน Owner เท่านั้น
/// </summary>
public class ChargeManager : MonoBehaviour
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

    // ── Events ────────────────────────────────────────────────────────────
    public static event Action<float> OnChargeChanged;  // normalized 0–1

    // ── Internal ──────────────────────────────────────────────────────────
    private Vector3            lastPos;
    private NetworkBehaviour   owner;   // PlayerWeaponManager
    private BunnyHopWeapon     weapon;

    // ── Init ──────────────────────────────────────────────────────────────
    void Awake()
    {
        owner = GetComponent<NetworkBehaviour>();
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

        float dist = Vector3.Distance(transform.position, lastPos);
        lastPos    = transform.position;

        if (dist > 0.001f)
        {
            float rate  = chargePerUnit * (IsExileActive ? 2f : 1f);
            CurrentCharge += dist * rate;
            OnChargeChanged?.Invoke(Mathf.Clamp01(CurrentCharge / MaxCharge));
        }

        if (CurrentCharge >= MaxCharge)
        {
            CurrentCharge = 0f;
            FireCount++;
            OnChargeChanged?.Invoke(0f);
            weapon?.FireOnCharge(FireCount);
        }
    }
}
