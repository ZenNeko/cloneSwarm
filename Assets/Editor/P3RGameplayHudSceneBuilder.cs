using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CloneSwarm.EditorTools.P3RBuilderKit;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// สร้างซีน **ภาพอ้างอิง** ของ GAMEPLAY HUD ตาม design handoff จอที่ 9
    /// เมนู: Tools > Clone Swarm > Build P3R Gameplay HUD Scene
    ///
    /// **จอนี้ต่างจากอีกแปดจอ — ตั้งใจไม่ต่อสายเข้า component จริงเลย**
    ///
    /// HUD จริงกระจายอยู่ใน `GameHUD` · `BossHUDUI` · `ObjectiveIndicatorUI` ·
    /// `TempPartyHUD` · `FloatingBuffUI` · `StatusHUDUI` · `AbilityHUDUI` ·
    /// `AugmentHUDUI` · `WeaponStatHUD` · `ChargeBarUI` — ทั้งหมดทำงานอยู่แล้วใน
    /// `SampleScene` และผูกกับ NetworkManager / ผู้เล่นที่ spawn แล้ว
    /// การสร้างใหม่ในซีนต้นแบบจะได้ของที่พังทันทีที่กด Play และไม่มีอะไรให้เอาไปใช้ต่อ
    ///
    /// **สิ่งที่จอนี้ใช้ทำ:** เอาไว้เทียบก่อน/หลัง เวลาจะรีสกิน HUD จริงในซีนเกม
    /// การเอาลงจริงคือ "ไปแก้สีกับฟอนต์ของ component ที่มีอยู่" ไม่ใช่ "เอาซีนนี้ไปแทน"
    ///
    /// **ข้อจำกัดของโซน C ที่ตกลงกันไว้ตั้งแต่รอบแรก — เอาได้แค่สี ฟอนต์ มุมบากเฉียง**
    ///   ห้ามเฉือนทั้งแถบ HP — ความยาวแถบคือข้อมูล การเอียงทำให้อ่านค่าผิด
    ///   ห้าม halftone หรือลายทับตัวเลข — ตัวเลขต้องอ่านออกตอนศัตรูเต็มจอ
    ///   ห้ามโมชั่น overshoot บนตัวเลขที่เปลี่ยนตลอด — HP ที่เด้งเกินแล้วเด้งกลับ
    ///     ทำให้ผู้เล่นอ่านค่าผิดในจังหวะที่ต้องตัดสินใจว่าจะถอยหรือสู้ต่อ
    /// ที่นี่จึงมีเฉพาะมุมบากบนสแลบเวลา/ปุ่มสกิล ส่วนแถบทุกอันเป็นสี่เหลี่ยมตรงหมด
    ///
    /// ก่อน/หลังต้องผ่าน **5-second test** — แคปจอตอนศัตรูเต็มจอ โชว์ 5 วิ ปิด
    /// แล้วถามว่า HP เหลือกี่ % สกิลไหนพร้อม · ถ้าความถูกต้องตกแม้แต่นิด แปลว่าแรงเกินไป
    /// </summary>
    public static class P3RGameplayHudSceneBuilder
    {
        private const string ScenePath = "Assets/GameScenes/Proto_GameplayHUD.unity";

        private const float Pad = 40f;

        private static readonly Color Slab    = new Color32(0x18, 0x24, 0xD8, 0xFF);
        private static readonly Color Chip    = new Color32(0x11, 0x18, 0x38, 0xFF);
        private static readonly Color BarBack = new Color32(0x1A, 0x1F, 0x33, 0xFF);
        private static readonly Color Hp      = new Color32(0x2C, 0xC5, 0xA0, 0xFF);
        private static readonly Color Exp     = new Color32(0x40, 0x73, 0xD9, 0xFF);

        [MenuItem("Tools/Clone Swarm/Build P3R Gameplay HUD Scene")]
        public static void Build()
        {
            if (!BeginScene(ScenePath, "GAMEPLAY HUD", out var scene)) return;

            BuildCamera(new Color32(0x14, 0x16, 0x1C, 0xFF));
            BuildEventSystem();
            var canvas = BuildCanvas("HUDCanvas");
            BuildScreen(canvas);

            EndScene(scene, ScenePath,
                "**ซีนนี้เป็นภาพอ้างอิงล้วน ไม่มี component HUD จริงสักตัว**\n" +
                "HUD จริงอยู่ใน SampleScene แล้วและผูกกับ NetworkManager · การเอาลงจริงคือ\n" +
                "ไปแก้สี/ฟอนต์ของ GameHUD · BossHUDUI · StatusHUDUI · AbilityHUDUI ฯลฯ\n" +
                "ไม่ใช่เอาซีนนี้ไปแทน\n\n" +
                "ข้อจำกัดโซน C ที่ต้องเคารพตอนรีสกิน:\n" +
                "  ห้ามเฉือนแถบ HP · ห้ามลายทับตัวเลข · ห้ามโมชั่น overshoot บนตัวเลขที่เปลี่ยนตลอด\n" +
                "ก่อน/หลังต้องผ่าน 5-second test ก่อน merge");
        }

        // ═══════════════════════════════════════════════════════════════════
        private static void BuildScreen(Canvas canvas)
        {
            var root = (RectTransform)canvas.transform;

            // พื้นหลังจำลองสนามรบ — ให้เห็นว่า HUD อ่านออกไหมบนพื้นที่ไม่ใช่สีดำสนิท
            var field = NewImage("FakeField", root, new Color32(0x14, 0x16, 0x1C, 0xFF));
            Stretch(field.rectTransform);

            var noise = NewImage("FakeCrowd", root, new Color(0.35f, 0.16f, 0.16f, 0.28f));
            Center(noise.rectTransform, 1180f, 620f);

            TopLeftCluster(root);
            TopCenterCluster(root);
            TopRightCluster(root);
            CenterBanner(root);
            LeftPartyCluster(root);
            BottomLeftCluster(root);
            BottomCenterCluster(root);
            BottomRightCluster(root);
        }

        // ── บนซ้าย: เวลา + เวฟ ───────────────────────────────────────────────
        private static void TopLeftCluster(RectTransform root)
        {
            var slab = NewImage("TimeSlab", root, Slab);
            TopLeft(slab.rectTransform, Pad, Pad, 208f, 62f);
            Shear(slab);                                  // สแลบเอียงได้ — ไม่ใช่แถบข้อมูล

            var time = NewMono("Time", slab.rectTransform, "14:22", 34f, 0.04f,
                               TextAlignmentOptions.Center);
            Stretch(time.rectTransform);

            var wave = NewMono("Wave", root, "WAVE 31", 20f, 0.2f,
                               TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.8f));
            TopLeft(wave.rectTransform, Pad + 226f, Pad + 16f, 260f, 32f);
        }

        // ── บนกลาง: บอส + มินิบอส ────────────────────────────────────────────
        private static void TopCenterCluster(RectTransform root)
        {
            var boss = NewRect("BossBar", root);
            CenterTop(boss, Pad, 760f, 58f);

            var name = NewMono("Name", boss, "PURPLE", 20f, 0.24f,
                               TextAlignmentOptions.MidlineLeft);
            TopLeft(name.rectTransform, 0f, 0f, 240f, 26f);

            var pct = NewMono("Percent", boss, "68%", 20f, 0.06f,
                              TextAlignmentOptions.MidlineRight);
            TopRight(pct.rectTransform, 0f, 0f, 140f, 26f);

            // แถบ HP บอส — สี่เหลี่ยมตรง ไม่เฉือน (ความยาว = ข้อมูล)
            Bar(boss, "Hp", 0f, 30f, 760f, 14f, 0.68f, Red);

            // มินิบอสสามตัว
            (string tag, float v)[] minis = { ("LICH", 0.62f), ("DUO", 0.28f), ("CAST", 0.9f) };
            for (int i = 0; i < minis.Length; i++)
            {
                var mini = NewRect($"Mini_{minis[i].tag}", root);
                CenterTop(mini, Pad + 70f, 760f, 26f);
                var mrt = mini;
                mrt.anchoredPosition = new Vector2(-254f + i * 254f, -(Pad + 70f));
                mrt.sizeDelta = new Vector2(236f, 26f);

                var tag = NewMono("Tag", mini, minis[i].tag, 14f, 0.2f,
                                  TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.62f));
                TopLeft(tag.rectTransform, 0f, 2f, 90f, 22f);

                Bar(mini, "Hp", 94f, 9f, 142f, 8f, minis[i].v, Orange);
            }
        }

        // ── บนขวา: objective ─────────────────────────────────────────────────
        private static void TopRightCluster(RectTransform root)
        {
            var panel = NewRect("Objectives", root);
            TopRight(panel, Pad, Pad, 400f, 190f);

            var head = NewMono("Head", panel, "OBJECTIVES", 15f, 0.28f,
                               TextAlignmentOptions.MidlineRight, new Color(1f, 1f, 1f, 0.45f));
            TopRight(head.rectTransform, 0f, 0f, 400f, 24f);

            ObjectiveRow(panel, "HOLD THE ZONE",  "0:42",  0, done: false);
            ObjectiveRow(panel, "COLLECT CORES",  "4 / 6", 1, done: false);
            ObjectiveRow(panel, "ESCORT",         "",      2, done: true);
        }

        private static void ObjectiveRow(RectTransform parent, string label, string value,
                                         int index, bool done)
        {
            var row = NewRect($"Obj_{index}", parent);
            TopRight(row, 0f, 34f + index * 44f, 400f, 38f);

            var bg = NewImage("Bg", row, Over(Color.black, Chip, 0.25f));
            Stretch(bg.rectTransform);

            AccentBar(row, done ? new Color(1f, 1f, 1f, 0.2f) : Teal, 4f);

            var name = NewMono("Label", row, label, 16f, 0.14f,
                               TextAlignmentOptions.MidlineLeft,
                               done ? new Color(1f, 1f, 1f, 0.3f) : Color.white);
            TopLeft(name.rectTransform, 18f, 8f, 250f, 24f);

            if (done)
            {
                // ไม่ใช้อักขระ ✓ — Sarabun ไม่มีกลิฟตัวนี้ จะได้กล่องสี่เหลี่ยม
                var mark = NewImage("Done", row, new Color(1f, 1f, 1f, 0.3f));
                TopRight(mark.rectTransform, 20f, 15f, 12f, 12f);
                Shear(mark);
            }
            else
            {
                var val = NewMono("Value", row, value, 18f, 0.06f,
                                  TextAlignmentOptions.MidlineRight);
                TopRight(val.rectTransform, 16f, 7f, 120f, 24f);
            }
        }

        // ── กลางจอ: แบนเนอร์เตือน ────────────────────────────────────────────
        private static void CenterBanner(RectTransform root)
        {
            var banner = NewRect("EnrageBanner", root);
            banner.anchorMin = banner.anchorMax = banner.pivot = new Vector2(0.5f, 0.5f);
            banner.sizeDelta = new Vector2(560f, 64f);
            banner.anchoredPosition = new Vector2(0f, 168f);

            var bg = NewImage("Bg", banner, Over(Orange, InkDeep, 0.86f));
            Stretch(bg.rectTransform);
            Shear(bg);                                   // แบนเนอร์เตือนเอียงได้ ไม่ใช่ข้อมูลต่อเนื่อง

            var label = NewMono("Label", banner, "ENRAGE IN 12s", 26f, 0.16f,
                                TextAlignmentOptions.Center, InkDeep);
            Stretch(label.rectTransform);
        }

        // ── กลางซ้าย: แถวปาร์ตี้ ─────────────────────────────────────────────
        private static void LeftPartyCluster(RectTransform root)
        {
            (string slot, string ch, string state, bool down)[] rows =
            {
                ("PLAYER 1", "HUNTER", "240 / 240", false),
                ("PLAYER 2", "GUNNER", "REVIVE 6s", true),
            };

            for (int i = 0; i < rows.Length; i++)
            {
                var row = NewRect($"Party_{i}", root);
                row.anchorMin = row.anchorMax = new Vector2(0f, 0.5f);
                row.pivot = new Vector2(0f, 0.5f);
                row.sizeDelta = new Vector2(380f, 44f);
                row.anchoredPosition = new Vector2(Pad, 40f - i * 52f);

                var bg = NewImage("Bg", row, Over(Color.black, Chip, 0.3f));
                Stretch(bg.rectTransform);
                AccentBar(row, rows[i].down ? Red : Exp, 4f);

                var slot = NewMono("Slot", row, rows[i].slot, 13f, 0.22f,
                                   TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.45f));
                TopLeft(slot.rectTransform, 16f, 4f, 130f, 20f);

                var ch = NewMono("Char", row, rows[i].ch, 17f, 0.1f,
                                 TextAlignmentOptions.MidlineLeft);
                TopLeft(ch.rectTransform, 16f, 21f, 150f, 22f);

                var state = NewMono("State", row, rows[i].state, 17f, 0.06f,
                                    TextAlignmentOptions.MidlineRight,
                                    rows[i].down ? Red : Hp);
                TopRight(state.rectTransform, 16f, 12f, 200f, 24f);
            }
        }

        // ── ล่างซ้าย: บัฟ + เลเวล + HP + EXP ─────────────────────────────────
        private static void BottomLeftCluster(RectTransform root)
        {
            string[] buffs = { "HST", "BRN 3", "SHD" };
            for (int i = 0; i < buffs.Length; i++)
            {
                var chip = NewRect($"Buff_{i}", root);
                BottomLeft(chip, Pad + i * 84f, 168f, 76f, 30f);
                var bg = NewImage("Bg", chip, Over(Teal, Chip, 0.4f));
                Stretch(bg.rectTransform);
                Shear(bg);
                var label = NewMono("Label", chip, buffs[i], 14f, 0.12f, TextAlignmentOptions.Center);
                Stretch(label.rectTransform);
            }

            var lv = NewRect("Level", root);
            BottomLeft(lv, Pad, 96f, 108f, 52f);
            var lvBg = NewImage("Bg", lv, Slab);
            Stretch(lvBg.rectTransform);
            Shear(lvBg);
            var lvLabel = NewMono("Label", lv, "LV 27", 22f, 0.08f, TextAlignmentOptions.Center);
            Stretch(lvLabel.rectTransform);

            var hp = NewMono("Hp", root, "181 / 275", 30f, 0.04f, TextAlignmentOptions.MidlineLeft);
            BottomLeft(hp.rectTransform, Pad + 126f, 108f, 240f, 40f);

            var heal = NewMono("Regen", root, "+38", 20f, 0.06f,
                               TextAlignmentOptions.MidlineLeft, Hp);
            BottomLeft(heal.rectTransform, Pad + 348f, 112f, 120f, 32f);

            // แถบ HP + EXP — สี่เหลี่ยมตรงทั้งคู่ ห้ามเฉือน
            var barRoot = NewRect("Bars", root);
            BottomLeft(barRoot, Pad + 126f, 62f, 420f, 34f);
            Bar(barRoot, "Hp",  0f, 0f,  420f, 12f, 0.66f, Hp);
            Bar(barRoot, "Exp", 0f, 20f, 420f, 8f,  0.62f, Exp);

            var expLabel = NewMono("ExpLabel", root, "EXP 62%", 14f, 0.2f,
                                   TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.5f));
            BottomLeft(expLabel.rectTransform, Pad + 556f, 62f, 180f, 22f);
        }

        // ── ล่างกลาง: อาวุธ + augment ────────────────────────────────────────
        private static void BottomCenterCluster(RectTransform root)
        {
            string[] weapons  = { "BLD", "ARC", "ORB", "SPK" };
            string[] augments = { "ATK", "HST", "AOE" };

            SlotRow(root, "Weapons",  weapons,  118f, Exp);
            SlotRow(root, "Augments", augments, 62f,  Green);
        }

        private static void SlotRow(RectTransform root, string name, string[] tags,
                                    float y, Color tint)
        {
            var row = NewRect(name, root);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0f);
            row.pivot     = new Vector2(0.5f, 0f);
            row.sizeDelta = new Vector2(tags.Length * 78f, 52f);
            row.anchoredPosition = new Vector2(0f, y);

            for (int i = 0; i < tags.Length; i++)
            {
                var slot = NewRect($"Slot_{i}", row);
                slot.anchorMin = slot.anchorMax = new Vector2(0f, 0.5f);
                slot.pivot     = new Vector2(0f, 0.5f);
                slot.sizeDelta = new Vector2(66f, 48f);
                slot.anchoredPosition = new Vector2(i * 78f, 0f);

                var bg = NewImage("Bg", slot, Over(tint, Chip, 0.35f));
                Stretch(bg.rectTransform);
                Shear(bg);

                var label = NewMono("Label", slot, tags[i], 15f, 0.1f, TextAlignmentOptions.Center);
                Stretch(label.rectTransform);
            }
        }

        // ── ล่างขวา: สกิล + charge ───────────────────────────────────────────
        private static void BottomRightCluster(RectTransform root)
        {
            // ป้ายปุ่มอ่านจาก IHUDAbility.HUDKeyLabel ตอนรันจริง — แบบเขียน Q/E ซึ่งล้าสมัย
            // ตั้งแต่ 28154a4f ที่ย้ายไปคลิกซ้าย/ขวา · ที่นี่โชว์ค่าที่ผูกอยู่จริงตอนนี้
            AbilitySlot(root, "SlotQ", "LMB", "3.4", Pad + 118f, ready: false);
            AbilitySlot(root, "SlotE", "RMB", "",    Pad,        ready: true);

            var charge = NewRect("Charge", root);
            BottomRight(charge, Pad, 62f, 340f, 30f);

            var label = NewMono("Label", charge, "CHARGE", 14f, 0.24f,
                                TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.5f));
            TopLeft(label.rectTransform, 0f, 4f, 110f, 22f);

            Bar(charge, "Bar", 116f, 11f, 160f, 10f, 0.78f, Gold);

            var pct = NewMono("Percent", charge, "78%", 15f, 0.06f,
                              TextAlignmentOptions.MidlineRight);
            TopRight(pct.rectTransform, 0f, 4f, 60f, 22f);

            var tab = NewMono("TabHint", root, "TAB  STATS", 14f, 0.22f,
                              TextAlignmentOptions.MidlineRight, new Color(1f, 1f, 1f, 0.35f));
            BottomRight(tab.rectTransform, Pad, 30f, 240f, 22f);
        }

        private static void AbilitySlot(RectTransform root, string name, string key,
                                        string cooldown, float xFromRight, bool ready)
        {
            var slot = NewRect(name, root);
            BottomRight(slot, xFromRight, 102f, 100f, 100f);

            var bg = NewImage("Bg", slot, ready ? Over(Exp, Chip, 0.45f) : Over(Color.black, Chip, 0.4f));
            Stretch(bg.rectTransform);
            Shear(bg);

            var keyLabel = NewMono("Key", slot, key, 15f, 0.14f,
                                   TextAlignmentOptions.TopLeft, new Color(1f, 1f, 1f, 0.6f));
            TopLeft(keyLabel.rectTransform, 12f, 8f, 80f, 22f);

            if (!string.IsNullOrEmpty(cooldown))
            {
                // ตัวเลขคูลดาวน์ — ห้ามใส่โมชั่น overshoot ตรงนี้ มันเปลี่ยนทุกเฟรม
                var cd = NewMono("Cooldown", slot, cooldown, 30f, 0.04f, TextAlignmentOptions.Center);
                Stretch(cd.rectTransform);
            }
            else
            {
                var readyLabel = NewMono("Ready", slot, "READY", 14f, 0.2f,
                                         TextAlignmentOptions.Center, Color.white);
                Stretch(readyLabel.rectTransform);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        /// <summary>
        /// แถบข้อมูล — **สี่เหลี่ยมตรงเสมอ ไม่มี Shear**
        /// ความยาวแถบคือค่าที่ผู้เล่นต้องอ่าน การเอียงทำให้ปลายแถบไม่ตรงกับตัวเลข
        /// </summary>
        private static void Bar(RectTransform parent, string name, float x, float y,
                                float w, float h, float fill, Color color)
        {
            var track = NewImage($"{name}Track", parent, BarBack);
            TopLeft(track.rectTransform, x, y, w, h);

            var bar = NewImage("Fill", track.rectTransform, color);
            var rt = bar.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(Mathf.Clamp01(fill), 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
