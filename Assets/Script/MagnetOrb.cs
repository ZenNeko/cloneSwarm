using Unity.Netcode;
using UnityEngine;

public class MagnetOrb : NetworkBehaviour
{
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

    private void Start()
    {
        // Fallback visual: ถ้าไม่มี MeshFilter ในวัตถุหรือลูก ให้สร้าง Sphere สีม่วง/แดง (Magenta) ขึ้นมาโชว์
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
                rend.material.color = Color.magenta;
            }
        }
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

        if (Time.frameCount % 90 == 0 || currentTarget == null)
            currentTarget = FindNearestPlayer();

        if (currentTarget == null) return;

        float dist = Vector3.Distance(transform.position, currentTarget.position);

        if (dist <= pickupRadius)
        {
            Collect();
            return;
        }

        if (dist <= GetAttractRadius())
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

    private bool isCollected = false;

    void Collect()
    {
        if (isCollected) return;
        isCollected = true;
        if (currentTarget != null)
        {
            // ดึงดูด ExpOrb ทั้งหมดบนแผนที่เข้าหาตัวผู้เล่นคนนี้
            ExpOrb[] expOrbs = Object.FindObjectsByType<ExpOrb>(FindObjectsSortMode.None);
            foreach (var orb in expOrbs)
            {
                orb.ForceAttractTo(currentTarget);
            }

            // ดึงดูด HealingOrb ทั้งหมดบนแผนที่เข้าหาตัวผู้เล่นคนนี้
            HealingOrb[] healingOrbs = Object.FindObjectsByType<HealingOrb>(FindObjectsSortMode.None);
            foreach (var orb in healingOrbs)
            {
                orb.ForceAttractTo(currentTarget);
            }

            // ดึงดูด ObjectiveOrb ทั้งหมดบนแผนที่เข้าหาตัวผู้เล่นคนนี้
            ObjectiveOrb[] objectiveOrbs = Object.FindObjectsByType<ObjectiveOrb>(FindObjectsSortMode.None);
            foreach (var orb in objectiveOrbs)
            {
                orb.ForceAttractTo(currentTarget);
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
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, GetAttractRadius());
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}
