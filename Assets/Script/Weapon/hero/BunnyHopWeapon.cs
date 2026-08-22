using System.Collections;
using UnityEngine;

/// <summary>
/// Signature Weapon ของ Riven — ไม่ใช้ cooldown timer แต่ใช้ CHARGE จาก ChargeManager
///
/// ทุก cast  : Dash + AoE radial 360° รอบตัว
///   cast คู่ : รัศมี ×evenCastRadiusMult + ดีดศัตรูกระเด็นออก (knock back)
///
/// Move Speed ถาวร: อาวุธเลเวลสูงขึ้น = เดินเร็วขึ้นถาวร ซึ่งทำให้ชาร์จไวขึ้นไปด้วย
///                  เป็นวงจรเร่งตัวเองที่เป็นแกนของตัวละครนี้ในสเปกต้นทาง
///
/// ขณะ AD_BladeOfExile active (เพิ่มเติมบน AoE ปกติ):
///   + ยิง Projectile ในทิศ dash
///   + AoE radius ×exileAoeMult
///   (Wind Slash ถูกถอดออก — รอ design ใหม่)
///
/// Super BunnyHop (IsSuper):
///   cast 2 เท่านั้น: AoE ตี 2 ครั้ง (double hit)
///
/// Runic Blade: ยิ่งอยู่ *ใกล้* ศัตรู → damage +0–15% (ตรงกับสเปกต้นทาง
///              "based on how close they are" — ของเดิมกลับด้าน ให้รางวัลการยืนไกล)
/// Shield: 25% ของ damage × จำนวนศัตรูโดน
/// </summary>
public class BunnyHopWeapon : WeaponBase
{
    [Header("Settings (Per-Weapon Prefab)")]
    [Tooltip("Projectile prefab สำหรับ weapon นี้ (ต้องมี NetworkObject + Projectile script)")]
    public GameObject projectilePrefab;

    protected override GameObject GetProjectilePrefab() => projectilePrefab;

    [Header("Dash")]
    public float dashDistance = 4f;
    public float dashDuration = 0.15f;

    [Header("AoE")]
    [Tooltip("Projectile maxRange ขณะ Exile = base range × ค่านี้")]
    public float exileProjectileRangeMult = 2f;

    [Header("Runic Blade Passive")]
    [Tooltip("ระยะที่โบนัสลดเหลือ 0 — ใกล้กว่านี้โบนัสไล่ขึ้นหา 15% ที่ระยะประชิด")]
    public float runicMaxRange = 12f;

    [Header("Shield")]
    [Tooltip("Shield = X% ของ damage ที่ทำ")]
    public float shieldPercent = 0.25f;

    /// <summary>
    /// Super/Fusion อ่านจาก tier ของ WeaponData ตรงๆ ไม่ใช่ธงที่ต้องมีคนมาเปิดให้
    ///
    /// ของเดิมเป็น _isSuper ที่เปิดด้วย ActivateSuper() ซึ่ง **ไม่มีใครเรียกทั้งโปรเจกต์**
    /// ผลคือ cast คู่ของ RabbitHop ไม่เคยระเบิดครั้งที่สองเลยตั้งแต่ต้น
    /// ReplaceWeapon แค่สลับ data (prefab เป็นตัวเดียวกัน) จึงไม่มีจังหวะไหนที่จะไปเรียกธงได้เลย
    ///
    /// tier เป็นแหล่งความจริงที่ UpgradeManager ใช้อยู่แล้วทั้งไฟล์ — ดึงจากที่เดียวกันจะไม่หลุดจากกันอีก
    /// และครอบ Fusion ด้วย (StormBunny) โดยไม่ต้องเพิ่มเงื่อนไข
    /// </summary>
    public bool IsSuper => data != null && data.tier != WeaponTier.Normal;

    [Header("Cast คู่")]
    [Tooltip("คูณรัศมี AoE ของ cast คู่ — 1 = เท่ากับ cast คี่")]
    public float evenCastRadiusMult = 1f;
    [Tooltip("หน่วงก่อนระเบิดครั้งที่สองของ Super (วินาที) - สเปกต้นทาง Carrot Crash ใช้ 0.4")]
    public float secondBlastDelay = 0.4f;
    [Tooltip("ระยะที่ศัตรูถูกผลักออกจากจุดลงจอด (หน่วย) — 0 = ไม่ผลัก")]
    public float knockBackForce = 1.5f;

    [Header("Move Speed ถาวร (ตามเลเวลอาวุธ)")]
    [Tooltip("โบนัสความเร็วต่อเลเวล 0-4 (0.05 = +5%) - สเปกต้นทางใช้ 5/10/15/20/25% - " +
             "array สั้นกว่าเลเวลจริงจะใช้ค่าสุดท้าย - ว่าง = ปิดฟีเจอร์")]
    public float[] moveSpeedPerLevel = { 0.05f, 0.10f, 0.15f, 0.20f, 0.25f };

    [Header("Blade of Exile Bonus")]
    [Tooltip("คูณ AoE radius เพิ่มเติมขณะ Exile active")]
    public float exileAoeMult = 1.5f;
    // Projectile speed + count อ่านจาก WeaponData → levels → projectileSpeed / projectileCount

    protected override bool UsesCooldownTimer => false;

    private ChargeManager chargeManager;

    /// <summary>ค่าที่บวกเข้า tempMoveSpeedBonus ไปแล้ว — เก็บไว้ถอนคืนให้ตรงตอนเปลี่ยนเลเวล/ถูกทำลาย</summary>
    private float _appliedMoveSpeed;

    protected override void OnInit()
    {
        chargeManager = manager.GetComponent<ChargeManager>();
        ApplyMoveSpeedForLevel();
    }

    protected override void OnLevelUp() => ApplyMoveSpeedForLevel();

    /// <summary>
    /// ถอนโบนัสเดิมออกก่อนแล้วค่อยบวกของเลเวลใหม่ — tempMoveSpeedBonus เป็นตัวรวมที่หลายระบบ
    /// บวกเข้ามาพร้อมกัน (Exile, Gunner passive, Support Arena, augment) เซ็ตทับตรงๆ จะลบของคนอื่นทิ้ง
    ///
    /// เฉพาะ owner: อาวุธถูกสร้างบนทุก client ผ่าน AddWeaponClientRpc แต่การเดินเป็น owner-authoritative
    /// ถ้าไม่กัน สำเนาบนเครื่องคนอื่นจะไปแก้ตัวเลขของ player object ที่ไม่ได้ขยับด้วยค่านั้นอยู่แล้ว
    /// </summary>
    void ApplyMoveSpeedForLevel()
    {
        if (manager == null || !manager.IsOwner) return;
        var pm = manager.playerMove;
        if (pm == null) return;

        float target = 0f;
        if (moveSpeedPerLevel != null && moveSpeedPerLevel.Length > 0)
        {
            int idx = Mathf.Clamp(currentLevel, 0, moveSpeedPerLevel.Length - 1);
            target = moveSpeedPerLevel[idx];
        }

        if (Mathf.Approximately(target, _appliedMoveSpeed)) return;

        pm.tempMoveSpeedBonus -= _appliedMoveSpeed;
        _appliedMoveSpeed      = target;
        pm.tempMoveSpeedBonus += _appliedMoveSpeed;
    }

    /// <summary>
    /// ถอนโบนัสคืนตอนอาวุธถูกทำลาย — ReplaceWeapon (อัปเป็น Super) ทำลายตัวเก่าทิ้ง
    /// ถ้าไม่ถอน โบนัสจะค้างแล้วตัวใหม่บวกซ้อนเข้าไปอีก
    /// </summary>
    void OnDestroy()
    {
        if (_appliedMoveSpeed == 0f) return;
        if (manager != null && manager.playerMove != null)
            manager.playerMove.tempMoveSpeedBonus -= _appliedMoveSpeed;
        _appliedMoveSpeed = 0f;
    }

    protected override void OnFire(WeaponLevelData ld) { /* ไม่ใช้ */ }

    // ── เรียกจาก ChargeManager เมื่อ CHARGE เต็ม ─────────────────────────
    public virtual void FireOnCharge(int fireCount)
    {
        if (manager == null || !manager.IsOwner) return;
        if (manager.playerMove != null && manager.playerMove.isDead.Value) return;

        var   ld     = data.GetLevelData(currentLevel);
        var   sm     = manager.statManager;
        float damage = ld.damage * (sm != null ? sm.GetPowerMultiplier() : 1f);
        float range  = ld.range  * (sm != null ? sm.GetAreaMultiplier()  : 1f);

        // cast คู่ = รัศมีคูณเพิ่ม + ผลักศัตรูออก
        bool  evenCast  = (fireCount % 2 == 0);
        float aoeRadius = range;
        if (evenCast && evenCastRadiusMult > 0f) aoeRadius *= evenCastRadiusMult;

        // Exile → radius ใหญ่ขึ้นอีก
        bool exileActive = GetExileActive();
        if (exileActive) aoeRadius *= exileAoeMult;

        // Runic Blade: ยิ่ง *ใกล้* ศัตรู → damage +0–15%
        // กลับทิศจากของเดิมซึ่งให้โบนัสตอนอยู่ไกล — นั่นให้รางวัลการยืนห่างแล้วตี
        // ทั้งที่กลไกของตัวละครคือพุ่งเข้าหา สเปกต้นทางเขียนว่า "how close they are"
        Transform targetEnemy = FindTargetEnemy(aoeRadius * 2f);
        if (targetEnemy != null)
        {
            float dist  = Vector3.Distance(transform.position, targetEnemy.position);
            float bonus = (1f - Mathf.Clamp01(dist / runicMaxRange)) * 0.15f;
            damage *= (1f + bonus);
        }

        damage = RollDamage(damage, out bool isCrit);

        // ทิศ dash = ทิศที่ผู้เล่นกด input อยู่
        Vector3 dashDir = manager.playerMove?.MoveDirection ?? Vector3.zero;
        if (dashDir.sqrMagnitude < 0.001f)
        {
            dashDir = targetEnemy != null
                ? (targetEnemy.position - transform.position)
                : transform.forward;
            dashDir.y = 0f;
            if (dashDir.sqrMagnitude > 0.001f) dashDir = dashDir.normalized;
            else dashDir = transform.forward;
        }

        StartCoroutine(DashAndFire(dashDir, aoeRadius, damage, evenCast, exileActive, isCrit));
    }

    protected virtual IEnumerator DashAndFire(Vector3 dir, float radius, float damage,
                            bool evenCast, bool exileActive, bool isCrit = false)
    {
        var pm = manager.playerMove;
        var rb = pm?.GetComponent<Rigidbody>();

        // ── Dash ──────────────────────────────────────────────────────────
        if (rb != null && pm != null)
        {
            pm.isDashing = true;
            rb.linearVelocity  = Vector3.zero;

            Vector3 startPos = rb.position;
            Vector3 endPos   = startPos + dir * dashDistance;
            float   elapsed  = 0f;

            // try/finally: isDashing ต้องถูกคืนค่าทุกทางออก ไม่ใช่แค่ทางที่วิ่งจนจบ
            // playermove.FixedUpdate ปล่อยให้ dash คุม velocity เอง ธงที่ค้าง true
            // แปลว่าผู้เล่นขยับไม่ได้ถาวร — ตายคา dash แล้ว Respawn มาก็ยืนนิ่งตลอดรัน
            try
            {
                while (elapsed < dashDuration)
                {
                    if (manager.playerMove != null && manager.playerMove.isDead.Value) yield break;
                    elapsed += Time.deltaTime;
                    float t  = Mathf.SmoothStep(0f, 1f, elapsed / dashDuration);
                    rb.MovePosition(Vector3.Lerp(startPos, endPos, t));
                    yield return new WaitForFixedUpdate();
                }

                rb.MovePosition(endPos);
                rb.linearVelocity  = Vector3.zero;
            }
            finally
            {
                pm.isDashing = false;
            }
        }
        else yield return null;

        Vector3 center      = transform.position;
        bool    doubleBlast = IsSuper && evenCast;

        // นับศัตรูในวง **ก่อน** ตี — ตัวที่ตายจากหมัดนี้ต้องถูกนับด้วย
        // ถ้านับหลังตี ตัวที่ตายจะหลุดจากการนับ กลายเป็นยิ่งฆ่าเก่งยิ่งได้โล่น้อย
        int enemiesHit = FindAllEnemiesInRange(radius).Length;

        // ── ระเบิดครั้งแรก ───────────────────────────────────────────────
        FireBlast(center, radius, damage, isCrit, evenCast ? knockBackForce : 0f);

        // ── Shield ────────────────────────────────────────────────────────
        // ไม่โดนใครเลย = ไม่ได้โล่ · เดิมคูณด้วย Mathf.Max(1f, count) ซึ่งตรึงตัวคูณขั้นต่ำไว้ที่ 1
        // ทำให้ร่ายลอยๆ กลางที่โล่งก็ได้โล่ทุกครั้ง ทั้งที่กลไกคือ "โล่จากดาเมจที่ตีออกไป"
        // (WeaponBase ร่ายเองตามคูลดาวน์ไม่สนว่ามีเป้าหรือไม่ จึงกลายเป็นโล่ฟรีตลอดเวลา)
        if (enemiesHit > 0)
            manager.AddShieldServerRpc(damage * shieldPercent * enemiesHit);

        // ── Exile Bonus: Projectile radial 360° (ทุก cast เมื่อ Exile active) ──
        if (exileActive)
        {
            // Projectile กระจาย 360°/count — ผ่าน BuildEffectiveLevelData เพื่อรับ stat bonus
            // (projectileCount + GetBonusProjectileCount, range × GetAreaMultiplier)
            var rawLd          = data != null ? data.GetLevelData(currentLevel) : new WeaponLevelData();
            var   projLd       = BuildEffectiveLevelData(rawLd);
            float baseRange    = projLd.range;
            float projMaxRange = baseRange * exileProjectileRangeMult;
            float projSpeed    = projLd.projectileSpeed;
            int   pCount       = Mathf.Max(1, projLd.projectileCount);
            float angleStep    = 360f / pCount;
            for (int i = 0; i < pCount; i++)
            {
                float   angle   = i * angleStep;
                Vector3 projDir = Quaternion.Euler(0f, angle, 0f) * dir; // dir = ทิศที่กำลังไป
                FireProjectile(center, projDir, damage, projSpeed,
                               piercing: projLd.piercing, maxRange: projMaxRange,
                               isCrit: isCrit);
            }
        }

        // ── ระเบิดครั้งที่สอง (Super + cast คู่) ──────────────────────────
        // วางไว้ท้ายสุดโดยตั้งใจ: โล่กับกระสุน Exile ยังออกทันทีพร้อมหมัดแรกเหมือนเดิม
        // มีแค่หมัดสองที่ถูกหน่วง ไม่ใช่ทั้งชุด
        //
        // ไม่นับศัตรูใหม่ — FireMelee ไป overlap ฝั่ง server ตอนเรียกอยู่แล้ว
        // ตัวที่เดินเข้ามาระหว่างหน่วงจึงโดนด้วย ตัวที่ตายไปแล้วก็หลุดเอง
        if (doubleBlast)
        {
            if (secondBlastDelay > 0f) yield return new WaitForSeconds(secondBlastDelay);

            // ตายระหว่างหน่วง = ไม่ต้องระเบิดต่อ
            if (manager == null || manager.playerMove == null || manager.playerMove.isDead.Value)
                yield break;

            // ใช้ center ตัวเดิม ไม่ใช่ transform.position — ระเบิดค้างที่จุดลงจอด
            // ระหว่างหน่วงผู้เล่นเดินต่อได้ ถ้าอ่านตำแหน่งใหม่ระเบิดจะวิ่งตามตัวไป
            // ซึ่งทำให้ศัตรูที่โดนหมัดแรกหลุดวงหมัดสอง และผู้เล่นเล็งจังหวะสองไม่ได้
            //
            // สเปกต้นทางระบุว่า secondary impact ทำซ้ำผลการดีดด้วย จึงส่ง knock back ไปอีกครั้ง
            FireBlast(center, radius, damage, isCrit, knockBackForce);
        }
    }

    /// <summary>
    /// ระเบิด AoE หนึ่งครั้ง — ดาเมจ + ผลัก + VFX ไปด้วยกันเสมอ
    /// แยกออกมาเพราะ Super ยิงสองครั้งคนละจังหวะ ถ้าเรียก FireMelee ในลูปแล้ววาด VFX นอกลูป
    /// ผู้เล่นจะเห็นระเบิดครั้งเดียวทั้งที่กินดาเมจสองครั้ง
    /// </summary>
    protected void FireBlast(Vector3 center, float radius, float damage, bool isCrit, float knockBack)
    {
        // ปล่อยทิศ knock back ว่างไว้ ฝั่ง server จะผลักออกจาก center เป็นรัศมีให้เอง
        FireMelee(center, radius, damage, isCrit, knockBack);
        // isAttackHit:false → ไม่ spawn HitEffect overlay (Enemy.EnemyTakeDamage จัดให้แล้ว)
        ShowVfx(ResolveHitVfx("MeteorAoE"), center, radius, isAttackHit: false);
    }

    protected bool GetExileActive()
    {
        var exile = manager != null ? manager.GetComponentInChildren<BladeOfExileWeapon>() : null;
        return exile != null && exile.IsExileActive;
    }

#if UNITY_EDITOR
    protected override void OnDrawGizmosSelected()
    {
        float baseRange = data != null ? data.GetLevelData(currentLevel).range : 3f;
        float aoeR      = baseRange;
        float projR     = baseRange * exileProjectileRangeMult;

        Vector3 pos = transform.position;

        // ── AoE cast 1 & 2 (เขียว — ขนาดเท่ากัน) ─────────────────────────
        UnityEditor.Handles.color = new Color(0f, 1f, 0f, 0.25f);
        UnityEditor.Handles.DrawSolidDisc(pos, Vector3.up, aoeR);
        UnityEditor.Handles.color = Color.green;
        UnityEditor.Handles.DrawWireDisc(pos, Vector3.up, aoeR);

        // ── Exile: Projectile radial (ส้ม) — อันแรกจาก forward ────────────
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
        int pCount = Mathf.Max(1, data != null ? data.GetLevelData(currentLevel).projectileCount : 4);
        for (int i = 0; i < pCount; i++)
        {
            float   angle   = i * (360f / pCount);
            Vector3 projDir = Quaternion.Euler(0f, angle, 0f) * transform.forward; // forward แทน dir จริงใน editor
            Gizmos.DrawRay(pos, projDir * projR);
            // จุดปลาย
            Gizmos.DrawWireSphere(pos + projDir * projR, i == 0 ? 0.2f : 0.12f);
        }

        // ── Label ─────────────────────────────────────────────────────────
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(pos + Vector3.right * aoeR,  $"aoe r={aoeR:F1}");
        UnityEditor.Handles.Label(pos + Vector3.forward * projR + Vector3.up * 0.3f,
                                                                   $"proj range={projR:F1}");
    }
#endif

}
