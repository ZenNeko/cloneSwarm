using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

public class playermove : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Health")]
    public float maxHealth = 100f;
    public UnityEvent onDeath;

    [Header("Health Regen")]
    [Tooltip("HP ที่ฟื้นต่อวินาที (0 = ปิด)")]
    public float healthRegenPerSecond = 0f;

    private float currentHealth;
    private Vector2 moveInput;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        currentHealth = maxHealth;
    }

    void Update()
    {
        if (healthRegenPerSecond > 0f && currentHealth < maxHealth)
        {
            currentHealth = Mathf.Min(currentHealth + healthRegenPerSecond * Time.deltaTime, maxHealth);
        }
    }

    void FixedUpdate()
    {
        if (rb != null)
        {
            Vector3 movement = new Vector3(moveInput.x, 0f, moveInput.y);
            rb.velocity = new Vector3(movement.x * moveSpeed, rb.velocity.y, movement.z * moveSpeed);
        }
    }

    public void Move(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>().normalized;
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            onDeath.Invoke();
            Destroy(gameObject);
        }
    }

    /// <summary>เรียกจาก UpgradeManager เมื่อ MaxHealth upgrade — heal ด้วยส่วนต่างด้วย</summary>
    public void GainMaxHealth(float amount)
    {
        maxHealth     += amount;
        currentHealth += amount;   // heal ไปพร้อมกัน
    }

    public float GetHealthPercent() => currentHealth / maxHealth;
    public float GetCurrentHealth() => currentHealth;
}
