using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// ปุ่มแบ่งช่องเลือกระดับความยาก
    ///
    /// ต่างจาก <see cref="P3RSegmentedControl"/> ตรงที่ตัวนั้นขับ <c>TMP_Dropdown</c>
    /// ส่วนตัวนี้ขับ enum <c>DifficultyTier</c> ตรงๆ แล้วส่งต่อให้
    /// <c>LobbyUI.SelectDifficulty()</c> ซึ่งเป็นทางเดียวที่เขียนค่าขึ้นเน็ตเวิร์กได้
    /// (client เปลี่ยนเองไม่ได้ ต้องผ่าน ServerRpc ใน LobbyState — ดู CLAUDE.md ข้อ 9)
    ///
    /// **ชื่อที่โชว์ใช้ชื่อ enum จริง ไม่ใช่ชื่อในแบบ** — แบบเขียน NORMAL / HARD / NIGHTMARE
    /// แต่ในเกมคือ Easy / Normal / Hard / Savage / Epic · ถ้าโชว์ตามแบบ ผู้เล่นจะเลือก
    /// "NIGHTMARE" แล้วได้ Savage ซึ่งไม่ตรงกับที่เห็นทุกที่อื่นในเกม
    /// </summary>
    [DisallowMultipleComponent]
    public class P3RDifficultySelector : MonoBehaviour, IP3RSegmentOwner
    {
        [Header("── Target ─────────────────────────────")]
        [Tooltip("ปล่อยว่างได้ในซีนต้นแบบ — ตอนนั้นปุ่มจะเปลี่ยนแค่หน้าตา ไม่ส่งค่าไปไหน")]
        public LobbyUI lobbyUI;

        [Header("── Wiring ─────────────────────────────")]
        public P3RSegmentButton segmentTemplate;
        public RectTransform    segmentContainer;

        [Header("── Tiers ──────────────────────────────")]
        [Tooltip("ระดับที่ให้เลือกได้ · เรียงตามที่อยากให้เห็นบนจอ")]
        public DifficultyTier[] tiers =
        {
            DifficultyTier.Easy,
            DifficultyTier.Normal,
            DifficultyTier.Hard,
            DifficultyTier.Savage,
            DifficultyTier.Epic,
        };

        public DifficultyTier Current { get; private set; } = DifficultyTier.Normal;

        [Header("── Layout ─────────────────────────────")]
        public float segmentWidth   = 148f;
        public float segmentHeight  = 42f;
        public float segmentSpacing = 8f;
        public float labelTracking  = 12f;

        private readonly List<P3RSegmentButton> segments = new();

        private void OnEnable() => Rebuild();

        public void Rebuild()
        {
            if (segmentTemplate == null) return;

            var parent = segmentContainer != null
                       ? segmentContainer
                       : (RectTransform)segmentTemplate.transform.parent;
            if (parent == null) return;

            var doomed = new List<GameObject>();
            foreach (Transform child in parent)
            {
                if (child == segmentTemplate.transform) continue;
                doomed.Add(child.gameObject);
            }
            foreach (var go in doomed) DestroyImmediate(go);
            segments.Clear();

            for (int i = 0; i < tiers.Length; i++)
            {
                var seg = Instantiate(segmentTemplate, parent);
                seg.gameObject.SetActive(true);
                seg.name = $"Tier_{tiers[i]}";

                var rt = (RectTransform)seg.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot     = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(segmentWidth, segmentHeight);
                rt.anchoredPosition = new Vector2(i * (segmentWidth + segmentSpacing), 0f);

                seg.Init(this, i, tiers[i].ToString().ToUpperInvariant(), labelTracking);
                segments.Add(seg);
            }

            Highlight(System.Array.IndexOf(tiers, Current));
        }

        /// <summary>ถูกเรียกจาก <see cref="P3RSegmentButton"/> ตอนผู้เล่นกด</summary>
        public void Choose(int index)
        {
            if (index < 0 || index >= tiers.Length) return;
            Current = tiers[index];
            Highlight(index);

            // ต้องผ่าน LobbyUI — มันเป็นตัวที่รู้ว่าเครื่องนี้เป็น host ไหม
            // และเป็นคนเรียก LobbyState.SetDifficultyServerRpc ให้
            if (lobbyUI != null) lobbyUI.SelectDifficulty(Current);
        }

        /// <summary>ตั้งค่าที่เลือกโดยไม่ส่งต่อ — ใช้ตอนรับค่าที่ server ถืออยู่มาแสดง</summary>
        public void SetWithoutNotify(DifficultyTier tier)
        {
            Current = tier;
            Highlight(System.Array.IndexOf(tiers, tier));
        }

        private void Highlight(int index)
        {
            for (int i = 0; i < segments.Count; i++)
                if (segments[i] != null) segments[i].SetSelected(i == index);
        }
    }
}
