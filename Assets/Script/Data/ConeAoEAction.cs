using UnityEngine;

/// <summary>
/// AoE รูปกรวย (sector) — ทรงที่ FF14 ใช้บ่อยรองจากวงกลม · cleave และ AoE ครึ่งห้อง/ควอดแรนต์
///
/// หันไปทางผู้เล่นที่ใกล้ที่สุดโดยอัตโนมัติเหมือน Line
/// `coneAngle = 180` → ครึ่งสนาม · `= 90` → ควอดแรนต์
/// </summary>
[CreateAssetMenu(fileName = "ConeAoEAction", menuName = "Boss/Actions/ConeAoEAction")]
public class ConeAoEAction : SpawnAoEActionBase
{
    [Header("Cone Settings")]
    [Tooltip("ระยะจากจุดยอดกรวย (เมตร)")]
    public float radius = 10f;
    [Tooltip("มุมกางทั้งหมด (องศา) — 90 = ควอดแรนต์ · 180 = ครึ่งสนาม · 360 = วงกลมเต็ม")]
    [Range(1f, 360f)] public float coneAngle = 90f;

    protected override AoEType GetAoEType() => AoEType.Cone;

    protected override void ConfigureTelegraphZone(TelegraphZone zone)
    {
        zone.radius    = radius;
        zone.coneAngle = coneAngle;
    }
}
