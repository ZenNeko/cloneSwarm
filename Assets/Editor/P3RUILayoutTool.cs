using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CloneSwarm.UI.P3R;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// ดูดตำแหน่งที่จัดไว้ด้วยมือในซีนกลับมาเก็บ แล้วเอากลับไปทาใหม่ได้
    ///
    /// เมนู:
    ///   Tools > Clone Swarm > UI Layout > 1. ดูดจากซีน   (Capture)
    ///   Tools > Clone Swarm > UI Layout > 2. ทากลับลงซีน (Apply)
    ///
    /// ═══ ลำดับการใช้ ═══
    ///
    ///   จัดใน Editor ให้พอใจ → **ดูด** → commit ไฟล์ asset
    ///   rebuild/migrate เมื่อไรก็ได้ → **ทากลับ** → ได้หน้าตาเดิม
    ///
    /// builder ยังเป็นเจ้าของโครงสร้าง (มีอะไรบ้าง ต่อสายกับใคร) ตัวนี้เป็นเจ้าของตำแหน่ง
    /// ของเฉพาะชิ้นที่ถูกดูดไว้ · ดู <see cref="P3RUILayout"/> สำหรับข้อควรระวัง
    ///
    /// ═══ ทำไมเป็น asset ไม่ใช่การเขียนตัวเลขกลับเข้าไฟล์ C# ═══
    ///
    /// เขียนโค้ดทับโค้ดคือเครื่องมือที่พังเงียบที่สุดแบบหนึ่ง — คอมเมนต์ที่อธิบายว่าทำไม
    /// ตัวเลขเป็นค่านั้นจะหายไปกับการเขียนทับ และ diff จะอ่านไม่ออกว่าอะไรเป็นของคนอะไร
    /// เป็นของเครื่อง · เก็บเป็น asset แยกทำให้ git เห็นชัดว่า "นี่คือของที่คนจัด"
    /// และ builder ยังอ่านรู้เรื่องเหมือนเดิมทุกบรรทัด
    /// </summary>
    public static class P3RUILayoutTool
    {
        private const string AssetPath = "Assets/ScriptableObjects/UI/P3RHudLayout.asset";
        private const string ScenePath = "Assets/GameScenes/SampleScene.unity";
        private const string PanelName = "P3R_HUD";

        /// <summary>
        /// ชั้นที่ลึกกว่านี้ไม่ดูด — ช่องในแถบ build ถูกสร้างตอนรันแล้ววางเองจากโค้ด
        /// เก็บตำแหน่งไว้ก็ไม่มีใครอ่าน แถมหลอกคนอ่านว่าแก้แล้วจะมีผล
        /// </summary>
        private const int DefaultDepth = 3;

        // ═══════════════════════════════════════════════════════════════════
        // 1. ดูด
        // ═══════════════════════════════════════════════════════════════════
        [MenuItem("Tools/Clone Swarm/UI Layout/1. ดูดจากซีน (Capture)")]
        public static void Capture()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var panel = FindInScene(scene, PanelName);
            if (panel == null)
            {
                Debug.LogError($"[UI Layout] หา '{PanelName}' ใน {ScenePath} ไม่เจอ");
                return;
            }

            var entries = new List<P3RUILayout.Entry>();
            Walk(panel.transform, panel.transform, 0, DefaultDepth, entries);

            var asset = AssetDatabase.LoadAssetAtPath<P3RUILayout>(AssetPath);
            bool isNew = asset == null;
            if (isNew)
            {
                asset = ScriptableObject.CreateInstance<P3RUILayout>();
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                AssetDatabase.CreateAsset(asset, AssetPath);
            }

            int before = asset.entries?.Length ?? 0;

            asset.sourceScene   = ScenePath;
            asset.panelName     = PanelName;
            asset.capturedDepth = DefaultDepth;
            asset.capturedAt    = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            asset.entries       = entries.ToArray();

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            Debug.Log($"[UI Layout] ดูด {entries.Count} ชิ้นจาก {PanelName} " +
                      $"(ลึก {DefaultDepth} ชั้น · เดิมมี {before}) → {AssetPath}\n" +
                      (isNew ? "สร้าง asset ใหม่\n" : "") +
                      "ชิ้นที่อยู่ในลิสต์นี้แล้ว **ค่าที่ builder เขียนจะไม่มีผลอีก** — " +
                      "อยากให้ builder คุมกลับ ให้ลบ entry นั้นทิ้ง");
        }

        /// <summary>
        /// เดินต้นไม้เก็บ RectTransform · path เป็นชื่อต่อกันด้วย '/' จากใต้ panel ลงมา
        ///
        /// **ชื่อซ้ำระดับเดียวกันจะชนกัน** — เก็บอันแรกที่เจอแล้วเตือน ดีกว่าเก็บทั้งคู่
        /// แล้วตอน apply ทาผิดตัวแบบไม่มีใครรู้
        /// </summary>
        private static void Walk(Transform root, Transform t, int depth, int maxDepth,
                                 List<P3RUILayout.Entry> into)
        {
            if (depth > 0 && t is RectTransform rt)
            {
                into.Add(new P3RUILayout.Entry
                {
                    path             = PathFrom(root, t),
                    anchorMin        = rt.anchorMin,
                    anchorMax        = rt.anchorMax,
                    pivot            = rt.pivot,
                    anchoredPosition = rt.anchoredPosition,
                    sizeDelta        = rt.sizeDelta,
                    activeSelf       = t.gameObject.activeSelf,
                });
            }

            if (depth >= maxDepth) return;
            foreach (Transform c in t) Walk(root, c, depth + 1, maxDepth, into);
        }

        // ═══════════════════════════════════════════════════════════════════
        // 2. ทากลับ
        // ═══════════════════════════════════════════════════════════════════
        [MenuItem("Tools/Clone Swarm/UI Layout/2. ทากลับลงซีน (Apply)")]
        public static void Apply()
        {
            var asset = AssetDatabase.LoadAssetAtPath<P3RUILayout>(AssetPath);
            if (asset == null)
            {
                Debug.LogError($"[UI Layout] ยังไม่มี {AssetPath} — กด 'ดูดจากซีน' ก่อน");
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var panel = FindInScene(scene, asset.panelName);
            if (panel == null)
            {
                Debug.LogError($"[UI Layout] หา '{asset.panelName}' ใน {ScenePath} ไม่เจอ");
                return;
            }

            var log = new StringBuilder($"[UI Layout] ทา {asset.entries.Length} ชิ้นลง " +
                                        $"{asset.panelName}\n");
            int applied = 0, same = 0;
            var missing = new List<string>();

            foreach (var e in asset.entries)
            {
                var t = panel.transform.Find(e.path) as RectTransform;
                if (t == null) { missing.Add(e.path); continue; }

                if (Matches(t, e)) { same++; continue; }

                t.anchorMin        = e.anchorMin;
                t.anchorMax        = e.anchorMax;
                t.pivot            = e.pivot;
                t.anchoredPosition = e.anchoredPosition;
                t.sizeDelta        = e.sizeDelta;
                if (t.gameObject.activeSelf != e.activeSelf)
                    t.gameObject.SetActive(e.activeSelf);

                EditorUtility.SetDirty(t);
                applied++;
                log.AppendLine($"  ทา {e.path}");
            }

            // หายไปคือสัญญาณจริง ไม่ใช่เรื่องปกติ — แปลว่า builder เปลี่ยนชื่อหรือลบชิ้นนั้น
            // แล้วค่าที่ดูดไว้กลายเป็นของกำพร้า · เงียบไว้ = asset โตขึ้นเรื่อยๆ ด้วยขยะ
            if (missing.Count > 0)
            {
                log.AppendLine($"  **หาไม่เจอ {missing.Count} ชิ้น** — " +
                               "builder เปลี่ยนชื่อ/ลบไปแล้ว ค่าที่ดูดไว้เป็นของกำพร้า:");
                foreach (var m in missing.Take(12)) log.AppendLine($"    · {m}");
                if (missing.Count > 12) log.AppendLine($"    · (อีก {missing.Count - 12})");
            }

            log.AppendLine($"  สรุป: ทา {applied} · ตรงอยู่แล้ว {same} · หาไม่เจอ {missing.Count}");

            if (applied > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                log.AppendLine($"  บันทึก {ScenePath}");
            }

            Debug.Log(log.ToString());
        }

        private static bool Matches(RectTransform t, P3RUILayout.Entry e)
            => t.anchorMin == e.anchorMin
            && t.anchorMax == e.anchorMax
            && t.pivot     == e.pivot
            && t.anchoredPosition == e.anchoredPosition
            && t.sizeDelta == e.sizeDelta
            && t.gameObject.activeSelf == e.activeSelf;

        // ── helpers ────────────────────────────────────────────────────────
        private static string PathFrom(Transform root, Transform t)
        {
            var parts = new List<string>();
            for (var p = t; p != null && p != root; p = p.parent) parts.Add(p.name);
            parts.Reverse();
            return string.Join("/", parts);
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
