using System;
using UnityEngine;

/// <summary>
/// กำหนด enemy composition ของแต่ละ wave
/// สร้างผ่าน Assets → Create → Swarm → Wave Config
/// </summary>
[CreateAssetMenu(fileName = "WaveConfig", menuName = "Swarm/Wave Config")]
public class WaveConfig : ScriptableObject
{
    [Serializable]
    public class EnemyEntry
    {
        [Tooltip("Enemy prefab ที่จะ spawn")]
        public GameObject prefab;
        [Tooltip("น้ำหนักการถูกเลือก (ยิ่งมาก ยิ่งถูกเลือกบ่อย)")]
        [Range(1, 10)] public int weight = 1;
    }

    [Header("Enemy Composition")]
    [Tooltip("ประเภท enemy ที่ spawn ใน wave นี้")]
    public EnemyEntry[] enemies;

    [Header("Spawn Rate Override")]
    [Tooltip("ปล่อยไว้ 0 = ใช้ค่าจาก WaveManager")]
    public float spawnIntervalOverride = 0f;

    // ── Random Weighted Pick ──────────────────────────────────────────────
    /// <summary>เลือก prefab แบบ weighted random — คืน null ถ้าไม่มี entry</summary>
    public GameObject PickRandomPrefab()
    {
        if (enemies == null || enemies.Length == 0) return null;

        int total = 0;
        foreach (var e in enemies) total += Mathf.Max(1, e.weight);

        int roll = UnityEngine.Random.Range(0, total);
        int acc  = 0;
        foreach (var e in enemies)
        {
            acc += Mathf.Max(1, e.weight);
            if (roll < acc) return e.prefab;
        }
        return enemies[enemies.Length - 1].prefab;
    }
}
