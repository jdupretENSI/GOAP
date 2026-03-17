using System.Collections.Generic;
using System.Linq;
using Building_Blocks;
using UnityEngine;
using UnityEngine.AI;
using Utilities;

[RequireComponent(typeof(NavMeshAgent))]
public class GoapAgent : MonoBehaviour {
    [Header("Sensors")] 
    [SerializeField] private Sensor _chaseSensor;
    [SerializeField] private Sensor _attackSensor;
    
    [Header("Known Locations")] 
    [SerializeField]
    private Transform _restingPosition;
    [SerializeField] private Transform _foodShack;
    [SerializeField] private Transform _doorOnePosition;
    [SerializeField] private Transform _doorTwoPosition;

    private NavMeshAgent _navMeshAgent;
    private Rigidbody _rb;
    
    [Header("Stats")] 
    public float Health = 100;
    public float Stamina = 100;

    private CountdownTimer _statsTimer;

    private GameObject _target;
    private Vector3 _destination;

    private AgentGoal _lastGoal;
    public AgentGoal CurrentGoal;
    public ActionPlan ActionPlan;
    public AgentAction CurrentAction;
    
    public Dictionary<string, AgentBelief> Beliefs;
    public HashSet<AgentAction> Actions;
    public HashSet<AgentGoal> Goals;
    
    [Inject] private GoapFactory _gFactory;
    private IGoapPlanner _gPlanner;

    private void Awake() {
        _navMeshAgent = GetComponent<NavMeshAgent>();
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;
        
        _gPlanner = _gFactory.CreatePlanner();
    }

    private void Start() {
        SetupTimers();
        SetupBeliefs();
        SetupActions();
        SetupGoals();
    }

    private void SetupBeliefs() {
        Beliefs = new Dictionary<string, AgentBelief>();
        BeliefFactory factory = new BeliefFactory(this, Beliefs);
        
        factory.AddBelief("Nothing", () => false);
        
        factory.AddBelief("AgentIdle", () => !_navMeshAgent.hasPath);
        factory.AddBelief("AgentMoving", () => _navMeshAgent.hasPath);
        factory.AddBelief("AgentHealthLow", () => Health < 30);
        factory.AddBelief("AgentIsHealthy", () => Health >= 50);
        factory.AddBelief("AgentStaminaLow", () => Stamina < 10);
        factory.AddBelief("AgentIsRested", () => Stamina >= 50);
        
        factory.AddLocationBelief("AgentAtDoorOne", 3f, _doorOnePosition);
        factory.AddLocationBelief("AgentAtDoorTwo", 3f, _doorTwoPosition);
        factory.AddLocationBelief("AgentAtRestingPosition", 3f, _restingPosition);
        factory.AddLocationBelief("AgentAtFoodShack", 3f, _foodShack);
        
        factory.AddSensorBelief("PlayerInChaseRange", _chaseSensor);
        factory.AddSensorBelief("PlayerInAttackRange", _attackSensor);
        factory.AddBelief("AttackingPlayer", () => false); // Player can always be attacked, this will never become true
    }

    private void SetupActions() {
        Actions = new HashSet<AgentAction>();
        
        Actions.Add(new AgentAction.Builder("Relax")
            .WithStrategy(new IdleStrategy(5))
            .AddEffect(Beliefs["Nothing"])
            .Build());
        
        Actions.Add(new AgentAction.Builder("Wander Around")
            .WithStrategy(new WanderStrategy(_navMeshAgent, 10))
            .AddEffect(Beliefs["AgentMoving"])
            .Build());

        Actions.Add(new AgentAction.Builder("MoveToEatingPosition")
            .WithStrategy(new MoveStrategy(_navMeshAgent, () => _foodShack.position))
            .AddEffect(Beliefs["AgentAtFoodShack"])
            .Build());
        
        Actions.Add(new AgentAction.Builder("Eat")
            .WithStrategy(new IdleStrategy(5))  // Later replace with a Command
            .AddPrecondition(Beliefs["AgentAtFoodShack"])
            .AddEffect(Beliefs["AgentIsHealthy"])
            .Build());

        Actions.Add(new AgentAction.Builder("MoveToDoorOne")
            .WithStrategy(new MoveStrategy(_navMeshAgent, () => _doorOnePosition.position))
            .AddEffect(Beliefs["AgentAtDoorOne"])
            .Build());

        Actions.Add(new AgentAction.Builder("MoveToDoorTwo")
            .WithStrategy(new MoveStrategy(_navMeshAgent, () => _doorTwoPosition.position))
            .AddEffect(Beliefs["AgentAtDoorTwo"])
            .Build());

        Actions.Add(new AgentAction.Builder("MoveFromDoorOneToRestArea")
            .WithCost(2)
            .WithStrategy(new MoveStrategy(_navMeshAgent, () => _restingPosition.position))
            .AddPrecondition(Beliefs["AgentAtDoorOne"])
            .AddEffect(Beliefs["AgentAtRestingPosition"])
            .Build());

        Actions.Add(new AgentAction.Builder("MoveFromDoorTwoRestArea")
            .WithStrategy(new MoveStrategy(_navMeshAgent, () => _restingPosition.position))
            .AddPrecondition(Beliefs["AgentAtDoorTwo"])
            .AddEffect(Beliefs["AgentAtRestingPosition"])
            .Build());

        Actions.Add(new AgentAction.Builder("Rest")
            .WithStrategy(new IdleStrategy(5))
            .AddPrecondition(Beliefs["AgentAtRestingPosition"])
            .AddEffect(Beliefs["AgentIsRested"])
            .Build());

        Actions.Add(new AgentAction.Builder("ChasePlayer")
            .WithStrategy(new MoveStrategy(_navMeshAgent, () => Beliefs["PlayerInChaseRange"].Location))
            .AddPrecondition(Beliefs["PlayerInChaseRange"])
            .AddEffect(Beliefs["PlayerInAttackRange"])
            .Build());

        Actions.Add(new AgentAction.Builder("AttackPlayer")
            .AddPrecondition(Beliefs["PlayerInAttackRange"])
            .AddEffect(Beliefs["AttackingPlayer"])
            .Build());
    }

    private void SetupGoals() {
        Goals = new HashSet<AgentGoal>();
        
        Goals.Add(new AgentGoal.Builder("Chill Out")
            .WithPriority(1)
            .WithDesiredEffect(Beliefs["Nothing"])
            .Build());
        
        Goals.Add(new AgentGoal.Builder("Wander")
            .WithPriority(1)
            .WithDesiredEffect(Beliefs["AgentMoving"])
            .Build());
        
        Goals.Add(new AgentGoal.Builder("KeepHealthUp")
            .WithPriority(2)
            .WithDesiredEffect(Beliefs["AgentIsHealthy"])
            .Build());

        Goals.Add(new AgentGoal.Builder("KeepStaminaUp")
            .WithPriority(2)
            .WithDesiredEffect(Beliefs["AgentIsRested"])
            .Build());
        
        Goals.Add(new AgentGoal.Builder("SeekAndDestroy")
            .WithPriority(3)
            .WithDesiredEffect(Beliefs["AttackingPlayer"])
            .Build());
    }

    private void SetupTimers() {
        _statsTimer = new CountdownTimer(2f);
        _statsTimer.OnTimerStop += () => {
            UpdateStats();
            _statsTimer.Start();
        };
        _statsTimer.Start();
    }

    // TODO move to stats system
    private void UpdateStats() {
        Stamina += InRangeOf(_restingPosition.position, 3f) ? 20 : -10;
        Health += InRangeOf(_foodShack.position, 3f) ? 20 : -5;
        Stamina = Mathf.Clamp(Stamina, 0, 100);
        Health = Mathf.Clamp(Health, 0, 100);
    }

    private bool InRangeOf(Vector3 pos, float range) => Vector3.Distance(transform.position, pos) < range;

    private void OnEnable() => _chaseSensor.OnTargetChanged += HandleTargetChanged;
    private void OnDisable() => _chaseSensor.OnTargetChanged -= HandleTargetChanged;

    private void HandleTargetChanged() {
        Debug.Log("Target changed, clearing current action and goal");
        // Force the planner to re-evaluate the plan
        CurrentAction = null;
        CurrentGoal = null;
    }

    private void Update() {
        _statsTimer.Tick(Time.deltaTime);
        
        // Update the plan and current action if there is one
        if (CurrentAction == null) {
            Debug.Log("Calculating any potential new plan");
            CalculatePlan();

            if (ActionPlan != null && ActionPlan.Actions.Count > 0) {
                _navMeshAgent.ResetPath();

                CurrentGoal = ActionPlan.AgentGoal;
                Debug.Log($"Goal: {CurrentGoal.Name} with {ActionPlan.Actions.Count} actions in plan");
                CurrentAction = ActionPlan.Actions.Pop();
                Debug.Log($"Popped action: {CurrentAction.Name}");
                // Verify all precondition effects are true
                if (CurrentAction.Preconditions.All(b => b.Evaluate())) {
                    CurrentAction.Start();
                } else {
                    Debug.Log("Preconditions not met, clearing current action and goal");
                    CurrentAction = null;
                    CurrentGoal = null;
                }
            }
        }

        // If we have a current action, execute it
        if (ActionPlan != null && CurrentAction != null) {
            CurrentAction.Update(Time.deltaTime);

            if (CurrentAction.Complete) {
                Debug.Log($"{CurrentAction.Name} complete");
                CurrentAction.Stop();
                CurrentAction = null;

                if (ActionPlan.Actions.Count == 0) {
                    Debug.Log("Plan complete");
                    _lastGoal = CurrentGoal;
                    CurrentGoal = null;
                }
            }
        }
    }

    private void CalculatePlan() {
        var priorityLevel = CurrentGoal?.Priority ?? 0;
        
        HashSet<AgentGoal> goalsToCheck = Goals;
        
        // If we have a current goal, we only want to check goals with higher priority
        if (CurrentGoal != null) {
            Debug.Log("Current goal exists, checking goals with higher priority");
            goalsToCheck = new HashSet<AgentGoal>(Goals.Where(g => g.Priority > priorityLevel));
        }
        
        var potentialPlan = _gPlanner.Plan(this, goalsToCheck, _lastGoal);
        if (potentialPlan != null) {
            ActionPlan = potentialPlan;
        }
    }
}