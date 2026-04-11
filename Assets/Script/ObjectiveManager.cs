using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// รับ event จาก GameTimeline แล้ว spawn ZoneObjective
///
/// Placement:
///   ใช้ ZoneObjectiveLocation[] เป็นจุดที่กำหนดไว้ใน scene
///   สุ่มเลือก objectiveCount จุด โดยกรองออกจุดที่ใกล้ผู้เล่นเกินไป
///   ถ้าจุดที่เหลือน้อยกว่า objectiveCount → ใช้ทั้งหมดที่มี
/// </summary>
public class ObjectiveManager : NetworkBehaviour
{
    [Header("Prefab")]
    [Tooltip("Prefab ที่มี ZoneObjective.cs + NetworkObject")]
    public GameObject zoneObjectivePrefab;

    [Header("Spawn Locations")]
    [Tooltip("จุดที่กำหนดไว้ใน scene — เลือกสุ่มจากนี้")]
    public GameObject[] ZoneObjectiveLocation = new GameObject[3];

    [Header("Player Distance Filter")]
    [Tooltip("ไม่ spawn จุดที่ใกล้ผู้เล่นคนใดคนหนึ่งน้อยกว่านี้")]
    public float minSpawnDistance = 10f;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        GameTimeline.OnObjectiveTime += SpawnObjectives;
    }

    public override void OnNetworkDespawn()
    {
        GameTimeline.OnObjectiveTime -= SpawnObjectives;
    }

    void SpawnObjectives()
    {
        if (!IsServer || zoneObjectivePrefab == null) return;

        var candidates = GetValidLocations();

        if (candidates.Count == 0)
        {
            Debug.LogWarning("[ObjectiveManager] ไม่มีจุดที่ห่างผู้เล่นพอ — ไม่มี objective spawn รอบนี้");
            return;
        }

        // สุ่มเลือก 1 จุด
        Vector3 chosen = candidates[Random.Range(0, candidates.Count)];
        var go = Instantiate(zoneObjectivePrefab, chosen, Quaternion.identity);
        go.GetComponent<NetworkObject>()?.Spawn(true);
        Debug.Log($"[ObjectiveManager] 🎯 Zone Objective spawned at {chosen}");
    }

    // ── กรองจุดที่ใกล้ผู้เล่นเกินไปออก ───────────────────────────────────
    List<Vector3> GetValidLocations()
    {
        var result  = new List<Vector3>();
        var players = GetAllPlayerPositions();

        foreach (var loc in ZoneObjectiveLocation)
        {
            if (loc == null) continue;
            Vector3 pos = loc.transform.position;

            bool tooClose = false;
            foreach (var p in players)
            {
                if (Vector3.Distance(pos, p) < minSpawnDistance)
                { tooClose = true; break; }
            }

            if (!tooClose) result.Add(pos);
        }

        return result;
    }

    List<Vector3> GetAllPlayerPositions()
    {
        var list = new List<Vector3>();
        if (NetworkManager.Singleton == null) return list;
        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (c.PlayerObject != null)
                list.Add(c.PlayerObject.transform.position);
        }
        return list;
    }

    // Fisher-Yates shuffle
    void Shuffle(List<Vector3> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
