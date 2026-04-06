using UnityEngine;

/// <summary>
/// ประเภท VFX — จับคู่กับสีและรูปแบบ particle ของแต่ละ weapon
/// </summary>
public enum VFXType
{
    BulletHit,          // Pistol / ทั่วไป     — เหลือง
    ShotgunHit,         // Shotgun              — ส้ม
    LaserHit,           // Laser                — ฟ้า cyan
    GrenadeExplosion,   // Grenade / Cluster    — ส้มแดง
    MineExplosion,      // Mine                 — ม่วง
    OrbiterHit,         // Orbiter              — เขียว teal
    OrbPickup,          // Objective Orb เก็บ  — ทอง
    EnemyDeath,         // ศัตรูตาย             — แดง
    RailgunBeam,        // Railgun pierce       — ฟ้าอ่อน
    PlasmaHit,          // PlasmaWhip           — ม่วงเข้ม
    WhipSlash,          // Whip / Chainsaw      — ขาวสว่าง กระจายวงกว้าง
}

/// <summary>
/// VFX ชั่วคราวแบบ procedural — สร้าง ParticleSystem runtime โดยไม่ต้องใช้ asset ภายนอก
///
/// เรียกจาก Client เท่านั้น (visual-only, ไม่มี network sync)
///   VFXFactory.Play(VFXType.BulletHit, hitPosition);
///   VFXFactory.PlayBeam(VFXType.RailgunBeam, from, to);
/// </summary>
public static class VFXFactory
{
    // ── Color palette ──────────────────────────────────────────────────────
    static readonly Color C_BULLET  = new(1.00f, 0.85f, 0.10f);
    static readonly Color C_SHOTGUN = new(1.00f, 0.55f, 0.05f);
    static readonly Color C_LASER   = new(0.10f, 0.95f, 1.00f);
    static readonly Color C_EXPLODE = new(1.00f, 0.40f, 0.05f);
    static readonly Color C_MINE    = new(0.80f, 0.20f, 1.00f);
    static readonly Color C_ORBIT   = new(0.10f, 0.90f, 0.80f);
    static readonly Color C_PICKUP  = new(1.00f, 0.90f, 0.20f);
    static readonly Color C_DEATH   = new(0.90f, 0.08f, 0.08f);
    static readonly Color C_RAIL    = new(0.65f, 0.85f, 1.00f);
    static readonly Color C_PLASMA  = new(0.60f, 0.10f, 1.00f);

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>One-shot particle burst ณ ตำแหน่งโลก</summary>
    public static void Play(VFXType type, Vector3 position)
    {
        var go = new GameObject($"[VFX]{type}");
        go.transform.position = position;

        var ps = go.AddComponent<ParticleSystem>();
        Configure(ps, type);

        var main = ps.main;
        float ttl = main.duration + main.startLifetime.constantMax + 0.3f;
        Object.Destroy(go, ttl);
    }

    /// <summary>Beam VFX (LineRenderer) จาก from → to แล้ว burst ที่ปลาย</summary>
    public static void PlayBeam(VFXType type, Vector3 from, Vector3 to, float duration = 0.15f)
    {
        Color c = GetColor(type);

        var go = new GameObject($"[VFX]Beam_{type}");
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        lr.startWidth    = type == VFXType.RailgunBeam ? 0.14f : 0.08f;
        lr.endWidth      = 0.02f;
        lr.useWorldSpace = true;

        // material ที่รองรับสีโดยไม่ต้องมี texture
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color        = c;
        lr.material      = mat;
        lr.startColor    = c;
        lr.endColor      = new Color(c.r, c.g, c.b, 0f);

        // burst ที่ปลาย beam
        VFXType hitType = type == VFXType.RailgunBeam ? VFXType.BulletHit : VFXType.PlasmaHit;
        Play(hitType, to);

        Object.Destroy(go, duration);
    }

    // ── Internal ───────────────────────────────────────────────────────────

    static void Configure(ParticleSystem ps, VFXType type)
    {
        // หยุด PS ก่อนตั้งค่า — AddComponent<ParticleSystem> auto-play ทันที
        // ถ้าไม่ Stop ก่อน การแก้ main.duration ขณะ playing จะเกิด error
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        Color col = GetColor(type);
        var   main = ps.main;

        main.loop            = false;
        main.playOnAwake     = false;   // เราเรียก ps.Play() เองท้าย Configure
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor      = col;
        main.gravityModifier = 0.25f;

        switch (type)
        {
            case VFXType.BulletHit:
                SetBurst(main, ps, count: 8,  speed: 4.5f, life: 0.22f, size: 0.08f);
                SetCone(ps, 60f);
                break;

            case VFXType.ShotgunHit:
                SetBurst(main, ps, count: 16, speed: 5.5f, life: 0.28f, size: 0.10f);
                SetSphere(ps, 0.15f);
                break;

            case VFXType.LaserHit:
                SetBurst(main, ps, count: 10, speed: 7f,   life: 0.16f, size: 0.06f);
                SetCone(ps, 28f);
                main.gravityModifier = 0f;
                break;

            case VFXType.GrenadeExplosion:
                SetBurst(main, ps, count: 42, speed: 9f,   life: 0.55f, size: 0.28f);
                SetSphere(ps, 0.65f);
                main.gravityModifier = 0.5f;
                break;

            case VFXType.MineExplosion:
                SetBurst(main, ps, count: 32, speed: 8f,   life: 0.50f, size: 0.22f);
                SetSphere(ps, 0.50f);
                main.gravityModifier = 0.4f;
                break;

            case VFXType.OrbiterHit:
                SetBurst(main, ps, count: 10, speed: 5f,   life: 0.25f, size: 0.09f);
                SetCone(ps, 50f);
                break;

            case VFXType.OrbPickup:
                SetBurst(main, ps, count: 30, speed: 5.5f, life: 0.90f, size: 0.20f);
                SetSphere(ps, 0.40f);
                main.gravityModifier = -0.20f;  // ลอยขึ้น
                break;

            case VFXType.EnemyDeath:
                SetBurst(main, ps, count: 22, speed: 5f,   life: 0.40f, size: 0.18f);
                SetSphere(ps, 0.25f);
                break;

            case VFXType.RailgunBeam:
                SetBurst(main, ps, count: 20, speed: 10f,  life: 0.28f, size: 0.12f);
                SetCone(ps, 18f);
                main.gravityModifier = 0f;
                break;

            case VFXType.PlasmaHit:
                SetBurst(main, ps, count: 16, speed: 5.5f, life: 0.30f, size: 0.13f);
                SetCone(ps, 65f);
                break;

            case VFXType.WhipSlash:
                SetBurst(main, ps, count: 28, speed: 6f, life: 0.35f, size: 0.15f);
                SetSphere(ps, 0.30f);
                main.gravityModifier = 0f;
                break;
        }

        FadeOut(ps, col);

        // เริ่ม play หลังตั้งค่าครบทุก property แล้ว
        ps.Play();
    }

    static void SetBurst(ParticleSystem.MainModule main, ParticleSystem ps,
        int count, float speed, float life, float size)
    {
        main.startSpeed    = speed;
        main.startLifetime = life;
        main.startSize     = size;
        main.maxParticles  = count * 2;
        main.duration      = 0.05f;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
    }

    static void SetCone(ParticleSystem ps, float angle)
    {
        var sh     = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle     = angle;
        sh.radius    = 0.05f;
    }

    static void SetSphere(ParticleSystem ps, float radius)
    {
        var sh     = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius    = radius;
    }

    static void FadeOut(ParticleSystem ps, Color col)
    {
        var clr    = ps.colorOverLifetime;
        clr.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(col, 0f), new GradientColorKey(Color.white, 0.7f) },
            new[] { new GradientAlphaKey(1f, 0f),  new GradientAlphaKey(0f, 1f) }
        );
        clr.color = g;
    }

    static Color GetColor(VFXType type) => type switch
    {
        VFXType.BulletHit        => C_BULLET,
        VFXType.ShotgunHit       => C_SHOTGUN,
        VFXType.LaserHit         => C_LASER,
        VFXType.GrenadeExplosion => C_EXPLODE,
        VFXType.MineExplosion    => C_MINE,
        VFXType.OrbiterHit       => C_ORBIT,
        VFXType.OrbPickup        => C_PICKUP,
        VFXType.EnemyDeath       => C_DEATH,
        VFXType.RailgunBeam      => C_RAIL,
        VFXType.PlasmaHit        => C_PLASMA,
        VFXType.WhipSlash        => Color.white,
        _                        => Color.white,
    };
}
