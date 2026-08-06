using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

public abstract class SpawnAoEActionBase : BossAction
{
    public enum TargetingMode { BossPosition, RandomPlayer, AllPlayers, NearestPlayer, StaticCoords }

    [Header("Targeting")]
    public TargetingMode targetingMode = TargetingMode.BossPosition;
    [Tooltip("ค่า offset ทิศทางที่บวกเพิ่มจากพิกัดเป้าหมาย (แกน XZ)")]
    public Vector3 targetOffset = Vector3.zero;

    [Header("Telegraph Properties")]
    public float warningDuration = 2.5f;
    public float damage = 25f;

    [Header("FFXIV Special Settings")]
    [Tooltip("เปิดให้ท่าโจมตีรูปแบบนี้วิ่งตามล่าผู้เล่นเป้าหมาย (Chase)")]
    public bool isChasing = false;
    [Tooltip("ให้จุดศูนย์กลางขยับตามตัวบอสผู้ปล่อยท่าตลอดเวลา")]
    public bool followCaster = false;
    [Tooltip("หมุนทิศทางตามตัว player แทนการขยับจุดศูนย์กลางไปทับตัว player (ใช้คู่กับ isChasing)")]
    public bool isRotatingChase = false;
    public bool isStackMarker = false;
    public bool isGaze = false;

    [Header("Knockback")]
    [Tooltip("ทิศทางการผลัก — ดูคำอธิบายแต่ละแบบใน KnockbackMode.cs")]
    public KnockbackMode knockbackMode = KnockbackMode.FromCenter;
    [Tooltip("ระยะผลักเป็นหน่วยระยะทาง (0 = ไม่ผลัก)\n" +
             "หมายเหตุ: ฟิลด์นี้เดิมคือ 'แรง' — ทุก asset เดิมตั้งไว้ 0 จึงไม่ต้องแปลงค่า")]
    [FormerlySerializedAs("knockbackForce")]
    [Min(0f)] public float knockbackDistance = 0f;
    [Tooltip("ระยะเวลาที่ผู้เล่นถูกผลัก (วินาที) — ความเร็วคำนวณจาก ระยะ ÷ เวลา")]
    [Min(0f)] public float knockbackDuration = 0.2f;
    [Tooltip("ใช้เฉพาะ KnockbackMode.FixedDirection — ทิศในพิกัดโลก (คิดเฉพาะแกน XZ)")]
    public Vector3 knockbackFixedDirection = Vector3.forward;

    protected abstract AoEType GetAoEType();
    protected abstract void ConfigureTelegraphZone(TelegraphZone zone);

    // warning + ช่วง resolve สั้นๆ หลัง telegraph ระเบิด
    public override float GetEditorDuration() => actionDelay + warningDuration + 0.5f;

    public override IEnumerator ExecuteCoroutine(NetworkBehaviour runner, GameObject telegraphPrefab)
    {
        if (actionDelay > 0f)
        {
            yield return new WaitForSeconds(actionDelay);
        }

        if (telegraphPrefab == null)
        {
            Debug.LogWarning($"[{GetType().Name}] telegraphPrefab is null!");
            yield break;
        }

        List<Vector3> spawnPositions = GetSpawnPositions(runner);
        foreach (var pos in spawnPositions)
        {
            // คำนวณทิศทางการหันหน้า: หากยิงใส่เป้าหมาย หรือหันไปทางเป้าหมาย
            Quaternion rot = Quaternion.identity;
            if (GetAoEType() == AoEType.Line)
            {
                // หมุนไปทางผู้เล่นที่ใกล้ที่สุดหรือเป้าหมายเพื่อให้พาดผ่านตัว
                Transform nearestPlayer = FindNearestPlayer(pos);
                if (nearestPlayer != null)
                {
                    Vector3 dir = (nearestPlayer.position - pos).normalized;
                    dir.y = 0f;
                    if (dir != Vector3.zero) rot = Quaternion.LookRotation(dir);
                }
            }

            var go = Instantiate(telegraphPrefab, pos, rot);
            var zone = go.GetComponent<TelegraphZone>();
            var no = go.GetComponent<NetworkObject>();

            if (zone != null && no != null)
            {
                zone.aoeType = GetAoEType();
                zone.warningDuration = warningDuration;
                zone.damage = damage;
                zone.isChasing = isChasing;
                zone.isRotatingChase = isRotatingChase;
                zone.isStackMarker = isStackMarker;
                zone.isGaze = isGaze;
                zone.knockbackMode = knockbackMode;
                zone.knockbackDistance = knockbackDistance;
                zone.knockbackDuration = knockbackDuration;
                zone.knockbackFixedDirection = knockbackFixedDirection;

                if (followCaster && runner != null)
                {
                    zone.casterNetworkObject = runner.NetworkObject;
                }

                ConfigureTelegraphZone(zone);

                no.Spawn(true);
                (runner as BossController)?.RegisterMechanic(no);
                zone.BroadcastInit();
            }
            else
            {
                Destroy(go);
            }
        }
    }

    private List<Vector3> GetSpawnPositions(NetworkBehaviour runner)
    {
        var list = new List<Vector3>();
        Vector3 basePos = runner.transform.position;

        switch (targetingMode)
        {
            case TargetingMode.BossPosition:
                list.Add(basePos + targetOffset);
                break;
            case TargetingMode.RandomPlayer:
                if (NetworkManager.Singleton != null)
                {
                    var clients = new List<NetworkClient>(NetworkManager.Singleton.ConnectedClientsList);
                    if (clients.Count > 0)
                    {
                        var target = clients[Random.Range(0, clients.Count)];
                        if (target.PlayerObject != null)
                        {
                            Vector3 pos = target.PlayerObject.transform.position;
                            pos.y = basePos.y;
                            list.Add(pos + targetOffset);
                        }
                        else
                        {
                            list.Add(basePos + targetOffset);
                        }
                    }
                    else
                    {
                        list.Add(basePos + targetOffset);
                    }
                }
                break;
            case TargetingMode.NearestPlayer:
                Transform nearest = FindNearestPlayer(basePos);
                if (nearest != null)
                {
                    Vector3 pos = nearest.position;
                    pos.y = basePos.y;
                    list.Add(pos + targetOffset);
                }
                else
                {
                    list.Add(basePos + targetOffset);
                }
                break;
            case TargetingMode.AllPlayers:
                if (NetworkManager.Singleton != null)
                {
                    foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
                    {
                        if (c.PlayerObject != null)
                        {
                            var pm = c.PlayerObject.GetComponent<playermove>();
                            if (pm != null && pm.isDead.Value) continue;

                            Vector3 pos = c.PlayerObject.transform.position;
                            pos.y = basePos.y;
                            list.Add(pos + targetOffset);
                        }
                    }
                }
                if (list.Count == 0)
                {
                    list.Add(basePos + targetOffset);
                }
                break;
            case TargetingMode.StaticCoords:
                list.Add(targetOffset);
                break;
        }

        return list;
    }
}
