using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// ⚠ TEMPORARY — party HUD ชั่วคราว ตั้งใจให้รื้อทิ้งเมื่อมี UI จริง
///
/// วิธีลบ: ลบไฟล์นี้ไฟล์เดียว จบ
///   — ไม่มีใครอ้างอิงถึงคลาสนี้
///   — ไม่ได้วางไว้ในซีนหรือ prefab (bootstrap ตัวเองตอน runtime)
///   — ไม่มี prefab / asset ที่ต้องตามลบ
///
/// แสดงผู้เล่นทุกคนในห้องพร้อม HP ทั้งค่าฝั่ง server และค่า local
/// กด F3 เปิด/ปิด
/// </summary>
public class TempPartyHUD : MonoBehaviour
{
    private static TempPartyHUD _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("~TempPartyHUD");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<TempPartyHUD>();
    }

    private GameObject _panel;
    private Transform _rowsContainer;
    private bool _isVisible = true;
    private float _updateTimer;
    private const float UPDATE_INTERVAL = 0.25f;

    private readonly List<RowUI> _rowPool = new List<RowUI>();

    private class RowUI
    {
        public GameObject Root;
        public TextMeshProUGUI Text;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // 1. Create Canvas (ScreenSpaceOverlay, sortingOrder 500, no GraphicRaycaster to avoid blocking UI clicks)
        var canvasGo = new GameObject("PartyHUD_Canvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // 2. Create Panel (Top-Left)
        _panel = new GameObject("PartyHUD_Panel");
        _panel.transform.SetParent(canvasGo.transform, false);

        var panelImg = _panel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.55f);

        var panelRect = _panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(10f, -10f);

        var vlg = _panel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.spacing = 5f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var csf = _panel.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _rowsContainer = _panel.transform;

        // Hide initially until active players found
        _panel.SetActive(false);
    }

    private void Update()
    {
        // Toggle F3 key
        if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
        {
            _isVisible = !_isVisible;
            if (_panel != null)
            {
                _panel.SetActive(_isVisible);
            }
        }

        if (!_isVisible) return;

        _updateTimer += Time.deltaTime;
        if (_updateTimer >= UPDATE_INTERVAL)
        {
            _updateTimer = 0f;
            UpdatePartyHUD();
        }
    }

    private void UpdatePartyHUD()
    {
        var players = FindObjectsByType<playermove>(FindObjectsSortMode.None);
        if (players == null || players.Length == 0)
        {
            if (_panel != null) _panel.SetActive(false);
            return;
        }

        if (_isVisible && _panel != null)
        {
            _panel.SetActive(true);
        }

        var sortedPlayers = players.OrderBy(p => p.OwnerClientId).ToArray();

        // Ensure pool has enough rows
        for (int i = _rowPool.Count; i < sortedPlayers.Length; i++)
        {
            _rowPool.Add(CreateRowUI());
        }

        for (int i = 0; i < _rowPool.Count; i++)
        {
            if (i < sortedPlayers.Length)
            {
                _rowPool[i].Root.SetActive(true);
                PopulateRowText(_rowPool[i].Text, sortedPlayers[i]);
            }
            else
            {
                _rowPool[i].Root.SetActive(false);
            }
        }
    }

    private RowUI CreateRowUI()
    {
        var rowGo = new GameObject("PartyRow");
        rowGo.transform.SetParent(_rowsContainer, false);

        var text = rowGo.AddComponent<TextMeshProUGUI>();
        text.fontSize = 18f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Left;

        return new RowUI { Root = rowGo, Text = text };
    }

    private void PopulateRowText(TextMeshProUGUI textComp, playermove pm)
    {
        string hostTag = (NetworkManager.Singleton != null && pm.OwnerClientId == NetworkManager.ServerClientId) ? "[HOST] " : "";
        string youTag = pm.IsOwner ? "[YOU ] " : "";
        string tags = $"{hostTag}{youTag}".TrimEnd();
        if (!string.IsNullOrEmpty(tags)) tags += " ";

        string charName = "?";
        var visual = pm.GetComponent<PlayerVisual>();
        if (visual != null)
        {
            var cd = visual.GetCharacterData(visual.CharacterIndex);
            if (cd != null && !string.IsNullOrEmpty(cd.characterName))
            {
                charName = cd.characterName;
            }
        }

        float netHp = pm.netHealth.Value;
        float netMaxHp = pm.netMaxHealth.Value;
        float localMaxHp = pm.maxHealth;
        bool isDead = pm.isDead.Value;

        // netMaxHealth vs maxHealth: netMaxHealth is server-authoritative, maxHealth is client local prediction.
        // Displaying both allows instant verification of audit 1.1 stat sync correctness.

        string deadSuffix = isDead ? " [DEAD]" : "";
        string line = $"{tags}Player {pm.OwnerClientId} ({charName})  HP: {netHp:F0} / {netMaxHp:F0} (local {localMaxHp:F0}){deadSuffix}";

        textComp.text = line;
        textComp.color = isDead ? Color.gray : Color.white;
    }
}
