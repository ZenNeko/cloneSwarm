public static class RunSetup
{
    public static MapData Map { get; private set; }
    public static DifficultyTier Difficulty { get; private set; } = DifficultyTier.Normal;

    public static void Set(MapData map, DifficultyTier tier)
    {
        Map = map;
        Difficulty = tier;
    }

    public static void Reset()
    {
        Map = null;
        Difficulty = DifficultyTier.Normal;
    }
}
