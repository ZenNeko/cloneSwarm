using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Elite Controller — wraps Enemy ด้วย modifier behaviors
///
/// ติดบน enemy ตอน spawn (server เรียก ApplyServer + BroadcastModsClientRpc)
///
/// Behaviors:
///   Shield   — server: ดูดดาเมจก่อนเข้า netHealth
///   Rage     — server: เมื่อ HP <= threshold → boost speed
///   Split    — server: ตอน enemy.onDeath → spawn mini copies
///   Exploder — server: ตอน enemy.onDeath → AoE damage รอบตัว
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
    private float _shieldHP;
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
            _enemy.onDeath.AddListener(OnEnemyDeath);
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
            _enemy.onDeath.RemoveListener(OnEnemyDeath);
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

            // Init Shield HP
            if (def.behaviorType == EliteModifierDef.BehaviorType.Shield)
                _shieldHP += def.shieldHP;
        }
    }

    // ── Server: damage absorption (Shield) ────────────────────────────────
    void OnHealthChanged(float prev, float curr)
    {
        if (!IsServer) return;

        // Shield: ถ้า curr < prev → ดาเมจมา → ดูดด้วย shield ก่อน
        if (_shieldHP > 0f && curr < prev)
        {
            float dmg = prev - curr;
            float absorbed = Mathf.Min(_shieldHP, dmg);
            _shieldHP -= absorbed;

            // Push HP กลับขึ้นเท่ากับที่ shield ดูด
            _enemy.netHealth.Value = Mathf.Min(_enemy.maxHealth, curr + absorbed);
            return; // ไม่ trigger rage check ใน frame นี้ (HP ยังไม่ลด)
        }

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

    // ── Server: death behaviors (Split / Exploder) ────────────────────────
    void OnEnemyDeath()
    {
        if (!IsServer) return;

        Vector3 pos = transform.position;

        foreach (int idx in _modIndices)
        {
            var def = EliteRegistry.Instance?.GetById(idx);
            if (def == null) continue;

            switch (def.behaviorType)
            {
                case EliteModifierDef.BehaviorType.Split:
                    SpawnSplits(def, pos);
                    break;
                case EliteModifierDef.BehaviorType.Exploder:
                    DealExplosion(def, pos);
                    break;
            }
        }
    }

    void SpawnSplits(EliteModifierDef def, Vector3 center)
    {
        if (def.splitPrefab == null) return;

        for (int i = 0; i < def.splitCount; i++)
        {
            float angle = (360f / def.splitCount) * i;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 1.2f;
            var go = Instantiate(def.splitPrefab, center + offset, Quaternion.identity);
            go.GetComponent<NetworkObject>()?.Spawn(true);
        }
    }

    void DealExplosion(EliteModifierDef def, Vector3 center)
    {
        if (NetworkManager.Singleton == null) return;

        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
        {
            var po = c.PlayerObject;
            if (po == null) continue;
            float d = Vector3.Distance(po.transform.position, center);
            if (d <= def.exploderRadius)
                po.GetComponent<playermove>()?.TakeDamage(def.exploderDamage);
        }

        PlayExplosionVfxClientRpc(center, def.exploderVfxType, def.exploderRadius);
    }

    [ClientRpc]
    void PlayExplosionVfxClientRpc(Vector3 pos, string vfxKey, float radius)
    {
        NetworkedVFXPool.Instance?.PlayByName(vfxKey, pos, scale: radius);
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

        // Outline
        _outline = GetComponent<EliteOutline>() ?? gameObject.AddComponent<EliteOutline>();
        _outline.outlineColor = firstDef.outlineColor;
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
        if (_outline != null) _outline.Clear();
        foreach (var c in _crownInstances)
            if (c != null) Destroy(c);
        _crownInstances.Clear();
    }
}
