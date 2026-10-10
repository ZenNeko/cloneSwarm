using System;
using UnityEngine;

/// <summary>
/// เพลงหนึ่งเพลงที่แยกเป็นชั้น (stem) — เล่นพร้อมกันทุกชั้นตลอดเวลา แล้วเกมปรับเสียงรายชั้น
/// ดูแบบทั้งหมดที่ docs/design-dynamic-bgm.md
///
/// ═══ กติกาของไฟล์เสียง ═══
///
/// ทุก stem ต้องยาวเท่ากัน **ระดับ sample** และ sample rate เท่ากัน ไม่งั้นลูปแต่ละรอบ
/// จะเหลื่อมเพิ่มทีละนิดจนได้ยินเป็นเสียงก้อง · LayeredMusicPlayer ตรวจให้ตอนเริ่มเล่น
/// และ P3RSmokeTest ตรวจทุก track ที่แมพอ้าง
///
/// Import ทุก stem แบบเดียวกัน: Load Type = Compressed In Memory · Vorbis · Preload
/// (ไม่ใช้ Streaming — หลายชั้นอ่านดิสก์พร้อมกัน และจังหวะเริ่มเล่นไม่แน่นอน)
///
/// ═══ ยังไม่มี stem ═══
///
/// ใส่เพลงเต็มเป็น stem เดียวได้เลย ระบบทำงานเหมือนเล่นเพลงธรรมดา
/// พอได้ stem จริงค่อยเพิ่มชั้น — mix อ้างด้วยชื่อ ของเดิมจึงไม่เลื่อน
/// </summary>
[CreateAssetMenu(fileName = "Track_New", menuName = "Clone Swarm/Audio/Layered Track")]
public class LayeredTrack : ScriptableObject
{
    [Serializable]
    public struct Stem
    {
        [Tooltip("ชื่อชั้น เช่น pad / drums / bass — mix ใน MusicProfile อ้างด้วยชื่อนี้ · ห้ามซ้ำ")]
        public string name;
        public AudioClip clip;
    }

    [Tooltip("ทุกคลิปต้องยาวเท่ากันเป๊ะ (จำนวน sample) และ sample rate เท่ากัน")]
    public Stem[] stems = new Stem[0];

    [Tooltip("ยังไม่ใช้ใน v1 — สำรองไว้สำหรับเปิดชั้นให้ตรงต้นห้องเพลง")]
    [Min(1f)] public float bpm = 120f;
    [Min(1)]  public int   beatsPerBar = 4;

    public int StemCount => stems != null ? stems.Length : 0;

    /// <summary>index ของ stem ชื่อนี้ · -1 ถ้าไม่มี</summary>
    public int IndexOf(string stemName)
    {
        if (stems == null || string.IsNullOrEmpty(stemName)) return -1;
        for (int i = 0; i < stems.Length; i++)
            if (stems[i].name == stemName) return i;
        return -1;
    }

    /// <summary>
    /// ปัญหาของ track นี้ · null = ใช้ได้ — ใช้ร่วมกันทั้งตอนรันและ smoke test
    /// </summary>
    public string Validate()
    {
        if (stems == null || stems.Length == 0) return "ไม่มี stem";

        int samples = -1, freq = -1;
        string first = null;
        for (int i = 0; i < stems.Length; i++)
        {
            var s = stems[i];
            if (string.IsNullOrEmpty(s.name)) return $"stem #{i} ไม่มีชื่อ";
            for (int j = 0; j < i; j++)
                if (stems[j].name == s.name) return $"ชื่อ stem '{s.name}' ซ้ำ";
            if (s.clip == null) return $"stem '{s.name}' ไม่มีคลิป";

            if (samples < 0) { samples = s.clip.samples; freq = s.clip.frequency; first = s.name; continue; }
            if (s.clip.frequency != freq)
                return $"stem '{s.name}' sample rate {s.clip.frequency} ≠ '{first}' {freq}";
            if (s.clip.samples != samples)
                return $"stem '{s.name}' ยาว {s.clip.samples} sample ≠ '{first}' {samples} — ลูปจะเหลื่อม";
        }
        return null;
    }
}
