using System.Collections.Generic;
using System.Linq;
using CloneSwarm.UI.P3R;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ควบคุม Panel แสดง upgrade cards เมื่อ Level Up
/// - แสดง countdown timer + แถบเติมเวลา (รับ event จาก SharedExperienceManager.OnTimerTick)
/// - แสดงสถานะรอเพื่อนเป็น dog-tag (LevelUpDogTagUI) + ข้อความ "X / Y ผู้เล่น"
/// - แสดงแถบ build (BuildStripUI) ให้ตัดสินใจได้โดยไม่ต้องจำว่าถืออะไรอยู่
/// - รับปุ่ม 1 / 2 / 3 เลือกการ์ด (Input System — ProjectSettings activeInputHandler = 1)
/// - ไม่ยุ่งกับ Time.timeScale — SharedExperienceManager จัดการแทน
/// </summary>
public class LevelUpUI : MonoBehaviour
{
    public static LevelUpUI Instance { get; private set; }

    [Header("Panel")]
    [Tooltip("Root GameObject ของ Panel ทั้งหมด — ตั้งค่า Inactive ไว้เริ่มต้น")]
    public GameObject panelRoot;

    [Header("Title")]
    [Tooltip("หัวเรื่องปกติ — ก้อน 'LEVEL UP!' ที่จัดไว้ในซีน")]
    public GameObject titleDefault;
    [Tooltip("หัวเรื่องตอนแจก augment — ก้อน 'AUGMENT' แยกอีกใบ\n\n" +
             "**สลับก้อน ไม่ใช่เขียนทับข้อความ** — หัวเรื่องเป็นตัวซ้อนหลายชั้นที่จัดทรง\n" +
             "มาในซีน เปลี่ยนคำอย่างเดียวแล้วทรงพัง (ความยาวคำไม่เท่ากัน ชั้นเงาเหลื่อมผิด)\n" +
             "แยกเป็นสองก้อนแล้วแต่ละก้อนจัดทรงของตัวเองได้อิสระ\n\n" +
             "ปล่อยว่าง = ใช้หัวเรื่องเดิมทุกกรณี (พฤติกรรมเดิม ไม่พัง)")]
    public GameObject titleAugment;

    [Header("Cards Section")]
    [Tooltip("GameObject ที่ครอบหัวเรื่อง + cardSlots ทั้งหมด — ซ่อนหลังเลือกแล้ว แต่ panelRoot ยังเปิดอยู่")]
    public GameObject cardsSection;
    [Tooltip("Parent ของ card ทั้งหมด (Panel) — ถ้าไม่ assign จะหาจาก cardsSection อัตโนมัติ")]
    public GameObject cardsContainer;
    [Tooltip("ลาก UpgradeCardUI ทั้ง 3 ใบมาใส่ที่นี่ — ใช้เฉพาะตอนไม่มี cardTemplate")]
    public List<UpgradeCardUI> cardSlots = new();

    [Tooltip("**prefab** ของการ์ด — ต่อช่องนี้แล้วจอจะสร้างการ์ดตามจำนวนที่ได้รับจริง\n\n" +
             "ต้องชี้ไฟล์ prefab ไม่ใช่ของในซีน · ของในซีนเป็น fileID ที่เปลี่ยนทุกครั้ง\n" +
             "ที่ย้ายจอ แล้วแม่แบบจะหายไปพร้อม panel เก่า\n\n" +
             "ปล่อยว่าง = พฤติกรรมเดิม (ใช้การ์ดที่มีอยู่แล้วใต้ cardsContainer)")]
    public UpgradeCardUI cardTemplate;

    [Tooltip("ระยะห่างระหว่างการ์ด (px) — ใช้ตอนสร้างจาก cardTemplate")]
    public float cardGap = 30f;

    // ── หัวเรื่อง "LEVEL UP!" ไม่ได้อยู่ที่นี่แล้ว ────────────────────────
    //
    // เคยมี `levelLabel` + `titleFormat` + `titleFormatNoLevel` ให้โค้ดเขียนหัวเรื่อง
    // ตอน `Show()` · ถอดออกแล้วเพราะ **หัวเรื่องเป็นงานจัดวาง ไม่ใช่ข้อมูล**
    //
    // มันเป็นข้อความคงที่ที่ถูกจัดทรงมาในซีน (ตัวซ้อนหลายชั้น · ขนาด · การตัดบรรทัด)
    // การเขียนทับตอนรันแปลว่าสิ่งที่เห็นใน Editor ไม่ใช่สิ่งที่เห็นในเกม — แก้ในซีน
    // แล้วหายทุกครั้งที่กด Play โดยไม่มีอะไรบอก
    //
    // เลเวลที่เพิ่งได้เป็น **ข้อมูล** จึงยังเขียนอยู่ — แต่ลง `levelValueLabel` ซึ่ง
    // เป็นป้ายของมันเอง ไม่ไปยุ่งกับทรงของหัวเรื่อง

    [Tooltip("ป้ายเลเวลใหม่แยกจากหัวเรื่อง — ปล่อยว่างได้ (ไม่ต่อ = ไม่บอกเลเวล)\n\n" +
             "หัวเรื่องเป็นข้อความคงที่ที่จัดทรงไว้ในซีน โค้ดไม่แตะ · เลเวลที่เพิ่งได้\n" +
             "เป็นข้อมูลที่เปลี่ยนทุกครั้ง จึงต้องมีป้ายของตัวเองแยกออกมา")]
    public TextMeshProUGUI levelValueLabel;
    [Tooltip("รูปแบบของป้ายเลเวลแยก · {0} = เลเวลใหม่")]
    public string levelValueFormat = "Lv {0}";

    [Header("Timer")]
    [Tooltip("**ทั้งก้อนของนาฬิกา** (ตัวเลข + แถบ + คำว่า SECONDS) — ซ่อนทั้งก้อนตอนไม่มีการนับ\n\n" +
             "นาฬิกาไม่ได้เดินทุกครั้งที่จอเปิด · เซิร์ฟเวอร์เริ่มนับต่อเมื่อ **มีคนเลือกไปแล้ว\n" +
             "หนึ่งคน และมีผู้เล่นมากกว่าหนึ่ง** — เล่นคนเดียวจึงไม่มีนาฬิกาเลย\n" +
             "ไม่ต่อช่องนี้ = ถอยไปซ่อนเฉพาะตัวเลขกับแถบ คำว่า SECONDS จะค้างอยู่")]
    public GameObject timerGroup;
    [Tooltip("แสดง countdown  เช่น '28'  — ซ่อนได้ถ้าไม่ต้องการ")]
    public TextMeshProUGUI timerLabel;
    [Tooltip("แถบเติมเวลา 180×6 ใต้ตัวเลข — ต้องเป็น Image type = Filled, Horizontal, Origin Left")]
    public Image timerFillBar;
    [Tooltip("สีปกติของตัวเลข timer")]
    public Color timerNormalColor  = Color.white;
    [Tooltip("สีเมื่อเหลือเวลาน้อย (< urgentThreshold วินาที)")]
    public Color timerUrgentColor  = new Color(1f, 0.3f, 0.2f);   // แดง
    [Tooltip("สีแถบเติมเวลาปกติ — Gold ตาม design token")]
    public Color timerFillColor    = new Color32(0xFF, 0xE6, 0x33, 0xFF);
    [Tooltip("วินาทีที่เปลี่ยนเป็นสีด่วน")]
    public float urgentThreshold   = 10f;

    [Header("Waiting Status")]
    [Tooltip("แสดงจำนวนคนที่เลือกแล้ว  เช่น 'รอผู้เล่น: 1 / 2' — ยังใช้ได้ ถ้าไม่ได้ใช้ dog-tag")]
    public TextMeshProUGUI waitingLabel;
    [Tooltip("แถบเล็กมุมจอตอนรอเพื่อน — ปล่อยว่างได้")]
    public GameObject waitingStrip;
    [Tooltip("dog-tag รายคน — ตัวที่แบบใหม่ใช้แทน waitingLabel")]
    public LevelUpDogTagUI dogTags;

    [Header("Build Strip")]
    [Tooltip("แถบ WEAPONS / PASSIVES มุมขวาล่าง — รีเฟรชตอนเปิดจอ")]
    public BuildStripUI buildStrip;

    [Header("Key Hint")]
    [Tooltip("คำใบ้ปุ่มข้างสถานะรอเพื่อน — ซ่อนพร้อมการ์ดหลังเลือกแล้ว")]
    public TextMeshProUGUI hintLabel;
    public string hintText = "1 · 2 · 3 หรือคลิกเพื่อเลือก";

    [Header("Card Glow")]
    [Tooltip("design handoff §Screen 1: วงเรืองแสงต้องเป็นสถานะ hover เท่านั้น\n" +
             "ส่วน 'แนะนำ' เหลือแค่ป้าย — ไม่งั้นแยกไม่ออกว่าใบไหนคือใบที่กำลังชี้อยู่\n" +
             "UpgradeCardUI เปิด glow ตาม isRecommended เอง ที่นี่จึงต้องปิดทับหลัง Populate")]
    public bool glowOnlyOnHover = true;

    // ── runtime ───────────────────────────────────────────────────────────
    /// <summary>การ์ดที่กำลังโชว์อยู่จริง เรียงตามที่ตาเห็น — ปุ่ม 1/2/3 อ้างอิงลิสต์นี้</summary>
    private readonly List<UpgradeCardUI> visibleSlots = new();
    private bool  cardsInteractable;

    /// <summary>
    /// เวลาทั้งหมดของรอบนี้ ใช้หารเพื่อหาสัดส่วนของแถบ
    /// <c>OnTimerTick</c> ส่งมาแค่เวลาที่เหลือ ไม่ได้ส่งเวลารวม —
    /// ค่าจริงอยู่ที่ <c>SharedExperienceManager.upgradePickSeconds</c> ซึ่งเป็น public field
    /// ทั้ง UpgradeTimerCoroutine และ OrbTimerCoroutine เริ่มนับจากค่านั้นทั้งคู่ จึงเชื่อถือได้
    /// ถ้าหา Instance ไม่เจอ (เช่นเปิดซีนต้นแบบเดี่ยวๆ) จะตกกลับไปจำ tick แรกที่ได้รับเป็น 100%
    /// </summary>
    private float timerTotal;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (panelRoot) panelRoot.SetActive(false);
    }

    void OnEnable()
    {
        SharedExperienceManager.OnTimerTick         += UpdateTimer;
        SharedExperienceManager.OnPickedCountChanged += UpdateWaiting;
        SharedExperienceManager.OnUpgradePhaseEnd   += Hide;
    }

    void OnDisable()
    {
        SharedExperienceManager.OnTimerTick         -= UpdateTimer;
        SharedExperienceManager.OnPickedCountChanged -= UpdateWaiting;
        SharedExperienceManager.OnUpgradePhaseEnd   -= Hide;
    }

    // ── Public API ────────────────────────────────────────────────────────
    /// <param name="cards">รายการ card ที่สุ่มได้ (UpgradeCardInfo)</param>
    /// <param name="onPicked">callback เมื่อ player เลือก card</param>
    /// <param name="level">Level ใหม่ที่ขึ้น (0 = แสดงแค่ LEVEL UP!)</param>
    public void Show(
        List<UpgradeCardInfo>              cards,
        System.Action<UpgradeCardInfo>     onPicked,
        int level = 0)
    {
        // 1. เปิด hierarchy ก่อนเสมอ
        if (panelRoot)      panelRoot.SetActive(true);
        if (cardsSection)   cardsSection.SetActive(true);
        if (cardsContainer) cardsContainer.SetActive(true);
        if (waitingStrip)   waitingStrip.SetActive(false);

        // 2. Header — **สลับก้อนหัวเรื่อง ไม่แตะข้อความ** ข้อความเป็นของซีน
        //
        // อ่านชนิดจาก **การ์ดที่กำลังจะโชว์จริง** ไม่ใช่จากพารามิเตอร์ที่ผู้เรียกส่งมา
        // หัวเรื่องจึงไม่มีทางไม่ตรงกับของที่อยู่บนจอ · ถ้ารับเป็น flag แยก
        // วันหนึ่งจะมีเส้นทางที่ลืมส่ง แล้วได้หัวเรื่อง "LEVEL UP!" คู่กับการ์ด augment
        //
        // ไม่ต่อ titleAugment = ไม่แตะอะไรเลย หัวเรื่องเดิมอยู่ครบ (ไม่ใช่จอไม่มีหัวเรื่อง)
        if (titleAugment != null)
        {
            bool allAugment = cards.Count > 0
                           && cards.TrueForAll(c => c != null && c.type == UpgradeCardType.Augment);

            titleAugment.SetActive(allAugment);
            if (titleDefault != null) titleDefault.SetActive(!allAugment);
        }

        //
        // เลเวลใหม่แยกป้าย — ซ่อนตอน Orb phase ที่ไม่มีเลเวลให้บอก
        // (ดีกว่าโชว์ "Lv 0" ซึ่งอ่านแล้วเข้าใจผิดว่าเลเวลตก)
        if (levelValueLabel)
        {
            bool hasLevel = level > 0;
            levelValueLabel.gameObject.SetActive(hasLevel);
            if (hasLevel) levelValueLabel.text = string.Format(levelValueFormat, level);
        }

        // 3. เตรียมช่องการ์ดให้พอดีกับจำนวนที่ได้รับ
        UpgradeCardUI[] slots = EnsureCardSlots(cards.Count);

        visibleSlots.Clear();
        for (int i = 0; i < slots.Length; i++)
        {
            if (i < cards.Count)
            {
                slots[i].gameObject.SetActive(true);
                slots[i].Populate(cards[i], onPicked);
                ApplyGlowPolicy(slots[i]);
                visibleSlots.Add(slots[i]);
            }
            else
            {
                slots[i].gameObject.SetActive(false);
            }
        }
        cardsInteractable = visibleSlots.Count > 0;

        // 4. Reset timer / waiting
        //
        // **ซ่อนนาฬิกาทั้งก้อนจนกว่าจะมี tick แรกจริง** — ไม่ใช่แค่ล้างตัวเลขเป็น ""
        //
        // เดิมล้างแต่ตัวเลข เหลือแถบทองเต็มกับคำว่า SECONDS ค้างอยู่ ซึ่ง **โกหก**:
        // แถบเต็มอ่านว่า "เวลายังเหลือทั้งหมด" ทั้งที่ความจริงคือไม่มีการนับเลย
        // และนั่นคือกรณีปกติ ไม่ใช่กรณีขอบ — เซิร์ฟเวอร์เริ่มนับต่อเมื่อมีคนเลือกไปแล้ว
        // หนึ่งคน **และ** มีผู้เล่นมากกว่าหนึ่ง เล่นคนเดียวจึงเห็นแถบเต็มค้างตลอดทุกครั้ง
        //
        // นาฬิกาโผล่เองใน UpdateTimer ตอน tick แรก — จังหวะที่มันเริ่มมีความหมายพอดี
        SetTimerVisible(false);
        ResetTimerTotal();
        if (timerLabel)  { timerLabel.color = timerNormalColor; timerLabel.text = ""; }
        if (timerFillBar) { timerFillBar.color = timerFillColor; timerFillBar.fillAmount = 1f; }
        if (waitingLabel)  waitingLabel.text = "";
        if (dogTags)       dogTags.ResetAll();

        // 5. คำใบ้ปุ่ม — จอนี้มีเวลาจำกัด ถ้าไม่บอกผู้เล่นจะไม่รู้ว่ากดเลขได้
        if (hintLabel)
        {
            hintLabel.text = hintText;
            hintLabel.gameObject.SetActive(true);
        }

        // 6. แถบ build — ดึงของที่ถืออยู่ ณ วินาทีนี้ (ก่อนการ์ดใบใหม่จะถูก apply)
        //    ล้มเหลวเงียบๆ ได้ ถ้ายังไม่มี player ในซีน แถบจะคงค่าเดิมไว้
        if (buildStrip) buildStrip.RefreshFromLocalPlayer();
    }

    /// <summary>
    /// คืนช่องการ์ดให้พอดีกับจำนวนที่ได้รับ
    ///
    /// ═══ ทำไมต้องสร้างจาก prefab ไม่ใช่ใช้ลูกที่มีอยู่ ═══
    ///
    /// ของเดิมวนตามลูกที่ builder สร้างไว้ **สามใบตายตัว** · `UpgradeManager.cardsPerLevel`
    /// เป็น 3 พอดีเลยไม่มีใครเห็นปัญหา แต่วันที่ใครตั้งเป็น 4–5 การ์ดส่วนเกินจะถูกทิ้ง
    /// เงียบๆ โดยไม่มี error — จอเดิมก่อน P3R มีห้าช่องและสร้างจาก prefab จริง
    ///
    /// ใช้ซ้ำของเดิมแทนการสร้างใหม่ทุกรอบ — การ์ดจำ y ตั้งต้นไว้สำหรับการยกใบแนะนำ
    /// และการทิ้ง/สร้างใหม่ทุกเลเวลคือขยะที่เก็บฟรีๆ กลางเกม
    ///
    /// `cardTemplate` ว่าง = ถอยไปใช้พฤติกรรมเดิม ซีนที่ยังไม่ได้ต่อจึงไม่พังทันที
    /// </summary>
    private UpgradeCardUI[] EnsureCardSlots(int count)
    {
        if (cardTemplate == null || cardsContainer == null)
            return cardsContainer
                ? cardsContainer.GetComponentsInChildren<UpgradeCardUI>(true)
                : cardSlots.ToArray();

        var parent = (RectTransform)cardsContainer.transform;

        // ปิดการ์ดที่ไม่ได้เกิดจากแม่แบบ (ตัวอย่างที่ builder วางไว้ให้ดูภาพต้นแบบ)
        // ปล่อยไว้จะได้การ์ดสองชุดซ้อนกัน ซึ่งเป็นอาการที่ไล่ต้นเหตุยาก
        if (!samplesCleared)
        {
            samplesCleared = true;
            foreach (var stray in parent.GetComponentsInChildren<UpgradeCardUI>(true))
                if (!spawned.Contains(stray)) stray.gameObject.SetActive(false);
        }

        var tmpl = (RectTransform)cardTemplate.transform;
        float w = tmpl.sizeDelta.x, h = tmpl.sizeDelta.y;

        while (spawned.Count < count)
        {
            var c = Instantiate(cardTemplate, parent);
            c.name = $"Card_{spawned.Count}";
            spawned.Add(c);
        }

        parent.sizeDelta = new Vector2(count * w + Mathf.Max(0, count - 1) * cardGap, h);

        for (int i = 0; i < spawned.Count; i++)
        {
            bool used = i < count;
            spawned[i].gameObject.SetActive(used);
            if (!used) continue;

            var rt = (RectTransform)spawned[i].transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(i * (w + cardGap), 0f);
        }

        return spawned.Take(count).ToArray();
    }

    private readonly List<UpgradeCardUI> spawned = new();
    private bool samplesCleared;

    /// <summary>
    /// เรียกหลัง player เลือก card แล้ว —
    /// ซ่อน cards + level label แต่คง panelRoot ไว้เพื่อแสดง timer / waiting count
    /// </summary>
    public void HideCards()
    {
        cardsInteractable = false;
        visibleSlots.Clear();

        if (cardsSection) cardsSection.SetActive(false);
        if (waitingStrip) waitingStrip.SetActive(true);
        if (hintLabel)    hintLabel.gameObject.SetActive(false);

        // เลือกไปแล้ว = ของในมือเปลี่ยนแล้ว · รีเฟรชเพื่อให้แถบตรงกับความจริงระหว่างรอเพื่อน
        if (buildStrip) buildStrip.RefreshFromLocalPlayer();
    }

    /// <summary>ปิด Panel ทั้งหมด — เรียกเมื่อทุกคนเลือกเสร็จ (OnUpgradePhaseEnd)</summary>
    public void Hide()
    {
        cardsInteractable = false;
        visibleSlots.Clear();

        if (waitingStrip) waitingStrip.SetActive(false);
        if (panelRoot)    panelRoot.SetActive(false);
    }

    // ── Keyboard 1 / 2 / 3 ────────────────────────────────────────────────
    void Update()
    {
        if (!cardsInteractable) return;
        if (cardsSection != null && !cardsSection.activeInHierarchy) return;

        // Keyboard.current เป็น null ได้จริงบนเครื่องที่ไม่มีคีย์บอร์ด (แพด/มือถือ) — ต้องเช็ค
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) PickByIndex(0);
        else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) PickByIndex(1);
        else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) PickByIndex(2);
    }

    /// <summary>
    /// เลือกการ์ดใบที่ index (0-based) เหมือนคลิกเอง
    /// ยิงผ่าน <c>selectButton.onClick</c> เพราะ callback ตัวจริงเป็น private ใน UpgradeCardUI
    /// — ทางนี้ทำให้คลิกกับกดปุ่มเดินเส้นทางเดียวกันเป๊ะ ไม่มีโอกาสแตกต่างกัน
    /// </summary>
    public void PickByIndex(int index)
    {
        if (!cardsInteractable) return;
        if (index < 0 || index >= visibleSlots.Count) return;

        var card = visibleSlots[index];
        if (card == null) return;

        if (card.selectButton != null)
        {
            if (!card.selectButton.interactable) return;
            card.selectButton.onClick.Invoke();
        }
        else
        {
            // ไม่มีปุ่ม → ยิง pointer click ให้ handler ตัวไหนก็ตามที่ผูกอยู่บนการ์ด
            ExecuteEvents.Execute(card.gameObject, new PointerEventData(EventSystem.current),
                                  ExecuteEvents.pointerClickHandler);
        }
    }

    // ── Card Glow ─────────────────────────────────────────────────────────
    /// <summary>
    /// บังคับให้วงเรืองแสงเป็นของ hover อย่างเดียวตามที่ design handoff สั่ง
    /// ป้าย "แนะนำ" (recommendedRibbon) ไม่ถูกแตะ — ยังทำหน้าที่บอกใบที่แนะนำเหมือนเดิม
    /// </summary>
    void ApplyGlowPolicy(UpgradeCardUI card)
    {
        if (!glowOnlyOnHover || card == null || card.recommendedGlowOutline == null) return;

        card.recommendedGlowOutline.SetActive(false);

        var hover = card.GetComponent<UpgradeCardGlowOnHover>();
        if (hover == null) hover = card.gameObject.AddComponent<UpgradeCardGlowOnHover>();
        hover.glow = card.recommendedGlowOutline;
    }

    // ── Timer ─────────────────────────────────────────────────────────────
    void ResetTimerTotal()
    {
        float configured = SharedExperienceManager.Instance != null
            ? SharedExperienceManager.Instance.upgradePickSeconds
            : 0f;
        // 0 = ยังไม่รู้ → UpdateTimer จะจำ tick แรกแทน
        timerTotal = configured > 0f ? configured : 0f;
    }

    /// <summary>
    /// ซ่อน/โชว์นาฬิกาทั้งก้อน
    ///
    /// ไม่ต่อ <c>timerGroup</c> ก็ยังดีกว่าเดิม — ซ่อนตัวเลขกับแถบให้เท่าที่รู้จัก
    /// แต่คำว่า SECONDS ที่เป็นลูกของก้อนเดียวกันจะค้างอยู่ เพราะโค้ดไม่รู้จักมัน
    /// **ไม่เดาจาก parent ของตัวเลข** — วันที่มีใครย้ายตัวเลขไปอยู่ใต้ของอื่น
    /// มันจะซ่อนของผิดชิ้นโดยไม่มีใครรู้ · ต่อช่องให้ชัดถูกกว่าเดาให้ฉลาด
    /// </summary>
    void SetTimerVisible(bool on)
    {
        if (timerGroup != null) { timerGroup.SetActive(on); return; }

        if (timerLabel   != null) timerLabel.gameObject.SetActive(on);
        if (timerFillBar != null) timerFillBar.gameObject.SetActive(on);
    }

    void UpdateTimer(float remaining)
    {
        // tick แรกคือสัญญาณเดียวที่บอกว่า "นาฬิกาเดินจริง" — ไม่มีธงอื่นให้ดู
        SetTimerVisible(true);

        if (timerTotal <= 0f) timerTotal = Mathf.Max(remaining, 0.0001f);

        if (timerLabel != null)
        {
            int secs = Mathf.CeilToInt(remaining);
            timerLabel.text  = secs > 0 ? secs.ToString() : "0";
            timerLabel.color = remaining <= urgentThreshold ? timerUrgentColor : timerNormalColor;
        }

        if (timerFillBar != null)
        {
            timerFillBar.fillAmount = Mathf.Clamp01(remaining / timerTotal);
            // แถบเปลี่ยนสีตามตัวเลขด้วย — ถ้าเหลือแถบทองบางๆ บนตัวเลขแดง จะอ่านเป็นคนละเรื่อง
            timerFillBar.color = remaining <= urgentThreshold ? timerUrgentColor : timerFillColor;
        }
    }

    // ── Waiting Count ─────────────────────────────────────────────────────
    void UpdateWaiting(int picked, int total)
    {
        if (dogTags != null) dogTags.SetCounts(picked, total);

        if (waitingLabel == null) return;
        waitingLabel.text = total > 1
            ? $"รอผู้เล่น: {picked} / {total}"
            : "";    // Solo ไม่ต้องแสดง
    }
}

/// <summary>
/// เปิด/ปิดวงเรืองแสงของการ์ดตามการชี้เมาส์
///
/// อยู่ไฟล์เดียวกับ LevelUpUI เพราะเป็นส่วนหนึ่งของนโยบาย "glow = hover เท่านั้น"
/// ที่ LevelUpUI เป็นคนบังคับ · ถูกใส่ให้อัตโนมัติตอน Populate ไม่ต้องแตะการ์ดใน Inspector
///
/// **ทางที่ถูกจริง** คือให้ <c>UpgradeCardUI</c> เลิกผูก recommendedGlowOutline กับ isRecommended
/// แล้วเปิด glow จากสถานะ hover/selected ของตัวเอง — แต่ไฟล์นั้นอยู่นอกขอบเขตงานนี้
/// ตัวนี้จึงเป็นการแก้จากข้างนอก ให้ผลตรงตามแบบไปก่อน
/// </summary>
[DisallowMultipleComponent]
public class UpgradeCardGlowOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [HideInInspector] public GameObject glow;

    public void OnPointerEnter(PointerEventData e) { if (glow) glow.SetActive(true); }
    public void OnPointerExit (PointerEventData e) { if (glow) glow.SetActive(false); }

    void OnDisable() { if (glow) glow.SetActive(false); }
}
