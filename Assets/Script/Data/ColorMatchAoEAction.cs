using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>วงสีจะสุ่มเกิดรอบจุดไหน</summary>
public enum ColorMatchCenterMode
{
    /// <summary>ตำแหน่งบอส ณ วินาทีที่ยิง action</summary>
    Boss,
    /// <summary>จุดยึดในสนามจาก BossEncounterConfig.arena — ไม่ขยับตามบอส</summary>
    Arena,
    /// <summary>ตำแหน่งของผู้เล่นแต่ละคน — วงของใครก็เกิดใกล้คนนั้น</summary>
    EachPlayer
}

[CreateAssetMenu(fileName = "ColorMatchAoEAction", menuName = "Boss/Actions/ColorMatchAoEAction")]
public class ColorMatchAoEAction : BossAction
{
    [Header("Color Match Settings")]
    public float radius = 3f;
    public float warningDuration = 3.5f;
    public float damage = 99f; // High damage for failing

    [Tooltip("ระยะจากจุดศูนย์กลางถึงวงที่จะเกิด — 0 = วงทุกใบซ้อนกันที่จุดศูนย์กลางพอดี")]
    public float spawnRadius = 6f;

    [Header("Center")]
    [Tooltip("Boss = ตามตัวบอส · Arena = จุดยึดในสนาม · EachPlayer = รอบตัวผู้เล่นแต่ละคน")]
    public ColorMatchCenterMode centerMode = ColorMatchCenterMode.Boss;

    [Tooltip("ใช้เมื่อ centerMode = Arena · ต้องมี BossEncounterConfig.arena ด้วย")]
    public ArenaAnchor arenaAnchor = ArenaAnchor.Center;

    [Tooltip("ใช้เมื่อ centerMode = Arena · 1 = ขอบสนาม · 0.5 = ครึ่งทาง (Center ไม่สนค่านี้)")]
    public float arenaDistanceScale = 1f;

    public override IEnumerator ExecuteCoroutine(NetworkBehaviour runner, GameObject telegraphPrefab)
    {
        if (actionDelay > 0f)
            yield return new WaitForSeconds(actionDelay);

        if (NetworkManager.Singleton == null || telegraphPrefab == null || runner == null) yield break;

        // เก็บตำแหน่งไปพร้อมกันเลย โหมด EachPlayer จะได้ไม่ต้องหา PlayerObject ซ้ำ
        List<(ulong id, Vector3 pos)> activePlayers = new List<(ulong, Vector3)>();
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;

            var pm = client.PlayerObject.GetComponent<playermove>();
            if (pm == null || pm.isDead.Value) continue;

            activePlayers.Add((client.ClientId, pm.transform.position));
        }

        // อ่านครั้งเดียวก่อนวนลูป — โหมด Boss จะได้ไม่เลื่อนตามบอสระหว่างที่ spawn อยู่
        Vector3 sharedCenter = ResolveSharedCenter(runner);

        foreach (var p in activePlayers)
        {
            Vector3 center = centerMode == ColorMatchCenterMode.EachPlayer ? p.pos : sharedCenter;

            Vector2 randCircle = Random.insideUnitCircle.normalized * spawnRadius;
            Vector3 spawnPos = center + new Vector3(randCircle.x, 0.1f, randCircle.y);

            SpawnColoredZone(spawnPos, telegraphPrefab, p.id, runner as BossController);
        }
    }

    private Vector3 ResolveSharedCenter(NetworkBehaviour runner)
    {
        if (centerMode != ColorMatchCenterMode.Arena) return runner.transform.position;

        ArenaDefinition arena = (runner as BossController)?.config?.arena;
        if (arena == null)
        {
            // ปล่อยผ่านไม่ได้ — ArenaAnchors.Resolve(null, …) คืนพิกัดรอบ world origin
            // วงจะไปโผล่นอกสนามแบบเงียบๆ หาสาเหตุยากมาก
            Debug.LogWarning($"[ColorMatch] {name}: centerMode = Arena แต่ BossEncounterConfig.arena ว่าง — ใช้ตำแหน่งบอสแทน");
            return runner.transform.position;
        }

        return ArenaAnchors.Resolve(arena, arenaAnchor, arenaDistanceScale);
    }

    private void SpawnColoredZone(Vector3 position, GameObject telegraphPrefab, ulong clientId, BossController boss)
    {
        var go = Instantiate(telegraphPrefab, position, Quaternion.identity);
        var zone = go.GetComponent<TelegraphZone>();
        var no = go.GetComponent<NetworkObject>();

        if (zone != null && no != null)
        {
            zone.aoeType = AoEType.Circle;
            zone.radius = radius;
            zone.warningDuration = warningDuration;
            zone.damage = damage;
            
            zone.isColorMatch.Value = true;
            zone.requiredClientId.Value = clientId;

            no.Spawn(true);
            boss?.RegisterMechanic(no);
            zone.BroadcastInit();

            // แจ้งผู้เล่นว่าต้องเข้าวงสีอะไร
            int slot = PlayerSlotRegistry.Instance != null
                ? PlayerSlotRegistry.Instance.GetSlot(clientId) : -1;
            if (slot < 0) slot = (int)(clientId % 4);

            string colorName = slot switch
            {
                0 => "RED",
                1 => "BLUE",
                2 => "GREEN",
                _ => "YELLOW"
            };
            Color uiColor = slot switch
            {
                0 => Color.red,
                1 => Color.blue,
                2 => Color.green,
                _ => Color.yellow
            };
            zone.NotifyColorClientRpc(clientId, colorName, uiColor);
        }
        else
        {
            Destroy(go);
        }
    }
}
