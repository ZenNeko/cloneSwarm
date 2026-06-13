using Unity.Netcode;
using UnityEngine;

public class HealingOrb : NetworkBehaviour
{
    [Header("Healing")]
    [Tooltip("เปอร์เซ็นต์เลือดที่ฟื้นฟู (0.2 = 20%)")]
    public float healPercent = 0.2f;

    [Header("Pickup")]
    [Tooltip("ระยะ base ที่ orb เริ่มวิ่งเข้าหา player")]
    public float attractRadius = 4f;
    public float moveSpeed    = 8f;
    public float pickupRadius = 0.4f;

    [Header("Bob Animation")]
    public float bobHeight = 0.2f;
    public float bobSpeed  = 2f;

    private Transform currentTarget;
    private Vector3   startPos;
    private bool      forceAttract = false;

    private void Start()
    {
        // Fallback visual: ถ้าไม่มี MeshFilter ในวัตถุหรือลูก ให้สร้าง Sphere สีเขียวขึ้นมาโชว์
        if (GetComponentInChildren<MeshFilter>() == null)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.transform.SetParent(transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * 0.5f;
            
            if (visual.TryGetComponent<Collider>(out Collider col))
            {
                Destroy(col);
            }
            if (visual.TryGetComponent<Renderer>(out Renderer rend))
            {
                rend.material.color = Color.green;
            }
        }
    }

    public void ForceAttractTo(Transform target)
    {
        if (target == null) return;
        currentTarget = target;
        forceAttract = true;
        moveSpeed = 15f; // Speed up when magnetized
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        startPos      = transform.position;
        currentTarget = FindNearestPlayer();
    }

    void Update()
    {
        if (!IsServer) return;

        if (!forceAttract && (Time.frameCount % 90 == 0 || currentTarget == null))
            currentTarget = FindNearestPlayer();

        if (currentTarget == null) return;

        float dist = Vector3.Distance(transform.position, currentTarget.position);

        if (dist <= pickupRadius)
        {
            Collect();
            return;
        }

        if (forceAttract || dist <= GetAttractRadius())
        {
            transform.position = Vector3.MoveTowards(
                transform.position, currentTarget.position, moveSpeed * Time.deltaTime);
        }
        else
        {
            // Bob animation รอ player เข้ามา
            float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(startPos.x, newY, startPos.z);
        }
    }

    void Collect()
    {
        if (currentTarget != null)
        {
            var pm = currentTarget.GetComponent<playermove>();
            if (pm != null)
            {
                pm.HealPercent(healPercent);
            }
        }

        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        else Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (other.CompareTag("Player"))
        {
            var pm = other.GetComponentInParent<playermove>();
            if (pm != null) Collect();
        }
    }

    float GetAttractRadius()
    {
        if (currentTarget == null) return attractRadius;

        // ดึงค่ารัศมีขยายจาก PlayerStatManager (ถ้ามี)
        var sm = currentTarget.GetComponent<PlayerStatManager>();
        float mult = sm != null ? sm.GetPickupRadiusMultiplier() : 1f;
        return attractRadius * mult;
    }

    Transform FindNearestPlayer()
    {
        if (NetworkManager.Singleton == null) return null;

        Transform nearest = null;
        float     minDist = float.MaxValue;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var obj = client.PlayerObject;
            if (obj == null) continue;

            float dist = Vector3.Distance(transform.position, obj.transform.position);
            if (dist < minDist) { minDist = dist; nearest = obj.transform; }
        }
        return nearest;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, GetAttractRadius());
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}
