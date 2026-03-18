using UnityEngine;
using UnityEngine.AI;
using UnityUtils;
using Utilities;

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
}