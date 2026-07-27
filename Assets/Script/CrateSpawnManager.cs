using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CrateSpawnManager : NetworkBehaviour
{
    public static CrateSpawnManager Instance { get; private set; }

    [Header("Crate Settings")]
    [Tooltip("Prefab ของกล่องไม้ที่ต้องการสปอว์น")]
    public GameObject cratePrefab;

    [Tooltip("ระยะเวลาการตรวจเช็คและเกิดใหม่ของกล่องไม้ (วินาที)")]
    public float spawnInterval = 15f;

    [Header("Spawn Points")]
    [Tooltip("ลิสต์ตำแหน่งจุดเกิดทั้งหมดที่ลากตั้งค่าไว้ในซีน")]
    public List<Transform> spawnPoints = new List<Transform>();

    // เก็บประวัติการจับคู่ จุดเกิด -> วัตถุกล่องที่กำลังตั้งอยู่
    private Dictionary<Transform, GameObject> activeCrates = new Dictionary<Transform, GameObject>();
    private float timer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // เมื่อเริ่มเกม ให้สปอว์นกล่องไม้ขึ้นมาที่จุดเกิดทุกจุดที่ระบุไว้ทันที
        foreach (var point in spawnPoints)
        {
            if (point != null)
            {
                SpawnCrateAtPoint(point);
            }
        }

        timer = 0f;
    }

    private void Update()
    {
        if (!IsServer) return;

        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;
            TrySpawnRandomCrate();
        }
    }

    private void TrySpawnRandomCrate()
    {
        // ค้นหาและคัดกรองจุดเกิดที่ว่างอยู่
        List<Transform> emptyPoints = new List<Transform>();
        foreach (var point in spawnPoints)
        {
            if (point == null) continue;

            // หากไม่มีข้อมูลใน Dictionary หรือ วัตถุถูกทำลายไปแล้ว (== null)
            if (!activeCrates.TryGetValue(point, out GameObject activeCrate) || activeCrate == null)
            {
                emptyPoints.Add(point);
            }
        }

        // หากทุกจุดสปอว์นเต็มอยู่ ไม่มีจุดว่างเลย ให้ข้ามการสปอว์น
        if (emptyPoints.Count == 0)
            return;

        // สุ่มเลือกจุดว่าง 1 จุดมาทำการเกิดกล่องไม้
        Transform selectedPoint = emptyPoints[Random.Range(0, emptyPoints.Count)];
        SpawnCrateAtPoint(selectedPoint);
    }

    private void SpawnCrateAtPoint(Transform point)
    {
        if (cratePrefab == null || point == null) return;

        // สปอว์นกล่องไม้ขึ้นมา
        GameObject crate = Instantiate(cratePrefab, point.position, point.rotation);
        
        // ลงทะเบียนบันทึกอ้างอิงไว้
        activeCrates[point] = crate;

        // สปอว์นผ่านระบบเครือข่าย
        var netObj = crate.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn(true);
        }
    }
}
