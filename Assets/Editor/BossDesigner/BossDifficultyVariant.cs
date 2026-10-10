using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// "สร้างเวอร์ชันความยาก…" — ก๊อป BossEncounterConfig ทั้งชุด + ตัวคูณ แล้วผูกเข้า MapData
    ///
    /// ═══ ก๊อปลึก ไม่ใช่ก๊อปไฟล์ ═══
    ///
    /// AssetDatabase.CopyAsset ก๊อปแค่ไฟล์ config (รวม sub-asset ในไฟล์) · แต่ timeline ของ
    /// BossConfig_01 ชี้ action ที่เป็น **ไฟล์แยก** (Boss poc 1/AoE_Boss_*.asset) · ถ้าไม่ก๊อปตาม
    /// เวอร์ชัน Savage จะชี้ไฟล์เดียวกับ Normal แล้วตัวคูณดาเมจไปเปลี่ยน Normal ด้วย — เงียบ
    ///
    /// จึงไล่ทุก object reference ในไฟล์ใหม่ · action ที่อยู่นอกไฟล์ถูกก๊อปเข้ามาเป็น sub-asset
    /// (ก๊อปครั้งเดียวต่อ action · ที่อ้างซ้ำชี้ตัวก๊อปเดียวกัน) แล้วไล่ต่อในตัวก๊อปจนหมด
    /// (ComboAction / RandomAttackAction / timeline ซ้อน)
    /// </summary>
    public class BossDifficultyVariant : EditorWindow
    {
        BossEncounterConfig _source;
        DifficultyTier _tier = DifficultyTier.Savage;
        // ×1 = ไม่คูณ — ตัวคูณของระดับอยู่ใน DifficultyProfile แล้ว (คูณตอนเล่น) · ใส่ตรงนี้อีกจะคูณซ้ำสองชั้น
        // เครื่องมือนี้เหลือไว้สำหรับระดับที่ "ท่าต่างจนเป็นคนละไฟต์" — ท่าต่อยอดใช้ช่วงระดับบนคลิปแทน
        float _warnMult = 1f, _damageMult = 1f, _intervalMult = 1f;
        MapData _map;
        System.Action<BossEncounterConfig> _onCreated;

        public static void Open(BossEncounterConfig source, System.Action<BossEncounterConfig> onCreated)
        {
            var w = CreateInstance<BossDifficultyVariant>();
            w.titleContent = new GUIContent("สร้างเวอร์ชันความยาก");
            w._source = source;
            w._onCreated = onCreated;
            w._map = AssetDatabase.FindAssets("t:MapData")
                .Select(g => AssetDatabase.LoadAssetAtPath<MapData>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(m => m?.tiers != null && m.tiers.Any(t => t?.mainBossConfig == source));
            w.minSize = w.maxSize = new Vector2(380, 290);
            w.ShowUtility();
        }

        void OnGUI()
        {
            if (_source == null) { EditorGUILayout.HelpBox("ไม่มี config ต้นทาง", MessageType.Error); return; }

            EditorGUILayout.LabelField("ต้นทาง", _source.name);
            _tier = (DifficultyTier)EditorGUILayout.EnumPopup("ระดับ", _tier);
            EditorGUILayout.HelpBox("ท่าเพิ่มในระดับสูง: ใช้ \"ออกเฉพาะระดับ\" บนคลิปแทนการก๊อป · ตัวเลขของระดับอยู่ใน DifficultyProfile (คูณตอนเล่น) — ตัวคูณข้างล่างจะคูณซ้ำ ปล่อย ×1 ไว้", MessageType.Info);
            EditorGUILayout.Space(4);
            _warnMult     = EditorGUILayout.Slider("เวลาเตือน ×",  _warnMult,     0.3f, 1.5f);
            _damageMult   = EditorGUILayout.Slider("ดาเมจ ×",      _damageMult,   0.5f, 4f);
            _intervalMult = EditorGUILayout.Slider("ช่วงห่างท่า ×", _intervalMult, 0.3f, 1.5f);
            EditorGUILayout.Space(4);
            _map = (MapData)EditorGUILayout.ObjectField("ผูกกับแมพ", _map, typeof(MapData), false);
            EditorGUILayout.HelpBox(_map != null
                ? $"จะตั้ง {_map.mapId}/{_tier} ให้ใช้ config ใหม่ · ถ้ายังไม่มีระดับนี้ จะก๊อปตาราง/สเกล/เพลงของ Normal มาให้"
                : "ไม่ผูกแมพ — ใส่เองทีหลังใน MapData", MessageType.None);

            if (GUILayout.Button("สร้าง"))
            {
                var created = Create(_source, _tier, _warnMult, _damageMult, _intervalMult, _map, out string log);
                Debug.Log("[BossDifficultyVariant]\n" + log, created);
                if (created != null) { _onCreated?.Invoke(created); Close(); }
            }
        }

        public static BossEncounterConfig Create(BossEncounterConfig src, DifficultyTier tier,
                                                 float warnMult, float damageMult, float intervalMult,
                                                 MapData map, out string log)
        {
            var sb = new System.Text.StringBuilder();
            string srcPath = AssetDatabase.GetAssetPath(src);
            string dst = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(Path.GetDirectoryName(srcPath), $"{src.name}_{tier}.asset").Replace('\\', '/'));

            if (!AssetDatabase.CopyAsset(srcPath, dst)) { log = $"ก๊อป {srcPath} ไม่สำเร็จ"; return null; }
            var cfg = AssetDatabase.LoadAssetAtPath<BossEncounterConfig>(dst);
            sb.AppendLine($"สร้าง {dst}");

            // ── ก๊อป action ที่อยู่นอกไฟล์เข้ามาเป็น sub-asset แล้วชี้ใหม่ ──
            BossActionEmbed.EmbedExternalActions(cfg, sb);

            // ── ตัวคูณ ──
            int actions = 0;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(dst))
            {
                switch (o)
                {
                    case SpawnAoEActionBase aoe:
                        aoe.warningDuration *= warnMult;
                        aoe.damage *= damageMult;
                        EditorUtility.SetDirty(aoe); actions++;
                        break;
                    case TetherAction t:
                        t.tetherFailDamage *= damageMult;
                        EditorUtility.SetDirty(t); actions++;
                        break;
                }
            }
            if (cfg.attackInterval > 0f) cfg.attackInterval *= intervalMult;
            foreach (var ph in cfg.phases ?? new List<BossPhase>())
                if (ph != null && ph.attackInterval > 0f) ph.attackInterval *= intervalMult;
            EditorUtility.SetDirty(cfg);
            sb.AppendLine($"ตัวคูณ: เตือน ×{warnMult:0.##} · ดาเมจ ×{damageMult:0.##} · ช่วงห่าง ×{intervalMult:0.##} · {actions} ท่า");
            sb.AppendLine("หมายเหตุ: ระยะห่างของคลิปบน timeline ไม่ถูกคูณ — จังหวะที่วางไว้ยังเหมือนเดิม");

            // ── ผูกแมพ ──
            if (map != null)
            {
                Undo.RecordObject(map, "Link Difficulty Variant");
                var list = (map.tiers ?? new MapData.TierContent[0]).ToList();
                var entry = list.FirstOrDefault(t => t != null && t.tier == tier);
                if (entry == null)
                {
                    var normal = map.GetTier(DifficultyTier.Normal);
                    entry = new MapData.TierContent();
                    if (normal != null) EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(normal), entry);
                    entry.tier = tier;
                    list.Add(entry);
                    sb.AppendLine($"เพิ่มระดับ {tier} ใน {map.mapId} (ก๊อปตาราง/สเกล/เพลงจาก Normal)");
                }
                entry.mainBossConfig = cfg;
                map.tiers = list.ToArray();
                EditorUtility.SetDirty(map);
                sb.AppendLine($"{map.mapId}/{tier} → {cfg.name}");
            }

            AssetDatabase.SaveAssets();
            log = sb.ToString();
            return cfg;
        }
    }
}
