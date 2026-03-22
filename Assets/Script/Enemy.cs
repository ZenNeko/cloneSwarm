using UnityEngine;
using UnityEngine.Events;

public class Enemy : MonoBehaviour
{
    [Header("Movement")]
    public Transform player;
    public float speed = 3f;

    [Header("Contact Damage")]
    public float contactDamage = 10f;
    public float damageCooldown = 1f;

    [Header("Health")]
    public float maxHealth = 30f;
    public UnityEvent onDeath;

    [Header("Experience")]
    public float expReward = 10f;
    [Tooltip("Prefab ของ ExpOrb ที่จะ drop เมื่อตาย")]
    public GameObject expOrbPrefab;

    private float currentHealth;
    private float damageTimer;

    void Start()
    {
        currentHealth = maxHealth;
    }

    void Update()
    {
        
        if (player != null)
        {
            transform.position = Vector3.MoveTowards(transform.position, player.position, speed * Time.deltaTime);
        }

        damageTimer += Time.deltaTime;
        
    }

    void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player") && damageTimer >= damageCooldown)
        {
            playermove playerHealth = other.GetComponent<playermove>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(contactDamage);
                damageTimer = 0f;
            }
        }
    }

    public void EnemyTakeDamage(float amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            onDeath.Invoke();

            // Drop ExpOrb ที่ตำแหน่ง enemy ตาย
            if (expOrbPrefab != null)
            {
                GameObject orb = Instantiate(expOrbPrefab, transform.position, Quaternion.identity);
                ExpOrb expOrb = orb.GetComponent<ExpOrb>();
                if (expOrb != null)
                    expOrb.Init(player, expReward);
            }
            else if (ExperienceManager.Instance != null)
            {
                // Fallback: ให้ EXP ตรงๆ ถ้าไม่มี Prefab
                ExperienceManager.Instance.AddExp(expReward);
            }

            Destroy(gameObject);
        }
    }

    public float GetHealthPercent() => currentHealth / maxHealth;
    public float GetCurrentHealth() => currentHealth;
}