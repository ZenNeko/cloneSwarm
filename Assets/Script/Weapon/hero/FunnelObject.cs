using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Hunter Ultimate — Funnel
/// ลำดับ: หาเป้า → พุ่ง → ยิง → หาเป้าใหม่
/// Self-despawn หลัง lifetime วินาที
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class FunnelObject : NetworkBehaviour
{
    // ── Init Values ───────────────────────────────────────────────────────
    private Vector3 orbitCenter;
    private float   orbitRadius;
    private float   laserDamage;
    private float   laserCooldown;
    private float   attackRange;
    private float   lifetime;
    private ulong   ownerClientId;

    // ── Inspector Config ──────────────────────────────────────────────────
    [Header("Movement")]
    [Tooltip("ความเร็วพุ่งหาเป้าหมาย (หน่วย/วินาที)")]
    public float dashSpeed = 14f;
    [Tooltip("ระยะที่หยุดพุ่งแล้วยิง (จากตัว enemy)")]
    public float fireStopDist = 1.5f;

    [Header("Beam Config")]
    [Tooltip("มุมกระจายระหว่าง beam แต่ละเส้น (องศา) — ใช้เมื่อ beamCount > 1")]
    public float beamSpreadDeg = 15f;

    [Header("Exp Collection")]
    [Tooltip("ระยะที่ funnel สามารถเก็บ ExpOrb ได้ (หน่วย)")]
    public float expCollectRadius = 2f;

    [Header("Return Config")]
    [Tooltip("ความเร็วบินกลับหา player เมื่อ ultimate หมด (หน่วย/วินาที)")]
    public float returnSpeed   = 20f;
    [Tooltip("ระยะที่ถือว่าถึง player แล้ว → despawn")]
    public float returnArrivalDist = 0.8f;
    [Tooltip("timeout สูงสุดของการบินกลับ (วินาที) — กัน funnel ค้างถ้า player ไกลมาก")]
    public float returnTimeout = 5f;

    [Header("Separation Config")]
    [Tooltip("ระยะขั้นต่ำจากจุดศูนย์กลาง player (หน่วย)")]
    public float minPlayerDist    = 2f;
    [Tooltip("ระยะขั้นต่ำระหว่าง funnel กับ funnel (หน่วย)")]
    public float funnelSeparation = 1f;

    // ── Runtime values (set via Init) ─────────────────────────────────────
    private int beamCount = 1;   // จำนวน beam ต่อการยิง (จาก ProjectileCount stat)

    // ── State Machine ─────────────────────────────────────────────────────
    private enum FunnelState { Idle, Dashing, Cooldown, Returning }
    private FunnelState state = FunnelState.Idle;

    private float     lifeTimer;
    private float     cooldownTimer;
    private float     expCheckTimer;
    private float     returnTimer;
    private Vector3   dashTarget;
    private Transform currentTarget;

    // ── Init (เรียกโดย SpawnFunnelsServerRpc) ─────────────────────────────
    public void Init(
        Vector3 center, float radius,
        float   damage, float cooldown,
        float   range,  float life,
        ulong   clientId,
        int     beams = 1)
    {
        orbitCenter   = center;
        orbitRadius   = radius;
        laserDamage   = damage;
        laserCooldown = cooldown;
        attackRange   = range;
        lifetime      = life;
        ownerClientId = clientId;
        beamCount     = Mathf.Max(1, beams);
    }

    // ── Update (Server only) ──────────────────────────────────────────────
    void Update()
    {
        if (!IsServer) return;

        lifeTimer += Time.deltaTime;

        // เมื่อ lifetime หมด → เริ่มบินกลับ (ยกเว้นถ้ากำลัง Returning อยู่แล้ว)
        if (lifeTimer >= lifetime && state != FunnelState.Returning)
        {
            state       = FunnelState.Returning;
            returnTimer = 0f;
            currentTarget = null;
        }

        UpdateOwnerCenter();

        switch (state)
        {
            case FunnelState.Idle:      UpdateIdle();      break;
            case FunnelState.Dashing:   UpdateDashing();   break;
            case FunnelState.Cooldown:  UpdateCooldown();  break;
            case FunnelState.Returning: UpdateReturning(); break;
        }

        // Separation + Exp เฉพาะตอนไม่ได้กำลังบินกลับ
        if (state != FunnelState.Returning)
        {
            SeparateFromOtherFunnels();
            CollectNearbyExpOrbs();
        }
    }

    // ── ติดตาม owner player position ─────────────────────────────────────
    void UpdateOwnerCenter()
    {
        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.Singleton.ConnectedClients
            .TryGetValue(ownerClientId, out var client))
        {
            if (client.PlayerObject != null)
                orbitCenter = client.PlayerObject.transform.position;
        }
    }

    // ── State: Idle ───────────────────────────────────────────────────────
    // รอในตำแหน่งปัจจุบัน (ไม่ drift ตาม player) + หาเป้าถัดไป
    void UpdateIdle()
    {
        // ไม่ drift กลับ orbit — funnel อยู่ที่เดิมในโลก
        // เพื่อไม่ให้ถูก "ลากตาม" เมื่อ player เคลื่อนที่

        currentTarget = FindNearestEnemy();
        if (currentTarget == null) return;

        UpdateDashTarget();
        state = FunnelState.Dashing;
    }

    // ── State: Dashing ────────────────────────────────────────────────────
    void UpdateDashing()
    {
        // เป้าหายไป → กลับ Idle
        if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
        {
            state = FunnelState.Idle;
            return;
        }

        // ติดตาม target ที่เคลื่อนที่
        UpdateDashTarget();

        // พุ่งหา dashTarget
        transform.position = Vector3.MoveTowards(
            transform.position, dashTarget, dashSpeed * Time.deltaTime);

        // ถึงระยะแล้ว → ยิง
        if (Vector3.Distance(transform.position, dashTarget) < 0.4f)
        {
            FireAtTarget(currentTarget);
            cooldownTimer = laserCooldown;
            state         = FunnelState.Cooldown;
        }
    }

    // ── State: Cooldown ───────────────────────────────────────────────────
    void UpdateCooldown()
    {
        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer <= 0f)
            state = FunnelState.Idle;
    }

    // ── State: Returning ──────────────────────────────────────────────────
    // บินกลับหา player เมื่อ ultimate หมด → despawn เมื่อถึงหรือ timeout
    void UpdateReturning()
    {
        returnTimer += Time.deltaTime;

        // พุ่งเข้าหา player โดยตรง (ความสูงเดิม → ค่อยๆ ปรับลงเล็กน้อย)
        Vector3 target = orbitCenter + Vector3.up * 1.2f;
        transform.position = Vector3.MoveTowards(
            transform.position, target, returnSpeed * Time.deltaTime);

        float dist = Vector3.Distance(transform.position, orbitCenter);

        if (dist <= returnArrivalDist || returnTimer >= returnTimeout)
            GetComponent<NetworkObject>()?.Despawn(true);
    }

    // ── Fire ──────────────────────────────────────────────────────────────
    void FireAtTarget(Transform target)
    {
        if (target == null) return;

        Vector3 origin  = transform.position;
        Vector3 baseDir = target.position - origin;
        baseDir.y = 0f;
        if (baseDir.sqrMagnitude < 0.001f) return;
        baseDir = baseDir.normalized;

        int   mask       = LayerMask.GetMask("Enemy");
        float startAngle = -(beamCount - 1) * beamSpreadDeg * 0.5f;

        for (int i = 0; i < beamCount; i++)
        {
            float   angle  = startAngle + i * beamSpreadDeg;
            Vector3 dir    = Quaternion.Euler(0f, angle, 0f) * baseDir;

            // beam กลาง (angle≈0) รับประกัน hit target โดยตรง
            if (i == (beamCount - 1) / 2)
                target.GetComponent<Enemy>()?.EnemyTakeDamage(laserDamage);

            // RaycastAll ทะลุ enemy ในแนว beam นี้
            if (mask != 0)
            {
                var hits = Physics.RaycastAll(origin, dir, attackRange, mask);
                foreach (var h in hits)
                {
                    if (h.transform == target && i == (beamCount - 1) / 2) continue;
                    h.collider.GetComponent<Enemy>()?.EnemyTakeDamage(laserDamage);
                }
            }
            else
            {
                var hits = Physics.RaycastAll(origin, dir, attackRange);
                foreach (var h in hits)
                {
                    if (h.transform == target && i == (beamCount - 1) / 2) continue;
                    if (h.collider.CompareTag("Enemy"))
                        h.collider.GetComponent<Enemy>()?.EnemyTakeDamage(laserDamage);
                }
            }

            ShowLaserVfxClientRpc(origin, origin + dir * attackRange);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    void UpdateDashTarget()
    {
        Vector3 toEnemy = (currentTarget.position - transform.position);
        toEnemy.y = 0f;
        if (toEnemy.sqrMagnitude < 0.001f) toEnemy = Vector3.forward;
        toEnemy = toEnemy.normalized;

        dashTarget   = currentTarget.position - toEnemy * fireStopDist;
        dashTarget.y = orbitCenter.y + 1.5f;  // รักษาความสูงคงที่
    }

    Transform FindNearestEnemy()
    {
        var cols      = PlayerWeaponManager.OverlapEnemy(transform.position, attackRange);
        Transform nearest = null;
        float     minDist = float.MaxValue;
        foreach (var c in cols)
        {
            float d = Vector3.Distance(transform.position, c.transform.position);
            if (d < minDist) { minDist = d; nearest = c.transform; }
        }
        return nearest;
    }

    // ── Exp Collection ────────────────────────────────────────────────────
    /// <summary>
    /// ตรวจสอบ ExpOrb ที่อยู่ในระยะ expCollectRadius
    /// ถ้าเจอ → เพิ่ม exp ให้ owner player ทันที + despawn orb
    /// </summary>
    void CollectNearbyExpOrbs()
    {
        expCheckTimer += Time.deltaTime;
        if (expCheckTimer < 0.2f) return;  // check ทุก 0.2s ไม่ต้องทุก frame
        expCheckTimer = 0f;

        var cols = Physics.OverlapSphere(transform.position, expCollectRadius);

        // หา owner PlayerStatManager สำหรับ ExpMultiplier
        PlayerStatManager ownerSM = null;
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.ConnectedClients.TryGetValue(ownerClientId, out var client))
        {
            ownerSM = client.PlayerObject?.GetComponent<PlayerStatManager>();
        }

        foreach (var c in cols)
        {
            var orb = c.GetComponent<ExpOrb>();
            if (orb == null || !orb.IsSpawned) continue;

            float finalExp = orb.expAmount * (ownerSM != null ? ownerSM.GetExpMultiplier() : 1f);
            SharedExperienceManager.Instance?.AddExp(finalExp);
            orb.NetworkObject.Despawn(true);
        }
    }

    void SeparateFromOtherFunnels()
    {
        var all  = FindObjectsByType<FunnelObject>(FindObjectsSortMode.None);
        var push = Vector3.zero;

        foreach (var other in all)
        {
            if (other == this) continue;
            Vector3 diff = transform.position - other.transform.position;
            float   dist = diff.magnitude;
            if (dist < funnelSeparation && dist > 0.001f)
                push += diff.normalized * (funnelSeparation - dist);
        }

        if (push.sqrMagnitude > 0.0001f)
            transform.position += push * Time.deltaTime * 4f;

        // บังคับ min distance จาก player เฉพาะตอนไม่ได้พุ่ง
        if (state != FunnelState.Dashing)
            EnforcePlayerDistance();
    }

    void EnforcePlayerDistance()
    {
        Vector3 toFunnel = transform.position - orbitCenter;
        toFunnel.y = 0f;
        float dist = toFunnel.magnitude;
        if (dist < minPlayerDist && dist > 0.001f)
        {
            Vector3 clamped = orbitCenter + toFunnel.normalized * minPlayerDist;
            clamped.y = transform.position.y;
            transform.position = clamped;
        }
    }

    [ClientRpc]
    void ShowLaserVfxClientRpc(Vector3 from, Vector3 to)
    {
        VFXFactory.PlayBeam(VFXType.RailgunBeam, from, to, duration: 0.08f);
    }
}
