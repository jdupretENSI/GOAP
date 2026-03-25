using System.Collections.Generic;
using System.Linq;
using Building_Blocks;
using UnityEngine;
using UnityEngine.AI;
using Utilities;

namespace Agents
{
    [System.Serializable]
    public class SerializableActionPlan
    {
        public string GoalName;
        public int ActionCount;
        public float TotalCost;
        public List<string> ActionNames = new List<string>();
    }

    [RequireComponent(typeof(NavMeshAgent))]
    public class GoapAgent : MonoBehaviour
    {
        [Header("Sensors")]
        [SerializeField] private Sensor _chaseSensor;
        [SerializeField] private Sensor _attackSensor;

        [Header("Known Locations")]
        [SerializeField] private Transform _healingStation;
        [SerializeField] private Transform _ammoStation;

        private NavMeshAgent _navMeshAgent;
        private Rigidbody _rb;

        [Header("Stats")]
        public float Health = 100;
        public int Ammo = 10;
        [SerializeField] private GameObject _projectile;

        [Header("Debug")]
        [SerializeField] private List<string> _activeBeliefs = new();
        [SerializeField] private SerializableActionPlan _debugPlan;
        [SerializeField] private string _currentActionName;

        private readonly RaycastHit[] _raycastHits = new RaycastHit[1];
        private GameObject _target;
        private Vector3 _destination;

        private AgentGoal _lastGoal;
        private AgentGoal _currentGoal;
        private ActionPlan _actionPlan;
        private AgentAction _currentAction;

        private Dictionary<string, AgentBelief> _beliefs;
        public HashSet<AgentAction> Actions;
        private HashSet<AgentGoal> _goals;

        private IGoapPlanner _gPlanner;

        // Flags for location beliefs (set by stations)
        private Dictionary<string, bool> _locationFlags = new Dictionary<string, bool>();

        private bool _needsReplan = true;   // start with a plan

        private void Awake()
        {
            _navMeshAgent = GetComponent<NavMeshAgent>();
            _rb = GetComponent<Rigidbody>();
            _rb.freezeRotation = true;

            _gPlanner = new GoapPlanner();
        }

        private void Start()
        {
            SetupBeliefs();
            SetupActions();
            SetupGoals();
        }

        private void SetupBeliefs()
        {
            _beliefs = new Dictionary<string, AgentBelief>();
            BeliefFactory factory = new BeliefFactory(this, _beliefs);

            factory.AddBelief("Nothing", () => false);
            factory.AddBelief("AgentIdle", () => !_navMeshAgent.hasPath);
            factory.AddBelief("AgentMoving", () => _navMeshAgent.hasPath);

            factory.AddBelief("LowHealth", () => Health < 50);
            factory.AddBelief("HighHealth", () => Health > 60);

            // Location beliefs – now rely on flags set by stations
            factory.AddLocationBelief("AgentAtHealingStation", 3f, _healingStation);
            factory.AddLocationBelief("AgentAtAmmoStation", 3f, _ammoStation);

            factory.AddBelief("NoAmmo", () => Ammo <= 0);
            factory.AddBelief("HasAmmo", () => Ammo >= 1);

            factory.AddSensorBelief("PlayerInChaseRange", _chaseSensor);
            factory.AddSensorBelief("PlayerInAttackRange", _attackSensor);
            factory.AddBelief("AttackingPlayer", () => false);
            factory.AddBelief("AimedAtPlayer", RaycastHitPlayer);

            // Subscribe to all beliefs
            foreach (var belief in _beliefs.Values)
            {
                belief.OnValueChanged += OnBeliefChanged;
            }
        }

        private void SetupActions()
        {
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
                    .AddPrecondition(_beliefs["HighHealth"])
                    .WithCost(2)
                    .AddEffect(_beliefs["PlayerInAttackRange"])
                    .Build(),

                new AgentAction.Builder("MeleeAttackPlayer")
                    .WithStrategy(new IdleStrategy(2))
                    .AddPrecondition(_beliefs["PlayerInAttackRange"])
                    .AddPrecondition(_beliefs["HighHealth"])
                    .AddEffect(_beliefs["AttackingPlayer"])
                    .Build(),

                new AgentAction.Builder("TakeAim")
                    .WithStrategy(new TakeAimStrategy(_navMeshAgent, () => _beliefs["PlayerInChaseRange"].Location))
                    .AddPrecondition(_beliefs["PlayerInChaseRange"])
                    .AddEffect(_beliefs["AimedAtPlayer"])
                    .Build(),

                new AgentAction.Builder("RangedAttackPlayer")
                    .WithStrategy(new RangedAttackStrategy(this))
                    .AddPrecondition(_beliefs["AimedAtPlayer"])
                    .AddPrecondition(_beliefs["HasAmmo"])
                    .AddEffect(_beliefs["AttackingPlayer"])
                    .Build(),

                new AgentAction.Builder("MoveToHealingStation")
                    .WithStrategy(new MoveStrategy(_navMeshAgent, () => _healingStation.position))
                    .AddEffect(_beliefs["AgentAtHealingStation"])
                    .Build(),

                new AgentAction.Builder("HealSelf")
                    .WithStrategy(new IdleStrategy(10))
                    .AddPrecondition(_beliefs["AgentAtHealingStation"])
                    .AddEffect(_beliefs["HighHealth"])
                    .Build(),

                new AgentAction.Builder("MoveToAmmoStation")
                    .WithStrategy(new MoveStrategy(_navMeshAgent, () => _ammoStation.position))
                    .AddEffect(_beliefs["AgentAtAmmoStation"])
                    .Build(),

                new AgentAction.Builder("Reload")
                    .WithStrategy(new IdleStrategy(10))
                    .AddPrecondition(_beliefs["AgentAtAmmoStation"])
                    .AddEffect(_beliefs["HasAmmo"])
                    .Build(),
            };
        }

        private void SetupGoals()
        {
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

                new AgentGoal.Builder("DestroyTarget")
                    .WithPriority(4)
                    .WithDesiredEffect(_beliefs["AttackingPlayer"])
                    .Build(),

                new AgentGoal.Builder("KeepHealthHigh")
                    .WithPriority(5)
                    .WithDesiredEffect(_beliefs["HighHealth"])
                    .Build(),

                new AgentGoal.Builder("KeepAmmoHigh")
                    .WithPriority(3)
                    .WithDesiredEffect(_beliefs["HasAmmo"])
                    .Build(),
            };
        }

        private void OnEnable() => _chaseSensor.OnTargetChanged += HandleTargetChanged;
        private void OnDisable() => _chaseSensor.OnTargetChanged -= HandleTargetChanged;

        private void HandleTargetChanged()
        {
            // Refresh sensor beliefs – they may have changed
            if (_beliefs.TryGetValue("PlayerInChaseRange", out AgentBelief chaseBelief))
                chaseBelief.Refresh();
            if (_beliefs.TryGetValue("PlayerInAttackRange", out AgentBelief attackBelief))
                attackBelief.Refresh();

            _needsReplan = true;
        }

        private void OnBeliefChanged(AgentBelief belief)
        {
            _needsReplan = true;
        }

        private void Update()
        {
            _currentActionName = _currentAction?.Name ?? "None";

            // Execute current action if there is one
            if (_currentAction != null)
            {
                _currentAction.Update(Time.deltaTime);

                if (_currentAction.Complete)
                {
                    _currentAction.Stop();
                    _currentAction = null;

                    if (_actionPlan.Actions.Count == 0)
                    {
                        // Plan finished – need a new one
                        _needsReplan = true;
                        _currentGoal = null;
                        _lastGoal = null;
                    }
                    else
                    {
                        // Get next action from the plan
                        _currentAction = _actionPlan.Actions.Pop();
                        UpdateDebugPlanInfo();

                        if (_currentAction.Preconditions.All(b => b.Evaluate()))
                        {
                            _currentAction.Start();
                        }
                        else
                        {
                            // Preconditions not met – force replan
                            _needsReplan = true;
                            _currentAction = null;
                        }
                    }
                }
            }

            // If we have no current action and a replan is needed, calculate a new plan
            if (_currentAction == null && _needsReplan)
            {
                CalculatePlan();
                _needsReplan = false;

                if (_actionPlan != null && _actionPlan.Actions.Count > 0)
                {
                    _navMeshAgent.ResetPath();
                    _currentGoal = _actionPlan.AgentGoal;
                    _currentAction = _actionPlan.Actions.Pop();

                    if (_currentAction.Preconditions.All(b => b.Evaluate()))
                    {
                        _currentAction.Start();
                    }
                    else
                    {
                        // Should not happen, but fallback
                        _currentAction = null;
                        _needsReplan = true;
                    }
                }
            }
        }

        private void CalculatePlan()
        {
            float priorityLevel = _currentGoal?.Priority ?? 0;

            HashSet<AgentGoal> goalsToCheck = _goals;

            if (_currentGoal != null)
            {
                goalsToCheck = new HashSet<AgentGoal>(_goals.Where(g => g.Priority > priorityLevel));
            }

            ActionPlan potentialPlan = _gPlanner.Plan(this, goalsToCheck, _lastGoal);
            if (potentialPlan != null)
            {
                _actionPlan = potentialPlan;
                UpdateDebugPlanInfo();
            }
        }

        // Public methods to modify state with belief refresh
        public void ModifyHealth(float delta)
        {
            Health = Mathf.Clamp(Health + delta, 0, 100);
            _beliefs["LowHealth"].Refresh();
            _beliefs["HighHealth"].Refresh();
        }

        public void ModifyAmmo(int delta)
        {
            Ammo = Mathf.Clamp(Ammo + delta, 0, 10);
            _beliefs["NoAmmo"].Refresh();
            _beliefs["HasAmmo"].Refresh();
        }

        // Convenience methods used by stations
        public void Heal() => ModifyHealth(10);
        public void Reload() => ModifyAmmo(1);

        public void RangedAttack()
        {
            if (Ammo <= 0) return;

            GameObject projectile = Instantiate(_projectile);
            projectile.transform.position = transform.position;
            projectile.transform.forward = transform.forward;
            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            rb.AddForce(transform.forward * 10f, ForceMode.Impulse);

            ModifyAmmo(-1);
        }

        // Location flag management
        public void SetLocationFlag(string key, bool value)
        {
            if (_locationFlags.TryGetValue(key, out bool old) && old == value) return;
            _locationFlags[key] = value;
            if (_beliefs.TryGetValue(key, out AgentBelief belief))
            {
                belief.Refresh();
            }
        }

        public bool GetLocationFlag(string key)
        {
            return _locationFlags.TryGetValue(key, out bool value) && value;
        }

        private bool RaycastHitPlayer()
        {
            int hits = Physics.RaycastNonAlloc(
                transform.position,
                transform.forward,
                _raycastHits,
                _chaseSensor.DetectionRadius,
                LayerMask.GetMask("Player")
            );
            return hits > 0;
        }

        #region Debug Tools
        private void UpdateDebugPlanInfo()
        {
            if (_actionPlan == null)
            {
                _debugPlan = null;
                return;
            }

            _debugPlan = new SerializableActionPlan
            {
                GoalName = _actionPlan.AgentGoal?.Name ?? "None",
                ActionCount = _actionPlan.Actions?.Count ?? 0,
                TotalCost = _actionPlan.TotalCost,
                ActionNames = _actionPlan.Actions?.Select(a => a.Name).ToList() ?? new List<string>()
            };
        }
        #endregion
    }
}