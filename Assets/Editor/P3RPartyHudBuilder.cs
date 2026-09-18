using System.Linq;
using System.Text;
using CloneSwarm.UI.P3R;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// แถวเพื่อนร่วมทีม + แถบ augment บน HUD ตอนเล่น
    /// เมนู: Tools > Clone Swarm > Wire Party HUD + Augment Strip
    ///
    /// ═══ ทำไมไม่สร้างผ่าน builder ของ HUD ทั้งแผง ═══
    ///
    /// `P3RGameplayHudV2SceneBuilder` สร้าง HUD ทั้งแผง และการ migrate คือ
    /// **ลบของเดิมทิ้งแล้วสร้างใหม่** — งานจัดวางที่ทำด้วยมือในซีนจะหายหมด
    /// ซึ่งตอนนี้มีเยอะ (ทุกชิ้นในมุมล่างซ้ายถูกขยับไปจากพิกัดในโค้ดแล้ว)
    ///
    /// ตัวนี้จึง **เติมของใหม่เข้าไปในแผงที่มีอยู่** ไม่แตะของเดิมสักชิ้น
    /// แม่แบบแถวยังออกจาก `P3RBuilderKit.SavePrefab` ตามกติกา (ยามกัน prefab
    /// ที่ถูกแก้มือไม่ให้ถูกเขียนทับเงียบๆ อยู่ในนั้น)
    ///
    /// ═══ ตำแหน่งที่วาง และทำไม ═══
    ///
    /// มุมล่างซ้ายเรียงจากล่างขึ้นบนตามลำดับที่ตาอ่าน: **ตัวเรา → ของที่เราถือ → เพื่อน**
    ///
    ///   y  45..137   Hp and Exp Bar · LevelSlab · CharacterIcon_Box   (ของเดิม)
    ///   y 137..187   ChargeBar_Panel · TabHint                        (ของเดิม)
    ///   y 196..240   AugmentStrip   ← ใหม่ · ข้างบนไอคอนตัวละคร
    ///   y 252..       PartyPanel    ← ใหม่ · แถวซ้อนขึ้นบน
    ///
    /// ตัวเลขพวกนี้อ่านจากซีนจริงตอนเขียน ไม่ใช่จากแบบ — ถ้าขยับของเดิมอีก
    /// ต้องขยับสองค่านี้ตาม (หรือลากเอาในซีน แล้วอย่ารันตัวนี้ซ้ำ)
    ///
    /// รันซ้ำได้ — มีอยู่แล้วก็แค่เช็คสายให้ ไม่ย้ายของที่ลากไว้
    /// </summary>
    public static class P3RPartyHudBuilder
    {
        private const string ScenePath = "Assets/GameScenes/SampleScene.unity";
        private const string HudPanel  = "P3R_HUD";
        private const string RowPrefab = P3RBuilderKit.PrefabDir + "/PartyMemberRow.prefab";
        private const string SlotPrefab = P3RBuilderKit.PrefabDir + "/BuildStripSlot.prefab";

        private const string PartyName = "PartyPanel";
        private const string AugName   = "AugmentStrip";

        private const float RowW = 414f;   // เท่ากับ Hp and Exp Bar เพื่อให้ขอบซ้าย-ขวาตรงกัน
        private const float RowH = 48f;

        private static readonly Color RowBg     = new Color32(0x11, 0x18, 0x38, 0xF2);
        private static readonly Color NameCol   = Color.white;
        private static readonly Color LabelCol  = new Color(1f, 1f, 1f, 0.55f);
        private static readonly Color AliveCol  = new Color32(0x3E, 0xE0, 0xC4, 0xFF);

        [MenuItem("Tools/Clone Swarm/Wire Party HUD + Augment Strip")]
        public static void Wire()
        {
            var rowAsset = BuildRowPrefab();
            if (rowAsset == null)
            {
                Debug.LogError("[ปาร์ตี้ HUD] สร้างแม่แบบแถวไม่สำเร็จ");
                return;
            }

            var slotAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefab);
            if (slotAsset == null)
                Debug.LogWarning($"[ปาร์ตี้ HUD] ไม่เจอ {SlotPrefab} — แถบ augment จะไม่มีช่อง");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var log = new StringBuilder($"[ปาร์ตี้ HUD] {ScenePath}\n");
            int n = 0;

            var hud = Find(scene, HudPanel);
            if (hud == null) { Debug.LogError($"[ปาร์ตี้ HUD] หา {HudPanel} ไม่เจอ"); return; }

            n += EnsureParty(hud.transform as RectTransform, rowAsset, log);
            n += EnsureAugment(hud.transform as RectTransform, slotAsset, log);

            if (n == 0) { Debug.Log(log.AppendLine("  ต่อครบแล้ว ไม่มีอะไรต้องทำ").ToString()); return; }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine($"  บันทึกแล้ว ({n} รายการ)");
            Debug.Log(log.ToString());
        }

        // ═══════════════════════════════════════════════════════════════════
        // PANELS IN SCENE
        // ═══════════════════════════════════════════════════════════════════
        private static int EnsureParty(RectTransform hud, GameObject rowAsset, StringBuilder log)
        {
            int n = 0;
            var rt = hud.Find(PartyName) as RectTransform;

            if (rt == null)
            {
                rt = NewRect(PartyName, hud);
                Corner(rt, new Vector2(40f, 252f), new Vector2(RowW, 3f * (RowH + 6f)));
                log.AppendLine($"  สร้าง {PartyName} ที่ (40, 252)");
                n++;
            }

            var hudUi = rt.GetComponent<PartyMemberHUD>() ?? rt.gameObject.AddComponent<PartyMemberHUD>();

            if (hudUi.rowTemplate == null || hudUi.rowTemplate.gameObject != rowAsset)
            {
                hudUi.rowTemplate = rowAsset.GetComponent<PartyMemberRowUI>();
                EditorUtility.SetDirty(hudUi);
                log.AppendLine("  rowTemplate → PartyMemberRow.prefab");
                n++;
            }

            if (hudUi.rowArea != rt) { hudUi.rowArea = rt; EditorUtility.SetDirty(hudUi); n++; }
            return n;
        }

        private static int EnsureAugment(RectTransform hud, GameObject slotAsset, StringBuilder log)
        {
            int n = 0;
            var rt = hud.Find(AugName) as RectTransform;

            if (rt == null)
            {
                rt = NewRect(AugName, hud);
                Corner(rt, new Vector2(40f, 196f), new Vector2(3f * 44f + 2f * 6f, 44f));
                log.AppendLine($"  สร้าง {AugName} ที่ (40, 196) — 3 ช่อง");
                n++;
            }

            var strip = rt.GetComponent<AugmentStripUI>() ?? rt.gameObject.AddComponent<AugmentStripUI>();

            var slotUi = slotAsset != null ? slotAsset.GetComponent<BuildStripSlot>() : null;
            if (slotUi != null && strip.slotTemplate != slotUi)
            {
                strip.slotTemplate = slotUi;
                EditorUtility.SetDirty(strip);
                log.AppendLine("  slotTemplate → BuildStripSlot.prefab (ช่องชุดเดียวกับแถบ build)");
                n++;
            }

            if (strip.slotArea != rt) { strip.slotArea = rt; EditorUtility.SetDirty(strip); n++; }
            return n;
        }

        // ═══════════════════════════════════════════════════════════════════
        // ROW PREFAB
        // ═══════════════════════════════════════════════════════════════════
        /// <summary>
        /// แถวหนึ่งบรรทัดตามภาพแบบ — เส้นสีซ้าย · PLAYER n เหนือชื่อ · ค่าชิดขวา
        ///
        /// ออกทาง <see cref="P3RBuilderKit.SavePrefab"/> เสมอ — สำเนาโลคอลของมันคือ
        /// ทางที่ `ResultPartyRow` หลุดจากยามกัน prefab มาแล้วครั้งหนึ่ง
        /// </summary>
        private static GameObject BuildRowPrefab()
        {
            var root = NewRect("PartyMemberRow", null);
            root.sizeDelta = new Vector2(RowW, RowH);

            var bg = root.gameObject.AddComponent<Image>();
            bg.color = RowBg;
            bg.raycastTarget = false;          // แถวนี้ไม่ได้กด — ดูอย่างเดียว

            var accent = NewImage("Accent", root, Color.white);
            var art = accent.rectTransform;
            art.anchorMin = new Vector2(0f, 0f);
            art.anchorMax = new Vector2(0f, 1f);
            art.pivot     = new Vector2(0f, 0.5f);
            art.sizeDelta = new Vector2(4f, 0f);
            art.anchoredPosition = Vector2.zero;

            // **สองบรรทัดต้องอยู่ในกล่องสูง 48 ทั้งคู่**
            //
            // รอบแรกวางชื่อที่ y -22 ซึ่งดันตัวอักษรทะลุขอบล่างของแถวออกไปราว 6px
            // เห็นในภาพเรนเดอร์แต่ไม่มีอะไรฟ้อง เพราะ TMP ไม่ได้ตัดข้อความที่ล้นกล่องพ่อ
            //
            // จัดใหม่ให้สมมาตรรอบกึ่งกลาง: ป้ายเล็กที่ +11 · ชื่อที่ -11
            // แต่ละกล่องสูงครึ่งหนึ่งของระยะนั้น จึงอยู่ในขอบทั้งคู่โดยไม่ต้องเดา
            var label = NewText("PlayerLabel", root, "PLAYER 1", 12f,
                                TextAlignmentOptions.MidlineLeft, LabelCol);
            Box(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(18f, 11f), new Vector2(220f, 16f));
            P3RBuilderKit.MonoStyle(label, 0.20f);

            var name = NewText("NameText", root, "HUNTER", 20f,
                               TextAlignmentOptions.MidlineLeft, NameCol);
            name.fontStyle = FontStyles.Bold;
            Box(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(18f, -11f), new Vector2(220f, 22f));

            var value = NewText("Value", root, "240 / 240", 20f,
                                TextAlignmentOptions.MidlineRight, AliveCol);
            value.fontStyle = FontStyles.Bold;
            Box(value.rectTransform, new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(180f, 24f));
            value.rectTransform.pivot = new Vector2(1f, 0.5f);

            var ui = root.gameObject.AddComponent<PartyMemberRowUI>();
            ui.background  = bg;
            ui.accent      = accent;
            ui.playerLabel = label;
            ui.nameText    = name;
            ui.valueText   = value;

            return P3RBuilderKit.SavePrefab(root.gameObject, RowPrefab);
        }

        // ═══════════════════════════════════════════════════════════════════
        // HELPERS — โลคอลโดยตั้งใจ ตัวสร้างจอทุกตัวในโปรเจกต์ทำแบบนี้
        // ═══════════════════════════════════════════════════════════════════
        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, Transform parent, Color c)
        {
            var img = NewRect(name, parent).gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string text,
                                               float size, TextAlignmentOptions align, Color c)
        {
            var t = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = align; t.color = c;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        /// <summary>วางมุมซ้ายล่างของจอ — anchor (0,0) pivot (0,0) เพื่อให้ตัวเลขอ่านตรงกับซีน</summary>
        private static void Corner(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        private static void Box(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        private static GameObject Find(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            return null;
        }
    }
}
