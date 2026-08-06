using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public struct LobbyPlayerEntry : INetworkSerializable, System.IEquatable<LobbyPlayerEntry>
{
    public ulong clientId;
    public FixedString32Bytes characterName;   // ว่าง = ยังไม่เลือก
    public bool ready;

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref clientId);
        s.SerializeValue(ref characterName);
        s.SerializeValue(ref ready);
    }

    public bool Equals(LobbyPlayerEntry o)
        => clientId == o.clientId && characterName.Equals(o.characterName) && ready == o.ready;
}

public class LobbyState : NetworkBehaviour
{
    public static LobbyState Instance { get; private set; }

    public NetworkVariable<FixedString64Bytes> SelectedMapId =
        new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<DifficultyTier> SelectedDifficulty =
        new(DifficultyTier.Normal, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkList<LobbyPlayerEntry> Players;   // new() ใน Awake เท่านั้น

    public static event System.Action OnLobbyChanged;   // UI subscribe

    private void Awake()
    {
        Players = new NetworkList<LobbyPlayerEntry>();
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;

        SelectedMapId.OnValueChanged += OnMapChanged;
        SelectedDifficulty.OnValueChanged += OnDifficultyChanged;
        Players.OnListChanged += OnPlayersListChanged;

        if (IsServer)
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }

            AddPlayerEntry(NetworkManager.ServerClientId);

            if (NetworkManager.Singleton != null)
            {
                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    if (client.ClientId != NetworkManager.ServerClientId)
                    {
                        AddPlayerEntry(client.ClientId);
                    }
                }
            }
        }

        OnLobbyChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        SelectedMapId.OnValueChanged -= OnMapChanged;
        SelectedDifficulty.OnValueChanged -= OnDifficultyChanged;
        if (Players != null)
        {
            Players.OnListChanged -= OnPlayersListChanged;
        }

        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnMapChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue) => OnLobbyChanged?.Invoke();
    private void OnDifficultyChanged(DifficultyTier previousValue, DifficultyTier newValue) => OnLobbyChanged?.Invoke();
    private void OnPlayersListChanged(NetworkListEvent<LobbyPlayerEntry> changeEvent) => OnLobbyChanged?.Invoke();

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        AddPlayerEntry(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        RemovePlayerEntry(clientId);
    }

    private void AddPlayerEntry(ulong clientId)
    {
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].clientId == clientId) return;
        }

        Players.Add(new LobbyPlayerEntry
        {
            clientId = clientId,
            characterName = default,
            ready = false
        });
    }

    private void RemovePlayerEntry(ulong clientId)
    {
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].clientId == clientId)
            {
                Players.RemoveAt(i);
                break;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetCharacterServerRpc(FixedString32Bytes name, ServerRpcParams p = default)
    {
        ulong sender = p.Receive.SenderClientId;
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].clientId == sender)
            {
                var entry = Players[i];
                entry.characterName = name;
                entry.ready = false;
                Players[i] = entry;
                break;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetReadyServerRpc(bool ready, ServerRpcParams p = default)
    {
        ulong sender = p.Receive.SenderClientId;
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].clientId == sender)
            {
                var entry = Players[i];
                entry.ready = ready;
                Players[i] = entry;
                break;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetMapServerRpc(FixedString64Bytes mapId, ServerRpcParams p = default)
    {
        if (p.Receive.SenderClientId != NetworkManager.ServerClientId) return;

        SelectedMapId.Value = mapId;
        ResetAllReady();
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetDifficultyServerRpc(DifficultyTier tier, ServerRpcParams p = default)
    {
        if (p.Receive.SenderClientId != NetworkManager.ServerClientId) return;

        SelectedDifficulty.Value = tier;
        ResetAllReady();
    }

    private void ResetAllReady()
    {
        for (int i = 0; i < Players.Count; i++)
        {
            var entry = Players[i];
            entry.ready = false;
            Players[i] = entry;
        }
    }

    public bool AllReady()
    {
        if (Players == null || Players.Count == 0) return false;
        for (int i = 0; i < Players.Count; i++)
        {
            if (!Players[i].ready) return false;
        }
        return true;
    }

    public bool TryGetEntry(ulong id, out LobbyPlayerEntry e)
    {
        if (Players != null)
        {
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].clientId == id)
                {
                    e = Players[i];
                    return true;
                }
            }
        }
        e = default;
        return false;
    }
}
