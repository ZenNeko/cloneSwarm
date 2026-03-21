using UnityEngine;

public class Enemy : MonoBehaviour
{
    public Transform player; // ตัวแปรอ้างอิงถึงผู้เล่น
    public float speed = 3f; // ความเร็วของศัตรู
    public float contactDamage = 10f; // ดาเมจที่ทำกับผู้เล่นเมื่อสัมผัส
    public float damageCooldown = 1f; // วินาทีระหว่างแต่ละครั้งที่ทำดาเมจ

    private float damageTimer;

    void Update()
    {
        if (player != null)
        {
            // ให้ศัตรูเดินเข้าหาตำแหน่งของผู้เล่น
            transform.position = Vector3.MoveTowards(transform.position, player.position, speed * Time.deltaTime);
        }

        damageTimer += Time.deltaTime;
    }

    void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player") && damageTimer >= damageCooldown)
        {
            Health playerHealth = other.GetComponent<Health>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(contactDamage);
                damageTimer = 0f;
            }
        }
    }
}