using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// เรนเดอร์ซีนต้นแบบออกเป็น PNG เพื่อเอาไปเทียบกับ mockup โดยไม่ต้องเปิด Unity เอง
    ///
    /// **ปัญหาหลักที่ต้องแก้: Screen Space Overlay เรนเดอร์ลง RenderTexture ไม่ได้**
    /// Canvas โหมด Overlay ถูกวาดตอนจบเฟรมโดยข้ามกล้องทั้งหมด `Camera.Render()` จึงได้จอเปล่า
    /// วิธีแก้: สลับ Canvas เป็น `ScreenSpaceCamera` ชั่วคราวแล้วผูกกับกล้องที่ยิงลง RenderTexture
    /// ซีนไม่ถูกเซฟ การสลับจึงหายไปเองเมื่อจบ
    ///
    /// **ต้องรันแบบมี graphics device** — `-batchmode` เฉยๆ ใช้ได้ แต่ `-nographics` ไม่ได้
    /// เพราะไม่มี device ให้สร้าง RenderTexture
    ///
    /// เมนู: Tools > Clone Swarm > Capture P3R Proto Screens
    /// batchmode: -executeMethod CloneSwarm.EditorTools.P3RScreenshotTool.CaptureAll
    /// ไฟล์ออกที่ &lt;project&gt;/Screenshots/ (นอก Assets จึงไม่ถูก import เป็น asset)
    /// </summary>
    public static class P3RScreenshotTool
    {
        private const int W = 1920, H = 1080;

        /// <summary>
        /// หาซีนต้นแบบเองจากชื่อ — เดิมเป็นลิสต์ที่พิมพ์มือ แล้วจอใหม่ก็เงียบหายไปจากชุดภาพ
        /// โดยไม่มีอะไรฟ้อง เพราะ "ไม่มีในลิสต์" กับ "เรนเดอร์แล้วไม่มีอะไร" หน้าตาเหมือนกัน
        /// </summary>
        private static string[] FindProtoScenes()
        {
            var found = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Scene Proto_", new[] { "Assets/GameScenes" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                // FindAssets จับชื่อแบบ fuzzy — คัดเฉพาะที่ขึ้นต้นด้วย Proto_ จริงๆ
                if (Path.GetFileName(path).StartsWith("Proto_")) found.Add(path);
            }
            found.Sort(System.StringComparer.Ordinal);
            return found.ToArray();
        }

        [MenuItem("Tools/Clone Swarm/Capture P3R Proto Screens")]
        public static void CaptureAll()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Screenshots"));
            Directory.CreateDirectory(outDir);

            var scenes = FindProtoScenes();
            if (scenes.Length == 0)
                Debug.LogWarning("[Shot] ไม่พบซีน Proto_* ใน Assets/GameScenes เลย");

            foreach (var scenePath in scenes)
            {
                if (!File.Exists(scenePath))
                {
                    Debug.LogWarning($"[Shot] ข้าม {scenePath} — ยังไม่มีซีนนี้");
                    continue;
                }

                string png = Path.Combine(outDir, Path.GetFileNameWithoutExtension(scenePath) + ".png");
                try
                {
                    Capture(scenePath, png);
                    Debug.Log($"[Shot] {png}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[Shot] {scenePath} ล้มเหลว: {e}");
                }
            }

            Debug.Log($"[Shot] เสร็จ · ไฟล์อยู่ที่ {outDir}");
        }

        private static void Capture(string scenePath, string pngPath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // ── เก็บ Canvas ทุกตัวในซีน (รวมที่ปิดอยู่ เผื่อ panel ถูกปิดไว้) ──
            var canvases = new List<Canvas>();
            foreach (var root in scene.GetRootGameObjects())
                canvases.AddRange(root.GetComponentsInChildren<Canvas>(true));

            if (canvases.Count == 0)
            {
                Debug.LogWarning($"[Shot] {scenePath} ไม่มี Canvas");
                return;
            }

            // สีพื้น: ใช้ของกล้องเดิมในซีน เพื่อให้จอที่มีฉากทับโปร่งแสงยังอ่านออก
            Color clear = new Color32(0x0A, 0x0E, 0x1E, 0xFF);
            foreach (var root in scene.GetRootGameObjects())
            {
                var existing = root.GetComponentInChildren<Camera>(true);
                if (existing == null) continue;
                clear = existing.backgroundColor;
                clear.a = 1f;                      // PNG ต้องทึบ ไม่งั้นได้ภาพโปร่งดูไม่รู้เรื่อง
                existing.gameObject.SetActive(false);   // กันไม่ให้มันเรนเดอร์ทับ
                break;
            }

            var camGO = new GameObject("~CaptureCam");
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic     = true;
            cam.orthographicSize = H * 0.5f;
            cam.nearClipPlane    = 0.1f;
            cam.farClipPlane     = 1000f;
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.backgroundColor  = clear;
            cam.cullingMask      = ~0;
            camGO.transform.position = new Vector3(0f, 0f, -100f);

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 8            // ตัวหนังสือใหญ่ๆ ขอบจะแตกถ้าไม่ทำ
            };
            cam.targetTexture = rt;

            // ── สลับ Overlay → ScreenSpaceCamera ────────────────────────────
            foreach (var c in canvases)
            {
                if (c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                c.renderMode    = RenderMode.ScreenSpaceCamera;
                c.worldCamera   = cam;
                c.planeDistance = 100f;

                // CanvasScaler อ้างอิงขนาดจอจริง ซึ่งใน batchmode ไม่ตรงกับ RT ที่เราเรนเดอร์
                // ล็อกเป็น ConstantPixelSize เพื่อให้ 1 หน่วยใน builder = 1 px ใน PNG เป๊ะ
                var scaler = c.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                    scaler.scaleFactor = 1f;
                }
            }

            // TMP คำนวณ mesh แบบ lazy — ไม่บังคับก่อน จะได้ภาพที่ตัวหนังสือหายหรือค้างเฟรมเก่า
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                    t.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);

            Canvas.ForceUpdateCanvases();
            foreach (var c in canvases)
                if (c.transform is RectTransform rtf)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rtf);

            cam.Render();

            // ── อ่านกลับเป็น PNG ────────────────────────────────────────────
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            File.WriteAllBytes(pngPath, tex.EncodeToPNG());

            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGO);
            rt.Release();
            Object.DestroyImmediate(rt);
            // ตั้งใจไม่เซฟซีน — การสลับ renderMode จะได้ไม่ติดไปกับไฟล์
        }
    }
}
