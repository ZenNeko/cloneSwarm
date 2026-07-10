using UnityEngine;

[CreateAssetMenu(fileName = "LineAoEAction", menuName = "Boss/Actions/LineAoEAction")]
public class LineAoEAction : SpawnAoEActionBase
{
    [Header("Line Settings")]
    public float lineLength = 10f;
    public float lineWidth = 2f;

    protected override AoEType GetAoEType()
    {
        return AoEType.Line;
    }

    protected override void ConfigureTelegraphZone(TelegraphZone zone)
    {
        zone.lineLength = lineLength;
        zone.lineWidth = lineWidth;
    }
}
