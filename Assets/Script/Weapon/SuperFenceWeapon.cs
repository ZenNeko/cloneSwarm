using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SuperFenceWeapon — ร่างพัฒนา (Super Version) ของ FenceWeapon
/// เชื่อมโยงทุกเสาที่มีระยะใกล้กันเข้าด้วยกันทั้งหมด (All-to-All Electric Grid)
/// ทำให้สามารถสร้างตาข่ายเลเซอร์ไฟแรงสูงป้องกันพื้นที่ได้
/// </summary>
public class SuperFenceWeapon : FenceWeapon
{
    protected override void GetDesiredConnections(List<GameObject> pillars, List<System.Tuple<GameObject, GameObject>> connections)
    {
        // โหมด Super: จับคู่เสาทุกต้นเข้าหากันทั้งหมดแบบไม่มีเว้น เพื่อสร้างตาข่ายเลเซอร์
        for (int i = 0; i < pillars.Count; i++)
        {
            for (int j = i + 1; j < pillars.Count; j++)
            {
                if (pillars[i] != null && pillars[j] != null)
                {
                    connections.Add(new System.Tuple<GameObject, GameObject>(pillars[i], pillars[j]));
                }
            }
        }
    }

    protected override void CheckPillarDamage(FencePillar pillar, int pillarIdx, List<FencePillar> snapshot, Vector3 pillarPos)
    {
        // โหมด Super: คำนวณทำดาเมจกับทุกเสาที่อยู่ในระยะรอบตัว
        for (int i = pillarIdx + 1; i < snapshot.Count; i++)
        {
            var other = snapshot[i];
            if (other.go != null)
            {
                CheckAndApplyDamage(pillar, other, pillarPos);
            }
        }
    }
}
