using UnityEngine;

public enum ArenaShape { Circle, Square }

[CreateAssetMenu(fileName = "Arena_New", menuName = "LoL Swarm/Arena Definition")]
public class ArenaDefinition : ScriptableObject
{
    public ArenaShape shape = ArenaShape.Circle;
    public Vector3    center = Vector3.zero;
    public float      radius = 20f;      // Circle = รัศมี · Square = ครึ่งด้าน
}
