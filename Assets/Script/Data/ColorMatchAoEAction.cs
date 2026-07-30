using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[CreateAssetMenu(fileName = "ColorMatchAoEAction", menuName = "Boss/Actions/ColorMatchAoEAction")]
public class ColorMatchAoEAction : BossAction
{
    [Header("Color Match Settings")]
    public float radius = 3f;
    public float warningDuration = 3.5f;
    public float damage = 99f; // High damage for failing

    [Tooltip("ระยะห่างจากบอสที่วงเวทย์จะสุ่มเกิด (0 = สุ่มรอบตัว)")]
    public float spawnRadius = 6f;

    public override IEnumerator ExecuteCoroutine(NetworkBehaviour runner, GameObject telegraphPrefab)
    {
        if (actionDelay > 0f)
            yield return new WaitForSeconds(actionDelay);

        if (NetworkManager.Singleton == null || telegraphPrefab == null || runner == null) yield break;

        List<ulong> activePlayers = new List<ulong>();
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
            {
                var pm = client.PlayerObject.GetComponent<playermove>();
                if (pm != null && !pm.isDead.Value)
                {
                    activePlayers.Add(client.ClientId);
                }
            }
        }

        foreach (var pId in activePlayers)
        {
            // สุ่มจุดเกิดวงเวทย์รอบๆ บอส
            Vector2 randCircle = Random.insideUnitCircle.normalized * spawnRadius;
            Vector3 spawnPos = runner.transform.position + new Vector3(randCircle.x, 0.1f, randCircle.y);

            SpawnColoredZone(spawnPos, telegraphPrefab, pId, runner as BossController);
        }
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
            string colorName = (clientId % 4) switch
            {
                0 => "RED",
                1 => "BLUE",
                2 => "GREEN",
                _ => "YELLOW"
            };
            Color uiColor = (clientId % 4) switch
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
