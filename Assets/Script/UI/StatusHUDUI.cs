using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CloneSwarm.Meta;

public class StatusHUDUI : MonoBehaviour
{
    [Header("Container & Template")]
    public Transform  container;
    public GameObject entryTemplate;

    private playermove _localPlayer;
    private PlayerStatusManager _statusManager;
    private readonly List<EntryItem> _spawnedEntries = new();

    private class EntryItem
    {
        public GameObject root;
        public Image icon;
        public Image timerFill;
        public TextMeshProUGUI stackText;
        public string statusId;
        public float duration;
    }

    private void Awake()
    {
        if (entryTemplate != null) entryTemplate.SetActive(false);
    }

    private void OnEnable()
    {
        playermove.OnLocalPlayerSpawned      += OnLocalPlayerSpawned;
        PlayerStatusManager.OnAnyStatusChanged += OnStatusChanged;

        FindAndHookLocalPlayer();
    }

    private void OnDisable()
    {
        playermove.OnLocalPlayerSpawned      -= OnLocalPlayerSpawned;
        PlayerStatusManager.OnAnyStatusChanged -= OnStatusChanged;

        ClearEntries();
    }

    private void OnLocalPlayerSpawned(Transform playerTransform)
    {
        if (playerTransform == null) return;

        _localPlayer = playerTransform.GetComponent<playermove>();
        if (_localPlayer == null) return;

        HookStatusManager();
        RefreshUI();
    }

    private void OnStatusChanged()
    {
        RefreshUI();
    }

    private void FindAndHookLocalPlayer()
    {
        if (_localPlayer != null) return;

        var players = FindObjectsByType<playermove>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p != null && p.IsOwner)
            {
                _localPlayer = p;
                break;
            }
        }

        HookStatusManager();
        RefreshUI();
    }

    private void HookStatusManager()
    {
        if (_localPlayer != null)
        {
            _statusManager = _localPlayer.GetComponent<PlayerStatusManager>();
        }
    }

    private void ClearEntries()
    {
        foreach (var item in _spawnedEntries)
        {
            if (item.root != null) Destroy(item.root);
        }
        _spawnedEntries.Clear();
    }

    public void RefreshUI()
    {
        ClearEntries();

        if (_statusManager == null) HookStatusManager();
        if (_statusManager == null || _statusManager.Statuses == null) return;
        if (container == null || entryTemplate == null) return;

        for (int i = 0; i < _statusManager.Statuses.Count; i++)
        {
            var entry = _statusManager.Statuses[i];
            string idStr = entry.statusId.ToString();
            var data = MetaDatabase.Instance != null ? MetaDatabase.Instance.GetStatus(idStr) : null;
            if (data == null) continue;

            var go = Instantiate(entryTemplate, container);
            go.SetActive(true);

            Image iconImg = null;
            Image fillImg = null;
            TextMeshProUGUI txt = null;

            var entryUI = go.GetComponent<StatusEntryUI>();
            if (entryUI != null)
            {
                iconImg = entryUI.icon;
                fillImg = entryUI.timerFill;
                txt = entryUI.stackText;
            }
            else
            {
                var images = go.GetComponentsInChildren<Image>(true);
                foreach (var img in images)
                {
                    if (img.gameObject.name.ToLower().Contains("fill") || img.type == Image.Type.Filled)
                        fillImg = img;
                    else if (iconImg == null)
                        iconImg = img;
                }
                txt = go.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (iconImg != null && data.icon != null) iconImg.sprite = data.icon;
            if (txt != null) txt.text = entry.stacks > 1 ? entry.stacks.ToString() : "";
            if (fillImg != null) fillImg.fillAmount = data.duration > 0f ? Mathf.Clamp01(entry.remaining / data.duration) : 1f;

            _spawnedEntries.Add(new EntryItem
            {
                root = go,
                icon = iconImg,
                timerFill = fillImg,
                stackText = txt,
                statusId = idStr,
                duration = data.duration
            });
        }
    }

    private void Update()
    {
        if (_statusManager == null || _statusManager.Statuses == null) return;

        for (int i = 0; i < _spawnedEntries.Count && i < _statusManager.Statuses.Count; i++)
        {
            var item = _spawnedEntries[i];
            var entry = _statusManager.Statuses[i];
            if (item.timerFill != null && item.duration > 0f)
            {
                item.timerFill.fillAmount = Mathf.Clamp01(entry.remaining / item.duration);
            }
            if (item.stackText != null)
            {
                item.stackText.text = entry.stacks > 1 ? entry.stacks.ToString() : "";
            }
        }
    }
}

public class StatusEntryUI : MonoBehaviour
{
    public Image icon;
    public Image timerFill;
    public TextMeshProUGUI stackText;
}
