using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// ต่อ <c>UpgradeCardUI.typeLabel</c> ให้ prefab การ์ด
    /// เมนู: Tools > Clone Swarm > Wire Card Type Label
    ///
    /// ═══ ป้ายนี้เคยมีแต่ตัว ไม่มีใครเขียน ═══
    ///
    /// `Header/TypeLabel` ถูกสร้างไว้ตั้งแต่แรกและ **ตัวสร้างอบข้อความตัวอย่างใส่**
    /// (WEAPON / SUPER / STAT) ซึ่งทำให้ภาพเรนเดอร์ดูถูกต้องมาตลอด
    /// แต่ `UpgradeCardUI` ไม่เคยมีช่องให้มันเลย — ตอนรันจริงจึงว่างเปล่าทุกใบ
    ///
    /// **นี่คือกับดักของการตรวจด้วยภาพอย่างเดียว**: ของที่อบไว้ใน prefab กับของที่
    /// โค้ดเขียนตอนรัน เป็นคนละเรื่องกัน และภาพ edit mode แยกสองอย่างนี้ไม่ออก
    ///
    /// ═══ ทำไมต้องมีปุ่มแยก ═══
    ///
    /// <see cref="P3RLevelUpSceneBuilder"/> ต่อให้อยู่แล้วตอนสร้างการ์ดใหม่ แต่การ
    /// สร้างใหม่เขียนทับทั้งใบ รวมถึงของที่แก้ด้วยมือใน prefab · ตัวนี้แตะช่องเดียว
    ///
    /// รันซ้ำได้ — ต่อไว้แล้วก็บอกว่าไม่มีอะไรต้องทำ
    /// </summary>
    public static class P3RCardTypeLabelWirer
    {
        private const string CardPrefab = "Assets/Prefab/UI/P3R/LevelUpCard.prefab";
        private const string LabelPath  = "Header/TypeLabel";

        [MenuItem("Tools/Clone Swarm/Wire Card Type Label")]
        public static void Wire()
        {
            var root = PrefabUtility.LoadPrefabContents(CardPrefab);
            if (root == null)
            {
                Debug.LogError($"[ป้ายชนิดการ์ด] เปิด {CardPrefab} ไม่ได้");
                return;
            }

            try
            {
                var log = new StringBuilder($"[ป้ายชนิดการ์ด] {CardPrefab}\n");

                var card = root.GetComponent<UpgradeCardUI>();
                if (card == null)
                {
                    Debug.LogError("[ป้ายชนิดการ์ด] ไม่มี UpgradeCardUI บน root ของ prefab");
                    return;
                }

                // หาตามเส้นทางที่ตัวสร้างวางไว้ก่อน แล้วค่อยถอยไปหาตามชื่อทั้งใบ
                // — prefab ที่ถูกจัดด้วยมืออาจย้ายมันไปอยู่ใต้ของอื่นแล้ว
                var t = root.transform.Find(LabelPath)
                     ?? root.GetComponentsInChildren<Transform>(true)
                            .FirstOrDefault(x => x.name == "TypeLabel");

                var label = t != null ? t.GetComponent<TMPro.TextMeshProUGUI>() : null;
                if (label == null)
                {
                    Debug.LogError($"[ป้ายชนิดการ์ด] หา '{LabelPath}' ที่เป็น TMP ไม่เจอในการ์ด");
                    return;
                }

                if (card.typeLabel == label)
                {
                    Debug.Log(log.AppendLine("  ต่อไว้แล้ว ไม่มีอะไรต้องทำ").ToString());
                    return;
                }

                card.typeLabel = label;
                log.AppendLine($"  typeLabel → {LabelPath}");

                PrefabUtility.SaveAsPrefabAsset(root, CardPrefab);
                log.AppendLine("  บันทึกแล้ว");
                Debug.Log(log.ToString());
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
