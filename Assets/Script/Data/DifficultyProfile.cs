using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// กติกากลางของระดับความยากหนึ่งระดับ — ทุกแมพใช้ร่วมกัน (docs/design-miniboss-roster-and-difficulty.md §10)
///
/// ═══ แยกจาก MapData ═══
/// ระดับความยากเป็น **กติกาของเกม** ไม่ใช่เนื้อหาของแมพ · Rabbit and Steel ใช้ตัวคูณชุดเดียวทุกด่าน
/// ถ้าใส่ใน MapData.tier ทุกแมพต้องกรอกซ้ำ แล้ววันหนึ่งแมพใหม่จะลืมกรอก
/// MapData.tier เหลือแค่เนื้อหา (ตาราง · wave · เพลง · config ทับ)
///
/// ═══ ท่าต่างกันไม่ได้อยู่ที่นี่ ═══
/// ท่าที่เพิ่มในระดับสูงคือคลิปใน timeline ที่ตั้งช่วงระดับ (TimelineClip.limitTiers) — ไฟล์บอสไฟล์เดียว
///
/// ไฟล์อยู่ใต้ Resources/Difficulty/ (หนึ่งไฟล์ต่อระดับ) · ไม่มีไฟล์ = ค่ากลาง ×1 ทุกช่อง = พฤติกรรมเดิม
/// ใช้บน server เท่านั้น ไม่มีอะไรต้อง sync
///
/// ═══ แมพที่อยากต่าง ═══
/// MapData.TierContent.difficultyOverride ชี้ไฟล์เฉพาะของแมพนั้น (ทั้งไฟล์ ไม่ผสมกับค่ากลาง) — ดู <see cref="Resolve"/>
/// ไฟล์เฉพาะแมพต้องอยู่ **นอก** Resources/Difficulty ไม่งั้นถูกนับเป็นค่ากลางของระดับนั้นซ้ำ
/// </summary>
[CreateAssetMenu(fileName = "DifficultyProfile_Normal", menuName = "LoL Swarm/Difficulty Profile")]
public class DifficultyProfile : ScriptableObject
{
    public DifficultyTier tier = DifficultyTier.Normal;

    [Header("HP — ศัตรูทุกตัว")]
    [Tooltip("HP ศัตรูทั่วไป มินิบอส และบอสใหญ่ — ตัวคูณเดียว (คูณทับ enemyScaling ของแมพ)\n" +
             "เดิมแยก bossHpMult ไว้ แล้วสองค่าเพี้ยนจากกันเอง (Easy บอส ×0.7 แต่ศัตรู ×0.8) — รวมเหลือตัวเดียว")]
    [Min(0.05f)] public float enemyHpMult = 1f;

    [Header("บอส + มินิบอส")]
    [Tooltip("ดาเมจ AoE และโซ่")]
    [Min(0f)] public float bossDamageMult = 1f;
    [Tooltip("เวลาเตือนของ AoE · น้อยกว่า 1 = หลบยากขึ้น")]
    [Min(0.1f)] public float bossWarningMult = 1f;
    [Tooltip("ช่วงห่างระหว่างท่า (attackInterval / cooldownAfter)")]
    [Min(0.1f)] public float bossIntervalMult = 1f;
    [Tooltip("ปิด = ไม่มี enrage ในระดับนี้ (เช่น Easy)")]
    public bool enrageEnabled = true;
    [Tooltip("เวลา enrage ของเฟส × ค่านี้ · น้อยกว่า 1 = ต้องตีเร็วขึ้น")]
    [Min(0.1f)] public float enrageTimeMult = 1f;

    [Header("จำนวนผู้เล่น — HP บอส/มินิบอส")]
    [Tooltip("ตัวคูณ HP ตามจำนวนผู้เล่นตอนบอสเกิด · ช่อง 0 = 1 คน … ช่อง 3 = 4 คน\n" +
             "จุดเริ่มจาก Rabbit and Steel: 0.9 / 1.7 / 2.4 / 3.0 — HP รวมมากขึ้น แต่ต่อคนน้อยลง")]
    public float[] hpByPlayers = { 0.9f, 1.7f, 2.4f, 3.0f };

    [Header("รางวัล")]
    [Tooltip("exp จากศัตรูทั่วไป × ค่านี้")]
    [Min(0f)] public float expMult = 1f;

    /// <summary>ตัวคูณ HP ตามจำนวนผู้เล่น · เกินตารางใช้ช่องสุดท้าย · ตารางว่าง = 1</summary>
    public float HpForPlayers(int players)
    {
        if (hpByPlayers == null || hpByPlayers.Length == 0) return 1f;
        int i = Mathf.Clamp(players - 1, 0, hpByPlayers.Length - 1);
        return Mathf.Max(0.05f, hpByPlayers[i]);
    }

    // ── หาไฟล์ของระดับ ───────────────────────────────────────────────────
    const string ResourcesDir = "Difficulty";
    static Dictionary<DifficultyTier, DifficultyProfile> s_cache;
    static DifficultyProfile s_neutral;

    /// <summary>ระดับของรันนี้ (RunSetup.Difficulty) บนแมพของรันนี้ — แมพทับได้</summary>
    public static DifficultyProfile Current => Resolve(RunSetup.Map, RunSetup.Difficulty);

    /// <summary>
    /// ตัวคูณที่ใช้จริงของแมพ+ระดับ · แมพตั้ง difficultyOverride ไว้ = ไฟล์นั้น · ไม่ตั้ง = ค่ากลางของระดับ
    /// เทียบระดับแบบตรงตัว — ไม่ใช้ GetTier เพราะมันถอยไปหา Normal ถ้าไม่มีระดับนั้น
    /// แล้ว override ของ Normal จะไปโผล่ใน Savage/Epic ที่แมพไม่ได้ตั้งไว้
    /// </summary>
    public static DifficultyProfile Resolve(MapData map, DifficultyTier tier)
    {
        if (map != null && map.tiers != null)
            foreach (var t in map.tiers)
                if (t != null && t.tier == tier && t.difficultyOverride != null) return t.difficultyOverride;
        return For(tier);
    }

    /// <summary>ไฟล์ของระดับที่ขอ · ไม่มี = ค่ากลาง ×1 (HP ตามจำนวนคน = 1 ด้วย — ไม่มีไฟล์ต้องไม่เปลี่ยนเกม)</summary>
    public static DifficultyProfile For(DifficultyTier tier)
    {
        var cache = s_cache;
        if (cache == null)
        {
            // สร้างในตัวแปรชั่วคราวแล้วค่อยใส่ — LoadAll ใน editor เรียก OnValidate ของไฟล์ที่โหลด
            // ซึ่งล้าง s_cache กลางลูป (เจอจาก test: NullReference ตอนเรียกครั้งแรก)
            cache = new Dictionary<DifficultyTier, DifficultyProfile>();
            foreach (var p in Resources.LoadAll<DifficultyProfile>(ResourcesDir))
            {
                if (p == null) continue;
                if (cache.ContainsKey(p.tier))
                    Debug.LogWarning($"[DifficultyProfile] มีสองไฟล์ของ {p.tier} — ใช้ '{cache[p.tier].name}' ข้าม '{p.name}'");
                else cache[p.tier] = p;
            }
            s_cache = cache;
        }
        if (cache.TryGetValue(tier, out var found)) return found;

        if (s_neutral == null)
        {
            s_neutral = CreateInstance<DifficultyProfile>();
            s_neutral.name = "(ค่ากลาง)";
            s_neutral.hpByPlayers = new[] { 1f };
            s_neutral.hideFlags = HideFlags.HideAndDontSave;
        }
        return s_neutral;
    }

    /// <summary>ล้างแคช — editor เรียกหลังสร้าง/แก้ไฟล์ (ตอนรันเกมไม่ต้อง)</summary>
    public static void ClearCache() => s_cache = null;

#if UNITY_EDITOR
    void OnValidate() => ClearCache();
#endif
}
