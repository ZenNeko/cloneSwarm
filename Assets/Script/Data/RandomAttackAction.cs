using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[CreateAssetMenu(fileName = "RandomAttackAction", menuName = "Boss/Actions/RandomAttackAction")]
public class RandomAttackAction : BossAction
{
    [Header("Pool of potential actions to pick from")]
    public List<BossAction> attackPool;

    [Header("Number of random attacks to execute simultaneously")]
    [Range(1, 5)]
    public int attackCount = 1;

    [Tooltip("วินาทีที่หน่วงเล็กน้อยระหว่างแต่ละท่าที่สุ่มได้ (เพื่อไม่ให้ปล่อยพร้อมกันสนิท)")]
    public float delayBetweenPicks = 0.4f;

    public override IEnumerator ExecuteCoroutine(NetworkBehaviour runner, GameObject telegraphPrefab)
    {
        if (actionDelay > 0f)
        {
            yield return new WaitForSeconds(actionDelay);
        }

        if (attackPool == null || attackPool.Count == 0) yield break;

        // Pick unique actions from the pool
        List<BossAction> available = new List<BossAction>(attackPool);
        List<BossAction> chosen = new List<BossAction>();

        int countToPick = Mathf.Min(attackCount, available.Count);
        for (int i = 0; i < countToPick; i++)
        {
            int index = Random.Range(0, available.Count);
            if (available[index] != null)
            {
                chosen.Add(available[index]);
            }
            available.RemoveAt(index); // Ensure uniqueness within this cast tick
        }

        // Execute each chosen action
        for (int i = 0; i < chosen.Count; i++)
        {
            if (chosen[i] != null && runner != null && runner.NetworkObject.IsSpawned)
            {
                runner.StartCoroutine(chosen[i].ExecuteCoroutine(runner, telegraphPrefab));
                if (delayBetweenPicks > 0f && i < chosen.Count - 1)
                {
                    yield return new WaitForSeconds(delayBetweenPicks);
                }
            }
        }
    }
}
