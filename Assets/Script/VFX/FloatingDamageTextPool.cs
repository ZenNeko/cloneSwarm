using System.Collections.Generic;
using UnityEngine;

public class FloatingDamageTextPool : MonoBehaviour
{
    private static FloatingDamageTextPool _instance;

    public static FloatingDamageTextPool Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("FloatingDamageTextPool");
                _instance = go.AddComponent<FloatingDamageTextPool>();
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Pool Setup")]
    public GameObject textPrefab;
    public int initialCapacity = 200;

    private readonly Queue<FloatingDamageText> _pool = new Queue<FloatingDamageText>();
    private bool _hasWarnedGrow = false;
    private int _totalCreated = 0;

    void Awake()
    {
        // ⚠️ ต้องใช้ _instance ตรงๆ ห้ามใช้ property Instance ในนี้
        // getter ของ Instance สร้าง GameObject + AddComponent ซึ่ง Unity เรียก Awake()
        // ทันทีแบบ synchronous → Awake เรียก getter อีก → _instance ยังเป็น null อยู่
        // (บรรทัดที่ assign ยังไม่ทำงานเสร็จ) → สร้างใหม่ไม่รู้จบ = stack overflow
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        PreallocatePool();
    }

    void OnDestroy()
    {
        // ใช้ _instance ตรงๆ ด้วยเหตุผลเดียวกับใน Awake — getter จะสร้าง GameObject ใหม่
        // ระหว่างที่ฉากกำลังถูกทำลาย ซึ่ง Unity ไม่ยอมและจะโยน error
        if (_instance == this) _instance = null;
    }

    private void PreallocatePool()
    {
        for (int i = 0; i < initialCapacity; i++)
        {
            var item = CreateNewItem();
            item.gameObject.SetActive(false);
            _pool.Enqueue(item);
        }
    }

    private FloatingDamageText CreateNewItem()
    {
        _totalCreated++;
        GameObject go;
        if (textPrefab != null)
        {
            go = Instantiate(textPrefab, transform);
        }
        else
        {
            go = new GameObject($"FloatingDamageText_{_totalCreated}");
            go.transform.SetParent(transform);
        }

        var fdt = go.GetComponent<FloatingDamageText>();
        if (fdt == null)
        {
            fdt = go.AddComponent<FloatingDamageText>();
        }
        return fdt;
    }

    public FloatingDamageText Play(Vector3 position, float damage, bool isCrit)
    {
        FloatingDamageText item;
        if (_pool.Count > 0)
        {
            item = _pool.Dequeue();
        }
        else
        {
            if (!_hasWarnedGrow)
            {
                Debug.LogWarning($"[FloatingDamageTextPool] Pool empty, growing pool beyond initial capacity (count: {_totalCreated})");
                _hasWarnedGrow = true;
            }
            item = CreateNewItem();
        }

        item.Init(damage, isCrit, position, this);
        return item;
    }

    public void ReturnToPool(FloatingDamageText item)
    {
        if (item == null) return;
        item.gameObject.SetActive(false);
        _pool.Enqueue(item);
    }
}
