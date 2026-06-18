using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Elite Controller — wraps Enemy ด้วย modifier behaviors
///
/// ติดบน enemy ตอน spawn (server เรียก ApplyServer + BroadcastModsClientRpc)
///
/// Behaviors:
///   Shield   — server: เพิ่ม Max Health ด้วยค่าของ Shield ตรงๆ
///   Rage     — server: เมื่อ HP <= threshold → boost speed
///
/// Client: render outline + crown
/// </summary>
[RequireComponent(typeof(Enemy))]
public class EliteController : NetworkBehaviour
{
    // Network: list ของ def indices (sync ผ่าน NetworkList)
    private NetworkList<int> _modIndices;

    // Cached refs
    private Enemy   _enemy;
    private EliteOutline _outline;
    private List<GameObject> _crownInstances = new();

    // Server-side runtime state
    private bool  _isRaging;
    private float _baseSpeed;

    void Awake()
    {
        _modIndices = new NetworkList<int>(
            new List<int>(),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
    }

    public override void OnNetworkSpawn()
    {
        _enemy = GetComponent<Enemy>();
        if (_enemy == null) { Debug.LogError("[EliteController] Enemy missing!"); return; }

        _modIndices.OnListChanged += OnModListChanged;

        if (IsServer)
        {
            _enemy.netHealth.OnValueChanged += OnHealthChanged;
            _baseSpeed = _enemy.speed;
        }

        // Apply visuals from current list (รองรับ late-join)
        ApplyVisualsFromList();
    }

    public override void OnNetworkDespawn()
    {
        _modIndices.OnListChanged -= OnModListChanged;

        if (IsServer && _enemy != null)
        {
            _enemy.netHealth.OnValueChanged -= OnHealthChanged;
        }

        ClearVisuals();
    }

    // ── Server: Apply mods ────────────────────────────────────────────────
    public void ApplyServer(EliteModifierDef[] defs)
    {
        if (!IsServer || defs == null) return;

        var registry = EliteRegistry.Instance;
        if (registry == null)
        {
            Debug.LogWarning("[EliteController] EliteRegistry missing — skipping elite spawn");
            return;
        }

        foreach (var def in defs)
        {
            if (def == null) continue;
            int idx = registry.GetIndex(def);
            if (idx < 0) continue;

            _modIndices.Add(idx);

            // Stat bonuses (stack — multiplies existing maxHealth)
            _enemy.ApplyWaveScaling(def.healthMultBonus, def.speedMultBonus, def.expMultBonus);
            _baseSpeed = _enemy.speed;

            // Add Shield HP directly to Max Health
            if (def.behaviorType == EliteModifierDef.BehaviorType.Shield)
            {
                _enemy.maxHealth += def.shieldHP;
                _enemy.netHealth.Value = _enemy.maxHealth;
            }
        }
    }

    // ── Server: damage absorption (Shield) ────────────────────────────────
    void OnHealthChanged(float prev, float curr)
    {
        if (!IsServer) return;

        // Rage: ถ้า HP <= threshold → boost speed
        CheckRage(curr);
    }

    void CheckRage(float currentHp)
    {
        if (_isRaging) return;
        if (_enemy.maxHealth <= 0f) return;

        foreach (int idx in _modIndices)
        {
            var def = EliteRegistry.Instance?.GetById(idx);
            if (def == null) continue;
            if (def.behaviorType != EliteModifierDef.BehaviorType.Rage) continue;

            float pct = currentHp / _enemy.maxHealth;
            if (pct <= def.rageHpThreshold)
            {
                _enemy.speed = _baseSpeed * def.rageSpeedMult;
                _isRaging = true;
                Debug.Log($"[EliteController] 🔥 Rage triggered (HP {pct:P0})");
                break;
            }
        }
    }



    // ── Client: visuals (outline + crown) ─────────────────────────────────
    void OnModListChanged(NetworkListEvent<int> _)
    {
        ApplyVisualsFromList();
    }

    void ApplyVisualsFromList()
    {
        ClearVisuals();
        if (_modIndices.Count == 0) return;

        var registry = EliteRegistry.Instance;
        if (registry == null) return;

        // ใช้สีจาก def ตัวแรกเป็น outline color (stack ก็ใช้ตัวแรก)
        EliteModifierDef firstDef = registry.GetById(_modIndices[0]);
        if (firstDef == null) return;

        // Apply visual scale
        transform.localScale = Vector3.one * firstDef.modelScale;

        // Outline
        _outline = GetComponent<EliteOutline>() ?? gameObject.AddComponent<EliteOutline>();
        _outline.outlineColor = firstDef.outlineColor;
        _outline.outlineScale = firstDef.outlineThickness;
        _outline.Apply();

        // Crown — spawn ตาม def ทุกตัวที่มี crown
        foreach (int idx in _modIndices)
        {
            var def = registry.GetById(idx);
            if (def == null || def.crownPrefab == null) continue;

            var crown = Instantiate(def.crownPrefab, transform);
            crown.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            _crownInstances.Add(crown);
        }
    }

    void ClearVisuals()
    {
        transform.localScale = Vector3.one;
        if (_outline != null) _outline.Clear();
        foreach (var c in _crownInstances)
            if (c != null) Destroy(c);
        _crownInstances.Clear();
    }
}
