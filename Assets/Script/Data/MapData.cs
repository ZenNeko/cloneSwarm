using UnityEngine;

[CreateAssetMenu(fileName = "Map_New", menuName = "LoL Swarm/Map Data")]
public class MapData : ScriptableObject
{
    public string mapId = "arena01";        // รหัสบนสาย ห้ามซ้ำ
    public string displayName = "Arena 01";
    [TextArea(1, 3)] public string description;
    public Sprite previewImage;
    public string sceneName = "SampleScene"; // ต้องอยู่ใน Build Settings

    [System.Serializable]
    public class TierContent
    {
        public DifficultyTier tier = DifficultyTier.Normal;
        public WaveConfig[] wavesByPhase;              // เรียงตามช่วงของรัน
        public BossEncounterConfig mainBossConfig;     // ปล่อย null = ใช้ของบน prefab
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
