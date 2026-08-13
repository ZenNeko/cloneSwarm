using UnityEditor;
using UnityEngine;

/// <summary>
/// ซ่อน field ที่ยังไม่มีผลกับค่าที่ตั้งอยู่ แล้วค่อยโผล่เมื่อเปิดใช้
///
/// `SpawnAoEActionBase` มี field ราว 23 ตัว แต่ส่วนใหญ่ไม่มีผลพร้อมกัน —
/// ตั้ง targetingMode = BossPosition แล้ว arenaAnchor / anchorChoices ก็ไม่ถูกอ่านเลย
/// การโชว์ทั้งหมดตลอดเวลาทำให้หาอันที่ต้องแก้จริงยาก และชวนให้ตั้งค่าที่ไม่มีผลแล้วงงว่าทำไมไม่เปลี่ยน
///
/// ทำงานทั้งใน Inspector ปกติและในแผงขวาของ Boss Designer (InspectorElement ใช้ custom editor ให้อยู่แล้ว)
/// </summary>
[CustomEditor(typeof(SpawnAoEActionBase), editorForChildClasses: true)]
public class SpawnAoEActionEditor : Editor
{
    const string ExpandPrefKey   = "BossDesigner.ShowExpandSweep";
    const string CastPrefKey     = "BossDesigner.ShowCastBar";
    const string RepeatPrefKey   = "BossDesigner.ShowRepeat";
    const string FfxivPrefKey    = "BossDesigner.ShowFfxivSpecial";
    const string KnockPrefKey    = "BossDesigner.ShowKnockback";

    /// <summary>
    /// หัวข้อกลุ่มที่ยุบได้ — ปิดอยู่ก็ยุบให้พ้นทาง ใช้อยู่ก็กางเองพร้อมสรุปค่าให้เห็นโดยไม่ต้องกาง
    /// จำสถานะกางไว้ใน EditorPrefs ต่อกลุ่ม จะได้ไม่ต้องกางใหม่ทุกครั้งที่เลือก asset อื่น
    /// </summary>
    static bool DrawGroupHeader(string prefKey, string title, bool active, string summary)
    {
        bool show = EditorPrefs.GetBool(prefKey, false) || active;
        EditorGUILayout.Space(4);
        bool newShow = EditorGUILayout.Foldout(
            show, active ? $"{title}  ({summary})" : $"{title}  (ปิดอยู่)", true);
        if (newShow != show) EditorPrefs.SetBool(prefKey, newShow);
        return newShow;
    }

    /// <summary>สรุปว่าเปิดอะไรอยู่บ้าง จะได้เห็นโดยไม่ต้องกางกลุ่ม</summary>
    static string FfxivSummary(bool chase, SerializedProperty follow, SerializedProperty stack, SerializedProperty gaze)
    {
        var on = new System.Collections.Generic.List<string>();
        if (chase)                                 on.Add("Chase");
        if (follow != null && follow.boolValue)    on.Add("FollowCaster");
        if (stack  != null && stack.boolValue)     on.Add("Stack");
        if (gaze   != null && gaze.boolValue)      on.Add("Gaze");
        return on.Count > 0 ? string.Join(", ", on) : "-";
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var targetingMode  = serializedObject.FindProperty("targetingMode");
        var arenaAnchor    = serializedObject.FindProperty("arenaAnchor");
        var rollName       = serializedObject.FindProperty("rollName");
        var castName       = serializedObject.FindProperty("castName");
        var repeatCount    = serializedObject.FindProperty("repeatCount");
        var isChasing      = serializedObject.FindProperty("isChasing");
        var knockbackDist  = serializedObject.FindProperty("knockbackDistance");
        var knockbackMode  = serializedObject.FindProperty("knockbackMode");
        var scaleStart     = serializedObject.FindProperty("scaleStart");
        var scaleEnd       = serializedObject.FindProperty("scaleEnd");
        var sweep          = serializedObject.FindProperty("sweepDegreesPerSecond");
        var castTime       = serializedObject.FindProperty("castTime");
        var repeatInterval = serializedObject.FindProperty("repeatInterval");
        var rerollRepeat   = serializedObject.FindProperty("rerollEachRepeat");
        var followCaster   = serializedObject.FindProperty("followCaster");
        var isRotating     = serializedObject.FindProperty("isRotatingChase");
        var isStack        = serializedObject.FindProperty("isStackMarker");
        var isGaze         = serializedObject.FindProperty("isGaze");
        var knockbackDur   = serializedObject.FindProperty("knockbackDuration");
        var knockbackFixed = serializedObject.FindProperty("knockbackFixedDirection");

        bool useArenaAnchor = targetingMode != null
            && (SpawnAoEActionBase.TargetingMode)targetingMode.enumValueIndex
               == SpawnAoEActionBase.TargetingMode.ArenaAnchor;
        // ArenaAnchors.Resolve คืนจุดศูนย์กลางทันทีเมื่อ anchor = Center — distanceScale ไม่ถูกอ่านเลย
        bool useDistScale   = useArenaAnchor && arenaAnchor != null
            && (ArenaAnchor)arenaAnchor.enumValueIndex != global::ArenaAnchor.Center;
        // anchorChoices ถูกอ่านเฉพาะตอนมี roll มาเลือกให้
        bool useAnchorList  = useArenaAnchor && rollName != null && !string.IsNullOrEmpty(rollName.stringValue);
        // AttackLoop เช็ค castTime > 0 && castName ไม่ว่าง — ไม่มีชื่อก็ไม่มี cast bar
        bool useCastTime    = castName != null && !string.IsNullOrEmpty(castName.stringValue);
        bool useRepeat      = repeatCount   != null && repeatCount.intValue > 1;
        bool useChase       = isChasing     != null && isChasing.boolValue;
        bool useKnockback   = knockbackDist != null && knockbackDist.floatValue > 0f;
        bool useFixedDir    = useKnockback && knockbackMode != null
            && (KnockbackMode)knockbackMode.enumValueIndex == KnockbackMode.FixedDirection;

        bool expandActive = (scaleStart != null && !Mathf.Approximately(scaleStart.floatValue, 1f))
                         || (scaleEnd   != null && !Mathf.Approximately(scaleEnd.floatValue, 1f))
                         || (sweep      != null && Mathf.Abs(sweep.floatValue) > 0.001f);

        var overrideCol = serializedObject.FindProperty("overrideTelegraphColors");
        var warnCol     = serializedObject.FindProperty("telegraphWarningColor");
        var dangerCol   = serializedObject.FindProperty("telegraphDangerColor");
        bool useOverrideColors = overrideCol != null && overrideCol.boolValue;

        var overrideOutline = serializedObject.FindProperty("overrideTelegraphOutlineColor");
        var outlineCol      = serializedObject.FindProperty("telegraphOutlineColor");
        bool useOverrideOutlineColor = overrideOutline != null && overrideOutline.boolValue;

        var overrideFx = serializedObject.FindProperty("overrideTelegraphEffects");
        var fxPulse    = serializedObject.FindProperty("pulseAmount");
        var fxBlink    = serializedObject.FindProperty("blinkAmount");
        var fxRing     = serializedObject.FindProperty("ringAmount");
        var fxSpeed    = serializedObject.FindProperty("ringSpeed");
        var fxPulseSpd = serializedObject.FindProperty("pulseSpeed");
        var fxSpacing  = serializedObject.FindProperty("ringSpacing");
        var fxRingW    = serializedObject.FindProperty("ringWidth");
        var fxOutlineW = serializedObject.FindProperty("outlineWidth");
        var fxGlow     = serializedObject.FindProperty("edgeGlow");
        var fxEdge     = serializedObject.FindProperty("edgeStrength");
        var fxAlpha    = serializedObject.FindProperty("baseAlpha");
        bool useOverrideFx = overrideFx != null && overrideFx.boolValue;

        bool ffxivActive = useChase
                        || (followCaster != null && followCaster.boolValue)
                        || (isStack      != null && isStack.boolValue)
                        || (isGaze       != null && isGaze.boolValue);

        var it = serializedObject.GetIterator();
        bool enterChildren = true;
        while (it.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (it.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(it, true);
                continue;
            }

            // ── กลุ่มที่ยุบได้ — ค่า default ของทั้งกลุ่มคือ "ปิดอยู่" ────────
            // วาดที่ property ตัวแรกของกลุ่ม แล้ว skip ตัวที่เหลือ

            if (it.propertyPath == "castName")
            {
                if (DrawGroupHeader(CastPrefKey, "Cast bar", useCastTime,
                                    $"ใช้อยู่: \"{castName.stringValue}\" {castTime.floatValue:0.#}s"))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(castName, true);
                    if (useCastTime) EditorGUILayout.PropertyField(castTime, true);
                    EditorGUI.indentLevel--;
                }
                continue;
            }
            if (it.propertyPath == "castTime") continue;

            if (it.propertyPath == "repeatCount")
            {
                if (DrawGroupHeader(RepeatPrefKey, "Repeat", useRepeat,
                                    $"ใช้อยู่: ×{repeatCount.intValue} ทุก {repeatInterval.floatValue:0.##}s"))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(repeatCount, true);
                    if (useRepeat)
                    {
                        EditorGUILayout.PropertyField(repeatInterval, true);
                        EditorGUILayout.PropertyField(rerollRepeat, true);
                    }
                    EditorGUI.indentLevel--;
                }
                continue;
            }
            if (it.propertyPath is "repeatInterval" or "rerollEachRepeat") continue;

            if (it.propertyPath == "isChasing")
            {
                if (DrawGroupHeader(FfxivPrefKey, "FFXIV Special Settings", ffxivActive,
                                    "ใช้อยู่: " + FfxivSummary(useChase, followCaster, isStack, isGaze)))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(isChasing, true);
                    if (useChase) EditorGUILayout.PropertyField(isRotating, true);
                    EditorGUILayout.PropertyField(followCaster, true);
                    EditorGUILayout.PropertyField(isStack, true);
                    EditorGUILayout.PropertyField(isGaze, true);
                    EditorGUI.indentLevel--;
                }
                continue;
            }
            if (it.propertyPath is "followCaster" or "isRotatingChase" or "isStackMarker" or "isGaze")
                continue;

            if (it.propertyPath == "knockbackMode")
            {
                if (DrawGroupHeader(KnockPrefKey, "Knockback", useKnockback,
                                    $"ใช้อยู่: {knockbackDist.floatValue:0.#} m ใน {knockbackDur.floatValue:0.##}s"))
                {
                    EditorGUI.indentLevel++;
                    // ระยะเป็นสวิตช์ของทั้งกลุ่ม (0 = ไม่ผลัก) วางไว้บนสุดให้เจอก่อน
                    EditorGUILayout.PropertyField(knockbackDist, true);
                    if (useKnockback)
                    {
                        EditorGUILayout.PropertyField(knockbackMode, true);
                        EditorGUILayout.PropertyField(knockbackDur, true);
                        if (useFixedDir) EditorGUILayout.PropertyField(knockbackFixed, true);
                    }
                    EditorGUI.indentLevel--;
                }
                continue;
            }
            if (it.propertyPath is "knockbackDistance" or "knockbackDuration" or "knockbackFixedDirection")
                continue;

            if (it.propertyPath == "scaleStart")
            {
                if (DrawGroupHeader(ExpandPrefKey, "Expanding / Sweeping", expandActive,
                                    $"ใช้อยู่: {scaleStart.floatValue:0.##}→{scaleEnd.floatValue:0.##}, sweep {sweep.floatValue:0.#}°/s"))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(scaleStart, true);
                    EditorGUILayout.PropertyField(scaleEnd, true);
                    EditorGUILayout.PropertyField(sweep, true);
                    EditorGUI.indentLevel--;
                }
                continue;
            }
            if (it.propertyPath is "scaleEnd" or "sweepDegreesPerSecond") continue;

            // สีต่อท่าเป็นของข้อยกเว้น — ยุบเป็นบรรทัดเดียวจนกว่าจะติ๊กเปิด
            if (it.propertyPath == "overrideTelegraphColors")
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(overrideCol, true);
                if (useOverrideColors)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(warnCol, true);
                    EditorGUILayout.PropertyField(dangerCol, true);
                    EditorGUI.indentLevel--;
                    EditorGUILayout.HelpBox(
                        "ทับ palette กลาง — สีจะไม่สื่อความหมายตามหมวดกลไกอีกต่อไป\n" +
                        "ถ้าใช้บ่อย ควรเพิ่มหมวดใหม่ใน TelegraphZone palette แทน",
                        MessageType.Info);
                }
                continue;
            }
            if (it.propertyPath is "telegraphWarningColor" or "telegraphDangerColor") continue;

            // สีขอบเป็นคนละ gate กับสีพื้น — ยุบเป็นบรรทัดเดียวเหมือนกัน
            if (it.propertyPath == "overrideTelegraphOutlineColor")
            {
                EditorGUILayout.PropertyField(overrideOutline, true);
                if (useOverrideOutlineColor)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(outlineCol, true);
                    EditorGUI.indentLevel--;
                }
                continue;
            }
            if (it.propertyPath == "telegraphOutlineColor") continue;

            if (it.propertyPath == "overrideTelegraphEffects")
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(overrideFx, true);
                if (useOverrideFx)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("ความแรง", EditorStyles.miniBoldLabel);
                    EditorGUILayout.PropertyField(fxPulse, true);
                    EditorGUILayout.PropertyField(fxBlink, true);
                    EditorGUILayout.PropertyField(fxRing, true);

                    EditorGUILayout.LabelField("จังหวะ", EditorStyles.miniBoldLabel);
                    EditorGUILayout.PropertyField(fxSpeed, true);
                    EditorGUILayout.PropertyField(fxPulseSpd, true);

                    EditorGUILayout.LabelField("ขนาด (เมตร)", EditorStyles.miniBoldLabel);
                    EditorGUILayout.PropertyField(fxSpacing, true);
                    EditorGUILayout.PropertyField(fxRingW, true);
                    EditorGUILayout.PropertyField(fxOutlineW, true);

                    EditorGUILayout.LabelField("ขอบ / ความทึบ", EditorStyles.miniBoldLabel);
                    EditorGUILayout.PropertyField(fxGlow, true);
                    EditorGUILayout.PropertyField(fxEdge, true);
                    EditorGUILayout.PropertyField(fxAlpha, true);
                    EditorGUI.indentLevel--;
                    EditorGUILayout.HelpBox(
                        "ทับหน้าตาจาก material ทั้งชุด — ท่าที่ยิงรัวควรลด blink ลง ไม่งั้นจอกะพริบจนอ่านไม่ออก\n" +
                        "ช่องหน่วยเมตรจะคงความหนาเท่ากันไม่ว่าวงจะใหญ่แค่ไหน",
                        MessageType.Info);
                }
                continue;
            }
            if (it.propertyPath is "pulseAmount" or "blinkAmount" or "ringAmount" or "ringSpeed"
                or "pulseSpeed" or "ringSpacing" or "ringWidth" or "outlineWidth"
                or "edgeGlow" or "edgeStrength" or "baseAlpha") continue;

            bool hide = it.propertyPath switch
            {
                "arenaAnchor"        => !useArenaAnchor,
                "arenaDistanceScale" => !useDistScale,
                "anchorChoices"      => !useAnchorList,
                _ => false,
            };
            if (hide) continue;

            EditorGUILayout.PropertyField(it, true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}

/// <summary>ซ่อน field ของโหมด tether ที่ไม่ได้เลือกอยู่ · เตือนโหมดที่ยังไม่ได้ทำ</summary>
[CustomEditor(typeof(TetherAction))]
public class TetherActionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var modeProp = serializedObject.FindProperty("mode");
        var mode = modeProp != null ? (TetherMode)modeProp.enumValueIndex : TetherMode.Far;

        if (mode == TetherMode.Transferable)
        {
            EditorGUILayout.HelpBox(
                "TetherMode.Transferable ยังไม่ได้ทำ (ADR-002) — ตอนรันจะ warn แล้ว fallback เป็น Far",
                MessageType.Warning);
        }

        var it = serializedObject.GetIterator();
        bool enterChildren = true;
        while (it.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (it.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(it, true);
                continue;
            }

            bool hide = it.propertyPath switch
            {
                "closeFailMode"                              => mode != TetherMode.Close,
                "leashScope" or "leashAnchorOffset"           => mode != TetherMode.Leash,
                // Leash ดึงกลับแทนการทำดาเมจ และใช้เสาเป็น anchor เสมอ
                "tetherFailDamage" or "tetherSoloSpawnOffset" => mode == TetherMode.Leash,
                _ => false,
            };
            if (hide) continue;

            EditorGUILayout.PropertyField(it, true);
        }

        serializedObject.ApplyModifiedProperties();

        if (mode == TetherMode.Leash)
        {
            EditorGUILayout.HelpBox(
                "Leash: Tether Distance = รัศมีที่ห้ามออก · ค่า default 25 ออกแบบมาสำหรับ Far " +
                "(วิ่งห่างกัน 25 m) ซึ่งกว้างเกือบทั้งสนาม — โหมดนี้ปกติใช้ราว 6–10",
                MessageType.Info);
        }
    }
}
