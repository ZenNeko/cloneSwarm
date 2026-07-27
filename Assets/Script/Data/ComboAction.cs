using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[CreateAssetMenu(fileName = "ComboAction", menuName = "Boss/Actions/ComboAction")]
public class ComboAction : BossAction
{
    [System.Serializable]
    public struct SubActionEntry
    {
        public BossAction action;
        [Tooltip("วินาทีที่หน่วงป้อนข้อมูลเพื่อปล่อยท่านี้ นับจากจุดเริ่มคอมโบ")]
        public float delayOffset;
    }

    [Header("Combo Choreography")]
    public List<SubActionEntry> subActions;

    public override IEnumerator ExecuteCoroutine(NetworkBehaviour runner, GameObject telegraphPrefab)
    {
        if (actionDelay > 0f)
        {
            yield return new WaitForSeconds(actionDelay);
        }

        foreach (var entry in subActions)
        {
            if (entry.action != null)
            {
                runner.StartCoroutine(RunSubActionDelayed(runner, telegraphPrefab, entry.action, entry.delayOffset));
            }
        }

        // คอมโบรันคู่ขนานกันไป คืนค่าทันทีเพื่อให้บอสล๊อคคูลดาวน์หลักได้ตามต้องการ
        yield break;
    }

    private IEnumerator RunSubActionDelayed(NetworkBehaviour runner, GameObject telegraphPrefab, BossAction action, float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (runner != null && runner.NetworkObject.IsSpawned)
        {
            yield return runner.StartCoroutine(action.ExecuteCoroutine(runner, telegraphPrefab));
        }
    }
}
