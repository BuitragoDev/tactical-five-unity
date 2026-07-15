using UnityEngine;
using UnityEngine.UIElements;

public class CustomSlider
{
    private VisualElement _container;
    private VisualElement _fill;
    private VisualElement _dragger;
    private VisualElement _tracker;

    public VisualElement Container => _container;

    private float _value;
    public System.Action<float> OnValueChanged;

    public float Value
    {
        get { return _value; }
        set
        {
            _value = Mathf.Clamp01(value);
            UpdateVisuals();
        }
    }

    public CustomSlider(VisualElement container, VisualElement fill, VisualElement dragger)
    {
        _container = container;
        _fill = fill;
        _dragger = dragger;

        if (_container == null) return;

        _container.RegisterCallback<PointerDownEvent>(OnPointerDown);
        _container.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        _container.RegisterCallback<PointerUpEvent>(OnPointerUp);
        _container.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
    }

    public void SetValueWithoutNotify(float value)
    {
        _value = Mathf.Clamp01(value);
        UpdateVisuals();
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (_container == null) return;
        _container.CapturePointer(evt.pointerId);
        UpdateValueFromPointer(evt.position);
        evt.StopPropagation();
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (_container == null) return;
        if (_container.HasPointerCapture(evt.pointerId))
        {
            UpdateValueFromPointer(evt.position);
            evt.StopPropagation();
        }
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (_container == null) return;
        if (_container.HasPointerCapture(evt.pointerId))
        {
            _container.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }
    }

    private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
    {
        // Pointer released
    }

    private void UpdateValueFromPointer(Vector2 pointerPos)
    {
        if (_container == null) return;

        float containerWidth = _container.resolvedStyle.width;
        if (containerWidth <= 0) return;

        float worldLeft = _container.worldBound.x;
        float localX = pointerPos.x - worldLeft;
        float newValue = Mathf.Clamp01(localX / containerWidth);

        if (!Mathf.Approximately(newValue, _value))
        {
            _value = newValue;
            UpdateVisuals();
            OnValueChanged?.Invoke(_value);
        }
    }

    private void UpdateVisuals()
    {
        if (_fill != null)
            _fill.style.width = new StyleLength(new Length(_value * 100f, LengthUnit.Percent));

        if (_dragger != null)
        {
            float draggerWidth = _dragger.resolvedStyle.width > 0 ? _dragger.resolvedStyle.width : 16f;
            float percent = _value * 100f;
            _dragger.style.left = new StyleLength(new Length(percent, LengthUnit.Percent));
        }
    }
}
