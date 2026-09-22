using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// อ่านข้อความจากตารางแปล — ทางเดียวของทั้งเกม
    ///
    /// ═══ ทำไมไม่ใช้ GetLocalizedString ตรงๆ ═══
    ///
    /// ของ Unity คืนข้อความ `No translation found for '...'` ซึ่งบอกว่าพัง แต่ไม่บอกว่า
    /// **ต้องไปแก้ที่ไหน** · ที่นี่ดึง entry มาเช็คเองแล้วบ่นพร้อมชื่อเมนูที่ต้องรัน
    ///
    /// แยก "ไม่มี key" ออกจาก "มี key แต่ค่าในภาษานี้ว่าง" ด้วย เพราะสองอย่างนี้แก้
    /// คนละที่ — อันแรกแก้ที่ตัว builder อันหลังแก้ที่ตารางโดยคนแปล
    ///
    /// ═══ คืน key ไม่ใช่คืนค่าว่าง ═══
    ///
    /// จอที่มีช่องว่างอ่านไม่ออกว่าตั้งใจหรือพัง · key ที่โผล่บนจอบอกทั้งว่าพัง
    /// และพังที่ไหน ซึ่งคือสิ่งที่คนเห็นหน้าจอต้องการรู้
    /// </summary>
    public static class P3RStrings
    {
        /// <param name="fixHint">ประโยคบอกวิธีแก้ — ต่อท้าย warning ให้คนอ่านทำต่อได้ทันที</param>
        public static string Resolve(string table, string key, string fixHint, params object[] args)
        {
            if (string.IsNullOrEmpty(key)) return "";

            string reason;
            try
            {
                var entry = LocalizationSettings.StringDatabase.GetTableEntry(table, key).Entry;

                if (entry != null)
                {
                    string text = args != null && args.Length > 0
                                ? entry.GetLocalizedString((IList<object>)args)
                                : entry.GetLocalizedString();

                    if (!string.IsNullOrEmpty(text)) return text;

                    reason = $"มี key แต่ค่าใน locale " +
                             $"'{LocalizationSettings.SelectedLocale?.Identifier.Code}' ว่าง";
                }
                else reason = "ไม่มี key นี้ในตาราง";
            }
            catch (System.Exception e)
            {
                reason = $"อ่านตารางไม่ได้ ({e.GetType().Name}: {e.Message})";
            }

            Debug.LogWarning($"[แปล] '{table}/{key}' — {reason} · จะโชว์ตัว key แทน\n       {fixHint}");
            return key;
        }

        /// <summary>คำใบ้มาตรฐานของตาราง UI — เขียนที่เดียว ไม่ให้แต่ละจุดเรียกคิดเอง</summary>
        public const string UiTable = "UI";
        public const string UiFixHint =
            "แก้โดยเพิ่มแถวใน P3RUiTableBuilder แล้วรัน Tools > Clone Swarm > Localization > 4. Build UI Table";

        /// <summary>ทางลัดของตาราง UI ซึ่งเป็นตารางที่จอส่วนใหญ่ใช้</summary>
        public static string Ui(string key, params object[] args)
            => Resolve(UiTable, key, UiFixHint, args);
    }
}
