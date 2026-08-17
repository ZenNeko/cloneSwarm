using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// เช็คว่า Assets/Shader/TelegraphUniversal.shadergraph ยังประกาศครบทุก property ที่
/// TelegraphZone.cs ยิงเข้าไปจริง (ดู TelegraphZone.ShaderIDs.GraphPropertyNames)
///
/// นี่คือของที่แปลงบั๊กคลาส `_SweepAmount` — C# ยิง SetFloat/SetColor เข้า property ที่
/// shader ไม่ประกาศ ไม่มี error ไม่มี warning ผลที่เห็นคือ "ปรับค่าแล้วภาพไม่ขยับ" ซึ่งหาสาเหตุยากมาก —
/// จาก "เงียบแล้วไม่รู้" ให้กลายเป็น "รันเมนูแล้วรู้ทันที"
///
/// ไม่ใช่ autonomous check (ไม่ผูกกับ compile หรือ CI) — เรียกเองผ่านเมนูตอนแก้กราฟหรือแก้ชื่อ property
/// </summary>
public static class TelegraphShaderValidator
{
    private const string ShaderPath = "Assets/Shader/TelegraphUniversal.shadergraph";

    [MenuItem("Tools/Telegraph/Validate Shader Properties")]
    public static void ValidateTelegraphShaderProperties()
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null)
        {
            Debug.LogError($"[TelegraphShaderValidator] หา shader ไม่เจอที่ {ShaderPath} — " +
                            "ย้ายไฟล์หรือเปลี่ยนชื่อหรือเปล่า? แก้ ShaderPath ในสคริปต์นี้ให้ตรง");
            return;
        }

        // สร้าง Material ชั่วคราวจาก shader จริง — Material.HasProperty อ่าน property list
        // ของ shader ที่ผูกอยู่ ไม่ต้องพึ่ง ShaderUtil (internal API ที่เปลี่ยนบ่อยระหว่างเวอร์ชัน)
        var probe = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var missing = new List<string>();
            foreach (var propName in TelegraphZone.ShaderIDs.GraphPropertyNames)
            {
                if (!probe.HasProperty(propName)) missing.Add(propName);
            }

            if (missing.Count == 0)
            {
                Debug.Log($"[TelegraphShaderValidator] OK — {shader.name} ประกาศครบทั้ง " +
                          $"{TelegraphZone.ShaderIDs.GraphPropertyNames.Length} property ที่ TelegraphZone.cs ใช้งานจริง");
            }
            else
            {
                Debug.LogError(
                    $"[TelegraphShaderValidator] {shader.name} ขาด {missing.Count} property ที่ " +
                    $"TelegraphZone.cs ยิงเข้าไป: {string.Join(", ", missing)}\n" +
                    "ผลที่ตามมาถ้าไม่แก้: HasProperty() คืน false ทุกครั้ง → SetFloat/SetColor ไม่ทำอะไรเลย " +
                    "เงียบสนิท ไม่มี exception — นี่คือบั๊กคลาส _SweepAmount ที่โปรเจกต์นี้เจอมาก่อน");
            }

            // เช็คแยกอีกชุด — ชื่อพวกนี้ตั้งใจไม่อยู่บน TelegraphUniversal (เล็งไป material สำรอง
            // URP/Lit กับ Standard ที่ GetWarningMaterial() ปั้นเอง) แจ้งเป็น log ธรรมดา ไม่ใช่ error
            // เผื่อ designer งงว่าทำไมชื่อพวกนี้ไม่ผ่านเช็คข้างบน
            var unexpectedlyPresent = TelegraphZone.ShaderIDs.FallbackMaterialPropertyNames
                .Where(probe.HasProperty).ToList();
            if (unexpectedlyPresent.Count > 0)
            {
                Debug.Log($"[TelegraphShaderValidator] หมายเหตุ — {shader.name} มี property ของ " +
                          $"material สำรองด้วย ({string.Join(", ", unexpectedlyPresent)}) ไม่ใช่ปัญหา " +
                          "แค่แจ้งเผื่อสับสน เพราะพวกนี้ปกติเล็งไป Standard/URP-Lit ไม่ใช่ TelegraphUniversal");
            }
        }
        finally
        {
            Object.DestroyImmediate(probe);
        }
    }
}
