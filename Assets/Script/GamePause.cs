using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>เหตุผลที่เกมหยุด — เรียงตาม priority จากต่ำไปสูง</summary>
public enum PauseReason
{
    PauseMenu   = 0,   // ผู้เล่นกด ESC
    PhaseSelect = 1,   // upgrade / orb phase กำลังเลือกการ์ด
    GameOver    = 2,   // win/lose panel ขึ้นแล้ว
}

/// <summary>สถานะเกมที่ derive จากเหตุผลที่ active อยู่ — ตัวที่ priority สูงสุดชนะ</summary>
public enum GameState { Playing, PauseMenu, PhaseSelect, GameOver }

/// <summary>
/// เจ้าของเดียวของ Time.timeScale — **ห้ามมีไฟล์อื่นเขียน Time.timeScale อีก**
///
/// ทำไมเป็น flag set ไม่ใช่ counter: การเรียกในโปรเจกต์นี้ไม่สมดุล
/// (EndUpgradePhase ยิงจาก 2 เส้นทาง · WinLoseUI ไม่เคย pop · PauseMenuUI คืนค่า 3 จุด)
/// Add/Remove เป็น idempotent — เรียกซ้ำหรือ Remove ตัวที่ไม่ได้ถืออยู่ก็ไม่พัง
/// </summary>
public static class GamePause
{
    static readonly HashSet<PauseReason> _active = new HashSet<PauseReason>();
    static float _resumeScale = 1f;

    /// <summary>ความเร็วตอนไม่ได้หยุด — DevTools slow-mo เขียนตัวนี้ ไม่ใช่ Time.timeScale</summary>
    public static float ResumeScale
    {
        get => _resumeScale;
        set { _resumeScale = Mathf.Max(0.01f, value); Apply(); }
    }

    public static bool IsPaused => _active.Count > 0;

    /// <summary>สถานะปัจจุบัน = เหตุผลที่ priority สูงสุด</summary>
    public static GameState Current
    {
        get
        {
            if (_active.Contains(PauseReason.GameOver))    return GameState.GameOver;
            if (_active.Contains(PauseReason.PhaseSelect)) return GameState.PhaseSelect;
            if (_active.Contains(PauseReason.PauseMenu))   return GameState.PauseMenu;
            return GameState.Playing;
        }
    }

    public static bool Has(PauseReason reason) => _active.Contains(reason);

    public static void Add(PauseReason reason)    { _active.Add(reason);    Apply(); }
    public static void Remove(PauseReason reason) { _active.Remove(reason); Apply(); }

    /// <summary>เคลียร์ทุกเหตุผล — เรียกอัตโนมัติตอนโหลดฉากใหม่</summary>
    public static void ResetAll() { _active.Clear(); _resumeScale = 1f; Apply(); }

    static void Apply() => Time.timeScale = _active.Count > 0 ? 0f : _resumeScale;

    // อัตโนมัติ — กันคนลืมเรียก ResetAll ตอนเปลี่ยนฉาก (เหมือน ServerSetMaxHealth ของ Round 1)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        _active.Clear();
        _resumeScale = 1f;
        Time.timeScale = 1f;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) => ResetAll();
}
