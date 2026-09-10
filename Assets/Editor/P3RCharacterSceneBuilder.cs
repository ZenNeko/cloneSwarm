using CloneSwarm.UI.P3R;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CloneSwarm.EditorTools.P3RBuilderKit;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// สร้างซีนต้นแบบจอ CHARACTER ตาม design handoff จอที่ 3
    /// เมนู: Tools > Clone Swarm > Build P3R Character Scene
    ///
    /// **`Lv. 1 / 25` ในแบบไม่มีในเกม** — ไม่มีระบบเลเวลรายตัวละคร
    /// สร้างช่องไว้แต่ปิดอยู่ · เปิดเมื่อมีระบบจริงแล้วเท่านั้น
    ///
    /// **ห้ามแตะ `CharacterSelectUI.SelectedCharacter`** — เป็น static ที่
    /// `PlayerWeaponManager` อ่านข้ามซีน · และตัวระบุบนเน็ตเวิร์กต้องเป็น
    /// `CharacterData.characterName` (string) ไม่ใช่ index (CLAUDE.md ข้อ 10)
    /// </summary>
    public static class P3RCharacterSceneBuilder
    {
        private const string ScenePath = "Assets/GameScenes/Proto_Character.unity";

        private const float TopBarH  = 84f;
        private const float TabBarH  = 62f;
        private const float ContentY = TopBarH + TabBarH;
        private const float PadX     = 64f;
        private const float ListW    = 360f;
        private const float RightW   = 480f;
        private const float Gap      = 24f;

        private static readonly Color TopBar  = new Color32(0x08, 0x0B, 0x18, 0xFF);
        private static readonly Color PanelBg = new Color32(0x0D, 0x12, 0x26, 0xFF);
        private static readonly Color CardBg  = new Color32(0x11, 0x18, 0x38, 0xFF);

        [MenuItem("Tools/Clone Swarm/Build P3R Character Scene")]
        public static void Build()
        {
            if (!BeginScene(ScenePath, "CHARACTER", out var scene)) return;

            BuildCamera(InkDeep);
            BuildEventSystem();
            var canvas = BuildCanvas();
            BuildScreen(canvas);

            EndScene(scene, ScenePath,
                "ขั้นถัดไป:\n" +
                "  1) ช่อง 'Lv. 1 / 25' ปิดไว้ — ยังไม่มีระบบเลเวลรายตัวละคร เปิดเมื่อมีของจริง\n" +
                "  2) พอร์เทรตยังว่าง — Char_Hunter / Char_Gunner มีฟิลด์ portrait ว่างทั้งคู่\n" +
                "  3) ATK/DEF โชว์ '—' เพราะ CharacterData มีแต่ baseHealth/baseMoveSpeed\n" +
                "     ที่ต่อสายเข้า gameplay จริง · สี่ค่าที่เหลือเป็น display-only\n" +
                "  4) การ์ดในซีนเป็นตัวอย่าง — ตอนรัน CharacterSelectUI สร้างใหม่จาก characters");
        }

        // ═══════════════════════════════════════════════════════════════════
        private static void BuildScreen(Canvas canvas)
        {
            var root = (RectTransform)canvas.transform;

            var backdrop = NewImage("Backdrop", root, InkDeep);
            Stretch(backdrop.rectTransform);

            var scan = NewImage("Scanlines", root, new Color(1f, 1f, 1f, 0.04f));
            Stretch(scan.rectTransform);
            scan.gameObject.SetActive(false);

            var ui = root.gameObject.AddComponent<CharacterSelectUI>();

            BuildChrome(root, ui);
            BuildCardList(root, ui);
            BuildPortrait(root, ui);
            BuildDetail(root, ui);
        }

        private static void BuildChrome(RectTransform root, CharacterSelectUI ui)
        {
            var bar = NewImage("TopBar", root, TopBar);
            var brt = bar.rectTransform;
            brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.sizeDelta = new Vector2(0f, TopBarH);
            brt.anchoredPosition = Vector2.zero;

            var title = NewMono("Title", brt, "CLONE SWARM", 22f, 0.26f, TextAlignmentOptions.MidlineLeft);
            TopLeft(title.rectTransform, PadX, 26f, 460f, 34f);

            var gold = NewMono("Gold", brt, "8,420 G", 22f, 0.10f,
                               TextAlignmentOptions.MidlineRight, Gold);
            TopRight(gold.rectTransform, PadX, 26f, 240f, 34f);
            ui.goldText = gold;

            var tabs = NewRect("TabBar", root);
            tabs.anchorMin = new Vector2(0f, 1f); tabs.anchorMax = new Vector2(1f, 1f);
            tabs.pivot = new Vector2(0.5f, 1f);
            tabs.sizeDelta = new Vector2(0f, TabBarH);
            tabs.anchoredPosition = new Vector2(0f, -TopBarH);

            string[] names = { "LOBBY", "MAP", "CHARACTER", "SHOP" };
            float x = PadX;
            for (int i = 0; i < names.Length; i++)
            {
                var tab = NewRect($"Tab_{names[i]}", tabs);
                tab.anchorMin = tab.anchorMax = new Vector2(0f, 0.5f);
                tab.pivot = new Vector2(0f, 0.5f);
                tab.sizeDelta = new Vector2(176f, 42f);
                tab.anchoredPosition = new Vector2(x, 0f);

                bool active = i == 2;
                var bg = NewImage("Bg", tab, active ? Primary : Lift(InkDeep, 0.05f));
                Stretch(bg.rectTransform);
                Shear(bg);

                var label = NewMono("Label", tab, names[i], 16f, 0.18f, TextAlignmentOptions.Center,
                                    active ? Color.white : new Color(1f, 1f, 1f, 0.55f));
                Stretch(label.rectTransform);
                x += 186f;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        private static void BuildCardList(RectTransform root, CharacterSelectUI ui)
        {
            float h = RefH - ContentY - 36f;

            var panel = NewImage("CardList", root, PanelBg);
            TopLeft(panel.rectTransform, PadX, ContentY + 12f, ListW, h);

            var cards = NewRect("Cards", panel.rectTransform);
            Inset(cards, 14f, 14f, 14f, 14f);
            ui.cardsContainer = cards;

            var template = BuildCardTemplate(cards);
            ui.cardTemplate = template.gameObject;

            // ตัวอย่างในซีน — ตอนรัน CharacterSelectUI สร้างใหม่จากรายชื่อจริง
            (string name, string role, string state)[] sample =
            {
                ("HUNTER", "RANGED",  "OWNED"),
                ("GUNNER", "BURST",   "OWNED"),
                ("RIVEN",  "DUELIST", "LOCKED"),
                ("? ? ?",  "",        "COMING SOON"),
            };

            for (int i = 0; i < sample.Length; i++)
            {
                var card = Object.Instantiate(template, cards);
                card.gameObject.SetActive(true);
                card.name = $"Sample_{i}";
                var rt = (RectTransform)card.transform;
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(0f, 108f);
                rt.anchoredPosition = new Vector2(0f, -i * 118f);

                SetChild(card, "Name",  sample[i].name, 14f);
                SetChild(card, "Role",  sample[i].role, 22f);
                SetChild(card, "State", sample[i].state, 22f);

                var st = card.transform.Find("State")?.GetComponent<TextMeshProUGUI>();
                if (st != null)
                    st.color = sample[i].state == "OWNED" ? Teal
                             : sample[i].state == "LOCKED" ? Amber
                             : new Color(1f, 1f, 1f, 0.3f);

                var bg = card.transform.Find("Bg")?.GetComponent<Image>();
                if (bg != null) bg.color = i == 2 ? Over(Primary, CardBg, 0.5f) : CardBg;

                var accent = card.transform.Find("Accent")?.GetComponent<Image>();
                if (accent != null) accent.color = i == 2 ? Primary : new Color(1f, 1f, 1f, 0.10f);
            }

            template.gameObject.SetActive(false);
        }

        private static void SetChild(RectTransform card, string child, string text, float tracking)
        {
            var t = card.transform.Find(child)?.GetComponent<TextMeshProUGUI>();
            if (t == null) return;
            t.gameObject.SetActive(!string.IsNullOrEmpty(text));
            P3RText.SetTextAndTracking(t, text, tracking);
        }

        private static RectTransform BuildCardTemplate(RectTransform parent)
        {
            var rt = NewRect("CharacterCardTemplate", parent);
            rt.sizeDelta = new Vector2(332f, 108f);

            var bg = NewImage("Bg", rt, CardBg);
            Stretch(bg.rectTransform);

            AccentBar(rt, new Color(1f, 1f, 1f, 0.10f));

            var icon = NewImage("Icon", rt, Lift(CardBg, 0.06f));
            TopLeft(icon.rectTransform, 20f, 20f, 68f, 68f);

            var name = NewMono("Name", rt, "HUNTER", 22f, 0.14f, TextAlignmentOptions.MidlineLeft);
            TopLeft(name.rectTransform, 102f, 22f, 200f, 30f);

            var role = NewMono("Role", rt, "RANGED", 13f, 0.22f,
                               TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.45f));
            TopLeft(role.rectTransform, 102f, 52f, 200f, 22f);

            var state = NewMono("State", rt, "OWNED", 13f, 0.22f,
                                TextAlignmentOptions.MidlineLeft, Teal);
            TopLeft(state.rectTransform, 102f, 74f, 200f, 22f);

            var lockOverlay = NewImage("LockOverlay", rt, new Color(6 / 255f, 8 / 255f, 18 / 255f, 0.6f));
            Stretch(lockOverlay.rectTransform);
            lockOverlay.gameObject.SetActive(false);

            var lockCost = NewMono("LockCost", lockOverlay.rectTransform, "1,000 G", 17f, 0.1f,
                                   TextAlignmentOptions.Center, Gold);
            Stretch(lockCost.rectTransform);

            var card = rt.gameObject.AddComponent<CharacterCardUI>();
            card.bgImage      = bg;
            card.iconImage    = icon;
            card.nameText     = name;
            card.weaponText   = role;
            card.lockOverlay  = lockOverlay.gameObject;
            card.lockCostText = lockCost;

            return rt;
        }

        // ═══════════════════════════════════════════════════════════════════
        private static void BuildPortrait(RectTransform root, CharacterSelectUI ui)
        {
            float h = RefH - ContentY - 36f;
            float x = PadX + ListW + Gap;
            float w = RefW - PadX * 2f - ListW - RightW - Gap * 2f;

            var panel = NewImage("PortraitPanel", root, Lift(InkDeep, 0.03f));
            TopLeft(panel.rectTransform, x, ContentY + 12f, w, h);

            var portrait = NewImage("Portrait", panel.rectTransform, Lift(InkDeep, 0.05f));
            Stretch(portrait.rectTransform);
            portrait.preserveAspect = true;
            ui.detailPortrait = portrait;

            var hint = NewMono("Hint", panel.rectTransform, "PORTRAIT", 17f, 0.3f,
                               TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.14f));
            Stretch(hint.rectTransform);

            // ── การ์ดคำบรรยายลอยล่าง ─────────────────────────────────────────
            var card = NewImage("DescCard", panel.rectTransform, new Color(10 / 255f, 14 / 255f, 30 / 255f, 0.92f));
            BottomLeft(card.rectTransform, 28f, 28f, w - 56f, 172f);
            AccentBar(card.rectTransform, Primary);

            var name = NewText("Name", card.rectTransform, "RIVEN", 34f, TextAlignmentOptions.MidlineLeft);
            MonoStyle(name, 0.04f);
            TopLeft(name.rectTransform, 26f, 16f, 420f, 42f);
            ui.detailName = name;

            // ไม่ใช้อักขระ ✓ — Sarabun ไม่มีกลิฟตัวนี้ TMP จะวาดเป็นกล่องสี่เหลี่ยม
            // วาดเป็นรูปเองแทน จะได้ไม่ต้องพึ่งว่าฟอนต์ตัวไหนมีอะไรบ้าง
            var check = NewMono("Check", card.rectTransform, "SELECTED", 14f, 0.2f,
                                TextAlignmentOptions.MidlineRight, Teal);
            TopRight(check.rectTransform, 26f, 22f, 240f, 28f);

            var checkMark = NewImage("CheckMark", card.rectTransform, Teal);
            TopRight(checkMark.rectTransform, 130f, 30f, 12f, 12f);
            Shear(checkMark);

            var desc = NewText("Desc", card.rectTransform,
                               "สะสมพลังจากการเคลื่อนที่ แล้วใช้มันเปิดใบดาบออก — " +
                               "พุ่งได้สามครั้ง จบด้วยฟันหนักหนึ่งที",
                               19f, TextAlignmentOptions.TopLeft, new Color(1f, 1f, 1f, 0.7f));
            Wrap(desc);
            TopLeft(desc.rectTransform, 26f, 66f, w - 108f, 86f);
            ui.detailDesc = desc;
        }

        // ═══════════════════════════════════════════════════════════════════
        private static void BuildDetail(RectTransform root, CharacterSelectUI ui)
        {
            float h = RefH - ContentY - 36f;

            var panel = NewImage("DetailPanel", root, PanelBg);
            TopRight(panel.rectTransform, PadX, ContentY + 12f, RightW, h);
            var prt = panel.rectTransform;

            var name = NewText("Name", prt, "RIVEN", 46f, TextAlignmentOptions.MidlineLeft);
            MonoStyle(name, 0.02f);
            TopLeft(name.rectTransform, 28f, 26f, 420f, 56f);

            var role = NewMono("Role", prt, "DUELIST", 16f, 0.26f,
                               TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.5f));
            TopLeft(role.rectTransform, 28f, 82f, 300f, 26f);

            // ระบบเลเวลรายตัวละครยังไม่มี — สร้างช่องไว้แต่ปิด
            var level = NewMono("Level", prt, "Lv. 1 / 25", 16f, 0.18f,
                                TextAlignmentOptions.MidlineRight, Gold);
            TopRight(level.rectTransform, 28f, 82f, 220f, 26f);
            level.gameObject.SetActive(false);

            // ── แท็บ STATS / ABILITY ────────────────────────────────────────
            ui.statsTabButton   = Tab(prt, "StatsTab",   "STATS",   28f,  active: true,  out var statsLabel);
            ui.abilityTabButton = Tab(prt, "AbilityTab", "ABILITY", 236f, active: false, out var abilLabel);
            ui.statsTabLabel   = statsLabel;
            ui.abilityTabLabel = abilLabel;

            // ── ตารางสเตต ────────────────────────────────────────────────────
            var stats = NewRect("StatsPanel", prt);
            TopLeft(stats, 28f, 190f, RightW - 56f, 340f);
            ui.statsPanel = stats.gameObject;

            // ATK/DEF/CRIT เป็น display-only — CharacterData ต่อสายเข้า gameplay
            // แค่ baseHealth กับ baseMoveSpeed จึงโชว์ '—' ไม่ใช่เลขที่ไม่มีความหมาย
            ui.statHpValue      = StatRow(stats, "HP",       "120", 0);
            ui.statAtkValue     = StatRow(stats, "ATK",      "—",   1);
            ui.statDefValue     = StatRow(stats, "DEF",      "—",   2);
            ui.statSpdValue     = StatRow(stats, "SPD",      "6.0", 3);
            ui.statCritRateValue= StatRow(stats, "CRIT",     "—",   4);
            ui.statCritDmgValue = StatRow(stats, "CRIT DMG", "—",   5);

            // ── แผงสกิล (ปิดไว้ อยู่คนละแท็บ) ───────────────────────────────
            var abil = NewRect("AbilityPanel", prt);
            TopLeft(abil, 28f, 190f, RightW - 56f, 340f);
            ui.abilityPanel = abil.gameObject;
            abil.gameObject.SetActive(false);

            ui.detailPassiveName  = AbilityRow(abil, "Passive",  "PASSIVE",  0);
            ui.detailWeaponName   = AbilityRow(abil, "Weapon",   "WEAPON",   1);
            ui.detailAbilityName  = AbilityRow(abil, "Ability",  "ABILITY",  2);
            ui.detailUltimateName = AbilityRow(abil, "Ultimate", "ULTIMATE", 3);

            // ── ปุ่ม SELECT ─────────────────────────────────────────────────
            var selRoot = NewRect("SelectButton", prt);
            BottomLeft(selRoot, 28f, 28f, RightW - 56f, 66f);

            var bg = NewImage("Bg", selRoot, Primary);
            Stretch(bg.rectTransform);
            bg.raycastTarget = true;
            Shear(bg);

            var label = NewMono("Label", selRoot, "SELECT", 22f, 0.18f, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);

            var btn = selRoot.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
            ui.selectButton       = btn;
            ui.confirmButton      = btn;
            ui.confirmButtonLabel = label;

            var status = NewMono("LockStatus", prt, "", 15f, 0.14f,
                                 TextAlignmentOptions.Center, Amber);
            BottomLeft(status.rectTransform, 28f, 100f, RightW - 56f, 24f);
            ui.lockStatusText = status;
        }

        private static Button Tab(RectTransform parent, string name, string text, float x,
                                  bool active, out TextMeshProUGUI label)
        {
            var root = NewRect(name, parent);
            TopLeft(root, x, 128f, 196f, 44f);

            var bg = NewImage("Bg", root, active ? Over(Primary, PanelBg, 0.55f) : Lift(PanelBg, 0.06f));
            Stretch(bg.rectTransform);
            bg.raycastTarget = true;
            Shear(bg);

            label = NewMono("Label", root, text, 16f, 0.2f, TextAlignmentOptions.Center,
                            active ? Color.white : new Color(1f, 1f, 1f, 0.55f));
            Stretch(label.rectTransform);

            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
            return btn;
        }

        private static TextMeshProUGUI StatRow(RectTransform parent, string label, string value, int index)
        {
            var row = NewRect($"Stat_{label}", parent);
            TopLeft(row, 0f, index * 52f, RightW - 56f, 44f);

            var bg = NewImage("Bg", row, index % 2 == 0 ? Lift(PanelBg, 0.04f) : Lift(PanelBg, 0.02f));
            Stretch(bg.rectTransform);

            var name = NewMono("Label", row, label, 15f, 0.22f,
                               TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.55f));
            var nrt = name.rectTransform;
            nrt.anchorMin = new Vector2(0f, 0f); nrt.anchorMax = new Vector2(1f, 1f);
            nrt.offsetMin = new Vector2(18f, 0f); nrt.offsetMax = new Vector2(-18f, 0f);

            var val = NewMono("Value", row, value, 20f, 0.06f, TextAlignmentOptions.MidlineRight);
            var vrt = val.rectTransform;
            vrt.anchorMin = new Vector2(0f, 0f); vrt.anchorMax = new Vector2(1f, 1f);
            vrt.offsetMin = new Vector2(18f, 0f); vrt.offsetMax = new Vector2(-18f, 0f);

            return val;
        }

        private static TextMeshProUGUI AbilityRow(RectTransform parent, string name, string head, int index)
        {
            var row = NewRect($"Ability_{name}", parent);
            TopLeft(row, 0f, index * 84f, RightW - 56f, 76f);

            var bg = NewImage("Bg", row, Lift(PanelBg, 0.04f));
            Stretch(bg.rectTransform);
            AccentBar(row, Primary, 4f);

            var kind = NewMono("Kind", row, head, 13f, 0.24f,
                               TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.45f));
            TopLeft(kind.rectTransform, 20f, 12f, 260f, 22f);

            var value = NewText("Name", row, "—", 20f, TextAlignmentOptions.MidlineLeft);
            TopLeft(value.rectTransform, 20f, 34f, 340f, 30f);
            return value;
        }
    }
}
