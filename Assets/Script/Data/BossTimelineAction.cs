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

    /// <summary>ความยาว timeline = จุดจบของคลิปที่จบช้าสุด + extraTailTime</summary>
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

    public override IEnumerator ExecuteCoroutine(NetworkBehaviour runner, GameObject telegraphPrefab)
    {
        if (actionDelay > 0f) yield return new WaitForSeconds(actionDelay);

        float duration = GetTimelineDuration();
        foreach (var track in tracks)
        {
            if (track?.clips == null) continue;
            foreach (var clip in track.clips)
            {
                if (clip?.action == null || clip.action == this) continue;
                runner.StartCoroutine(RunClipDelayed(runner, telegraphPrefab, clip.action, clip.startTime));
            }
        }

        if (waitForTimelineEnd && duration > 0f)
        {
            yield return new WaitForSeconds(duration);
        }
    }

    private IEnumerator RunClipDelayed(NetworkBehaviour runner, GameObject telegraphPrefab, BossAction action, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (runner != null && runner.NetworkObject.IsSpawned)
        {
            yield return runner.StartCoroutine(action.ExecuteCoroutine(runner, telegraphPrefab));
        }
    }
}
