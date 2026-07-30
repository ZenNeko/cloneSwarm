using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Base class for all modular boss attacks (FFXIV / Rabbit & Steel style)
/// </summary>
public abstract class BossAction : ScriptableObject
{
    [Header("Modular Action Settings")]
    [Tooltip("วินาทีที่หน่วงก่อนจะเริ่มทำท่านี้")]
    public float actionDelay = 0f;
    [Tooltip("วินาทีที่เป็น Cooldown ล็อคการกระทำถัดไปหลังทำท่านี้เสร็จ")]
    public float cooldownAfter = 0f;

    /// <summary>
    /// ทำการรัน Action แบบ Coroutine บน Server
    /// </summary>
    /// <param name="runner">ตัวเรียกใช้งาน (เช่น MiniBossAI หรือ MainBoss)</param>
    /// <param name="telegraphPrefab">TelegraphZone prefab</param>
    public abstract IEnumerator ExecuteCoroutine(NetworkBehaviour runner, GameObject telegraphPrefab);

    // ── Editor Support (Boss Designer) ────────────────────────────────────
    /// <summary>กันการเรียกซ้อนไม่รู้จบ กรณี action อ้างอิงกันเป็นวงกลม (Combo/Timeline ซ้อนตัวเอง)</summary>
    protected static int s_editorDurationDepth;

    /// <summary>
    /// ประมาณความยาวรวมของท่านี้เป็นวินาที (รวม actionDelay) — ใช้วาดความยาวคลิปใน Boss Designer
    /// ไม่มีผลต่อ gameplay
    /// </summary>
    public virtual float GetEditorDuration() => actionDelay + 1f;

    // Helper: ค้นหาผู้เล่นที่อยู่ใกล้ที่สุด
    protected Transform FindNearestPlayer(Vector3 origin)
    {
        if (NetworkManager.Singleton == null) return null;
        Transform nearest = null;
        float minDist = float.MaxValue;
        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
        {
            var obj = c.PlayerObject;
            if (obj == null) continue;
            var pm = obj.GetComponent<playermove>();
            if (pm != null && pm.isDead.Value) continue; // ข้ามคนตาย

            float d = Vector3.Distance(origin, obj.transform.position);
            if (d < minDist) { minDist = d; nearest = obj.transform; }
        }
        return nearest;
    }
}
