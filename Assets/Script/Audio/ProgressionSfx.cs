using UnityEngine;

/// <summary>
/// เสียงของจอเลือกการ์ด — เลเวลอัป และ augment · วางไว้ในซีนเกม (SampleScene) ตัวเดียว
///
/// ═══ สองจังหวะ · ได้ยินคนละแบบ ═══
///
///   จอเปิด  → **ทุกคนพร้อมกัน** · OnUpgradePhaseStart / OnOrbPhaseStart ยิงบนทุก client ผ่าน ClientRpc
///             (เลเวลเป็นของกลาง และ orb ที่ใครเก็บก็เปิดจอให้ทุกคน)
///   กดเลือก → **เฉพาะตัวเอง** · UpgradeManager.OnLocalCardPicked ยิงบนเครื่อง owner เท่านั้น
///             แต่ละคนเลือกการ์ดของตัวเอง จังหวะกดของเพื่อนไม่ใช่เสียงของเรา
///             (หมดเวลาแล้วระบบเลือกให้ก็นับว่าเลือก — ได้ของจริง จึงมีเสียงเหมือนกัน)
///
/// orb ปกติ (การ์ดรางวัลใบเดียว) มีเสียงจอเปิดของตัวเอง · เสียงกดเลือกใช้ cardPickClip เดียวกับเลเวลอัป
///
/// ไม่ใช่ NetworkBehaviour — event ทุกตัวถึงเครื่องที่ควรได้ยินอยู่แล้ว ไม่ต้องส่งอะไรเพิ่ม
///
/// ═══ ทำไมไม่ฝากไว้ที่ LevelUpUI ═══
///
/// panel ของจอการ์ดถูกปิด (SetActive false) ตอนไม่ได้โชว์ · subscribe ใน OnEnable ของมัน
/// จะไม่ทำงานในจังหวะที่ event ยิงพอดี — ตัวนี้อยู่บน object ที่เปิดตลอดรัน
/// </summary>
public class ProgressionSfx : MonoBehaviour
{
    [Header("จอเปิด — ทุกคนได้ยินพร้อมกัน")]
    public AudioClip levelUpOpenClip;
    [Range(0f, 1f)] public float levelUpOpenVolume = 0.8f;
    public AudioClip augmentOpenClip;
    [Range(0f, 1f)] public float augmentOpenVolume = 0.8f;
    [Tooltip("จอการ์ดรางวัลใบเดียวจาก orb ปกติ (ไม่ใช่ augment)")]
    public AudioClip orbRewardOpenClip;
    [Range(0f, 1f)] public float orbRewardOpenVolume = 0.8f;

    [Header("กดเลือก — ได้ยินแค่ตัวเอง")]
    [Tooltip("การ์ดเลเวลอัป และการ์ดรางวัลจาก orb ปกติ")]
    public AudioClip cardPickClip;
    [Range(0f, 1f)] public float cardPickVolume = 0.8f;
    public AudioClip augmentPickClip;
    [Range(0f, 1f)] public float augmentPickVolume = 0.9f;

    void OnEnable()
    {
        SharedExperienceManager.OnUpgradePhaseStart += OnLevelUpOpen;
        SharedExperienceManager.OnOrbPhaseStart     += OnOrbOpen;
        UpgradeManager.OnLocalCardPicked            += OnPicked;
    }

    void OnDisable()
    {
        SharedExperienceManager.OnUpgradePhaseStart -= OnLevelUpOpen;
        SharedExperienceManager.OnOrbPhaseStart     -= OnOrbOpen;
        UpgradeManager.OnLocalCardPicked            -= OnPicked;
    }

    void OnLevelUpOpen(int level) => Play(levelUpOpenClip, levelUpOpenVolume);

    void OnOrbOpen(OrbReward reward)
    {
        if (reward == OrbReward.Augment) Play(augmentOpenClip,   augmentOpenVolume);
        else                             Play(orbRewardOpenClip, orbRewardOpenVolume);
    }

    void OnPicked(UpgradeCardInfo card)
    {
        if (card == null) return;
        if (card.type == UpgradeCardType.Augment) Play(augmentPickClip, augmentPickVolume);
        else                                      Play(cardPickClip,    cardPickVolume);
    }

    static void Play(AudioClip clip, float volume) => SoundManager.Instance.PlaySfx2D(clip, volume);

#if UNITY_EDITOR
    // ใส่ component ครั้งแรก (หรือกด Reset) → เติมคลิปจากชุด HintsStarsLite ที่มีในโปรเจกต์ให้ก่อน
    // เลือกจากชื่อไฟล์ ยังไม่ได้ฟัง — เปลี่ยนได้ตามใจใน Inspector
    void Reset()
    {
        levelUpOpenClip = Load("Magic Score 5");
        augmentOpenClip = Load("Cosmic Reveal");
        orbRewardOpenClip = Load("Discovery 1");
        cardPickClip    = Load("Approved 1");
        augmentPickClip = Load("Unlocked Secret");
    }

    static AudioClip Load(string name) =>
        UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/HintsStarsLite/{name}.wav");
#endif
}
