using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// ตั้งระบบเสียงของซีนเกมให้ครบในคำสั่งเดียว — แทนขั้นตอนที่ต้องคลิกใน Editor เอง
    ///
    ///   1. LayeredTrack ของ Arena01 จากคลิปที่ SceneBGMPlayer เล่นอยู่ (stem เดียว "main" จนกว่าจะมี stem จริง)
    ///   2. MusicProfile ที่เปิด "main" เต็มตลอด — ฟังเหมือนเดิมทุกอย่าง แต่ระบบซ้อนชั้นพร้อมให้เติม
    ///   3. ผูก profile ลง MapData_Arena01 ระดับ Normal
    ///   4. MusicDirector + ProgressionSfx บน GameObject เดียวกับ SceneBGMPlayer ใน SampleScene
    ///
    /// ═══ รันซ้ำได้ ═══
    ///
    /// ทุกขั้นเช็คก่อนว่ามีแล้วหรือยัง · มีแล้วไม่แตะ — ค่าที่จูนเองไว้ใน asset/component ไม่ถูกเขียนทับ
    ///
    /// batchmode (Editor ต้องปิด):
    ///   Unity.exe -quit -batchmode -nographics -projectPath "…" \
    ///     -executeMethod CloneSwarm.EditorTools.AudioSceneSetup.RunBatch -logFile "&lt;absolute&gt;/audio.log"
    /// </summary>
    public static class AudioSceneSetup
    {
        const string ScenePath   = "Assets/GameScenes/SampleScene.unity";
        const string AudioDir    = "Assets/ScriptableObjects/Audio";
        const string TrackPath   = AudioDir + "/Track_Arena01.asset";
        const string ProfilePath = AudioDir + "/Music_Arena01.asset";
        const string MapPath     = "Assets/ScriptableObjects/Map/MapData_Arena01.asset";
        const string SfxDir      = "Assets/HintsStarsLite/";

        [MenuItem("Tools/Clone Swarm/Audio/Setup Music + Progression SFX")]
        static void RunFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var log = Run();
            EditorUtility.DisplayDialog("Audio Setup", string.Join("\n", log), "OK");
        }

        public static void RunBatch()
        {
            try
            {
                foreach (var line in Run()) Debug.Log("[AudioSceneSetup] " + line);
                Debug.Log("[AudioSceneSetup] DONE");
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[AudioSceneSetup] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        static List<string> Run()
        {
            var log = new List<string>();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var bgm = Object.FindObjectsByType<SceneBGMPlayer>(FindObjectsInactive.Include)
                            .FirstOrDefault();
            if (bgm == null) throw new System.Exception($"ไม่พบ SceneBGMPlayer ใน {ScenePath} — ไม่รู้ว่าจะวาง component ไว้ที่ไหน");
            if (bgm.bgm == null) throw new System.Exception("SceneBGMPlayer ไม่มีคลิป — สร้าง track ไม่ได้");

            // ── 1. track ────────────────────────────────────────────────
            if (!AssetDatabase.IsValidFolder(AudioDir))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Audio");

            var track = AssetDatabase.LoadAssetAtPath<LayeredTrack>(TrackPath);
            if (track == null)
            {
                track = ScriptableObject.CreateInstance<LayeredTrack>();
                track.stems = new[] { new LayeredTrack.Stem { name = "main", clip = bgm.bgm } };
                AssetDatabase.CreateAsset(track, TrackPath);
                log.Add($"สร้าง {TrackPath} — stem 'main' = {bgm.bgm.name}");
            }
            else log.Add($"มีแล้ว {TrackPath} ({track.StemCount} stem) — ไม่แตะ");

            // ── 2. profile ──────────────────────────────────────────────
            var profile = AssetDatabase.LoadAssetAtPath<MusicProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<MusicProfile>();
                profile.track = track;
                profile.timeBands = new[]
                {
                    new MusicProfile.TimeBand
                    {
                        atMinutes = 0f,
                        mix = new StemMix { levels = new[] { new StemLevel { stem = "main", level = 1f } } },
                    },
                };
                AssetDatabase.CreateAsset(profile, ProfilePath);
                log.Add($"สร้าง {ProfilePath} — ช่วงเดียว 'main' เต็ม (ฟังเหมือนเดิม)");
            }
            else log.Add($"มีแล้ว {ProfilePath} — ไม่แตะ");

            // ── 3. MapData ──────────────────────────────────────────────
            var map = AssetDatabase.LoadAssetAtPath<MapData>(MapPath);
            var tier = map != null ? map.GetTier(DifficultyTier.Normal) : null;
            if (tier == null) log.Add($"⚠ ไม่พบ {MapPath} ระดับ Normal — ข้ามการผูก");
            else if (tier.musicProfile == null)
            {
                tier.musicProfile = profile;
                EditorUtility.SetDirty(map);
                log.Add($"ผูก {profile.name} ลง {map.name} / Normal");
            }
            else log.Add($"{map.name} / Normal มี musicProfile แล้ว ({tier.musicProfile.name}) — ไม่แตะ");

            // ── 4. components ในซีน ─────────────────────────────────────
            var host = bgm.gameObject;

            var director = host.GetComponent<MusicDirector>();
            if (director == null)
            {
                director = host.AddComponent<MusicDirector>();
                // เปิด SampleScene ตรงๆ (ไม่ผ่านล็อบบี้) RunSetup.Map ว่าง — ให้ซีนมี profile สำรองไว้
                director.sceneProfile = profile;
                log.Add($"เพิ่ม MusicDirector บน '{host.name}' (sceneProfile = {profile.name})");
            }
            else log.Add($"'{host.name}' มี MusicDirector แล้ว — ไม่แตะ");

            var sfx = host.GetComponent<ProgressionSfx>();
            if (sfx == null)
            {
                sfx = host.AddComponent<ProgressionSfx>();
                sfx.levelUpOpenClip   = Clip("Magic Score 5",   log);
                sfx.augmentOpenClip   = Clip("Cosmic Reveal",   log);
                sfx.orbRewardOpenClip = Clip("Discovery 1",     log);
                sfx.cardPickClip      = Clip("Approved 1",      log);
                sfx.augmentPickClip   = Clip("Unlocked Secret", log);
                log.Add($"เพิ่ม ProgressionSfx บน '{host.name}'");
            }
            else log.Add($"'{host.name}' มี ProgressionSfx แล้ว — ไม่แตะ");

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new System.Exception("บันทึกซีนไม่สำเร็จ");
            AssetDatabase.SaveAssets();
            log.Add($"บันทึก {ScenePath}");
            return log;
        }

        static AudioClip Clip(string name, List<string> log)
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxDir + name + ".wav");
            if (c == null) log.Add($"⚠ ไม่พบคลิป {SfxDir}{name}.wav — ช่องนี้ว่างไว้");
            return c;
        }
    }
}
