using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > Swarm > Build Level Layout
///
/// สร้าง geometry ของ Arena ใน scene ปัจจุบัน
/// รันซ้ำได้ — ลบ "Level" เก่าแล้วสร้างใหม่
///
/// Layout (80×80 arena, center = 0,0,0):
///   • Ground      — 80×80 platform สีเทาอ่อน
///   • Inner Ring  — 4 pillar รัศมี 14  (N/S/E/W)   สูง 3 unit
///   • Outer Ring  — 4 pillar รัศมี 26  (NE/NW/SE/SW) สูง 2.5 unit
///   • Corner L    — 4 กลุ่มมุม (L-shape) รัศมี 36
///   • Center mark — platform ตรงกลาง สูง 0.3 unit, รัศมี 6
///   • Perimeter   — low wall ทั้ง 4 ด้าน ยาว 80, สูง 1.5
///   • SpawnPoints — 8 จุดสำหรับ Objective/Boss spawn รอบ edge
/// </summary>
public static class LevelLayoutBuilder
{
    // ── Palette ────────────────────────────────────────────────────────────
    static readonly Color COL_GROUND    = new(0.78f, 0.78f, 0.80f);
    static readonly Color COL_PILLAR    = new(0.25f, 0.25f, 0.28f);
    static readonly Color COL_WALL      = new(0.38f, 0.38f, 0.42f);
    static readonly Color COL_CORNER    = new(0.30f, 0.32f, 0.38f);
    static readonly Color COL_CENTER    = new(0.60f, 0.60f, 0.65f);
    static readonly Color COL_SPAWN     = new(0.20f, 0.80f, 0.30f);

    [MenuItem("Tools/Swarm/Build Level Layout")]
    public static void Build()
    {
        // ── ลบ level เก่า ─────────────────────────────────────────────────
        var old = GameObject.Find("Level");
        if (old != null)
        {
            Object.DestroyImmediate(old);
            Debug.Log("[LevelBuilder] ลบ Level เก่าแล้ว");
        }

        var root = new GameObject("Level");

        // ── Ground ────────────────────────────────────────────────────────
        // Plane default = 10×10 → scale 8 = 80×80
        var ground = MakeBox(root, "Ground",
            pos:   new Vector3(0, -0.05f, 0),
            scale: new Vector3(80, 0.10f, 80),
            color: COL_GROUND);

        // ── Center Platform ───────────────────────────────────────────────
        MakeBox(root, "CenterPlatform",
            pos:   new Vector3(0, 0.15f, 0),
            scale: new Vector3(10f, 0.30f, 10f),
            color: COL_CENTER);

        // ── Inner Ring Pillars (N/S/E/W, radius 14) ───────────────────────
        float[] innerAngles = { 0f, 90f, 180f, 270f };
        for (int i = 0; i < innerAngles.Length; i++)
        {
            float rad = innerAngles[i] * Mathf.Deg2Rad;
            var pos = new Vector3(Mathf.Sin(rad) * 14f, 1.5f, Mathf.Cos(rad) * 14f);
            MakeBox(root, $"Pillar_Inner_{i}",
                pos:   pos,
                scale: new Vector3(2f, 3f, 2f),
                color: COL_PILLAR);
        }

        // ── Outer Ring Pillars (NE/NW/SE/SW, radius 26) ───────────────────
        float[] outerAngles = { 45f, 135f, 225f, 315f };
        for (int i = 0; i < outerAngles.Length; i++)
        {
            float rad = outerAngles[i] * Mathf.Deg2Rad;
            var pos = new Vector3(Mathf.Sin(rad) * 26f, 1.25f, Mathf.Cos(rad) * 26f);
            MakeBox(root, $"Pillar_Outer_{i}",
                pos:   pos,
                scale: new Vector3(2f, 2.5f, 2f),
                color: COL_PILLAR);
        }

        // ── Corner L-Barriers (4 มุม รัศมี 36) ───────────────────────────
        // แต่ละ L มี 2 ชิ้น: horizontal + vertical
        var corners = new Vector3[]
        {
            new( 36f, 0,  36f),
            new(-36f, 0,  36f),
            new( 36f, 0, -36f),
            new(-36f, 0, -36f),
        };

        for (int i = 0; i < corners.Length; i++)
        {
            var c = corners[i];
            float sx = Mathf.Sign(c.x);
            float sz = Mathf.Sign(c.z);

            // แนวนอน
            MakeBox(root, $"Corner_{i}_H",
                pos:   c + new Vector3(sx * -2f, 0.75f, 0),
                scale: new Vector3(8f, 1.5f, 2f),
                color: COL_CORNER);
            // แนวตั้ง
            MakeBox(root, $"Corner_{i}_V",
                pos:   c + new Vector3(0, 0.75f, sz * -2f),
                scale: new Vector3(2f, 1.5f, 8f),
                color: COL_CORNER);
        }

        // ── Perimeter Walls (4 ด้าน, half-wall) ──────────────────────────
        // ผนังบาง สูง 1.5 เพื่อบล็อก line-of-sight บางส่วน
        float hw = 40f;   // half-width of arena

        // North
        MakeBox(root, "Wall_N",
            pos:   new Vector3(0, 0.75f, hw),
            scale: new Vector3(80f, 1.5f, 0.8f),
            color: COL_WALL);
        // South
        MakeBox(root, "Wall_S",
            pos:   new Vector3(0, 0.75f, -hw),
            scale: new Vector3(80f, 1.5f, 0.8f),
            color: COL_WALL);
        // East
        MakeBox(root, "Wall_E",
            pos:   new Vector3(hw, 0.75f, 0),
            scale: new Vector3(0.8f, 1.5f, 80f),
            color: COL_WALL);
        // West
        MakeBox(root, "Wall_W",
            pos:   new Vector3(-hw, 0.75f, 0),
            scale: new Vector3(0.8f, 1.5f, 80f),
            color: COL_WALL);

        // ── Cover Scatter (กลุ่มเล็กกลาง zone) ───────────────────────────
        // 4 กลุ่ม × 2 ชิ้น กระจาย mid-range
        var midCovers = new Vector3[]
        {
            new( 20f, 0,   5f),
            new(-20f, 0,  -5f),
            new(  5f, 0,  20f),
            new( -5f, 0, -20f),
        };
        for (int i = 0; i < midCovers.Length; i++)
        {
            var p = midCovers[i];
            MakeBox(root, $"Cover_{i}_A",
                pos:   p + Vector3.up,
                scale: new Vector3(1.5f, 2f, 4f),
                color: COL_CORNER);
            MakeBox(root, $"Cover_{i}_B",
                pos:   p + new Vector3(2.5f, 0.75f, 0),
                scale: new Vector3(1.5f, 1.5f, 1.5f),
                color: COL_CORNER);
        }

        // ── SpawnPoints (8 จุด สำหรับ Objective / Boss spawn) ────────────
        var spawnRoot = new GameObject("SpawnPoints");
        spawnRoot.transform.SetParent(root.transform, false);

        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            var   sp    = new GameObject($"SpawnPoint_{i}");
            sp.transform.SetParent(spawnRoot.transform, false);
            sp.transform.position = new Vector3(
                Mathf.Sin(angle) * 35f,
                0f,
                Mathf.Cos(angle) * 35f);

#if UNITY_EDITOR
            // เพิ่ม icon ใน scene view เพื่อให้มองเห็น
            var icon = EditorGUIUtility.IconContent("sv_label_3");
            EditorGUIUtility.SetIconForObject(sp, (Texture2D)icon.image);
#endif
        }

        // ── Mark scene dirty ──────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        EditorUtility.DisplayDialog(
            "Level Layout Built",
            "✅ สร้าง Level เสร็จแล้ว\n\n" +
            "━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
            "Layout:\n" +
            "  Ground        80×80\n" +
            "  Center Plat   10×10\n" +
            "  Inner Pillars 4× (r=14, สูง 3)\n" +
            "  Outer Pillars 4× (r=26, สูง 2.5)\n" +
            "  Corner L-Bar  4× (r=36)\n" +
            "  Mid Cover     4 กลุ่ม\n" +
            "  Perimeter     4 ผนัง\n" +
            "  SpawnPoints   8 จุด (r=35)\n\n" +
            "ขั้นตอนต่อไป:\n" +
            "  1. Save scene (Ctrl+S)\n" +
            "  2. ปรับ material / lighting ให้สวยงาม\n" +
            "  3. Assign SpawnPoints ให้ ObjectiveManager / BossManager\n" +
            "  4. ตั้ง layer ผนัง = 'Obstacle' ถ้าต้องการ block player\n" +
            "  5. เพิ่ม NavMesh bake ถ้าใช้ NavMesh agent",
            "OK");

        Debug.Log("[LevelBuilder] ✅ Level built — 'Level' GameObject อยู่ใน Hierarchy");
    }

    // ── Helper ─────────────────────────────────────────────────────────────
    static GameObject MakeBox(GameObject parent, string name,
        Vector3 pos, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale    = scale;

        var mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        go.GetComponent<Renderer>().sharedMaterial = mat;

        return go;
    }
}
