using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Timeline ของท่าบอส — หลาย track, แต่ละ track มีคลิป (BossAction + เวลาเริ่ม)
/// ทุกคลิปถูก schedule คู่ขนานนับจากจุดเริ่ม timeline (generalize มาจาก ComboAction)
///
/// ใช้เป็นชุดท่าประจำเฟสได้: ใส่เป็น action เดียวใน BossPhase.actions
/// → AttackLoop จะรัน timeline จนจบ แล้วเว้น cooldownAfter/attackInterval ก่อนวนรอบใหม่
///
/// แก้ไขด้วยหน้าต่าง Tools → Boss Designer (ดับเบิลคลิก asset ก็เปิดได้)
/// </summary>
[CreateAssetMenu(fileName = "BossTimelineAction", menuName = "Boss/Actions/TimelineAction")]
public class BossTimelineAction : BossAction
{
    [System.Serializable]
    public class TimelineClip
    {
        public BossAction action;
        [Tooltip("วินาทีเริ่มนับจากจุดเริ่ม timeline")]
        [Min(0f)] public float startTime;

        // ── ช่วงระดับความยาก — ท่าต่อยอดแบบ Rabbit and Steel ในไฟล์บอสไฟล์เดียว ──
        // ปิด (ค่าเริ่ม) = ออกทุกระดับ · เป็น bool นำ ไม่ใช่ maxTier = Epic ตรงๆ เพราะคลิปเก่าในไฟล์
        // ไม่มีช่องนี้ ถ้าค่าที่หายไปอ่านเป็น 0 (Easy) คลิปเก่าทั้งหมดจะออกแค่ Easy
        [Tooltip("เปิด = คลิปนี้ออกเฉพาะระดับ minTier ถึง maxTier · ปิด = ทุกระดับ")]
        public bool limitTiers;
        public DifficultyTier minTier = DifficultyTier.Easy;
        public DifficultyTier maxTier = DifficultyTier.Epic;

        public bool ActiveIn(DifficultyTier t) => !limitTiers || (t >= minTier && t <= maxTier);
    }

    [System.Serializable]
    public class TimelineTrack
    {
        public string trackName = "Track";
        public List<TimelineClip> clips = new List<TimelineClip>();
    }

    [Header("Timeline")]
    public List<TimelineTrack> tracks = new List<TimelineTrack>();

    [Tooltip("รอให้ timeline เดินจนจบก่อนคืน control ให้ AttackLoop (เปิดไว้เมื่อใช้เป็นชุดท่าประจำเฟส)")]
    public bool waitForTimelineEnd = true;

    [Tooltip("เวลาหางเพิ่มท้าย timeline (วินาที) — เผื่อให้ท่าสุดท้าย resolve จบก่อนวนรอบใหม่")]
    [Min(0f)] public float extraTailTime = 0f;

    /// <summary>
    /// ความยาว timeline = จุดจบของคลิปที่จบช้าสุด + extraTailTime
    ///
    /// **ใช้วาดไม้บรรทัดใน Boss Designer เท่านั้น** — ของเดิมเอาค่านี้ไป WaitForSeconds
    /// เป็นจังหวะของเฟสจริงๆ ซึ่งแปลว่าความเร็วบอสขึ้นกับค่าประมาณที่ตั้งใจให้แค่พอเห็นภาพ
    /// </summary>
    public float GetTimelineDuration()
    {
        if (s_editorDurationDepth > 8) return 0f;
        s_editorDurationDepth++;
        try
        {
            float end = 0f;
            foreach (var track in tracks)
            {
                if (track?.clips == null) continue;
                foreach (var clip in track.clips)
                {
                    if (clip?.action == null || clip.action == this) continue;
                    end = Mathf.Max(end, clip.startTime + clip.action.GetEditorDuration());
                }
            }
            return end + extraTailTime;
        }
        finally { s_editorDurationDepth--; }
    }

    public override float GetEditorDuration() => actionDelay + GetTimelineDuration();

    /// <summary>
    /// ตัวนับคลิปที่ยังไม่จบ ของ **การเรียกครั้งนี้ครั้งเดียว**
    ///
    /// BossAction เป็น ScriptableObject ที่ใช้ร่วมกันทั้งเกม — บอสสองตัวรัน timeline เดียวกัน
    /// พร้อมกันได้ ถ้าเก็บตัวนับเป็นฟิลด์ของ asset ตัวเลขจะปนกันจนไม่มีใครรอจบเลย
    /// </summary>
    private class ClipTally { public int Running; }

    public override IEnumerator ExecuteCoroutine(NetworkBehaviour runner, GameObject telegraphPrefab)
    {
        if (actionDelay > 0f) yield return new WaitForSeconds(actionDelay);

        // AttackLoop roll ให้เฉพาะ action ระดับบนสุด — คลิปในนี้รันผ่าน StartCoroutine ตรงๆ
        // ถ้าไม่ roll ให้ คลิปจะ Peek ได้ -1 ตลอดและ roll ทั้งระบบจะไร้ผล
        // roll ครั้งเดียวต่อ rollName เพื่อให้ทุกคลิปในชุดเดียวกันได้ค่าตรงกัน
        RollForSubActions(runner, EnumerateClipActions());

        int gen = RunGenerationOf(runner);
        var tally = new ClipTally();

        foreach (var track in tracks)
        {
            if (track?.clips == null) continue;
            foreach (var clip in track.clips)
            {
                if (clip?.action == null || clip.action == this) continue;
                if (!clip.ActiveIn(TierOf(runner))) continue;   // คลิปของระดับอื่น
                tally.Running++;
                runner.StartCoroutine(RunClipDelayed(runner, telegraphPrefab, clip.action, clip.startTime, gen, tally));
            }
        }

        if (!waitForTimelineEnd) yield break;

        // รอ "คลิปจบจริง" ไม่ใช่รอนาฬิกาตามค่าประมาณ — ท่าที่ยาวกว่าที่วาดไว้จะไม่ถูกตัดกลางคัน
        // และท่าที่สั้นกว่าจะไม่ทิ้งช่องว่างเปล่าไว้ท้ายเฟส
        while (tally.Running > 0)
        {
            if (!RunStillValid(runner, gen)) yield break;   // เปลี่ยนเฟส/ตาย — เลิกรอ
            yield return null;
        }

        if (extraTailTime > 0f) yield return new WaitForSeconds(extraTailTime);
    }

    private IEnumerable<BossAction> EnumerateClipActions()
    {
        if (tracks == null) yield break;
        foreach (var track in tracks)
        {
            if (track?.clips == null) continue;
            foreach (var clip in track.clips)
                if (clip?.action != null) yield return clip.action;
        }
    }

    private IEnumerator RunClipDelayed(NetworkBehaviour runner, GameObject telegraphPrefab,
                                       BossAction action, float delay, int gen, ClipTally tally)
    {
        // finally ต้องลดตัวนับทุกทาง ไม่งั้นคลิปที่ยกเลิกกลางคันจะทำให้ timeline รอค้างตลอดไป
        try
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            // เช็คหลังหน่วงเสร็จ — คลิปที่ยังไม่ถึงคิวตอนเปลี่ยนเฟส ต้องไม่ยิงเข้าไปในเฟสใหม่
            if (!RunStillValid(runner, gen)) yield break;

            yield return runner.StartCoroutine(action.ExecuteCoroutine(runner, telegraphPrefab));
        }
        finally
        {
            tally.Running--;
        }
    }
}
