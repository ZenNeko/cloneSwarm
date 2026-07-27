using UnityEngine;

public class DestructibleCrate : Enemy
{
    [Header("Loot Drop Settings")]
    [Tooltip("ตารางดรอปไอเทมที่เป็น ScriptableObject (CloneSwarm/Loot Drop Table)")]
    public LootDropTable lootDropTable;

    [Tooltip("จุดที่ต้องการให้ไอเทมดรอปออกมาระหว่างพัง (ปล่อยว่างจะดรอปที่จุดศูนย์กลางของกล่อง)")]
    public Transform lootDropPoint;

    private void Update() {}
    private void FixedUpdate() {}

    public override void OnNetworkSpawn()
    {
        // บันทึกการตั้งค่า Rigidbody ดั้งเดิมจาก Prefab ก่อนโดนคลาสแม่เขียนทับ
        var rb = GetComponent<Rigidbody>();
        bool savedKinematic = false;
        bool savedGravity = false;
        CollisionDetectionMode savedCollisionMode = CollisionDetectionMode.Discrete;
        RigidbodyInterpolation savedInterpolation = RigidbodyInterpolation.None;
        bool hasRb = rb != null;

        if (hasRb)
        {
            savedKinematic = rb.isKinematic;
            savedGravity   = rb.useGravity;
            savedCollisionMode = rb.collisionDetectionMode;
            savedInterpolation = rb.interpolation;
        }

        // เรียกใช้งานฟังก์ชันพื้นฐานของคลาสแม่ (สปอว์น/ซิงค์เลือดและลงทะเบียน ActiveEnemies)
        base.OnNetworkSpawn();

        // คืนค่า Rigidbody ของ Prefab ดั้งเดิม
        if (hasRb)
        {
            rb.isKinematic = savedKinematic;
            rb.useGravity  = savedGravity;
            rb.collisionDetectionMode = savedCollisionMode;
            rb.interpolation = savedInterpolation;
        }

        // เชื่อมระบบดรอปของ LootDropTable เข้ากับ Event onDeath อัตโนมัติบน Server
        // ส่งตำแหน่งจริงของกล่องไม้ขณะถูกทำลาย (transform.position) ไปให้สัญญาณเพื่อป้องกันข้อผิดพลาดทางตำแหน่ง
        if (IsServer)
        {
            onDeath.AddListener(OnDeathDrop);
        }
    }

    private void OnDeathDrop()
    {
        if (lootDropTable != null)
        {
            Vector3 dropPosition = lootDropPoint != null ? lootDropPoint.position : transform.position;
            lootDropTable.TriggerDrop(dropPosition);
        }
    }
}
