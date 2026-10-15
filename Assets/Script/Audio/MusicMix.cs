using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// สูตร mix ของ MusicDirector — คณิตล้วน ไม่แตะ GameTimeline / SoundManager / นาฬิกา
/// MusicDirector เก็บข้อเท็จจริงแล้วส่งมาที่นี่ทุกรอบ · แยกออกมาให้ EditMode test (MusicMixTests)
/// ไล่ทุกสถานการณ์ได้โดยไม่ต้องเปิดซีนหรือรอนาทีที่ 15
///
/// ลำดับ:  บอสใหญ่  >  max(ช่วงเวลา, overlay มินิบอส)  แล้วใส่ override จอเลือกการ์ดทับ
/// (ชนะ/แพ้อยู่บนสุด แต่เป็นการหยุดเพลง ไม่ใช่ mix — MusicDirector จัดการเอง)
/// </summary>
public class MusicMix
{
    public struct Facts
    {
        public float minutes;
        public bool  mainBossPhase;
        public int   bossPhase;
        /// <summary>มินิบอสมีชีวิต หรือยังอยู่ในช่วง release hold</summary>
        public bool  miniBoss;
        public bool  cardPick;
        /// <summary>สิ่งที่ส่งไปรอบก่อน — ใช้เลือกว่าการเปลี่ยนรอบนี้ fade ด้วยค่าไหน</summary>
        public bool  sentOverlay, sentCardPick;
    }

    public struct Result
    {
        public LayeredTrack track;
        public float  fade;
        public float  duck;
        public bool   overlay;
        public string label;
    }

    /// <summary>ระดับต่อ stem ของ Result.track ล่าสุด · ขนาด = จำนวน stem · ใช้ซ้ำทุกรอบ อย่าเก็บไว้</summary>
    public float[] Levels => _work;

    float[] _work    = new float[0];
    float[] _preBoss = new float[0];   // mix ล่าสุดของเพลงหลักก่อนบอสออก — ใช้ตอนไม่มีธีมบอส

    // ชื่อ stem → index แปลงครั้งเดียวต่อ (รายการ, track) · ไม่เทียบสตริงทุกรอบ
    readonly Dictionary<(StemLevel[], LayeredTrack), int[]> _indexCache = new();
    readonly HashSet<string> _warnedNames = new();

    MusicProfile _profile;

    public Result Evaluate(MusicProfile profile, in Facts f)
    {
        _profile = profile;

        bool bossTheme = f.mainBossPhase && profile.mainBossTrack != null && profile.mainBossTrack.StemCount > 0;
        var  track     = bossTheme ? profile.mainBossTrack : profile.track;

        EnsureSize(ref _work, track.StemCount);
        System.Array.Clear(_work, 0, _work.Length);

        var r = new Result { track = track };

        if (bossTheme)
        {
            var mix = profile.PhaseMix(f.bossPhase);
            Fill(_work, mix, track);
            r.fade  = profile.FadeOf(mix);
            r.label = $"MainBoss (ธีม) เฟส {f.bossPhase + 1}";
        }
        else if (f.mainBossPhase)
        {
            // ไม่มีธีมบอส → เพลงเดิมเล่นต่อ ค้าง mix ล่าสุดก่อนบอสออก ไม่ปรับอะไร (ตัดสินแล้ว 2026-09-23)
            // เข้ามากลางไฟต์บอสจะไม่มี mix ก่อนหน้า → ใช้ช่วงเวลาล่าสุดแทน ดีกว่าเงียบทั้งไฟต์
            if (_preBoss.Length == 0)
            {
                int last = profile.BandIndexAt(f.minutes);
                EnsureSize(ref _preBoss, _work.Length);
                if (last >= 0) Fill(_preBoss, profile.timeBands[last].mix, track);
            }
            System.Array.Copy(_preBoss, _work, Mathf.Min(_preBoss.Length, _work.Length));
            r.fade  = profile.defaultFade;
            r.label = "MainBoss (เพลงเดิม)";
        }
        else
        {
            int band = profile.BandIndexAt(f.minutes);
            StemMix bandMix = band >= 0 ? profile.timeBands[band].mix : null;
            Fill(_work, bandMix, track);
            r.fade  = profile.FadeOf(bandMix);
            r.label = band >= 0 ? $"ช่วง {band + 1} (นาที {profile.timeBands[band].atMinutes:0.#})" : "ไม่มีช่วงเวลา";

            r.overlay = f.miniBoss;
            if (r.overlay)
            {
                MaxInto(_work, profile.miniBossOverlay, track);
                r.label += " + มินิบอส";
            }
            if (r.overlay != f.sentOverlay) r.fade = profile.FadeOf(profile.miniBossOverlay);

            EnsureSize(ref _preBoss, _work.Length);
            System.Array.Copy(_work, _preBoss, _work.Length);
        }

        // จอเลือกการ์ด — ใส่ท้ายสุด ปิดจอแล้วสูตรข้างบนคืน mix ที่ถูกให้เอง
        if (f.cardPick)
        {
            Override(_work, profile.cardPickOverrides, track);
            r.label += " · เลือกการ์ด";
        }
        if (f.cardPick != f.sentCardPick) r.fade = profile.cardPickFade;

        r.duck = f.cardPick ? profile.cardPickDuck : 1f;
        return r;
    }

    // ── Mix helpers ───────────────────────────────────────────────────────
    void Fill(float[] into, StemMix mix, LayeredTrack track)
    {
        if (mix?.levels == null) return;
        var idx = Indices(mix.levels, track, warn: true);
        for (int i = 0; i < idx.Length; i++)
            if (idx[i] >= 0) into[idx[i]] = mix.levels[i].level;
    }

    void MaxInto(float[] into, StemMix mix, LayeredTrack track)
    {
        if (mix?.levels == null) return;
        var idx = Indices(mix.levels, track, warn: true);
        for (int i = 0; i < idx.Length; i++)
            if (idx[i] >= 0) into[idx[i]] = Mathf.Max(into[idx[i]], mix.levels[i].level);
    }

    void Override(float[] into, StemLevel[] levels, LayeredTrack track)
    {
        if (levels == null) return;
        // ไม่เตือน — รายการนี้ใช้ทั้งกับเพลงหลักและธีมบอส ชื่อของอีกเพลงจึงไม่เจอเป็นเรื่องปกติ
        // ชื่อที่พิมพ์ผิดจริงๆ P3RSmokeTest จับให้ (ต้องมีในเพลงใดเพลงหนึ่ง)
        var idx = Indices(levels, track, warn: false);
        for (int i = 0; i < idx.Length; i++)
            if (idx[i] >= 0) into[idx[i]] = levels[i].level;
    }

    int[] Indices(StemLevel[] levels, LayeredTrack track, bool warn)
    {
        var key = (levels, track);
        if (_indexCache.TryGetValue(key, out var idx) && idx.Length == levels.Length) return idx;

        idx = new int[levels.Length];
        for (int i = 0; i < levels.Length; i++)
        {
            idx[i] = track.IndexOf(levels[i].stem);
            if (warn && idx[i] < 0 && !string.IsNullOrEmpty(levels[i].stem) &&
                _warnedNames.Add($"{track.name}/{levels[i].stem}"))
                Debug.LogWarning($"[Music] '{_profile.name}' อ้าง stem '{levels[i].stem}' ที่ไม่มีใน track '{track.name}' — ข้ามชั้นนี้", _profile);
        }
        _indexCache[key] = idx;
        return idx;
    }

    static void EnsureSize(ref float[] arr, int n)
    {
        if (arr.Length != n) arr = new float[n];
    }
}
