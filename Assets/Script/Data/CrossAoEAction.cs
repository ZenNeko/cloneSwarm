using UnityEngine;

[CreateAssetMenu(fileName = "CrossAoEAction", menuName = "Boss/Actions/CrossAoEAction")]
public class CrossAoEAction : SpawnAoEActionBase
{
    [Header("Cross Settings")]
    public float lineLength = 10f;
    public float lineWidth = 2f;

    protected override AoEType GetAoEType()
    {
        return AoEType.Cross;
    }

    protected override void ConfigureTelegraphZone(TelegraphZone zone)
    {
        zone.lineLength = lineLength;
        zone.lineWidth = lineWidth;
    }
}
