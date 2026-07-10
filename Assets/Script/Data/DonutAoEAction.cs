using UnityEngine;

[CreateAssetMenu(fileName = "DonutAoEAction", menuName = "Boss/Actions/DonutAoEAction")]
public class DonutAoEAction : SpawnAoEActionBase
{
    [Header("Donut Settings")]
    public float radius = 5f;
    public float innerRadius = 2f;

    protected override AoEType GetAoEType()
    {
        return AoEType.Donut;
    }

    protected override void ConfigureTelegraphZone(TelegraphZone zone)
    {
        zone.radius = radius;
        zone.innerRadius = innerRadius;
    }
}
