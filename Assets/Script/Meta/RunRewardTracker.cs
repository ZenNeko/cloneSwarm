using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CloneSwarm.Meta
{
    /// <summary>
    /// เหตุผลที่ได้ทองหนึ่งบรรทัดในจอสรุปผล
    ///
    /// มีเฉพาะเหตุผลที่ **ระบบนับจริงอยู่แล้ว** เท่านั้น — ห้ามเพิ่มค่าใหม่จนกว่าจะมีตัวนับจริง
    /// ("บอสที่ล้ม" / "เควสสำเร็จ" ยังไม่มีตัวนับ จึงยังไม่อยู่ในนี้ · ดูหมายเหตุที่ <see cref="RunRewardTracker"/>)
    /// </summary>
    public enum RewardReason : byte
    {
        EnemyKills    = 0,   // ทองจาก expReward ของศัตรูที่ตาย
        TimeSurvived  = 1,   // นาทีที่รอด × goldPerMinuteSurvived
        WinBonus      = 2,   // โบนัสตอนล้ม Main Boss สำเร็จ
        ObjectiveGold = 3,   // ทองพิเศษที่ระบบอื่นเติมผ่าน AddBonusGold()
        GoldFind      = 4,   // ส่วนต่างที่ talent GoldFind เพิ่มให้เฉพาะคนนี้
    }

    /// <summary>
    /// บัญชีรางวัลของผู้เล่น **หนึ่งคน** ที่ server คำนวณเสร็จแล้ว
    ///
    /// **ทำไมถึงเป็น struct ไม่ใช่พารามิเตอร์ RPC หลายตัว** — เหตุผลเดียวกับ TelegraphInit
    /// (CLAUDE.md หัวข้อ Telegraph): ค่าที่ client ต้องเห็นมีแนวโน้มจะเพิ่มเรื่อยๆ
    /// ถ้าเพิ่มเป็นพารามิเตอร์จะต้องแก้ signature ทุกครั้ง แล้วพังทุกจุดที่เรียก
    /// ยัดไว้ใน struct เดียวแล้วเติมฟิลด์แทน — จุดที่เรียกไม่ต้องแก้เลย
    ///
    /// ทุกค่าที่เป็นทองคือ **ทองจริงที่ผู้เล่นคนนี้ได้รับ** (คูณ GoldFind แล้วแยกส่วนต่างออกมาเป็นบรรทัดของตัวเอง)
    /// และประกันว่า killGold + timeGold + winGold + objectiveGold + goldFindGold == totalGold เป๊ะ
    /// เพราะจอสรุปผลเอาแต่ละบรรทัดมาบวกให้ผู้เล่นดู — บวกแล้วไม่ตรงยอดรวมคือบั๊กที่เห็นด้วยตา
    /// </summary>
    public struct RunRewardBreakdown : INetworkSerializable
    {
        // ── ผลของรอบ ────────────────────────────────────────────────────
        public bool  won;
        public float survivedSec;
        public int   level;
        public int   kills;

        // ── ทองแยกเหตุผล ────────────────────────────────────────────────
        public int killGold;
        public int timeGold;
        public int winGold;
        public int objectiveGold;
        public int goldFindGold;

        public float goldFindMultiplier;
        public int   totalGold;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref won);
            s.SerializeValue(ref survivedSec);
            s.SerializeValue(ref level);
            s.SerializeValue(ref kills);

            s.SerializeValue(ref killGold);
            s.SerializeValue(ref timeGold);
            s.SerializeValue(ref winGold);
            s.SerializeValue(ref objectiveGold);
            s.SerializeValue(ref goldFindGold);

            s.SerializeValue(ref goldFindMultiplier);
            s.SerializeValue(ref totalGold);
        }

        /// <summary>นาทีที่รอด (ปัดลง) — ใช้เขียนป้าย "รอดถึงนาที N"</summary>
        public int SurvivedMinutes => Mathf.FloorToInt(survivedSec / 60f);

        /// <summary>
        /// แตกเป็นบรรทัดตามลำดับที่จะโชว์ · skipZero ตัดบรรทัดที่ได้ 0 ทิ้ง
        /// (เช่น แพ้ = ไม่มีบรรทัดโบนัสชนะ · ไม่มี talent GoldFind = ไม่มีบรรทัดตัวคูณ)
        ///
        /// คืนเฉพาะ **ตัวเลข** ส่วนคำไทยอยู่ที่ฝั่ง UI (RewardLineUI.DefaultLabel)
        /// — struct นี้เดินทางข้ามเน็ตเวิร์ก ไม่ควรแบกข้อความจอไปด้วย
        /// </summary>
        public List<(RewardReason reason, int gold, int count)> ToLines(bool skipZero = true)
        {
            var list = new List<(RewardReason, int, int)>(5);
            void Add(RewardReason r, int g, int c) { if (!skipZero || g != 0) list.Add((r, g, c)); }

            Add(RewardReason.TimeSurvived,  timeGold,      SurvivedMinutes);
            Add(RewardReason.EnemyKills,    killGold,      kills);
            Add(RewardReason.ObjectiveGold, objectiveGold, 0);
            Add(RewardReason.WinBonus,      winGold,       0);
            Add(RewardReason.GoldFind,      goldFindGold,  0);
            return list;
        }
    }

    /// <summary>
    /// นับทอง/สถิติระหว่าง run แล้วจ่ายเข้าไฟล์เซฟตอนจบเกม
    ///
    /// **วางบน GameObject เดียวกับ GameTimeline** (ต้องมี NetworkObject ใน scene)
    ///
    /// Flow:
    ///   server  — Enemy.OnEnemyDiedServer → สะสม baseRunGold + killCount
    ///   จบเกม   — server คำนวณทองของแต่ละคน (คูณ GoldFinder ของคนนั้น)
    ///             → ส่ง ClientRpc แบบเจาะจงคนละค่า พร้อม RunRewardBreakdown แยกเหตุผล
    ///   client  — MetaProgression.RecordRunResult() เขียนลงเซฟของเครื่องตัวเอง
    ///
    /// **เหตุผลที่ยังไม่มีในบัญชี** (ห้ามแต่งตัวเลขขึ้นมาเอง):
    ///   • "บอสที่ล้ม N ตัว" — BossManager มีแค่ event ตอน Main Boss ตาย ไม่มีตัวนับมินิบอส
    ///     และ MetaDatabase ไม่มีค่าทองต่อบอส
    ///   • "เควสสำเร็จ N อัน" — ZoneObjective.OnObjectiveCompleted มีอยู่จริง แต่ไม่มีใครนับ
    ///     และไม่มีค่าทองต่อเควสใน MetaDatabase
    ///   ทั้งสองอันเสียบเข้าช่อง RewardReason.ObjectiveGold ได้ทันทีผ่าน AddBonusGold()
    ///   เมื่อไรที่ตกลงค่าทองกันแล้ว
    /// </summary>
    public class RunRewardTracker : NetworkBehaviour
    {
        public static RunRewardTracker Instance { get; private set; }

        /// <summary>ยิงบน client ที่ได้รางวัล — WinLoseUI subscribe เพื่อโชว์ตัวเลข
        /// **เก็บไว้เพื่อความเข้ากันได้** — ของใหม่ให้ใช้ OnRewardBreakdownGranted</summary>
        public static event System.Action<int, int> OnRewardGranted;   // (goldEarned, totalGold)

        /// <summary>ยิงบน client ที่ได้รางวัล พร้อมบัญชีแยกเหตุผล · (breakdown, ยอดทองในคลังหลังบวกแล้ว)</summary>
        public static event System.Action<RunRewardBreakdown, int> OnRewardBreakdownGranted;

        // ── Server state ──────────────────────────────────────────────────
        float baseRunGold;      // ทองจากศัตรูล้วนๆ
        float bonusGold;        // ทองพิเศษจากระบบอื่น (objective / crate) — แยกไว้เพื่อให้บัญชีอ่านออก
        int   killCount;
        bool  paidOut;

        // ═══════════════════════════════════════════════════════════════════
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnEnable()
        {
            GameTimeline.OnGameWon  += HandleWin;
            GameTimeline.OnGameLost += HandleLose;
        }

        void OnDisable()
        {
            GameTimeline.OnGameWon  -= HandleWin;
            GameTimeline.OnGameLost -= HandleLose;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) Enemy.OnEnemyDiedServer += HandleEnemyDied;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) Enemy.OnEnemyDiedServer -= HandleEnemyDied;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Accrual (server)
        // ═══════════════════════════════════════════════════════════════════
        void HandleEnemyDied(Enemy e, Vector3 _)
        {
            if (!IsServer || e == null) return;

            killCount++;
            float perExp = MetaDatabase.Instance != null ? MetaDatabase.Instance.goldPerExp : 0.1f;
            baseRunGold += e.expReward * perExp;
        }

        /// <summary>ให้ระบบอื่น (objective / crate) เติมทองพิเศษได้ — server only
        /// เข้าบรรทัด RewardReason.ObjectiveGold ในจอสรุปผล ไม่ปนกับทองจากศัตรู</summary>
        public void AddBonusGold(float amount)
        {
            if (!IsServer || amount <= 0f) return;
            bonusGold += amount;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Payout
        // ═══════════════════════════════════════════════════════════════════
        // GameTimeline ยิง event นี้บนทุก client (ผ่าน ClientRpc) — เอาเฉพาะฝั่ง server
        void HandleWin (float t, int lvl) { if (IsServer) PayOut(true,  t, lvl); }
        void HandleLose(float t, int lvl) { if (IsServer) PayOut(false, t, lvl); }

        void PayOut(bool won, float survivedSec, int level)
        {
            if (paidOut) return;
            paidOut = true;

            var db = MetaDatabase.Instance;
            float minutes   = survivedSec / 60f;
            float timeGoldF = minutes * (db != null ? db.goldPerMinuteSurvived : 20);
            float winGoldF  = won ? (db != null ? db.winBonusGold : 500) : 0;

            // ปัดทีละก้อน **ก่อน** รวม เพื่อให้บรรทัดในจอบวกกันได้ยอดตรงกับยอดรวมเป๊ะ
            // (ของเดิมรวมเป็น float แล้วปัดทีเดียว ยอดรวมจึงต่างจากผลบวกของบรรทัดได้ ±1 — ผู้เล่นเห็น)
            int killPart  = Mathf.RoundToInt(baseRunGold);
            int bonusPart = Mathf.RoundToInt(bonusGold);
            int timePart  = Mathf.RoundToInt(timeGoldF);
            int winPart   = Mathf.RoundToInt(winGoldF);
            int basePart  = killPart + bonusPart + timePart + winPart;

            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                var po = client.PlayerObject;
                float mult = po != null
                    ? (po.GetComponent<PlayerTalentApplier>()?.GoldFindMultiplier ?? 1f)
                    : 1f;

                int goldForThisPlayer = Mathf.RoundToInt(basePart * mult);

                var breakdown = new RunRewardBreakdown
                {
                    won                = won,
                    survivedSec        = survivedSec,
                    level              = level,
                    kills              = killCount,

                    killGold           = killPart,
                    timeGold           = timePart,
                    winGold            = winPart,
                    objectiveGold      = bonusPart,
                    // ส่วนต่างจาก talent เป็นบรรทัดของตัวเอง — คิดจากผลต่างเพื่อให้ยอดรวมไม่มีเศษหาย
                    goldFindGold       = goldForThisPlayer - basePart,

                    goldFindMultiplier = mult,
                    totalGold          = goldForThisPlayer,
                };

                GrantRewardClientRpc(breakdown,
                    new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new[] { client.ClientId } }
                    });
            }

            Debug.Log($"[Reward] จบเกม {(won ? "WIN" : "LOSE")} · kill {killPart} + time {timePart} + " +
                      $"win {winPart} + bonus {bonusPart} = {basePart} (ก่อนคูณ GoldFinder)");
        }

        /// <summary>รันบนเครื่องผู้เล่นแต่ละคน — เขียนลงเซฟของเครื่องนั้น</summary>
        [ClientRpc]
        void GrantRewardClientRpc(RunRewardBreakdown b, ClientRpcParams rpcParams = default)
        {
            MetaProgression.RecordRunResult(b.won, b.totalGold, b.survivedSec, b.level, b.kills);

            OnRewardBreakdownGranted?.Invoke(b, MetaProgression.Gold);
            OnRewardGranted?.Invoke(b.totalGold, MetaProgression.Gold);
        }
    }
}
