using Unity.Netcode;
using UnityEngine;

public class PlayerSlotRegistry : NetworkBehaviour
{
    public static PlayerSlotRegistry Instance { get; private set; }

    // index in list = slot (0..3), value = clientId (ulong.MaxValue = empty)
    public NetworkList<ulong> Slots;

    private void Awake()
    {
        Slots = new NetworkList<ulong>();
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;

        if (IsServer)
        {
            // Initialize 4 empty slots if empty
            if (Slots.Count == 0)
            {
                for (int i = 0; i < 4; i++)
                {
                    Slots.Add(ulong.MaxValue);
                }
            }

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

                // Assign host and existing clients
                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    AssignSlot(client.ClientId);
                }
            }
        }
    }

    public override void OnNetworkDespawn()
    {
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

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        AssignSlot(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        ReleaseSlot(clientId);
    }

    private void AssignSlot(ulong clientId)
    {
        // Check if already assigned
        for (int i = 0; i < Slots.Count; i++)
        {
            if (Slots[i] == clientId) return;
        }

        // Find first empty slot
        for (int i = 0; i < Slots.Count; i++)
        {
            if (Slots[i] == ulong.MaxValue)
            {
                Slots[i] = clientId;
                break;
            }
        }
    }

    private void ReleaseSlot(ulong clientId)
    {
        for (int i = 0; i < Slots.Count; i++)
        {
            if (Slots[i] == clientId)
            {
                Slots[i] = ulong.MaxValue;
                break;
            }
        }
    }

    public int GetSlot(ulong clientId)
    {
        if (Slots != null)
        {
            for (int i = 0; i < Slots.Count; i++)
            {
                if (Slots[i] == clientId) return i;
            }
        }
        return -1;
    }

    public bool TryGetSlot(ulong clientId, out int slot)
    {
        slot = GetSlot(clientId);
        return slot >= 0;
    }
}
