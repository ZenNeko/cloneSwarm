using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    private Camera _camera;
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset;
    void Start()
    {
        
    }

    // Update is called once per frame
    void LateUpdate()
    {
        _camera = GetComponent<Camera>();
        if (_camera != null && target != null)
        {
            transform.position = target.position + offset;
            transform.LookAt(target.position);
        }
    }
}
