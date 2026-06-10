using UnityEngine;

/// <summary>
/// VFX API — Thin wrapper สำหรับ weapon scripts
///
/// Delegates ทุก call ลง NetworkedVFXPool ซึ่งใช้ object pooling จริง (ไม่มี GC spike)
///
/// Weapon scripts ใช้:
///   VFXFactory.Play("SlashHit", pos)
///   VFXFactory.PlayBeam("None", from, to)
///
/// Setup ใน Unity:
///   — กำหนด prefab ใน VFXDatabase (ScriptableObject)
///   — กำหนด beamPrefab ใน NetworkedVFXPool.beamPrefab (Inspector)
///   — ไม่ต้องวาง VFXFactory ใน scene แยก (NetworkedVFXPool ทำทุกอย่าง)
/// </summary>
public static class VFXFactory
{
    /// <summary>Spawn VFX ณ ตำแหน่งโลก (ใช้ object pool — ไม่มี GC)</summary>
    public static void Play(string key, Vector3 position, float scale = 1f)
    {
        if (NetworkedVFXPool.Instance == null)
        {
            Debug.LogWarning("[VFXFactory] NetworkedVFXPool.Instance ไม่พบ — วาง NetworkedVFXPool ใน scene");
            return;
        }
        NetworkedVFXPool.Instance.PlayByName(key, position, scale);
    }

    /// <summary>Spawn Beam จาก from → to + burst VFX ที่ปลาย (ใช้ object pool)</summary>
    public static void PlayBeam(string key, Vector3 from, Vector3 to, float duration = 0.15f)
    {
        if (NetworkedVFXPool.Instance == null)
        {
            Debug.LogWarning("[VFXFactory] NetworkedVFXPool.Instance ไม่พบ");
            return;
        }

        NetworkedVFXPool.Instance.PlayBeam(from, to, duration);

        // Burst VFX ที่ปลาย beam — ใช้ key เดิมที่ส่งเข้ามา
        if (!string.IsNullOrEmpty(key) && key != "None")
            NetworkedVFXPool.Instance.PlayByName(key, to);
    }
}
