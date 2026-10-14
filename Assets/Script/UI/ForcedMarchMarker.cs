using UnityEngine;

/// <summary>
/// ลูกศรบนพื้นใต้ผู้เล่น บอกทิศที่จะโดนบังคับเดิน (ForcedMarchAction) — ภาพล้วน ไม่มีผลกับ gameplay
///
/// ไม่ต้องตั้งค่าอะไรใน prefab: playermove.ShowForcedMarchMarkerClientRpc เติม component นี้เองตอนใช้ครั้งแรก
/// และปั้น LineRenderer ของตัวเอง (แบบเดียวกับ WorldNumberTag ที่ปั้น TextMeshPro เอง)
///
/// ลูกศรยึดทิศของโลก ไม่หมุนตามตัวละคร — ทิศที่โดนบังคับคือทิศในสนาม ไม่ใช่ทิศที่ตัวละครหัน
/// </summary>
public class ForcedMarchMarker : MonoBehaviour
{
    const float Length   = 2.6f;
    const float Wing     = 0.9f;
    const float Width    = 0.32f;
    const float GroundY  = 0.08f;

    static readonly Color ArrowColor = new Color(1f, 0.85f, 0.2f, 1f);

    LineRenderer _line;
    Vector3      _dir = Vector3.forward;
    float        _hideAt;

    public void Show(Vector3 direction, float seconds)
    {
        direction.y = 0f;
        if (seconds <= 0f || direction.sqrMagnitude < 0.0001f)
        {
            Hide();
            return;
        }

        _dir    = direction.normalized;
        _hideAt = Time.time + seconds;
        EnsureLine();
        if (_line != null)
        {
            _line.enabled = true;
            UpdateShape();
        }
    }

    void Hide()
    {
        if (_line != null) _line.enabled = false;
    }

    void EnsureLine()
    {
        if (_line != null) return;

        var go = new GameObject("ForcedMarchArrow");
        go.transform.SetParent(transform, worldPositionStays: false);
        _line = go.AddComponent<LineRenderer>();
        _line.useWorldSpace     = true;
        _line.positionCount     = 5;
        _line.startWidth        = Width;
        _line.endWidth          = Width;
        _line.numCapVertices    = 2;
        _line.numCornerVertices = 2;
        _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _line.receiveShadows    = false;

        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                  ?? Shader.Find("Sprites/Default")
                  ?? Shader.Find("Unlit/Color");
        if (shader != null) _line.material = new Material(shader) { color = ArrowColor };
        _line.startColor = _line.endColor = ArrowColor;
        _line.enabled = false;
    }

    void LateUpdate()
    {
        if (_line == null || !_line.enabled) return;
        if (Time.time >= _hideAt) { Hide(); return; }
        UpdateShape();
    }

    void UpdateShape()
    {
        Vector3 basePos = transform.position;
        basePos.y += GroundY;

        Vector3 right = Vector3.Cross(Vector3.up, _dir);
        Vector3 tail  = basePos + _dir * 0.6f;
        Vector3 tip   = basePos + _dir * (0.6f + Length);
        Vector3 back  = tip - _dir * Wing;

        _line.SetPosition(0, tail);
        _line.SetPosition(1, tip);
        _line.SetPosition(2, back - right * Wing);
        _line.SetPosition(3, tip);
        _line.SetPosition(4, back + right * Wing);

        // กะพริบให้ตาจับ — ช่วงใกล้เดินจริงไม่ต่างกัน อ่านจาก cast bar/เวลาเตือนแทน
        float a = 0.65f + 0.35f * Mathf.Sin(Time.time * 10f);
        var c = ArrowColor; c.a = a;
        _line.startColor = _line.endColor = c;
    }

    void OnDestroy()
    {
        if (_line != null && _line.material != null) Destroy(_line.material);
    }
}
