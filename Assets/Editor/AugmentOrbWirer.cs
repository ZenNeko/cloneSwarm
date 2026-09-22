using System.Linq;
using System.Text;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// ต่อ <c>AugOrb.prefab</c> ให้เป็น orb ที่เก็บแล้วได้การ์ด augment
    /// เมนู: Tools > Clone Swarm > Wire Augment Orb
    ///
    /// ═══ prefab ที่ส่งมามีแต่รูป ═══
    ///
    /// `AugOrb.prefab` เป็น variant ของโมเดล StarGem1 + สคริปต์หมุน/ลอย เท่านั้น
    /// ไม่มี `NetworkObject` · ไม่มี collider · ไม่มีสคริปต์เกม — วางลงซีนแล้ว
    /// มันจะเป็นก้อนหินสวยๆ ที่เก็บไม่ได้ และ **ไม่มี error ให้เห็นด้วย**
    ///
    /// ═══ ใช้ ObjectiveOrb ตัวเดิม ไม่ทำสคริปต์ใหม่ ═══
    ///
    /// การดูดเข้าหาผู้เล่น · รัศมีเก็บ · การคูณด้วย pickupRadius ของสเตตัส ·
    /// การ despawn · VFX ตอนเก็บ — เหมือนกันทุกบรรทัดกับ orb เดิม
    /// ต่างกันแค่ **กองที่สุ่มการ์ด** ซึ่งตอนนี้เป็นช่อง `reward` ช่องเดียว
    ///
    /// ถ้าก๊อป `ObjectiveOrb` ไปเป็น `AugmentOrb` จะได้โค้ดซ้ำ 130 บรรทัดที่
    /// รอวันเพี้ยนออกจากกัน — บั๊กแบบเดียวกับแถบ build ที่เคยมีสองอัน
    ///
    /// ═══ ต้องลงทะเบียน network prefab ด้วย ═══
    ///
    /// NGO ปฏิเสธการ spawn prefab ที่ไม่อยู่ใน `DefaultNetworkPrefabs.asset`
    /// (CLAUDE.md ข้อ 7) · ลืมข้อนี้แล้วอาการคือ orb ไม่โผล่ฝั่ง client เท่านั้น
    /// host เห็นปกติ — เจอได้ต่อเมื่อทดสอบสองเครื่อง
    ///
    /// รันซ้ำได้ — ต่อครบแล้วก็บอกว่าไม่มีอะไรต้องทำ
    /// </summary>
    public static class AugmentOrbWirer
    {
        private const string OrbPrefab  = "Assets/Prefab/Exp orb/AugOrb.prefab";
        private const string PrefabList = "Assets/DefaultNetworkPrefabs.asset";

        [MenuItem("Tools/Clone Swarm/Wire Augment Orb")]
        public static void Wire()
        {
            var log = new StringBuilder($"[AugOrb] {OrbPrefab}\n");
            int n = 0;

            var root = PrefabUtility.LoadPrefabContents(OrbPrefab);
            if (root == null)
            {
                Debug.LogError($"[AugOrb] เปิด {OrbPrefab} ไม่ได้ — ย้ายไฟล์ไปไหนหรือเปล่า");
                return;
            }

            try
            {
                if (root.GetComponent<NetworkObject>() == null)
                {
                    root.AddComponent<NetworkObject>();
                    log.AppendLine("  ใส่ NetworkObject");
                    n++;
                }

                // collider เป็น **ทางสำรอง** — ObjectiveOrb เก็บด้วยระยะใน Update อยู่แล้ว
                // แต่ถ้าผู้เล่นวิ่งผ่านเร็วกว่ากรอบเดียว ระยะอาจข้ามไป trigger รับไว้แทน
                var col = root.GetComponent<SphereCollider>();
                if (col == null)
                {
                    col = root.AddComponent<SphereCollider>();
                    col.radius = 0.6f;
                    log.AppendLine("  ใส่ SphereCollider (r=0.6)");
                    n++;
                }
                if (!col.isTrigger)
                {
                    col.isTrigger = true;
                    log.AppendLine("  ตั้ง collider เป็น trigger (ไม่งั้นมันจะดันผู้เล่น)");
                    n++;
                }

                var orb = root.GetComponent<ObjectiveOrb>();
                if (orb == null)
                {
                    orb = root.AddComponent<ObjectiveOrb>();
                    log.AppendLine("  ใส่ ObjectiveOrb");
                    n++;
                }

                if (orb.reward != OrbReward.Augment)
                {
                    orb.reward = OrbReward.Augment;
                    log.AppendLine("  ตั้ง reward = Augment");
                    n++;
                }

                if (n > 0) PrefabUtility.SaveAsPrefabAsset(root, OrbPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            n += RegisterNetworkPrefab(log);

            if (n == 0) { Debug.Log(log.AppendLine("  ต่อครบแล้ว ไม่มีอะไรต้องทำ").ToString()); return; }

            AssetDatabase.SaveAssets();
            log.AppendLine($"  เสร็จ ({n} รายการ)");
            log.AppendLine("  ทดสอบ: ลาก AugOrb ลง SampleScene แล้วกด Play — เดินไปชนแล้วจอเลือกการ์ดต้องเด้ง");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// ใส่ชื่อลงทะเบียน network prefab — NGO ไม่ยอม spawn ของที่ไม่อยู่ในลิสต์
        /// เทียบด้วยตัว asset ไม่ใช่ชื่อ · ลิสต์นี้มีเป็นร้อยรายการ การเทียบชื่อจะพลาด
        /// </summary>
        private static int RegisterNetworkPrefab(StringBuilder log)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(PrefabList);
            if (list == null)
            {
                Debug.LogError($"[AugOrb] หา {PrefabList} ไม่เจอ — NGO จะ spawn orb ไม่ได้");
                return 0;
            }

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(OrbPrefab);
            if (go == null) return 0;

            if (list.PrefabList.Any(p => p != null && p.Prefab == go))
            {
                log.AppendLine("  ลงทะเบียน network prefab ไว้แล้ว");
                return 0;
            }

            list.Add(new NetworkPrefab { Prefab = go });
            EditorUtility.SetDirty(list);
            log.AppendLine($"  ลงทะเบียนเข้า {PrefabList}");
            return 1;
        }
    }
}
