using Unity.Netcode;
using UnityEngine;

public class OrbDropManager : MonoBehaviour
{
    public static OrbDropManager Instance { get; private set; }

    [Header("Prefabs")]
    public GameObject healingOrbPrefab;
    public GameObject magnetOrbPrefab;

    [Header("Drop Rates (0.01 = 1%)")]
    [Range(0f, 1f)]
    public float healingDropRate = 0.04f;
    [Range(0f, 1f)]
    public float magnetDropRate = 0.01f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        Enemy.OnEnemyDiedServer += HandleEnemyDiedServer;
    }

    private void OnDisable()
    {
        Enemy.OnEnemyDiedServer -= HandleEnemyDiedServer;
    }

    private void HandleEnemyDiedServer(Enemy enemy, Vector3 deathPosition)
    {
        // ทำงานเฉพาะบน Server เท่านั้น
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        // ข้ามการสปอว์นหากผู้ตายคือกล่องไม้
        if (enemy is DestructibleCrate)
            return;

        float roll = Random.value;

        // ตรวจสอบ Magnet ก่อน (มีโอกาสออกยากกว่า)
        if (roll <= magnetDropRate)
        {
            SpawnOrb(magnetOrbPrefab, deathPosition);
        }
        else if (roll <= magnetDropRate + healingDropRate)
        {
            SpawnOrb(healingOrbPrefab, deathPosition);
        }
    }

    private void SpawnOrb(GameObject prefab, Vector3 position)
    {
        if (prefab == null) return;

        // สุ่มขยับตำแหน่งเล็กน้อยรอบๆ จุดตาย (แนวระนาบ XZ)
        Vector2 offset2D = Random.insideUnitCircle * 0.5f;
        Vector3 spawnPos = position + new Vector3(offset2D.x, 0.1f, offset2D.y);

        GameObject orb = Instantiate(prefab, spawnPos, Quaternion.identity);
        
        var netObj = orb.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn(true);
        }
        else
        {
            Debug.LogError($"[OrbDropManager] '{orb.name}' ไม่มี NetworkObject — spawn เฉพาะฝั่ง server client จะไม่เห็น");
        }
    }
}
