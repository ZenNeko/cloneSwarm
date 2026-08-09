using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CloneSwarm.Meta;

public struct StatusEntry : INetworkSerializable, IEquatable<StatusEntry>
{
    public FixedString32Bytes statusId;
    public int   stacks;
    public float remaining;    // วินาทีที่เหลือ

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref statusId);
        s.SerializeValue(ref stacks);
        s.SerializeValue(ref remaining);
    }

    public bool Equals(StatusEntry o)
        => statusId.Equals(o.statusId) && stacks == o.stacks && Mathf.Approximately(remaining, o.remaining);
}

public class PlayerStatusManager : NetworkBehaviour
{
    [Tooltip("prefab telegraph ที่ส่งให้ onExpire action ใช้")]
    public GameObject telegraphPrefab;

    public NetworkList<StatusEntry> Statuses;

    public static event Action OnAnyStatusChanged;

    private void Awake()
    {
        Statuses = new NetworkList<StatusEntry>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Statuses.OnListChanged += OnStatusesListChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        Statuses.OnListChanged -= OnStatusesListChanged;
    }

    private void OnStatusesListChanged(NetworkListEvent<StatusEntry> changeEvent)
    {
        OnAnyStatusChanged?.Invoke();
    }

    // ── Server API ────────────────────────────────────────────────────────
    public void ApplyStatus(string statusId, int stacks = 1)
    {
        if (!IsServer || string.IsNullOrEmpty(statusId)) return;

        var data = MetaDatabase.Instance != null ? MetaDatabase.Instance.GetStatus(statusId) : null;
        if (data == null) return;

        FixedString32Bytes fixedId = new FixedString32Bytes(statusId);
        int existingIndex = -1;

        for (int i = 0; i < Statuses.Count; i++)
        {
            if (Statuses[i].statusId.Equals(fixedId))
            {
                existingIndex = i;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            var entry = Statuses[existingIndex];
            int newStacks = Mathf.Min(entry.stacks + stacks, data.maxStacks);
            Statuses[existingIndex] = new StatusEntry
            {
                statusId = fixedId,
                stacks = newStacks,
                remaining = data.duration
            };
        }
        else
        {
            int initStacks = Mathf.Min(stacks, data.maxStacks);
            Statuses.Add(new StatusEntry
            {
                statusId = fixedId,
                stacks = initStacks,
                remaining = data.duration
            });
        }
    }

    public void RemoveStatus(string statusId)
    {
        if (!IsServer || string.IsNullOrEmpty(statusId)) return;

        FixedString32Bytes fixedId = new FixedString32Bytes(statusId);
        for (int i = 0; i < Statuses.Count; i++)
        {
            if (Statuses[i].statusId.Equals(fixedId))
            {
                Statuses.RemoveAt(i);
                break;
            }
        }
    }

    public void ClearAll()
    {
        if (!IsServer) return;
        Statuses.Clear();
    }

    private void Update()
    {
        if (!IsServer || Statuses == null || Statuses.Count == 0) return;

        float dt = Time.deltaTime;
        for (int i = Statuses.Count - 1; i >= 0; i--)
        {
            var entry = Statuses[i];
            string idStr = entry.statusId.ToString();
            var data = MetaDatabase.Instance != null ? MetaDatabase.Instance.GetStatus(idStr) : null;
            if (data == null)
            {
                Statuses.RemoveAt(i);
                continue;
            }

            float rem = entry.remaining - dt;
            int curStacks = entry.stacks;
            bool expired = false;

            if (data.decayInterval > 0f && data.duration > 0f)
            {
                float timePassed = data.duration - rem;
                if (timePassed >= data.decayInterval)
                {
                    curStacks--;
                    rem = data.duration;
                    if (curStacks <= 0) expired = true;
                }
            }
            else
            {
                if (rem <= 0f) expired = true;
            }

            if (expired || curStacks <= 0)
            {
                Statuses.RemoveAt(i);
                if (data.onExpire != null)
                {
                    StartCoroutine(data.onExpire.ExecuteCoroutine(this, telegraphPrefab));
                }
            }
            else
            {
                Statuses[i] = new StatusEntry
                {
                    statusId = entry.statusId,
                    stacks = curStacks,
                    remaining = rem
                };
            }
        }
    }

    // ── Public Read API ───────────────────────────────────────────────────
    public float GetDamageTakenMult()
    {
        if (Statuses == null || Statuses.Count == 0) return 1.0f;

        float mult = 1.0f;
        for (int i = 0; i < Statuses.Count; i++)
        {
            var entry = Statuses[i];
            if (entry.stacks <= 0) continue;

            string idStr = entry.statusId.ToString();
            var data = MetaDatabase.Instance != null ? MetaDatabase.Instance.GetStatus(idStr) : null;
            if (data == null) continue;

            mult *= Mathf.Pow(data.damageTakenMultPerStack, entry.stacks);
        }

        return mult;
    }
}
