using UnityEditor;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Tools > Swarm > Create Projectile Prefabs
///
/// สร้าง prefab สำหรับ projectile แต่ละ type:
///   Proj_Pistol   — เหลือง  sphere เล็ก        → hitVFX: BulletHit
///   Proj_Shotgun  — ส้ม     sphere เล็กมาก      → hitVFX: ShotgunHit
///   Proj_Laser    — ฟ้า     capsule ยาว         → hitVFX: LaserHit
///   Proj_Orbiter  — teal    sphere กลาง         → hitVFX: OrbiterHit
///   Proj_Cluster  — แดง     sphere จิ๋ว (pellet) → hitVFX: BulletHit
///
/// หลังรัน:
///   1. Assets/prefab/Projectile/ จะมี prefab ใหม่ 5 ตัว
///   2. ไป assign ให้ WeaponData / PlayerWeaponManager ตาม weapon type
///   3. แต่ละ prefab มี NetworkObject → ต้องลงทะเบียนใน NetworkManager > Prefabs
/// </summary>
public static class ProjectilePrefabCreator
{
    const string OUT_DIR = "Assets/prefab/Projectile";

    struct ProjDef
    {
        public string   name;
        public Color    color;
        public Vector3  scale;
        public PrimitiveType shape;    // Sphere หรือ Capsule
        public string   vfx;
        public float    trailTime;
        public float    trailWidth;
        public bool     isHoming;      // default false = direction mode
    }

    [MenuItem("Tools/Swarm/Create Projectile Prefabs")]
    public static void CreateAll()
    {
        System.IO.Directory.CreateDirectory(OUT_DIR);

        var defs = new ProjDef[]
        {
            new() {
                name       = "Proj_Pistol",
                color      = new Color(1.00f, 0.85f, 0.10f),   // เหลือง
                scale      = new Vector3(0.20f, 0.20f, 0.20f),
                shape      = PrimitiveType.Sphere,
                vfx        = "HitEffect",
                trailTime  = 0.08f,
                trailWidth = 0.06f,
            },
            new() {
                name       = "Proj_Shotgun",
                color      = new Color(1.00f, 0.55f, 0.05f),   // ส้ม
                scale      = new Vector3(0.15f, 0.15f, 0.15f),
                shape      = PrimitiveType.Sphere,
                vfx        = "HitEffect",
                trailTime  = 0.05f,
                trailWidth = 0.05f,
            },
            new() {
                name       = "Proj_Laser",
                color      = new Color(0.10f, 0.95f, 1.00f),   // ฟ้า cyan
                scale      = new Vector3(0.07f, 0.07f, 0.40f),
                shape      = PrimitiveType.Capsule,
                vfx        = "HitEffect",
                trailTime  = 0.10f,
                trailWidth = 0.04f,
            },
            new() {
                name       = "Proj_Orbiter",
                color      = new Color(0.10f, 0.90f, 0.80f),   // teal
                scale      = new Vector3(0.28f, 0.28f, 0.28f),
                shape      = PrimitiveType.Sphere,
                vfx        = "OrbiterHit",
                trailTime  = 0.12f,
                trailWidth = 0.08f,
            },
            new() {
                name       = "Proj_Cluster",
                color      = new Color(0.90f, 0.20f, 0.10f),   // แดง (pellet)
                scale      = new Vector3(0.12f, 0.12f, 0.12f),
                shape      = PrimitiveType.Sphere,
                vfx        = "HitEffect",
                trailTime  = 0.05f,
                trailWidth = 0.04f,
            },
        };

        int created = 0;
        foreach (var d in defs)
        {
            string path = $"{OUT_DIR}/{d.name}.prefab";

            // ── Root GameObject ───────────────────────────────────────────
            var root = new GameObject(d.name);

            // ── Visual mesh ───────────────────────────────────────────────
            var mesh = GameObject.CreatePrimitive(d.shape);
            mesh.name = "Visual";
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localScale = d.scale;

            // Remove default collider from visual (collider lives on root)
            Object.DestroyImmediate(mesh.GetComponent<Collider>());

            // Set color
            var rend = mesh.GetComponent<Renderer>();
            var mat  = new Material(Shader.Find("Standard"));
            mat.color = d.color;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", d.color * 0.5f);
            rend.sharedMaterial = mat;

            // ── TrailRenderer ─────────────────────────────────────────────
            var trail        = root.AddComponent<TrailRenderer>();
            trail.time       = d.trailTime;
            trail.startWidth = d.trailWidth;
            trail.endWidth   = 0f;
            trail.material   = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = d.color;
            trail.endColor   = new Color(d.color.r, d.color.g, d.color.b, 0f);

            // ── Physics: Rigidbody ────────────────────────────────────────
            var rb               = root.AddComponent<Rigidbody>();
            rb.isKinematic       = true;
            rb.useGravity        = false;
            rb.interpolation     = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // ── Physics: Collider ─────────────────────────────────────────
            var col       = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius    = Mathf.Max(d.scale.x, d.scale.y, d.scale.z) * 0.6f;

            // ── Projectile script ─────────────────────────────────────────
            var proj    = root.AddComponent<Projectile>();
            proj.hitVFX = d.vfx;
            // speed / damage / range จะถูก set โดย weapon script ตอน fire

            // ── NetworkObject ─────────────────────────────────────────────
            root.AddComponent<NetworkObject>();

            // ── Save as prefab ────────────────────────────────────────────
            bool success;
            PrefabUtility.SaveAsPrefabAsset(root, path, out success);
            Object.DestroyImmediate(root);

            if (success) created++;
            else Debug.LogError($"[PrefabCreator] ❌ ไม่สามารถสร้าง {d.name}");
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Projectile Prefabs",
            $"✅ สร้างสำเร็จ {created}/{defs.Length} prefabs\n" +
            $"📁 {OUT_DIR}/\n\n" +
            "ขั้นตอนต่อไป:\n" +
            "1. เปิด NetworkManager → เพิ่ม prefab ทุกตัวใน Network Prefabs List\n" +
            "2. Assign Proj_Pistol ให้ PlayerWeaponManager.projectilePrefab\n" +
            "3. Assign Proj_Laser ให้ LaserWeapon (ถ้ามี prefab override)\n" +
            "4. Assign Proj_Orbiter ให้ OrbiterWeapon\n" +
            "5. Assign Proj_Cluster ให้ GrenadeProjectile.clusterPelletPrefab (ถ้าต้องการ)",
            "OK");
    }
}
