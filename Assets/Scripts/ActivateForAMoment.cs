using UnityEngine;

public class ActivateForAMoment : MonoBehaviour
{
    [SerializeField] private int framesBeforeItDisables;
    private int timer;

    private void OnEnable()
    {
        timer = framesBeforeItDisables;
    }

    private void FixedUpdate()
    {
        if(timer > 0)
        {
            --timer;
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

}
