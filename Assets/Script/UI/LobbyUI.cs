using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CloneSwarm.Meta;

public class LobbyUI : MonoBehaviour
{
    [Header("TabBar & Maps")]
    public TabBar tabBar;
    public List<MapData> maps = new();

    [Header("Party UI")]
    public Transform partyContainer;
    public GameObject partyRowTemplate;

    [Header("Controls")]
    public Button readyButton;
    public TextMeshProUGUI readyButtonText;
    public Button startRunButton;

    [Header("Selection Labels")]
    public TextMeshProUGUI mapNameLabel;
    public TextMeshProUGUI difficultyLabel;

    private readonly List<GameObject> spawnedPartyRows = new();

    private void Start()
    {
        if (readyButton != null)
        {
            readyButton.onClick.AddListener(OnReadyButtonClicked);
        }

        if (startRunButton != null)
        {
            startRunButton.onClick.AddListener(OnStartRunClicked);
        }
    }

    private void OnEnable()
    {
        LobbyState.OnLobbyChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        LobbyState.OnLobbyChanged -= Refresh;
    }

    public void SelectCharacter(string characterName)
    {
        if (LobbyState.Instance != null)
        {
            LobbyState.Instance.SetCharacterServerRpc(characterName);
        }
    }

    public void SelectMap(MapData map)
    {
        if (LobbyState.Instance != null && map != null)
        {
            LobbyState.Instance.SetMapServerRpc(map.mapId);
        }
    }

    public void SelectDifficulty(DifficultyTier tier)
    {
        if (LobbyState.Instance != null)
        {
            LobbyState.Instance.SetDifficultyServerRpc(tier);
        }
    }

    private void OnReadyButtonClicked()
    {
        if (LobbyState.Instance == null || NetworkManager.Singleton == null) return;

        ulong localId = NetworkManager.Singleton.LocalClientId;
        if (LobbyState.Instance.TryGetEntry(localId, out var entry))
        {
            LobbyState.Instance.SetReadyServerRpc(!entry.ready);
        }
    }

    private void OnStartRunClicked()
    {
        if (LobbyState.Instance == null) return;

        MapData selectedMap = GetSelectedMap(LobbyState.Instance.SelectedMapId.Value.ToString());
        DifficultyTier selectedTier = LobbyState.Instance.SelectedDifficulty.Value;

        RunSetup.Set(selectedMap, selectedTier);

        string sceneName = selectedMap != null ? selectedMap.sceneName : "SampleScene";
        if (GameSessionManager.Instance != null)
        {
            GameSessionManager.Instance.StartGame(sceneName);
        }
        else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    public void Refresh()
    {
        bool isHost = (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost) ||
                     (GameSessionManager.Instance != null && GameSessionManager.Instance.IsHost);

        if (tabBar != null)
        {
            tabBar.SetTabVisible("map", isHost);
        }

        if (LobbyState.Instance == null) return;

        if (partyContainer != null && partyRowTemplate != null)
        {
            partyRowTemplate.SetActive(false);
            foreach (var row in spawnedPartyRows)
            {
                if (row != null) Destroy(row);
            }
            spawnedPartyRows.Clear();

            for (int i = 0; i < LobbyState.Instance.Players.Count; i++)
            {
                var entry = LobbyState.Instance.Players[i];
                var go = Instantiate(partyRowTemplate, partyContainer);
                go.SetActive(true);
                spawnedPartyRows.Add(go);

                string charName = entry.characterName.ToString();
                CharacterData charData = !string.IsNullOrEmpty(charName) && MetaDatabase.Instance != null
                    ? MetaDatabase.Instance.GetCharacter(charName)
                    : null;

                var textComp = go.GetComponentInChildren<TextMeshProUGUI>();
                if (textComp != null)
                {
                    string displayName = charData != null ? charData.characterName : (string.IsNullOrEmpty(charName) ? "Selecting..." : charName);
                    textComp.text = $"Player {entry.clientId}: {displayName} [{(entry.ready ? "READY" : "NOT READY")}]";
                }
            }
        }

        if (NetworkManager.Singleton != null && LobbyState.Instance.TryGetEntry(NetworkManager.Singleton.LocalClientId, out var myEntry))
        {
            if (readyButtonText != null)
            {
                readyButtonText.text = myEntry.ready ? "CANCEL READY" : "READY";
            }
        }

        string mapId = LobbyState.Instance.SelectedMapId.Value.ToString();
        MapData map = GetSelectedMap(mapId);
        if (mapNameLabel != null)
        {
            mapNameLabel.text = map != null ? map.displayName : (string.IsNullOrEmpty(mapId) ? "Select Map" : mapId);
        }
        if (difficultyLabel != null)
        {
            difficultyLabel.text = LobbyState.Instance.SelectedDifficulty.Value.ToString();
        }

        if (startRunButton != null)
        {
            startRunButton.interactable = isHost && LobbyState.Instance.AllReady();
        }
    }

    private MapData GetSelectedMap(string mapId)
    {
        if (maps == null || string.IsNullOrEmpty(mapId)) return null;
        return maps.Find(m => m != null && m.mapId == mapId);
    }
}
