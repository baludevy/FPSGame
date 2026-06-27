using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(RectTransform))]
public class FrametimeGraph : MonoBehaviour
{
    [Header("Graph Size")]
    public float graphWidth = 0f;
    public float graphHeight = 0f;

    [Header("Data")]
    public int maxSamples = 128;
    public float maxDisplayValue = 50f;

    [Header("Colors")]
    public Color colorLine          = new Color(0.0f, 1.0f, 1.0f, 1.0f);
    public Color colorOutline       = new Color(1.0f, 1.0f, 1.0f, 1.0f);
    [Range(1f, 5f)]
    public float outlineThickness   = 1f;

    [Header("Line")]
    [Range(1f, 8f)]
    public float lineWidth = 2f;
    public Material lineMaterial;

    [Header("Optional Labels")]
    public TMP_Text labelTitle;
    public TMP_Text labelCurrentValue;
    public TMP_Text labelAvgValue;
    public TMP_Text labelMinValue;
    public TMP_Text labelMaxValue;

    [Header("Display Options")]
    public string titleText = "Frametime (ms)";

    [Header("Auto Scale (Y axis)")]
    public bool  autoScale = true;
    public float autoScaleHeadroom = 1.25f;
    public float autoScaleMin = 8f;
    public float autoScaleSpeed = 6f;

    private RectTransform _rt;
    private Canvas        _canvas;

    private readonly List<RawImage> _lineSegments = new();
    private readonly RawImage[]     _outlines     = new RawImage[4];

    private float _displayMax;

    private readonly List<float> _samples = new();
    private float _min, _max, _avg, _current;
    private bool _pendingRedraw;

    private void Awake()
    {
        _rt     = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        _displayMax = maxDisplayValue;

        BuildOutline();
        if (labelTitle != null) labelTitle.text = titleText;
    }

    private void OnValidate()
    {
        _pendingRedraw = true;
    }

    private void Update()
    {
        if (_pendingRedraw)
        {
            _pendingRedraw = false;
            RedrawAll();
        }
    }

    public void AddSample(float valueMs)
    {
        _samples.Add(valueMs);
        while (_samples.Count > maxSamples) _samples.RemoveAt(0);

        UpdateStats();
        UpdateAutoScale();
        RedrawAll();
        UpdateLabels();
    }

    private void UpdateAutoScale()
    {
        if (!autoScale)
        {
            _displayMax = maxDisplayValue;
            return;
        }

        float target = Mathf.Max(autoScaleMin, _max * autoScaleHeadroom);
        float dt = Time.unscaledDeltaTime;
        float k  = dt > 0f ? 1f - Mathf.Exp(-autoScaleSpeed * dt) : 1f;
        _displayMax = Mathf.Lerp(_displayMax, target, k);
    }

    public void Clear()
    {
        _samples.Clear();
        RedrawAll();
    }

    public void SetSize(float width, float height)
    {
        graphWidth  = width;
        graphHeight = height;
        _rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        _rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   height);
        RedrawAll();
    }

    private Vector2 GraphSize()
    {
        float w = graphWidth  > 0 ? graphWidth  : _rt.rect.width;
        float h = graphHeight > 0 ? graphHeight : _rt.rect.height;
        return new Vector2(w, h);
    }

    private void BuildOutline()
    {
        for (int i = 0; i < 4; i++)
        {
            _outlines[i] = CreateRawImage($"Outline_{i}", colorOutline);
        }
    }

    private void RedrawAll()
    {
        if (_rt == null) return;
        Vector2 size = GraphSize();
        float w = size.x, h = size.y;

        int sampleCount = _samples.Count;

        DrawHorizontalBar(_outlines[0], 0f, h, w, outlineThickness, colorOutline);
        DrawHorizontalBar(_outlines[1], 0f, 0f, w, outlineThickness, colorOutline);
        
        var leftRt = _outlines[2].rectTransform;
        leftRt.anchorMin = Vector2.zero; leftRt.anchorMax = Vector2.zero; leftRt.pivot = new Vector2(0.5f, 0f);
        leftRt.anchoredPosition = new Vector2(0f, 0f); leftRt.sizeDelta = new Vector2(outlineThickness, h); leftRt.localRotation = Quaternion.identity;
        _outlines[2].color = colorOutline; _outlines[2].gameObject.SetActive(true);

        var rightRt = _outlines[3].rectTransform;
        rightRt.anchorMin = Vector2.zero; rightRt.anchorMax = Vector2.zero; rightRt.pivot = new Vector2(0.5f, 0f);
        rightRt.anchoredPosition = new Vector2(w, 0f); rightRt.sizeDelta = new Vector2(outlineThickness, h); rightRt.localRotation = Quaternion.identity;
        _outlines[3].color = colorOutline; _outlines[3].gameObject.SetActive(true);

        int used = 0;

        for (int i = 0; i < sampleCount - 1; i++)
        {
            float xa = SampleToX(i,     sampleCount, w);
            float xb = SampleToX(i + 1, sampleCount, w);
            
            float ya = ValueToY(_samples[i], h);
            float yb = ValueToY(_samples[i + 1], h);

            RawImage seg = GetPooledSegment(used++);
            DrawSegment(seg, xa, ya, xb, yb, lineWidth, colorLine);
        }

        for (int i = used; i < _lineSegments.Count; i++)
            _lineSegments[i].gameObject.SetActive(false);
    }

    private RawImage GetPooledSegment(int index)
    {
        while (_lineSegments.Count <= index)
            _lineSegments.Add(CreateRawImage($"Seg_{_lineSegments.Count}", Color.white));
        _lineSegments[index].gameObject.SetActive(true);
        return _lineSegments[index];
    }

    private float SampleToX(int index, int total, float width)
        => total <= 1 ? 0 : (index / (float)(total - 1)) * width;

    private float CurrentMax => Mathf.Max(0.0001f, autoScale ? _displayMax : maxDisplayValue);

    private float ValueToY(float value, float height)
        => Mathf.Clamp01(value / CurrentMax) * height;

    private void DrawSegment(RawImage img, float x0, float y0, float x1, float y1, float thickness, Color color)
    {
        img.gameObject.SetActive(true);
        img.color = color;

        float dx  = x1 - x0;
        float dy  = y1 - y0;
        float len = Mathf.Sqrt(dx * dx + dy * dy);
        float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;

        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot     = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x0, y0);
        rt.sizeDelta        = new Vector2(len, thickness);
        rt.localRotation    = Quaternion.Euler(0, 0, angle);
    }

    private void DrawHorizontalBar(RawImage img, float x, float y, float width, float thickness, Color color)
    {
        img.gameObject.SetActive(true);
        img.color = color;

        var rt = img.rectTransform;
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.zero;
        rt.pivot            = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta        = new Vector2(width, thickness);
        rt.localRotation    = Quaternion.identity;
    }

    private void EnsureCount(List<RawImage> list, int count, string prefix)
    {
        while (list.Count < count)
            list.Add(CreateRawImage($"{prefix}_{list.Count}", Color.white));
        for (int i = 0; i < list.Count; i++)
            list[i].gameObject.SetActive(i < count);
    }

    private static void HideAll(List<RawImage> list)
    {
        foreach (var img in list) img.gameObject.SetActive(false);
    }

    private RawImage CreateRawImage(string goName, Color color)
    {
        var go  = new GameObject(goName, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(transform, false);

        var img = go.GetComponent<RawImage>();
        img.color = color;
        if (lineMaterial != null) img.material = lineMaterial;

        return img;
    }

    private void UpdateStats()
    {
        int count = _samples.Count;
        if (count == 0) return;

        _min = float.MaxValue;
        _max = float.MinValue;
        float sum = 0;
        for (int i = 0; i < count; i++)
        {
            float s = _samples[i];
            if (s < _min) _min = s;
            if (s > _max) _max = s;
            sum += s;
        }
        _avg = sum / count;
        _current = _samples[count - 1];
    }

    private void UpdateLabels()
    {
        if (labelCurrentValue != null)
        {
            labelCurrentValue.text  = $"{_current:F2} ms";
            labelCurrentValue.color = colorLine;
        }
        if (labelAvgValue != null) labelAvgValue.text = $"avg {_avg:F2} ms";
        if (labelMinValue != null) labelMinValue.text = $"min {_min:F2} ms";
        if (labelMaxValue != null) labelMaxValue.text = $"max {_max:F2} ms";
    }
}