using UnityEngine;

/// <summary>
/// ตาม player ของตัวเอง (IsOwner) โดยรับ Transform ผ่าน static event
/// </summary>
public class FollowCamera : MonoBehaviour
{
    [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -5f);

    private Transform target;

    void OnEnable()  => playermove.OnLocalPlayerSpawned += SetTarget;
    void OnDisable() => playermove.OnLocalPlayerSpawned -= SetTarget;

    void SetTarget(Transform t) => target = t;

    void LateUpdate()
    {
        if (target == null) return;
        transform.position = target.position + offset;
        transform.LookAt(target.position);
    }
}
