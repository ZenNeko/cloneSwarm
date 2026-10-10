using System;
using System.Collections.Generic;
using System.Linq;
using CloneSwarm.Meta;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// ตารางเทียบตัวเลขใน Balance Tool — เปิด asset ทีละตัวไม่มีทางเห็นว่าอะไรแรงเกินหรืออ่อนเกิน
// ต้องเห็นทุกตัวเรียงกันในหน่วยเดียวกัน
//
// ตัวเลขทุกช่องคำนวณจาก ScriptableObject ตรงๆ (อ่านอย่างเดียว) · แก้ค่าที่ asset ในหมวดของมัน
// แล้วกด "คำนวณใหม่"
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// อาวุธทุกตัว — DPS ดิบต่อเลเวล และเทียบ Super กับ Lv สุดท้ายของตัวฐาน
///
/// ═══ DPS ดิบ = damage × projectileCount ÷ cooldown ═══
///
/// **ไม่ได้** นับ pierce · AoE ที่โดนหลายตัว · duration ของของที่วางทิ้งไว้ · crit · สเตตัสผู้เล่น
/// จึงเทียบข้ามประเภทอาวุธตรงๆ ไม่ได้ (Orbiter กับ Shotgun คนละเรื่อง) · ใช้ดูภายในสายเดียวกัน
/// ว่าเลเวลโตสม่ำเสมอไหม และ Super แรงกว่าตัวฐานจริงหรือเปล่า (GDD ข้อ 6.5 บอกว่าต้องแรงกว่า)
/// </summary>
[Serializable]
public class WeaponBalanceTable
{
    [Serializable]
    public class Row
    {
        [TableColumnWidth(150), DisplayAsString] public string weapon;
        [TableColumnWidth(60),  DisplayAsString] public string tier;
        [TableColumnWidth(45),  DisplayAsString] public string lv;
        [TableColumnWidth(70),  DisplayAsString, LabelText("DPS Lv1")] public string dpsFirst;
        [TableColumnWidth(70),  DisplayAsString, LabelText("DPS สูงสุด")] public string dpsLast;
        [TableColumnWidth(55),  DisplayAsString, LabelText("โต ×")] public string growth;
        [TableColumnWidth(130), DisplayAsString] public string super;
        [TableColumnWidth(70),  DisplayAsString, LabelText("Super ÷ ฐาน")] public string superRatio;
        [DisplayAsString, LabelText("ข้อสังเกต")] public string note;

        [HideInInspector] public WeaponData asset;

        [Button("เปิด"), TableColumnWidth(50)]
        void Ping() { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); }
    }

    [InfoBox("DPS ดิบ = damage × projectileCount ÷ cooldown — ไม่นับ pierce / AoE / duration / crit / สเตตัสผู้เล่น\n" +
             "เทียบได้ภายในสายเดียวกันเท่านั้น (เลเวลโตสม่ำเสมอไหม · Super แรงกว่า Lv สุดท้ายของตัวฐานไหม)\n" +
             "แถว Fusion: คอลัมน์ super = วัตถุดิบ A + B · ratio = Fusion ÷ วัตถุดิบตัวที่แรงกว่า")]
    [TableList(IsReadOnly = true, ShowPaging = false, AlwaysExpanded = true)]
    public List<Row> rows = new();

    public WeaponBalanceTable() => Refresh();

    [Button("คำนวณใหม่", ButtonSizes.Medium), PropertyOrder(-1)]
    public void Refresh()
    {
        rows.Clear();
        var all = AssetDatabase.FindAssets("t:WeaponData")
                               .Select(AssetDatabase.GUIDToAssetPath)
                               .Select(AssetDatabase.LoadAssetAtPath<WeaponData>)
                               .Where(w => w != null)
                               .OrderBy(w => w.tier).ThenBy(w => w.name)
                               .ToList();

        // Fusion → วัตถุดิบสองตัว · Fusion ควรแรงกว่าตัวที่แรงที่สุดในสองตัวที่เสียไป
        // (กิน Super สองช่องคืนมาช่องเดียว — ถ้าอ่อนกว่า ผู้เล่นที่รู้จะไม่ fuse)
        var fusionInputs = AssetDatabase.FindAssets("t:WeaponFusionRecipe")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<WeaponFusionRecipe>)
            .Where(r => r != null && r.fusionResult != null)
            .GroupBy(r => r.fusionResult)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var w in all)
        {
            var notes = new List<string>();
            int n = w.levels?.Length ?? 0;
            int expected = w.tier == WeaponTier.Normal ? 5 : 1;
            if (n != expected) notes.Add($"มี {n} เลเวล (ควร {expected})");

            float first = n > 0 ? Dps(w.levels[0]) : 0f;
            float last  = n > 0 ? Dps(w.levels[n - 1]) : 0f;

            // เลเวลที่ DPS ตกจากเลเวลก่อน — มักเป็นค่าที่ก๊อปมาแล้วลืมแก้
            for (int i = 1; i < n; i++)
                if (w.levels[i] != null && w.levels[i - 1] != null && Dps(w.levels[i]) < Dps(w.levels[i - 1]) - 0.01f)
                    notes.Add($"Lv{i + 1} DPS ต่ำกว่า Lv{i}");

            string superName = "", ratio = "";
            if (w.superVersion != null)
            {
                superName = w.superVersion.name;
                if (w.superVersion.tier != WeaponTier.Super)
                    notes.Add($"superVersion ชี้ไป {w.superVersion.tier} ไม่ใช่ Super");

                var sl = w.superVersion.levels;
                if (sl != null && sl.Length > 0 && last > 0f)
                {
                    float r = Dps(sl[0]) / last;
                    ratio = $"{r:0.00}";
                    if (r < 1f) notes.Add("Super อ่อนกว่าตัวฐานที่เต็มเลเวล");
                }
            }
            else if (w.tier == WeaponTier.Normal && w.exclusiveCharacter == null && !IsPassive(w))
            {
                notes.Add("ไม่มี Super");
            }

            if (w.tier == WeaponTier.Fusion && fusionInputs.TryGetValue(w, out var recipe))
            {
                // asset ที่ถูกลบไปแล้วยังค้างเป็น GUID ในสูตร — Unity คืน null · สูตรนี้ไม่มีวัน fuse ได้
                if (recipe.superWeaponA == null || recipe.superWeaponB == null)
                    notes.Add("สูตรอ้างอาวุธที่ไม่มีอยู่แล้ว — fuse ไม่ได้");

                float a = FirstDps(recipe.superWeaponA), b = FirstDps(recipe.superWeaponB);
                float best = Mathf.Max(a, b);
                superName = $"{Short(recipe.superWeaponA)} + {Short(recipe.superWeaponB)}";
                if (best > 0f)
                {
                    ratio = $"{first / best:0.00}";
                    if (first < best) notes.Add("อ่อนกว่าวัตถุดิบที่แรงสุด");
                }
            }

            rows.Add(new Row
            {
                asset      = w,
                weapon     = w.name.Replace("WD_", ""),
                tier       = w.tier.ToString(),
                lv         = n.ToString(),
                dpsFirst   = n > 0 ? $"{first:0.#}" : "-",
                dpsLast    = n > 1 ? $"{last:0.#}" : "",
                growth     = n > 1 && first > 0f ? $"{last / first:0.0#}" : "",
                super      = superName.Replace("WD_", ""),   // Fusion: วัตถุดิบ A + B
                superRatio = ratio,
                note       = string.Join(" · ", notes),
            });
        }
    }

    static float FirstDps(WeaponData w) =>
        w?.levels != null && w.levels.Length > 0 ? Dps(w.levels[0]) : 0f;

    static string Short(WeaponData w) => w != null ? w.name.Replace("WD_", "") : "?";

    static bool IsPassive(WeaponData w) =>
        AssetDatabase.GetAssetPath(w).Replace('\\', '/').Contains("/Passive/");

    static float Dps(WeaponLevelData l) =>
        l == null ? 0f : l.damage * Mathf.Max(1, l.projectileCount) / Mathf.Max(0.05f, l.cooldown);
}

/// <summary>
/// Talent ทุกตัว — ผลที่ได้เมื่อซื้อเต็ม เทียบกับทองที่ต้องจ่ายทั้งหมด
/// ใช้ดูว่ามี talent ไหนคุ้มเกินจนทุกคนซื้อตัวเดียว หรือแพงจนไม่มีใครซื้อ
/// </summary>
[Serializable]
public class TalentBalanceTable
{
    [Serializable]
    public class Row
    {
        [TableColumnWidth(130), DisplayAsString] public string talent;
        [TableColumnWidth(110), DisplayAsString, LabelText("ผล")] public string effect;
        [TableColumnWidth(40),  DisplayAsString, LabelText("Max")] public string max;
        [TableColumnWidth(90),  DisplayAsString, LabelText("เต็มแล้วได้")] public string total;
        [TableColumnWidth(90),  DisplayAsString, LabelText("ราคา Lv1")] public string firstCost;
        [TableColumnWidth(90),  DisplayAsString, LabelText("ราคารวม")] public string totalCost;
        [DisplayAsString, LabelText("ข้อสังเกต")] public string note;

        [HideInInspector] public TalentData asset;

        [Button("เปิด"), TableColumnWidth(50)]
        void Ping() { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); }
    }

    [InfoBox("ราคารวม = ทองที่ต้องจ่ายตั้งแต่ Lv1 จนเต็ม · เรียงจากถูกไปแพง")]
    [TableList(IsReadOnly = true, ShowPaging = false, AlwaysExpanded = true)]
    public List<Row> rows = new();

    public TalentBalanceTable() => Refresh();

    [Button("คำนวณใหม่", ButtonSizes.Medium), PropertyOrder(-1)]
    public void Refresh()
    {
        rows.Clear();
        var all = AssetDatabase.FindAssets("t:TalentData")
                               .Select(AssetDatabase.GUIDToAssetPath)
                               .Select(AssetDatabase.LoadAssetAtPath<TalentData>)
                               .Where(t => t != null)
                               .Select(t => (t, cost: t.costPerLevel?.Sum() ?? 0))
                               .OrderBy(x => x.cost);

        foreach (var (t, cost) in all)
        {
            int max = t.costPerLevel?.Length ?? 0;
            var notes = new List<string>();
            if (max == 0) notes.Add("ไม่มีราคา — ซื้อไม่ได้");
            for (int i = 1; i < max; i++)
                if (t.costPerLevel[i] < t.costPerLevel[i - 1]) { notes.Add($"Lv{i + 1} ถูกกว่า Lv{i}"); break; }

            bool stat = t.mode == TalentEffectMode.Stat;
            string per = stat ? Fmt(t.statType, t.valuePerLevel) : t.mode.ToString();

            rows.Add(new Row
            {
                asset     = t,
                talent    = t.talentId.Replace("talent_", ""),
                effect    = stat ? $"{t.statType} {per}" : per,
                max       = max.ToString(),
                total     = stat ? Fmt(t.statType, t.valuePerLevel * max) : "",
                firstCost = max > 0 ? $"{t.costPerLevel[0]:N0}" : "-",
                totalCost = $"{cost:N0}",
                note      = string.Join(" · ", notes),
            });
        }
    }

    // StatData ใช้ทศนิยมสำหรับ % — ค่าต่ำกว่า 1 ถือเป็น % ยกเว้นสเตตัสที่เป็น flat เสมอ
    static string Fmt(StatType s, float v)
    {
        bool flat = s is StatType.AbilityHaste or StatType.ProjectileCount or StatType.MaxHealth
                      or StatType.Armor or StatType.HealthRegen;
        return flat ? $"+{v:0.##}" : $"+{v * 100f:0.#}%";
    }
}
