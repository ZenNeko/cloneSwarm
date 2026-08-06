using System;
using System.IO;
using UnityEngine;

namespace CloneSwarm.Meta
{
    /// <summary>
    /// อ่าน/เขียนไฟล์เซฟ — static ไม่ต้องมี GameObject
    ///
    ///   SaveManager.Data      → SaveData ปัจจุบัน (โหลด lazy ครั้งแรกที่เรียก)
    ///   SaveManager.Save()    → เขียนลงดิสก์แบบ atomic
    ///   SaveManager.MarkDirty() → ตั้ง flag ให้ auto-flush เขียนให้ทีหลัง
    ///
    /// ไฟล์อยู่ที่ Application.persistentDataPath/profile.json
    /// เขียนแบบ temp → replace เพื่อกันไฟล์พังตอนเกม crash กลางคัน
    /// และเก็บ .bak ไว้หนึ่งชุดเสมอ
    /// </summary>
    public static class SaveManager
    {
        const string FileName   = "profile.json";
        const string BackupName = "profile.bak.json";
        const string TempName   = "profile.tmp.json";

        static SaveData _data;
        static bool     _dirty;

        public static string SavePath   => Path.Combine(Application.persistentDataPath, FileName);
        public static string BackupPath => Path.Combine(Application.persistentDataPath, BackupName);
        static string        TempPath   => Path.Combine(Application.persistentDataPath, TempName);

        /// <summary>ยิงหลังโหลด/รีเซ็ต/เซฟสำเร็จ — UI subscribe เพื่อ refresh ตัวเลข</summary>
        public static event Action OnDataChanged;

        // ═══════════════════════════════════════════════════════════════════
        // Access
        // ═══════════════════════════════════════════════════════════════════
        public static SaveData Data
        {
            get
            {
                if (_data == null) Load();
                return _data;
            }
        }

        /// <summary>บอกว่ามีอะไรเปลี่ยน — auto-flush จะเขียนให้ตอนออกเกม/สลับ scene</summary>
        public static void MarkDirty()
        {
            _dirty = true;
            OnDataChanged?.Invoke();
        }

        // ═══════════════════════════════════════════════════════════════════
        // Load
        // ═══════════════════════════════════════════════════════════════════
        public static void Load()
        {
            _data = ReadFrom(SavePath) ?? ReadFrom(BackupPath);

            if (_data == null)
            {
                _data = SaveData.CreateNew();
                Debug.Log("[Save] ไม่พบไฟล์เซฟ — สร้างโปรไฟล์ใหม่");
                Save();
            }
            else
            {
                Migrate(_data);
            }

            _dirty = false;
            OnDataChanged?.Invoke();
        }

        static SaveData ReadFrom(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return null;

                var d = JsonUtility.FromJson<SaveData>(json);
                if (d == null) return null;

                // JsonUtility คืน null ให้ List ที่ไม่มีใน json — กัน NRE ตรงนี้
                d.talents            ??= new System.Collections.Generic.List<SaveData.TalentEntry>();
                d.unlockedCharacters ??= new System.Collections.Generic.List<string>();
                return d;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] อ่าน {Path.GetFileName(path)} ไม่สำเร็จ: {e.Message}");
                return null;
            }
        }

        /// <summary>อัปเกรดไฟล์เซฟเวอร์ชันเก่า — เพิ่ม case ใหม่เมื่อ bump CurrentVersion</summary>
        static void Migrate(SaveData d)
        {
            if (d.version == SaveData.CurrentVersion) return;

            if (d.version < 1)
            {
                // v0 → v1: ไฟล์ก่อนมี versioning
                if (string.IsNullOrEmpty(d.profileId))
                    d.profileId = Guid.NewGuid().ToString("N");
            }

            Debug.Log($"[Save] migrate v{d.version} → v{SaveData.CurrentVersion}");
            d.version = SaveData.CurrentVersion;
            _dirty    = true;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Save
        // ═══════════════════════════════════════════════════════════════════
        /// <summary>เขียนลงดิสก์ทันที (atomic). คืน false ถ้าเขียนไม่สำเร็จ</summary>
        public static bool Save()
        {
            if (_data == null) return false;

            _data.lastPlayedUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            try
            {
                string json = JsonUtility.ToJson(_data, prettyPrint: true);

                // 1. เขียนลง temp ก่อน — ถ้าพังกลางทาง ไฟล์จริงยังอยู่ครบ
                File.WriteAllText(TempPath, json);

                // 2. ไฟล์เดิม → .bak
                if (File.Exists(SavePath))
                {
                    if (File.Exists(BackupPath)) File.Delete(BackupPath);
                    File.Move(SavePath, BackupPath);
                }

                // 3. temp → ไฟล์จริง
                File.Move(TempPath, SavePath);

                _dirty = false;
                OnDataChanged?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] เขียนไฟล์เซฟไม่สำเร็จ: {e.Message}");
                return false;
            }
        }

        /// <summary>เขียนเฉพาะเมื่อมี MarkDirty ค้างอยู่</summary>
        public static void FlushIfDirty()
        {
            if (_dirty) Save();
        }

        // ═══════════════════════════════════════════════════════════════════
        // Reset (ใช้ในปุ่ม "ลบข้อมูล" และตอนเทส)
        // ═══════════════════════════════════════════════════════════════════
        public static void ResetProfile()
        {
            _data = SaveData.CreateNew();
            Save();
            Debug.Log("[Save] รีเซ็ตโปรไฟล์เรียบร้อย");
        }

        // ═══════════════════════════════════════════════════════════════════
        // Auto-flush hook — ไม่ต้องวาง GameObject ใน scene
        // ═══════════════════════════════════════════════════════════════════
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InstallAutoFlush()
        {
            var go = new GameObject("~SaveAutoFlush");
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<SaveAutoFlush>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }
    }

    /// <summary>เขียนไฟล์เซฟให้อัตโนมัติตอนเกมปิด / ถูกพับลง (มือถือ)</summary>
    public class SaveAutoFlush : MonoBehaviour
    {
        void OnApplicationQuit()               => SaveManager.FlushIfDirty();
        void OnApplicationPause(bool paused)   { if (paused) SaveManager.FlushIfDirty(); }
        void OnApplicationFocus(bool focused)  { if (!focused) SaveManager.FlushIfDirty(); }
    }
}
