using UnityEngine;

public class SavedCharacterStats : CharacterStats
{
    private static SavedCharacterStats playerInstance;
    void Awake()
    {
        DontDestroyOnLoad(this);

        if (playerInstance == null)
        {
            playerInstance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
