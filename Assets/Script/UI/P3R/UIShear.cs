using UnityEngine;
using UnityEngine.UI;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// เฉือนกล่อง uGUI ให้เป็นสี่เหลี่ยมด้านขนาน — ลายเซ็นของภาษาภาพ Persona 3 Reload
    ///
    /// **ข้อจำกัดที่ต้องรู้ก่อนใช้:**
    /// `TextMeshProUGUI` **ไม่สน** `BaseMeshEffect` — TMP สร้าง mesh เองคนละทาง
    /// ตัวนี้จึงใช้ได้กับ `Image` / `RawImage` เท่านั้น
    /// ถ้าอยากได้ตัวหนังสือเอียงให้ใช้ font asset ตัว Italic (`Sarabun-*Italic SDF`)
    /// แต่ design handoff ระบุไว้เองว่า "พื้นหลังปุ่มเอียง ตัวหนังสือไม่เอียง" — ตรงกับข้อจำกัดนี้พอดี
    /// จึงไม่ต้องทำอะไรเพิ่ม
    ///
    /// **เทียบกับ CSS:** `transform: skewX(-9deg)` ในแบบ = <see cref="angleDegrees"/> = **9**
    /// (CSS นับแกน y ลง Unity นับขึ้น เครื่องหมายจึงกลับกัน · ค่าบวก = ด้านบนเอนไปทางขวา)
    ///
    /// เฉือนรอบ **กึ่งกลางแนวตั้งของ rect** ไม่ใช่รอบ pivot — ย้าย pivot แล้วทรงไม่เปลี่ยน
    /// </summary>
    [AddComponentMenu("UI/Effects/UI Shear")]
    [DisallowMultipleComponent]
    public class UIShear : BaseMeshEffect
    {
        [Tooltip("องศาที่เอน · ค่าบวก = ด้านบนเอนไปทางขวา\n" +
                 "CSS skewX(-9deg) ของ design handoff = 9 ที่นี่")]
        [Range(-45f, 45f)] public float angleDegrees = 9f;

        [Tooltip("เฉือนแนวตั้งแทน (y ขยับตาม x) — สำหรับแบนเนอร์ที่แบบใช้ skewY(-4.5deg)\n" +
                 "CSS skewY(-4.5deg) = angleDegrees 4.5 พร้อมติ๊กช่องนี้")]
        public bool shearVertical;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            float tan = Mathf.Tan(angleDegrees * Mathf.Deg2Rad);
            if (Mathf.Approximately(tan, 0f)) return;

            Rect  r      = ((RectTransform)transform).rect;
            float pivotX = r.center.x;
            float pivotY = r.center.y;

            var v = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                var p = v.position;

                if (shearVertical) p.y += (p.x - pivotX) * tan;
                else               p.x += (p.y - pivotY) * tan;

                v.position = p;
                vh.SetUIVertex(v, i);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (graphic != null) graphic.SetVerticesDirty();
        }
#endif
    }
}
