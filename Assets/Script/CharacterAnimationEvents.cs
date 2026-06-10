using UnityEngine;

/// <summary>
/// Receiver สำหรับ Animation Events ที่ฝังในไฟล์ FBX ของ character pack
/// (Synty / Mixamo / Custom rig — มักมี OnFootstep, OnLand, OnHit ฯลฯ)
///
/// **ติดตั้ง:** แนบบน root ของ character model prefab (เดียวกับ Animator)
///
/// **ขยาย:** เพิ่ม method ตามชื่อ event ใน animation clip (case-sensitive)
/// ดูใน Animation window → Events ของแต่ละ clip
///
/// **ทำไมต้องมี:** Unity AnimationEvent หา method โดยชื่อในทุก MonoBehaviour
/// บน GameObject เดียวกับ Animator — ถ้าไม่เจอ → warning ทุก frame ที่ event firing
/// </summary>
public class CharacterAnimationEvents : MonoBehaviour
{
    [Header("Footstep SFX")]
    [Tooltip("เสียงเดิน — สุ่มเล่น 1 clip ต่อ step (left/right slot ใช้ปนกัน)")]
    public AudioClip[] footstepClips;
    [Range(0f, 1f)] public float footstepVolume = 0.4f;
    [Range(0f, 0.5f)] public float footstepPitchVariance = 0.1f;

    [Header("Land SFX (ตอนกระโดดลง / dash จบ)")]
    public AudioClip[] landClips;
    [Range(0f, 1f)] public float landVolume = 0.6f;

    [Header("VFX (Optional)")]
    [Tooltip("Dust puff prefab ที่เท้าตอนเดิน — ปล่อยว่างได้")]
    public GameObject footstepVfxPrefab;

    // ── Animation Event Receivers ─────────────────────────────────────────
    // ⚠ ต้องตรงชื่อกับ event ใน clip (case-sensitive) — Unity invoke ผ่าน reflection
    //
    // **No-op design**: ถ้าไม่ assign clip / vfx ใน Inspector → method ทำงานเงียบๆ
    //   ไม่แตะ SoundManager.Instance (กัน auto-create) ไม่ allocate VFX
    //   — Unity ยังเรียกผ่าน animation event แต่ฟังก์ชัน return ทันที

    /// <summary>Default footstep ของ character pack ส่วนใหญ่</summary>
    public void OnFootstep(AnimationEvent _ = null)
    {
        // SFX — เล่นเฉพาะถ้ามี clip
        bool hasSfx = footstepClips != null && footstepClips.Length > 0;
        if (hasSfx)
            SoundManager.Instance.PlayRandomSfx(footstepClips, transform.position,
                footstepVolume, footstepPitchVariance);

        // VFX — spawn เฉพาะถ้ามี prefab
        if (footstepVfxPrefab != null)
        {
            var go = Instantiate(footstepVfxPrefab, transform.position, Quaternion.identity);
            Destroy(go, 2f);
        }
    }

    /// <summary>ตอนกระโดดลงพื้น / dash landing</summary>
    public void OnLand(AnimationEvent _ = null)
    {
        if (landClips == null || landClips.Length == 0) return;
        SoundManager.Instance.PlayRandomSfx(landClips, transform.position, landVolume, 0.05f);
    }

    // ── Stubs (กัน warning ของ events ที่ไม่ได้ใช้) ───────────────────────
    // ถ้า clip อื่นมี event อะไร เพิ่ม method ที่นี่แล้วเดี๋ยว Unity จะหยุดเตือน
    public void OnHit(AnimationEvent _ = null)             { /* hook ภายหลัง */ }
    public void OnAttackHit(AnimationEvent _ = null)       { /* hook ภายหลัง */ }
    public void OnAttackEnd(AnimationEvent _ = null)       { /* hook ภายหลัง */ }
    public void OnDeathEnd(AnimationEvent _ = null)        { /* hook ภายหลัง */ }
}
