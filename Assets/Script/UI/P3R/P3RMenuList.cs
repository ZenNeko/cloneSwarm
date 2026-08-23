using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// รายการเมนูแนวตั้งสไตล์ P3R — เจ้าของการเลือก อนิเมชันเข้า และเสียง
    ///
    /// ใช้ Input System อย่างเดียว (ProjectSettings activeInputHandler = 1 · ไม่มี Input.GetKey เก่า)
    /// ทุกอนิเมชันใช้ unscaledDeltaTime — เมนูต้องลื่นแม้ timeScale = 0
    ///
    /// การต่อออก: subscribe <see cref="OnConfirm"/> ในโค้ด หรือลาก UnityEvent ใน Inspector
    /// ค่าที่ส่งกลับคือ <see cref="P3RMenuItem.id"/> (string) ไม่ใช่ index — สลับลำดับรายการแล้วไม่พัง
    /// </summary>
    [DisallowMultipleComponent]
    public class P3RMenuList : MonoBehaviour
    {
        [Header("── Theme ──────────────────────────────")]
        public P3RTheme theme;

        [Header("── Items ──────────────────────────────")]
        [Tooltip("เรียงบนลงล่างตามที่เห็นบนจอ · builder เติมให้อัตโนมัติ")]
        public List<P3RMenuItem> items = new();

        [Header("── Behaviour ──────────────────────────")]
        [Tooltip("เล่นอนิเมชันไถลเข้าทุกครั้งที่ panel ถูกเปิด")]
        public bool playIntroOnEnable = true;

        [Tooltip("เลื่อนจากตัวล่างสุดแล้วกดลงต่อ → วนกลับตัวบนสุด")]
        public bool wrapAround = true;

        [Tooltip("รับ Esc เป็นปุ่มยกเลิก แล้วยิง OnCancel")]
        public bool listenEscape = true;

        [Header("── Events ─────────────────────────────")]
        [Tooltip("ส่ง P3RMenuItem.id ของรายการที่ถูกยืนยัน")]
        public UnityEvent<string> onConfirmEvent;
        public UnityEvent onCancelEvent;

        /// <summary>ยิงพร้อม id ของรายการที่ถูกยืนยัน</summary>
        public event Action<string> OnConfirm;
        public event Action OnCancel;

        // ── runtime ────────────────────────────────────────────────────────
        private int index = -1;
        private bool introPlaying;
        private Coroutine introRoutine;

        public P3RMenuItem Current => (index >= 0 && index < items.Count) ? items[index] : null;

        // ═══════════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════════════════════
        private void Awake()
        {
            foreach (var it in items) if (it != null) it.Apply(theme);
        }

        private void OnEnable()
        {
            // เลือกตัวแรกที่กดได้ ไม่ใช่ตัวแรกในลิสต์ — CHANGE EPISODE เป็นตัวเทาที่ห้ามถูกเลือก
            int first = NextInteractable(-1, +1, allowWrap: true);
            SetIndex(first, instant: true, playSfx: false);

            if (playIntroOnEnable)
            {
                if (introRoutine != null) StopCoroutine(introRoutine);
                introRoutine = StartCoroutine(PlayIntro());
            }
        }

        private void OnDisable()
        {
            if (introRoutine != null) { StopCoroutine(introRoutine); introRoutine = null; }
            introPlaying = false;
        }

        // ═══════════════════════════════════════════════════════════════════
        // INPUT
        // ═══════════════════════════════════════════════════════════════════
        private void Update()
        {
            TickBars();
            if (introPlaying) return;   // ระหว่างของยังบินเข้า ห้ามรับอินพุต

            int step = ReadStep();
            if (step != 0) SetIndex(NextInteractable(index, step, wrapAround), instant: false, playSfx: true);

            if (ReadConfirm()) ConfirmItem(Current);
            else if (listenEscape && ReadCancel()) RaiseCancel();
        }

        private int ReadStep()
        {
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) return -1;
                if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) return +1;
            }
            // dpad เท่านั้น — analog stick ต้องมี deadzone + repeat timer ซึ่งยังไม่ได้ทำ
            var g = Gamepad.current;
            if (g != null)
            {
                if (g.dpad.up.wasPressedThisFrame) return -1;
                if (g.dpad.down.wasPressedThisFrame) return +1;
            }
            return 0;
        }

        private bool ReadConfirm()
        {
            var k = Keyboard.current;
            if (k != null && (k.enterKey.wasPressedThisFrame ||
                              k.numpadEnterKey.wasPressedThisFrame ||
                              k.spaceKey.wasPressedThisFrame)) return true;
            var g = Gamepad.current;
            return g != null && g.buttonSouth.wasPressedThisFrame;
        }

        private bool ReadCancel()
        {
            var k = Keyboard.current;
            if (k != null && k.escapeKey.wasPressedThisFrame) return true;
            var g = Gamepad.current;
            return g != null && g.buttonEast.wasPressedThisFrame;
        }

        // ═══════════════════════════════════════════════════════════════════
        // SELECTION
        // ═══════════════════════════════════════════════════════════════════
        /// <summary>หาตัวถัดไปที่กดได้ · ข้ามตัวเทาทั้งหมด · คืน from เดิมถ้าไม่มีตัวไหนกดได้เลย</summary>
        private int NextInteractable(int from, int step, bool allowWrap)
        {
            if (items.Count == 0) return -1;
            int i = from;
            for (int guard = 0; guard < items.Count; guard++)
            {
                i += step;
                if (i < 0 || i >= items.Count)
                {
                    if (!allowWrap) return from;
                    i = (i + items.Count) % items.Count;
                }
                if (items[i] != null && items[i].interactable) return i;
            }
            return from;
        }

        public void SetIndex(int newIndex, bool instant, bool playSfx)
        {
            if (newIndex < 0 || newIndex >= items.Count) return;
            if (newIndex == index) return;

            if (Current != null) Current.SetSelected(false, instant);
            index = newIndex;
            if (Current != null) Current.SetSelected(true, instant);

            if (playSfx && theme != null) PlayClip(theme.moveSfx);
        }

        /// <summary>เมาส์ชี้โดนรายการ — ชี้คือเลือก ไม่มีสถานะกลาง</summary>
        public void SelectByItem(P3RMenuItem item, bool fromPointer)
        {
            if (introPlaying) return;
            int i = items.IndexOf(item);
            if (i >= 0) SetIndex(i, instant: false, playSfx: true);
        }

        public void ConfirmItem(P3RMenuItem item)
        {
            if (introPlaying || item == null || !item.interactable) return;
            if (item != Current) SetIndex(items.IndexOf(item), instant: false, playSfx: false);

            if (theme != null) PlayClip(theme.confirmSfx);
            OnConfirm?.Invoke(item.id);
            onConfirmEvent?.Invoke(item.id);
        }

        private void RaiseCancel()
        {
            if (theme != null) PlayClip(theme.cancelSfx);
            OnCancel?.Invoke();
            onCancelEvent?.Invoke();
        }

        /// <summary>เปิด/ปิดรายการตาม id — เช่นปิด CONTINUE ตอนยังไม่มีเซฟ</summary>
        public void SetInteractable(string id, bool on)
        {
            foreach (var it in items)
            {
                if (it == null || it.id != id) continue;
                it.interactable = on;
                it.SetSelected(it == Current, instant: true);
                // ถ้าตัวที่กำลังเลือกอยู่เพิ่งถูกปิด ต้องเด้งไปตัวที่กดได้ทันที
                if (it == Current && !on)
                {
                    int fallback = NextInteractable(index, +1, allowWrap: true);
                    index = -1;
                    SetIndex(fallback, instant: true, playSfx: false);
                }
                return;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // MOTION
        // ═══════════════════════════════════════════════════════════════════
        /// <summary>
        /// ไล่แถบของทุกตัวเข้าหาเป้าแบบ MoveTowards (เชิงเส้น) ไม่ใช่ Lerp
        /// ตั้งใจให้เป็นการ "กวาด" ด้วยความเร็วคงที่ — Lerp จะให้หางนุ่มซึ่งผิดภาษา P3R
        /// </summary>
        private void TickBars()
        {
            if (theme == null) return;
            float speed = theme.barTweenDuration <= 0f ? 999f : 1f / theme.barTweenDuration;
            float dt = Time.unscaledDeltaTime * speed;

            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it == null) continue;
                float target = (i == index && it.interactable) ? 1f : 0f;
                float cur = it.BarReveal;
                if (!Mathf.Approximately(cur, target)) it.BarReveal = Mathf.MoveTowards(cur, target, dt);
            }
        }

        private IEnumerator PlayIntro()
        {
            if (theme == null || items.Count == 0) yield break;
            introPlaying = true;

            foreach (var it in items) if (it != null) it.SetIntroProgress(0f, theme.introSlideDistance);

            float total = theme.introStagger * Mathf.Max(0, items.Count - 1) + theme.introDuration;
            float t = 0f;
            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] == null) continue;
                    float local = (t - i * theme.introStagger) / Mathf.Max(0.0001f, theme.introDuration);
                    local = Mathf.Clamp01(local);
                    items[i].SetIntroProgress(theme.EaseOutBack(local), theme.introSlideDistance);
                }
                yield return null;
            }

            foreach (var it in items) if (it != null) it.SetIntroProgress(1f, theme.introSlideDistance);
            introPlaying = false;
            introRoutine = null;
        }

        // ═══════════════════════════════════════════════════════════════════
        // AUDIO — ผ่าน SoundManager เสมอ ตาม CLAUDE.md ข้อ 3
        // ═══════════════════════════════════════════════════════════════════
        private void PlayClip(AudioClip clip)
        {
            if (clip == null) return;
            if (SoundManager.Instance != null) SoundManager.Instance.PlaySfx2D(clip, theme.sfxVolume);
        }

#if UNITY_EDITOR
        /// <summary>ยัด theme ลงทุกรายการใหม่โดยไม่ต้องกด Play — ใช้ตอนจูนค่าใน Inspector</summary>
        [ContextMenu("Apply Theme To Items")]
        private void EditorApplyTheme()
        {
            foreach (var it in items)
            {
                if (it == null) continue;
                it.Apply(theme);
                UnityEditor.EditorUtility.SetDirty(it);
            }
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
