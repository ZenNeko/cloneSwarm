using System;
using UnityEngine;

/// <summary>
/// ช่องนี้เก็บ **ชื่อ stem** ของ LayeredTrack — Inspector เปลี่ยนเป็น dropdown (StemIdDrawer)
///
/// อ้างด้วยชื่อไม่ใช่เลขลำดับ เพราะแต่ละแมพมีจำนวน stem ไม่เท่ากัน และเพิ่ม stem ทีหลังได้ ·
/// ถ้าเก็บเป็นลำดับ แทรก stem ใหม่ตรงกลางแล้วค่าหลังจุดนั้นเลื่อนไปผิดชั้นเงียบๆ
/// </summary>
public class StemIdAttribute : PropertyAttribute { }

[Serializable]
public struct StemLevel
{
    [StemId] public string stem;
    [Range(0f, 1f)] public float level;
}

[Serializable]
public class StemMix
{
    [Tooltip("stem ที่ไม่อยู่ในรายการ = เงียบ (0)")]
    public StemLevel[] levels = new StemLevel[0];

    [Tooltip("เวลา fade เข้า mix นี้ (วินาที) · ติดลบ = ใช้ค่าตั้งต้นของ profile")]
    public float fadeSeconds = -1f;
}

/// <summary>
/// เพลงของหนึ่งแมพ/หนึ่งระดับความยาก — ผูกที่ MapData.TierContent.musicProfile
/// MusicDirector อ่านแล้วคำนวณว่าตอนนี้แต่ละชั้นควรดังเท่าไร
///
/// ลำดับความสำคัญ (ดู docs/design-dynamic-bgm.md ข้อ 4.2):
///   ชนะ/แพ้  >  บอสใหญ่  >  max(ช่วงเวลา, overlay มินิบอส)  แล้วค่อยใส่ override ของจอเลือกการ์ดทับ
/// </summary>
[CreateAssetMenu(fileName = "Music_New", menuName = "Clone Swarm/Audio/Music Profile")]
public class MusicProfile : ScriptableObject
{
    [Serializable]
    public struct TimeBand
    {
        [Tooltip("นาทีที่ช่วงนี้เริ่ม · ช่วงที่นาทีน้อยสุดครอบตั้งแต่เริ่มเกมเสมอ")]
        [Min(0f)] public float atMinutes;
        public StemMix mix;
    }

    [Header("เพลงหลักของรัน")]
    public LayeredTrack track;

    [Tooltip("ชั้นที่เปิดตามนาที — เรียงแบบเดียวกับ WavePhase")]
    public TimeBand[] timeBands = new TimeBand[0];

    [Header("มินิบอส")]
    [Tooltip("ชั้นที่เปิดเพิ่มตอนมินิบอสมีชีวิต · ใช้ค่าที่มากกว่าต่อชั้น ไม่ลดชั้นที่เปิดอยู่")]
    public StemMix miniBossOverlay = new StemMix();

    [Tooltip("ค้าง overlay ไว้กี่วินาทีหลังมินิบอสตัวสุดท้ายตาย — กันชั้นเปิดปิดถี่")]
    [Min(0f)] public float miniBossReleaseHold = 3f;

    [Header("บอสใหญ่")]
    [Tooltip("ธีมบอส · ว่าง = เพลงเดิมเล่นต่อและค้าง mix ล่าสุด ไม่ปรับอะไร")]
    public LayeredTrack mainBossTrack;

    [Tooltip("ใช้เฉพาะเมื่อมีธีมบอส · index = เฟส · ขาดช่องไหนใช้ช่องก่อนหน้า · อ้าง stem ของธีมบอส")]
    public StemMix[] mainBossPhases = new StemMix[0];

    [Header("จอเลือกการ์ด (Level Up / Orb)")]
    [Tooltip("stem ที่มีในรายการ = บังคับเป็นค่านี้ · ไม่มีในรายการ = ไม่แตะ")]
    public StemLevel[] cardPickOverrides = new StemLevel[0];

    [Tooltip("ลดเสียงทั้งเพลงระหว่างเลือกการ์ด (1 = ไม่ลด)")]
    [Range(0f, 1f)] public float cardPickDuck = 0.5f;

    [Tooltip("fade เข้า/ออกจอเลือกการ์ด — สั้นกว่าปกติ เพราะจอเด้งขึ้นทันที")]
    [Min(0f)] public float cardPickFade = 0.4f;

    [Header("จบรัน")]
    public AudioClip winStinger;
    public AudioClip loseStinger;

    [Tooltip("fade เพลงลงตอนจบรัน (วินาที)")]
    [Min(0f)] public float endFade = 1.5f;

    [Header("ค่าตั้งต้น")]
    [Min(0f)] public float defaultFade = 2f;

    public float FadeOf(StemMix mix) =>
        mix != null && mix.fadeSeconds >= 0f ? mix.fadeSeconds : defaultFade;

    /// <summary>band ที่ครอบนาทีนี้ · -1 ถ้าไม่มี band เลย</summary>
    public int BandIndexAt(float minutes)
    {
        if (timeBands == null || timeBands.Length == 0) return -1;

        // ไม่พึ่งลำดับใน Inspector — หา band ที่เริ่มช้าสุดแต่ยังไม่เกินนาทีนี้
        int best = -1, earliest = 0;
        for (int i = 0; i < timeBands.Length; i++)
        {
            if (timeBands[i].atMinutes < timeBands[earliest].atMinutes) earliest = i;
            if (timeBands[i].atMinutes <= minutes &&
                (best < 0 || timeBands[i].atMinutes >= timeBands[best].atMinutes))
                best = i;
        }
        return best >= 0 ? best : earliest;   // band แรกครอบตั้งแต่เริ่มเกมเสมอ
    }

    /// <summary>mix ของเฟสนี้ · ขาดช่องใช้ช่องก่อนหน้า · null ถ้าไม่มีเลย</summary>
    public StemMix PhaseMix(int phase)
    {
        if (mainBossPhases == null || mainBossPhases.Length == 0) return null;
        return mainBossPhases[Mathf.Clamp(phase, 0, mainBossPhases.Length - 1)];
    }
}
