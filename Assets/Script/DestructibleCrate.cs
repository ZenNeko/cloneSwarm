using UnityEngine;

public class DestructibleCrate : Enemy
{
    [Header("Loot Drop Settings")]
    [Tooltip("ตารางดรอปไอเทมที่เป็น ScriptableObject (CloneSwarm/Loot Drop Table)")]
    public LootDropTable lootDropTable;

    // บดบัง (Shadow) Unity lifecycle methods ของคลาสแม่ (Enemy.cs)
    // เพื่อยกเลิกการวิ่งหาผู้เล่น การเดิน และระบบสร้างความเสียหายเมื่อเดินชน
    private new void Update() {}
    private new void FixedUpdate() {}

    public override void OnNetworkSpawn()
    {
        // เรียกใช้งานฟังก์ชันพื้นฐานของคลาสแม่ (สปอว์น/ซิงค์เลือดและอื่นๆ)
        base.OnNetworkSpawn();

        // บังคับให้ Rigidbody เป็น kinematic เสมอเพื่อล็อคตำแหน่งกล่อง
        // ไม่ให้ขยับเขยื้อนเมื่อโดนเบียดโดยผู้เล่นหรือมอนสเตอร์ตัวอื่น
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
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
            lootDropTable.TriggerDrop(transform.position);
        }
    }
}
