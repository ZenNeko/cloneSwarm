using Unity.Netcode;
using UnityEngine;

/// <summary>
/// GrenadeProjectile — NetworkObject
/// บินไปยัง targetPos แล้วระเบิด AoE
/// ถ้า cluster = true → ยิง pellets รอบทิศทางหลังระเบิด
///
/// Prefab setup:
///   • NetworkObject component
///   • Rigidbody (kinematic=true)
///   • Sphere Collider (isTrigger optional)
///   • GrenadeProjectile script
/// </summary>
public class GrenadeProjectile : NetworkBehaviour
{
    [HideInInspector] public float   damage;
    [HideInInspector] public float   radius;
    [HideInInspector] public float   fuseTime;
    [HideInInspector] public bool    cluster;
    [HideInInspector] public Vector3 targetPos;
    [HideInInspector] public PlayerWeaponManager weaponManager;

    [Header("Visual")]
    public GameObject explosionVfxPrefab;

    [Header("Cluster")]
    public int   clusterPellets    = 8;
    public float clusterDmgPercent = 0.4f;   // % ของ damage หลัก
    public float clusterProjSpeed  = 14f;

    private float   timer;
    private float   travelTime;
    private Vector3 startPos;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        startPos   = transform.position;
        travelTime = Mathf.Clamp(Vector3.Distance(startPos, targetPos) / 12f, 0.3f, 2f);
    }

    void Update()
    {
        if (!IsServer) return;

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / travelTime);
        // arc trajectory
        float arcHeight = 1.5f;
        Vector3 pos = Vector3.Lerp(startPos, targetPos, t);
        pos.y += arcHeight * Mathf.Sin(t * Mathf.PI);
        transform.position = pos;

        if (timer >= travelTime)
        {
            timer = float.MaxValue;
            Explode();
        }
    }

    void Explode()
    {
        // AoE damage
        var mask = LayerMask.GetMask("Enemy");
        foreach (var c in Physics.OverlapSphere(targetPos, radius, mask))
            c.GetComponent<Enemy>()?.EnemyTakeDamage(damage);

        // Cluster pellets
        if (cluster && weaponManager != null)
        {
            float pelletDmg = damage * clusterDmgPercent;
            for (int i = 0; i < clusterPellets; i++)
            {
                float   angle = (360f / clusterPellets) * i;
                Vector3 dir   = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                weaponManager.FireProjectileServerRpc(
                    targetPos + Vector3.up * 0.3f, dir,
                    pelletDmg, clusterProjSpeed, 1, 0f);
            }
        }

        // VFX (all clients)
        SpawnVfxClientRpc(targetPos);

        if (NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }

    [ClientRpc]
    void SpawnVfxClientRpc(Vector3 pos)
    {
        if (explosionVfxPrefab != null)
            Destroy(Instantiate(explosionVfxPrefab, pos, Quaternion.identity), 3f);
        else
            VFXFactory.Play(cluster ? VFXType.GrenadeExplosion : VFXType.GrenadeExplosion, pos);
    }
}
