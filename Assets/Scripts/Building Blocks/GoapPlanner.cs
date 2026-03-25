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
        // Cache for state keys to avoid repeated string allocations
        private readonly Dictionary<int, string> _stateKeyCache = new();
        
        public ActionPlan Plan(GoapAgent agent, HashSet<AgentGoal> goals, AgentGoal mostRecentGoal = null)
        {
            // Pre-filter goals that are already satisfied
            var validGoals = goals
                .Where(g => g.DesiredEffects.Any(b => !b.Evaluate()))
                .ToList();
            
            // Sort once with priority adjustment for most recent goal
            validGoals.Sort((a, b) =>
            {
                float priorityA = a == mostRecentGoal ? a.Priority - 0.01f : a.Priority;
                float priorityB = b == mostRecentGoal ? b.Priority - 0.01f : b.Priority;
                return priorityB.CompareTo(priorityA); // Descending order
            });

            foreach (AgentGoal goal in validGoals)
            {
                // Get required effects that aren't already true
                HashSet<AgentBelief> startRequired = new(goal.DesiredEffects.Where(b => !b.Evaluate()));
                
                // Early skip if no effects needed (should be caught by filter, but safe check)
                if (startRequired.Count == 0) continue;
                
                ActionPlan plan = FindPlanForGoal(agent, goal, startRequired);
                if (plan != null)
                {
                    return plan;
                }
            }
            
            return null;
        }

        private ActionPlan FindPlanForGoal(GoapAgent agent, AgentGoal goal, HashSet<AgentBelief> startRequired)
        {
            // Create start node
            Node startNode = new(null, null, startRequired, 0);
            float startH = Heuristic(startRequired);
            
            // Use priority queue with better performance characteristics
            var queue = new PriorityQueue<Node>();
            queue.Enqueue(startNode, startH);
            
            // Use dictionary with custom comparer for HashSet<string> to avoid string concatenation
            var visited = new Dictionary<int, float>();
            visited[GetStateKeyHash(startRequired)] = 0;

            while (queue.Count > 0)
            {
                Node currentNode = queue.Dequeue();
                
                // Goal reached - reconstruct plan
                if (currentNode.RequiredEffects.Count == 0)
                {
                    return ReconstructPlan(goal, currentNode);
                }
                
                // Generate successors
                foreach (AgentAction action in agent.Actions)
                {
                    // Check if this action can help achieve any required effect
                    if (!HasUsefulEffects(action, currentNode.RequiredEffects)) continue;
                    
                    // Calculate new required effects
                    HashSet<AgentBelief> newRequired = CalculateNewRequired(currentNode.RequiredEffects, action);
                    
                    float newCost = currentNode.Cost + action.Cost;
                    float newHeuristic = Heuristic(newRequired);
                    int stateKey = GetStateKeyHash(newRequired);
                    
                    // Check if we've found a better path to this state
                    if (visited.TryGetValue(stateKey, out float existingCost) && newCost >= existingCost)
                    {
                        continue;
                    }
                    
                    visited[stateKey] = newCost;
                    Node newNode = new(currentNode, action, newRequired, newCost);
                    queue.Enqueue(newNode, newCost + newHeuristic);
                }
            }
            
            return null;
        }

        private bool HasUsefulEffects(AgentAction action, HashSet<AgentBelief> requiredEffects)
        {
            foreach (AgentBelief effect in action.Effects)
            {
                if (requiredEffects.Contains(effect))
                {
                    return true;
                }
            }
            return false;
        }

        private HashSet<AgentBelief> CalculateNewRequired(HashSet<AgentBelief> currentRequired, AgentAction action)
        {
            // Create new hashset with capacity hint for better performance
            var newRequired = new HashSet<AgentBelief>(currentRequired.Count + action.Preconditions.Count);
            
            // Add all current required effects except those satisfied by this action
            foreach (AgentBelief effect in currentRequired)
            {
                if (!action.Effects.Contains(effect))
                {
                    newRequired.Add(effect);
                }
            }
            
            // Add preconditions that aren't already satisfied
            foreach (AgentBelief precondition in action.Preconditions)
            {
                if (!precondition.Evaluate())
                {
                    newRequired.Add(precondition);
                }
            }
            
            return newRequired;
        }

        private ActionPlan ReconstructPlan(AgentGoal goal, Node endNode)
        {
            var actionStack = new Stack<AgentAction>();
            Node node = endNode;
            
            // Collect actions in reverse order
            while (node.Parent != null)
            {
                if (node.Action != null)
                {
                    actionStack.Push(node.Action);
                }
                node = node.Parent;
            }
            
            // Reverse to correct order
            var correctOrderStack = new Stack<AgentAction>();
            while (actionStack.Count > 0)
            {
                correctOrderStack.Push(actionStack.Pop());
            }
            
            return new ActionPlan(goal, correctOrderStack, endNode.Cost);
        }

        private int GetStateKeyHash(HashSet<AgentBelief> requiredEffects)
        {
            // Use a deterministic hash code based on belief names
            int hash = 17;
            foreach (var belief in requiredEffects.OrderBy(b => b.Name))
            {
                hash = hash * 31 + belief.Name.GetHashCode();
            }
            return hash;
        }

        private static float Heuristic(HashSet<AgentBelief> requiredEffects)
        {
            // Simple count heuristic - can be improved with domain knowledge
            return requiredEffects.Count;
        }
    }

    /// <summary>
    /// Node graph of actions, made up of a Parent Node, an Agent Action, a HashSet of AgentBelief,
    /// and the Cost as a float
    /// </summary>
    public class Node
    {
        public Node Parent { get; }
        public AgentAction Action { get; }
        public HashSet<AgentBelief> RequiredEffects { get; }
        public float Cost { get; }

        public Node(Node parent, AgentAction action, HashSet<AgentBelief> effects, float cost)
        {
            Parent = parent;
            Action = action;
            RequiredEffects = new HashSet<AgentBelief>(effects);
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
        public float TotalCost { get; set; }
    
        public ActionPlan(AgentGoal agentGoal, Stack<AgentAction> actions, float totalCost)
        {
            AgentGoal = agentGoal;
            Actions = actions;
            TotalCost = totalCost;
        }
    }
}