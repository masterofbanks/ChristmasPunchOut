using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            DontDestroyOnLoad(gameObject);
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Loads a scene given the index. If teh index is out of range, loads the main menu
    /// </summary>
    /// <param name="index"></param>
    public void LoadLevel(int index)
    {
        int sceneToLoad = index;
        if(sceneToLoad < 0 || sceneToLoad >= SceneManager.sceneCountInBuildSettings)
        {
            sceneToLoad = 0;
        }

        SceneManager.LoadScene(sceneToLoad);
    }
}
