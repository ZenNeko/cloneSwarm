using System.Collections.Generic;
using System.Linq;
using PhEngine.ThaiTextCare.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// ซ่อมการวางวรรณยุกต์ไทยในฟอนต์ TMP ทุกตัวที่เกมใช้จริง
    /// เมนู: Tools > Clone Swarm > Fix Thai Fonts
    ///
    /// **อาการ** — วรรณยุกต์ที่ต้องซ้อนบนสระถูกวาดทับสระพอดี กลายเป็นก้อนเดียวจนอ่านไม่ออก
    /// `เพื่อ` เห็นเป็น `เพือ` · `ที่` เห็นเป็น `ที` · `อยู่` เห็นเป็น `อยู`
    /// กลิฟไม่ได้หายไปไหน แค่ไม่ถูกยกขึ้นชั้นที่สอง (ซูมดูจะเห็นขอบซ้อนกันอยู่)
    ///
    /// **ต้นเหตุ** — TMP ไม่ทำ mark-to-mark positioning ให้เอง ต้องมีค่าปรับคู่กลิฟใน
    /// `fontFeatureTable` และตารางนั้นว่างเปล่าอยู่ เพราะฟอนต์ตั้งเป็น Dynamic atlas
    /// (`m_CharacterTable: []`) — กลิฟถูกแรสเตอร์ตอนรัน แต่ค่าปรับตำแหน่งไม่ได้ตามมาด้วย
    ///
    /// **สิ่งที่เครื่องมือนี้ทำ ต่อฟอนต์หนึ่งตัว**
    ///   1. อบตัวอักษร (ละติน + บล็อกไทยทั้งบล็อก + เครื่องหมายที่ UI ใช้) ลง character table
    ///      พร้อม `includeFontFeatures: true` เพื่อดึง GPOS ของฟอนต์เองเข้ามาด้วย
    ///   2. ปิด `m_ClearDynamicDataOnBuild` — ไม่งั้นตอน build ตารางที่อบไว้ถูกล้างทิ้ง
    ///      แล้วอาการกลับมาเฉพาะในบิลด์จริง ซึ่งเป็นบั๊กที่หายากที่สุดแบบหนึ่ง
    ///   3. รัน `ThaiFontDoctor` ทับอีกชั้น — ค่าที่ผู้เขียนแพ็กเกจจูนมาแล้วสำหรับ
    ///      คู่ (สระบน + วรรณยุกต์) ซึ่งครอบกรณีที่ GPOS ของฟอนต์ยังไม่ครบ
    ///
    /// สร้าง doctor asset **แยกต่อฟอนต์** เพราะ `ApplyModifications()` จำคู่ที่ตัวเองใส่ไว้
    /// เพื่อลบทิ้งตอนรันซ้ำ · ใช้ตัวเดียวข้ามหลายฟอนต์แล้วมันจะไปลบคู่ของฟอนต์อื่น
    /// และแต่ละน้ำหนักฟอนต์ก็ควรจูน yPlacement ต่างกันอยู่แล้ว
    ///
    /// รันซ้ำได้เรื่อยๆ ไม่สะสมของเสีย
    /// </summary>
    public static class P3RThaiFontFix
    {
        private const string DoctorDir     = "Assets/ScriptableObjects/UI/ThaiFont";
        private const string ExampleSearch = "ThaiFontDoctor";

        [MenuItem("Tools/Clone Swarm/Fix Thai Fonts")]
        public static void FixAll()
        {
            var fonts = FindFontsUsedByContent();
            if (fonts.Count == 0)
            {
                Debug.LogWarning("[ThaiFont] ไม่พบ TMP_FontAsset ที่ถูกซีนหรือ prefab อ้างถึงเลย");
                return;
            }

            var template = FindDoctorTemplate();
            EnsureFolder();

            var unicodes = BuildCharacterSet();
            int healed = 0;

            foreach (var font in fonts)
            {
                if (!Bake(font, unicodes)) continue;
                ApplyDoctor(font, template);
                healed++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ThaiFont] ซ่อมแล้ว {healed}/{fonts.Count} ฟอนต์ · doctor asset อยู่ที่ {DoctorDir}\n" +
                      "ถ้ายังเห็นวรรณยุกต์ทับสระอยู่ ให้เปิด doctor asset ของฟอนต์นั้นแล้วปรับ yPlacement");
        }

        // ═══════════════════════════════════════════════════════════════════
        /// <summary>
        /// ใส่ <c>ThaiTextNurse</c> ให้ TMP ทุกตัวในทุกซีนที่ "ขึ้นบรรทัดใหม่ได้"
        ///
        /// ภาษาไทยไม่มีช่องว่างระหว่างคำ TMP จึงตัดบรรทัดตรงไหนก็ได้ที่พอดีขอบ
        /// อาการ: `บรรทัด` ถูกผ่าเป็น `บร` + `รทัด` คนละบรรทัด
        /// Nurse แทรกช่องว่างความกว้างศูนย์ตามขอบคำจากพจนานุกรมให้ TMP ตัดถูกที่
        ///
        /// ตัวที่ตั้ง NoWrap ไว้ไม่ต้องใส่ — มันไม่มีวันขึ้นบรรทัดใหม่อยู่แล้ว
        /// การใส่ให้ทุกตัวจะเพิ่ม preprocessor ที่ทำงานเปล่าๆ ทุกครั้งที่ข้อความเปลี่ยน
        /// </summary>
        [MenuItem("Tools/Clone Swarm/Add Thai Word Break To Scenes")]
        public static void AddWordBreakToScenes()
        {
            string opened = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            int totalAdded = 0, scenesTouched = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/GameScenes" }))
            {
                string path  = AssetDatabase.GUIDToAssetPath(guid);
                var    scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                                   path, UnityEditor.SceneManagement.OpenSceneMode.Single);

                int added = 0;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        if (t.textWrappingMode != TextWrappingModes.Normal) continue;
                        if (t.GetComponent<PhEngine.ThaiTextCare.ThaiTextNurse>() != null) continue;
                        t.gameObject.AddComponent<PhEngine.ThaiTextCare.ThaiTextNurse>();
                        added++;
                    }

                if (added > 0)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                    totalAdded += added;
                    scenesTouched++;
                    Debug.Log($"[ThaiFont] {path}: ใส่ ThaiTextNurse {added} ตัว");
                }
            }

            if (!string.IsNullOrEmpty(opened))
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(opened);

            Debug.Log($"[ThaiFont] ตัดคำไทย: ใส่ทั้งหมด {totalAdded} ตัว ใน {scenesTouched} ซีน");
        }

        // ═══════════════════════════════════════════════════════════════════
        /// <summary>
        /// ฟอนต์ที่ "เกมใช้จริง" = ฟอนต์ที่เป็น dependency ของซีนหรือ prefab สักอัน
        /// จงใจไม่ทำเป็นลิสต์ที่พิมพ์มือ — เพิ่มฟอนต์ใหม่แล้วมันจะถูกเก็บเองรอบถัดไป
        /// และไม่ไปอบฟอนต์ 9 น้ำหนักที่ไม่มีใครใช้ให้เปลือง atlas ในรีโป
        /// </summary>
        private static List<TMP_FontAsset> FindFontsUsedByContent()
        {
            var roots = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
                .Concat(AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .ToArray();

            var fonts = new List<TMP_FontAsset>();
            foreach (var dep in AssetDatabase.GetDependencies(roots, true))
            {
                if (!dep.EndsWith(".asset")) continue;
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(dep);
                if (font != null && !fonts.Contains(font)) fonts.Add(font);
            }
            return fonts;
        }

        /// <summary>ละติน + บล็อกไทยทั้งบล็อก + เครื่องหมายที่ UI ของเราใช้จริง</summary>
        private static uint[] BuildCharacterSet()
        {
            var set = new List<uint>();
            for (uint c = 0x0020; c <= 0x007E; c++) set.Add(c);   // ASCII ที่พิมพ์ได้
            for (uint c = 0x0E00; c <= 0x0E7F; c++) set.Add(c);   // ไทยทั้งบล็อก รวมสระและวรรณยุกต์

            // เครื่องหมายที่โผล่ในแบบ — ขาดตัวไหนจะได้สี่เหลี่ยมเปล่าแทน
            foreach (uint c in new uint[]
            {
                0x00A0, 0x00B7, 0x00D7,             //   · ×
                0x2013, 0x2014, 0x2026,             // – — …
                0x2018, 0x2019, 0x201C, 0x201D,     // ‘ ’ “ ”
                0x2022, 0x2192, 0x2713,             // • → ✓
            }) set.Add(c);

            return set.ToArray();
        }

        // ═══════════════════════════════════════════════════════════════════
        private static bool Bake(TMP_FontAsset font, uint[] unicodes)
        {
            if (font.atlasPopulationMode == AtlasPopulationMode.Static)
            {
                // Static แปลว่ามีคนอบไว้แล้ว — TryAddCharacters จะไม่ทำอะไรให้
                Debug.Log($"[ThaiFont] ข้าม {font.name} — เป็น Static atlas อยู่แล้ว");
                return true;
            }

            // includeFontFeatures: true = ดึง GPOS/kerning ของฟอนต์เองเข้า fontFeatureTable ด้วย
            // นี่คือส่วนที่ทำให้ mark positioning ของไทยมีข้อมูลให้ TMP ใช้ตั้งแต่แรก
            bool ok = font.TryAddCharacters(unicodes, out var missing, includeFontFeatures: true);

            if (missing != null && missing.Length > 0)
            {
                var thaiMissing = missing.Where(u => u >= 0x0E00 && u <= 0x0E7F).ToArray();
                if (thaiMissing.Length > 0 && thaiMissing.Length < 40)
                    Debug.LogWarning($"[ThaiFont] {font.name}: ฟอนต์ไม่มีกลิฟไทย {thaiMissing.Length} ตัว " +
                                     $"({string.Join(" ", thaiMissing.Select(u => $"U+{u:X4}"))})");
            }

            // ต้องปิด ไม่งั้นตอน build ตารางที่เพิ่งอบถูกล้างทิ้งทั้งหมด
            // แล้วอาการวรรณยุกต์ทับสระกลับมาเฉพาะในบิลด์ ซึ่งใน Editor จะไม่มีทางเห็น
            var so   = new SerializedObject(font);
            var prop = so.FindProperty("m_ClearDynamicDataOnBuild");
            if (prop != null && prop.boolValue)
            {
                prop.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(font);
            return ok || font.characterTable.Count > 0;
        }

        // ═══════════════════════════════════════════════════════════════════
        private static void ApplyDoctor(TMP_FontAsset font, ThaiFontDoctor template)
        {
            if (template == null) return;

            // ฟอนต์ที่ไม่มีกลิฟไทยเลย (Inter · Jost · LiberationSans) ไม่มีคู่ให้ปรับสักคู่
            // ถ้าไม่กันตรงนี้จะได้ doctor asset เปล่าๆ กองไว้ในรีโปโดยไม่ทำอะไรเลย
            bool hasThai = font.characterTable.Any(c => c.unicode >= 0x0E00 && c.unicode <= 0x0E7F);
            if (!hasThai) return;

            string path   = $"{DoctorDir}/{font.name} Doctor.asset";
            var    doctor = AssetDatabase.LoadAssetAtPath<ThaiFontDoctor>(path);

            if (doctor == null)
            {
                // คัดลอกจากตัวอย่างของแพ็กเกจ เพื่อเอาค่าที่ผู้เขียนจูนมาแล้วมาใช้
                // ไม่พิมพ์ค่าเองเพราะค่าพวกนี้มาจากการดูด้วยตาจริง ไม่ใช่สูตร
                string src = AssetDatabase.GetAssetPath(template);
                if (!AssetDatabase.CopyAsset(src, path))
                {
                    Debug.LogWarning($"[ThaiFont] คัดลอก doctor ไป {path} ไม่สำเร็จ");
                    return;
                }
                AssetDatabase.ImportAsset(path);
                doctor = AssetDatabase.LoadAssetAtPath<ThaiFontDoctor>(path);
            }

            if (doctor == null) return;

            doctor.fontAsset = font;
            doctor.ApplyModifications();
            EditorUtility.SetDirty(doctor);
        }

        private static ThaiFontDoctor FindDoctorTemplate()
        {
            // ตัวอย่างอยู่ในแพ็กเกจ — ต้องค้นใน Packages ด้วย ไม่ใช่แค่ Assets
            foreach (var guid in AssetDatabase.FindAssets($"{ExampleSearch} t:ThaiFontDoctor"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(DoctorDir)) continue;          // ของที่เราสร้างเอง ไม่ใช่ต้นแบบ
                var asset = AssetDatabase.LoadAssetAtPath<ThaiFontDoctor>(path);
                if (asset != null && asset.glyphCombinationList.Count > 0)
                {
                    Debug.Log($"[ThaiFont] ใช้ต้นแบบจาก {path} ({asset.glyphCombinationList.Count} คู่กลิฟ)");
                    return asset;
                }
            }

            Debug.LogWarning("[ThaiFont] ไม่พบ ThaiFontDoctor ตัวอย่างในแพ็กเกจ — " +
                             "จะอบตัวอักษรให้อย่างเดียว ไม่ได้ใส่ค่าปรับตำแหน่งเพิ่ม");
            return null;
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(DoctorDir)) return;
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects/UI"))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "UI");
            AssetDatabase.CreateFolder("Assets/ScriptableObjects/UI", "ThaiFont");
        }
    }
}
