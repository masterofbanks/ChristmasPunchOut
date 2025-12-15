using UnityEngine;

public class MainMenuAudio : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        AudioManager.Instance.PlayMusic("BGMusic");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
