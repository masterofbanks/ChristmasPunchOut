using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private CharacterStats _stats;
    [SerializeField] private Image _guage;
    private float _guageMaxWidth;

    private void Start()
    {
        Debug.Assert(_stats, "Set the '_stats' field of 'HealthBar'", this);
        Debug.Assert(_guage, "Set the '_guage' field of 'HealthBar'", this);

        _guageMaxWidth = _guage.rectTransform.rect.width;
    }

    private void Update()
    {
        float newWidth = Mathf.Lerp(0, _guageMaxWidth, _stats.Health / _stats.MaxHealth);
        _guage.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, newWidth);
    }
}
