using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// ตัดช่องบน HUD ให้เหลือเท่าเพดานจริง — อาวุธตาม
    /// <see cref="PlayerWeaponManager.MaxWeaponSlots"/> · สเตตัสตาม
    /// <see cref="PlayerStatManager.MaxStatSlots"/>
    /// เมนู: Tools > Clone Swarm > Trim HUD Slots → Max*Slots
    ///
    /// ═══ ทำไมต้องมีตัวนี้ ═══
    ///
    /// `WeaponStatHUD.weaponSlots` ประกาศไว้ว่า `new SlotUI[MaxWeaponSlots]` ก็จริง แต่
    /// **ค่าเริ่มต้นในโค้ดใช้กับ component ที่เพิ่งใส่ใหม่เท่านั้น** — ของที่อยู่ในซีนแล้ว
    /// ถูก serialize ไว้ยาว 6 ช่อง การแก้ค่าคงที่ในโค้ดไม่ย้อนไปแตะซีน
    ///
    /// และ `RefreshWeapons()` วนด้วย `weaponSlots.Length` ไม่ใช่ค่าคงที่ ดังนั้นช่องที่เกิน
    /// จะยังถูกวาดเป็นช่องว่างค้างอยู่บนจอตลอดเกม
    ///
    /// ตัวนี้จึงลบ GameObject ของช่องที่เกินทิ้ง ย่อ array แล้วจัดแถวใหม่ให้อยู่กลางเหมือนเดิม
    ///
    /// ═══ ขอบเขต ═══
    ///
    /// - ลบเฉพาะช่องท้ายแถวที่ index ≥ MaxWeaponSlots — ช่องที่เหลือไม่ถูกแตะ
    /// - หาตัวช่องจากของที่ผูกไว้จริง (bg/icon/name/level) ไม่ได้เดาจากชื่อ object
    /// - แถวที่มี LayoutGroup คุมอยู่ ไม่จัดตำแหน่งเอง ปล่อยให้ Unity จัด
    /// - รันซ้ำได้ รอบสองจะรายงานว่าไม่มีอะไรต้องทำ
    /// </summary>
    public static class P3RWeaponSlotTrimmer
    {
        private static readonly string[] Scenes =
        {
            "Assets/GameScenes/Proto_GameplayHUD2.unity",
            "Assets/GameScenes/SampleScene.unity",
        };

        [MenuItem("Tools/Clone Swarm/Trim HUD Slots → Max*Slots")]
        public static void Trim()
        {
            int maxW = PlayerWeaponManager.MaxWeaponSlots;
            int maxS = PlayerStatManager.MaxStatSlots;

            if (!Application.isBatchMode &&
                !EditorUtility.DisplayDialog(
                    $"ตัดช่อง HUD ให้เหลือ อาวุธ {maxW} · สเตตัส {maxS}",
                    "จะลบ GameObject ของช่องที่เกินเพดานออกจาก HUD ในซีนเกม\n" +
                    "แล้วจัดแถวใหม่ให้อยู่กลางเหมือนเดิม\n\n" +
                    "ควรมี working tree ที่สะอาดก่อนกด",
                    "ตัด", "ยกเลิก"))
                return;

            var log = new StringBuilder($"[ตัดช่อง HUD] เพดาน อาวุธ {maxW} · สเตตัส {maxS}\n");

            foreach (var path in Scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int changed = 0;

                foreach (var hud in Object.FindObjectsByType<WeaponStatHUD>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    changed += TrimRow(hud, "อาวุธ", hud.weaponSlots, maxW,
                                       a => hud.weaponSlots = a, log);
                    changed += TrimRow(hud, "สเตตัส", hud.statSlots, maxS,
                                       a => hud.statSlots = a, log);
                }

                if (changed > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    log.AppendLine($"  บันทึก {path}");
                }
                else
                {
                    log.AppendLine($"  {path} — ไม่มีอะไรต้องทำ");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        /// <summary>คืนจำนวนช่องที่ถูกลบ · `assign` เขียนอาเรย์ที่ย่อแล้วกลับเข้า field ที่ถูกตัว</summary>
        private static int TrimRow(WeaponStatHUD hud, string what, WeaponStatHUD.SlotUI[] slots,
                                   int max, System.Action<WeaponStatHUD.SlotUI[]> assign,
                                   StringBuilder log)
        {
            if (slots == null || slots.Length <= max) return 0;

            string where = Path(hud.transform);
            log.AppendLine($"  {where} · {what} — {slots.Length} ช่อง → {max}");

            // ── จำแถวไว้ก่อนลบ เพราะพอลบแล้วจะหาที่อยู่ไม่ได้ ─────────────────
            RectTransform strip = null;
            var keptRoots = new List<RectTransform>();
            for (int i = 0; i < max; i++)
            {
                var r = SlotRoot(slots, i);
                if (r == null) continue;
                keptRoots.Add(r);
                if (strip == null) strip = r.parent as RectTransform;
            }

            float oldWidth = strip != null ? strip.rect.width : 0f;

            // ── ลบช่องที่เกิน ──────────────────────────────────────────────
            int removed = 0;
            for (int i = max; i < slots.Length; i++)
            {
                var root = SlotRoot(slots, i);
                if (root == null) continue;
                Object.DestroyImmediate(root.gameObject);
                removed++;
            }

            var trimmed = new WeaponStatHUD.SlotUI[max];
            System.Array.Copy(slots, trimmed, max);
            assign(trimmed);
            EditorUtility.SetDirty(hud);

            Relayout(strip, keptRoots, oldWidth, log);
            log.AppendLine($"    ลบ {removed} ช่อง");
            return removed > 0 ? removed : 1;   // ย่อ array ก็นับว่าเปลี่ยน
        }

        /// <summary>
        /// จัดแถวใหม่หลังตัดช่อง — ย่อความกว้างแถวลงตามจำนวนช่องที่เหลือ
        ///
        /// แถวถูกวาง pivot กลาง ถ้าไม่ย่อความกว้าง ช่องที่เหลือจะค้างเยื้องไปทางซ้าย
        /// ระยะห่างวัดจากของจริงที่อยู่ในซีน ไม่ได้อ่านค่าคงที่ของ builder
        /// (ซีนที่ย้ายมาแล้วอาจถูกมือแก้ระยะไปแล้ว)
        /// </summary>
        private static void Relayout(RectTransform strip, List<RectTransform> kept,
                                     float oldWidth, StringBuilder log)
        {
            if (strip == null || kept.Count == 0) return;

            if (strip.GetComponent<LayoutGroup>() != null)
            {
                log.AppendLine("    แถวมี LayoutGroup — ปล่อยให้ Unity จัดเอง");
                return;
            }

            float slotW = kept[0].rect.width;
            float gap   = kept.Count >= 2
                        ? kept[1].anchoredPosition.x - kept[0].anchoredPosition.x - slotW
                        : 0f;

            float newWidth = kept.Count * slotW + (kept.Count - 1) * gap;
            if (newWidth <= 0f || Mathf.Approximately(newWidth, oldWidth)) return;

            // ป้ายหัวแถว (หรืออะไรก็ตามที่กว้างเท่าแถวพอดี) ต้องหดตาม ไม่งั้นข้อความเยื้อง
            foreach (Transform t in strip)
            {
                var child = t as RectTransform;
                if (child == null || kept.Contains(child)) continue;
                if (!Mathf.Approximately(child.rect.width, oldWidth)) continue;
                child.sizeDelta = new Vector2(newWidth, child.sizeDelta.y);
                EditorUtility.SetDirty(child);
            }

            strip.sizeDelta = new Vector2(newWidth, strip.sizeDelta.y);
            EditorUtility.SetDirty(strip);
            log.AppendLine($"    ย่อแถว {oldWidth:0} → {newWidth:0} px");
        }

        /// <summary>
        /// หา GameObject ของช่องจากของที่ผูกไว้จริง
        ///
        /// ไต่ขึ้นจาก <c>bg</c> ไปเรื่อยๆ จนกว่าจะถึง parent ที่กินช่องอื่นด้วย แล้วถอยหนึ่งขั้น
        /// — นั่นคือรากของช่องนี้ · ไม่เดาจากชื่อ เพราะซีนเก่าไม่ได้ตั้งชื่อ `Slot_n`
        /// </summary>
        private static RectTransform SlotRoot(WeaponStatHUD.SlotUI[] slots, int index)
        {
            var me = slots[index];
            if (me?.bg == null) return null;

            var others = new List<Transform>();
            for (int i = 0; i < slots.Length; i++)
            {
                if (i == index || slots[i]?.bg == null) continue;
                others.Add(slots[i].bg.transform);
            }

            Transform best = me.bg.transform;
            for (Transform p = best.parent; p != null; p = p.parent)
            {
                // ถึง Canvas แล้วแปลว่าไต่เลยตัวแถวไปแล้ว — หยุด กันลบทั้งจอ
                if (p.GetComponent<Canvas>() != null) break;
                if (others.Any(o => o.IsChildOf(p))) break;
                best = p;
            }
            return best as RectTransform ?? me.bg.rectTransform;
        }

        private static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }
    }
}
