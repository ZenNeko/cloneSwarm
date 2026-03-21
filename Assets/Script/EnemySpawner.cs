using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab; // Prefab ของศัตรู
    public Transform player; // อ้างอิงตัวผู้เล่น
    public float spawnRate = 1f; // ระยะเวลาในการเกิดศัตรู (วินาที/ตัว)
    public float spawnRadius = 10f; // ระยะห่างจากผู้เล่นที่จะให้ศัตรูเกิด

    void Start()
    {
        // สั่งให้เริ่มเรียกฟังก์ชัน SpawnEnemy วนซ้ำไปเรื่อยๆ
        InvokeRepeating("SpawnEnemy", 0f, spawnRate);
    }

    void SpawnEnemy()
    {
        if (player == null) return;

        // สุ่มตำแหน่งแบบวงกลม 2D (แกน X และ Z สำหรับเกม 3D) รอบๆ ผู้เล่น
        Vector2 randomCircle = Random.insideUnitCircle.normalized;
        
        // แปลงให้อยู่ในระนาบ X-Z แทนที่จะเป็น X-Y (ซึ่งทำให้ศัตรูเกิดในอากาศ)
        Vector3 randomDirection = new Vector3(randomCircle.x, 0f, randomCircle.y);
        
        // กำหนดตำแหน่งที่จะเกิดโดยอ้างอิงความสูง (Y) จากตัวผู้เล่น
        Vector3 spawnPosition = player.position + (randomDirection * spawnRadius);

        // สร้างศัตรูใหม่
        GameObject newEnemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
        
        // กำหนดเป้าหมายให้ศัตรูตัวใหม่รู้ว่าใครคือผู้เล่น
        Enemy enemyScript = newEnemy.GetComponent<Enemy>();
        if (enemyScript != null)
        {
            enemyScript.player = player;
        }
    }

    // แสดงเส้น Gizmos ในหน้าต่าง Scene เพื่อให้เห็นระยะการเกิดของศัตรู
    void OnDrawGizmosSelected()
    {
        Vector3 center = player != null ? player.position : transform.position;

#if UNITY_EDITOR
        // วาดเส้นวงกลมแบบแบน (Circle) บนระนาบพื้น (หันหน้าขึ้นตามแกน Y)
        Handles.color = Color.red;
        Handles.DrawWireDisc(center, Vector3.up, spawnRadius);
#endif
    }
}
