using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[CreateAssetMenu(fileName = "KeepMovingAction", menuName = "Boss/Actions/KeepMovingAction")]
public class KeepMovingAction : SpawnAoEActionBase
{
    [Header("Keep Moving Settings")]
    [Tooltip("ระยะเวลาที่ใช้ mechanic นี้ (วินาที)")]
    public float mechanicDuration = 10f;
    [Tooltip("เวลาที่อนุญาตให้ยืนนิ่งได้ก่อนโดนลงโทษ (วินาที)")]
    public float allowedIdleTime = 1.5f;

    [Header("Punishment Telegraph (Circle)")]
    public float radius = 2f;
    // warningDuration / damage / detonateVfx ใช้ตัวที่สืบทอดมาจาก SpawnAoEActionBase แล้ว
    // (เดิม field ซ้ำอยู่ที่นี่ — ย้ายไปรวมจุดเดียวตาม ADR-005 debt #3)

    protected override AoEType GetAoEType() => AoEType.Circle;

    protected override void ConfigureTelegraphZone(TelegraphZone zone)
    {
        zone.radius = radius;
    }

    public override IEnumerator ExecuteCoroutine(NetworkBehaviour runner, GameObject telegraphPrefab)
    {
        if (actionDelay > 0f)
            yield return new WaitForSeconds(actionDelay);

        if (NetworkManager.Singleton == null || telegraphPrefab == null) yield break;

        float timer = 0f;
        float checkTimer = 0f;

        // Map ClientId -> Last Position & Idle Time
        Dictionary<ulong, Vector3> lastCheckPositions = new Dictionary<ulong, Vector3>();
        Dictionary<ulong, float> idleTimers = new Dictionary<ulong, float>();

        while (timer < mechanicDuration)
        {
            if (runner == null || !runner.NetworkObject.IsSpawned) yield break;

            checkTimer += Time.deltaTime;
            bool doCheck = false;
            if (checkTimer >= 0.25f)
            {
                doCheck = true;
                checkTimer = 0f;
            }

            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject != null)
                {
                    var pm = client.PlayerObject.GetComponent<playermove>();
                    if (pm != null && !pm.isDead.Value)
                    {
                        ulong cid = client.ClientId;
                        Vector3 currentPos = client.PlayerObject.transform.position;

                        if (!lastCheckPositions.ContainsKey(cid))
                        {
                            lastCheckPositions[cid] = currentPos;
                            idleTimers[cid] = 0f;
                        }

                        if (doCheck)
                        {
                            float dist = Vector3.Distance(currentPos, lastCheckPositions[cid]);

                            // ถ้าเคลื่อนที่น้อยกว่า 0.1 เมตรใน 0.25 วินาที ถือว่าหยุดนิ่ง (ความเร็ว < 0.4 m/s)
                            if (dist < 0.1f)
                            {
                                idleTimers[cid] += 0.25f;
                            }
                            else
                            {
                                idleTimers[cid] = 0f;
                            }

                            lastCheckPositions[cid] = currentPos;

                            if (idleTimers[cid] >= allowedIdleTime)
                            {
                                idleTimers[cid] = 0f; // รีเซ็ตเพื่อไม่ให้สปอว์นซ้ำรัวๆ
                                SpawnZoneAt(runner, telegraphPrefab, currentPos, Quaternion.identity);
                            }
                        }
                    }
                }
            }

            timer += Time.deltaTime;
            yield return null;
        }
    }
}
