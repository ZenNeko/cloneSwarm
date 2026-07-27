using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[CreateAssetMenu(fileName = "TetherAction", menuName = "Boss/Actions/TetherAction")]
public class TetherAction : BossAction
{
    [Header("Tether Settings")]
    public float tetherDistance = 25f;
    public float tetherDuration = 10f;
    public float tetherFailDamage = 50f;
    [Tooltip("ระยะห่างเสาจากผู้เล่นในกรณีเล่นคนเดียว")]
    public float tetherSoloSpawnOffset = 10f;

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

        var clients = new List<NetworkClient>(NetworkManager.Singleton.ConnectedClientsList);
        if (clients.Count == 0) yield break;

        Vector3 spawnPos = runner.transform.position;
        if (clients.Count == 1 && clients[0].PlayerObject != null)
        {
            // Solo Mode: สร้างเสาห่างจากผู้เล่น
            Vector3 playerPos = clients[0].PlayerObject.transform.position;
            Vector2 rand = Random.insideUnitCircle.normalized * tetherSoloSpawnOffset;
            spawnPos = new Vector3(playerPos.x + rand.x, playerPos.y, playerPos.z + rand.y);
        }

        var go = Instantiate(bossController.tetherPrefab, spawnPos, Quaternion.identity);
        var tether = go.GetComponent<BossTether>();
        var no = go.GetComponent<NetworkObject>();

        if (tether == null || no == null)
        {
            Destroy(go);
            yield break;
        }

        // โหลดค่าต่างๆ
        tether.requiredDistance = tetherDistance;
        tether.duration = tetherDuration;
        tether.failDamage = tetherFailDamage;

        no.Spawn(true);

        if (clients.Count == 1)
        {
            tether.ActivateSolo(clients[0].ClientId);
            Debug.Log($"[TetherAction] 🔗 Solo Tether → Client {clients[0].ClientId}");
        }
        else
        {
            // Co-op Mode: ผูกผู้เล่น 2 คน
            int idxA = Random.Range(0, clients.Count);
            int idxB;
            do { idxB = Random.Range(0, clients.Count); } while (idxB == idxA);
            
            tether.Activate(clients[idxA].ClientId, clients[idxB].ClientId);
            Debug.Log($"[TetherAction] 🔗 Tether: Client {clients[idxA].ClientId} ↔ Client {clients[idxB].ClientId}");
        }
    }
}
