using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// รวมสถานะของผู้เล่นไว้มุมล่างซ้ายมุมเดียว
    /// เมนู: Tools > Clone Swarm > Regroup HUD Bottom-Left
    ///
    /// ═══ ทำอะไร ═══
    ///
    ///   1. ย้าย `ChargeBar_Panel` จากมุมล่างขวา → เหนือ HP ชิดแนวเดียวกับแถบข้อมูล
    ///   2. ย้าย `TabHint` จากมุมล่างขวา → ใต้แถบ passive แนวเดียวกัน
    ///   3. เพิ่มช่องรูปตัวละคร `CharacterIcon_Box` เหนือสแลบเลเวล ชิดขอบซ้ายเดียวกัน
    ///
    /// ═══ ทำไมย้าย ═══
    ///
    /// แถบ passive (CHARGE / KILLS / HITS) เป็นทรัพยากรของ **ตัวผู้เล่น** เหมือนเลือด
    /// กับ EXP ไม่ใช่ของช่องสกิล · อยู่คนละมุมกับพวกเดียวกันแปลว่าต้องกวาดตาข้ามจอ
    /// เพื่ออ่านสภาพตัวเองให้ครบ ทั้งที่เป็นข้อมูลชุดเดียวกันหมด
    ///
    /// ═══ ทำไมไม่ rebuild ซีนใหม่ทั้งอัน ═══
    ///
    /// `P3RGameplayHudV2SceneBuilder` + `P3RGameplayHudV2Migrator` ทำงานเป็นคู่ และ
    /// migrator คัดค่าทั้งชุดไปทับตัวคุมจริงบน `HUDCanvas` · รันใหม่เพื่อขยับสามชิ้น
    /// คือเอาสายอีกหลายสิบเส้นที่ไม่ได้แก้ไปเสี่ยงฟรีๆ (สาขานี้เคยเสียงานถาวรมาแล้วสองครั้ง)
    ///
    /// ตัวเลขตำแหน่งทุกตัวตรงกับ builder เป๊ะ — rebuild เมื่อไรก็ได้ผลเดียวกัน
    ///
    /// ═══ ไม่ลบอะไรเลย ═══
    ///
    /// ChargeBar กับ TabHint ถูก **ย้าย** ไม่ได้สร้างใหม่ · สายที่ `GameHUD` ถืออยู่
    /// (`chargeBarRoot` · `chargeBarFill` · `chargeBarText`) จึงยังชี้ของเดิมทุกเส้น
    ///
    /// รันซ้ำได้ — ตำแหน่งถูกตั้งเป็นค่าสัมบูรณ์ ไม่ใช่ขยับสะสม
    /// </summary>
    public static class P3RHudBottomLeftRegroup
    {
        private static readonly string[] Scenes =
        {
            "Assets/GameScenes/Proto_GameplayHUD2.unity",
            "Assets/GameScenes/SampleScene.unity",
        };

        private const string PanelName = "P3R_HUD";
        private const string IconName  = "CharacterIcon_Box";

        // ตรงกับ P3RGameplayHudV2SceneBuilder — แก้ที่ไหนต้องแก้ทั้งสองที่
        private const float Pad      = 40f;
        private const float BarX     = Pad + 126f;   // แนวซ้ายของแถบ HP/EXP
        private const float IconSize = 108f;
        private const float IconY    = 160f;
        private const float ChargeY  = 160f;
        private const float TabY     = 196f;

        private static readonly Color Chip = new Color32(0x11, 0x18, 0x38, 0xFF);

        [MenuItem("Tools/Clone Swarm/Regroup HUD Bottom-Left")]
        public static void Regroup()
        {
            if (!Application.isBatchMode &&
                !EditorUtility.DisplayDialog(
                    "รวมสถานะผู้เล่นไว้มุมล่างซ้าย",
                    "จะย้าย ChargeBar กับ TabHint มาอยู่กับ HP/EXP\n" +
                    "แล้วเพิ่มช่องรูปตัวละครเหนือสแลบเลเวล\n\n" +
                    "ไม่ลบอะไร · ควรมี working tree ที่สะอาดก่อนกด",
                    "ย้าย", "ยกเลิก"))
                return;

            var log = new StringBuilder("[HUD-ล่างซ้าย] รวมสถานะผู้เล่นไว้มุมเดียว\n");

            foreach (var path in Scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                var panel = FindInScene(scene, PanelName);
                if (panel == null)
                {
                    log.AppendLine($"  {path} — หา {PanelName} ไม่เจอ ข้าม");
                    continue;
                }

                int n = Apply((RectTransform)panel.transform, scene, log);
                if (n == 0) { log.AppendLine($"  {path} — ไม่มีอะไรต้องทำ"); continue; }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                log.AppendLine($"  บันทึก {path} ({n} รายการ)");
            }

            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        private static int Apply(RectTransform panel, Scene scene, StringBuilder log)
        {
            int n = 0;

            n += MoveBottomLeft(panel, "ChargeBar_Panel", BarX, ChargeY, log);
            n += MoveBottomLeft(panel, "TabHint",         BarX, TabY,   log);

            // ป้าย TAB เดิมชิดขวาเพราะเคยอยู่มุมขวา — มาอยู่ซ้ายแล้วต้องชิดซ้าย
            // ไม่งั้นข้อความจะลอยไปกองอยู่ปลายกรอบ ห่างจากแถบที่มันควรเรียงด้วย
            var tab = panel.Find("TabHint")?.GetComponent<TMPro.TextMeshProUGUI>();
            if (tab != null && tab.alignment != TMPro.TextAlignmentOptions.MidlineLeft)
            {
                tab.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
                EditorUtility.SetDirty(tab);
                log.AppendLine("    TabHint → ชิดซ้าย");
                n++;
            }

            n += EnsureCharacterIcon(panel, scene, log);
            return n;
        }

        private static int MoveBottomLeft(RectTransform panel, string name,
                                          float x, float y, StringBuilder log)
        {
            var t = panel.Find(name) as RectTransform;
            if (t == null) { log.AppendLine($"    ไม่เจอ '{name}'"); return 0; }

            var size = t.rect.size;
            t.anchorMin = t.anchorMax = t.pivot = Vector2.zero;   // ล่างซ้าย
            t.sizeDelta = size;
            t.anchoredPosition = new Vector2(x, y);
            EditorUtility.SetDirty(t);

            log.AppendLine($"    ย้าย '{name}' → ล่างซ้าย ({x}, {y})");
            return 1;
        }

        /// <summary>
        /// สร้างช่องรูปตัวละครถ้ายังไม่มี แล้วต่อเข้า <c>GameHUD.characterIcon</c>
        ///
        /// ตัวคุมจริงอยู่บน `HUDCanvas` ไม่ใช่บน panel — ต้องหาจากทั้งซีน
        /// ไม่ใช่ `panel.GetComponent` (ซีนต้นแบบมีตัวขนบน panel ด้วย จึงต่อทั้งสองตัว)
        /// </summary>
        private static int EnsureCharacterIcon(RectTransform panel, Scene scene, StringBuilder log)
        {
            var existing = panel.Find(IconName);
            Image art;

            if (existing != null)
            {
                art = existing.Find("CharacterIcon")?.GetComponent<Image>();
                if (art == null) { log.AppendLine($"    '{IconName}' มีอยู่แต่ไม่มีรูปข้างใน"); return 0; }
            }
            else
            {
                var box = NewImage(IconName, panel, Chip);
                var brt = box.rectTransform;
                brt.anchorMin = brt.anchorMax = brt.pivot = Vector2.zero;
                brt.sizeDelta = new Vector2(IconSize, IconSize);
                brt.anchoredPosition = new Vector2(Pad, IconY);
                Border(brt, new Color(1f, 1f, 1f, 0.18f));

                // **สีขาวล้วนเสมอ ห้ามย้อม** — สี Image คูณเข้ากับพิกเซลของ sprite
                art = NewImage("CharacterIcon", brt, Color.white);
                var art_rt = art.rectTransform;
                art_rt.anchorMin = Vector2.zero;
                art_rt.anchorMax = Vector2.one;
                art_rt.offsetMin = new Vector2(6f, 6f);
                art_rt.offsetMax = new Vector2(-6f, -6f);
                art.preserveAspect = true;

                // ปิดไว้จนกว่า GameHUD จะเจอ local player — Image ที่ไม่มี sprite
                // วาดสี่เหลี่ยมทึบ ไม่ได้วาดเปล่า เปิดค้างไว้ = กล่องขาวทั้งเกม
                art.enabled = false;

                log.AppendLine($"    สร้าง '{IconName}' ({Pad}, {IconY}) ขนาด {IconSize}");
            }

            int wired = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var hud in root.GetComponentsInChildren<GameHUD>(true))
                {
                    if (hud.characterIcon == art) continue;
                    hud.characterIcon = art;
                    EditorUtility.SetDirty(hud);
                    wired++;
                    log.AppendLine($"    ต่อสาย GameHUD.characterIcon บน '{hud.name}'");
                }
            }

            return existing == null || wired > 0 ? 1 + wired : 0;
        }

        // ── helpers ────────────────────────────────────────────────────────
        private static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>กรอบสี่ด้านวาดด้วย Image จริง — `Outline` ใช้ไม่ได้กับพื้น alpha ต่ำ</summary>
        private static void Border(RectTransform target, Color color)
        {
            var root = new GameObject("Border", typeof(RectTransform));
            root.transform.SetParent(target, false);
            var rrt = (RectTransform)root.transform;
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;

            Edge("Top",    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f));
            Edge("Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f));
            Edge("Left",   new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(1f, 0f));
            Edge("Right",  new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(1f, 0f));

            void Edge(string n, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size)
            {
                var img = NewImage(n, rrt, color);
                var rt  = img.rectTransform;
                rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
                rt.sizeDelta = size; rt.anchoredPosition = Vector2.zero;
            }
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            return null;
        }
    }
}
