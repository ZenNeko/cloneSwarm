using UnityEngine;

public class ExpOrb : MonoBehaviour
{
    [Header("EXP")]
    public float expAmount = 10f;

    [Header("Pickup")]
    [Tooltip("ระยะ base ที่ orb เริ่มวิ่งเข้าหา player (ถ้าไม่มี PlayerWeapon)")]
    public float attractRadius = 4f;
    [Tooltip("อัตราส่วน attractRadius ต่อ AttackRange ของ PlayerWeapon (0 = ใช้ attractRadius คงที่)")]
    public float attractRadiusRatio = 0.5f;
    [Tooltip("ความเร็วที่ orb วิ่งเข้าหา player")]
    public float moveSpeed = 8f;
    [Tooltip("ระยะที่ถือว่า 'เก็บได้' และให้ EXP ทันที")]
    public float pickupRadius = 0.4f;

    [Header("Bob Animation")]
    [Tooltip("ความสูงที่ลอยขึ้นลง")]
    public float bobHeight = 0.2f;
    [Tooltip("ความเร็วการลอย")]
    public float bobSpeed = 2f;

    private Transform    player;
    private PlayerWeapon playerWeapon;
    private Vector3      startPos;
    private bool         isAttracting;

    // ──────────────────────────────────────────────
    //  Init — เรียกจาก Enemy ตอน spawn
    // ──────────────────────────────────────────────
    public void Init(Transform playerTransform, float exp)
    {
        player       = playerTransform;
        expAmount    = exp;
        startPos     = transform.position;

        // หา PlayerWeapon เพื่ออ่าน attackRange แบบ dynamic
        playerWeapon = playerTransform.GetComponentInChildren<PlayerWeapon>()
                    ?? playerTransform.GetComponentInParent<PlayerWeapon>();
    }

    // ──────────────────────────────────────────────
    //  คำนวณ attractRadius จาก AttackRange ของ player
    // ──────────────────────────────────────────────
    float GetAttractRadius()
    {
        if (attractRadiusRatio > 0f && playerWeapon != null)
            return playerWeapon.attackRange * attractRadiusRatio;
        return attractRadius;
    }

    // ──────────────────────────────────────────────
    //  Update
    // ──────────────────────────────────────────────
    void Update()
    {
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);

        if (dist <= pickupRadius)
        {
            Collect();
            return;
        }

        if (dist <= GetAttractRadius())
        {
            // วิ่งเข้าหา player
            isAttracting = true;
            transform.position = Vector3.MoveTowards(
                transform.position,
                player.position,
                moveSpeed * Time.deltaTime
            );
        }
        else
        {
            // ลอยขึ้นลง (bob) รอ player เข้ามา
            isAttracting = false;
            float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(startPos.x, newY, startPos.z);
        }
    }

    // ──────────────────────────────────────────────
    //  Fallback — เก็บด้วย Trigger (ถ้า Collider ตั้งเป็น IsTrigger)
    // ──────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            Collect();
    }

    // ──────────────────────────────────────────────
    //  Collect
    // ──────────────────────────────────────────────
    void Collect()
    {
        if (ExperienceManager.Instance != null)
            ExperienceManager.Instance.AddExp(expAmount);

        Destroy(gameObject);
    }

    // ──────────────────────────────────────────────
    //  Gizmo
    // ──────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, GetAttractRadius());
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}
