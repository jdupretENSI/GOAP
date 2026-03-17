using System;
using UnityEngine;
using Utilities;

namespace Building_Blocks
{
    /// <summary>
    /// Detects things as the game runs. Like the player coming into view, into a hitbox or if ammo has spawned.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class Sensor : MonoBehaviour
    {
        [SerializeField] private float _detectionRadius;
        // Timer to reevaluate what is inside the sensor
        [SerializeField] private float _timerInterval;
        
        SphereCollider _sphereCollider;
        
        public event Action OnTargetChanged = delegate { };

        public Vector3 TargetPostion => _target ? _target.transform.position : Vector3.zero;
        public bool IsTragetInRange => TargetPostion != Vector3.zero;
        
        private GameObject _target;
        private Vector3 _lastKnownTragetPosition;
        private CountdownTimer _timer;

        private void Awake()
        {
            _sphereCollider = GetComponent<SphereCollider>();
            _sphereCollider.isTrigger = true;
            _sphereCollider.radius = _detectionRadius;
        }

        private void Start()
        {
            _timer = new CountdownTimer(_timerInterval);
            _timer.OnTimerStop += () =>
            {
                UpdateTargetPosition(_target.OrNull());
                _timer.Start();
            };
            _timer.Start();
        }

        private void Update()
        {
            _timer.Tick(Time.deltaTime);
        }

        
        private void UpdateTargetPosition(GameObject target = null)
        {
            _target =  target;

            if (!IsTragetInRange ||
                (_lastKnownTragetPosition == TargetPostion && _lastKnownTragetPosition == Vector3.zero)) return;
            
            _lastKnownTragetPosition = TargetPostion;
            OnTargetChanged?.Invoke();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            
            UpdateTargetPosition(other.gameObject);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            
            UpdateTargetPosition();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = IsTragetInRange ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, _detectionRadius);
        }
    }
}