using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Menu Scene — จัดการปุ่ม Play Solo และ (ในอนาคต) Online
/// Play Solo = StartHost() โดยไม่มี Relay → โหลด Game Scene
/// </summary>
public class MenuManager : MonoBehaviour
{
    [Header("Buttons")]
    public Button playSoloButton;
    public Button onlineButton;

    [Header("Scene")]
    [Tooltip("ชื่อ Scene เกมที่จะโหลด (ต้องอยู่ใน Build Settings)")]
    public string gameSceneName = "SampleScene";

    [Header("Status")]
    public TextMeshProUGUI statusText;
    
    [Header("Online UI (Building Block)")]
    [Tooltip("GameObject ที่มี UIDocument ของ Building Block JoinByCode")]
    public GameObject onlinePanel;
    public GameObject MainPanel;// UIDocument GameObject — ซ่อนไว้ก่อน

    void Start()
    {
        playSoloButton.onClick.AddListener(OnPlaySolo);

        if (onlineButton != null)
            onlineButton.onClick.AddListener(OnOnlineClicked);

        // ซ่อน online panel ไว้ก่อน
        if (onlinePanel != null)
            onlinePanel.SetActive(false);
    }

    void OnPlaySolo()
    {
        playSoloButton.interactable = false;
        SetStatus("Starting host...");

        // StartHost แบบ local ไม่ผ่าน Relay
        NetworkManager.Singleton.StartHost();

        // โหลด Game Scene ผ่าน NetworkSceneManager
        // (ทำให้ NetworkObject spawn ถูกต้องใน scene ใหม่)
        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    // ── Online ────────────────────────────────────────────────────────────
    void OnOnlineClicked()
    {
        // สลับแสดง Building Block online panel
        if (onlinePanel != null)
        {
            onlinePanel.SetActive(!onlinePanel.activeSelf);
            MainPanel.SetActive(!MainPanel.activeSelf);
        }
        
    }

    void SetStatus(string msg)
    {
        if (statusText) statusText.text = msg;
    }
}
