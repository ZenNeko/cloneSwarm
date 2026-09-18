using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// สร้างตัวเกมสำหรับ Windows
    /// เมนู: Tools > Clone Swarm > Build Windows Player
    /// batchmode: -executeMethod CloneSwarm.EditorTools.P3RPlayerBuild.BuildWindows
    ///
    /// ═══ สร้าง Addressables เองก่อนเสมอ ═══
    ///
    /// ตารางแปลทั้งหมดเดินทางผ่าน Addressables · `m_BuildAddressablesWithPlayerBuild: 0`
    /// ในโปรเจกต์นี้แปลว่า "ใช้ค่าจาก preference ของเครื่อง" ซึ่งเก็บใน EditorPrefs
    /// **ไม่ได้อยู่ในรีโป** — เครื่องที่ปิดค่านั้นไว้จะได้ตัวเกมที่ไม่มีข้อความสักตัว
    /// และไม่มีอะไรตอนสร้างบอกว่าขาด
    ///
    /// สร้างเองตรงนี้จึงตัดความขึ้นกับเครื่องออกทั้งหมด · ช้ากว่านิดหน่อยแต่ผลลัพธ์
    /// เหมือนกันทุกเครื่อง ซึ่งเป็นสิ่งที่ build ต้องเป็น
    ///
    /// ═══ ซีนมาจาก Build Settings ไม่ใช่รายชื่อในไฟล์นี้ ═══
    ///
    /// รายชื่อที่เขียนไว้ในสคริปต์จะกลายเป็นนิยามที่สองของ "เกมมีซีนอะไรบ้าง"
    /// แล้ววันหนึ่งจะไม่ตรงกับที่เห็นใน Build Settings โดยไม่มีใครรู้
    /// </summary>
    public static class P3RPlayerBuild
    {
        private const string OutDir  = "Builds/Windows";
        private const string ExeName = "CloneSwarm.exe";

        [MenuItem("Tools/Clone Swarm/Build Windows Player")]
        public static void BuildWindows()
        {
            // ── 1. Addressables ก่อน ──────────────────────────────────────
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[Build] ไม่เจอ AddressableAssetSettings — " +
                               "ตารางแปลจะไม่ติดไปกับตัวเกม");
                return;
            }

            AddressableAssetSettings.BuildPlayerContent(out var addrResult);
            if (!string.IsNullOrEmpty(addrResult.Error))
            {
                Debug.LogError($"[Build] Addressables ล้มเหลว: {addrResult.Error}");
                return;
            }

            Debug.Log($"[Build] Addressables เสร็จ — {addrResult.FileRegistry.GetFilePaths().Count()} ไฟล์ " +
                      $"({addrResult.Duration:F1}s)");

            // ── 2. ตัวเกม ─────────────────────────────────────────────────
            var scenes = EditorBuildSettings.scenes
                                            .Where(s => s.enabled)
                                            .Select(s => s.path)
                                            .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[Build] ไม่มีซีนที่เปิดไว้ใน Build Settings");
                return;
            }

            Debug.Log($"[Build] ซีนที่จะใส่ไป {scenes.Length} ซีน: {string.Join(" · ", scenes.Select(Path.GetFileNameWithoutExtension))}");

            string fullOut = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutDir));
            Directory.CreateDirectory(fullOut);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes           = scenes,
                locationPathName = Path.Combine(fullOut, ExeName),
                target           = BuildTarget.StandaloneWindows64,
                options          = BuildOptions.None,
            });

            var sum = report.summary;

            if (sum.result != BuildResult.Succeeded)
            {
                // ไล่ทุก error ออกมา ไม่ใช่แค่บอกว่าล้มเหลว — ตัวเลขข้อผิดพลาดอย่างเดียว
                // ทำให้ต้องไปเปิด log ยาวหาเอง ซึ่งเป็นงานที่เครื่องมือควรทำให้
                foreach (var step in report.steps)
                    foreach (var msg in step.messages)
                        if (msg.type is LogType.Error or LogType.Exception)
                            Debug.LogError($"[Build] {step.name}: {msg.content}");

                Debug.LogError($"[Build] ❌ {sum.result} — error {sum.totalErrors} · warning {sum.totalWarnings}");
                return;
            }

            Debug.Log($"[Build] ✅ สำเร็จ — {sum.totalSize / 1024f / 1024f:F1} MB · " +
                      $"ใช้เวลา {sum.totalTime.TotalMinutes:F1} นาที · warning {sum.totalWarnings}\n" +
                      $"       {Path.Combine(fullOut, ExeName)}");
        }
    }
}
