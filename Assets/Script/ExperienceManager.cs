using UnityEngine;
using UnityEngine.Events;

public class ExperienceManager : MonoBehaviour
{
    public static ExperienceManager Instance { get; private set; }

    [Header("Level Settings")]
    public int currentLevel = 1;
    public int maxLevel = 30;
    public float baseExpToLevel = 100f;
    [Tooltip("EXP ที่ต้องการเพิ่มขึ้นเท่าไหร่ต่อเลเวล (multiplier)")]
    public float expGrowthRate = 1.25f;

    [Header("Multipliers")]
    [Tooltip("คูณ EXP ที่ได้รับ — เพิ่มได้จาก ExpBonus upgrade")]
    public float expMultiplier = 1f;

    [Header("Events")]
    [Tooltip("เรียกเมื่อ Level Up พร้อมส่งค่า level ใหม่")]
    public UnityEvent<int> onLevelUp;
    [Tooltip("เรียกทุกครั้งที่ EXP เปลี่ยน (currentExp, expToNextLevel)")]
    public UnityEvent<float, float> onExpChanged;

    private float currentExp;
    private float expToNextLevel;

    // ──────────────────────────────────────────────
    //  Init
    // ──────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        expToNextLevel = CalcExpToNextLevel(currentLevel);
        onExpChanged.Invoke(currentExp, expToNextLevel);
    }

    // ──────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────

    /// <summary>เพิ่ม EXP และ Level Up อัตโนมัติถ้าเต็ม</summary>
    public void AddExp(float amount)
    {
        if (currentLevel >= maxLevel) return;

        currentExp += amount * expMultiplier;

        // รองรับการ Level Up หลายครั้งพร้อมกัน
        while (currentExp >= expToNextLevel && currentLevel < maxLevel)
        {
            currentExp -= expToNextLevel;
            currentLevel++;
            expToNextLevel = CalcExpToNextLevel(currentLevel);

            Debug.Log($"[EXP] Level Up! → Level {currentLevel}");
            onLevelUp.Invoke(currentLevel);
        }

        onExpChanged.Invoke(currentExp, expToNextLevel);
    }

    public float GetExpPercent()   => (expToNextLevel > 0) ? currentExp / expToNextLevel : 1f;
    public float GetCurrentExp()   => currentExp;
    public float GetExpToNext()    => expToNextLevel;
    public int   GetCurrentLevel() => currentLevel;

    // ──────────────────────────────────────────────
    //  Formula
    // ──────────────────────────────────────────────

    /// <summary>EXP ที่ต้องการสำหรับ level นี้ (exponential scaling)</summary>
    float CalcExpToNextLevel(int level)
    {
        return Mathf.Floor(baseExpToLevel * Mathf.Pow(expGrowthRate, level - 1));
    }
}
