using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// รับ event จาก GameTimeline แล้ว spawn ZoneObjective
///
/// Placement:
///   ใช้ ZoneObjectiveLocation[] เป็นจุดที่กำหนดไว้ใน scene
///   - Pick 1 จุดเป็น delivery zone (กรองห่างผู้เล่น ≥ minSpawnDistance)
///   - ถ้า zone.availableQuests มี FetchAndDeliver → pick N จุดที่เหลือเป็น item spawn
///     (ห่าง delivery zone ≥ minItemDistanceFromZone) → ใส่ใน zone.itemSpawnPositions ก่อน Spawn()
/// </summary>
public class ObjectiveManager : NetworkBehaviour
{
    [Header("Prefab")]
    [Tooltip("Prefab ที่มี ZoneObjective.cs + NetworkObject")]
    public GameObject zoneObjectivePrefab;

    [Header("Spawn Locations")]
    [Tooltip("จุดที่กำหนดไว้ใน scene — เลือกสุ่มจากนี้ (ใช้ทั้ง delivery zone และ item spawn)")]
    public GameObject[] ZoneObjectiveLocation = new GameObject[3];

    [Header("Player Distance Filter")]
    [Tooltip("ไม่ spawn delivery zone ที่ใกล้ผู้เล่นน้อยกว่านี้")]
    public float minSpawnDistance = 10f;

    [Header("Fetch Item Placement")]
    [Tooltip("ปิด (แนะนำ) = ZoneObjective สุ่มตำแหน่ง item สดรอบโซนเองทุกครั้ง\n" +
             "เปิด = ล็อกให้ item ไปโผล่เฉพาะจุดใน ZoneObjectiveLocation ด้านบน\n" +
             "(จุดในซีนมีจำกัด → ตำแหน่งซ้ำทุกรอบ และรองรับ item ได้ไม่กี่ชิ้น)")]
    public bool useFixedItemLocations = false;

    [Tooltip("ใช้เฉพาะตอนเปิด useFixedItemLocations — item ต้องห่างจาก delivery zone อย่างน้อยเท่านี้")]
    public float minItemDistanceFromZone = 8f;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        GameTimeline.OnObjectiveTime += SpawnObjectives;
    }

    public override void OnNetworkDespawn()
    {
        GameTimeline.OnObjectiveTime -= SpawnObjectives;
    }

    /// <summary>Force spawn objective (dev tool only — server only)</summary>
    public void DevSpawnObjective() => SpawnObjectives();

    void SpawnObjectives()
    {
        if (!IsServer || zoneObjectivePrefab == null) return;

        // ── Step 1: pick delivery zone location ──────────────────────────
        var validForDelivery = GetValidLocations();
        if (validForDelivery.Count == 0)
        {
            Debug.LogWarning("[ObjectiveManager] ไม่มีจุดที่ห่างผู้เล่นพอ — ไม่มี objective spawn รอบนี้");
            return;
        }
        Vector3 deliveryPos = validForDelivery[Random.Range(0, validForDelivery.Count)];

        // ── Step 2: instantiate zone (ยังไม่ Spawn) ──────────────────────
        var go   = Instantiate(zoneObjectivePrefab, deliveryPos, Quaternion.identity);
        var zone = go.GetComponent<ZoneObjective>();
        if (zone == null)
        {
            Debug.LogError("[ObjectiveManager] zoneObjectivePrefab ไม่มี ZoneObjective component");
            Destroy(go);
            return;
        }

        // ── Step 3: item locations ───────────────────────────────────────
        // ปกติปล่อยว่าง → ZoneObjective สุ่มตำแหน่งสดตอน quest เริ่มจริง
        // เติมให้เฉพาะตอนสั่ง lock ไว้ที่จุดในซีน
        bool mayNeedItems = zone.availableQuests != null
                         && zone.availableQuests.Contains(ZoneObjective.QuestType.FetchAndDeliver);
        if (useFixedItemLocations && mayNeedItems)
        {
            var itemPositions = PickItemLocations(deliveryPos, zone.requiredDeliveryCount);
            zone.itemSpawnPositions = itemPositions;

            if (itemPositions.Count < zone.requiredDeliveryCount)
                Debug.LogWarning($"[ObjectiveManager] เลือก item locations ได้แค่ {itemPositions.Count}/{zone.requiredDeliveryCount}");
        }
        else
        {
            zone.itemSpawnPositions.Clear();
        }

        // ── Step 4: Spawn (NetworkObject) → OnNetworkSpawn ของ zone ทำงาน ──
        go.GetComponent<NetworkObject>()?.Spawn(true);

        Debug.Log($"[ObjectiveManager] 🎯 ZoneObjective spawned at {deliveryPos}");
    }

    // ── กรองจุดที่ใกล้ผู้เล่นเกินไปออก (สำหรับ delivery zone) ─────────────
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

    // ── เลือก N item locations — ห่างจาก delivery zone อย่างน้อย minItemDistanceFromZone ──
    List<Vector3> PickItemLocations(Vector3 deliveryPos, int count)
    {
        var candidates = new List<Vector3>();
        foreach (var loc in ZoneObjectiveLocation)
        {
            if (loc == null) continue;
            Vector3 pos = loc.transform.position;

            // ข้าม delivery position เอง + ที่ใกล้ delivery เกินไป
            if (Vector3.Distance(pos, deliveryPos) < minItemDistanceFromZone) continue;

            candidates.Add(pos);
        }

        // Fisher-Yates shuffle เพื่อสุ่มลำดับ
        Shuffle(candidates);

        // คืน N ตัวแรก (ถ้าไม่พอจะคืนทั้งหมด)
        int take = Mathf.Min(count, candidates.Count);
        return candidates.GetRange(0, take);
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
