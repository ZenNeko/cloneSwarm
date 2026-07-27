using UnityEngine;

[CreateAssetMenu(fileName = "CircleAoEAction", menuName = "Boss/Actions/CircleAoEAction")]
public class CircleAoEAction : SpawnAoEActionBase
{
    [Header("Circle Settings")]
    public float radius = 3f;

    protected override AoEType GetAoEType()
    {
        return AoEType.Circle;
    }

    protected override void ConfigureTelegraphZone(TelegraphZone zone)
    {
        zone.radius = radius;
    }
}
