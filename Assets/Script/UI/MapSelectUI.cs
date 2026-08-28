using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// หน้าเลือกแมพ (แท็บ MAP) — โครงเดียวกับ CharacterSelectUI แต่เล็กกว่ามาก
/// เพราะแมพไม่มีระบบปลดล็อก ไม่มีทอง ไม่มีแท็บย่อย
///
/// รายชื่อแมพอ่านจาก LobbyUI.maps ไม่ถือลิสต์เอง — ไม่งั้นจะมีสองที่ที่ต้องจำอัปเดต
/// และ LobbyUI คือคนที่ส่งค่าขึ้นเน็ตเวิร์กอยู่แล้ว
///
/// วางบน Canvas ได้ (เหมือน CharacterSelectUI) ส่วน MapCarousel ต้องอยู่บน "แผงที่ลาก"
/// ซึ่งเป็น ancestor ตัวใดก็ได้เหนือ cardsContainer — เพราะ event ลาก/ลูกกลิ้งไปไม่ถึง Canvas
/// แต่ส่งถึงทุก ancestor ระหว่างทาง (ซีนจริงอยู่บน LeftPanel ไม่ใช่ Viewport)
/// </summary>
public class MapSelectUI : MonoBehaviour
{
    [Header("── Source ──────────────────────────────")]
    [Tooltip("อ่าน maps จากตัวนี้ และส่งการเลือกกลับผ่าน SelectMap ของมัน")]
    public LobbyUI lobbyUI;

    [Header("── Card List ───────────────────────────")]
    [Tooltip("Container ที่การ์ดจะถูกสร้างเข้าไป — MapCarousel จะไปอยู่บน parent ของมัน")]
    public Transform  cardsContainer;
    [Tooltip("Card template — MapCard prefab asset หรือ object ในซีนที่ถูกซ่อนให้อัตโนมัติ")]
    public GameObject cardTemplate;

    [Header("── Detail Panel ────────────────────────")]
    public Image           detailPreview;
    public TextMeshProUGUI detailName;
    public TextMeshProUGUI detailDesc;
    [Tooltip("ป้ายชื่อซีนที่จะโหลด — ปล่อยว่างได้")]
    public TextMeshProUGUI detailSceneName;

    [Header("── Card Colors ─────────────────────────")]
    public Color selectedColor = new Color(0.3f, 0.7f, 1f);
    public Color normalColor   = new Color(0.2f, 0.2f, 0.25f, 1f);

    [Header("── Debug ───────────────────────────────")]
    public bool verboseLog = true;

    private MapCarousel carousel;
    private MapData     selected;

    void Log(string msg) { if (verboseLog) Debug.Log("[MapSelect] " + msg); }

    static string Name(MapData m) => m != null ? m.displayName : "(null)";

    // ══════════════════════════════════════════════════════════════════════
    void Start()
    {
        if (lobbyUI == null) lobbyUI = GetComponent<LobbyUI>() ?? FindFirstObjectByType<LobbyUI>();
        if (lobbyUI == null)
        {
            Debug.LogError("[MapSelect] ไม่พบ LobbyUI — อ่านรายชื่อแมพไม่ได้และส่งการเลือกออกไม่ได้");
            return;
        }

        BuildCarousel();

        // เปิดมาให้ตรงกับแมพที่เลือกค้างไว้ ถ้าไม่มีก็ตัวแรก
        var start = RunSetup.Map;
        if (start == null || (carousel != null && carousel.IndexOf(start) < 0))
            start = lobbyUI.maps.Count > 0 ? lobbyUI.maps[0] : null;

        Log("Start — แมพที่ค้างอยู่ = '" + Name(RunSetup.Map) + "' → เริ่มที่ '" + Name(start) + "'");

        if (carousel != null && start != null)
        {
            int idx = carousel.IndexOf(start);
            // JumpTo ไม่ยิง OnSettled จึงไม่คอมมิตซ้ำกับบรรทัดล่าง
            if (idx >= 0) carousel.JumpTo(idx);
        }

        SelectMap(start, "เริ่มต้น");
    }

    void OnDestroy()
    {
        if (carousel != null) carousel.OnSettled -= OnCarouselSettled;
    }

    // ══════════════════════════════════════════════════════════════════════
    void BuildCarousel()
    {
        if (cardsContainer == null)
        {
            Debug.LogError("[MapSelect] cardsContainer ยังไม่ได้ assign!");
            return;
        }
        if (cardTemplate == null)
        {
            Debug.LogError("[MapSelect] cardTemplate ยังไม่ได้ assign!");
            return;
        }
        if (lobbyUI.maps == null || lobbyUI.maps.Count == 0)
        {
            Debug.LogWarning("[MapSelect] LobbyUI.maps ว่าง — ลาก MapData assets มาใส่ใน Inspector ของ LobbyUI");
            return;
        }

        var rect = cardsContainer as RectTransform;
        var host = rect != null ? rect.parent as RectTransform : null;
        if (host == null)
        {
            Debug.LogError("[MapSelect] cardsContainer ต้องเป็น RectTransform ที่มีพ่อเป็นแผง — วาง carousel ไม่ได้");
            return;
        }

        // ค้นขึ้นไปทั้งสาย ไม่ใช่แค่พ่อของ cardsContainer โดยตรง
        // ซีนจริงเป็น LeftPanel > Viewport > cardsContainer และ component อยู่บน LeftPanel
        // ตามที่ CarouselBase เขียนไว้เองว่า "ต้องวางบน object ของแผงที่จะลาก"
        // ของเดิมหาแค่ host (= Viewport) ไม่เจอ แล้ว AddComponent ตัวใหม่ที่ reference ว่าง
        // ทับไปเงียบๆ — ตัวที่ต่อสายไว้ใน Editor จึงไม่เคยถูก Setup เลย
        carousel = rect.GetComponentInParent<MapCarousel>(true);
        if (carousel == null)
        {
            carousel = host.gameObject.AddComponent<MapCarousel>();
            Debug.LogWarning("[MapSelect] ไม่พบ MapCarousel เหนือ '" + rect.name + "' ขึ้นไปเลย จึงเพิ่มให้บน '" +
                             host.name + "' ตอนรัน — ช่องภาพใหญ่กับปุ่มลูกศรจะว่าง ถ้าต้องการใช้ ให้เพิ่ม component นี้ใน Editor แล้วลาก reference");
        }

        carousel.OnSettled -= OnCarouselSettled;
        carousel.OnSettled += OnCarouselSettled;
        carousel.Setup(lobbyUI.maps, cardTemplate, rect, selectedColor, normalColor);
    }

    /// <summary>carousel เข้าช่องนิ่งแล้ว — จุดเดียวที่การหมุนกลายเป็นการเลือก</summary>
    void OnCarouselSettled(int index)
    {
        SelectMap(carousel != null ? carousel.GetMap(index) : null, "carousel");
    }

    // ══════════════════════════════════════════════════════════════════════
    void SelectMap(MapData map, string source)
    {
        Log("── SelectMap('" + Name(map) + "') · จาก: " + source + " ──");

        // เฉพาะ host เปลี่ยนแมพได้ — LobbyState ทิ้ง ServerRpc ของ client เงียบๆ
        // ถ้าปล่อยให้ commit เครื่อง client จะเห็นแมพหนึ่ง แต่เข้าเกมได้อีกแมพหนึ่ง
        // จึงดีด carousel กลับไปที่แมพที่ server ถืออยู่แทน
        if (lobbyUI != null && !lobbyUI.IsHost)
        {
            var serverMap = lobbyUI.NetworkSelectedMap;
            if (serverMap != null && serverMap != map)
            {
                Log("ไม่ใช่ host — ดีดกลับไปแมพของ host: '" + Name(serverMap) + "'");
                selected = serverMap;
                RefreshDetail();
                if (carousel != null)
                {
                    int back = carousel.IndexOf(serverMap);
                    // JumpTo ไม่ยิง OnSettled จึงไม่วนกลับเข้ามาที่นี่อีก
                    if (back >= 0) carousel.JumpTo(back);
                }
                return;
            }

            // server ยังไม่ได้เลือกแมพ — โชว์ในเครื่องได้ แต่ไม่ส่งขึ้นเน็ตเวิร์ก
            selected = map;
            RefreshDetail();
            return;
        }

        selected = map;
        RefreshDetail();

        if (map == null) return;

        // LobbyUI เป็นคนส่งขึ้นเน็ตเวิร์ก (RunSetup + SetMapServerRpc) — ไม่ทำซ้ำที่นี่
        if (lobbyUI != null) lobbyUI.SelectMap(map);
    }

    void RefreshDetail()
    {
        if (selected == null) return;

        // ภาพพรีวิวต้องมีเจ้าของเดียว — ถ้า carousel ถือภาพใหญ่อยู่ ปล่อยให้มันวาด
        bool previewOwnedByCarousel = carousel != null && carousel.previewImage != null;
        if (!previewOwnedByCarousel)
        {
            if (detailPreview != null)
            {
                detailPreview.sprite  = selected.previewImage;
                detailPreview.enabled = selected.previewImage != null;
            }
        }
        else if (detailPreview != null && detailPreview != carousel.previewImage)
        {
            detailPreview.enabled = false;
        }

        if (detailName      != null) detailName.text      = selected.displayName;
        if (detailDesc      != null) detailDesc.text      = selected.description;
        if (detailSceneName != null) detailSceneName.text = selected.sceneName;
    }
}
