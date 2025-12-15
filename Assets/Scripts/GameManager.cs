using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("StartingValues")]
    public GameObject PlayerPrefab;
    public CharacterStats CurrentGlobalPlayerStats;
    public Transform PlayerSpawnPos;
    public GameObject Enemy;
    public GameObject playerHealthBar;
    public string nextSceneName;
    private GameObject _currentPlayer;

    private bool lockIt;

    public bool playerIsInitiallyInCombatMode = true;

    private void Awake()
    {
        CurrentGlobalPlayerStats = GameObject.FindWithTag("ahh").GetComponent<CharacterStats>();
        _currentPlayer = Instantiate(PlayerPrefab, PlayerSpawnPos.position, Quaternion.identity);
        _currentPlayer.GetComponent<CharacterStats>().ChangeStats(CurrentGlobalPlayerStats);
        lockIt = true;
        Enemy.GetComponent<BaseEnemy>().Player = _currentPlayer.transform;
        playerHealthBar.GetComponent<HealthBar>()._stats = _currentPlayer.GetComponent<CharacterStats>();
        _currentPlayer.GetComponent<PlayerController>().canFreeMove = !playerIsInitiallyInCombatMode;
    }

    private void FixedUpdate()
    {
        CharacterStats currentStats = _currentPlayer.GetComponent<CharacterStats>();
        CharacterStats enemyStats = Enemy.GetComponent<CharacterStats>();
        if (!currentStats.IsAlive() && lockIt)
        {
            Debug.Log("Hello?");

            LoseCondition();
        }

        else if(!enemyStats.IsAlive() && lockIt)
        {
            WinCondition();
        }
    }

    public void LoseCondition()
    {
        lockIt = false;
        StartCoroutine(LoseRoutine());
    }

    IEnumerator LoseRoutine()
    {
        yield return new WaitForSeconds(1.0f);
        CurrentGlobalPlayerStats.ChangeStats(2.0f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void WinCondition()
    {
        lockIt = false;
        StartCoroutine(WinRoutine());
    }

    IEnumerator WinRoutine()
    {
        yield return new WaitForSeconds(1.0f);
        SceneManager.LoadScene(nextSceneName);
    }


}
