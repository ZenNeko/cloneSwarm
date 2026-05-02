using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Elite outline visual — ใช้ inverted hull mesh trick (ไม่ต้องใช้ shader พิเศษ)
///
/// สร้าง child copy ของทุก MeshRenderer ในตัว enemy:
///   - flip normal (cull front face)
///   - scale up เล็กน้อย (1.05x)
///   - apply outline material สี outlineColor
///
/// Fallback ถ้าไม่มี mesh: ไม่ทำอะไร (crown ก็พอ)
/// </summary>
public class EliteOutline : MonoBehaviour
{
    [Header("Outline (set by EliteController)")]
    public Color  outlineColor = Color.yellow;
    [Tooltip("scale-up ratio ของ inverted hull")]
    public float  outlineScale = 1.05f;
    [Tooltip("Material override (ปล่อยว่าง = สร้าง runtime URP unlit)")]
    public Material outlineMaterial;

    private List<GameObject> _outlineObjects = new();

    public void Apply()
    {
        Clear();

        var renderers = GetComponentsInChildren<MeshRenderer>(includeInactive: true);
        Material outlineMat = outlineMaterial != null ? outlineMaterial : CreateOutlineMaterial();

        foreach (var rend in renderers)
        {
            var mf = rend.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            var go = new GameObject(rend.name + "_Outline");
            go.transform.SetParent(rend.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale    = Vector3.one * outlineScale;

            var mfNew = go.AddComponent<MeshFilter>();
            mfNew.sharedMesh = mf.sharedMesh;

            var mrNew = go.AddComponent<MeshRenderer>();
            mrNew.sharedMaterial = outlineMat;
            mrNew.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mrNew.receiveShadows    = false;

            _outlineObjects.Add(go);
        }
    }

    public void Clear()
    {
        foreach (var go in _outlineObjects)
            if (go != null) Destroy(go);
        _outlineObjects.Clear();
    }

    Material CreateOutlineMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit")
                  ?? Shader.Find("Unlit/Color");
        var mat = new Material(shader)
        {
            color = outlineColor,
        };
        // URP Cull mode: 1=Front (we want to render front of inverted hull only)
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 1f);
        return mat;
    }

    void OnDestroy()
    {
        Clear();
    }
}
