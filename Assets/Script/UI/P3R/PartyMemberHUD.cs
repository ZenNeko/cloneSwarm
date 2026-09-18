using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// รายชื่อเพื่อนร่วมทีมบน HUD ตอนเล่น — อ่านผู้เล่นทุกคนแล้วป้อนลง <see cref="PartyMemberRowUI"/>
    ///
    /// ═══ อ่านรายชื่อผู้เล่นจากไหน ═══
    ///
    /// **ห้ามใช้ `NetworkManager.ConnectedClientsList`** — มันเป็นของฝั่ง server เท่านั้น
    /// เรียกบน client ได้ลิสต์ว่าง (หรือ throw) · อาการคือ host เห็นครบ client เห็นแถวเดียว
    /// ซึ่งเจอได้ต่อเมื่อทดสอบสองเครื่อง
    ///
    /// ใช้ `SpawnManager.PlayerObjects` แทน — NGO เติมลิสต์นี้ให้ทุก peer ตอน spawn
    ///
    /// ═══ ทำไมไม่ subscribe NetworkVariable ═══
    ///
    /// จำนวนแถวเปลี่ยนได้ตลอด (คนเข้า/หลุด/ตาย) การผูก-ถอด callback ให้ตรงกับ pool
    /// เป็นจุดที่ลืมบ่อยที่สุด · อ่านค่าเป็นจังหวะแทน — ค่าพวกนี้เปลี่ยนช้ากว่าเฟรมมาก
    /// และจอนี้ไม่ได้ต้องการความแม่นระดับเฟรม
    /// </summary>
    [DisallowMultipleComponent]
    public class PartyMemberHUD : MonoBehaviour
    {
        [Header("── Wiring ─────────────────────────────")]
        [Tooltip("แม่แบบแถวหนึ่งบรรทัด — **ต้องชี้ไฟล์ prefab** ไม่ใช่ของในซีน\n\n" +
                 "ของในซีนเป็น fileID ที่เปลี่ยนทุกครั้งที่ย้ายจอ แล้วแม่แบบจะหายไปพร้อมแผงเก่า")]
        public PartyMemberRowUI rowTemplate;
        [Tooltip("ที่วางแถว — ปล่อยว่าง = วางบน GameObject ตัวเอง")]
        public RectTransform    rowArea;

        [Header("── Layout ─────────────────────────────")]
        [Tooltip("ความสูงของหนึ่งแถว (px ที่ 1920×1080)")]
        public float rowHeight = 48f;
        [Tooltip("ช่องไฟระหว่างแถว")]
        public float rowGap    = 6f;
        [Tooltip("true = แถวเรียงขึ้นบน (ต่อจากสถานะตัวเองที่อยู่ล่าง) · false = เรียงลงล่าง")]
        public bool  stackUpward = true;

        [Header("── Behaviour ──────────────────────────")]
        [Tooltip("วินาทีระหว่างการอ่านค่าใหม่ · 0 = ทุกเฟรม (ไม่จำเป็น)")]
        public float refreshInterval = 0.25f;
        // เคยตั้งเป็น true ด้วยเหตุผลว่าอยากเห็นตัวเลขของตัวเองเทียบกับเพื่อนในบรรทัดเดียวกัน
        // พอเปิดตัวเกมจริงดูแล้วมันคือการโชว์ HP ของเราซ้ำสองที่บนจอเดียวกัน ทั้งที่แถบสถานะ
        // ล่างซ้ายบอกอยู่แล้ว — แถวปาร์ตี้มีไว้ดูคนที่เรามองไม่เห็น ไม่ใช่ดูตัวเอง
        [Tooltip("true = โชว์ตัวเราเองด้วย · false = โชว์เฉพาะเพื่อน")]
        public bool  includeSelf = false;

        private readonly List<PartyMemberRowUI> spawned = new();
        private float timer;

        private void OnEnable()
        {
            if (rowTemplate != null && rowTemplate.gameObject.scene.IsValid())
                rowTemplate.gameObject.SetActive(false);
            timer = 0f;
            Refresh();
        }

        private void Update()
        {
            if (refreshInterval <= 0f) { Refresh(); return; }

            // unscaled — ตอนเลือกการ์ด/หยุดเกม timeScale เป็น 0 แต่แถวยังต้องตรงความจริง
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = refreshInterval;
            Refresh();
        }

        /// <summary>อ่านผู้เล่นทุกคนแล้วเขียนลงแถว — เรียกเองได้ทุกเมื่อ</summary>
        public void Refresh()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null || rowTemplate == null)
            {
                HideAllRows();
                return;
            }

            ulong me = nm.LocalClientId;

            int used = 0;
            foreach (var obj in nm.SpawnManager.PlayerObjects)
            {
                if (obj == null) continue;
                var pm = obj.GetComponent<playermove>();
                if (pm == null) continue;
                if (!includeSelf && obj.OwnerClientId == me) continue;

                var row = RowAt(used);
                Bind(row, pm, obj.OwnerClientId);
                Place(row, used);
                used++;
            }

            for (int i = used; i < spawned.Count; i++)
                if (spawned[i] != null) spawned[i].gameObject.SetActive(false);
        }

        private void Bind(PartyMemberRowUI row, playermove pm, ulong clientId)
        {
            // สีประจำช่อง **ต้องผ่าน PlayerSlotRegistry** ห้ามใช้ clientId % 4 (CLAUDE.md ข้อ 11)
            // clientId ไม่ได้เริ่มที่ 0 และไม่ได้เรียงต่อกันเมื่อมีคนหลุดกลางทาง
            int slot = PlayerSlotRegistry.Instance != null
                     ? PlayerSlotRegistry.Instance.GetSlot(clientId)
                     : 0;

            row.SetIdentity(slot + 1, CharacterNameOf(pm), SlotColor(slot));

            if (pm.isDead.Value) row.SetDowned(pm.respawnCountdown.Value);
            else                 row.SetAlive(pm.netHealth.Value, pm.netMaxHealth.Value);
        }

        /// <summary>
        /// ชื่อตัวละครที่ **อ่านได้จากทุกเครื่อง**
        ///
        /// `PlayerWeaponManager.characterData` เป็นช่อง Inspector ที่มีค่าเฉพาะฝั่ง owner —
        /// ใช้ตัวนั้นอย่างเดียวแล้วเพื่อนร่วมทีมจะขึ้น "—" ทุกคน และเจอได้ต่อเมื่อทดสอบสองเครื่อง
        /// (`ResultPartyRowUI` เขียนข้อจำกัดนี้ไว้แล้วแต่ยังไม่ได้แก้)
        ///
        /// ของจริงที่ sync ข้ามเครื่องคือ `PlayerVisual.CharacterIndex` ซึ่งเป็น NetworkVariable
        /// แบบ Everyone read · ตกกลับไปใช้ช่อง Inspector เฉพาะตอนที่ยังไม่มี index (เล่นคนเดียว
        /// หรือยังไม่ได้เลือกตัวละคร)
        /// </summary>
        private static string CharacterNameOf(playermove pm)
        {
            var visual = pm.GetComponent<PlayerVisual>();
            var cd = visual != null ? visual.GetCharacterData(visual.CharacterIndex) : null;

            if (cd == null) cd = pm.GetComponent<PlayerWeaponManager>()?.characterData;

            return cd != null ? cd.characterName : null;
        }

        /// <summary>
        /// สีประจำช่อง — ชุดเดียวกับที่ผู้เล่นเห็นบนตัวละครในสนาม
        /// เก็บไว้ที่นี่เพราะยังไม่มี asset กลางที่ถือสีสี่ช่องนี้ · ถ้าวันหนึ่งมี ให้ย้ายไปอ่านจากที่นั่น
        /// </summary>
        private static Color SlotColor(int slot) => slot switch
        {
            0 => new Color32(0x3B, 0x6B, 0xFF, 0xFF),   // น้ำเงิน
            1 => new Color32(0xE8, 0x3A, 0x3A, 0xFF),   // แดง
            2 => new Color32(0x3E, 0xE0, 0x7A, 0xFF),   // เขียว
            _ => new Color32(0xF2, 0xC4, 0x3C, 0xFF),   // เหลือง
        };

        private PartyMemberRowUI RowAt(int index)
        {
            while (spawned.Count <= index)
            {
                var parent = rowArea != null ? rowArea : (RectTransform)transform;
                var clone  = Instantiate(rowTemplate, parent);
                clone.name = $"PartyRow_{spawned.Count}";
                spawned.Add(clone);
            }

            var row = spawned[index];
            row.gameObject.SetActive(true);
            return row;
        }

        private void Place(PartyMemberRowUI row, int index)
        {
            var rt = row.transform as RectTransform;
            if (rt == null) return;

            float step = rowHeight + rowGap;
            float y    = stackUpward ? index * step : -index * step;
            rt.anchoredPosition = new Vector2(0f, y);
        }

        private void HideAllRows()
        {
            foreach (var r in spawned)
                if (r != null) r.gameObject.SetActive(false);
        }
    }
}
