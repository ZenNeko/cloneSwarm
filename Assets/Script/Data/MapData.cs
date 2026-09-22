using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "Map_New", menuName = "LoL Swarm/Map Data")]
public class MapData : ScriptableObject
{
    public string mapId = "arena01";        // รหัสบนสาย ห้ามซ้ำ
    // Display — แปลได้ ชี้ไป String Table 'Content'
    public LocalizedString displayName;
    public LocalizedString description;

    /// <summary>ข้อความที่แปลแล้วตาม locale ปัจจุบัน — ว่างเมื่อยังไม่ได้ผูก entry
    /// ทุกที่ที่เอาไปแสดงต้องอ่าน property พวกนี้ ไม่ใช่ field ตรงๆ</summary>
    public string DisplayName => displayName.IsEmpty ? mapId : displayName.GetLocalizedString();
    public string Description  => description.IsEmpty ? "" : description.GetLocalizedString();

    public Sprite previewImage;
    public string sceneName = "SampleScene"; // ต้องอยู่ใน Build Settings

    [System.Serializable]
    public class TierContent
    {
        public DifficultyTier tier = DifficultyTier.Normal;
        public WaveConfig[] wavesByPhase;              // เรียงตามช่วงของรัน
        public BossEncounterConfig mainBossConfig;     // ปล่อย null = ใช้ของบน prefab
        [Tooltip("ปล่อยว่าง = ใช้ของบน prefab")]
        public BossEncounterConfig miniBossConfig;

        // จังหวะของรัน — โซนเควสต์กับมินิบอสออกนาทีไหนบ้าง
        //
        // อยู่ระดับ tier เพราะความยากคือ "จังหวะ" ไม่ใช่แค่ตัวเลข HP · แมพเดียวกัน
        // ระดับยากกว่าควรอัดมินิบอสถี่ขึ้นได้ โดยไม่ต้องทำแมพใหม่ทั้งใบ
        //
        // ปล่อยนัดหมายว่าง = ใช้ตารางของซีน · ตั้งแล้วจะ **แทนที่ทั้งชุด** ไม่ผสม
        [Tooltip("ตารางเวลาของ tier นี้ · นัดหมายว่าง = ใช้ของในซีน")]
        public TimelineSchedule schedule = new TimelineSchedule();

        // ศัตรูแรงขึ้นเร็วแค่ไหน — ปิดไว้ = ใช้ค่าในซีน
        //
        // อยู่ระดับ tier เพราะนี่คือสิ่งที่ทำให้ Hard ต่างจาก Normal จริงๆ
        // แมพเดียวกัน ตารางเวลาเดียวกัน แต่ศัตรูโตคนละอัตรา
        [Tooltip("สเกลศัตรูต่อ wave ของ tier นี้ · ปิด = ใช้ค่าในซีน")]
        public EnemyScaling enemyScaling = new EnemyScaling();
    }
    public TierContent[] tiers;

    /// <summary>สเกลศัตรูของ tier ที่ขอ — null เมื่อ tier นั้นไม่ได้เปิดสวิตช์ไว้</summary>
    public EnemyScaling GetEnemyScaling(DifficultyTier tier)
    {
        var t = GetTier(tier);
        return t != null && t.enemyScaling != null && t.enemyScaling.enabled
             ? t.enemyScaling : null;
    }

    /// <summary>ตารางเวลาของ tier ที่ขอ — null เมื่อแมพไม่ได้กำหนดอะไรไว้</summary>
    public TimelineSchedule GetSchedule(DifficultyTier tier)
    {
        var t = GetTier(tier);
        return t != null ? t.schedule : null;
    }

    /// <summary>
    /// คืน TierContent ของ tier ที่ขอ — ถ้าไม่มี ให้ fallback ไป Normal, ถ้า Normal ก็ไม่มีคืน null
    /// </summary>
    public TierContent GetTier(DifficultyTier tier)
    {
        if (tiers == null || tiers.Length == 0) return null;

        for (int i = 0; i < tiers.Length; i++)
        {
            if (tiers[i] != null && tiers[i].tier == tier)
                return tiers[i];
        }

        if (tier != DifficultyTier.Normal)
        {
            for (int i = 0; i < tiers.Length; i++)
            {
                if (tiers[i] != null && tiers[i].tier == DifficultyTier.Normal)
                    return tiers[i];
            }
        }

        return null;
    }
}
