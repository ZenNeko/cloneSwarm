using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
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

        /// <summary>
        /// แคป HUD ในซีนเกมจริง — ตัวเดียวที่ไม่ใช่ซีน `Proto_*`
        ///
        /// **ครอบแค่สีกับฟอนต์** ซึ่งพอดีกับสิ่งที่ `P3RGameplayHudRestyler` แก้
        /// ค่าที่เห็นบนแถบ/ตัวเลขเป็นค่าที่ค้างอยู่ในซีน ไม่ใช่ค่าจริงตอนเล่น เพราะ
        /// เปิดแบบ edit mode — HP จริง · คูลดาวน์ · ไอคอนสกิล ต้องมีผู้เล่น spawn ก่อน
        /// จอนี้จึงยืนยัน "สีกับฟอนต์ลงถูกไหม" ได้ แต่ยืนยัน **5-second test ไม่ได้**
        /// </summary>
        [MenuItem("Tools/Clone Swarm/Capture Gameplay HUD (SampleScene)")]
        public static void CaptureGameplayHud()
        {
            const string scenePath = "Assets/GameScenes/SampleScene.unity";
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Screenshots"));
            Directory.CreateDirectory(outDir);
            string png = Path.Combine(outDir, "SampleScene_HUD.png");

            try
            {
                // จอที่ถือ singleton **ต้องเปิดค้างไว้ในซีน** ไม่งั้น Awake ไม่วิ่งแล้ว
                // Instance เป็น null · ตอนเล่นจริงมันซ่อนตัวเองใน Awake/Start แต่ edit mode
                // ไม่มี Awake ทั้งสามจอจึงซ้อนทับ HUD จนมองไม่เห็นอะไรเลย
                // ปิดให้เฉพาะตอนแคป — Capture ไม่เซฟซีนอยู่แล้ว สถานะในไฟล์จึงไม่ถูกแตะ
                // `P3R_Loading` อยู่ในลิสต์ด้วยเหตุผลเดียวกันแต่คนละกลไก — ม่านรอผู้เล่น
                // ถูกเปิดค้างในซีนแล้วให้ `GameplayLoadingGate` ปิดตอนรัน · edit mode
                // ไม่มีใครปิดให้ มันจึงทึบเต็มจอจนภาพออกมาเป็นหน้า NOW LOADING ล้วน
                Capture(scenePath, png,
                        hideObjects: new[] { "P3R_LevelUp", "P3R_Pause", "P3R_WinLose", "P3R_Loading" });
                Debug.Log($"[Shot] {png}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Shot] {scenePath} ล้มเหลว: {e}");
            }
        }

        /// <summary>
        /// แคปซีนเกมตอน **จอ Level Up เปิดอยู่** — ไว้พิสูจน์ลำดับการวาด
        ///
        /// จอ Level Up ปูแผ่นทึบ 98% เต็มจอ · แถบ `BuildStrip_Shared` ต้องอยู่ **เหนือ**
        /// แผ่นนั้นเพราะเป็นลูกของ `HUDCanvas` ลำดับหลัง `P3R_LevelUp`
        /// ถ้าภาพออกมาไม่เห็นแถบ แปลว่าลำดับพี่น้องผิด ไม่ใช่เรื่องสี
        ///
        /// ยังต้องปิด `P3R_Pause` · `P3R_WinLose` · `P3R_Loading` — edit mode ไม่มี Awake
        /// ให้จอพวกนั้นซ่อนตัวเอง มันจึงทับทุกอย่างจนไม่เหลืออะไรให้ดู
        ///
        /// **สิ่งที่ภาพนี้ยืนยันไม่ได้**: อะไรก็ตามที่เกิดตอน `Show()` — นาฬิกาที่ซ่อน
        /// จนกว่าจะมี tick แรกจะยังโผล่อยู่ในภาพ เพราะ edit mode ไม่มีใครเรียก `Show()`
        /// เรื่องนั้นเป็นของสโมกเทสต์ ไม่ใช่ของภาพ
        /// </summary>
        [MenuItem("Tools/Clone Swarm/Capture Gameplay HUD + Level Up")]
        public static void CaptureGameplayHudWithLevelUp()
        {
            const string scenePath = "Assets/GameScenes/SampleScene.unity";
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Screenshots"));
            Directory.CreateDirectory(outDir);
            string png = Path.Combine(outDir, "SampleScene_HUD_LevelUp.png");

            try
            {
                Capture(scenePath, png,
                        hideObjects: new[] { "P3R_Pause", "P3R_WinLose", "P3R_Loading" });
                Debug.Log($"[Shot] {png}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Shot] {scenePath} ล้มเหลว: {e}");
            }
        }

        /// <summary>
        /// แคป HUD พร้อม **ของจำลอง** ในแถวปาร์ตี้กับช่อง augment
        ///
        /// สองแผงนี้สร้างลูกตอนรันเท่านั้น (ต้องมีผู้เล่น spawn + PlayerAugmentManager)
        /// edit mode จึงเห็นเป็นกล่องเปล่า · ใส่ของจำลองให้ดูว่า **จัดวางพอดีไหม**
        /// ซึ่งเป็นสิ่งเดียวที่ภาพตอบได้ — ค่าจริงถูกไหมเป็นเรื่องของสโมกเทสต์กับสองเครื่อง
        ///
        /// ซีนไม่ถูกเซฟ ของจำลองหายไปพร้อมรอบนี้
        /// </summary>
        [MenuItem("Tools/Clone Swarm/Capture Gameplay HUD (party preview)")]
        public static void CaptureGameplayHudParty()
        {
            const string scenePath = "Assets/GameScenes/SampleScene.unity";
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Screenshots"));
            Directory.CreateDirectory(outDir);
            string png = Path.Combine(outDir, "SampleScene_HUD_Party.png");

            try
            {
                Capture(scenePath, png,
                        hideObjects: new[] { "P3R_LevelUp", "P3R_Pause", "P3R_WinLose", "P3R_Loading" },
                        populate: PopulatePartySamples);
                Debug.Log($"[Shot] {png}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Shot] {scenePath} ล้มเหลว: {e}");
            }
        }

        /// <summary>ใส่สองแถว (เป็น / ล้ม) กับสามช่อง augment ตามภาพแบบ</summary>
        private static void PopulatePartySamples(Scene scene)
        {
            var party = FindByName(scene, "PartyPanel")?.GetComponent<CloneSwarm.UI.P3R.PartyMemberHUD>();
            if (party != null && party.rowTemplate != null)
            {
                var parent = party.rowArea != null ? party.rowArea : (RectTransform)party.transform;

                var a = Object.Instantiate(party.rowTemplate, parent);
                a.name = "Sample_Row0";
                a.SetIdentity(1, "HUNTER", new Color32(0x3B, 0x6B, 0xFF, 0xFF));
                a.SetAlive(240f, 240f);
                ((RectTransform)a.transform).anchoredPosition = new Vector2(0f, party.rowHeight + party.rowGap);

                var b = Object.Instantiate(party.rowTemplate, parent);
                b.name = "Sample_Row1";
                b.SetIdentity(2, "GUNNER", new Color32(0xE8, 0x3A, 0x3A, 0xFF));
                b.SetDowned(6f);
                ((RectTransform)b.transform).anchoredPosition = Vector2.zero;
            }

            var strip = FindByName(scene, "AugmentStrip")?.GetComponent<CloneSwarm.UI.P3R.AugmentStripUI>();
            if (strip != null && strip.slotTemplate != null)
            {
                var parent = strip.slotArea != null ? strip.slotArea : (RectTransform)strip.transform;
                string[] abbrev = { "GLA", "BLO", "" };

                for (int i = 0; i < strip.slotCount; i++)
                {
                    var slot = Object.Instantiate(strip.slotTemplate, parent);
                    slot.name = $"Sample_Aug{i}";
                    slot.gameObject.SetActive(true);

                    var rt = (RectTransform)slot.transform;
                    rt.sizeDelta = new Vector2(strip.slotSize, strip.slotSize);
                    rt.anchoredPosition = new Vector2(i * (strip.slotSize + strip.slotGap), 0f);

                    if (string.IsNullOrEmpty(abbrev[i])) { slot.ShowEmpty(strip.borderColor); continue; }

                    // level = 0 และสีเดียวทุกช่อง — augment ไม่มีระดับและไม่มีเลเวลแล้ว
                    // ของจำลองต้องสะท้อนกติกาจริง ไม่งั้นภาพจะโชว์สิ่งที่เกมทำไม่ได้
                    slot.ShowEntry(
                        new CloneSwarm.UI.P3R.BuildStripUI.Entry { abbrev = abbrev[i], level = 0 },
                        strip.augmentColor, strip.borderColor, strip.augmentColor);
                }
            }
        }

        private static GameObject FindByName(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            return null;
        }

        /// <param name="populate">
        /// เรียกหลังเปิดซีนแต่ก่อนเรนเดอร์ — ใส่ของจำลองให้แผงที่ตอน edit mode ยังว่าง
        ///
        /// **ปลอดภัยเพราะ Capture ไม่เคยเซฟซีน** ของที่ใส่อยู่แค่ในหน่วยความจำรอบนี้
        /// จำเป็นสำหรับแผงที่สร้างลูกตอนรัน (แถวปาร์ตี้ · ช่อง augment) ซึ่งถ้าไม่ใส่
        /// ภาพจะออกมาเป็นกล่องเปล่าสองใบ แล้วอ่านไม่ออกว่าจัดวางพอดีไหม
        /// </param>
        private static void Capture(string scenePath, string pngPath, string[] hideObjects = null,
                                    System.Action<Scene> populate = null)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            if (hideObjects != null)
            {
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        if (System.Array.IndexOf(hideObjects, t.name) >= 0)
                            t.gameObject.SetActive(false);
            }

            populate?.Invoke(scene);

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
