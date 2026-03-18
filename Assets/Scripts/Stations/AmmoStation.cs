using Agents;
using UnityEngine;
using Utilities;

[RequireComponent(typeof(Collider))]
public class AmmoStation : MonoBehaviour
{
    private CountdownTimer _countdownTimer;
    private GoapAgent _goapAgent;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SetupTimers();
    }

    // Update is called once per frame
    void Update()
    {
        if (!_goapAgent) return;
        
        _countdownTimer.Tick(Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy")) return;
        
        _goapAgent = other.gameObject.GetComponent<GoapAgent>();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Enemy")) return;
        
        _goapAgent = null;
    }

    void SetupTimers() {
        _countdownTimer = new CountdownTimer(2f);
        _countdownTimer.OnTimerStop += () => {
            _goapAgent.Reload();
            _countdownTimer.Start();
        };
        _countdownTimer.Start();
    }
}
