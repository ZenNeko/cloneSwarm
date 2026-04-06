using UnityEngine;

/// <summary>
/// หมุน GameObject หันหน้าหา Main Camera ทุก frame
/// ใช้กับ world-space Canvas เช่น progress bar บน ZoneObjective
/// </summary>
public class BillboardFaceCamera : MonoBehaviour
{
    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        transform.rotation = Quaternion.LookRotation(
            transform.position - cam.transform.position);
    }
}
