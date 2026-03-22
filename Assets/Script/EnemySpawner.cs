using Unity.Netcode;
using UnityEngine;

public class EnemySpawner : NetworkBehaviour
{
    public GameObject enemyPrefab;
    public float spawnRate   = 1f;
    public float spawnRadius = 10f;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        // Spawn เฉพาะ Server
        if (!IsServer) return;
        InvokeRepeating(nameof(SpawnEnemy), 1f, spawnRate);
    }

    public override void OnNetworkDespawn()
    {
        CancelInvoke(nameof(SpawnEnemy));
    }

    // ── Spawn ─────────────────────────────────────────────────────────────
    void SpawnEnemy()
    {
        if (!IsServer) return;

        Transform spawnNear = GetRandomPlayerTransform();
        if (spawnNear == null) return;

        Vector2 rand = Random.insideUnitCircle.normalized;
        Vector3 pos  = spawnNear.position + new Vector3(rand.x, 0f, rand.y) * spawnRadius;

        GameObject enemy = Instantiate(enemyPrefab, pos, Quaternion.identity);
        enemy.GetComponent<NetworkObject>()?.Spawn(true);
        // Enemy.OnNetworkSpawn จะหา target เองอัตโนมัติ
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    Transform GetRandomPlayerTransform()
    {
        var clients = NetworkManager.Singleton.ConnectedClientsList;
        if (clients.Count == 0) return null;

        int idx = Random.Range(0, clients.Count);
        return clients[idx].PlayerObject?.transform;
    }

    // ── Gizmo ─────────────────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.red;
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, spawnRadius);
#endif
    }
}
