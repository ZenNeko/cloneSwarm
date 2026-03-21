using UnityEngine;
using UnityEngine.InputSystem;

public class playermove : MonoBehaviour
{
    public float moveSpeed = 5f;
    private Vector2 moveInput;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (rb != null)
        {
            // แปลง Vector2 (X, Y) จาก Input เป็น Vector3 (X, 0, Z) สำหรับ 3 มิติ
            Vector3 movement = new Vector3(moveInput.x, 0f, moveInput.y);
            
            // ใช้ rb.velocity.y เดิมไว้ เผื่อมีการตกจากที่สูง (Gravity)
            rb.velocity = new Vector3(movement.x * moveSpeed, rb.velocity.y, movement.z * moveSpeed);
        }
    }
    

    public void Move(InputAction.CallbackContext context)
    {
        // อ่านค่า Input เป็น Vector2
        moveInput = context.ReadValue<Vector2>().normalized;
    }
}
