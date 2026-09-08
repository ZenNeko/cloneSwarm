using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// จุดเดียวที่รู้ว่าสกิลช่องไหนผูกกับปุ่มอะไร
///
/// binding ทั้งหมดอยู่ใน <c>Assets/ScriptableObjects/Resources/AbilityInputActions.inputactions</c>
/// เปิดด้วย Input Actions editor ของ Unity แล้วลากเปลี่ยนปุ่มได้เลย — ไม่ต้องแตะโค้ด
/// ไม่ต้องแตะ prefab และไม่ต้องแก้ทีละ ability
///
/// ชื่อ action ตรงกับ <c>IHUDAbility.HUDSlotKey</c> ตรงๆ ("SlotQ" / "SlotE" / "SlotR")
/// AbilityBase จึงหา action ของตัวเองได้เองโดยไม่ต้องมีช่องให้ตั้งใน Inspector
///
/// โหลดผ่าน Resources แบบเดียวกับ MetaDatabase — asset ตัวเดียวใช้ได้ทั้งซีนเมนูและซีนเกม
/// และรอดข้าม scene load เพราะเป็น asset ไม่ใช่ object ในซีน
/// </summary>
public static class AbilityInput
{
    const string ResourcePath = "AbilityInputActions";

    static InputActionAsset _asset;
    static bool             _warned;

    static InputActionAsset Asset
    {
        get
        {
            if (_asset != null) return _asset;

            _asset = Resources.Load<InputActionAsset>(ResourcePath);
            if (_asset == null)
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogError(
                        "[AbilityInput] ไม่พบ Assets/ScriptableObjects/Resources/AbilityInputActions.inputactions " +
                        "— สกิลทุกตัวจะกดไม่ติด");
                }
                return null;
            }

            // เปิดทั้ง asset ครั้งเดียว — ability อ่านแบบ polling (WasPressedThisFrame)
            // ไม่ได้ subscribe callback จึงไม่ต้องเปิด/ปิดตามอายุของแต่ละ ability
            _asset.Enable();
            return _asset;
        }
    }

    /// <summary>หา action ของช่องสกิล — slotKey คือค่าเดียวกับ IHUDAbility.HUDSlotKey ("Q"/"E"/"R")</summary>
    public static InputAction Find(string slotKey)
    {
        if (string.IsNullOrEmpty(slotKey)) return null;
        var a = Asset;
        return a != null ? a.FindAction("Slot" + slotKey, false) : null;
    }

    /// <summary>
    /// true = ตอนนี้ห้ามรับ input ของสกิล
    ///
    /// ต้องเช็ค IsPaused ด้วย ไม่ใช่แค่ LocalInputSuspended — PauseReason.PhaseSelect
    /// (จอเลือกการ์ด level up) กับ GameOver เซ็ตแค่ timeScale = 0 ไม่ได้เซ็ต
    /// LocalInputSuspended และ Update() ยังเดินต่อตอน timeScale = 0
    /// พอสกิลย้ายมาอยู่ปุ่มเมาส์ ทุกครั้งที่คลิกการ์ดจะกลายเป็นการร่ายสกิลไปด้วย
    /// </summary>
    public static bool Blocked => GamePause.IsPaused || GamePause.LocalInputSuspended;
}
