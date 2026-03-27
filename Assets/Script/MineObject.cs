using Unity.Netcode;
using UnityEngine;

/// <summary>
/// MineObject — NetworkObject
/// วางบนพื้น ระเบิดเมื่อศัตรูเข้า triggerRadius หรือหมดอายุ
///
/// Prefab setup:
///   • NetworkObject component
///   • MineObject script
///   (ไม่ต้องมี Collider ใน scene — ใช้ OverlapSphere check)
/// </summary>
public class MineObject : NetworkBehaviour
{
    [HideInInspector] public float damage;
    [HideInInspector] public float triggerRadius = 1.5f;

    [Header("Settings")]
    public float lifetime   = 15f;   // หมดอายุ (วินาที)
    public float checkRate  = 0.2f;  // ตรวจทุก X วินาที

    [Header("Visual")]
    public GameObject explodeVfxPrefab;

    private float lifeTimer;
    private float checkTimer;
    private bool  exploded;

    void Update()
    {
        if (!IsServer || exploded) return;

        lifeTimer  += Time.deltaTime;
        checkTimer += Time.deltaTime;

        if (lifeTimer >= lifetime)
        {
            Detonate(); return;
        }

        if (checkTimer < checkRate) return;
        checkTimer = 0f;

        var mask = LayerMask.GetMask("Enemy");
        var hits = Physics.OverlapSphere(transform.position, triggerRadius, mask);
        if (hits.Length > 0) Detonate();
    }

    void Detonate()
    {
        if (exploded) return;
        exploded = true;

        var mask = LayerMask.GetMask("Enemy");
        foreach (var c in Physics.OverlapSphere(transform.position, triggerRadius * 2f, mask))
            c.GetComponent<Enemy>()?.EnemyTakeDamage(damage);

        ExplodeClientRpc(transform.position);
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
    }

    [ClientRpc]
    void ExplodeClientRpc(Vector3 pos)
    {
        if (explodeVfxPrefab != null)
            Destroy(Instantiate(explodeVfxPrefab, pos, Quaternion.identity), 3f);
    }
}
