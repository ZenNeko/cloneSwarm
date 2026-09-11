using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// แผงติดตาม Zone Objective — หนึ่งแถวต่อ objective หนึ่งอัน รองรับหลายอันพร้อมกัน
///
/// เดิมมีแต่ announcement บรรทัดเดียวที่เด้งขึ้นแล้วหายไปใน ~3 วินาที ผู้เล่นที่พลาดตอนนั้น
/// (หรือเพิ่งตายอยู่) จะไม่มีทางรู้เลยว่ากำลังทำ quest อะไรอยู่ และไม่มีที่ไหนบอกเวลาที่เหลือ
///
/// โครงเดียวกับ BossHUDUI ที่จัดการหลอด MiniBoss หลายตัว — container + row prefab +
/// Dictionary ผูก object กับแถว · ObjectiveManager.SpawnObjectives() เป็นพหูพจน์อยู่แล้ว
/// และ ZoneObjectiveLocation เป็น array 3 ช่อง ระบบจึงรองรับหลาย objective มาแต่ต้น
///
/// ── Setup ─────────────────────────────────────────────────────────────────
///   1. สร้าง panelRoot ใน Canvas ของซีน ข้างในมี container ที่ใส่ VerticalLayoutGroup
///      (+ ContentSizeFitter → Vertical Fit = Preferred Size ถ้าอยากให้แผงหดตามจำนวนแถว)
///   2. ทำ prefab ของแถวจาก ObjectiveTrackerEntry แล้วลากมาใส่ entryPrefab
///   3. แปะ component นี้ไว้ที่ไหนก็ได้ในซีน แล้วต่อ 3 ช่องให้ครบ
///   4. ปิด Raycast Target ของทุกชิ้นบนแผง — สกิลผูกกับคลิกซ้าย/ขวาแล้ว (ดู AbilityInput)
///
/// วาง 1 ตัวต่อซีนเกม เกิดและตายไปกับซีนเหมือน BossHUDUI / QuestCarryHUD / StatusHUDUI
/// </summary>
public class ObjectiveTrackerHUD : MonoBehaviour
{
    [Header("── Panel ───────────────────────────────────────")]
    [Tooltip("Root ของแผงทั้งอัน — ซ่อนอัตโนมัติเมื่อไม่มี objective")]
    public GameObject panelRoot;
    [Tooltip("Container ที่แถวจะ Instantiate เข้าไป — ควรมี VerticalLayoutGroup\n" +
             "ว่าง = ใช้ transform ของ component นี้เอง")]
    public Transform  entryContainer;
    [Tooltip("Prefab ที่มี ObjectiveTrackerEntry.cs")]
    public ObjectiveTrackerEntry entryPrefab;

    [Header("── ขีดจำกัด ────────────────────────────────────")]
    [Tooltip("แสดงได้มากสุดกี่แถวพร้อมกัน — เกินกว่านี้ objective ใหม่จะไม่มีแถว\n" +
             "(ยังเล่นได้ปกติ แค่ไม่มีที่บนจอ) · 0 = ไม่จำกัด")]
    [Min(0)] public int maxVisibleEntries = 3;

    // ── State ─────────────────────────────────────────────────────────────
    readonly Dictionary<ZoneObjective, ObjectiveTrackerEntry> _entries = new();

    /// <summary>แถวที่จบแล้วกำลังค้างจอ — ไม่มี key ใน _entries แล้ว (objective ตายไปแล้ว)
    /// แต่ยังต้องถือไว้จนครบ hideDelay ถึงจะลบทิ้ง</summary>
    readonly List<ObjectiveTrackerEntry> _fading = new();

    // ══════════════════════════════════════════════════════════════════════
    void Awake()
    {
        WarnIfMisconfigured();
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // static event → subscribe ใน OnEnable / unsubscribe ใน OnDisable (CLAUDE.md ข้อ 5)
    void OnEnable()
    {
        ZoneObjective.OnObjectiveSpawned   += OnSpawned;
        ZoneObjective.OnObjectiveCompleted += OnCompleted;
        ZoneObjective.OnObjectiveExpired   += OnExpired;
        WinLoseUI.OnAnyResultTriggered     += OnGameResult;
    }

    void OnDisable()
    {
        ZoneObjective.OnObjectiveSpawned   -= OnSpawned;
        ZoneObjective.OnObjectiveCompleted -= OnCompleted;
        ZoneObjective.OnObjectiveExpired   -= OnExpired;
        WinLoseUI.OnAnyResultTriggered     -= OnGameResult;
    }

    void WarnIfMisconfigured()
    {
        if (entryPrefab == null)
            Debug.LogWarning("[ObjectiveTracker] ยังไม่ได้ต่อ entryPrefab — จะไม่มีแถวไหนถูกสร้างเลย");

        if (panelRoot == null)
        {
            Debug.LogWarning("[ObjectiveTracker] ยังไม่ได้ต่อ panelRoot — แผงจะไม่ถูกซ่อน/แสดงให้");
            return;
        }

        // กราฟิกที่ยังเปิด raycastTarget จะกินคลิกซ้าย/ขวาที่ตอนนี้เป็นปุ่มสกิล
        // เตือนแทนที่จะแอบปิดให้ เพราะแผงที่จัดเองอาจตั้งใจให้กดได้
        foreach (var g in panelRoot.GetComponentsInChildren<Graphic>(true))
        {
            if (g == null || !g.raycastTarget) continue;
            Debug.LogWarning($"[ObjectiveTracker] '{g.name}' ยังเปิด Raycast Target อยู่ — " +
                             "สกิลผูกกับคลิกซ้าย/ขวาแล้ว แผงนี้จะกินคลิกจนสกิลกดไม่ติดตรงมุมนั้น " +
                             "ปิด Raycast Target ใน Inspector ของทุกชิ้นบนแผง");
        }
    }

    // ── Events ────────────────────────────────────────────────────────────
    void OnSpawned(ZoneObjective z)
    {
        if (z == null || entryPrefab == null) return;
        if (_entries.ContainsKey(z)) return;

        // เกมจบไปแล้ว objective ที่ตามมาทีหลังไม่ต้องเด้งทับจอผลลัพธ์
        if (WinLoseUI.IsShowing) return;

        if (maxVisibleEntries > 0 && _entries.Count >= maxVisibleEntries)
        {
            Debug.LogWarning($"[ObjectiveTracker] มี objective พร้อมกันเกิน {maxVisibleEntries} อัน — " +
                             "อันนี้จะไม่มีแถวบนจอ (ยังเล่นได้ปกติ) ปรับ maxVisibleEntries ถ้าต้องการ");
            return;
        }

        var parent = entryContainer != null ? entryContainer : transform;
        var entry  = Instantiate(entryPrefab, parent);
        entry.Initialize(z);
        _entries[z] = entry;

        if (panelRoot != null) panelRoot.SetActive(true);
    }

    void OnCompleted(ZoneObjective z) => Finish(z, completed: true);
    void OnExpired  (ZoneObjective z) => Finish(z, completed: false);

    /// <summary>objective จบแล้ว — ย้ายแถวไปกอง fading ให้ค้างจอครบ hideDelay ก่อนลบ
    /// ปลดออกจาก _entries ทันทีเพื่อคืนโควตาให้ objective อันถัดไป</summary>
    void Finish(ZoneObjective z, bool completed)
    {
        if (z == null || !_entries.TryGetValue(z, out var entry)) return;

        _entries.Remove(z);
        if (entry == null) return;

        entry.MarkFinished(completed);
        _fading.Add(entry);
    }

    /// <summary>เรียกจาก WinLoseUI.OnAnyResultTriggered — เกมจบแล้ว เก็บแผงทันที
    /// ไม่ให้ตัวนับเดินทับจอ VICTORY/DEFEAT (แพตเทิร์นเดียวกับ GameHUD.HideRespawnOverlay)</summary>
    void OnGameResult() => ClearAll();

    // ── Per-frame ─────────────────────────────────────────────────────────
    void Update()
    {
        // แถวที่จบแล้วและครบเวลาค้างจอ — ลบทิ้ง
        for (int i = _fading.Count - 1; i >= 0; i--)
        {
            var e = _fading[i];
            if (e == null)            { _fading.RemoveAt(i); continue; }
            if (!e.ReadyToRemove)     continue;

            _fading.RemoveAt(i);
            Destroy(e.gameObject);
        }

        // objective ที่หายไปโดยไม่ผ่าน event (host หลุด / ถูกทำลายตรงๆ)
        // ตัว entry ปิดตัวเองไปแล้วผ่าน MarkFinished แต่ dictionary ยังถือ key ค้างอยู่
        SweepDeadKeys();

        if (panelRoot != null && panelRoot.activeSelf && _entries.Count == 0 && _fading.Count == 0)
            panelRoot.SetActive(false);
    }

    void SweepDeadKeys()
    {
        if (_entries.Count == 0) return;

        List<ZoneObjective> dead = null;
        foreach (var kv in _entries)
        {
            if (kv.Key != null && kv.Key.isActiveAndEnabled) continue;
            (dead ??= new List<ZoneObjective>()).Add(kv.Key);
        }
        if (dead == null) return;

        foreach (var z in dead)
        {
            if (!_entries.TryGetValue(z, out var entry)) continue;
            _entries.Remove(z);
            if (entry == null) continue;

            // ปกติ entry.Update เห็นว่า objective ตายแล้วและ MarkFinished ตัวเองไปก่อนแล้ว
            // แต่ถ้าแถวถูกปิดอยู่ Update ของมันไม่รัน จะไม่มีใครตั้งนาฬิกาค้างจอให้
            // แล้วมันจะติดอยู่ใน _fading ตลอดกาล แผงก็ไม่ยอมซ่อน — มาร์กให้ตรงนี้เลย
            // (Objective != null = ยังไม่ถูกมาร์ก · MarkFinished เซ็ตมันเป็น null)
            if (entry.Objective != null) entry.MarkFinished(false);
            if (!_fading.Contains(entry)) _fading.Add(entry);
        }
    }

    void ClearAll()
    {
        foreach (var e in _entries.Values)
            if (e != null) Destroy(e.gameObject);
        _entries.Clear();

        foreach (var e in _fading)
            if (e != null) Destroy(e.gameObject);
        _fading.Clear();

        if (panelRoot != null) panelRoot.SetActive(false);
    }
}
