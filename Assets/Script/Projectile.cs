using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 12f;
    public float damage = 20f;
    public float maxRange = 20f;

    private Transform target;
    private Vector3   moveDirection;   // ใช้เมื่อไม่มี target (กระสุนแตกข้าง)
    private Vector3   startPosition;

    /// <summary>Homing mode — ติดตาม target</summary>
    public void Init(Transform target)
    {
        this.target   = target;
        moveDirection = Vector3.zero;
        startPosition = transform.position;
    }

    /// <summary>Direction mode — บินตรง ไม่ homing</summary>
    public void InitDirection(Vector3 direction)
    {
        moveDirection = direction.normalized;
        target        = null;
        startPosition = transform.position;
    }

    void Update()
    {
        if (moveDirection != Vector3.zero)
        {
            // Direction mode
            transform.position += moveDirection * speed * Time.deltaTime;
        }
        else
        {
            // Homing mode
            if (target == null) { Destroy(gameObject); return; }
            transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);
        }

        if (Vector3.Distance(startPosition, transform.position) >= maxRange)
            Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            Enemy enemy = other.GetComponent<Enemy>();
            if (enemy != null)
                enemy.EnemyTakeDamage(damage);

            Destroy(gameObject);
        }
    }
}
