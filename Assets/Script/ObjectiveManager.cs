using Unity.Netcode;
using UnityEngine;

/// <summary>
/// รับ event จาก GameTimeline แล้ว spawn ZoneObjective ทุก 2 นาที
/// Server only
/// </summary>
public class ObjectiveManager : NetworkBehaviour
{
    [Header("Prefab")]
    [Tooltip("Prefab ที่มี ZoneObjective.cs + NetworkObject")]
    public GameObject zoneObjectivePrefab;

    [Header("Placement")]
    [Tooltip("spawn ห่างจาก player กี่ unit (min)")]
    public float minSpawnDistance = 5f;
    [Tooltip("spawn ห่างจาก player กี่ unit (max)")]
    public float maxSpawnDistance = 12f;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        GameTimeline.OnObjectiveTime += SpawnObjective;
    }

    public override void OnNetworkDespawn()
    {
        GameTimeline.OnObjectiveTime -= SpawnObjective;
    }

    void SpawnObjective()
    {
        if (!IsServer || zoneObjectivePrefab == null) return;

        Vector3 pos = GetSpawnPosition();
        var go = Instantiate(zoneObjectivePrefab, pos, Quaternion.identity);
        go.GetComponent<NetworkObject>()?.Spawn(true);
        Debug.Log($"[ObjectiveManager] 🎯 Zone Objective spawned at {pos}");
    }

    Vector3 GetSpawnPosition()
    {
        var clients = NetworkManager.Singleton.ConnectedClientsList;
        Vector3 center = Vector3.zero;

        if (clients.Count > 0)
        {
            foreach (var c in clients)
                if (c.PlayerObject != null) center += c.PlayerObject.transform.position;
            center /= clients.Count;
        }

        // หาจุดที่ไม่ซ้อนกับผู้เล่นมากเกินไป
        for (int attempt = 0; attempt < 10; attempt++)
        {
            Vector2 rand  = Random.insideUnitCircle.normalized;
            float   dist  = Random.Range(minSpawnDistance, maxSpawnDistance);
            Vector3 candidate = center + new Vector3(rand.x, 0f, rand.y) * dist;

            // ตรวจว่าห่างจาก player พอ
            bool tooClose = false;
            foreach (var c in clients)
            {
                if (c.PlayerObject == null) continue;
                if (Vector3.Distance(candidate, c.PlayerObject.transform.position) < minSpawnDistance)
                { tooClose = true; break; }
            }
            if (!tooClose) return candidate;
        }

        // Fallback
        return center + Vector3.right * maxSpawnDistance;
    }
}
