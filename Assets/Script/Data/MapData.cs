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
    }
    public TierContent[] tiers;

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
