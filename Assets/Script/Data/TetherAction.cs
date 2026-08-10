using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// spawn BossTether แล้วสั่ง Activate — ดู docs/adr-002-tether-modes.md
///
/// อยากให้บอสสุ่มโหมด: ทำ asset หลายตัวต่างโหมด แล้วโยนเข้า pool ของ RandomAttackAction
/// ไม่ต้องเขียนการสุ่มลงในนี้
/// </summary>
[CreateAssetMenu(fileName = "TetherAction", menuName = "Boss/Actions/TetherAction")]
public class TetherAction : BossAction
{
    [Header("Mode")]
    public TetherMode mode = TetherMode.Far;
    [Tooltip("ใช้เมื่อ mode = Close · ตอนนี้ทำแค่ OnTimeout (ตัวอื่นจะ warn แล้ว fallback)")]
    public CloseFailMode closeFailMode = CloseFailMode.OnTimeout;

    [Header("Tether Settings")]
    [Tooltip("Far = ระยะที่ต้องวิ่งห่างให้สายขาด · Close = ระยะที่ห้ามเกิน · Leash = รัศมีที่ห้ามออก")]
    public float tetherDistance = 25f;
    public float tetherDuration = 10f;
    [Tooltip("Leash ไม่ใช้ค่านี้ — Leash ดึงกลับแทนการทำดาเมจ")]
    public float tetherFailDamage = 50f;
    [Tooltip("ระยะห่างเสาจากผู้เล่น กรณีไม่มีคนอื่นให้ผูก (Far/Close)")]
    public float tetherSoloSpawnOffset = 10f;

    [Header("Leash")]
    public LeashScope leashScope = LeashScope.EachPlayerOwnAnchor;
    [Tooltip("เยื้องจุดศูนย์กลางวงจากตัวผู้เล่นเท่าไหร่ — 0 = วงล้อมรอบตำแหน่งที่ยืนอยู่พอดี")]
    public float leashAnchorOffset = 0f;

    public override float GetEditorDuration() => actionDelay + tetherDuration;

    public override IEnumerator ExecuteCoroutine(NetworkBehaviour runner, GameObject telegraphPrefab)
    {
        if (actionDelay > 0f) yield return new WaitForSeconds(actionDelay);

        var bossController = runner as BossController;
        if (bossController == null || bossController.tetherPrefab == null)
        {
            Debug.LogWarning("[TetherAction] BossController หรือ tetherPrefab หาไม่เจอ!");
            yield break;
        }

        if (NetworkManager.Singleton == null) yield break;

        // เป้าหลัก (คนที่ต้องวิ่ง) ต้องยังไม่ตาย — ทุกโหมด
        var alive = CollectPlayers(aliveOnly: true);
        if (alive.Count == 0) yield break;

        if (mode == TetherMode.Leash) SpawnLeash(bossController, alive);
        else                          SpawnPairOrPillar(bossController, alive);
    }

    // ── Far / Close / (Transferable → BossTether จะ fallback เป็น Far) ─────
    private void SpawnPairOrPillar(BossController boss, List<(ulong id, Vector3 pos)> alive)
    {
        var target = alive[Random.Range(0, alive.Count)];

        // anchor —
        //   Far  : ต้องเป็นคนเป็นเท่านั้น · ศพวิ่งหนีไม่ได้ สายไม่มีทางขาด กลไกแก้ไม่ได้
        //   Close: คนเป็นก่อน แต่ถ้าเพื่อนตายหมดผูกกับศพได้ · คนเป็นเดินไปยืนข้างศพเอาได้ ยังแก้ได้อยู่
        var anchors = alive.FindAll(p => p.id != target.id);
        if (anchors.Count == 0 && mode == TetherMode.Close)
            anchors = CollectPlayers(aliveOnly: false).FindAll(p => p.id != target.id);

        if (anchors.Count > 0)
        {
            var anchor = anchors[Random.Range(0, anchors.Count)];
            var tether = SpawnTether(boss, boss.transform.position);
            if (tether == null) return;

            tether.Activate(target.id, anchor.id);
            Debug.Log($"[TetherAction] 🔗 {mode}: Client {target.id} ↔ Client {anchor.id}");
            return;
        }

        // ไม่มีใครให้ผูก → ผูกกับเสาแทน
        Vector3 spawnPos = OffsetAround(target.pos, tetherSoloSpawnOffset);
        var solo = SpawnTether(boss, spawnPos);
        if (solo == null) return;

        solo.ActivateOnPillar(target.id);
        Debug.Log($"[TetherAction] 🔗 {mode} (เสา): Client {target.id}");
    }

    // ── Leash ─────────────────────────────────────────────────────────────
    private void SpawnLeash(BossController boss, List<(ulong id, Vector3 pos)> alive)
    {
        switch (leashScope)
        {
            case LeashScope.SingleRandom:
            {
                var p = alive[Random.Range(0, alive.Count)];
                SpawnLeashFor(boss, p.id, OffsetAround(p.pos, leashAnchorOffset));
                break;
            }

            case LeashScope.AllSharedAnchor:
            {
                Vector3 shared = boss.transform.position;
                foreach (var p in alive) SpawnLeashFor(boss, p.id, shared);
                break;
            }

            case LeashScope.EachPlayerOwnAnchor:
            default:
                foreach (var p in alive) SpawnLeashFor(boss, p.id, OffsetAround(p.pos, leashAnchorOffset));
                break;
        }

        Debug.Log($"[TetherAction] ⛓ Leash {leashScope} → {alive.Count} คน");
    }

    private void SpawnLeashFor(BossController boss, ulong clientId, Vector3 anchorPos)
    {
        var tether = SpawnTether(boss, anchorPos);
        if (tether == null) return;
        tether.ActivateOnPillar(clientId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private List<(ulong id, Vector3 pos)> CollectPlayers(bool aliveOnly)
    {
        var list = new List<(ulong, Vector3)>();
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;

            var pm = client.PlayerObject.GetComponent<playermove>();
            if (pm == null) continue;
            if (aliveOnly && pm.isDead.Value) continue;

            list.Add((client.ClientId, pm.transform.position));
        }
        return list;
    }

    private BossTether SpawnTether(BossController boss, Vector3 pos)
    {
        var go = Instantiate(boss.tetherPrefab, pos, Quaternion.identity);
        var tether = go.GetComponent<BossTether>();
        var no = go.GetComponent<NetworkObject>();

        if (tether == null || no == null)
        {
            Destroy(go);
            return null;
        }

        // โหลดค่าลง instance ฝั่ง server — ค่าที่ client ต้องใช้ถูกส่งต่อผ่าน ClientRpc ใน Activate
        tether.mode             = mode;
        tether.closeFailMode    = closeFailMode;
        tether.requiredDistance = tetherDistance;
        tether.duration         = tetherDuration;
        tether.failDamage       = tetherFailDamage;

        no.Spawn(true);
        boss.RegisterMechanic(no);
        return tether;
    }

    private static Vector3 OffsetAround(Vector3 origin, float distance)
    {
        if (distance <= 0f) return origin;
        Vector2 rand = Random.insideUnitCircle.normalized * distance;
        return new Vector3(origin.x + rand.x, origin.y, origin.z + rand.y);
    }
}
