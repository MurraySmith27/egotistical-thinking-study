


using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class GameTimerSystem : NetworkBehaviour
{
    private static GameTimerSystem _instance;
    
    public static GameTimerSystem Instance
    {
        get { return _instance; }
    }
    
    public NetworkVariable<int> timerSecondsRemaining = new NetworkVariable<int>();

    public NetworkVariable<bool> isGamePaused = new NetworkVariable<bool>();

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
        }
    }
    
    public void OnGameStart()
    {
        if (this.IsServer)
        {
            timerSecondsRemaining.Value = GameRoot.Instance.configData.GameTimerSeconds;
            
            isGamePaused.Value = true;

            StartCoroutine(GameTimerCountdown());

        }
    }

    private IEnumerator GameTimerCountdown()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            yield return new WaitUntil(() => !isGamePaused.Value);
            timerSecondsRemaining.Value--;
        }
    }
}
