using System;
using System.Collections.Generic;
using System.Linq;
using Agents;
using UnityEngine;
using Utilities;

namespace Building_Blocks
{
    public interface IGoapPlanner
    {
        ActionPlan Plan(GoapAgent agent, HashSet<AgentGoal> goals, AgentGoal mostRecentGoal = null);
    }

    public class GoapPlanner : IGoapPlanner
    {
        public ActionPlan Plan(GoapAgent agent, HashSet<AgentGoal> goals, AgentGoal mostRecentGoal = null)
        {
            // Order goals by priority, descending
            List<AgentGoal> orderedGoals = goals
                // Don't include goals where the effects are already complete
                .Where(g => g.DesiredEffects.Any(b => !b.Evaluate()))
                // Operator to not grab the same top goal over and over
                .OrderByDescending(g => g == mostRecentGoal ? g.Priority -0.01 : g.Priority)
                .ToList();
            
            foreach (AgentGoal goal in orderedGoals)
            {
                // Step 1: Get required effects that aren't already true
                HashSet<AgentBelief> startRequired = new(goal.DesiredEffects
                    .Where(b => !b.Evaluate()));
                
                // If nothing required, this goal is already satisfied
                if (startRequired.Count == 0) continue;
                
                Debug.Log($"Trying to achieve goal: {goal.Name}");
                
                // Step 2: Create start node
                Node startNode = new(null, null, startRequired, 0);
                float startH = Heuristic(startRequired);
                
                // Step 3: Initialize priority queue and visited set
                PriorityQueue<Node> queue = new();
                queue.Enqueue(startNode, startH);
                
                Dictionary<string, float> visited = new() { [GetStateKey(startRequired)] = 0 };

                // Step 4: A* loop
                while (queue.Count > 0)
                {
                    Node currentNode = queue.Dequeue();
                    
                    // Check if goal reached
                    if (currentNode.RequiredEffects.Count == 0)
                    {
                        Debug.Log($"Goal {goal.Name} reached!");
                        
                        // Reconstruct plan by walking backwards from the goal node
                        Stack<AgentAction> actionStack = new();
                        Node node = currentNode;
                        
                        // Collect actions in reverse order (from goal back to start)
                        while (node.Parent != null)
                        {
                            if (node.Action != null)
                            {
                                actionStack.Push(node.Action);
                            }
                            node = node.Parent;
                        }
                        
                        // Reverse to get correct execution order
                        Stack<AgentAction> correctOrderStack = new();
                        while (actionStack.Count > 0)
                        {
                            correctOrderStack.Push(actionStack.Pop());
                        }
                        
                        Debug.Log($"Found plan with {correctOrderStack.Count} actions for goal: {goal.Name}");
                        return new ActionPlan(goal, correctOrderStack, currentNode.Cost);
                    }
                    
                    // Generate successors
                    foreach (AgentAction action in agent.Actions)
                    {
                        // Check if this action satisfies any required effects
                        bool hasIntersection = action.Effects.Any(e => currentNode.RequiredEffects.Contains(e));
                        if (!hasIntersection) continue;
                        
                        Debug.Log($"Action {action.Name} can help achieve {goal.Name}");
                        
                        // Calculate new required effects: (current - action.Effects) + action.Preconditions
                        HashSet<AgentBelief> newRequired = new(currentNode.RequiredEffects);
                        
                        // Remove effects that this action satisfies
                        newRequired.ExceptWith(action.Effects);
                        
                        // Add preconditions, but ONLY if they aren't already true in the current world state
                        foreach (AgentBelief precondition in action.Preconditions)
                        {
                            if (!precondition.Evaluate())
                            {
                                newRequired.Add(precondition);
                            }
                        }
                        
                        float newG = currentNode.Cost + action.Cost;
                        float newH = Heuristic(newRequired);
                        float newF = newG + newH;
                        
                        string stateKey = GetStateKey(newRequired);
                        
                        // Check if we've seen this state with a better or equal cost
                        if (visited.TryGetValue(stateKey, out float existingCost))
                        {
                            if (newG >= existingCost) continue;
                        }
                        
                        visited[stateKey] = newG;
                        Node newNode = new(currentNode, action, newRequired, newG);
                        queue.Enqueue(newNode, newF);
                    }
                }
                
                Debug.Log($"No plan found for goal: {goal.Name}");
            }
            return null;
        }
        
        private static string GetStateKey(HashSet<AgentBelief> requiredEffects)
        {
            // You'll need to sort the belief names to ensure consistent keys
            // For now, just join them with commas
            return string.Join(",", requiredEffects.Select(b => b.Name).OrderBy(n => n));
        }
        private static float Heuristic(HashSet<AgentBelief> requiredEffects)
        {
            // Start simple - just count remaining effects
            // You can improve this later
            return requiredEffects.Count;
        }
    }

    /// <summary>
    /// Node graph of actions, made up of a Parent Node, an Agent Action, a HashSet of AgentBelief,
    /// a List of Nodes for the Leaves and the Cost as a float
    /// </summary>
    public class Node
    {
        public Node Parent { get; }
        public AgentAction Action { get; }
        public HashSet<AgentBelief> RequiredEffects { get; }
        public List<Node> Leaves { get; }
        public float Cost { get; }
    
        public bool IsLeafDead => Leaves.Count == 0 && Action == null;

        public Node(Node parent, AgentAction action, HashSet<AgentBelief> effects, float cost)
        {
            Parent = parent;
            Action = action;
            RequiredEffects = new HashSet<AgentBelief>(effects);
            Leaves = new List<Node>();
            Cost = cost;
        }
    }

    /// <summary>
    /// Struct for plan storage
    /// </summary>
    [Serializable]
    public class ActionPlan
    {
        public AgentGoal AgentGoal { get; }
        public Stack<AgentAction> Actions { get; }
        public float TotalCost {get; set;}
    
        public ActionPlan(AgentGoal agentGoal,  Stack<AgentAction> actions,  float totalCost)
        {
            AgentGoal = agentGoal;
            Actions = actions;
            TotalCost = totalCost;
        }
    }
}