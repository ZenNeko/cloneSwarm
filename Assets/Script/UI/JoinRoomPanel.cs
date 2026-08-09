using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// แผงเข้าห้องตัวเดียวที่ใช้ร่วมกันทั้งเมนูหลักและล็อบบี้
///
/// วางเป็น modal ลอยทับ — ไม่ใช่ลูกของ mainPanel/lobbyPanel และไม่อยู่ในกลุ่ม
/// MenuManager.ShowPanel() ที่ปิดกันเอง
///
/// สคริปต์นี้อยู่บน GameObject ที่ active ตลอด · panelRoot เป็นลูกที่เปิด/ปิด
/// (แบบเดียวกับ WinLoseUI) ถ้าเอาไปแปะบนตัวที่ปิดอยู่ Awake ไม่ทำงาน
/// Instance เป็น null แล้วปุ่ม Join ตายเงียบทั้งสองที่
/// </summary>
public class JoinRoomPanel : MonoBehaviour
{
    public static JoinRoomPanel Instance { get; private set; }

    /// <summary>เข้าห้องสำเร็จ — MenuManager สลับไปหน้าล็อบบี้</summary>
    public static event System.Action OnJoined;
    /// <summary>เข้าห้องไม่สำเร็จ "หลังจากปิด network ไปแล้ว" — ใครเปิดล็อบบี้อยู่ต้องกู้ offline host คืน</summary>
    public static event System.Action OnJoinFailed;

    [Header("Panel")]
    [Tooltip("ลูกที่เปิด/ปิด — ห้ามใส่ตัวเดียวกับ GameObject ที่แปะสคริปต์นี้")]
    public GameObject panelRoot;

    [Header("Widgets")]
    public TMP_InputField  codeInput;
    public Button          confirmButton;
    public Button          cancelButton;
    public TextMeshProUGUI statusText;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    void OnEnable()  => GameSessionManager.OnStatus += HandleStatus;
    void OnDisable() => GameSessionManager.OnStatus -= HandleStatus;

    void Start()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
        if (cancelButton  != null) cancelButton.onClick.AddListener(Close);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void HandleStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }

    public void Open()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        if (statusText != null) statusText.text = "";
        if (codeInput != null)
        {
            codeInput.text = "";
            codeInput.Select();
            codeInput.ActivateInputField();
        }
        SetInteractable(true);
    }

    public void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    void SetInteractable(bool on)
    {
        if (confirmButton != null) confirmButton.interactable = on;
        if (cancelButton  != null) cancelButton.interactable  = on;
        if (codeInput     != null) codeInput.interactable     = on;
    }

    async void OnConfirmClicked()
    {
        if (codeInput == null || string.IsNullOrWhiteSpace(codeInput.text)) return;

        var gsm = GameSessionManager.Instance;
        if (gsm == null || gsm.IsBusy) return;

        SetInteractable(false);

        // เข้าจากล็อบบี้ = มี offline host รันอยู่ ต้องปิดก่อนถึงจะเป็น client ได้
        // เข้าจากเมนูหลัก = ยังไม่ listening สามบรรทัดนี้เป็น no-op
        await gsm.LeaveSessionIfActiveAsync();
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening) nm.Shutdown();
        while (nm != null && nm.IsListening) await System.Threading.Tasks.Task.Yield();

        bool ok = await gsm.JoinSessionAsync(codeInput.text.Trim());

        if (!ok)
        {
            // ข้อความ error มาทาง OnStatus แล้ว — เปิดปุ่มคืนให้ลองใหม่หรือกดยกเลิก
            SetInteractable(true);
            OnJoinFailed?.Invoke();
            return;
        }

        Close();
        OnJoined?.Invoke();
    }
}
