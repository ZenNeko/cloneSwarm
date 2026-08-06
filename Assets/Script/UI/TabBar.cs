using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[System.Serializable]
public class TabEntry
{
    public string id;          // "lobby" / "character" / "map" / "shop"
    public GameObject panel;       // panel ที่จะเปิด/ปิด
    public Button button;      // ปุ่มบนแถบ (คลิกได้ด้วย)
    public TextMeshProUGUI label;
    [HideInInspector] public bool visible = true;
}

public class TabBar : MonoBehaviour
{
    public List<TabEntry> tabs = new();
    public Color activeColor = Color.white;
    public Color inactiveColor = Color.gray;

    public static event System.Action<string> OnTabChanged;

    private string currentTabId;

    private void Start()
    {
        foreach (var tab in tabs)
        {
            if (tab == null) continue;
            string tabId = tab.id;
            if (tab.button != null)
            {
                tab.button.onClick.AddListener(() => Select(tabId));
            }
        }
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.qKey.wasPressedThisFrame)
        {
            Cycle(-1);
        }
        else if (keyboard.eKey.wasPressedThisFrame)
        {
            Cycle(1);
        }
    }

    public void SetTabVisible(string id, bool visible)
    {
        var tab = tabs.Find(t => t != null && t.id == id);
        if (tab == null) return;

        tab.visible = visible;
        if (tab.button != null)
        {
            tab.button.gameObject.SetActive(visible);
        }

        if (!visible && currentTabId == id)
        {
            Cycle(1);
        }
    }

    public void Select(string id)
    {
        var target = tabs.Find(t => t != null && t.id == id && t.visible);
        if (target == null) return;

        currentTabId = id;

        foreach (var tab in tabs)
        {
            if (tab == null) continue;
            bool isSelected = (tab.id == id && tab.visible);

            if (tab.panel != null)
            {
                tab.panel.SetActive(isSelected);
            }

            if (tab.label != null)
            {
                tab.label.color = isSelected ? activeColor : inactiveColor;
            }
        }

        OnTabChanged?.Invoke(id);
    }

    public void Cycle(int dir)
    {
        if (tabs == null || tabs.Count == 0) return;

        List<int> visibleIndices = new List<int>();
        for (int i = 0; i < tabs.Count; i++)
        {
            if (tabs[i] != null && tabs[i].visible)
            {
                visibleIndices.Add(i);
            }
        }

        if (visibleIndices.Count == 0) return;

        int currentVisiblePos = -1;
        for (int i = 0; i < visibleIndices.Count; i++)
        {
            if (tabs[visibleIndices[i]].id == currentTabId)
            {
                currentVisiblePos = i;
                break;
            }
        }

        if (currentVisiblePos == -1)
        {
            Select(tabs[visibleIndices[0]].id);
            return;
        }

        int nextVisiblePos = (currentVisiblePos + dir) % visibleIndices.Count;
        if (nextVisiblePos < 0) nextVisiblePos += visibleIndices.Count;

        Select(tabs[visibleIndices[nextVisiblePos]].id);
    }
}
