using UnityEngine;
using UnityEngine.Events;

public class Health : MonoBehaviour
{
    public float maxHealth = 100f;
    private float currentHealth;

    public UnityEvent onDeath; // ใช้ Inspector กำหนด callback เพิ่มเติมได้

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            Die();
        }
    }

    void Die()
    {
        onDeath.Invoke();
        Destroy(gameObject);
    }

    // คืนค่า 0-1 สำหรับ UI Health Bar
    public float GetHealthPercent() => currentHealth / maxHealth;
    public float GetCurrentHealth() => currentHealth;
}
