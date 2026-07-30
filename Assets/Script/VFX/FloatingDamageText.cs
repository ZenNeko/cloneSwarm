using TMPro;
using UnityEngine;

public class FloatingDamageText : MonoBehaviour
{
    [Header("Components")]
    public TextMeshPro textMesh;

    [Header("Settings")]
    public float duration = 0.8f;
    public float floatSpeed = 1.2f;
    public float normalFontSize = 4f;
    public float critFontSize = 6f;
    public Color normalColor = Color.white;
    public Color critColor = new Color(1f, 0.85f, 0.1f, 1f);

    private float _elapsedTime;
    private Vector3 _startPos;
    private Camera _cam;
    private FloatingDamageTextPool _pool;
    private Color _activeColor;

    void Awake()
    {
        EnsureTextMesh();
    }

    private void EnsureTextMesh()
    {
        if (textMesh == null)
        {
            textMesh = GetComponent<TextMeshPro>();
            if (textMesh == null)
            {
                textMesh = gameObject.AddComponent<TextMeshPro>();
                textMesh.alignment = TextAlignmentOptions.Center;
                textMesh.rectTransform.sizeDelta = new Vector2(3f, 1f);
            }
        }
    }

    public void Init(float damage, bool isCrit, Vector3 basePos, FloatingDamageTextPool pool)
    {
        _pool = pool;
        _elapsedTime = 0f;

        EnsureTextMesh();

        // T5: Horizontal random offset (±0.3)
        Vector3 offset = new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f));
        _startPos = basePos + offset;
        transform.position = _startPos;

        if (textMesh != null)
        {
            textMesh.text = Mathf.RoundToInt(damage).ToString();
            textMesh.fontSize = isCrit ? critFontSize : normalFontSize;
            textMesh.fontStyle = isCrit ? FontStyles.Bold : FontStyles.Normal;
            _activeColor = isCrit ? critColor : normalColor;
            textMesh.color = _activeColor;
        }

        gameObject.SetActive(true);
    }

    void Update()
    {
        _elapsedTime += Time.deltaTime;
        if (_elapsedTime >= duration)
        {
            if (_pool != null)
            {
                _pool.ReturnToPool(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
            return;
        }

        // Float upwards
        transform.position = _startPos + Vector3.up * (floatSpeed * (_elapsedTime / duration));

        // Fade out
        if (textMesh != null)
        {
            float alpha = Mathf.Clamp01(1f - (_elapsedTime / duration));
            Color c = _activeColor;
            c.a = alpha;
            textMesh.color = c;
        }
    }

    void LateUpdate()
    {
        // T1: Cache Camera.main (same pattern as WorldHPBar.cs:102)
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        transform.LookAt(
            transform.position + _cam.transform.rotation * Vector3.forward,
            _cam.transform.rotation * Vector3.up
        );
    }
}
