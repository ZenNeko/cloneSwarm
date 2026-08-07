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
    [Header("Cast bar")]
    [Tooltip("ชื่อท่าที่โชว์บน cast bar — ว่าง = ไม่โชว์ cast bar")]
    public string castName = "";
    [Tooltip("วินาทีที่ cast bar วิ่ง — 0 = ไม่โชว์")]
    public float castTime = 0f;
    [Header("Roll")]
    [Tooltip("ชื่อ roll ที่ action นี้อ่าน — ว่าง = ไม่ใช้ roll")]
    public string rollName = "";
    [Tooltip("วินาทีที่เป็น Cooldown ล็อคการกระทำถัดไปหลังทำท่านี้เสร็จ")]
    public float cooldownAfter = 0f;

    /// <summary>
    /// ทำการรัน Action แบบ Coroutine บน Server
    /// </summary>
    /// <param name="runner">ตัวเรียกใช้งาน (BossController บน prefab บอส)</param>
    /// <param name="telegraphPrefab">TelegraphZone prefab</param>
    public abstract IEnumerator ExecuteCoroutine(NetworkBehaviour runner, GameObject telegraphPrefab);

    // ── Editor Support (Boss Designer) ────────────────────────────────────
    /// <summary>กันการเรียกซ้อนไม่รู้จบ กรณี action อ้างอิงกันเป็นวงกลม (Combo/Timeline ซ้อนตัวเอง)</summary>
    protected static int s_editorDurationDepth;

    /// <summary>
    /// ประมาณความยาวรวมของท่านี้เป็นวินาที (รวม actionDelay และ castTime) — ใช้วาดความยาวคลิปใน Boss Designer
    /// ไม่มีผลต่อ gameplay
    /// </summary>
    public virtual float GetEditorDuration() => actionDelay + castTime + 1f;

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

    /// <summary>
    /// อ่านค่า roll ที่ resolve แล้วของ fight นี้ — คืน -1 ถ้าไม่มี roll หรือหาไม่เจอ
    /// </summary>
    protected int GetRoll(NetworkBehaviour runner)
    {
        if (string.IsNullOrEmpty(rollName) || runner == null) return -1;
        var boss = runner as BossController;
        if (boss != null && boss.Rolls != null)
        {
            return boss.Rolls.Peek(rollName);
        }
        return -1;
    }
}
