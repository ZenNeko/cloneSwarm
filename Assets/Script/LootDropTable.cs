using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[System.Serializable]
public class LootEntry
{
    [Tooltip("Prefab ของไอเทมที่จะดรอป (ต้องมี NetworkObject)")]
    public GameObject prefab;

    [Tooltip("ค่าน้ำหนักโอกาสดรอป ยิ่งค่าเยอะยิ่งสุ่มเจอออกง่าย")]
    public int weight = 1;
}

[CreateAssetMenu(fileName = "NewLootDropTable", menuName = "CloneSwarm/Loot Drop Table")]
public class LootDropTable : ScriptableObject
{
    [Tooltip("โอกาสโดยรวมที่จะเกิดการดรอปไอเทม (1.0 = 100%, 0.5 = 50%)")]
    [Range(0f, 1f)]
    public float dropChance = 1f;

    [Tooltip("รายการไอเทมในตารางสุ่มและค่าน้ำหนัก")]
    public List<LootEntry> lootTable = new List<LootEntry>();

    /// <summary>
    /// สั่งสุ่มไอเทมและสปอว์น ณ ตำแหน่งที่ระบุ (ทำเฉพาะบนเซิร์ฟเวอร์)
    /// </summary>
    public void TriggerDrop(Vector3 position)
    {
        // ทำงานเฉพาะฝั่ง Server เท่านั้นในการสปอว์น Network Object
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        // สุ่มโอกาสดรอปพื้นฐานก่อน
        if (Random.value > dropChance)
            return;

        GameObject selectedPrefab = GetRandomLootPrefab();
        if (selectedPrefab != null)
        {
            SpawnDroppedItem(selectedPrefab, position);
        }
    }

    private GameObject GetRandomLootPrefab()
    {
        if (lootTable == null || lootTable.Count == 0)
            return null;

        int totalWeight = 0;
        foreach (var entry in lootTable)
        {
            if (entry.prefab != null && entry.weight > 0)
            {
                totalWeight += entry.weight;
            }
        }

        if (totalWeight == 0)
            return null;

        int roll = Random.Range(0, totalWeight);
        int currentWeightSum = 0;

        foreach (var entry in lootTable)
        {
            if (entry.prefab == null || entry.weight <= 0)
                continue;

            currentWeightSum += entry.weight;
            if (roll < currentWeightSum)
            {
                return entry.prefab;
            }
        }

        return null;
    }

    private void SpawnDroppedItem(GameObject prefab, Vector3 position)
    {
        // สุ่มระยะ offset เล็กน้อยบนแนวราบ XZ เพื่อไม่ให้ไอเทมซ้อนกันพอดี
        Vector2 offset2D = Random.insideUnitCircle * 0.3f;
        Vector3 spawnPos = position + new Vector3(offset2D.x, 0.1f, offset2D.y);

        GameObject droppedObj = Instantiate(prefab, spawnPos, Quaternion.identity);
        
        var netObj = droppedObj.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn(true);
        }
    }
}
