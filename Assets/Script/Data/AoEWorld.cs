using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ข้อเท็จจริงของโลกที่ท่า AoE ต้องรู้เพื่อหาจุดเกิด — บอสอยู่ไหน ผู้เล่นอยู่ไหน สนามเป็นยังไง
///
/// ═══ ทำไมต้องมีชั้นนี้ ═══
///
/// เดิมโค้ดหาจุดเกิดรับ <see cref="NetworkBehaviour"/> แล้วไปถาม NetworkManager เอง
/// จึงเรียกได้เฉพาะตอนเกมกำลังรัน · Boss Designer อยากวาดว่าท่านี้ลงตรงไหนก็ต้องเขียนสูตรซ้ำ
/// อีกชุด แล้วสองชุดนั้นจะเพี้ยนจากกันทันทีที่ใครแก้ข้างใดข้างหนึ่ง — ภาพที่โกหกพาไปแก้ผิดที่
///
/// แยกข้อเท็จจริงออกมาเป็น struct ตรงกลางแทน: runtime สร้างจาก NetworkManager ·
/// editor สร้างจากผู้เล่นสมมติ · **สูตรหาจุดเกิดมีชุดเดียว** ภาพที่วาดกับของที่ยิงจริง
/// จึงมาจากบรรทัดเดียวกัน
/// </summary>
public struct AoEWorld
{
    /// <summary>ตำแหน่งบอสผู้ปล่อยท่า — เป็นทั้งจุดอ้างอิงและระนาบ y ของทุกจุดเกิด</summary>
    public Vector3 bossPos;

    /// <summary>ตำแหน่งผู้เล่นที่ยังไม่ตาย (ปรับ y ให้เท่าบอสแล้ว) · ว่างได้</summary>
    public List<Vector3> alivePlayers;

    /// <summary>สนามของ encounter · null = ไม่ได้ผูกไว้ (ท่าที่ต้องใช้จะ warn เอง)</summary>
    public ArenaDefinition arena;

    /// <summary>ค่าสุ่มของ fight นี้ · null = ไม่มี (พรีวิวส่ง context ปลอมที่ล็อกค่าไว้ได้)</summary>
    public RollContext rolls;

    /// <summary>สร้างจากสถานะเกมจริง — ใช้บนเซิร์ฟเวอร์ตอนยิงท่า</summary>
    public static AoEWorld FromRunner(NetworkBehaviour runner)
    {
        var boss = runner as BossController;
        var world = new AoEWorld
        {
            bossPos      = runner != null ? runner.transform.position : Vector3.zero,
            alivePlayers = new List<Vector3>(),
            arena        = boss != null ? boss.config?.arena : null,
            rolls        = boss != null ? boss.Rolls : null,
        };

        if (NetworkManager.Singleton == null) return world;

        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (c.PlayerObject == null) continue;
            var pm = c.PlayerObject.GetComponent<playermove>();
            if (pm == null || pm.isDead.Value) continue;

            // ปรับ y ให้เท่าบอสตั้งแต่ตรงนี้ — ทุกจุดเกิดอยู่ระนาบเดียวกันเสมอ
            Vector3 p = c.PlayerObject.transform.position;
            p.y = world.bossPos.y;
            world.alivePlayers.Add(p);
        }

        return world;
    }

    public int PlayerCount => alivePlayers != null ? alivePlayers.Count : 0;

    /// <summary>ผู้เล่นที่ใกล้จุดนี้ที่สุด — false ถ้าไม่มีใครเหลือ</summary>
    public bool TryNearestPlayer(Vector3 origin, out Vector3 nearest)
    {
        nearest = default;
        if (PlayerCount == 0) return false;

        float best = float.MaxValue;
        foreach (var p in alivePlayers)
        {
            float d = (p - origin).sqrMagnitude;
            if (d < best) { best = d; nearest = p; }
        }
        return true;
    }
}
