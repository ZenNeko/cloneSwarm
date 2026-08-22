using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Valor — Riven's Q ability  (AbilityBase — ไม่ใช่ WeaponBase)
/// กด Q → Dash ไปในทิศที่เล็ง ลงจอดแล้วทุบพื้น สร้างดาเมจ + stun ในวง

///
/// อิงสเปกต้นทาง:
///   RANGE = EFFECT RADIUS  → ระยะ dash เท่ากับรัศมี AoE ทั้งคู่มาจาก AbilityData.range
///                            ต่างกันตรง AoE คูณ Area stat ส่วนระยะ dash ไม่คูณ
///   SPEED = ตั้งใจไม่ทำตามสเปก — dash ใช้เวลาคงที่ (dashDuration) ไม่ผูกกับ MoveSpeed
///           ผูกแล้ว dash ยืดเป็น ~0.6 วิ และ stat/บัฟความเร็วจะไปเปลี่ยนจังหวะสกิลด้วย
///   STUN  = 2 วิ (× Duration stat)
///
/// AbilityData (cooldown, damage, range) อยู่ใน AbilityData asset
/// </summary>
public class ValorWeapon : AbilityBase, IHUDAbility
{
    [Header("Input Key")]
    [Tooltip("ปุ่มที่กดเพื่อใช้สกิล")]
    public Key activateKey = Key.Q;

    [Header("Dash")]
    [Tooltip("เวลาที่ใช้ dash (วินาที) — คงที่ ไม่ผูกกับ MoveSpeed - ระยะทางมาจาก AbilityData.range")]
    public float dashDuration = 0.12f;

    [Header("Stun")]
    [Tooltip("วินาทีที่ศัตรูในวงถูกหยุด — คูณ Duration stat")]
    public float stunDuration = 2f;

    // ── Cooldown state ────────────────────────────────────────────────────
    public bool  IsOnCooldown      { get; private set; }
    public float CooldownRemaining { get; private set; }
    public float CooldownMax       { get; private set; }

    // ── Events (UI ฟัง) ───────────────────────────────────────────────────
    public static event System.Action<ValorWeapon, float> OnCooldownChanged;
    public static event System.Action<ValorWeapon>        OnActivated;

    private bool          isDashing;

    // ── IHUDAbility ───────────────────────────────────────────────────────
    public string HUDSlotKey       => "Q";   // Valor ของ Riven อยู่ Q เสมอ
    public string HUDKeyLabel      => activateKey.ToString();
    public bool   IsActiveMode     => false;
    public float  ActiveRemaining  => 0f;
    public float  ActiveMax        => 0f;

    // ── Init ──────────────────────────────────────────────────────────────
    // ── Update — input + cooldown tick ────────────────────────────────────
    void Update()
    {
        // Cooldown countdown (ทุก client — เพื่อ UI sync)
        if (IsOnCooldown)
        {
            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Time.deltaTime);
            OnCooldownChanged?.Invoke(this, CooldownRemaining / CooldownMax);
            if (CooldownRemaining <= 0f) IsOnCooldown = false;
        }

        // Input — Owner only
        if (manager == null || !manager.IsOwner) return;
        if (manager.playerMove != null && manager.playerMove.isDead.Value) return;
        if (IsOnCooldown || isDashing) return;
        if (GamePause.LocalInputSuspended) return;   // host เปิดเมนู pause ใน multiplayer

        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb[activateKey].wasPressedThisFrame)
            Activate();
    }

    // ── Activate ──────────────────────────────────────────────────────────
    void Activate()
    {
        var ld = data.GetLevelData(currentLevel);

        // Cooldown scale ตาม Ability Haste
        float cd = ld.cooldown;
        if (manager.statManager != null) cd *= manager.statManager.GetCooldownMultiplier();
        CooldownMax       = cd;
        CooldownRemaining = cd;
        IsOnCooldown      = true;
        OnCooldownChanged?.Invoke(this, 1f);
        OnActivated?.Invoke(this);

        float areaMult = manager.statManager != null ? manager.statManager.GetAreaMultiplier() : 1f;

        // สเปกต้นทาง: RANGE กับ EFFECT RADIUS เป็นเลขเดียวกัน (250) แต่มีแค่ EFFECT RADIUS
        // ที่ติดป้าย "× Area" — ระยะ dash จึงอ่าน ld.range ดิบ ส่วนวง AoE คูณ Area stat
        float dashDist = ld.range;
        float range    = ld.range * areaMult;

        // ทิศ dash = ทิศที่ผู้เล่นกด input อยู่
        // fallback → ทิศหาศัตรูที่ใกล้สุด → transform.forward
        Vector3 dir = manager.playerMove?.MoveDirection ?? Vector3.zero;
        if (dir.sqrMagnitude < 0.001f)
        {
            Transform nearest = FindNearestEnemy(range * 1.5f);
            dir = nearest != null
                ? (nearest.position - transform.position)
                : transform.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
            dir = dir.normalized;
        }

        float damage = RollDamage(ld.damage, out bool isCrit);
        StartCoroutine(DashAndBlast(dir, dashDist, range, damage, isCrit));
    }

    // ── Dash coroutine ────────────────────────────────────────────────────
    IEnumerator DashAndBlast(Vector3 dir, float dashDist, float radius, float damage, bool isCrit)
    {
        isDashing = true;

        var pm = manager.playerMove;
        var rb = pm?.GetComponent<Rigidbody>();

        // try/finally: ธงทั้งสองตัวต้องถูกคืนค่าทุกทางออก ไม่ใช่แค่ทางที่วิ่งจนจบ
        // playermove.isDashing ที่ค้าง true = ขยับไม่ได้ถาวร (FixedUpdate ปล่อยให้ dash คุมเอง)
        // isDashing ในตัวเองที่ค้าง = กด Q ไม่ติดอีกเลยทั้งรัน
        try
        {
            if (rb != null && pm != null)
            {
                pm.isDashing      = true;
                rb.linearVelocity  = Vector3.zero;

                Vector3 startPos  = rb.position;
                Vector3 endPos    = startPos + dir * dashDist;
                float   elapsed   = 0f;

                while (elapsed < dashDuration)
                {
                    elapsed += Time.deltaTime;
                    float t  = Mathf.SmoothStep(0f, 1f, elapsed / dashDuration);
                    rb.MovePosition(Vector3.Lerp(startPos, endPos, t));
                    yield return new WaitForFixedUpdate();
                }

                rb.MovePosition(endPos);
                rb.linearVelocity  = Vector3.zero;
            }
            else yield return null;

            FireMelee(transform.position, radius, damage, isCrit);

            // VFX ทุบพื้น — scale ตามรัศมีจริง เลือก key ได้ที่ abilityVfxType บน Valor_Weapon.prefab
            // fallback เป็น "None" เพราะยังไม่มี VFX ประจำตัว = เงียบจนกว่า designer จะเลือก
            ShowVfx(ResolveVfx("None"), transform.position, radius);

            // Stun — วงเดียวกับ AoE · Enemy.ApplyFreeze หยุด velocity + ยิง NotifyFreezeClientRpc
            // ให้เห็นผลบนทุกเครื่องอยู่แล้ว จึงไม่ต้องกระจาย VFX เพิ่มเอง
            if (stunDuration > 0f)
            {
                float durMult = manager.statManager != null ? manager.statManager.GetDurationMultiplier() : 1f;
                manager.ApplyFreezeToEnemiesServerRpc(transform.position, radius, stunDuration * durMult);
            }
        }
        finally
        {
            if (pm != null) pm.isDashing = false;
            isDashing = false;
        }
    }
}
