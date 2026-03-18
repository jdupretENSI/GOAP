using System.Collections.Generic;
using System.Linq;
using Building_Blocks;
using UnityEngine;
using UnityEngine.AI;
using Utilities;

namespace Agents
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class GoapAgent : MonoBehaviour {
        [Header("Sensors")] 
        [SerializeField] private Sensor _chaseSensor;
        [SerializeField] private Sensor _attackSensor;
        [SerializeField] private Sensor _rangedAttackSensor;

        [Header("Known Locations")] 
        [SerializeField] private Transform _healingStation;
        [SerializeField] private Transform _ammoStation;

        private NavMeshAgent _navMeshAgent;
        private Rigidbody _rb;
    
        [Header("Stats")] 
        public float Health = 100;
        public float Stamina = 100;
        public int Ammo = 10;

        private CountdownTimer _statsTimer;

        private GameObject _target;
        private Vector3 _destination;

        private AgentGoal _lastGoal;
        private AgentGoal _currentGoal;
        private ActionPlan _actionPlan;
        private AgentAction _currentAction;

        private Dictionary<string, AgentBelief> _beliefs;
        public HashSet<AgentAction> Actions;
        private HashSet<AgentGoal> _goals;
    
        //[Inject] private GoapFactory _gFactory;
        private IGoapPlanner _gPlanner;

        private void Awake() {
            _navMeshAgent = GetComponent<NavMeshAgent>();
            _rb = GetComponent<Rigidbody>();
            _rb.freezeRotation = true;
        
            _gPlanner = new GoapPlanner();
        }

        private void Start() {
            //SetupTimers();
            SetupBeliefs();
            SetupActions();
            SetupGoals();
        }

        private void SetupBeliefs() {
            _beliefs = new Dictionary<string, AgentBelief>();
            BeliefFactory factory = new BeliefFactory(this, _beliefs);
        
            factory.AddBelief("Nothing", () => false);
        
            factory.AddBelief("AgentIdle", () => !_navMeshAgent.hasPath);
            factory.AddBelief("AgentMoving", () => _navMeshAgent.hasPath);
            
            factory.AddBelief("LowHealth", () => Health < 50);
            factory.AddBelief("HighHealth", () => Health > 60);
            
            factory.AddLocationBelief("AgentAtHealingStation", 3f, _healingStation);
            
            factory.AddBelief("LowStamina", () => Stamina < 20);
            factory.AddBelief("HighStamina", () => Stamina > 50);
            
            factory.AddBelief("NoAmmo", () => Ammo <= 0);
            factory.AddBelief("FullClip", () => Ammo >= 10);
            factory.AddLocationBelief("AgentAtAmmoStation", 3f, _ammoStation);
        
            factory.AddSensorBelief("PlayerInChaseRange", _chaseSensor);
            factory.AddSensorBelief("PlayerInAttackRange", _attackSensor);
            factory.AddSensorBelief("PlayerInShootingRange", _rangedAttackSensor);
            factory.AddBelief("AttackingPlayer", () => false); // Player can always be attacked, this will never become true
        }

        private void SetupActions() {
            Actions = new HashSet<AgentAction>
            {
                new AgentAction.Builder("Relax")
                    .WithStrategy(new IdleStrategy(2))
                    .AddEffect(_beliefs["Nothing"])
                    .Build(),
                
                new AgentAction.Builder("Wander Around")
                    .WithStrategy(new WanderStrategy(_navMeshAgent, 10))
                    .AddEffect(_beliefs["AgentMoving"])
                    .Build(),
                
                new AgentAction.Builder("ChasePlayer")
                    .WithStrategy(new MoveStrategy(_navMeshAgent, () => _beliefs["PlayerInChaseRange"].Location))
                    .AddPrecondition(_beliefs["PlayerInChaseRange"])
                    .AddEffect(_beliefs["PlayerInAttackRange"])
                    .Build(),
                
                new AgentAction.Builder("AttackPlayer")
                    .WithStrategy(new IdleStrategy(2))
                    .AddPrecondition(_beliefs["PlayerInAttackRange"])
                    .AddEffect(_beliefs["AttackingPlayer"])
                    .Build(),
                
                new AgentAction.Builder("MoveToHealingStation")
                    .WithStrategy(new MoveStrategy(_navMeshAgent, () => _healingStation.position))
                    .AddEffect(_beliefs["AgentAtHealingStation"])
                    .Build(),
                
                new AgentAction.Builder("HealSelf")
                    .WithStrategy(new IdleStrategy(10)) // Wait for the station to heal you
                    .AddPrecondition(_beliefs["AgentAtHealingStation"])
                    .AddEffect(_beliefs["HighHealth"])
                    .Build(),
                
                new AgentAction.Builder("MoveToAmmoStation")
                    .WithStrategy(new MoveStrategy(_navMeshAgent, () => _ammoStation.position))
                    .AddEffect(_beliefs["AgentAtAmmoStation"])
                    .Build(),
                
                new AgentAction.Builder("Reload")
                    .WithStrategy(new IdleStrategy(10)) // Wait for the station to heal you
                    .AddPrecondition(_beliefs["AgentAtAmmoStation"])
                    .AddEffect(_beliefs["FullClip"])
                    .Build(),
            
            };
        }

        private void SetupGoals() {
            _goals = new HashSet<AgentGoal>
            {
                new AgentGoal.Builder("Chill Out")
                    .WithPriority(1)
                    .WithDesiredEffect(_beliefs["Nothing"])
                    .Build(),
                    
                new AgentGoal.Builder("Wander")
                    .WithPriority(2)
                    .WithDesiredEffect(_beliefs["AgentMoving"])
                    .Build(),
                    
                new AgentGoal.Builder("SeekAndDestroy")
                    .WithPriority(3)
                    .WithDesiredEffect(_beliefs["AttackingPlayer"])
                    .Build(),
                
                new AgentGoal.Builder("KeepHealthHigh")
                    .WithPriority(4)
                    .WithDesiredEffect(_beliefs["HighHealth"])
                    .Build(),
                
                new AgentGoal.Builder("KeepAmmoHigh")
                    .WithPriority(3)
                    .WithDesiredEffect(_beliefs["FullClip"])
                    .Build(),
            };
        }

        private void SetupTimers() {
            _statsTimer = new CountdownTimer(2f);
            _statsTimer.OnTimerStop += () => {
                _statsTimer.Start();
            };
            _statsTimer.Start();
        }

        private bool InRangeOf(Vector3 pos, float range) => Vector3.Distance(transform.position, pos) < range;

        private void OnEnable() => _chaseSensor.OnTargetChanged += HandleTargetChanged;
        private void OnDisable() => _chaseSensor.OnTargetChanged -= HandleTargetChanged;

        private void HandleTargetChanged() {
            Debug.Log("Target changed, clearing current action and goal");
            // Force the planner to re-evaluate the plan
            _currentAction = null;
            _currentGoal = null;
        }

        private void Update() {
        
            // Update the plan and current action if there is one
            if (_currentAction == null) {
                Debug.Log("Calculating any potential new plan");
                CalculatePlan();

                if (_actionPlan != null && _actionPlan.Actions.Count > 0) {
                    _navMeshAgent.ResetPath();

                    _currentGoal = _actionPlan.AgentGoal;
                    Debug.Log($"Goal: {_currentGoal.Name} with {_actionPlan.Actions.Count} actions in plan");
                
                    _currentAction = _actionPlan.Actions.Pop();
                    Debug.Log($"Popped action: {_currentAction.Name}");
                
                    // Verify all precondition effects are true
                    if (_currentAction.Preconditions.All(b => b.Evaluate())) {
                        _currentAction.Start();
                    } else {
                        Debug.Log("Preconditions not met, clearing current action and goal");
                        _currentAction = null;
                        _currentGoal = null;
                    }
                }
            }

            // If we have a current action, execute it
            if (_actionPlan != null && _currentAction != null) {
                _currentAction.Update(Time.deltaTime);

                if (_currentAction.Complete) {
                    Debug.Log($"{_currentAction.Name} complete");
                    _currentAction.Stop();
                    _currentAction = null;

                    if (_actionPlan.Actions.Count == 0) {
                        Debug.Log("Plan complete");
                        _lastGoal = _currentGoal;
                        _currentGoal = null;
                    }
                }
            }
        }

        private void CalculatePlan() {
            float priorityLevel = _currentGoal?.Priority ?? 0;
        
            HashSet<AgentGoal> goalsToCheck = _goals;
        
            // If we have a current goal, we only want to check goals with higher priority
            if (_currentGoal != null) {
                Debug.Log("Current goal exists, checking goals with higher priority");
                goalsToCheck = new HashSet<AgentGoal>(_goals.Where(g => g.Priority > priorityLevel));
            }
        
            ActionPlan potentialPlan = _gPlanner.Plan(this, goalsToCheck, _lastGoal);
            if (potentialPlan != null) {
                _actionPlan = potentialPlan;
            }
        }

        public void Heal()
        {
            Health += 10;
        }

        public void Reload()
        {
            Ammo += 1;
        }
    }
}