using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Limit Cut — แจกเลขลำดับให้ผู้เล่นทุกคน แล้วระเบิดไล่ทีละเลข
///
/// กลไกคลาสสิกของ FF14: ทุกคนได้เลข 1..N เห็นเลขของกันและกัน แล้วต้องผลัดกันออกไปรับท่า
/// ตามลำดับ · ต้องอ่านเลขคนอื่นได้ถึงจะรู้ว่าตัวเองต้องขยับตอนไหน
///
/// **ต้องมี `WorldNumberTag` บน player prefab ไม่งั้นไม่มีใครเห็นเลขและกลไกเล่นไม่ได้**
///
/// `rollName` แบบ `RollKind.Order` จะสลับลำดับคนทุกครั้ง — คนเดิมไม่ได้เลข 1 ตลอด
/// </summary>
[CreateAssetMenu(fileName = "LimitCutAction", menuName = "Boss/Actions/LimitCutAction")]
public class LimitCutAction : SpawnAoEActionBase
{
    [Header("Limit Cut")]
    [Tooltip("เวลาที่ให้ดูเลขก่อนเลข 1 จะระเบิด (วินาที)")]
    [Min(0f)] public float readTime = 3f;
    [Tooltip("เว้นกี่วินาทีระหว่างแต่ละเลข")]
    [Min(0.1f)] public float perNumberDelay = 1.5f;

    [Header("Telegraph ของแต่ละเลข")]
    public float radius = 4f;
    // warningDuration / damage / detonateVfx ใช้ตัวที่สืบทอดมาจาก SpawnAoEActionBase แล้ว
    // (เดิม field ซ้ำอยู่ที่นี่ — ย้ายไปรวมจุดเดียวตาม ADR-005 debt #3)

    protected override AoEType GetAoEType() => AoEType.Circle;

    protected override void ConfigureTelegraphZone(TelegraphZone zone)
    {
        zone.radius = radius;
    }

    public override float GetEditorDuration()
        => actionDelay + readTime + perNumberDelay * 4f + warningDuration;

    public override IEnumerator ExecuteCoroutine(NetworkBehaviour runner, GameObject telegraphPrefab)
    {
        if (actionDelay > 0f) yield return new WaitForSeconds(actionDelay);
        if (NetworkManager.Singleton == null || telegraphPrefab == null) yield break;

        var boss = runner as BossController;

        // เอาเฉพาะคนเป็น — แจกเลขให้ศพแล้วคิวนั้นจะว่างเปล่า ผู้เล่นนับจังหวะผิด
        var players = new List<playermove>();
        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (c.PlayerObject == null) continue;
            var pm = c.PlayerObject.GetComponent<playermove>();
            if (pm == null || pm.isDead.Value) continue;
            players.Add(pm);
        }
        if (players.Count == 0) yield break;

        // RollKind.Order สลับลำดับ — หมุนคิวตามค่า roll เพื่อไม่ให้คนเดิมได้เลข 1 ทุกรอบ
        int shift = 0;
        if (boss?.Rolls != null && !string.IsNullOrEmpty(rollName)
            && boss.Rolls.GetKind(rollName) == RollKind.Order)
        {
            int v = boss.Rolls.Peek(rollName);
            if (v >= 0) shift = v % players.Count;
        }
        if (shift > 0)
        {
            var rotated = new List<playermove>(players.Count);
            for (int i = 0; i < players.Count; i++) rotated.Add(players[(i + shift) % players.Count]);
            players = rotated;
        }

        // แจกเลข
        for (int i = 0; i < players.Count; i++) players[i].limitCutNumber.Value = i + 1;

        GameHUD.Instance?.ShowAnnouncement("LIMIT CUT — ดูเลขแล้วออกตามลำดับ!", new Color(1f, 0.9f, 0.3f));

        if (readTime > 0f) yield return new WaitForSeconds(readTime);

        // ระเบิดไล่ทีละเลข
        for (int i = 0; i < players.Count; i++)
        {
            var pm = players[i];

            // คนที่ตายระหว่างคิวยังต้องกินเวลาช่องของตัวเอง ไม่งั้นจังหวะที่คนอื่นนับไว้จะเพี้ยน
            if (pm != null && !pm.isDead.Value)
            {
                SpawnZoneAt(runner, telegraphPrefab, pm.transform.position, Quaternion.identity);
                pm.limitCutNumber.Value = 0;
            }

            if (i < players.Count - 1) yield return new WaitForSeconds(perNumberDelay);
        }

        yield return new WaitForSeconds(warningDuration);

        // กันเลขค้างบนหัวถ้ามีใครหลุดคิวไป
        foreach (var pm in players)
            if (pm != null) pm.limitCutNumber.Value = 0;
    }
}
