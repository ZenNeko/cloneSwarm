using Unity.Netcode;
using UnityEngine;

namespace CloneSwarm.Meta
{
    /// <summary>
    /// นับทอง/สถิติระหว่าง run แล้วจ่ายเข้าไฟล์เซฟตอนจบเกม
    ///
    /// **วางบน GameObject เดียวกับ GameTimeline** (ต้องมี NetworkObject ใน scene)
    ///
    /// Flow:
    ///   server  — Enemy.OnEnemyDiedServer → สะสม runGold + killCount
    ///   จบเกม   — server คำนวณทองของแต่ละคน (คูณ GoldFinder ของคนนั้น)
    ///             → ส่ง ClientRpc แบบเจาะจงคนละค่า
    ///   client  — MetaProgression.RecordRunResult() เขียนลงเซฟของเครื่องตัวเอง
    /// </summary>
    public class RunRewardTracker : NetworkBehaviour
    {
        public static RunRewardTracker Instance { get; private set; }

        /// <summary>ยิงบน client ที่ได้รางวัล — WinLoseUI subscribe เพื่อโชว์ตัวเลข</summary>
        public static event System.Action<int, int> OnRewardGranted;   // (goldEarned, totalGold)

        // ── Server state ──────────────────────────────────────────────────
        float baseRunGold;
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

        /// <summary>ให้ระบบอื่น (objective / crate) เติมทองพิเศษได้ — server only</summary>
        public void AddBonusGold(float amount)
        {
            if (!IsServer || amount <= 0f) return;
            baseRunGold += amount;
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
            float minutes  = survivedSec / 60f;
            float timeGold = minutes * (db != null ? db.goldPerMinuteSurvived : 20);
            float winGold  = won ? (db != null ? db.winBonusGold : 500) : 0;

            float total = baseRunGold + timeGold + winGold;

            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                var po = client.PlayerObject;
                float mult = po != null
                    ? (po.GetComponent<PlayerTalentApplier>()?.GoldFindMultiplier ?? 1f)
                    : 1f;

                int goldForThisPlayer = Mathf.RoundToInt(total * mult);

                GrantRewardClientRpc(goldForThisPlayer, won, survivedSec, level, killCount,
                    new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new[] { client.ClientId } }
                    });
            }

            Debug.Log($"[Reward] จบเกม {(won ? "WIN" : "LOSE")} · base {baseRunGold:F0} + " +
                      $"time {timeGold:F0} + win {winGold:F0} = {total:F0} (ก่อนคูณ GoldFinder)");
        }

        /// <summary>รันบนเครื่องผู้เล่นแต่ละคน — เขียนลงเซฟของเครื่องนั้น</summary>
        [ClientRpc]
        void GrantRewardClientRpc(int gold, bool won, float survivedSec, int level, int kills,
                                  ClientRpcParams rpcParams = default)
        {
            MetaProgression.RecordRunResult(won, gold, survivedSec, level, kills);
            OnRewardGranted?.Invoke(gold, MetaProgression.Gold);
        }
    }
}
