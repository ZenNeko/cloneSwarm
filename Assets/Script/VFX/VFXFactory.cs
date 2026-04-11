using UnityEngine;

/// <summary>
/// VFX API — Thin wrapper สำหรับ weapon scripts
///
/// Delegates ทุก call ลง NetworkedVFXPool ซึ่งใช้ object pooling จริง (ไม่มี GC spike)
///
/// Weapon scripts ใช้:
///   VFXFactory.Play(VFXType.SlashHit, pos)
///   VFXFactory.PlayBeam(VFXType.None, from, to)
///
/// Setup ใน Unity:
///   — กำหนด prefab ใน NetworkedVFXPool.vfxTypeMappings[] (Inspector)
///   — กำหนด beamPrefab ใน NetworkedVFXPool.beamPrefab (Inspector)
///   — ไม่ต้องวาง VFXFactory ใน scene แยก (NetworkedVFXPool ทำทุกอย่าง)
/// </summary>
public static class VFXFactory
{
    /// <summary>Spawn VFX ณ ตำแหน่งโลก (ใช้ object pool — ไม่มี GC)</summary>
    public static void Play(VFXType type, Vector3 position, float scale = 1f)
    {
        if (NetworkedVFXPool.Instance == null)
        {
            Debug.LogWarning("[VFXFactory] NetworkedVFXPool.Instance ไม่พบ — วาง NetworkedVFXPool ใน scene");
            return;
        }
        NetworkedVFXPool.Instance.PlayByType(type, position, scale);
    }

    /// <summary>Spawn Beam จาก from → to + burst VFX ที่ปลาย (ใช้ object pool)</summary>
    public static void PlayBeam(VFXType type, Vector3 from, Vector3 to, float duration = 0.15f)
    {
        if (NetworkedVFXPool.Instance == null)
        {
            Debug.LogWarning("[VFXFactory] NetworkedVFXPool.Instance ไม่พบ");
            return;
        }

        NetworkedVFXPool.Instance.PlayBeam(from, to, duration);

        // Burst VFX ที่ปลาย beam — ใช้ type เดิมที่ส่งเข้ามา
        if (type != VFXType.None)
            NetworkedVFXPool.Instance.PlayByType(type, to);
    }
}
