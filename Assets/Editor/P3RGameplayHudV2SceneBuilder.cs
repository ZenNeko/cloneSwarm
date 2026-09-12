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

        private static readonly Color Slab    = new Color32(0x18, 0x24, 0xD8, 0xFF);
        private static readonly Color Chip    = new Color32(0x11, 0x18, 0x38, 0xFF);
        private static readonly Color BarBack = new Color32(0x1A, 0x1F, 0x33, 0xFF);
        private static readonly Color HpColor = new Color32(0x2C, 0xC5, 0xA0, 0xFF);
        private static readonly Color ExpColor= new Color32(0x40, 0x73, 0xD9, 0xFF);

        [MenuItem("Tools/Clone Swarm/Build P3R Gameplay HUD v2 Scene")]
        public static void Build()
        {
            if (!BeginScene(ScenePath, "GAMEPLAY HUD v2", out var scene)) return;

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

            var hud = panel.gameObject.AddComponent<GameHUD>();

            BuildTopLeft(panel, hud);
            BuildTopCenter(panel);
            BuildTopRight(panel);
            BuildAnnouncement(panel, hud);
            BuildParty(panel);
            BuildBottomLeft(panel, hud);
            BuildBottomCenter(panel);
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
        // บนกลาง — ที่ว่างของ BossHUDUI (มันสร้างแถบเองตอนรัน)
        // ═══════════════════════════════════════════════════════════════════
        private static void BuildTopCenter(RectTransform root)
        {
            var boss = NewRect("BossArea", root);
            CenterTop(boss, Pad, 760f, 96f);

            var hint = NewMono("PlaceholderHint", boss, "BOSS AREA", 14f, 0.28f,
                               TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.18f));
            Stretch(hint.rectTransform);
        }

        // ═══════════════════════════════════════════════════════════════════
        // บนขวา — ที่ว่างของ ObjectiveTrackerHUD
        // ═══════════════════════════════════════════════════════════════════
        private static void BuildTopRight(RectTransform root)
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
        // กลางซ้าย — ที่ว่างของ TempPartyHUD
        // ═══════════════════════════════════════════════════════════════════
        private static void BuildParty(RectTransform root)
        {
            var list = NewRect("PartyContainer", root);
            list.anchorMin = list.anchorMax = new Vector2(0f, 0.5f);
            list.pivot     = new Vector2(0f, 0.5f);
            list.sizeDelta = new Vector2(380f, 120f);
            list.anchoredPosition = new Vector2(Pad, 20f);

            var vlg = list.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing              = 8f;
            vlg.childControlWidth    = true;
            vlg.childControlHeight   = false;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment       = TextAnchor.UpperLeft;
        }

        // ═══════════════════════════════════════════════════════════════════
        // ล่างซ้าย — บัฟ · เลเวล · HP · โล่ · EXP
        // ═══════════════════════════════════════════════════════════════════
        private static void BuildBottomLeft(RectTransform root, GameHUD hud)
        {
            var buffs = NewRect("BuffContainer", root);
            BottomLeft(buffs, Pad, 176f, 420f, 30f);
            var hlg = buffs.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing              = 8f;
            hlg.childControlWidth    = false;
            hlg.childControlHeight   = false;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;

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
        }

        // ═══════════════════════════════════════════════════════════════════
        // ล่างกลาง — ที่ว่างของ WeaponStatHUD (มันสร้างช่องเองจาก template)
        // ═══════════════════════════════════════════════════════════════════
        private static void BuildBottomCenter(RectTransform root)
        {
            SlotStrip(root, "WeaponRow", "WEAPONS", 118f);
            SlotStrip(root, "StatRow",   "PASSIVES", 62f);
        }

        private static void SlotStrip(RectTransform root, string name, string label, float y)
        {
            var strip = NewRect(name, root);
            strip.anchorMin = strip.anchorMax = new Vector2(0.5f, 0f);
            strip.pivot     = new Vector2(0.5f, 0f);
            strip.sizeDelta = new Vector2(520f, 52f);
            strip.anchoredPosition = new Vector2(0f, y);

            var cap = NewMono("Label", strip, label, 13f, 0.24f,
                              TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.35f));
            TopLeft(cap.rectTransform, 0f, -18f, 520f, 18f);

            var row = NewRect($"{name}Con", strip);
            Stretch(row);
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing              = 12f;
            hlg.childControlWidth    = false;
            hlg.childControlHeight   = false;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment       = TextAnchor.MiddleCenter;
        }

        // ═══════════════════════════════════════════════════════════════════
        // ล่างขวา — ช่องสกิล + แถบ passive
        // ═══════════════════════════════════════════════════════════════════
        private static void BuildBottomRight(RectTransform root, GameHUD hud)
        {
            hud.qSlot = AbilitySlot(root, "Q_Slot", Pad + 118f);
            hud.eSlot = AbilitySlot(root, "E_Slot", Pad);

            var charge = NewRect("ChargeBar_Panel", root);
            BottomRight(charge, Pad, 62f, 340f, 30f);

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

            var tab = NewMono("TabHint", root, "TAB  STATS", 14f, 0.22f,
                              TextAlignmentOptions.MidlineRight, new Color(1f, 1f, 1f, 0.35f));
            BottomRight(tab.rectTransform, Pad, 30f, 240f, 22f);
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

            var fill = NewImage("Fill", track.rectTransform, color);
            Stretch(fill.rectTransform);
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
