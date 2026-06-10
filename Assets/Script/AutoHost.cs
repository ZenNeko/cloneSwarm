using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Dev-only auto-host — สำหรับเปิด test scene แบบกด Play ตรงๆ
///
/// **ใช้กับ:**
///   • WeaponTestScene
///   • Any scene ที่อยากเปิดเทสในเบบ standalone (ไม่ต้องผ่าน MenuScene)
///
/// **ทำงานยังไง:**
///   1. รอ 1 frame ให้ NetworkManager พร้อม
///   2. ถ้า NetworkManager ยังไม่ Start → `StartHost()` อัตโนมัติ
///   3. Player object spawn → ทดสอบได้เลย
///
/// **Setup:** วาง component นี้บน GameObject ใน scene (ที่ไหนก็ได้)
///
/// **⚠ Production:**
///   • ปิด `autoHost` ก่อน build, หรือลบ component ออก
///   • หรือเลือก scene ที่ไม่ตรง MenuScene เพื่อกัน accident
/// </summary>
public class AutoHost : MonoBehaviour
{
    [Tooltip("เปิด = StartHost ถ้า NetworkManager ยังไม่ start ตอน scene load")]
    public bool autoHost = true;

    [Tooltip("ชื่อ scene ที่ห้าม auto-host (กัน accident ใน MenuScene)\n" +
             "ถ้าตรงกับ scene ปัจจุบัน → skip")]
    public string[] skipInScenes = { "MenuScene" };

    [Tooltip("Delay ก่อน StartHost (วินาที) — เผื่อ services ยังไม่ init")]
    public float startDelay = 0.5f;

    IEnumerator Start()
    {
        if (!autoHost) yield break;

        // Skip ใน scene ที่ list ไว้
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        foreach (var skip in skipInScenes)
            if (currentScene == skip) yield break;

        yield return new WaitForSeconds(startDelay);

        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogWarning("[AutoHost] NetworkManager.Singleton not found in scene");
            yield break;
        }
        if (nm.IsListening) yield break;   // already started

        bool ok = nm.StartHost();
        Debug.Log(ok
            ? $"[AutoHost] ✓ Started host in '{currentScene}'"
            : $"[AutoHost] ✗ StartHost() failed in '{currentScene}'");
    }
}
