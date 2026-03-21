using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 12f;
    public float damage = 20f;
    public float maxRange = 20f;

    private Transform target;
    private Vector3 startPosition;

    public void Init(Transform target)
    {
        this.target = target;
        startPosition = transform.position;
    }

    void Update()
    {
        // ถ้า target ตายไปแล้ว ให้ลบ projectile
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);

        // ลบถ้าเกินระยะ
        if (Vector3.Distance(startPosition, transform.position) >= maxRange)
        {
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            Health health = other.GetComponent<Health>();
            if (health != null)
                health.TakeDamage(damage);

            Destroy(gameObject);
        }
    }
}
