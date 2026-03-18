using System;
using UnityEngine;
using UnityEngine.AI;

namespace Player
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Stats")]
        [SerializeField] private float _health = 100f;
        
        [SerializeField] private Collider _collider;
        [SerializeField] private Rigidbody _rigidbody;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Enemy")) return;

            PlayerHit();
        }

        private void PlayerHit()
        {
            _health -= 10f;
            
            _rigidbody.AddForce(_rigidbody.transform.forward * -1 * -10, ForceMode.Impulse);
        }
    }
}
