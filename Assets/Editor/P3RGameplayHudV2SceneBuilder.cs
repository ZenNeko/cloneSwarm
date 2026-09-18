using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using CloneSwarm.UI.P3R;
using static CloneSwarm.EditorTools.P3RBuilderKit;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// สร้าง HUD ตอนเล่น **เวอร์ชันใหม่ตามเลย์เอาต์ของแบบ** ลงซีน `Proto_GameplayHUD2`
    /// เมนู: Tools > Clone Swarm > Build P3R Gameplay HUD v2 Scene
    ///
    /// ═══ ต่างจาก P3RGameplayHudSceneBuilder ยังไง ═══
    ///
    /// ตัวนั้นเป็น **ภาพอ้างอิงล้วน** — ป้ายทุกอันเป็นข้อความตายตัว แถบทุกอันเป็น
    /// `anchorMax` ที่ขยับไม่ได้ตอนรัน เอาไปใช้ต่อไม่ได้เลย มีไว้เทียบตาอย่างเดียว
    ///
    /// ตัวนี้สร้างของจริงที่ **ต่อสายได้** — `GameHUD` อยู่บน panel และทุกช่องถูกเติมแล้ว
    /// แถบทุกอันเป็น `Image.type = Filled` ซึ่งเป็นเงื่อนไขที่ `fillAmount` ต้องการ
    /// (ของในซีนเก่าบางอันเป็น anchor-based ซึ่ง `GameHUD` ขับไม่ได้)
    ///
    /// ═══ "ไม่ต่อก็ได้ แต่ต้องเผื่อการต่อไว้" — แปลว่าอะไรตรงนี้ ═══
    ///
    /// จอนี้ **ยังไม่แตะ `SampleScene`** เลย · HUD ที่เล่นอยู่ตอนนี้ไม่ถูกกระทบ
    /// สิ่งที่เตรียมไว้ให้คือ:
    ///   1. `GameHUD` ต่อครบทุกช่องอยู่แล้วในซีนนี้ — ย้ายทั้ง panel ไปก็ใช้ได้ทันที
    ///   2. ที่ว่างของระบบอื่นถูกวางไว้เป็น container ชื่อชัด (`ObjectiveContainer` ·
    ///      `PartyContainer` · `BuffContainer` · `WeaponRow` · `StatRow` · `BossArea`)
    ///      ตัวคุมของระบบพวกนั้นอยู่บน `HUDCanvas` ไม่ใช่บน panel จึงแค่ชี้ช่องใหม่
    ///   3. ชื่อ panel ขึ้นต้น `P3R_` ตามกติกา `P3RScreenWirer.FindPanels` จะเห็นเอง
    ///
    /// **ยังไม่ลงมือย้าย** เพราะการย้ายคือทิ้ง HUD ตัวเดิมทั้งดุ้น ซึ่งต้องตัดสินใจแยก
    /// ดูรายการที่ต้องทำตอนย้ายท้ายไฟล์ (`MigrationNotes`)
    ///
    /// ═══ ข้อจำกัดโซน C ที่ยังต้องเคารพ แม้เป็นจอใหม่ ═══
    ///   แถบข้อมูลเป็นสี่เหลี่ยมตรงเสมอ — ความยาวคือค่าที่ต้องอ่าน เอียงแล้วอ่านผิด
    ///   ไม่มีลายทับตัวเลข · ไม่มีโมชั่น overshoot บนตัวเลขที่เปลี่ยนตลอด
    ///   เอียงได้เฉพาะสแลบ/ชิป/ปุ่ม ซึ่งไม่ได้ถือค่าต่อเนื่อง
    /// </summary>
    public static class P3RGameplayHudV2SceneBuilder
    {
        private const string ScenePath = "Assets/GameScenes/Proto_GameplayHUD2.unity";
        private const string PanelName = "P3R_HUD";

        private const float Pad = 40f;

        /// <summary>
        /// sprite ของแถบ — **จำเป็น ไม่ใช่ของตกแต่ง**
        ///
        /// `Image` ที่ไม่มี sprite จะตกไปใช้ `Graphic.OnPopulateMesh` ซึ่งวาดสี่เหลี่ยม
        /// เต็มกรอบและ **ข้าม `type` กับ `fillAmount` ทั้งหมด** · แถบที่ไม่มี sprite จึง
        /// ค้างเต็มตลอดเกมโดยไม่มี error สักบรรทัด — `GameHUD` สั่ง `fillAmount` ไปก็เงียบ
        ///
        /// ใช้ตัวเดียวกับที่แถบ HUD เดิมใช้อยู่แล้ว (ยืนยันจาก `HP_Fill`/`EXP_Fill`/
        /// `Charge_Fill` ในซีน) เพื่อไม่ให้หน้าตาเปลี่ยนและไม่ต้องเพิ่ม asset ใหม่เข้าโปรเจกต์
        /// </summary>
        private const string BarSprite =
            "Assets/_Heathen Engineering/Assets/UX/Icons/Flat Icons [Free]/Free Flat Solid Brush Icon.png";

        private static Sprite barSprite;

        private static readonly Color Slab    = new Color32(0x18, 0x24, 0xD8, 0xFF);
        private static readonly Color Chip    = new Color32(0x11, 0x18, 0x38, 0xFF);
        private static readonly Color BarBack = new Color32(0x1A, 0x1F, 0x33, 0xFF);
        private static readonly Color HpColor = new Color32(0x2C, 0xC5, 0xA0, 0xFF);
        private static readonly Color ExpColor= new Color32(0x40, 0x73, 0xD9, 0xFF);

        [MenuItem("Tools/Clone Swarm/Build P3R Gameplay HUD v2 Scene")]
        public static void Build()
        {
            if (!BeginScene(ScenePath, "GAMEPLAY HUD v2", out var scene)) return;

            // โหลด **หลัง** BeginScene เสมอ — โหลดก่อน NewScene แล้ว batchmode จะปลด asset
            // ทิ้งจนกลายเป็น fake-null ซีนสร้างจนจบ exit 0 แต่ไม่มีอะไรอยู่ในนั้น
            barSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BarSprite);
            if (barSprite == null)
            {
                Debug.LogError($"[P3R] หา sprite ของแถบไม่เจอที่ {BarSprite} — ยกเลิกทั้งรอบ\n" +
                               "ปล่อยผ่านจะได้แถบที่ขยับไม่ได้ทั้งจอโดยไม่มี error ตอนรัน");
                return;
            }

            BuildCamera(new Color32(0x14, 0x16, 0x1C, 0xFF));
            BuildEventSystem();
            var canvas = BuildCanvas("HUDCanvas");

            // พื้นจำลองสนามรบ — ให้ตัดสินได้ว่า HUD อ่านออกไหมบนพื้นที่ไม่ใช่ดำสนิท
            var field = NewImage("FakeField", (RectTransform)canvas.transform,
                                 new Color32(0x14, 0x16, 0x1C, 0xFF));
            Stretch(field.rectTransform);
            var crowd = NewImage("FakeCrowd", (RectTransform)canvas.transform,
                                 new Color(0.35f, 0.16f, 0.16f, 0.28f));
            Center(crowd.rectTransform, 1180f, 620f);

            var panel = NewRect(PanelName, canvas.transform);
            Stretch(panel);

            // ตัวคุมบน panel เป็น **ตัวขน** ไม่ใช่ตัวที่จะทำงานจริง — ในซีนเกม ตัวคุมทุกตัว
            // อยู่บน `HUDCanvas` มาตลอด · ตัวย้ายจะคัดค่าจากตรงนี้ไปใส่ตัวจริงแล้วลบพวกนี้ทิ้ง
            // วิธีนี้ทำให้ "สายที่ควรเป็น" อยู่ในไฟล์ซีนจริงๆ ตรวจได้ ไม่ใช่ตารางในหัวใคร
            var hud       = panel.gameObject.AddComponent<GameHUD>();
            var weapons   = panel.gameObject.AddComponent<WeaponStatHUD>();
            var boss      = panel.gameObject.AddComponent<BossHUDUI>();
            var objective = panel.gameObject.AddComponent<ObjectiveTrackerHUD>();

            BuildTopLeft(panel, hud);
            BuildTopCenter(panel, boss);
            BuildTopRight(panel, objective);
            BuildAnnouncement(panel, hud);
            BuildBottomLeft(panel, hud);
            BuildBottomCenter(panel, weapons);
            BuildBottomRight(panel, hud);
            BuildRespawnOverlay(panel, hud);

            ApplyHudColors(hud);

            EndScene(scene, ScenePath, MigrationNotes);
        }

        // ═══════════════════════════════════════════════════════════════════
        // บนซ้าย — เวลา + เวฟ
        // ═══════════════════════════════════════════════════════════════════
        private static void BuildTopLeft(RectTransform root, GameHUD hud)
        {
            var slab = NewImage("TimeSlab", root, Slab);
            TopLeft(slab.rectTransform, Pad, Pad, 208f, 62f);
            Shear(slab);                        // สแลบเอียงได้ ไม่ได้ถือค่าต่อเนื่อง

            var time = NewMono("TimerText", slab.rectTransform, "00:00", 34f, 0.04f,
                               TextAlignmentOptions.Center);
            Stretch(time.rectTransform);
            Guard(time);
            hud.timerLabel = time;

            // WAVE — **ยังไม่มีใครขับ** · GameTimeline มีเวฟอยู่แล้วแต่ไม่มีช่องใน GameHUD
            // ปล่อยเป็นป้ายนิ่งไว้ก่อน ดีกว่าโชว์เลขปลอมที่ดูเหมือนของจริง
            var wave = NewMono("WaveLabel", root, "WAVE —", 20f, 0.2f,
                               TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.8f));
            TopLeft(wave.rectTransform, Pad + 226f, Pad + 16f, 260f, 32f);
            Guard(wave);
        }

        // ═══════════════════════════════════════════════════════════════════
        // บนกลาง — แถบบอส + แถบร่าย
        //
        // `BossHUDUI` สร้างแถบบอสเองจาก `miniBossBarPrefab` ตอนรัน ที่นี่จึงเตรียม
        // แค่ root กับ container · ส่วนแถบร่ายเป็นของนิ่งที่มันขับตรงๆ ต้องสร้างครบ
        // ═══════════════════════════════════════════════════════════════════
        private static void BuildTopCenter(RectTransform root, BossHUDUI boss)
        {
            var panel = NewRect("BossPanel", root);
            CenterTop(panel, Pad, 760f, 96f);

            var list = NewRect("BossContainer", panel);
            Stretch(list);
            var vlg = list.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing              = 6f;
            vlg.childControlWidth    = true;
            vlg.childControlHeight   = false;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment       = TextAnchor.UpperCenter;

            boss.miniBossPanel     = panel.gameObject;
            boss.miniBossContainer = list;

            // แถบร่าย — อยู่ใต้แถบบอส · สี่เหลี่ยมตรง ไม่เฉือน (ความยาว = เวลาที่เหลือ)
            var cast = NewRect("CastBar", root);
            CenterTop(cast, Pad + 104f, 560f, 34f);

            var castName = NewMono("CastName", cast, "CASTING", 16f, 0.18f,
                                   TextAlignmentOptions.MidlineLeft);
            TopLeft(castName.rectTransform, 0f, 0f, 380f, 22f);
            Guard(castName);

            boss.castFill     = FilledBar(cast, "Cast", 0f, 0f, 560f, 10f,
                                          new Color32(0xF0, 0x86, 0x54, 0xFF));
            boss.castFill.fillAmount = 0f;      // เริ่มที่ว่าง ไม่ใช่เต็ม
            boss.castNameText = castName;
            boss.castBarRoot  = cast.gameObject;
        }

        // ═══════════════════════════════════════════════════════════════════
        // บนขวา — ที่ว่างของ ObjectiveTrackerHUD
        // ═══════════════════════════════════════════════════════════════════
        private static void BuildTopRight(RectTransform root, ObjectiveTrackerHUD objective)
        {
            var panel = NewRect("ObjectivePanel", root);
            TopRight(panel, Pad, Pad, 420f, 220f);

            var head = NewMono("Head", panel, "OBJECTIVES", 15f, 0.28f,
                               TextAlignmentOptions.MidlineRight, new Color(1f, 1f, 1f, 0.45f));
            TopRight(head.rectTransform, 0f, 0f, 420f, 24f);

            // แถวถูก Instantiate จาก ObjectiveTrackerEntry.prefab ตอนรัน — ที่นี่เตรียมแค่ที่วาง
            // VerticalLayoutGroup ตามที่ tooltip ของ entryContainer ระบุไว้
            var list = NewRect("ObjectiveContainer", panel);
            TopRight(list, 0f, 32f, 420f, 188f);
            var vlg = list.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing              = 6f;
            vlg.childControlWidth    = true;
            vlg.childControlHeight   = false;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment       = TextAnchor.UpperRight;

            objective.panelRoot      = panel.gameObject;
            objective.entryContainer = list;
        }

        // ═══════════════════════════════════════════════════════════════════
        private static void BuildAnnouncement(RectTransform root, GameHUD hud)
        {
            var label = NewMono("Announcement", root, "ANNOUNCEMENT", 26f, 0.16f,
                                TextAlignmentOptions.Center);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax =
                label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.rectTransform.sizeDelta        = new Vector2(900f, 64f);
            label.rectTransform.anchoredPosition = new Vector2(0f, 168f);
            Guard(label);
            hud.announcementLabel = label;
        }

        // ═══════════════════════════════════════════════════════════════════
        // ล่างซ้าย — บัฟ · เลเวล · HP · โล่ · EXP
        // ═══════════════════════════════════════════════════════════════════
        private static void BuildBottomLeft(RectTransform root, GameHUD hud)
        {
            // **ไม่มีแถวบัฟกับแถวปาร์ตี้ตามแบบ** — แบบวาดชิป [HST][BRN 3][SHD] กับแถว
            // PLAYER 1/2 ไว้ แต่ในเกมไม่มีระบบไหนป้อนได้เลย:
            //   `FloatingBuffUI` เป็น world-space ลอยเหนือหัวผู้เล่น ไม่ใช่ชิปมุมจอ
            //   `TempPartyHUD` bootstrap ตัวเองตอนรันด้วย DontDestroyOnLoad ไม่อยู่ในซีน
            //     และไฟล์มันเขียนไว้เองว่าเป็นของชั่วคราวที่ตั้งใจให้รื้อทิ้ง
            // วางกล่องเปล่าไว้รอคือบอกคนอ่านโค้ดว่ามีระบบที่ยังไม่มีอยู่จริง

            var lv = NewImage("LevelSlab", root, Slab);
            BottomLeft(lv.rectTransform, Pad, 96f, 108f, 52f);
            Shear(lv);
            var lvText = NewMono("Level_Text", lv.rectTransform, "Lv 1", 22f, 0.08f,
                                 TextAlignmentOptions.Center);
            Stretch(lvText.rectTransform);
            Guard(lvText);
            hud.levelText = lvText;

            var hpText = NewMono("HP_Text", root, "100 / 100", 30f, 0.04f,
                                 TextAlignmentOptions.MidlineLeft);
            BottomLeft(hpText.rectTransform, Pad + 126f, 108f, 300f, 40f);
            Guard(hpText);
            hud.hpText = hpText;

            // แถบข้อมูลทั้งสาม — สี่เหลี่ยมตรง ไม่มี Shear
            hud.hpFill  = FilledBar(root, "HP_Bar",  Pad + 126f, 62f, 420f, 14f, HpColor);
            hud.expFill = FilledBar(root, "EXP_Bar", Pad + 126f, 44f, 420f, 8f,  ExpColor);

            // โล่มีแถบของตัวเอง + root แยก เพราะ GameHUD ปิด/เปิดทั้งแถบตามว่ามีโล่ไหม
            var shield = FilledBar(root, "Shield_Bar", Pad + 126f, 80f, 420f, 8f,
                                   new Color32(0x40, 0x73, 0xD9, 0xFF));
            hud.shieldFill    = shield;
            hud.shieldBarRoot = shield.transform.parent.gameObject;

            var expLabel = NewMono("ExpLabel", root, "EXP", 14f, 0.2f,
                                   TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.5f));
            BottomLeft(expLabel.rectTransform, Pad + 556f, 40f, 180f, 22f);

            BuildCharacterIcon(root, hud);
            BuildChargeBar(root, hud);
            BuildTabHint(root);
        }

        // ═══════════════════════════════════════════════════════════════════
        // ช่องรูปตัวละคร — เหนือสแลบเลเวล ชิดขอบซ้ายเดียวกัน
        //
        // ทำไมอยู่ตรงนี้: แถวสถานะของผู้เล่นอยู่มุมนี้ทั้งชุดแล้ว (เลเวล · เลือด · EXP)
        // รูปตัวละครเป็นข้อมูลชุดเดียวกัน — "ฉันเป็นใคร สภาพยังไง" อ่านรวดเดียวจบ
        //
        // **ปิด `enabled` ไว้** — `GameHUD.ApplyCharacterIcon` เปิดให้เองตอนเจอ local player
        // `Image` ที่ไม่มี sprite วาดสี่เหลี่ยมทึบ ไม่ได้วาดเปล่า ปล่อยเปิดไว้ = กล่องขาวค้างทั้งเกม
        // ═══════════════════════════════════════════════════════════════════
        private static void BuildCharacterIcon(RectTransform root, GameHUD hud)
        {
            const float Size = 108f;

            var box = NewImage("CharacterIcon_Box", root, Chip);
            BottomLeft(box.rectTransform, Pad, 160f, Size, Size);
            AddBorder(box.rectTransform, "Border", 1f, new Color(1f, 1f, 1f, 0.18f));

            // **สีขาวล้วนเสมอ ห้ามย้อม** — สี Image คูณเข้ากับพิกเซลของ sprite
            var art = NewImage("CharacterIcon", box.rectTransform, Color.white);
            Inset(art.rectTransform, 6f, 6f, 6f, 6f);
            art.preserveAspect = true;
            art.enabled = false;
            hud.characterIcon = art;
        }

        // ═══════════════════════════════════════════════════════════════════
        // แถบ passive (CHARGE / KILLS / HITS) — ย้ายมาจากมุมล่างขวา
        //
        // มันเป็นทรัพยากรของ **ตัวผู้เล่น** เหมือนเลือดกับ EXP ไม่ใช่ของช่องสกิล
        // อยู่คนละมุมกับพวกเดียวกันทำให้ต้องกวาดตาข้ามจอเพื่ออ่านสภาพตัวเองครบชุด
        // ═══════════════════════════════════════════════════════════════════
        private static void BuildChargeBar(RectTransform root, GameHUD hud)
        {
            var charge = NewRect("ChargeBar_Panel", root);
            BottomLeft(charge, Pad + 126f, 160f, 340f, 30f);

            var label = NewMono("ChargeLabel", charge, "CHARGE", 14f, 0.24f,
                                TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.5f));
            TopLeft(label.rectTransform, 0f, 4f, 110f, 22f);

            hud.chargeBarFill = FilledBar(charge, "Charge_Bar", 116f, 11f, 160f, 10f,
                                          new Color32(0xFF, 0xE6, 0x33, 0xFF));

            var pct = NewMono("ChargeText", charge, "0", 15f, 0.06f,
                              TextAlignmentOptions.MidlineRight);
            TopRight(pct.rectTransform, 0f, 4f, 60f, 22f);
            Guard(pct);
            hud.chargeBarText = pct;

            // **ช่องที่ซีนเก่าปล่อยว่างไว้** จน GameHUD ซ่อนแถบไม่ได้เลย — ต่อให้ตั้งแต่ต้น
            hud.chargeBarRoot = charge.gameObject;
        }

        /// <summary>
        /// ป้ายบอกปุ่ม — ใต้แถบ passive ชิดแนวเดียวกับแถบข้อมูล
        ///
        /// **ยังไม่มีจอ stats จริงอยู่หลังปุ่มนี้** · ป้ายบอกสิ่งที่ยังไม่มี แต่ของเดิม
        /// ก็บอกอยู่แล้วและไม่ใช่เรื่องที่งานนี้แก้ — ย้ายที่อย่างเดียว ไม่เพิ่มคำโกหกใหม่
        /// </summary>
        private static void BuildTabHint(RectTransform root)
        {
            var tab = NewMono("TabHint", root, "TAB  STATS", 14f, 0.22f,
                              TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.35f));
            BottomLeft(tab.rectTransform, Pad + 126f, 196f, 240f, 22f);
        }

        // ═══════════════════════════════════════════════════════════════════
        // ล่างกลาง — ช่องอาวุธ 6 + ช่องสเตตัส 6
        //
        // `WeaponStatHUD` ใช้ **อาเรย์ขนาดตายตัว** ที่ชี้ `bg`/`icon`/`nameTxt`/`levelTxt`
        // ทีละช่อง ไม่ใช่ container ที่มันสร้างของเอง · ช่องทั้งหมดจึงต้องมีอยู่จริงในซีน
        // ตั้งแต่แรก และต้องต่อเข้าอาเรย์ให้ครบ ไม่งั้นช่องที่ขาดจะเงียบไปเฉยๆ
        // ═══════════════════════════════════════════════════════════════════
        private static void BuildBottomCenter(RectTransform root, WeaponStatHUD weapons)
        {
            // ระยะสองแถวต้องเผื่อป้ายกำกับที่ลอยอยู่เหนือแต่ละแถว — ชิดกว่านี้ป้ายของแถวล่าง
            // จะไปทับช่องของแถวบน (เห็นชัดตอนช่องถูกเฉือน มุมมันยื่นออกมา)
            weapons.weaponSlots = SlotStrip(root, "WeaponRow", "WEAPONS", 132f,
                                            PlayerWeaponManager.MaxWeaponSlots);
            weapons.statSlots   = SlotStrip(root, "StatRow",   "PASSIVES", 56f,
                                            PlayerStatManager.MaxStatSlots);
        }

        private static WeaponStatHUD.SlotUI[] SlotStrip(RectTransform root, string name,
                                                        string label, float y, int count)
        {
            const float SlotW = 62f, SlotH = 48f, Gap = 10f;
            float width = count * SlotW + (count - 1) * Gap;

            var strip = NewRect(name, root);
            strip.anchorMin = strip.anchorMax = new Vector2(0.5f, 0f);
            strip.pivot     = new Vector2(0.5f, 0f);
            strip.sizeDelta = new Vector2(width, SlotH);
            strip.anchoredPosition = new Vector2(0f, y);

            var cap = NewMono("Label", strip, label, 12f, 0.24f,
                              TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.35f));
            TopLeft(cap.rectTransform, 0f, -16f, width, 16f);

            var slots = new WeaponStatHUD.SlotUI[count];
            for (int i = 0; i < count; i++)
            {
                var slot = NewRect($"Slot_{i}", strip);
                slot.anchorMin = slot.anchorMax = new Vector2(0f, 0.5f);
                slot.pivot     = new Vector2(0f, 0.5f);
                slot.sizeDelta = new Vector2(SlotW, SlotH);
                slot.anchoredPosition = new Vector2(i * (SlotW + Gap), 0f);

                var bg = NewImage("Bg", slot, Chip);
                Stretch(bg.rectTransform);
                Shear(bg);

                // ไอคอน — ปิดไว้จนกว่าจะมีของจริง · WeaponStatHUD ตั้ง sprite กับ color เอง
                var icon = NewImage("Icon", slot, Color.white);
                Inset(icon.rectTransform, 10f, 8f, 10f, 16f);
                icon.enabled = false;

                var nameTxt = NewMono("Abbrev", slot, "", 12f, 0.1f,
                                      TextAlignmentOptions.Center);
                BottomLeft(nameTxt.rectTransform, 0f, 2f, SlotW, 16f);
                Guard(nameTxt);

                var lvTxt = NewMono("Level", slot, "", 11f, 0.06f,
                                    TextAlignmentOptions.TopRight, new Color(1f, 1f, 1f, 0.7f));
                TopRight(lvTxt.rectTransform, 4f, 3f, 34f, 14f);
                Guard(lvTxt);

                slots[i] = new WeaponStatHUD.SlotUI
                {
                    bg = bg, icon = icon, nameTxt = nameTxt, levelTxt = lvTxt,
                };
            }
            return slots;
        }

        // ═══════════════════════════════════════════════════════════════════
        // ล่างขวา — ช่องสกิลอย่างเดียว
        //
        // แถบ passive กับป้าย TAB ย้ายไปอยู่กับ HP/EXP มุมล่างซ้ายแล้ว
        // (ดู BuildChargeBar / BuildTabHint) — มุมนี้เหลือแต่ของที่ "กดได้"
        // ═══════════════════════════════════════════════════════════════════
        private static void BuildBottomRight(RectTransform root, GameHUD hud)
        {
            hud.qSlot = AbilitySlot(root, "Q_Slot", Pad + 118f);
            hud.eSlot = AbilitySlot(root, "E_Slot", Pad);
        }

        /// <summary>
        /// ช่องสกิลหนึ่งช่อง — ครบทุกชิ้นที่ <see cref="GameHUD.AbilitySlotUI"/> ต้องการ
        ///
        /// ป้ายปุ่ม **ไม่ฝังค่า** — `GameHUD.ApplySlotIdentity` เขียนทับด้วย
        /// `IHUDAbility.HUDKeyLabel` ตอนรัน ซึ่งตอนนี้คืน LMB/RMB ไม่ใช่ Q/E
        /// ค่าที่ใส่ไว้ตรงนี้เป็นแค่ placeholder ให้เห็นตำแหน่งตอนดูภาพ
        /// </summary>
        private static GameHUD.AbilitySlotUI AbilitySlot(RectTransform root, string name, float xFromRight)
        {
            var slot = NewRect(name, root);
            BottomRight(slot, xFromRight, 102f, 100f, 100f);

            var bg = NewImage("Bg", slot, Chip);
            Stretch(bg.rectTransform);
            Shear(bg);

            // ไอคอนสกิล — **ห้ามย้อม** สีคูณเข้าพิกเซลของ sprite
            // GameHUD ลงสีสถานะที่ slotBg แทน ซึ่งเป็นเหตุผลที่ช่องนี้ต้องมีสองชั้น
            var icon = NewImage("Icon", slot, Color.white);
            Inset(icon.rectTransform, 18f, 18f, 18f, 18f);
            // ปิดไว้จนกว่าจะมี sprite จริง — `GameHUD.ApplySlotIdentity` เปิดให้เองตอนหา
            // ability เจอ · Image ที่ไม่มี sprite วาดเป็นสี่เหลี่ยมทึบเต็มช่อง
            icon.enabled = false;

            var glow = NewImage("ActiveGlow", slot, new Color32(0xF0, 0x86, 0x54, 0xD9));
            Stretch(glow.rectTransform);
            Shear(glow);
            glow.gameObject.SetActive(false);

            // แผ่นคูลดาวน์ — Filled แนวตั้งให้ไหลลงเหมือนเกมแนวนี้ทั่วไป
            var cd = NewImage("CooldownFill", slot, new Color(6 / 255f, 8 / 255f, 18 / 255f, 0.72f));
            Stretch(cd.rectTransform);
            cd.sprite      = barSprite;       // เหตุผลเดียวกับแถบ — ไม่มี sprite = fill ไม่ขยับ
            cd.type        = Image.Type.Filled;
            cd.fillMethod  = Image.FillMethod.Vertical;
            cd.fillOrigin  = (int)Image.OriginVertical.Top;
            cd.fillAmount  = 0f;

            var key = NewMono("KeyHint", slot, "—", 15f, 0.14f,
                              TextAlignmentOptions.TopLeft, new Color(1f, 1f, 1f, 0.6f));
            TopLeft(key.rectTransform, 12f, 8f, 80f, 22f);
            Guard(key);

            var cdText = NewMono("CDText", slot, "", 30f, 0.04f, TextAlignmentOptions.Center);
            Stretch(cdText.rectTransform);
            Guard(cdText);

            return new GameHUD.AbilitySlotUI
            {
                root         = slot.gameObject,
                slotBg       = bg,
                iconImage    = icon,
                cooldownFill = cd,
                cooldownText = cdText,
                keyHintText  = key,
                activeGlow   = glow.gameObject,
            };
        }

        // ═══════════════════════════════════════════════════════════════════
        private static void BuildRespawnOverlay(RectTransform root, GameHUD hud)
        {
            var overlay = NewImage("RespawnOverlay", root,
                                   new Color(6 / 255f, 8 / 255f, 18 / 255f, 0.72f));
            Stretch(overlay.rectTransform);

            var text = NewMono("RespawnText", overlay.rectTransform, "RESPAWN IN: 10", 40f, 0.1f,
                               TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            Guard(text);

            overlay.gameObject.SetActive(false);

            hud.respawnPanel         = overlay.gameObject;
            hud.respawnCountdownText = text;
        }

        // ═══════════════════════════════════════════════════════════════════
        private static void ApplyHudColors(GameHUD hud)
        {
            // ชุดเดียวกับที่ P3RGameplayHudRestyler ลงให้ HUD ตัวเดิม — จะได้ไม่แตกกันสองชุด
            hud.abilityReadyColor    = Slab;
            hud.abilityCooldownColor = Chip;
            hud.abilityActiveColor   = new Color32(0xFF, 0xE6, 0x33, 0xFF);
            hud.mainBossAnnouncementColor = new Color32(0xD8, 0x20, 0x20, 0xFF);
            hud.announcementDuration = 2.5f;
        }

        /// <summary>
        /// แถบข้อมูล — ราง + ตัวเติมที่เป็น <c>Image.Type.Filled</c>
        ///
        /// **ต้องเป็น Filled เท่านั้น** — `GameHUD` ขับด้วย `fillAmount` ซึ่งไม่มีผลกับ
        /// Image แบบ Simple · ซีนเก่ามีแถบที่ทำด้วย anchorMax ซึ่งขยับตอนรันไม่ได้เลย
        /// และไม่มีอะไรฟ้อง แถบก็แค่ค้างอยู่ที่เดิม
        ///
        /// ไม่มี <c>Shear</c> เด็ดขาด — ความยาวแถบคือค่าที่ผู้เล่นต้องอ่าน
        /// </summary>
        private static Image FilledBar(RectTransform parent, string name, float x, float y,
                                       float w, float h, Color color)
        {
            var track = NewImage($"{name}Track", parent, BarBack);
            BottomLeft(track.rectTransform, x, y, w, h);

            track.sprite = barSprite;

            var fill = NewImage("Fill", track.rectTransform, color);
            Stretch(fill.rectTransform);
            fill.sprite     = barSprite;      // ไม่มี sprite = fillAmount ไม่มีผล (ดู BarSprite)
            fill.type       = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            return fill;
        }

        /// <summary>
        /// ป้ายที่สคริปต์เขียนทับตอนรันต้องมีตัวเฝ้าระยะถ่าง/ขนาดของไทย
        ///
        /// `MonoStyle` ตัดสินใจตอนสร้างซีน ซึ่งตอนนั้นข้อความยังเป็น placeholder ภาษาอังกฤษ
        /// พอตอนรันมีคนเขียนข้อความไทยลงไป ค่าจากตอนสร้างยังค้างอยู่แล้วสระลอย
        /// `P3RThaiRiskAudit` สแกนเฉพาะซีนที่ระบุไว้ จอใหม่จึงต้องแปะเองตั้งแต่แรก
        /// </summary>
        private static void Guard(TextMeshProUGUI t)
        {
            if (t == null || t.GetComponent<P3RThaiTracking>() != null) return;
            var g = t.gameObject.AddComponent<P3RThaiTracking>();
            g.latinSpacing  = t.characterSpacing;
            g.latinFontSize = t.fontSize;
        }

        // ═══════════════════════════════════════════════════════════════════
        private const string MigrationNotes =
            "**จอนี้ยังไม่ได้ต่อเข้าเกม และยังไม่แตะ SampleScene เลย**\n" +
            "HUD ที่เล่นอยู่ตอนนี้ไม่ถูกกระทบ · ตรงนี้คือของที่เตรียมไว้ให้ตอนจะย้ายจริง\n\n" +
            "ต่อครบแล้วในซีนนี้ — GameHUD ทุกช่อง:\n" +
            "  timerLabel · hpText · hpFill · shieldFill · shieldBarRoot · expFill · levelText\n" +
            "  announcementLabel · respawnPanel · respawnCountdownText\n" +
            "  chargeBarRoot (ช่องที่ซีนเก่าปล่อยว่างจนซ่อนแถบไม่ได้) · chargeBarFill · chargeBarText\n" +
            "  qSlot / eSlot ครบทั้งเจ็ดชิ้นต่อช่อง\n\n" +
            "ยังต้องทำตอนย้าย — ตัวคุมพวกนี้อยู่บน HUDCanvas ไม่ได้อยู่บน panel:\n" +
            "  ObjectiveTrackerHUD → panelRoot = ObjectivePanel · entryContainer = ObjectiveContainer\n" +
            "  TempPartyHUD        → container = PartyContainer\n" +
            "  FloatingBuffUI      → container = BuffContainer\n" +
            "  WeaponStatHUD       → แถวอาวุธ = WeaponRowCon · แถวสเตตัส = StatRowCon\n" +
            "  BossHUDUI           → วางแถบใน BossArea\n\n" +
            "ที่ยังไม่มีที่มาให้ดึง (จงใจไม่ใส่เลขปลอม):\n" +
            "  WaveLabel — GameTimeline มีเวฟ แต่ GameHUD ไม่มีช่องรับ ต้องเพิ่มก่อน\n" +
            "  TabHint   — ยังไม่มีจอ STATS ให้เปิด\n\n" +
            "เลย์เอาต์ต่างจาก HUD ตัวเดิมทั้งจอ — การย้ายคือเปลี่ยนที่ที่ตาผู้เล่นเคยชิน\n" +
            "ต้องผ่าน 5-second test ก่อน merge: แคปตอนศัตรูเต็มจอ โชว์ 5 วิ ปิด\n" +
            "แล้วถามว่า HP เหลือกี่ % สกิลไหนพร้อม · ตกแม้แต่นิดแปลว่ายังไม่พร้อมย้าย";
    }
}
