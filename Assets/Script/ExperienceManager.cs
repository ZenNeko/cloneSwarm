using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// EXP / Level system per-player — อยู่บน Player Prefab
/// ไม่มี Singleton แล้ว ใช้ static events แทน
/// </summary>
public class ExperienceManager : NetworkBehaviour
{
    [Header("Level Settings")]
    public int   maxLevel      = 30;
    public float baseExpToLevel = 100f;
    [Tooltip("EXP ที่ต้องการเพิ่มเท่าไหร่ต่อเลเวล")]
    public float expGrowthRate = 1.25f;

    [Header("Multipliers")]
    public float expMultiplier = 1f;

    // ── Network Variables ─────────────────────────────────────────────────
    public NetworkVariable<float> netCurrentExp = new NetworkVariable<float>(
        0f,  NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int>   netCurrentLevel = new NetworkVariable<int>(
        1,   NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> netExpToNext = new NetworkVariable<float>(
        100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ── Static Events (แทน Singleton) ────────────────────────────────────
    /// <summary>ยิงจาก Owner เมื่อ Level Up</summary>
    public static event Action<int>         OnLocalLevelUp;
    /// <summary>ยิงจาก Owner ทุกครั้งที่ EXP เปลี่ยน</summary>
    public static event Action<float,float> OnLocalExpChanged;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (IsServer)
            netExpToNext.Value = CalcExpToNextLevel(1);

        if (IsOwner)
        {
            netCurrentExp.OnValueChanged   += (_, v) => OnLocalExpChanged?.Invoke(v, netExpToNext.Value);
            netCurrentLevel.OnValueChanged += (_, v) => OnLocalLevelUp?.Invoke(v);

            // ยิงค่าเริ่มต้นให้ UI รู้
            OnLocalExpChanged?.Invoke(netCurrentExp.Value, netExpToNext.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;
        netCurrentExp.OnValueChanged   -= (_, v) => OnLocalExpChanged?.Invoke(v, netExpToNext.Value);
        netCurrentLevel.OnValueChanged -= (_, v) => OnLocalLevelUp?.Invoke(v);
    }

    // ── Public API ────────────────────────────────────────────────────────
    /// <summary>เรียกจาก ExpOrb บน Server</summary>
    public void AddExp(float amount)
    {
        if (!IsServer) return;
        if (netCurrentLevel.Value >= maxLevel) return;

        float exp = netCurrentExp.Value + amount * expMultiplier;

        while (exp >= netExpToNext.Value && netCurrentLevel.Value < maxLevel)
        {
            exp -= netExpToNext.Value;
            netCurrentLevel.Value++;
            netExpToNext.Value = CalcExpToNextLevel(netCurrentLevel.Value);
            Debug.Log($"[EXP] Level Up! → {netCurrentLevel.Value}");
        }

        netCurrentExp.Value = exp;
    }

    float CalcExpToNextLevel(int level) =>
        Mathf.Floor(baseExpToLevel * Mathf.Pow(expGrowthRate, level - 1));

    public float GetExpPercent()   => netExpToNext.Value > 0 ? netCurrentExp.Value / netExpToNext.Value : 1f;
    public float GetCurrentExp()   => netCurrentExp.Value;
    public float GetExpToNext()    => netExpToNext.Value;
    public int   GetCurrentLevel() => netCurrentLevel.Value;
}
