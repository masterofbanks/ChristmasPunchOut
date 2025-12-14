using TMPro;
using UnityEngine;

public class RoundTimerVisual : MonoBehaviour
{
    [SerializeField] private RoundTimer _timer;
    [SerializeField] private TextMeshProUGUI _text;

    void Start()
    {
        Debug.Assert(_timer, "Set the '_timer' field of 'RoundTimerVisual'", this);
    }

    void Update()
    {
        int minutesPlace = Mathf.FloorToInt(_timer.CurrentTimeInSeconds / 60);
        int secondsPlace = Mathf.FloorToInt(_timer.CurrentTimeInSeconds % 60);
        _text.text = $"{minutesPlace:00}:{secondsPlace:00}";
    }
}
