using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// MapData Inspector = เส้นเวลาของรัน (MapTimelineGUI) + ช่องเดิมทั้งหมดข้างล่าง
    /// เส้นเวลาวาดด้วย IMGUI เพื่อใช้ชุดเดียวกับ Balance Tool (Odin) — ดูเหตุผลใน MapTimelineGUI
    /// </summary>
    [CustomEditor(typeof(MapData))]
    public class MapDataEditor : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            var timeline = new IMGUIContainer(() => MapTimelineGUI.Draw((MapData)target));
            timeline.style.marginBottom = 10;
            root.Add(timeline);

            var def = new VisualElement();
            InspectorElement.FillDefaultInspector(def, serializedObject, this);
            root.Add(def);

            // แก้ในลิสต์ข้างล่าง → เส้นเวลาวาดใหม่ทันที (IMGUI ไม่รู้เองจนกว่าจะมี event)
            root.TrackSerializedObjectValue(serializedObject, _ => timeline.MarkDirtyRepaint());
            return root;
        }
    }
}
