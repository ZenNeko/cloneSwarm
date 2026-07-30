using UnityEngine;

/// <summary>
/// หมุน GameObject หันหน้าหา Main Camera ทุก frame
/// ใช้กับ world-space Canvas เช่น progress bar บน ZoneObjective
/// </summary>
public class BillboardFaceCamera : MonoBehaviour
{
    private Camera _cam;

    void LateUpdate()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;
        transform.rotation = Quaternion.LookRotation(
            transform.position - _cam.transform.position);
    }
}
