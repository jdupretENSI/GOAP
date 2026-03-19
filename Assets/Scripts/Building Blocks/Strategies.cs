using System;
using Agents;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityUtils;
using Utilities;
using Random = UnityEngine.Random;

namespace Building_Blocks
{
    public interface IActionStrategy
    {
        bool CanPerform { get; }
        bool Complete { get; }

        void Start()
        {
            //noop
        }

        void Update(float deltaTime)
        {
            //noop
        }

        void Stop()
        {
            //noop
        }
    }

    public class IdleStrategy : IActionStrategy
    {
        public bool CanPerform => true;
        public bool Complete { get; private set; }

        private readonly CountdownTimer _timer;

        public IdleStrategy(float duration)
        {
            _timer = new CountdownTimer(duration);
            _timer.OnTimerStart += () => Complete = false;
            _timer.OnTimerStop += () => Complete = true;
        }

        public void Start() => _timer.Start();
        public void Update(float deltaTime) => _timer.Tick(deltaTime);
    }
    
    /// <summary>
    /// Move to a random location in the navmesh within the wander radius
    /// </summary>
    public class WanderStrategy : IActionStrategy
    {
        private readonly NavMeshAgent _agent;
        private readonly float _wanderRadius;
        
        public bool CanPerform => !Complete;
        public bool Complete => _agent.remainingDistance <= 2f && !_agent.pathPending;

        public WanderStrategy(NavMeshAgent agent, float wanderRadius)
        {
            _agent = agent;
            _wanderRadius = wanderRadius;
        }

        public void Start()
        {
            for (int i = 0; i < 5; i++)
            {
                Vector3 randomDirection = (Random.insideUnitSphere * _wanderRadius).With(y: 0);

                if (!NavMesh.SamplePosition(_agent.transform.position + randomDirection,
                        out NavMeshHit hit, _wanderRadius, 1))
                    continue;
                
                _agent.SetDestination(hit.position);
                return;
            }
        }
    }

    /// <summary>
    /// Move to a specific location given by a sensor
    /// </summary>
    public class MoveStrategy : IActionStrategy
    {
        private readonly NavMeshAgent _agent;
        private readonly Func<Vector3> _destination;
        
        public bool CanPerform => !Complete;
        public bool Complete => _agent.remainingDistance <= 2f && !_agent.pathPending;
        public MoveStrategy(NavMeshAgent navMeshAgent, Func<Vector3> func)
        {
            _agent = navMeshAgent;
            _destination = func;
        }
        
        public void Start() => _agent.SetDestination(_destination());
        public void Stop() => _agent.ResetPath();

    }

    public class RangedAttackStrategy : IActionStrategy
    {
        private readonly GoapAgent _agent;
        private readonly float _attackDuration;
        private float _timer;
    
        public bool CanPerform => true;
        public bool Complete { get; private set; }

        public RangedAttackStrategy(GoapAgent agent, float attackDuration = 1f)
        {
            _agent = agent;
            _attackDuration = attackDuration;
        }

        public void Start()
        {
            _timer = 0;
            Complete = false;
        
            // Perform the attack
            _agent.RangedAttack();
        }

        public void Update(float deltaTime)
        {
            _timer += deltaTime;
            if (_timer >= _attackDuration)
            {
                Complete = true;
            }
        }
    }
    
    public class TakeAimStrategy : IActionStrategy
    {
        private readonly NavMeshAgent _agent;
        private readonly Func<Vector3> _target;
        private readonly float _aimTolerance;
        private readonly float _rotationSpeed;
    
        public bool CanPerform => true;
        public bool Complete { get; private set; }

        public TakeAimStrategy(NavMeshAgent agent, Func<Vector3> target, 
            float aimTolerance = 5f, float rotationSpeed = 360f)
        {
            _agent = agent;
            _target = target;
            _aimTolerance = aimTolerance;
            _rotationSpeed = rotationSpeed;
        }

        public void Start()
        {
            // Stop movement while aiming
            _agent.isStopped = true;
        }

        public void Update(float deltaTime)
        {
            Vector3 targetPosition = _target();
            Vector3 directionToTarget = (targetPosition - _agent.transform.position).normalized;
        
            // Zero out the y component to prevent tilting
            directionToTarget.y = 0;
        
            if (directionToTarget != Vector3.zero)
            {
                // Smoothly rotate towards target
                Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
                _agent.transform.rotation = Quaternion.RotateTowards(
                    _agent.transform.rotation, 
                    targetRotation, 
                    _rotationSpeed * deltaTime
                );
            }
        
            // Check if we're facing the target within tolerance
            float angleToTarget = Vector3.Angle(_agent.transform.forward, directionToTarget);
            Complete = angleToTarget <= _aimTolerance;
        }

        public void Stop()
        {
            _agent.isStopped = false;
        }
    }
}