using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// แผงซ้ายของ Boss Designer — ท่าสำเร็จรูปเรียงตาม "สิ่งที่ผู้เล่นต้องทำ"
    ///
    /// ═══ ทำไมไม่ใช่เมนู New/ชื่อคลาส แบบเดิม ═══
    ///
    /// คนออกแบบคิดว่า "อยากให้ผู้เล่นวิ่งออกจากวง" ไม่ได้คิดว่า "CircleAoEAction + AllPlayers"
    /// เมนูเดิมบังคับให้แปลในหัวก่อนทุกครั้ง · ท่าสำเร็จรูปคือ action ที่ตั้งค่าเข้าท่าไว้แล้ว
    /// เรียงตามกริยาของผู้เล่น
    ///
    /// ═══ ท่าสำเร็จรูปคือไฟล์ธรรมดา ═══
    ///
    /// BossAction asset ใต้ <see cref="PresetsDir"/> · หมวด = ชื่อโฟลเดอร์ย่อย (ตัวเลขนำหน้าใช้เรียงลำดับ
    /// ไม่แสดง) · ใครสร้างท่าดีๆ คลิกขวาที่คลิป "บันทึกเป็นท่าสำเร็จรูป" แผงนี้ก็โตเอง
    ///
    /// ใช้แล้วได้ **สำเนา** ฝังใน config เสมอ (ไม่ชี้ไฟล์ท่าสำเร็จรูปตรงๆ) — แก้ท่าในบอสตัวหนึ่ง
    /// ต้องไม่ไปเปลี่ยนท่าสำเร็จรูปหรือบอสตัวอื่น
    /// </summary>
    public class BossPalette : VisualElement
    {
        public const string PresetsDir = "Assets/ScriptableObjects/BossAction/Presets";
        public const string DragKey    = "CloneSwarm.BossPreset";

        readonly System.Action<BossAction> _onPick;
        readonly ScrollView _list = new ScrollView();

        public BossPalette(System.Action<BossAction> onPick)
        {
            _onPick = onPick;
            style.width = Mathf.Round(150 * Mathf.Max(1f, BossDesignerWindow.FontScale));
            style.flexShrink = 0;
            style.borderRightWidth = 1;
            style.borderRightColor = new Color(1f, 1f, 1f, 0.06f);
            style.paddingLeft = 4;
            style.paddingRight = 4;

            var title = new Label("ท่าสำเร็จรูป");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginTop = 4;
            Add(title);

            var hint = new Label("คลิก = วางที่ playhead · ลากลงเลนได้");
            hint.style.fontSize = BossDesignerWindow.Fs(9);
            hint.style.opacity = 0.55f;
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.marginBottom = 4;
            Add(hint);

            _list.style.flexGrow = 1;
            Add(_list);
            Refresh();
        }

        public static bool IsPreset(Object o)
        {
            string p = AssetDatabase.GetAssetPath(o);
            return !string.IsNullOrEmpty(p) && p.Replace('\\', '/').StartsWith(PresetsDir + "/");
        }

        /// <summary>หมวดจากชื่อโฟลเดอร์ย่อย · "1 หลบออก" → "หลบออก"</summary>
        static string CategoryOf(string path)
        {
            string rel = path.Replace('\\', '/').Substring(PresetsDir.Length + 1);
            int slash = rel.IndexOf('/');
            if (slash < 0) return "อื่นๆ";
            string folder = rel.Substring(0, slash);
            int sp = folder.IndexOf(' ');
            return sp > 0 && char.IsDigit(folder[0]) ? folder.Substring(sp + 1) : folder;
        }

        /// <summary>
        /// ท่าสำเร็จรูปทั้งหมดเรียงตามหมวด — แผงนี้กับเมนู "+" ของเลนใช้รายการเดียวกัน
        /// (เดิมเมนู "+" เรียงตามชื่อคลาส เลยมีสองคลังที่หน้าตาไม่เหมือนกัน)
        /// </summary>
        public static IEnumerable<(string category, BossAction preset)> AllPresets()
        {
            if (!AssetDatabase.IsValidFolder(PresetsDir)) yield break;
            foreach (var p in AssetDatabase.FindAssets("t:BossAction", new[] { PresetsDir })
                         .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p))
                if (AssetDatabase.LoadMainAssetAtPath(p) is BossAction a) yield return (CategoryOf(p), a);
        }

        public void Refresh()
        {
            _list.Clear();
            if (!AssetDatabase.IsValidFolder(PresetsDir))
            {
                _list.Add(Note("ยังไม่มีท่าสำเร็จรูป — Tools > Clone Swarm > Boss > Create Default Presets"));
                return;
            }

            string current = null;
            foreach (var (cat, action) in AllPresets())
            {
                if (cat != current)
                {
                    current = cat;
                    var h = new Label("▸ " + cat);
                    h.style.unityFontStyleAndWeight = FontStyle.Bold;
                    h.style.fontSize = BossDesignerWindow.Fs(11);
                    h.style.marginTop = 6;
                    h.style.opacity = 0.85f;
                    _list.Add(h);
                }
                _list.Add(Item(action));
            }
        }

        VisualElement Item(BossAction preset)
        {
            var el = new Label(preset.name);
            el.tooltip = $"{preset.GetType().Name}\nคลิก = วางที่ playhead · ลากลงเลนเพื่อวางตรงเวลาที่ปล่อย";
            el.style.paddingLeft = 10;
            el.style.paddingTop = el.style.paddingBottom = 2;
            el.style.borderTopLeftRadius = el.style.borderTopRightRadius = 3;
            el.style.borderBottomLeftRadius = el.style.borderBottomRightRadius = 3;
            el.style.backgroundColor = new Color(1f, 1f, 1f, 0.03f);
            el.style.marginBottom = 1;
            el.style.whiteSpace = WhiteSpace.Normal;

            Color accent = BossDesignerWindow.ClipColorOf(preset);
            el.style.borderLeftWidth = 3;
            el.style.borderLeftColor = accent;

            el.RegisterCallback<MouseEnterEvent>(_ => el.style.backgroundColor = new Color(1f, 1f, 1f, 0.10f));
            el.RegisterCallback<MouseLeaveEvent>(_ => el.style.backgroundColor = new Color(1f, 1f, 1f, 0.03f));

            // คลิกเฉยๆ = วาง · กดแล้วลากเกิน 4px = เริ่มลาก (DragAndDrop ของ Unity ให้เลนรับได้)
            Vector2 down = default;
            bool pressed = false, dragging = false;
            el.RegisterCallback<MouseDownEvent>(e => { if (e.button == 0) { pressed = true; dragging = false; down = e.mousePosition; } });
            el.RegisterCallback<MouseMoveEvent>(e =>
            {
                if (!pressed || dragging || (e.mousePosition - down).sqrMagnitude < 16f) return;
                dragging = true;
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new Object[] { preset };
                DragAndDrop.SetGenericData(DragKey, true);
                DragAndDrop.StartDrag(preset.name);
            });
            el.RegisterCallback<MouseUpEvent>(e =>
            {
                if (pressed && !dragging && e.button == 0) _onPick?.Invoke(preset);
                pressed = false;
            });
            return el;
        }

        /// <summary>บันทึก action เป็นท่าสำเร็จรูปใหม่ — ให้เลือกหมวด (โฟลเดอร์) และชื่อเอง</summary>
        public static bool SaveAsPreset(BossAction action)
        {
            if (action == null) return false;
            if (!AssetDatabase.IsValidFolder(PresetsDir)) CreateFolderDeep(PresetsDir);

            string path = EditorUtility.SaveFilePanelInProject(
                "บันทึกเป็นท่าสำเร็จรูป", action.name, "asset",
                "เลือกโฟลเดอร์หมวด (เช่น 1 หลบออก) ใต้ Presets แล้วตั้งชื่อท่า", PresetsDir);
            if (string.IsNullOrEmpty(path)) return false;
            if (!path.Replace('\\', '/').StartsWith(PresetsDir + "/"))
            {
                EditorUtility.DisplayDialog("บันทึกเป็นท่าสำเร็จรูป",
                    $"ต้องเก็บไว้ใต้ {PresetsDir} ไม่งั้นแผงท่าสำเร็จรูปจะมองไม่เห็น", "OK");
                return false;
            }

            var copy = Object.Instantiate(action);
            copy.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(copy, path);
            AssetDatabase.SaveAssets();
            return true;
        }

        public static void CreateFolderDeep(string path)
        {
            string parent = "Assets";
            foreach (var part in path.Split('/').Skip(1))
            {
                string next = parent + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, part);
                parent = next;
            }
        }

        static Label Note(string text)
        {
            var l = new Label(text);
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.fontSize = BossDesignerWindow.Fs(10);
            l.style.opacity = 0.6f;
            return l;
        }
    }
}
