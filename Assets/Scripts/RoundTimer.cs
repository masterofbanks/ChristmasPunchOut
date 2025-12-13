using UnityEngine;

public class RoundTimer : MonoBehaviour
{
    [SerializeField] private float _initialTimeInSeconds = 120;
    [SerializeField] private float _currentTimeInSeconds;
    public float CurrentTimeInSeconds
    {
        get
        {
            return Mathf.Max(0.0f, _currentTimeInSeconds);
        }
    }

    void Start()
    {
        _currentTimeInSeconds = _initialTimeInSeconds;
    }

    private void Update()
    {
        _currentTimeInSeconds -= Time.deltaTime;
    }
}
