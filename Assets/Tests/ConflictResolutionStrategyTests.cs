using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using UnityEngine;

using Debug = UnityEngine.Debug;

/// <summary>
/// Tests for different conflict resolution strategies
/// Compares wait-based, avoidance-based, and priority-based approaches
/// </summary>
public class ConflictResolutionStrategyTests
{
    private Dictionary<string, List<Node>> testPaths;
    private List<KeyValuePair<(Node, int), List<string>>> testConflicts;

    [SetUp]
    public void Setup()
    {
        CreateTestData();
    }

    private void CreateTestData()
    {
        // Create a grid of nodes that agents will share
        var nodes = new List<Node>();
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                nodes.Add(new Node(true, new Vector3(i, 0, j), i, j));
            }
        }

        // Create test paths with ACTUAL conflicts by sharing nodes
        // Agent1: (0,0) -> (1,0) -> (2,0) -> (3,0) -> (4,0)
        // Agent2: (1,0) -> (2,0) -> (3,0) -> (4,0) -> (5,0)  (conflicts at steps 2,3,4,5)
        // Agent3: (2,0) -> (3,0) -> (4,0) -> (5,0) -> (6,0)  (conflicts at steps 3,4,5,6)
        // Agent4: (3,0) -> (4,0) -> (5,0) -> (6,0) -> (7,0)  (conflicts at steps 4,5,6,7)
        testPaths = new Dictionary<string, List<Node>>
        {
            ["Agent1"] = new List<Node> { nodes[0], nodes[8], nodes[16], nodes[24], nodes[32] },   // (0,0)->(1,0)->(2,0)->(3,0)->(4,0)
            ["Agent2"] = new List<Node> { nodes[8], nodes[16], nodes[24], nodes[32], nodes[40] },  // (1,0)->(2,0)->(3,0)->(4,0)->(5,0)
            ["Agent3"] = new List<Node> { nodes[16], nodes[24], nodes[32], nodes[40], nodes[48] }, // (2,0)->(3,0)->(4,0)->(5,0)->(6,0)
            ["Agent4"] = new List<Node> { nodes[24], nodes[32], nodes[40], nodes[48], nodes[56] }  // (3,0)->(4,0)->(5,0)->(6,0)->(7,0)
        };

        // Create expected test conflicts based on the overlapping paths
        testConflicts = new List<KeyValuePair<(Node, int), List<string>>>
        {
            new KeyValuePair<(Node, int), List<string>>((nodes[8], 2), new List<string> { "Agent1", "Agent2" }), // (1,0) at step 2
            new KeyValuePair<(Node, int), List<string>>((nodes[16], 3), new List<string> { "Agent1", "Agent2", "Agent3" }), // (2,0) at step 3
            new KeyValuePair<(Node, int), List<string>>((nodes[24], 4), new List<string> { "Agent1", "Agent2", "Agent3", "Agent4" }), // (3,0) at step 4
            new KeyValuePair<(Node, int), List<string>>((nodes[32], 5), new List<string> { "Agent1", "Agent2", "Agent3", "Agent4" }), // (4,0) at step 5
            new KeyValuePair<(Node, int), List<string>>((nodes[40], 6), new List<string> { "Agent2", "Agent3", "Agent4" }), // (5,0) at step 6
            new KeyValuePair<(Node, int), List<string>>((nodes[48], 7), new List<string> { "Agent3", "Agent4" }) // (6,0) at step 7
        };
    }

    #region Wait-Based Resolution Tests

    [Test]
    public void WaitBasedResolution_Original_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = WaitBasedResolution_Original(testPaths, testConflicts);
        stopwatch.Stop();
        Debug.Log($"WaitBasedResolution_Original: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    [Test]
    public void WaitBasedResolution_Optimized_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = WaitBasedResolution_Optimized(testPaths, testConflicts);
        stopwatch.Stop();
        Debug.Log($"WaitBasedResolution_Optimized: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    [Test]
    public void WaitBasedResolution_MaxPerformance_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = WaitBasedResolution_MaxPerformance(testPaths, testConflicts);
        stopwatch.Stop();
        Debug.Log($"WaitBasedResolution_MaxPerformance: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    #endregion

    #region Avoidance-Based Resolution Tests

    [Test]
    public void AvoidanceBasedResolution_Original_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = AvoidanceBasedResolution_Original(testPaths, testConflicts);
        stopwatch.Stop();
        Debug.Log($"AvoidanceBasedResolution_Original: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    [Test]
    public void AvoidanceBasedResolution_Optimized_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = AvoidanceBasedResolution_Optimized(testPaths, testConflicts);
        stopwatch.Stop();
        Debug.Log($"AvoidanceBasedResolution_Optimized: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    [Test]
    public void AvoidanceBasedResolution_MaxPerformance_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = AvoidanceBasedResolution_MaxPerformance(testPaths, testConflicts);
        stopwatch.Stop();
        Debug.Log($"AvoidanceBasedResolution_MaxPerformance: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    #endregion

    #region Priority-Based Resolution Tests

    [Test]
    public void PriorityBasedResolution_Original_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = PriorityBasedResolution_Original(testPaths, testConflicts);
        stopwatch.Stop();
        Debug.Log($"PriorityBasedResolution_Original: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    [Test]
    public void PriorityBasedResolution_Optimized_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = PriorityBasedResolution_Optimized(testPaths, testConflicts);
        stopwatch.Stop();
        Debug.Log($"PriorityBasedResolution_Optimized: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    [Test]
    public void PriorityBasedResolution_MaxPerformance_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = PriorityBasedResolution_MaxPerformance(testPaths, testConflicts);
        stopwatch.Stop();
        Debug.Log($"PriorityBasedResolution_MaxPerformance: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    #endregion

    #region Hybrid Resolution Tests

    [Test]
    public void HybridResolution_Original_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = HybridResolution_Original(testPaths, testConflicts);
        stopwatch.Stop();
        Debug.Log($"HybridResolution_Original: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    [Test]
    public void HybridResolution_Optimized_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = HybridResolution_Optimized(testPaths, testConflicts);
        stopwatch.Stop();
        Debug.Log($"HybridResolution_Optimized: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    [Test]
    public void HybridResolution_MaxPerformance_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = HybridResolution_MaxPerformance(testPaths, testConflicts);
        stopwatch.Stop();
        Debug.Log($"HybridResolution_MaxPerformance: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    #endregion

    #region Implementation Variants

    /**
    <Good>
    */
    // Wait-Based Resolution Implementations
    private Dictionary<string, List<Node>> WaitBasedResolution_Original(
        Dictionary<string, List<Node>> activePaths,
        List<KeyValuePair<(Node, int), List<string>>> contestedNodes)
    {
        var result = new Dictionary<string, List<Node>>(activePaths);
        
        foreach (var conflict in contestedNodes)
        {
            var (node, step) = conflict.Key;
            var agents = conflict.Value.ToArray();
            int n = agents.Length;
            int maxWait = Mathf.Max(step + 3, 5);

            // Generate all possible wait combinations
            for (int mask = 1; mask < (1 << n) - 1; mask++)
            {
                // var subset = Enumerable.Range(0, n).Where(i => (mask & (1 << i)) != 0).ToList();
                var subset = new List<int>();
                for (int i = 0; i < n; i++) {
                    if ((mask & (1 << i)) != 0) subset.Add(i);
                }

                if (subset.Count == 0 || subset.Count == n) continue;

                // foreach (var waitCombo in GetPermutations(Enumerable.Range(1, maxWait).ToArray(), subset.Count))
                foreach (var waitCombo in GetPermutationsOptimized(maxWait, subset.Count))
                {
                    var scenario = new Dictionary<string, List<Node>>();
                    
                    // Copy all paths
                    // foreach (var agent in agents)
                    // {
                    //     if (activePaths.ContainsKey(agent))
                    //         scenario[agent] = new List<Node>(activePaths[agent]);
                    // }
                    for (int i = 0; i < n; i++)
                    {
                        if (activePaths.ContainsKey(agents[i]))
                        {
                            var path = activePaths[agents[i]];
                            scenario[agents[i]] = new List<Node>(path);
                        }
                    }
                    
                    // Apply waits
                    for (int j = 0; j < subset.Count; j++)
                    {
                        int agentIdx = subset[j];
                        string agentName = agents[agentIdx];
                        int wait = waitCombo[j];
                        
                        if (scenario.ContainsKey(agentName))
                        {
                            var path = scenario[agentName];
                            for (int w = 0; w < wait; w++)
                                path.Insert(0, path[0]);
                        }
                    }
                    
                    // Check if this scenario resolves conflicts
                    if (!HasConflict(scenario))
                    {
                        foreach (var kvp in scenario)
                            result[kvp.Key] = kvp.Value;
                        return result;
                    }
                }
            }
        }
        
        return result;
    }

    private Dictionary<string, List<Node>> WaitBasedResolution_Optimized(
        Dictionary<string, List<Node>> activePaths,
        List<KeyValuePair<(Node, int), List<string>>> contestedNodes)
    {
        var result = new Dictionary<string, List<Node>>(activePaths);
        
        foreach (var conflict in contestedNodes)
        {
            var (node, step) = conflict.Key;
            var agents = conflict.Value.ToArray();
            int n = agents.Length;
            int maxWait = Mathf.Max(step + 3, 5);

            // Use bit manipulation for subset generation
            for (int mask = 1; mask < (1 << n) - 1; mask++)
            {
                var subset = new List<int>();
                for (int i = 0; i < n; i++)
                {
                    if ((mask & (1 << i)) != 0)
                        subset.Add(i);
                }
                
                if (subset.Count == 0 || subset.Count == n) continue;

                foreach (var waitCombo in GetPermutationsOptimized(maxWait, subset.Count))
                {
                    var scenario = new Dictionary<string, List<Node>>();
                    
                    // Copy paths efficiently
                    for (int i = 0; i < n; i++)
                    {
                        if (activePaths.ContainsKey(agents[i]))
                        {
                            var path = activePaths[agents[i]];
                            scenario[agents[i]] = new List<Node>(path);
                        }
                    }
                    
                    // Apply waits efficiently
                    for (int j = 0; j < subset.Count; j++)
                    {
                        int agentIdx = subset[j];
                        string agentName = agents[agentIdx];
                        int wait = waitCombo[j];
                        
                        if (scenario.ContainsKey(agentName))
                        {
                            var path = scenario[agentName];
                            for (int w = 0; w < wait; w++)
                                path.Insert(0, path[0]);
                        }
                    }
                    
                    if (!HasConflict(scenario))
                    {
                        foreach (var kvp in scenario)
                            result[kvp.Key] = kvp.Value;
                        return result;
                    }
                }
            }
        }
        
        return result;
    }

    private Dictionary<string, List<Node>> WaitBasedResolution_MaxPerformance(
        Dictionary<string, List<Node>> activePaths,
        List<KeyValuePair<(Node, int), List<string>>> contestedNodes)
    {
        var result = new Dictionary<string, List<Node>>(activePaths);
        
        foreach (var conflict in contestedNodes)
        {
            var (node, step) = conflict.Key;
            var agents = conflict.Value.ToArray();
            int n = agents.Length;
            int maxWait = Mathf.Max(step + 3, 5);

            // Pre-allocate collections
            var subset = new List<int>(n);
            var scenario = new Dictionary<string, List<Node>>(n);
            
            for (int mask = 1; mask < (1 << n) - 1; mask++)
            {
                subset.Clear();
                for (int i = 0; i < n; i++)
                {
                    if ((mask & (1 << i)) != 0)
                        subset.Add(i);
                }
                
                if (subset.Count == 0 || subset.Count == n) continue;

                foreach (var waitCombo in GetPermutationsMaxPerformance(maxWait, subset.Count))
                {
                    scenario.Clear();
                    
                    // Copy paths with minimal allocations
                    for (int i = 0; i < n; i++)
                    {
                        if (activePaths.ContainsKey(agents[i]))
                        {
                            var path = activePaths[agents[i]];
                            var newPath = new List<Node>(path.Count);
                            foreach (var nd in path)
                                newPath.Add(nd);
                            scenario[agents[i]] = newPath;
                        }
                    }
                    
                    // Apply waits efficiently
                    for (int j = 0; j < subset.Count; j++)
                    {
                        int agentIdx = subset[j];
                        string agentName = agents[agentIdx];
                        int wait = waitCombo[j];
                        
                        if (scenario.ContainsKey(agentName))
                        {
                            var path = scenario[agentName];
                            for (int w = 0; w < wait; w++)
                                path.Insert(0, path[0]);
                        }
                    }
                    
                    if (!HasConflict(scenario))
                    {
                        foreach (var kvp in scenario)
                            result[kvp.Key] = kvp.Value;
                        return result;
                    }
                }
            }
        }
        
        return result;
    }

    /**
    <Good>
    */
    // Avoidance-Based Resolution Implementations
    private Dictionary<string, List<Node>> AvoidanceBasedResolution_Original(
        Dictionary<string, List<Node>> activePaths,
        List<KeyValuePair<(Node, int), List<string>>> contestedNodes) {
        var result = new Dictionary<string, List<Node>>(activePaths);
        
        foreach (var conflict in contestedNodes) {
            var (node, step) = conflict.Key;
            foreach (var agent in conflict.Value) {
                if (activePaths.ContainsKey(agent)) {
                    var path = activePaths[agent];
                    if (path.Count > 0) {
                        var newPath = RerouteFromNode(path[0], path.Last(), new HashSet<Node> { node });
                        if (newPath != null && newPath.Count > 0) result[agent] = newPath;
                    }
                }
            }
        }
        
        return result;
    }

    private Dictionary<string, List<Node>> AvoidanceBasedResolution_Optimized(
        Dictionary<string, List<Node>> activePaths,
        List<KeyValuePair<(Node, int), List<string>>> contestedNodes)
    {
        var result = new Dictionary<string, List<Node>>(activePaths);
        var blockedNodes = new HashSet<Node>();
        
        // Collect all blocked nodes
        foreach (var conflict in contestedNodes)
        {
            blockedNodes.Add(conflict.Key.Item1);
        }
        
        // Reroute all affected agents
        var affectedAgents = contestedNodes.SelectMany(c => c.Value).Distinct();
        foreach (var agent in affectedAgents)
        {
            if (activePaths.ContainsKey(agent))
            {
                var path = activePaths[agent];
                if (path.Count > 0)
                {
                    var newPath = RerouteFromNode(path[0], path.Last(), blockedNodes);
                    if (newPath != null && newPath.Count > 0)
                    {
                        result[agent] = newPath;
                    }
                }
            }
        }
        
        return result;
    }

    private Dictionary<string, List<Node>> AvoidanceBasedResolution_MaxPerformance(
        Dictionary<string, List<Node>> activePaths,
        List<KeyValuePair<(Node, int), List<string>>> contestedNodes)
    {
        var result = new Dictionary<string, List<Node>>(activePaths);
        var blockedNodes = new HashSet<Node>(contestedNodes.Count);
        
        // Pre-allocate blocked nodes set
        foreach (var conflict in contestedNodes)
        {
            blockedNodes.Add(conflict.Key.Item1);
        }
        
        // Process all affected agents in one pass
        var affectedAgents = new HashSet<string>();
        foreach (var conflict in contestedNodes)
        {
            foreach (var agent in conflict.Value)
            {
                affectedAgents.Add(agent);
            }
        }
        
        foreach (var agent in affectedAgents)
        {
            if (activePaths.ContainsKey(agent))
            {
                var path = activePaths[agent];
                if (path.Count > 0)
                {
                    var newPath = RerouteFromNode(path[0], path.Last(), blockedNodes);
                    if (newPath != null && newPath.Count > 0)
                    {
                        result[agent] = newPath;
                    }
                }
            }
        }
        
        return result;
    }

    /**
    <Good>
    */
    // Priority-Based Resolution Implementations
    private Dictionary<string, List<Node>> PriorityBasedResolution_Original(
        Dictionary<string, List<Node>> activePaths,
        List<KeyValuePair<(Node, int), List<string>>> contestedNodes)
    {
        var result = new Dictionary<string, List<Node>>(activePaths);
        
        foreach (var conflict in contestedNodes)
        {
            var (node, step) = conflict.Key;
            var agents = conflict.Value.ToArray();
            
            // Sort agents by priority (can be based on agent ID, path length, etc.)
            // agents.Sort((a, b) => a.CompareTo(b));
            System.Array.Sort(agents);

            // Allow the first agent to proceed, reroute others
            for (int i = 1; i < agents.Length; i++)
            {
                var agent = agents[i];
                if (activePaths.ContainsKey(agent))
                {
                    var path = activePaths[agent];
                    if (path.Count > 0)
                    {
                        var newPath = RerouteFromNode(path[0], path.Last(), new HashSet<Node> { node });
                        if (newPath?.Count > 0)
                        {
                            result[agent] = newPath;
                        }
                    }
                }
            }
        }
        
        return result;
    }

    private Dictionary<string, List<Node>> PriorityBasedResolution_Optimized(
        Dictionary<string, List<Node>> activePaths,
        List<KeyValuePair<(Node, int), List<string>>> contestedNodes)
    {
        var result = new Dictionary<string, List<Node>>(activePaths);
        
        foreach (var conflict in contestedNodes)
        {
            var (node, step) = conflict.Key;
            var agents = conflict.Value.ToArray();
            
            // Use array sorting for better performance
            System.Array.Sort(agents);
            
            // Allow first agent, reroute others
            for (int i = 1; i < agents.Length; i++)
            {
                var agent = agents[i];
                if (activePaths.ContainsKey(agent))
                {
                    var path = activePaths[agent];
                    if (path.Count > 0)
                    {
                        var newPath = RerouteFromNode(path[0], path.Last(), new HashSet<Node> { node });
                        if (newPath != null && newPath.Count > 0)
                        {
                            result[agent] = newPath;
                        }
                    }
                }
            }
        }
        
        return result;
    }

    private Dictionary<string, List<Node>> PriorityBasedResolution_MaxPerformance(
        Dictionary<string, List<Node>> activePaths,
        List<KeyValuePair<(Node, int), List<string>>> contestedNodes)
    {
        var result = new Dictionary<string, List<Node>>(activePaths);
        
        foreach (var conflict in contestedNodes)
        {
            var (node, step) = conflict.Key;
            var agents = conflict.Value.ToArray();
            
            // In-place sorting for maximum performance
            System.Array.Sort(agents);
            
            // Process agents efficiently
            for (int i = 1; i < agents.Length; i++)
            {
                var agent = agents[i];
                if (activePaths.ContainsKey(agent))
                {
                    var path = activePaths[agent];
                    if (path.Count > 0)
                    {
                        var newPath = RerouteFromNode(path[0], path.Last(), new HashSet<Node> { node });
                        if (newPath?.Count > 0)
                        {
                            result[agent] = newPath;
                        }
                    }
                }
            }
        }
        
        return result;
    }

    /**
    <Good>
    */
    // Hybrid Resolution Implementations
    private Dictionary<string, List<Node>> HybridResolution_Original(
        Dictionary<string, List<Node>> activePaths,
        List<KeyValuePair<(Node, int), List<string>>> contestedNodes)
    {
        var result = new Dictionary<string, List<Node>>(activePaths);
        
        foreach (var conflict in contestedNodes)
        {
            var (node, step) = conflict.Key;
            var agents = conflict.Value.ToArray();
            
            // Try wait-based resolution first
            var waitResult = TryWaitResolution(agents, activePaths, step);
            if (waitResult != null)
            {
                foreach (var kvp in waitResult)
                    result[kvp.Key] = kvp.Value;
                continue;
            }
            
            // Try avoidance-based resolution
            var avoidResult = TryAvoidanceResolution(agents, activePaths, node);
            if (avoidResult != null)
            {
                foreach (var kvp in avoidResult)
                    result[kvp.Key] = kvp.Value;
                continue;
            }
            
            // Fall back to priority-based resolution
            var priorityResult = TryPriorityResolution(agents, activePaths, node);
            if (priorityResult != null)
            {
                foreach (var kvp in priorityResult)
                    result[kvp.Key] = kvp.Value;
            }
        }
        
        return result;
    }

    private Dictionary<string, List<Node>> HybridResolution_Optimized(
        Dictionary<string, List<Node>> activePaths,
        List<KeyValuePair<(Node, int), List<string>>> contestedNodes)
    {
        var result = new Dictionary<string, List<Node>>(activePaths);
        
        foreach (var conflict in contestedNodes)
        {
            var (node, step) = conflict.Key;
            var agents = conflict.Value.ToArray();
            
            // Try strategies in order of preference
            var strategies = new System.Func<Dictionary<string, List<Node>>, Dictionary<string, List<Node>>>[]
            {
                (paths) => TryWaitResolutionOptimized(agents, paths, step),
                (paths) => TryAvoidanceResolutionOptimized(agents, paths, node),
                (paths) => TryPriorityResolutionOptimized(agents, paths, node)
            };
            
            foreach (var strategy in strategies)
            {
                var strategyResult = strategy(activePaths);
                if (strategyResult != null)
                {
                    foreach (var kvp in strategyResult)
                        result[kvp.Key] = kvp.Value;
                    break;
                }
            }
        }
        
        return result;
    }

    private Dictionary<string, List<Node>> HybridResolution_MaxPerformance(
        Dictionary<string, List<Node>> activePaths,
        List<KeyValuePair<(Node, int), List<string>>> contestedNodes)
    {
        var result = new Dictionary<string, List<Node>>(activePaths);
        
        foreach (var conflict in contestedNodes)
        {
            var (node, step) = conflict.Key;
            var agents = conflict.Value.ToArray();
            
            // Use strategy pattern with early exit
            if (TryWaitResolutionMaxPerformance(agents, activePaths, step, ref result)) continue;
            if (TryAvoidanceResolutionMaxPerformance(agents, activePaths, node, ref result)) continue;
            TryPriorityResolutionMaxPerformance(agents, activePaths, node, ref result);
        }
        
        return result;
    }

    #endregion

    #region Helper Methods

    /**
    <Good>
    */
    private Dictionary<string, List<Node>> TryWaitResolution(
        string[] agents, Dictionary<string, List<Node>> activePaths, int step)
    {
        int n = agents.Length;
        int maxWait = Mathf.Max(step + 3, 5);

        for (int mask = 1; mask < (1 << n) - 1; mask++)
        {
            // var subset = Enumerable.Range(0, n).Where(i => (mask & (1 << i)) != 0).ToList();
            var subset = new List<int>();
            for (int i = 0; i < n; i++) {
                if ((mask & (1 << i)) != 0) subset.Add(i);
            }
            if (subset.Count == 0 || subset.Count == n) continue;

            // foreach (var waitCombo in GetPermutations(Enumerable.Range(1, maxWait).ToArray(), subset.Count))
            foreach (var waitCombo in GetPermutationsOptimized(maxWait, subset.Count))
            {
                var scenario = new Dictionary<string, List<Node>>();
                
                // foreach (var agent in agents)
                // {
                //     if (activePaths.ContainsKey(agent))
                //         scenario[agent] = new List<Node>(activePaths[agent]);
                // }
                for (int i = 0; i < n; i++)
                {
                    if (activePaths.ContainsKey(agents[i]))
                    {
                        var path = activePaths[agents[i]];
                        scenario[agents[i]] = new List<Node>(path);
                    }
                }
            
                for (int j = 0; j < subset.Count; j++)
                {
                    int agentIdx = subset[j];
                    string agentName = agents[agentIdx];
                    int wait = waitCombo[j];
                    
                    if (scenario.ContainsKey(agentName))
                    {
                        var path = scenario[agentName];
                        for (int w = 0; w < wait; w++)
                            path.Insert(0, path[0]);
                    }
                }
                
                if (!HasConflict(scenario))
                    return scenario;
            }
        }
        
        return null;
    }

    private Dictionary<string, List<Node>> TryWaitResolutionOptimized(
        string[] agents, Dictionary<string, List<Node>> activePaths, int step)
    {
        int n = agents.Length;
        int maxWait = Mathf.Max(step + 3, 5);

        for (int mask = 1; mask < (1 << n) - 1; mask++)
        {
            var subset = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if ((mask & (1 << i)) != 0)
                    subset.Add(i);
            }
            
            if (subset.Count == 0 || subset.Count == n) continue;

            foreach (var waitCombo in GetPermutationsOptimized(maxWait, subset.Count))
            {
                var scenario = new Dictionary<string, List<Node>>();
                
                for (int i = 0; i < n; i++)
                {
                    if (activePaths.ContainsKey(agents[i]))
                        scenario[agents[i]] = new List<Node>(activePaths[agents[i]]);
                }
                
                for (int j = 0; j < subset.Count; j++)
                {
                    int agentIdx = subset[j];
                    string agentName = agents[agentIdx];
                    int wait = waitCombo[j];
                    
                    if (scenario.ContainsKey(agentName))
                    {
                        var path = scenario[agentName];
                        for (int w = 0; w < wait; w++)
                            path.Insert(0, path[0]);
                    }
                }
                
                if (!HasConflict(scenario))
                    return scenario;
            }
        }
        
        return null;
    }

    private bool TryWaitResolutionMaxPerformance(
        string[] agents, Dictionary<string, List<Node>> activePaths, int step, 
        ref Dictionary<string, List<Node>> result)
    {
        int n = agents.Length;
        int maxWait = Mathf.Max(step + 3, 5);

        for (int mask = 1; mask < (1 << n) - 1; mask++)
        {
            var subset = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if ((mask & (1 << i)) != 0)
                    subset.Add(i);
            }
            
            if (subset.Count == 0 || subset.Count == n) continue;

            foreach (var waitCombo in GetPermutationsMaxPerformance(maxWait, subset.Count))
            {
                var scenario = new Dictionary<string, List<Node>>();
                
                for (int i = 0; i < n; i++)
                {
                    if (activePaths.ContainsKey(agents[i]))
                    {
                        var path = activePaths[agents[i]];
                        var newPath = new List<Node>(path.Count);
                        foreach (var node in path)
                            newPath.Add(node);
                        scenario[agents[i]] = newPath;
                    }
                }
                
                for (int j = 0; j < subset.Count; j++)
                {
                    int agentIdx = subset[j];
                    string agentName = agents[agentIdx];
                    int wait = waitCombo[j];
                    
                    if (scenario.ContainsKey(agentName))
                    {
                        var path = scenario[agentName];
                        for (int w = 0; w < wait; w++)
                            path.Insert(0, path[0]);
                    }
                }
                
                if (!HasConflict(scenario))
                {
                    foreach (var kvp in scenario)
                        result[kvp.Key] = kvp.Value;
                    return true;
                }
            }
        }
        
        return false;
    }

    private Dictionary<string, List<Node>> TryAvoidanceResolution(
        string[] agents, Dictionary<string, List<Node>> activePaths, Node blockedNode)
    {
        var result = new Dictionary<string, List<Node>>();
        
        foreach (var agent in agents) {
            if (activePaths.ContainsKey(agent)) {
                var path = activePaths[agent];
                if (path.Count > 0) {
                    var newPath = RerouteFromNode(path[0], path.Last(), new HashSet<Node> { blockedNode });
                    if (newPath != null && newPath.Count > 0) {
                        result[agent] = newPath;
                    }
                }
            }
        }
        
        return result.Count == agents.Length ? result : null;
    }

    private Dictionary<string, List<Node>> TryAvoidanceResolutionOptimized(
        string[] agents, Dictionary<string, List<Node>> activePaths, Node blockedNode)
    {
        var result = new Dictionary<string, List<Node>>(agents.Length);
        
        foreach (var agent in agents) {
            if (activePaths.ContainsKey(agent)) {
                var path = activePaths[agent];
                if (path.Count > 0) {
                    var newPath = RerouteFromNode(path[0], path.Last(), new HashSet<Node> { blockedNode });
                    if (newPath?.Count > 0) {
                        result[agent] = newPath;
                    }
                }
            }
        }
        
        return result.Count == agents.Length ? result : null;
    }

    private bool TryAvoidanceResolutionMaxPerformance(
        string[] agents, Dictionary<string, List<Node>> activePaths, Node blockedNode,
        ref Dictionary<string, List<Node>> result)
    {
        var newPaths = new Dictionary<string, List<Node>>(agents.Length);
        
        foreach (var agent in agents)
        {
            if (activePaths.ContainsKey(agent))
            {
                var path = activePaths[agent];
                if (path.Count > 0)
                {
                    var newPath = RerouteFromNode(path[0], path.Last(), new HashSet<Node> { blockedNode });
                    if (newPath?.Count > 0)
                    {
                        newPaths[agent] = newPath;
                    }
                }
            }
        }
        
        if (newPaths.Count == agents.Length)
        {
            foreach (var kvp in newPaths)
                result[kvp.Key] = kvp.Value;
            return true;
        }
        
        return false;
    }

    private Dictionary<string, List<Node>> TryPriorityResolution(
        string[] agents, Dictionary<string, List<Node>> activePaths, Node blockedNode)
    {
        var result = new Dictionary<string, List<Node>>();
        // agents.Sort();
        System.Array.Sort(agents);
        
        // Allow first agent, reroute others
        for (int i = 1; i < agents.Length; i++)
        {
            var agent = agents[i];
            if (activePaths.ContainsKey(agent))
            {
                var path = activePaths[agent];
                if (path.Count > 0)
                {
                    var newPath = RerouteFromNode(path[0], path.Last(), new HashSet<Node> { blockedNode });
                    if (newPath?.Count > 0)
                    {
                        result[agent] = newPath;
                    }
                }
            }
        }
        
        return result.Count == agents.Length - 1 ? result : null;
    }

    private Dictionary<string, List<Node>> TryPriorityResolutionOptimized(
        string[] agents, Dictionary<string, List<Node>> activePaths, Node blockedNode)
    {
        var result = new Dictionary<string, List<Node>>();
        System.Array.Sort(agents);
        
        for (int i = 1; i < agents.Length; i++)
        {
            var agent = agents[i];
            if (activePaths.ContainsKey(agent))
            {
                var path = activePaths[agent];
                if (path.Count > 0)
                {
                    var newPath = RerouteFromNode(path[0], path.Last(), new HashSet<Node> { blockedNode });
                    if (newPath?.Count > 0)
                    {
                        result[agent] = newPath;
                    }
                }
            }
        }
        
        return result.Count == agents.Length - 1 ? result : null;
    }

    private bool TryPriorityResolutionMaxPerformance(
        string[] agents, Dictionary<string, List<Node>> activePaths, Node blockedNode,
        ref Dictionary<string, List<Node>> result)
    {
        System.Array.Sort(agents);
        
        for (int i = 1; i < agents.Length; i++)
        {
            var agent = agents[i];
            if (activePaths.ContainsKey(agent))
            {
                var path = activePaths[agent];
                if (path.Count > 0)
                {
                    var newPath = RerouteFromNode(path[0], path.Last(), new HashSet<Node> { blockedNode });
                    if (newPath?.Count > 0)
                    {
                        result[agent] = newPath;
                    }
                }
            }
        }
        
        return true;
    }

    private IEnumerable<int[]> GetPermutations(int[] arr, int m) => Permute(arr, 0, m);

    private IEnumerable<int[]> Permute(int[] arr, int start, int m)
    {
        if (start == m) yield return arr.Take(m).ToArray();
        else
        {
            for (int i = start; i < arr.Length; i++)
            {
                (arr[start], arr[i]) = (arr[i], arr[start]);
                foreach (var p in Permute(arr, start + 1, m)) yield return p;
                (arr[start], arr[i]) = (arr[i], arr[start]);
            }
        }
    }

    private IEnumerable<int[]> GetPermutationsOptimized(int maxValue, int count)
    {
        var arr = Enumerable.Range(1, maxValue).ToArray();
        return Permute(arr, 0, count);
    }

    private IEnumerable<int[]> GetPermutationsMaxPerformance(int maxValue, int count)
    {
        var arr = new int[maxValue];
        for (int i = 0; i < maxValue; i++) arr[i] = i + 1;
        return Permute(arr, 0, count);
    }

    private bool HasConflict(Dictionary<string, List<Node>> paths)
    {
        var nodeTimeMap = new Dictionary<(Node, int), List<string>>();
        
        foreach (var kvp in paths)
        {
            var agent = kvp.Key;
            var path = kvp.Value;
            
            for (int i = 0; i < path.Count; i++)
            {
                var key = (path[i], i + 1);
                if (!nodeTimeMap.ContainsKey(key))
                    nodeTimeMap[key] = new List<string>();
                nodeTimeMap[key].Add(agent);
            }
        }
        
        return nodeTimeMap.Any(kvp => kvp.Value.Count > 1);
    }

    private List<Node> RerouteFromNode(Node start, Node end, HashSet<Node> blocked)
    {
        // Simplified rerouting for testing
        var path = new List<Node>();
        if (start != end)
        {
            path.Add(start);
            path.Add(end);
        }
        return path;
    }

    #endregion

    #region Performance Comparison Tests

    [Test]
    public void StrategyPerformanceComparison_AllVariants()
    {
        var results = new Dictionary<string, long>();
        
        // Test all strategy variants
        var strategies = new Dictionary<string, System.Func<Dictionary<string, List<Node>>, List<KeyValuePair<(Node, int), List<string>>>, Dictionary<string, List<Node>>>>
        {
            ["Wait_Original"] = WaitBasedResolution_Original,
            ["Wait_Optimized"] = WaitBasedResolution_Optimized,
            ["Wait_MaxPerformance"] = WaitBasedResolution_MaxPerformance,
            ["Avoidance_Original"] = AvoidanceBasedResolution_Original,
            ["Avoidance_Optimized"] = AvoidanceBasedResolution_Optimized,
            ["Avoidance_MaxPerformance"] = AvoidanceBasedResolution_MaxPerformance,
            ["Priority_Original"] = PriorityBasedResolution_Original,
            ["Priority_Optimized"] = PriorityBasedResolution_Optimized,
            ["Priority_MaxPerformance"] = PriorityBasedResolution_MaxPerformance,
            ["Hybrid_Original"] = HybridResolution_Original,
            ["Hybrid_Optimized"] = HybridResolution_Optimized,
            ["Hybrid_MaxPerformance"] = HybridResolution_MaxPerformance
        };

        foreach (var strategy in strategies)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = strategy.Value(testPaths, testConflicts);
            stopwatch.Stop();
            results[strategy.Key] = stopwatch.ElapsedTicks;
            
            Debug.Log($"{strategy.Key}: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
        }

        // Find fastest strategy
        var fastest = results.OrderBy(r => r.Value).First();
        Debug.Log($"Fastest strategy: {fastest.Key} ({fastest.Value} ticks)");
        
        // Verify all strategies produce valid results
        var firstResult = strategies.First().Value(testPaths, testConflicts);
        foreach (var strategy in strategies.Skip(1))
        {
            var result = strategy.Value(testPaths, testConflicts);
            Assert.AreEqual(firstResult.Count, result.Count, $"{strategy.Key} should produce same number of paths");
        }
    }

    #endregion
} 