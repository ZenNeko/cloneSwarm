using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Per-character visual model + Owner-authoritative NetworkVariable sync
///
/// **ทำงานยังไง:**
///   1. Owner client อ่าน `CharacterSelectUI.SelectedCharacter` ตอน spawn → ส่ง index ผ่าน ServerRpc
///   2. Server set `characterIndex` (NetworkVariable) → broadcast ทุก client
///   3. ทุก client (รวม owner) `OnValueChanged` → spawn model prefab ของตัวละครนั้นเป็น child ของ modelHolder
///   4. Owner: คำนวณ Speed จาก local position delta (instant) + sync ผ่าน `_netSpeed` NetVar
///   5. Non-owner: อ่าน `_netSpeed.Value` → smooth → drive animator
///   6. Attack: ServerRpc → ClientRpc broadcast → ทุก client SetTrigger
///
/// **Setup บน Player Prefab:**
///   1. Add PlayerVisual component บน root (ที่มี NetworkObject + playermove)
///   2. สร้าง empty child ชื่อ "ModelHolder" → ลากมาใส่ modelHolder field
///   3. Assign `characters[]` array — เรียง CharacterData ทุกตัวที่ผู้เล่นเลือกได้
///
/// **Setup ในแต่ละ Character Model Prefab:**
///   • มี Animator component พร้อม Animator Controller
///   • Animator parameters (ตาม controller แบบ state transition):
///       - IsMoving (bool)  — Idle ↔ Run_N
///       - IsDead   (bool)  — → Die
///       - Attack   (trigger) — attack animation
///   • States: Idle (entry), Run_N, Die
///   • Transitions:
///       Idle → Run_N : IsMoving == true, Has Exit Time = false
///       Run_N → Idle : IsMoving == false, Has Exit Time = false
///       Any State / Idle → Die : IsDead == true, Has Exit Time = false
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class PlayerVisual : NetworkBehaviour
{
    [Title("Setup")]
    [Required("ลาก empty Transform ใต้ player prefab มาใส่ (จุดที่ model จะ spawn ออกมา)")]
    public Transform modelHolder;

    [Required("ใส่ CharacterData ทุกตัวที่ผู้เล่นเลือกได้ — index ใช้ sync ข้าม network")]
    public CharacterData[] characters;

    [Title("Animator Param Names")]
    [Tooltip("Bool param — true เมื่อ player กำลังเคลื่อนที่\n" +
             "Trigger transition Idle → Run_N (และ Run_N → Idle เมื่อ false)\n" +
             "ปล่อยว่าง = ไม่ใช้")]
    public string moveParam   = "IsMoving";

    [Tooltip("Float param สำหรับ Blend Tree (Idle ↔ Walk ↔ Run)\n" +
             "ใช้แทน moveParam ถ้า animator ใช้ Blend Tree\n" +
             "ปล่อยว่าง = ไม่ใช้ (animator แบบ state transition ใช้ moveParam แทน)")]
    public string speedParam  = "";

    [Tooltip("Bool param — true เมื่อ player ตาย — trigger transition → Die")]
    public string deadParam   = "IsDead";

    [Tooltip("Trigger param — เรียกตอนยิง/โจมตี")]
    public string attackParam = "Attack";

    [Tooltip("Float param สำหรับ state Multiplier (ถ้า state ใช้ 'Multiplier: Parameter')\n" +
             "ปล่อยว่าง = ไม่ใช้ (state แบบธรรมดาไม่ต้องใช้)")]
    public string motionSpeedParam = "";

    [Tooltip("ค่า MotionSpeed ตอนเดิน — 1 = ปกติ, สูงขึ้น = animation เร็วขึ้น\n" +
             "ใช้กับ motionSpeedParam ด้านบน (ถ้า set)")]
    [Range(0.1f, 3f)] public float motionSpeedValue = 1f;

    [Title("Movement Detection")]
    [Tooltip("ความเร็วต่ำสุด (เมตร/วินาที) ที่ถือว่ากำลังเคลื่อนที่ — กัน floating-point jitter")]
    [Range(0.01f, 1f)] public float movingThreshold = 0.05f;

    [Tooltip("ความเร็วสูงสุดของ player (เมตร/วินาที) สำหรับ normalize speedParam\n" +
             "เช่น player.speed = 5 → ตั้ง 5 ที่นี่ → Speed param จะอยู่ 0..1 พอดี\n" +
             "(ถ้า MoveSpeed stat เพิ่มขึ้นเกิน maxSpeed → ค่าจะ clamp ที่ 1)")]
    [Min(0.1f)] public float maxSpeed = 5f;

    [Tooltip("Smoothing speedParam — ทำให้ blend transition นุ่มขึ้น (วินาที)\n" +
             "0 = ไม่ smooth (ตอบสนองทันที) | 0.1-0.2 = smooth พอดี")]
    [Range(0f, 0.5f)] public float speedSmoothTime = 0.1f;

    [Title("Model Rotation")]
    [Tooltip("หมุน model ไปทางที่กำลังเดิน — playermove ไม่ rotate transform ให้\n" +
             "ปิด = model ไม่หมุน (ถ้าอยากใช้ logic อื่น เช่น aim weapon)")]
    public bool faceMoveDirection = true;

    [Tooltip("ความเร็วการหมุน (องศา/วินาที) — สูงขึ้น = หันเร็ว")]
    [Range(180f, 1440f)] public float rotateSpeed = 720f;

    // ── Network state ─────────────────────────────────────────────────────
    NetworkVariable<int> _charIndex = new(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Owner-authoritative speed sync — Owner คำนวณจาก local delta แล้ว push ให้ non-owners
    NetworkVariable<float> _netSpeed = new(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    float _speedSyncTimer;   // throttle update rate (10Hz)

    /// <summary>index ตัวละครที่ server validate แล้ว (-1 = ยังไม่ถูกเซ็ต)</summary>
    public int CharacterIndex => _charIndex.Value;

    /// <summary>อ่าน CharacterData แบบ bounds-checked — คืน null ถ้า index ไม่ถูกต้อง</summary>
    public CharacterData GetCharacterData(int idx)
    {
        if (characters == null || idx < 0 || idx >= characters.Length) return null;
        return characters[idx];
    }

    /// <summary>หา index ของ CharacterData ใน characters[] — คืน -1 ถ้าไม่เจอ</summary>
    public int IndexOfCharacter(CharacterData cd)
    {
        if (cd == null || characters == null) return -1;
        for (int i = 0; i < characters.Length; i++)
            if (characters[i] == cd) return i;
        return -1;
    }

    // ── Local cache ───────────────────────────────────────────────────────
    GameObject _spawnedModel;
    Animator   _animator;
    playermove _playerMove;
    Vector3    _lastPos;
    float      _smoothedSpeed;     // SmoothDamp output
    float      _smoothSpeedVel;    // SmoothDamp velocity

    // ── Lifecycle ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        _playerMove = GetComponent<playermove>();
        _lastPos    = transform.position;

        _charIndex.OnValueChanged += OnCharIndexChanged;

        if (IsOwner)
        {
            // Owner: อ่าน selection จาก static (set ใน MenuScene → CharacterSelectUI.OnConfirm)
            int idx = ResolveIndexFromSelection();
            SetCharacterServerRpc(idx);
        }
        else if (_charIndex.Value >= 0)
        {
            // Late-join: characterIndex มีค่าอยู่แล้ว → spawn model ทันที
            SpawnModel(_charIndex.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        _charIndex.OnValueChanged -= OnCharIndexChanged;
        if (_spawnedModel != null) Destroy(_spawnedModel);
    }

    // ── Per-frame: drive animator ─────────────────────────────────────────
    // Owner-authoritative pattern:
    //   • Owner: calc Speed จาก local delta (instant) → sync ผ่าน _netSpeed (10Hz throttle)
    //   • Non-owner: อ่าน _netSpeed.Value ที่ owner ส่งมา → smooth → drive animator
    //   • IsDead: ทุก client อ่าน playermove.isDead.Value (NetworkVariable แยกอยู่แล้ว)
    void Update()
    {
        if (_animator == null) return;

        // Position delta — ใช้ทุก client (Owner: local accurate, Non-owner: interpolated)
        Vector3 pos    = transform.position;
        Vector3 delta  = pos - _lastPos;
        delta.y        = 0f;
        _lastPos       = pos;

        // ── Calc target speed ──
        float targetSpeed;
        if (IsOwner)
        {
            // Owner: คำนวณ local — instant response
            float rawSpeed   = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            targetSpeed      = Mathf.Clamp01(rawSpeed / Mathf.Max(maxSpeed, 0.001f));
            if (targetSpeed * maxSpeed < movingThreshold) targetSpeed = 0f;

            // Throttle sync ไป non-owners (10Hz + threshold 0.02 กัน spam)
            _speedSyncTimer -= Time.deltaTime;
            if (_speedSyncTimer <= 0f)
            {
                _speedSyncTimer = 0.1f;
                if (Mathf.Abs(_netSpeed.Value - targetSpeed) > 0.02f)
                    _netSpeed.Value = targetSpeed;
            }
        }
        else
        {
            // Non-owner: read synced value จาก owner
            targetSpeed = _netSpeed.Value;
        }

        // ── Smooth + drive animator ──
        _smoothedSpeed = speedSmoothTime > 0f
            ? Mathf.SmoothDamp(_smoothedSpeed, targetSpeed, ref _smoothSpeedVel, speedSmoothTime)
            : targetSpeed;

        if (!string.IsNullOrEmpty(speedParam))
            _animator.SetFloat(speedParam, _smoothedSpeed);

        if (!string.IsNullOrEmpty(moveParam))
            _animator.SetBool(moveParam, _smoothedSpeed * maxSpeed > movingThreshold);

        if (!string.IsNullOrEmpty(motionSpeedParam))
            _animator.SetFloat(motionSpeedParam, motionSpeedValue);

        // IsDead — ทุก client อ่าน playermove.isDead.Value (NetworkVariable sync แล้ว)
        if (_playerMove != null && !string.IsNullOrEmpty(deadParam))
            _animator.SetBool(deadParam, _playerMove.isDead.Value);

        // ── Rotate model ตาม movement direction ──
        if (faceMoveDirection && _spawnedModel != null && _smoothedSpeed > 0.05f
            && delta.sqrMagnitude > 0.0001f)
        {
            Vector3 dir = delta.normalized;
            Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
            _spawnedModel.transform.rotation = Quaternion.RotateTowards(
                _spawnedModel.transform.rotation, target, rotateSpeed * Time.deltaTime);
        }
    }

    // ── Public API: trigger animations ────────────────────────────────────
    /// <summary>
    /// เรียกจาก WeaponBase ตอนยิง — Owner เห็นทันที + sync ไปทุก client ผ่าน RPC
    /// </summary>
    public void TriggerAttack()
    {
        PlayLocalAttack();                            // Owner เห็นทันที (no RTT delay)
        if (IsOwner) RequestAttackServerRpc();        // Sync ไปคนอื่น
    }

    void PlayLocalAttack()
    {
        if (_animator != null && !string.IsNullOrEmpty(attackParam))
            _animator.SetTrigger(attackParam);
    }

    [ServerRpc] void RequestAttackServerRpc() => BroadcastAttackClientRpc();

    [ClientRpc]
    void BroadcastAttackClientRpc()
    {
        if (IsOwner) return;   // Owner played already
        PlayLocalAttack();
    }

    // ── Network sync ──────────────────────────────────────────────────────
    int ResolveIndexFromSelection()
    {
        var selected = CharacterSelectUI.SelectedCharacter;
        if (selected == null || characters == null || characters.Length == 0)
        {
            Debug.LogWarning("[PlayerVisual] No SelectedCharacter — default to index 0");
            return 0;
        }
        for (int i = 0; i < characters.Length; i++)
            if (characters[i] == selected) return i;

        Debug.LogWarning($"[PlayerVisual] Selected '{selected.characterName}' ไม่อยู่ใน characters[] " +
                         "ของ player prefab — default index 0");
        return 0;
    }

    [ServerRpc]
    void SetCharacterServerRpc(int idx)
    {
        if (characters == null || characters.Length == 0) return;
        _charIndex.Value = Mathf.Clamp(idx, 0, characters.Length - 1);
    }

    void OnCharIndexChanged(int oldVal, int newVal) => SpawnModel(newVal);

    void SpawnModel(int idx)
    {
        if (characters == null || idx < 0 || idx >= characters.Length) return;

        // ล้าง model เก่า (กรณี change character ระหว่าง play — ปกติไม่เกิด แต่ safe)
        if (_spawnedModel != null) Destroy(_spawnedModel);

        var cd = characters[idx];
        if (cd == null || cd.characterModelPrefab == null)
        {
            Debug.LogWarning($"[PlayerVisual] CharacterData[{idx}] หรือ characterModelPrefab เป็น null");
            return;
        }

        Transform parent = modelHolder != null ? modelHolder : transform;
        _spawnedModel = Instantiate(cd.characterModelPrefab, parent);
        _spawnedModel.transform.localPosition = Vector3.zero;
        _spawnedModel.transform.localRotation = Quaternion.identity;
        // ใช้ scale ของ prefab — ผู้ออกแบบกำหนดขนาดมาแล้ว (เช่น 0.8 สำหรับตัวเตี้ย)
        _spawnedModel.transform.localScale    = cd.characterModelPrefab.transform.localScale;

        _animator = _spawnedModel.GetComponentInChildren<Animator>();
        if (_animator == null)
        {
            Debug.LogWarning($"[PlayerVisual] Model '{cd.characterModelPrefab.name}' ไม่มี Animator — animation จะไม่ทำงาน");
            return;
        }

        // ── สำคัญ: Force Animator ให้ animate ตลอด (ไม่ cull) ──
        //   Unity quirk: Animator culling อาจทำให้ blend tree param ค้างกลางทาง
        //   (เช่น Speed ค้างที่ 0.5 แม้ SetFloat = 1.0)
        //   อาการ: animation ดูถูกต้องเมื่อเปิด Animator window แต่เพี้ยนเมื่อปิด
        //   Fix: ตั้ง cullingMode = AlwaysAnimate → bypass camera/renderer culling
        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // กัน animation freeze เพิ่มเติม: ให้ Update Mode = Normal (ไม่ใช่ AnimatePhysics)
        // → sync กับ Time.deltaTime ของ Update() ที่เรา drive params
        _animator.updateMode = AnimatorUpdateMode.Normal;

        ValidateAnimatorParams(cd.characterModelPrefab.name);
    }

    /// <summary>
    /// ตรวจว่า param ที่ตั้งใน Inspector มีจริงใน Animator Controller
    /// — log warning ถ้าหายไป (กัน silent fail แบบ Run ค้าง / Die ไม่ trigger)
    /// </summary>
    void ValidateAnimatorParams(string modelName)
    {
        if (_animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning($"[PlayerVisual] '{modelName}' Animator ไม่มี Controller");
            return;
        }

        var existing = new System.Collections.Generic.HashSet<string>();
        foreach (var p in _animator.parameters) existing.Add(p.name);

        CheckParam(speedParam,       "speedParam (float Blend Tree)", modelName, existing);
        CheckParam(moveParam,        "moveParam (bool IsMoving)",     modelName, existing);
        CheckParam(deadParam,        "deadParam (bool IsDead)",       modelName, existing);
        CheckParam(attackParam,      "attackParam (trigger Attack)",  modelName, existing);
        CheckParam(motionSpeedParam, "motionSpeedParam (float Multiplier)", modelName, existing);
    }

    void CheckParam(string param, string label, string modelName, System.Collections.Generic.HashSet<string> existing)
    {
        if (string.IsNullOrEmpty(param)) return;   // ปล่อยว่าง = ไม่ใช้
        if (!existing.Contains(param))
            Debug.LogWarning($"[PlayerVisual] Animator ของ '{modelName}' ไม่มี parameter '{param}' " +
                             $"({label}) — animation อาจไม่ทำงาน");
    }
}
