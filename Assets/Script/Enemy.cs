using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
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
    public static readonly List<Enemy> ActiveEnemies = new List<Enemy>();

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

    [Header("Experience")]
    public float     expReward   = 10f;
    public GameObject expOrbPrefab;

    // ── Network State ─────────────────────────────────────────────────────
    public NetworkVariable<float> netHealth = new NetworkVariable<float>(
        30f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Transform  currentTarget;
    private playermove targetPlayerMove;  // cached — avoid GetComponent allocation per damage tick
    private float      damageTimer;
    private Rigidbody  rb;

    // ── Pathfinding Waypoints (Waypoint Interpolation) ──────────────────
    private List<Vector3> _pathWaypoints = new List<Vector3>();
    private int           _currentWaypointIndex = 0;
    private float         _nextPathUpdateTime = 0f;
    private const float   PATH_UPDATE_INTERVAL = 0.4f; // อัปเดตทุก 0.4 วินาที

    private static readonly Collider[] _separationBuffer = new Collider[16];


    // ── Lifecycle ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (!ActiveEnemies.Contains(this))
        {
            ActiveEnemies.Add(this);
        }

        rb = GetComponent<Rigidbody>();
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
        netHealth.Value = maxHealth;
        currentTarget   = FindNearestPlayer();
        _nextPathUpdateTime = Time.time + Random.Range(0f, PATH_UPDATE_INTERVAL);
    }


    public override void OnNetworkDespawn()
    {
        ActiveEnemies.Remove(this);
    }

    private void OnDestroy()
    {
        ActiveEnemies.Remove(this);
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

    // ── FixedUpdate: Server only (movement with physics) ────────────────
    void FixedUpdate()
    {
        if (!IsServer || suppressDefaultMovement || currentTarget == null) return;

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


        Vector3 move = finalDir * speed * Time.fixedDeltaTime;
        Vector3 next = transform.position + move;

        if (rb != null)
        {
            // ล้างความเร็วและแรงหมุนตกค้างจากฟิสิกส์ชนในเฟรมก่อน เพื่อป้องกันแรงเฉื่อยสะสมต้านการเดิน
            rb.velocity = Vector3.zero;
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
        NotifyHitClientRpc(transform.position, isCrit);
        if (netHealth.Value > 0f) return;

        onDeath.Invoke();
        OnEnemyDiedServer?.Invoke(this, transform.position);
        SpawnExpOrb();
        NotifyDeathClientRpc(transform.position);
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        else Destroy(gameObject);
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
            // Fallback: ให้ EXP ตรงกับ player ที่ใกล้ที่สุด
            currentTarget?.GetComponent<ExperienceManager>()?.AddExp(expReward);
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
        maxHealth       = maxHealth * healthMult;
        netHealth.Value = maxHealth;
        speed           = speed * speedMult;
        expReward       = expReward * expMult;
    }

    [ClientRpc]
    void NotifyHitClientRpc(Vector3 pos, bool isCrit)
    {
        OnAnyEnemyHit?.Invoke();
        string hitType = isCrit ? "CritHitEffect" : "HitEffect";
        NetworkedVFXPool.Instance?.PlayByName(hitType, pos);
    }

    [ClientRpc]
    void NotifyDeathClientRpc(Vector3 deathPos)
    {
        OnAnyEnemyDied?.Invoke();
        OnAnyEnemyDiedAt?.Invoke(deathPos);
        NetworkedVFXPool.Instance?.PlayByName("EnemyDeath", deathPos);
    }

    public float GetHealthPercent() => netHealth.Value / maxHealth;
    public float GetCurrentHealth() => netHealth.Value;
}
