using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// ลดข้อมูลเครือข่ายของ prefab — รันซ้ำได้ ค่าที่ตั้งแล้วไม่แตะ
    ///
    /// ═══ 1. ศัตรูปกติ (ทุก prefab ที่ WaveConfig อ้างถึง) ═══
    ///
    /// เดิม NetworkTransform ของศัตรูทุกตัว sync ตำแหน่ง 3 แกน · หมุน 3 แกน · scale 3 แกน ความละเอียดเต็ม
    /// ทั้งที่ศัตรูเดินบนพื้นราบ หันแค่แกน Y และไม่เคยเปลี่ยนขนาด · ตั้งให้เหลือ
    ///   ตำแหน่ง X/Z · หมุน Y · ไม่ส่ง scale · half float
    /// ตามแนวที่เอกสาร NGO แนะนำ (ปิดแกนที่ไม่เปลี่ยน + half precision) และแบบ LoL Swarm
    /// ที่ "ส่งข้อมูลให้ client น้อยลงและเลือกส่ง"
    ///
    /// ข้อสมมติ: ศัตรูไม่ขยับแกน Y บน host · ถ้าวันหนึ่งมีศัตรูกระโดด/บิน ต้องเปิด SyncPositionY คืนให้ตัวนั้น
    ///
    /// บอสไม่แตะ — จำนวนน้อย และบางตัวอาจมีท่าที่ใช้ scale/แกนอื่น
    ///
    /// ═══ 2. VFX ที่เล่นบนเครื่องตัวเองจาก pool ═══
    ///
    /// ต้องไม่มี NetworkObject / NetworkTransform ที่ไหนเลยในลำดับชั้น · commit a9550015/c77a3c0c
    /// ล้างแค่ตัวราก — VFX_Beam_ThunderRail ยังมีซ้อนอยู่ในลูกสองตัว (NGO ไม่รองรับ NetworkObject ซ้อน)
    ///
    /// batchmode (Editor ต้องปิด):
    ///   Unity.exe -quit -batchmode -nographics -projectPath "…" \
    ///     -executeMethod CloneSwarm.EditorTools.NetworkPrefabTuning.RunBatch -logFile "&lt;absolute&gt;/net.log"
    /// </summary>
    public static class NetworkPrefabTuning
    {
        const string VfxDir = "Assets/Prefab/VFX";

        [MenuItem("Tools/Clone Swarm/Network/Tune Enemy + VFX Prefabs")]
        static void RunFromMenu()
        {
            var log = Run();
            EditorUtility.DisplayDialog("Network Prefab Tuning", string.Join("\n", log), "OK");
        }

        public static void RunBatch()
        {
            try
            {
                foreach (var line in Run()) Debug.Log("[NetworkPrefabTuning] " + line);
                Debug.Log("[NetworkPrefabTuning] DONE");
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[NetworkPrefabTuning] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        static List<string> Run()
        {
            var log = new List<string>();
            foreach (var path in EnemyPrefabPaths()) TuneEnemy(path, log);
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { VfxDir })
                                              .Select(AssetDatabase.GUIDToAssetPath))
                StripNetworkFromVfx(path, log);
            AssetDatabase.SaveAssets();
            return log;
        }

        /// <summary>ทุก prefab ที่ WaveConfig สุ่มออกได้ — คือศัตรูปกติทั้งหมดที่ EnemySpawner ปล่อย</summary>
        static IEnumerable<string> EnemyPrefabPaths()
        {
            var paths = new SortedSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:WaveConfig"))
            {
                string wavePath = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var dep in AssetDatabase.GetDependencies(wavePath, false))
                    if (dep.EndsWith(".prefab")) paths.Add(dep);
            }
            return paths;
        }

        static void TuneEnemy(string path, List<string> log)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var nt = root.GetComponent<NetworkTransform>();
                if (nt == null) { log.Add($"ข้าม {path} — ไม่มี NetworkTransform ที่ราก"); return; }

                bool already = nt.SyncPositionX && !nt.SyncPositionY && nt.SyncPositionZ
                            && !nt.SyncRotAngleX && nt.SyncRotAngleY && !nt.SyncRotAngleZ
                            && !nt.SyncScaleX && !nt.SyncScaleY && !nt.SyncScaleZ
                            && nt.UseHalfFloatPrecision;
                if (already) { log.Add($"ตั้งแล้ว {path} — ไม่แตะ"); return; }

                nt.SyncPositionX = true;  nt.SyncPositionY = false; nt.SyncPositionZ = true;
                nt.SyncRotAngleX = false; nt.SyncRotAngleY = true;  nt.SyncRotAngleZ = false;
                nt.SyncScaleX    = false; nt.SyncScaleY    = false; nt.SyncScaleZ    = false;
                nt.UseHalfFloatPrecision = true;

                PrefabUtility.SaveAsPrefabAsset(root, path);
                log.Add($"ตั้ง {path} — ตำแหน่ง XZ · หมุน Y · ไม่ส่ง scale · half float");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void StripNetworkFromVfx(string path, List<string> log)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // NetworkTransform ก่อน — มันพึ่ง NetworkObject อยู่ ลบกลับลำดับจะติด RequireComponent
                var nts = root.GetComponentsInChildren<NetworkTransform>(true);
                var nos = root.GetComponentsInChildren<NetworkObject>(true);
                if (nts.Length == 0 && nos.Length == 0) return;

                foreach (var c in nts) Object.DestroyImmediate(c, true);
                foreach (var c in nos) Object.DestroyImmediate(c, true);

                PrefabUtility.SaveAsPrefabAsset(root, path);
                log.Add($"ล้าง {path} — NetworkTransform {nts.Length} · NetworkObject {nos.Length} " +
                        "(VFX เล่นบนเครื่องตัวเองจาก pool ไม่ใช่วัตถุเครือข่าย)");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
