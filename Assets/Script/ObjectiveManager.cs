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
    // ช่องเดิม — ยังใช้ได้เหมือนเดิม ถือเป็นแบบเดียวถ้า zoneVariants ว่าง
    [Tooltip("Prefab ที่มี ZoneObjective.cs + NetworkObject · ใช้เมื่อ zoneVariants ว่าง")]
    public GameObject zoneObjectivePrefab;

    /// <summary>
    /// โซนหนึ่งแบบ — prefab กับโอกาสที่จะถูกเลือก
    ///
    /// **แบบต่างกันที่ของที่มันให้ ไม่ใช่ที่กติกา** — โซนทุกแบบใช้ `ZoneObjective`
    /// ตัวเดียวกัน ต่างกันที่ `orbPrefab` ข้างใน (orb ปกติ / orb ที่ให้ augment)
    /// จึงทำเป็น prefab variant ได้ แล้วการจูนกติกาโซนที่ตัวแม่ไหลลงทุกแบบเอง
    /// </summary>
    [System.Serializable]
    public struct ZoneVariant
    {
        [Tooltip("ชื่อเรียกแบบนี้ — ใช้ให้ GameTimeline นัดหมายเจาะจงได้ (เช่น \"augment\")")]
        public string id;
        [Tooltip("Prefab ที่มี ZoneObjective.cs + NetworkObject")]
        public GameObject prefab;
        [Tooltip("น้ำหนักการสุ่มเทียบกับแบบอื่น · 0 = ปิดชั่วคราวโดยไม่ต้องลบทิ้ง")]
        [Min(0f)] public float weight;
    }

    // โซนหลายแบบพร้อมน้ำหนัก — ว่าง = ใช้ zoneObjectivePrefab ข้างบนแบบเดียว
    //
    // ใส่แบบที่สองที่ orbPrefab เป็น AugOrb แล้วผู้เล่นจะได้ augment จากการทำ
    // เควสต์เป็นบางครั้ง แทนที่จะได้เฉพาะตอนเลเวลที่กำหนดไว้
    [Tooltip("โซนหลายแบบพร้อมน้ำหนัก — ว่าง = ใช้ zoneObjectivePrefab ข้างบนแบบเดียว")]
    public ZoneVariant[] zoneVariants = new ZoneVariant[0];

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
        GameTimeline.OnObjectiveTime += SpawnObjectives;   // Action<string> — ชื่อแบบที่นัดไว้
    }

    public override void OnNetworkDespawn()
    {
        GameTimeline.OnObjectiveTime -= SpawnObjectives;
    }

    /// <summary>Force spawn objective (dev tool only — server only)</summary>
    public void DevSpawnObjective() => SpawnObjectives("");

    void SpawnObjectives(string variantId)
    {
        if (!IsServer) return;

        var zonePrefab = PickZonePrefab(variantId);
        if (zonePrefab == null) return;

        // ── Step 1: pick delivery zone location ──────────────────────────
        var validForDelivery = GetValidLocations();
        if (validForDelivery.Count == 0)
        {
            Debug.LogWarning("[ObjectiveManager] ไม่มีจุดที่ห่างผู้เล่นพอ — ไม่มี objective spawn รอบนี้");
            return;
        }
        Vector3 deliveryPos = validForDelivery[Random.Range(0, validForDelivery.Count)];

        // ── Step 2: instantiate zone (ยังไม่ Spawn) ──────────────────────
        var go   = Instantiate(zonePrefab, deliveryPos, Quaternion.identity);
        var zone = go.GetComponent<ZoneObjective>();
        if (zone == null)
        {
            Debug.LogError($"[ObjectiveManager] '{zonePrefab.name}' ไม่มี ZoneObjective component");
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

        Debug.Log($"[ObjectiveManager] 🎯 '{zonePrefab.name}' spawned at {deliveryPos}");
    }

    /// <summary>
    /// สุ่มแบบของโซนตามน้ำหนัก
    ///
    /// ═══ ช่องเดิมยังทำงานอยู่ ═══
    ///
    /// `zoneVariants` ว่าง = ใช้ `zoneObjectivePrefab` แบบเดียวเหมือนเดิมเป๊ะ
    /// ซีนที่ยังไม่ได้เติมลิสต์จึงไม่เปลี่ยนพฤติกรรมเลย — การเพิ่มแบบที่สองเป็น
    /// การตั้งค่า ไม่ใช่การอัปเกรดที่บังคับให้ทุกซีนตามมาแก้
    ///
    /// ช่องที่ prefab ว่างหรือน้ำหนัก 0 ถูกข้าม · น้ำหนักรวมเป็น 0 ทั้งลิสต์
    /// ก็ถอยไปใช้ช่องเดิม แทนที่จะเงียบแล้วไม่มี objective ออกมาทั้งเกม
    /// </summary>
    GameObject PickZonePrefab(string variantId)
    {
        // นัดหมายเจาะจง — หาแบบตามชื่อก่อนเสมอ
        //
        // **หาไม่เจอแล้วต้องบ่น** ไม่ใช่เงียบแล้วสุ่มแทน · ชื่อพิมพ์ผิดกับ
        // "ดวงไม่ดีเลยไม่ออก" หน้าตาเหมือนกัน คนตั้งตารางจึงแยกไม่ออกเลย
        if (!string.IsNullOrEmpty(variantId))
        {
            if (zoneVariants != null)
                foreach (var v in zoneVariants)
                    if (v.prefab != null && v.id == variantId) return v.prefab;

            Debug.LogWarning($"[ObjectiveManager] นัดหมายขอแบบ '{variantId}' " +
                             "แต่ไม่มีใน zoneVariants → สุ่มตามน้ำหนักแทน");
        }

        float total = 0f;
        if (zoneVariants != null)
            foreach (var v in zoneVariants)
                if (v.prefab != null) total += Mathf.Max(0f, v.weight);

        if (total <= 0f)
        {
            if (zoneObjectivePrefab == null)
                Debug.LogWarning("[ObjectiveManager] ไม่มีแบบของโซนให้ spawn เลย — " +
                                 "ต่อ zoneObjectivePrefab หรือเติม zoneVariants");
            return zoneObjectivePrefab;
        }

        float roll = Random.Range(0f, total);
        float acc  = 0f;
        foreach (var v in zoneVariants)
        {
            if (v.prefab == null) continue;
            acc += Mathf.Max(0f, v.weight);
            if (roll <= acc) return v.prefab;
        }
        return zoneObjectivePrefab;   // ไม่ควรถึง — กันพลาดจากทศนิยม
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
