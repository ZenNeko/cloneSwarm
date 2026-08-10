using UnityEditor;
using UnityEngine;

/// <summary>
/// ลาก center / radius ของสนามได้ในหน้า Scene แทนการพิมพ์ตัวเลข — ดู docs/adr-003
///
/// ArenaDefinition เป็น ScriptableObject ไม่มี transform และไม่ได้อยู่ในซีน จึงใช้ OnDrawGizmos ไม่ได้
/// ต้องเกาะ SceneView.duringSceneGui ตอนที่ asset ถูกเลือกอยู่
///
/// เหตุผลที่ต้องมี: anchor 25 จุด (รวมตำแหน่งนาฬิกา 1-12) มองไม่เห็นเลยถ้าไม่วาด
/// ต้องเดาว่า Clock3 กับ QuadrantNE ไปโผล่ตรงไหน ซึ่งเดาผิดแล้วหาสาเหตุยากมาก
/// </summary>
[CustomEditor(typeof(ArenaDefinition))]
public class ArenaDefinitionEditor : Editor
{
    const float AnchorDotSize = 0.35f;

    static bool showAnchors = true;
    static bool showClockAnchors = false;

    void OnEnable()  => SceneView.duringSceneGui += OnSceneGUI;
    void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Scene View", EditorStyles.boldLabel);
        showAnchors      = EditorGUILayout.Toggle("โชว์ anchor", showAnchors);
        using (new EditorGUI.DisabledScope(!showAnchors))
            showClockAnchors = EditorGUILayout.Toggle("รวมตำแหน่งนาฬิกา", showClockAnchors);

        EditorGUILayout.HelpBox(
            "ลากลูกศรเพื่อย้ายจุดศูนย์กลาง · ลากวงนอกเพื่อปรับรัศมี\n" +
            "อย่าลืมผูก asset นี้เข้า BossEncounterConfig.arena ไม่งั้น centerMode = Arena จะ fallback ไปตำแหน่งบอส",
            MessageType.Info);
    }

    void OnSceneGUI(SceneView sv)
    {
        var arena = target as ArenaDefinition;
        if (arena == null) return;

        // ── ขอบสนาม ──────────────────────────────────────────────────────
        Handles.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        if (arena.shape == ArenaShape.Square)
            Handles.DrawWireCube(arena.center, new Vector3(arena.radius * 2f, 0.05f, arena.radius * 2f));
        else
            Handles.DrawWireDisc(arena.center, Vector3.up, arena.radius);

        // ── anchor ───────────────────────────────────────────────────────
        if (showAnchors) DrawAnchors(arena);

        // ── handles ──────────────────────────────────────────────────────
        EditorGUI.BeginChangeCheck();

        Vector3 newCenter = Handles.PositionHandle(arena.center, Quaternion.identity);

        Handles.color = new Color(1f, 0.8f, 0.3f, 1f);
        float newRadius = Handles.RadiusHandle(Quaternion.identity, arena.center, arena.radius);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(arena, "Edit Arena");
            arena.center = newCenter;
            arena.radius = Mathf.Max(0.1f, newRadius);
            EditorUtility.SetDirty(arena);
        }

        Handles.Label(arena.center + Vector3.up * 0.5f,
            $"{arena.name}\n{arena.shape} · r = {arena.radius:0.#}");
    }

    void DrawAnchors(ArenaDefinition arena)
    {
        var labelStyle = new GUIStyle(EditorStyles.miniLabel);
        labelStyle.normal.textColor = Color.white;

        foreach (ArenaAnchor a in System.Enum.GetValues(typeof(ArenaAnchor)))
        {
            bool isClock = a.ToString().StartsWith("Clock");
            if (isClock && !showClockAnchors) continue;

            Vector3 p = ArenaAnchors.Resolve(arena, a, 1f);

            Handles.color = a == ArenaAnchor.Center ? Color.white
                          : isClock                 ? new Color(0.5f, 0.8f, 1f, 0.8f)
                                                    : new Color(0.4f, 1f, 0.6f, 0.9f);
            Handles.SphereHandleCap(0, p, Quaternion.identity, AnchorDotSize, EventType.Repaint);
            Handles.Label(p + Vector3.up * 0.4f, a.ToString(), labelStyle);
        }
    }
}
