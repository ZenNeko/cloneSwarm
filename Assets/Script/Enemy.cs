using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class Enemy : NetworkBehaviour
{
    // ── Global kill broadcast (Server → all clients) ───────────────────────
    /// <summary>ยิงบน ALL clients ทุกครั้งที่ enemy ตาย — subscribe ด้วย GunnerPassiveWeapon</summary>
    public static event System.Action OnAnyEnemyDied;
    /// <summary>ยิงบน ALL clients ทุกครั้งที่ enemy ตาย พร้อม world position</summary>
    public static event System.Action<Vector3> OnAnyEnemyDiedAt;
    /// <summary>ยิงบน ALL clients ทุกครั้งที่ enemy โดนดาเมจ — subscribe ด้วย HunterPassiveWeapon</summary>
    public static event System.Action OnAnyEnemyHit;
    /// <summary>ยิงบน SERVER เท่านั้น ทุกครั้งที่ enemy ตาย พร้อมอินสแตนซ์และตำแหน่ง</summary>
    public static event System.Action<Enemy, Vector3> OnEnemyDiedServer;

    /// <summary>ลิสต์เก็บรายชื่อศัตรูทั้งหมดที่ยังแอ็คทีฟอยู่ในแผนที่ เพื่อใช้แทน FindObjectsOfType</summary>
    public static readonly HashSet<Enemy> ActiveEnemies = new HashSet<Enemy>();

    [Header("Movement")]
    public float speed = 3f;
    [Tooltip("ความเร็วในการหันหน้าเข้าหาทิศทางการเคลื่อนที่ (องศาต่อวินาที) — 0 = หันหน้าทันที")]
    public float rotationSpeed = 360f;
    [HideInInspector] public bool suppressDefaultMovement = false;


    [Header("Wall Avoidance")]
    [Tooltip("Layer ที่ถือว่าเป็นกำแพง — enemy จะไถลตามกำแพงแทนติดอยู่กับที่")]
    public LayerMask wallLayer = ~0;            // default: ทุก layer
    [Tooltip("รัศมี SphereCast เพื่อตรวจกำแพงข้างหน้า")]
    public float wallCheckRadius = 0.4f;
    [Tooltip("ระยะ probe ข้างหน้า")]
    public float wallCheckDistance = 0.6f;

    [Header("Pathfinding")]
    [Tooltip("ใช้ Flow Field (LoL Swarm style) — sample direction จาก FlowFieldPathfinder.Instance\n" +
             "ปิด = เดินตรงเข้าหา player + wall slide เป็น fallback")]
    public bool useFlowField = true;

    [Header("Steering Behaviors")]
    [Tooltip("น้ำหนักของแรงผลักแยกห่างจากศัตรูตัวอื่น (0 = ไม่ผลัก, 1 = ผลักแรงสุด)")]
    [Range(0f, 1f)]
    public float separationWeight = 0.3f;
    [Tooltip("รัศมีตรวจสอบเพื่อผลักมอนสเตอร์ตัวอื่นออก")]
    public float separationRadius = 1.2f;

    [Header("Contact Damage")]
    public float contactDamage  = 10f;
    public float damageCooldown = 1f;
    [Tooltip("ระยะที่ถือว่าชนกับผู้เล่น (แทน OnTriggerStay)")]
    public float damageRadius   = 1.2f;

    [Header("Health")]
    public float maxHealth = 30f;
    public UnityEvent onDeath;

    [Header("VFX")]
    [Tooltip("VFX ตอนตาย (ADR-006) — ว่าง = fallback ไปใช้ VFXDatabase.entries key \"EnemyDeath\" ตัวเดิม\n" +
             "(prefab ทุกตัวที่ยังไม่ตั้งช่องนี้จะเห็นเตือนใน Console ครั้งเดียว — ต้องให้ designer ลากใส่ทีหลัง)")]
    public VFXAsset deathVfx;

    [Header("Experience")]
    public float     expReward   = 10f;
    public GameObject expOrbPrefab;

    // ── Network State ─────────────────────────────────────────────────────
    public NetworkVariable<float> netHealth = new NetworkVariable<float>(
        30f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>maxHealth ที่ replicate แล้ว — client ต้องอ่านตัวนี้ ห้ามอ่าน field maxHealth
    /// เพราะ ApplyWaveScaling / Elite แก้ field เฉพาะฝั่ง server</summary>
    public NetworkVariable<float> netMaxHealth = new NetworkVariable<float>(
        30f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Transform  currentTarget;
    private playermove targetPlayerMove;  // cached — avoid GetComponent allocation per damage tick
    private float      damageTimer;
    private Rigidbody  rb;
    private MeshRenderer _meshRenderer;
    private MaterialPropertyBlock _mpb;

    // ── Pathfinding Waypoints (Waypoint Interpolation) ──────────────────
    private List<Vector3> _pathWaypoints = new List<Vector3>();
    private int           _currentWaypointIndex = 0;
    private float         _nextPathUpdateTime = 0f;
    private const float   PATH_UPDATE_INTERVAL = 0.4f; // อัปเดตทุก 0.4 วินาที

    private static readonly Collider[] _separationBuffer = new Collider[16];


    // ── Lifecycle ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        ActiveEnemies.Add(this);

        rb = GetComponent<Rigidbody>();
        _meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (rb != null)
        {
            // non-kinematic + Discrete — ถูกกว่า ContinuousDynamic ~4× สำหรับ enemy เดินช้า
            // (ถ้า tunnel ทะลุ wall บางที → ลอง ContinuousSpeculative)
            rb.isKinematic            = false;
            rb.useGravity             = false;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.interpolation          = RigidbodyInterpolation.Interpolate;   // smooth visual บน client
        }

        if (!IsServer) return;
        netMaxHealth.Value = maxHealth;
        netHealth.Value    = maxHealth;
        currentTarget   = FindNearestPlayer();
        _nextPathUpdateTime = Time.time + Random.Range(0f, PATH_UPDATE_INTERVAL);
    }


    public override void OnNetworkDespawn()
    {
        ActiveEnemies.Remove(this);
    }

    public override void OnDestroy()
    {
        ActiveEnemies.Remove(this);
        base.OnDestroy();
    }

    // ── Update: Server only (targeting + damage) ──────────────────────────
    void Update()
    {
        if (!IsServer) return;

        // Re-target ทุก 60 frame (~1 วินาที) — และ cache playermove ref ตอน retarget
        if (Time.frameCount % 60 == 0 || currentTarget == null)
        {
            currentTarget    = FindNearestPlayer();
            targetPlayerMove = currentTarget != null ? currentTarget.GetComponent<playermove>() : null;
        }

        if (currentTarget != null)
        {
            // Distance-based damage (ไม่ต้องใช้ trigger collider)
            damageTimer += Time.deltaTime;
            if (damageTimer >= damageCooldown)
            {
                // sqrMagnitude เร็วกว่า Vector3.Distance (~3×) — เลี่ยง sqrt
                float distSq = (transform.position - currentTarget.position).sqrMagnitude;
                if (distSq <= damageRadius * damageRadius)
                {
                    // cached ref — กัน GetComponent alloc per tick
                    if (targetPlayerMove != null) targetPlayerMove.TakeDamage(contactDamage);
                    damageTimer = 0f;
                }
            }
        }
    }

    // ── Knock Up (Server-side CC) ────────────────────────────────────────
    private bool  isKnockedUp = false;
    private float knockUpTimer = 0f;
    private float knockUpDuration = 0f;
    private float knockUpSpeedY = 0f;
    private float gravityY = -9.81f;

    public void ApplyKnockUp(float force, float duration)
    {
        if (!IsServer) return;
        isKnockedUp = true;
        knockUpDuration = duration;
        knockUpTimer = 0f;
        knockUpSpeedY = force;
    }

    // ── FixedUpdate: Server only (movement with physics) ────────────────
    void FixedUpdate()
    {
        if (!IsServer || currentTarget == null) return;

        if (isKnockedUp)
        {
            knockUpTimer += Time.fixedDeltaTime;
            if (knockUpTimer >= knockUpDuration)
            {
                isKnockedUp = false;
                var p = transform.position;
                p.y = 0f;
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.MovePosition(p);
                }
                else
                {
                    transform.position = p;
                }
            }
            else
            {
                knockUpSpeedY += gravityY * Time.fixedDeltaTime;
                var p = transform.position;
                p.y += knockUpSpeedY * Time.fixedDeltaTime;
                if (p.y < 0f) p.y = 0f;

                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.MovePosition(p);
                }
                else
                {
                    transform.position = p;
                }
            }
            return;
        }

        if (suppressDefaultMovement) return;

        Vector3 dir          = Vector3.zero;
        bool    flowProvided = false;

        if (useFlowField && FlowFieldPathfinder.Instance != null)
        {
            // 1) อัปเดตเส้นทาง Waypoints (กระจายโหลดเฟรม)
            if (Time.time >= _nextPathUpdateTime || _pathWaypoints == null || _pathWaypoints.Count == 0)
            {
                _pathWaypoints = FlowFieldPathfinder.Instance.GetPathWaypoints(transform.position, 15);
                _currentWaypointIndex = 0;
                _nextPathUpdateTime = Time.time + PATH_UPDATE_INTERVAL + Random.Range(-0.05f, 0.05f);
            }

            // 2) เคลื่อนที่ไปตามลำดับ Waypoints
            if (_pathWaypoints != null && _currentWaypointIndex < _pathWaypoints.Count)
            {
                Vector3 targetWp = _pathWaypoints[_currentWaypointIndex];
                Vector3 toWp = targetWp - transform.position;
                toWp.y = 0f;

                // เช็คว่าชน/ถึงจุดนำทางหรือยัง (ระยะทางที่ 0.45 เมตร)
                if (toWp.sqrMagnitude < 0.2f)
                {
                    _currentWaypointIndex++;
                    if (_currentWaypointIndex < _pathWaypoints.Count)
                    {
                        targetWp = _pathWaypoints[_currentWaypointIndex];
                        toWp = targetWp - transform.position;
                        toWp.y = 0f;
                    }
                }

                dir = toWp;
                flowProvided = dir.sqrMagnitude > 0.01f;
            }
        }

        // Fallback: direct toward target ถ้า flow field ไม่ครอบคลุม (นอก grid หรือใกล้เป้าหมายจนหมดจุดนำทาง)
        if (!flowProvided)
            dir = currentTarget.position - transform.position;

        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;
        dir.Normalize();

        // 3) คำนวณ Separation Force (แรงผลักหลบเพื่อน) เพื่อเกลี่ยฝูงยูนิตไม่ให้อัดทับกัน
        if (separationWeight > 0.001f && separationRadius > 0.01f)
        {
            Vector3 separationForce = Vector3.zero;
            int numNeighbors = Physics.OverlapSphereNonAlloc(
                transform.position, 
                separationRadius, 
                _separationBuffer, 
                LayerMask.GetMask("Enemy"),
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < numNeighbors; i++)
            {
                Collider c = _separationBuffer[i];
                if (c == null || c.gameObject == gameObject) continue;

                Vector3 diff = transform.position - c.transform.position;
                diff.y = 0f;
                float distSq = diff.sqrMagnitude;
                if (distSq > 0.0001f && distSq < separationRadius * separationRadius)
                {
                    float dist = Mathf.Sqrt(distSq);
                    separationForce += diff.normalized / dist;
                }
            }

            if (separationForce.sqrMagnitude > 0.01f)
            {
                dir = (dir * (1f - separationWeight)) + (separationForce.normalized * separationWeight);
                dir.Normalize();
            }
        }

        // Wall slide: ใช้เป็น safety net (flow field กว้างกว่า collider ของ obstacle อาจมีช่องว่าง)
        // ถ้า flow field พา enemy ไปทางที่ปลอดภัยอยู่แล้ว wall slide ก็ no-op
        Vector3 finalDir = ResolveWallSlide(dir);

        // หมุนตัวยูนิตเข้าหาทิศทางเคลื่อนที่จริง (finalDir)
        if (finalDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(finalDir, Vector3.up);
            Quaternion nextRot = targetRot;

            if (rotationSpeed > 0f)
            {
                nextRot = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
            }

            if (rb != null)
            {
                rb.MoveRotation(nextRot);
            }
            else
            {
                transform.rotation = nextRot;
            }
        }


        Vector3 move = finalDir * speed * _speedMultiplier * Time.fixedDeltaTime;
        Vector3 next = transform.position + move;

        if (rb != null)
        {
            // ล้างความเร็วและแรงหมุนตกค้างจากฟิสิกส์ชนในเฟรมก่อน เพื่อป้องกันแรงเฉื่อยสะสมต้านการเดิน
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.MovePosition(next);
        }
        else
        {
            transform.position = next;
        }
    }



    /// <summary>
    /// ถ้ามีกำแพงข้างหน้า → ฉาย direction บน wall plane เพื่อให้ enemy ไถลตามกำแพง
    /// ลอง slide ทั้งซ้ายและขวา เลือกอันที่เข้าใกล้ player มากกว่า
    /// </summary>
    Vector3 ResolveWallSlide(Vector3 dir)
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        if (!Physics.SphereCast(origin, wallCheckRadius, dir, out RaycastHit hit,
                                wallCheckDistance, wallLayer, QueryTriggerInteraction.Ignore))
            return dir;   // ทางสะดวก

        // ข้าม collider ของผู้เล่น/ศัตรูเอง
        if (hit.collider.GetComponent<playermove>() != null) return dir;
        if (hit.collider.GetComponent<Enemy>()       != null) return dir;

        // โปรเจกต์ direction บน wall plane — ลบส่วนที่ pushed เข้ากำแพง
        Vector3 wallNormal = hit.normal; wallNormal.y = 0f;
        if (wallNormal.sqrMagnitude < 0.001f) return dir;
        wallNormal.Normalize();

        Vector3 slid = Vector3.ProjectOnPlane(dir, wallNormal);
        slid.y = 0f;
        if (slid.sqrMagnitude < 0.001f) return Vector3.zero;
        return slid.normalized;
    }

    // ── Invincibility (Server-side) ───────────────────────────────────────
    /// <summary>Server flag — บอสใช้ระหว่าง phase transition</summary>
    [HideInInspector] public bool serverInvincible = false;

    // ── Damage ────────────────────────────────────────────────────────────
    public void EnemyTakeDamage(float amount, bool isCrit = false)
    {
        if (!IsServer) return;
        if (serverInvincible) return;   // skip damage ระหว่าง phase transition

        netHealth.Value = Mathf.Max(0f, netHealth.Value - amount);
        NotifyHitClientRpc(transform.position, isCrit, amount);
        if (netHealth.Value > 0f) return;

        onDeath.Invoke();
        OnEnemyDiedServer?.Invoke(this, transform.position);
        SpawnExpOrb();
        NotifyDeathClientRpc(transform.position);
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
    }

    // ── Burn DOT (Server-side) ────────────────────────────────────────────
    private Coroutine burnCoroutine;
    private float     activeBurnEndTime;

    public void ApplyBurnDot(float duration, float damagePerTick, float tickInterval, bool isCrit, string weaponName, PlayerWeaponManager manager)
    {
        if (!IsServer) return;

        float newEndTime = Time.time + duration;
        if (burnCoroutine != null)
        {
            if (newEndTime > activeBurnEndTime)
            {
                activeBurnEndTime = newEndTime;
            }
            return;
        }

        activeBurnEndTime = newEndTime;
        burnCoroutine     = StartCoroutine(BurnRoutine(damagePerTick, tickInterval, isCrit, weaponName, manager));
    }

    private System.Collections.IEnumerator BurnRoutine(float damagePerTick, float tickInterval, bool isCrit, string weaponName, PlayerWeaponManager manager)
    {
        while (Time.time < activeBurnEndTime)
        {
            yield return new WaitForSeconds(tickInterval);
            if (!NetworkObject.IsSpawned || netHealth.Value <= 0f) break;

            EnemyTakeDamage(damagePerTick, isCrit);
            if (manager != null)
            {
                manager.RegisterWeaponDamage(weaponName, damagePerTick);
            }
        }
        burnCoroutine = null;
    }

    // ── Slow / Freeze Debuff (Server-side) ────────────────────────────────
    /// <summary>Speed multiplier ชั่วคราว — 1 = ปกติ, 0.5 = slow 50%, 0 = freeze</summary>
    private float     _speedMultiplier = 1f;
    private Coroutine _slowCoroutine;
    private float     _slowEndTime;
    private Coroutine _freezeCoroutine;
    private float     _freezeEndTime;

    /// <summary>ลดความเร็ว enemy ชั่วคราว — Server only
    /// slowPercent: 0.5 = ลดเหลือ 50% speed, 0.3 = ลดเหลือ 30%</summary>
    public void ApplySlowDebuff(float duration, float slowPercent)
    {
        if (!IsServer) return;

        float newEnd = Time.time + duration;
        if (_slowCoroutine != null)
        {
            // ต่ออายุถ้า duration ใหม่ยาวกว่า
            if (newEnd > _slowEndTime) _slowEndTime = newEnd;
            // ใช้ slow ที่แรงกว่า (ค่าต่ำกว่า = ช้ากว่า)
            float currentSlow = _speedMultiplier;
            if (slowPercent < currentSlow) _speedMultiplier = slowPercent;
            return;
        }

        _slowEndTime     = newEnd;
        _speedMultiplier = Mathf.Clamp01(slowPercent);
        _slowCoroutine   = StartCoroutine(SlowRoutine());
    }

    private IEnumerator SlowRoutine()
    {
        while (Time.time < _slowEndTime)
            yield return null;

        _speedMultiplier = 1f;
        _slowCoroutine   = null;
    }

    /// <summary>แช่แข็ง enemy — หยุดเคลื่อนที่ทั้งหมด ชั่วคราว — Server only</summary>
    public void ApplyFreeze(float duration)
    {
        if (!IsServer) return;

        float newEnd = Time.time + duration;
        if (_freezeCoroutine != null)
        {
            if (newEnd > _freezeEndTime) _freezeEndTime = newEnd;
            return;
        }

        _freezeEndTime = newEnd;
        // หยุด slow ถ้ามี (freeze แรงกว่า)
        if (_slowCoroutine != null) { StopCoroutine(_slowCoroutine); _slowCoroutine = null; }
        _speedMultiplier         = 0f;
        suppressDefaultMovement  = true;
        _freezeCoroutine         = StartCoroutine(FreezeRoutine());
        NotifyFreezeClientRpc(true);
    }

    private IEnumerator FreezeRoutine()
    {
        // หยุด velocity ขณะ freeze
        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }

        while (Time.time < _freezeEndTime)
            yield return null;

        suppressDefaultMovement = false;
        _speedMultiplier        = 1f;
        _freezeCoroutine        = null;
        NotifyFreezeClientRpc(false);
    }

    [ClientRpc]
    void NotifyFreezeClientRpc(bool frozen)
    {
        // Visual feedback: เปลี่ยนสี / material ชั่วคราว (optional — designer implement ทีหลัง)
        if (_meshRenderer == null) _meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (_meshRenderer != null)
        {
            _mpb ??= new MaterialPropertyBlock();
            _meshRenderer.GetPropertyBlock(_mpb);
            Color freezeColor = frozen ? new Color(0.5f, 0.8f, 1f, 1f) : Color.white;
            var sharedMat = _meshRenderer.sharedMaterial;
            if (sharedMat != null)
            {
                if (sharedMat.HasProperty("_BaseColor")) _mpb.SetColor("_BaseColor", freezeColor);
                if (sharedMat.HasProperty("_Color"))     _mpb.SetColor("_Color",     freezeColor);
            }
            _meshRenderer.SetPropertyBlock(_mpb);
        }
    }

    // ── Drop ExpOrb ───────────────────────────────────────────────────────
    void SpawnExpOrb()
    {
        if (expOrbPrefab != null)
        {
            GameObject orb = Instantiate(expOrbPrefab, transform.position, Quaternion.identity);
            orb.GetComponent<NetworkObject>()?.Spawn(true);
            orb.GetComponent<ExpOrb>()?.SetExpAmount(expReward);
        }
        else
        {
            // Fallback: ให้ EXP ตรงกับ SharedExperienceManager
            SharedExperienceManager.Instance?.AddExp(expReward);
        }
    }

    // ── Find Nearest Player ───────────────────────────────────────────────
    Transform FindNearestPlayer()
    {
        if (NetworkManager.Singleton == null) return null;

        Transform nearest = null;
        float     minDist = float.MaxValue;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var playerObj = client.PlayerObject;
            if (playerObj == null) continue;
            var pm = playerObj.GetComponent<playermove>();
            if (pm != null && pm.isDead.Value) continue;   // ข้ามผู้เล่นที่ตายแล้ว

            float dist = Vector3.Distance(transform.position, playerObj.transform.position);
            if (dist < minDist) { minDist = dist; nearest = playerObj.transform; }
        }
        return nearest;
    }

    /// <summary>
    /// เรียกจาก EnemySpawner หลัง Spawn — คูณ stats ตาม wave
    /// </summary>
    /// <param name="healthMult">HP multiplier (1 + wave × healthMultPerWave)</param>
    /// <param name="speedMult">Speed multiplier</param>
    /// <param name="expMult">EXP reward multiplier (1 + wave × expMultPerWave) — ตั้งค่าได้ใน WaveManager</param>
    public void ApplyWaveScaling(float healthMult, float speedMult, float expMult = 1f)
    {
        if (!IsServer) return;
        ServerSetMaxHealth(maxHealth * healthMult);
        netHealth.Value = maxHealth;
        speed           = speed * speedMult;
        expReward       = expReward * expMult;
    }

    /// <summary>ตั้ง maxHealth ฝั่ง server พร้อม replicate — ใช้ตัวนี้เสมอ ห้ามเขียน maxHealth ตรงๆ
    /// จากภายนอก ไม่งั้น client จะได้ค่าเก่า</summary>
    public void ServerSetMaxHealth(float value)
    {
        if (!IsServer) return;
        maxHealth          = value;
        netMaxHealth.Value = value;
    }

    [ClientRpc]
    void NotifyHitClientRpc(Vector3 pos, bool isCrit, float damage)
    {
        OnAnyEnemyHit?.Invoke();
        string hitType = isCrit ? "CritHitEffect" : "HitEffect";
        NetworkedVFXPool.Instance?.PlayByName(hitType, pos);
        NetworkedVFXPool.Instance?.PlayDamageNumber(pos, damage, isCrit);
    }

    /// <summary>เตือนครั้งเดียวทั้งเกม (ไม่ใช่ต่อ instance) — enemy ตายเป็นร้อยตัวต่อรอบ
    /// เตือนทุกครั้งจะถล่ม Console จนอ่านอะไรไม่ออก (ADR-006)</summary>
    private static bool _warnedMissingDeathVfx;

    [ClientRpc]
    void NotifyDeathClientRpc(Vector3 deathPos)
    {
        OnAnyEnemyDied?.Invoke();
        OnAnyEnemyDiedAt?.Invoke(deathPos);

        // ADR-006: ลาก VFXAsset ตรงๆ ได้แล้ว — แต่ Enemy อยู่บน prefab จำนวนมาก field ใหม่
        // จะเป็น null บนทุก prefab ที่ยังไม่ได้แก้ไข ต้อง fallback ไปทาง string key เดิมไม่งั้น
        // VFX ตายจะหายไปเงียบๆ ทั้งเกมจนกว่า designer จะไล่เติมให้ครบ
        if (deathVfx != null && NetworkedVFXPool.Instance != null && NetworkedVFXPool.Instance.vfxDatabase != null)
        {
            int id = NetworkedVFXPool.Instance.vfxDatabase.GetIdForAsset(deathVfx);
            NetworkedVFXPool.Instance.PlayById(id, deathPos);
        }
        else
        {
            if (!_warnedMissingDeathVfx)
            {
                _warnedMissingDeathVfx = true;
                Debug.LogWarning($"[Enemy] '{name}' ไม่ได้ตั้ง deathVfx (ADR-006) — ใช้ VFXDatabase.entries key \"EnemyDeath\" ตัวเดิมไปก่อน · " +
                                  "เตือนครั้งเดียวทั้งเกม ไม่ใช่ต่อตัว — ต้องให้ designer ไล่เติม deathVfx บน prefab enemy ทุกตัว");
            }
            NetworkedVFXPool.Instance?.PlayByName("EnemyDeath", deathPos);
        }
    }

    public float GetHealthPercent() => netMaxHealth.Value > 0f ? netHealth.Value / netMaxHealth.Value : 0f;
    public float GetCurrentHealth() => netHealth.Value;
}
