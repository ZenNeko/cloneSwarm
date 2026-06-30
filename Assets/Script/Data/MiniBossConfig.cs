using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// สืบทอดจาก BossEncounterConfig เพื่อรักษาความเข้ากันได้ของ Asset เดิมในโปรเจกต์ (Backward Compatibility)
/// ข้อมูลทุกอย่างตอนนี้ถูกย้ายไปอยู่ที่คลาสแม่ BossEncounterConfig แล้ว
/// </summary>
[CreateAssetMenu(fileName = "MiniBossConfig", menuName = "Game/Legacy/MiniBossConfig")]
public class MiniBossConfig : BossEncounterConfig
{
    [Header("Legacy Actions (จะถูกโอนไปยัง Phase แรกอัตโนมัติหาก phases ว่าง)")]
    public List<BossAction> actions = new List<BossAction>();

    private void OnValidate()
    {
        // โอนย้ายข้อมูลจาก actions เก่า เข้าสู่ระบบ phases อัตโนมัติ (Backward Compatibility)
        if (phases == null || phases.Count == 0)
        {
            if (actions != null && actions.Count > 0)
            {
                phases = new List<BossPhase>
                {
                    new BossPhase
                    {
                        transitionHealthPct = 0f,
                        invincibilityDuration = 0f,
                        cameraShakeMagnitude = 0f,
                        actions = new List<BossAction>(actions)
                    }
                };
            }
        }
    }
}
