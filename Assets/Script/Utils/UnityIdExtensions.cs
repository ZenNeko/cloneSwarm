using UnityEngine;

public static class UnityIdExtensions
{
    /// <summary>
    /// Helper extension method to retrieve integer Instance ID safely on Unity 6.7
    /// without triggering CS0619 obsolete warnings or Odin Inspector EntityId reflection errors.
    /// </summary>
    public static int GetId(this Object obj)
    {
        if (obj == null) return 0;
        return obj.GetEntityId().GetHashCode();
    }
}
