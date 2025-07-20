using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using UnityEngine;

using Debug = UnityEngine.Debug;

/// <summary>
/// Comprehensive tests for the resolve recursive method
/// Tests multiple implementation variants with different performance characteristics
/// </summary>
public class ResolveRecursiveTests
{
    private PathSupervisor pathSupervisor;
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
        for (int i = 0; i < 10; i++)
        {
            for (int j = 0; j < 10; j++)
            {
                nodes.Add(new Node(true, new Vector3(i, 0, j), i, j));
            }
        }

        // Create test paths with ACTUAL conflicts by sharing nodes
        // Agent1: (0,0) -> (1,0) -> (2,0) -> (3,0)
        // Agent2: (1,0) -> (2,0) -> (3,0) -> (4,0)  (conflicts at steps 2,3,4)
        // Agent3: (2,0) -> (3,0) -> (4,0) -> (5,0)  (conflicts at steps 3,4,5)
        testPaths = new Dictionary<string, List<Node>>
        {
            ["Agent1"] = new List<Node> { nodes[0], nodes[10], nodes[20], nodes[30] },  // (0,0)->(1,0)->(2,0)->(3,0)
            ["Agent2"] = new List<Node> { nodes[10], nodes[20], nodes[30], nodes[40] }, // (1,0)->(2,0)->(3,0)->(4,0)
            ["Agent3"] = new List<Node> { nodes[20], nodes[30], nodes[40], nodes[50] }  // (2,0)->(3,0)->(4,0)->(5,0)
        };

        // Create expected test conflicts based on the overlapping paths
        testConflicts = new List<KeyValuePair<(Node, int), List<string>>>
        {
            new KeyValuePair<(Node, int), List<string>>((nodes[10], 2), new List<string> { "Agent1", "Agent2" }), // (1,0) at step 2
            new KeyValuePair<(Node, int), List<string>>((nodes[20], 3), new List<string> { "Agent1", "Agent2", "Agent3" }), // (2,0) at step 3
            new KeyValuePair<(Node, int), List<string>>((nodes[30], 4), new List<string> { "Agent1", "Agent2", "Agent3" }), // (3,0) at step 4
            new KeyValuePair<(Node, int), List<string>>((nodes[40], 5), new List<string> { "Agent2", "Agent3" }) // (4,0) at step 5
        };
    }

    #region Resolve Recursive Method Tests

    [Test]
    public void ResolveRecursive_Original_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = ResolveRecursive_Original(testPaths, testConflicts);
        stopwatch.Stop();
        Debug.Log($"ResolveRecursive_Original: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    [Test]
    public void ResolveRecursive_Optimized_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = ResolveRecursive_Optimized(testPaths, testConflicts);
        stopwatch.Stop();
        Debug.Log($"ResolveRecursive_Optimized: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    [Test]
    public void ResolveRecursive_MaxPerformance_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = ResolveRecursive_MaxPerformance(testPaths, testConflicts);
        stopwatch.Stop();
        Debug.Log($"ResolveRecursive_MaxPerformance: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    [Test]
    public void ResolveRecursive_OneLiner_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = ResolveRecursive_OneLiner(testPaths, testConflicts);
        stopwatch.Stop();
        Debug.Log($"ResolveRecursive_OneLiner: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    [Test]
    public void ResolveRecursive_Functional_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = ResolveRecursive_Functional(testPaths, testConflicts);
        stopwatch.Stop();
        Debug.Log($"ResolveRecursive_Functional: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    #endregion

    #region Implementation Variants

    // Original implementation (based on current PathSupervisor)
    private Dictionary<string, List<Node>> ResolveRecursive_Original(
        Dictionary<string, List<Node>> activePaths,
        List<KeyValuePair<(Node, int), List<string>>> contestedNodes)
    {
        var nextActivePaths = new Dictionary<string, List<Node>>(activePaths);
        
        if (contestedNodes.Count == 0) return nextActivePaths;

        // var allConflictAgents = contestedNodes.SelectMany(c => c.Value).Distinct().ToList();
        var allConflictAgents = new HashSet<string>();
        foreach (var conflict in contestedNodes) {
            foreach (var agent in conflict.Value) {
                allConflictAgents.Add(agent);
            }
        }
        if (allConflictAgents.Count == 0) return nextActivePaths;

        var conflictedPaths = new Dictionary<string, List<Node>>();
        foreach (var agent in allConflictAgents)
        {
            if (nextActivePaths.ContainsKey(agent)) conflictedPaths[agent] = nextActivePaths[agent];
        }

        var scenarios = new List<Dictionary<string, List<Node>>>();
        
        // Generate wait scenarios
        foreach (var conflict in contestedNodes)
        {
            var (node, step) = conflict.Key;
            var agentList = conflict.Value.ToArray();
            int n = agentList.Length;
            int k = Mathf.Max(step + 3, 5);

            for (int mask = 1; mask < (1 << n) - 1; mask++)
            {
                // var subset = Enumerable.Range(0, n).Where(i => (mask & (1 << i)) != 0).ToList();
                var subset = new List<int>();
                for (int i = 0; i < n; i++) {
                    if ((mask & (1 << i)) != 0) subset.Add(i);
                }
                if (subset.Count == 0 || subset.Count == n) continue;

                // Generate wait permutations
                //foreach (var waitCombo in GetPermutations(Enumerable.Range(1, k).ToArray(), subset.Count))
                // foreach (var waitCombo in GetPermutationsOptimized(k, subset.Count))
                foreach (var waitCombo in GetPermutationsMaxPerformance(k, subset.Count))
                {
                    var scenario = new Dictionary<string, List<Node>>();
                    for (int i = 0; i < n; i++)
                    {
                        if (nextActivePaths.ContainsKey(agentList[i])) {
                            var path = nextActivePaths[agentList[i]];
                            scenario[agentList[i]] = new List<Node>(path);
                        }
                    }
                    
                    for (int j = 0; j < subset.Count; j++)
                    {
                        int agentIdx = subset[j];
                        string agentName = agentList[agentIdx];
                        int wait = waitCombo[j];
                        if (scenario.ContainsKey(agentName)) {
                            var path = scenario[agentName];
                            // for (int w = 0; w < wait; w++) path.Insert(0, path[0]);
                            var waitPath = new List<Node>(path.Count + wait);
                            for (int w = 0; w < wait; w++) waitPath.Add(path[0]);
                            waitPath.AddRange(path);
                            scenario[agentName] = waitPath;
                        }
                    }
                    
                    if (scenario.Count == n) scenarios.Add(scenario);
                }
            }
        }

        // Generate avoidance scenarios
        var avoid = new Dictionary<string, List<Node>>();
        foreach (var contest in contestedNodes) {
            var (node, step) = contest.Key;
            foreach (var a in contest.Value) {
                if (conflictedPaths.ContainsKey(a) && conflictedPaths[a]?.Count > 0) {
                    var path = RerouteFromNode(conflictedPaths[a][0], conflictedPaths[a].Last(), new HashSet<Node> { node });
                    if (path?.Count > 0) {
                        avoid[a] = path;
                    }
                }
            }
        }
        if (avoid.Count == allConflictAgents.Count) scenarios.Add(avoid);

        // Select best scenario
        Dictionary<string, List<Node>> best = null;
        bool foundConflictFree = false;
        int bestTotalCost = int.MaxValue;
        
        foreach (var s in scenarios)
        {
            bool hasConflict = HasConflict(s);
            // int totalCost = s.Values.Sum(p => p.Count);
            int totalCost = 0;
            foreach (var path in s.Values)
                totalCost += path.Count;

            if (!hasConflict)
            {
                if (!foundConflictFree || totalCost < bestTotalCost)
                {
                    bestTotalCost = totalCost;
                    foundConflictFree = true;
                    best = s;
                }
            }
            else if (!foundConflictFree && totalCost < bestTotalCost)
            {
                bestTotalCost = totalCost;
                best = s;
            }
        }

        if (best != null)
        {
            foreach (var kvp in best)
            {
                nextActivePaths[kvp.Key] = kvp.Value;
            }
        }

        return nextActivePaths;
    }

    // Optimized implementation with better data structures
    private Dictionary<string, List<Node>> ResolveRecursive_Optimized(
        Dictionary<string, List<Node>> activePaths,
        List<KeyValuePair<(Node, int), List<string>>> contestedNodes)
    {
        var nextActivePaths = new Dictionary<string, List<Node>>(activePaths);
        
        if (contestedNodes.Count == 0) return nextActivePaths;

        var allConflictAgents = new HashSet<string>();
        foreach (var conflict in contestedNodes)
        {
            foreach (var agent in conflict.Value)
            {
                allConflictAgents.Add(agent);
            }
        }

        if (allConflictAgents.Count == 0) return nextActivePaths;

        var conflictedPaths = new Dictionary<string, List<Node>>();
        foreach (var agent in allConflictAgents)
        {
            if (nextActivePaths.ContainsKey(agent))
                conflictedPaths[agent] = nextActivePaths[agent];
        }

        var scenarios = new List<Dictionary<string, List<Node>>>();
        
        // Generate scenarios more efficiently
        foreach (var conflict in contestedNodes)
        {
            var (node, step) = conflict.Key;
            var agentList = conflict.Value.ToArray();
            int n = agentList.Length;
            int k = Mathf.Max(step + 3, 5);

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

                // Generate wait scenarios
                foreach (var waitCombo in GetPermutationsOptimized(k, subset.Count))
                {
                    var scenario = new Dictionary<string, List<Node>>();
                    
                    // Copy all paths
                    for (int i = 0; i < n; i++)
                    {
                        var path = nextActivePaths[agentList[i]];
                        if (path?.Count > 0)
                            scenario[agentList[i]] = new List<Node>(path);
                    }
                    
                    // Apply waits
                    for (int j = 0; j < subset.Count; j++)
                    {
                        int agentIdx = subset[j];
                        string agentName = agentList[agentIdx];
                        int wait = waitCombo[j];
                        
                        if (scenario.ContainsKey(agentName))
                        {
                            var waitPath = new List<Node>(scenario[agentName]);
                            for (int w = 0; w < wait; w++) 
                                waitPath.Insert(0, waitPath[0]);
                            scenario[agentName] = waitPath;
                        }
                    }
                    
                    if (scenario.Count == n) scenarios.Add(scenario);
                }
            }
        }

        // Generate avoidance scenarios
        var avoid = new Dictionary<string, List<Node>>();
        foreach (var contest in contestedNodes)
        {
            var (node, step) = contest.Key;
            foreach (var a in contest.Value)
            {
                if (conflictedPaths.ContainsKey(a) && conflictedPaths[a]?.Count > 0)
                {
                    var path = RerouteFromNode(conflictedPaths[a][0], conflictedPaths[a].Last(), new HashSet<Node> { node });
                    if (path?.Count > 0)
                    {
                        avoid[a] = path;
                    }
                }
            }
        }
        if (avoid.Count == allConflictAgents.Count) scenarios.Add(avoid);

        // Select best scenario
        Dictionary<string, List<Node>> best = null;
        bool foundConflictFree = false;
        int bestTotalCost = int.MaxValue;
        
        foreach (var s in scenarios)
        {
            bool hasConflict = HasConflict(s);
            int totalCost = s.Values.Sum(p => p.Count);
            if (!hasConflict)
            {
                if (!foundConflictFree || totalCost < bestTotalCost)
                {
                    bestTotalCost = totalCost;
                    foundConflictFree = true;
                    best = s;
                }
            }
            else if (!foundConflictFree && totalCost < bestTotalCost)
            {
                bestTotalCost = totalCost;
                best = s;
            }
        }

        if (best != null)
        {
            foreach (var kvp in best)
            {
                nextActivePaths[kvp.Key] = kvp.Value;
            }
        }

        return nextActivePaths;
    }

    // Maximum performance implementation
    private Dictionary<string, List<Node>> ResolveRecursive_MaxPerformance(
        Dictionary<string, List<Node>> activePaths,
        List<KeyValuePair<(Node, int), List<string>>> contestedNodes)
    {
        var nextActivePaths = new Dictionary<string, List<Node>>(activePaths);
        
        if (contestedNodes.Count == 0) return nextActivePaths;

        // Pre-allocate collections
        var allConflictAgents = new HashSet<string>(16);
        var conflictedPaths = new Dictionary<string, List<Node>>(16);
        var scenarios = new List<Dictionary<string, List<Node>>>(32);
        
        // Collect conflict agents
        foreach (var conflict in contestedNodes)
        {
            foreach (var agent in conflict.Value)
            {
                allConflictAgents.Add(agent);
            }
        }

        if (allConflictAgents.Count == 0) return nextActivePaths;

        // Build conflicted paths
        foreach (var agent in allConflictAgents)
        {
            if (nextActivePaths.ContainsKey(agent))
                conflictedPaths[agent] = nextActivePaths[agent];
        }

        // Generate scenarios with minimal allocations
        foreach (var conflict in contestedNodes)
        {
            var (node, step) = conflict.Key;
            var agentList = conflict.Value.ToArray();
            int n = agentList.Length;
            int k = Mathf.Max(step + 3, 5);

            for (int mask = 1; mask < (1 << n) - 1; mask++)
            {
                var subset = new List<int>(n);
                for (int i = 0; i < n; i++)
                {
                    if ((mask & (1 << i)) != 0)
                        subset.Add(i);
                }
                
                if (subset.Count == 0 || subset.Count == n) continue;

                foreach (var waitCombo in GetPermutationsMaxPerformance(k, subset.Count))
                {
                    var scenario = new Dictionary<string, List<Node>>(n);
                    
                    // Copy paths efficiently
                    for (int i = 0; i < n; i++)
                    {
                        var path = nextActivePaths[agentList[i]];
                        if (path?.Count > 0)
                        {
                            var newPath = new List<Node>(path.Count);
                            foreach (var nd in path)
                                newPath.Add(nd);
                            scenario[agentList[i]] = newPath;
                        }
                    }
                    
                    // Apply waits efficiently
                    for (int j = 0; j < subset.Count; j++)
                    {
                        int agentIdx = subset[j];
                        string agentName = agentList[agentIdx];
                        int wait = waitCombo[j];
                        
                        if (scenario.ContainsKey(agentName))
                        {
                            var path = scenario[agentName];
                            var waitPath = new List<Node>(path.Count + wait);
                            for (int w = 0; w < wait; w++) 
                                waitPath.Add(path[0]);
                            waitPath.AddRange(path);
                            scenario[agentName] = waitPath;
                        }
                    }
                    
                    if (scenario.Count == n) scenarios.Add(scenario);
                }
            }
        }

        // Generate avoidance scenarios
        var avoid = new Dictionary<string, List<Node>>(allConflictAgents.Count);
        foreach (var contest in contestedNodes)
        {
            var (node, step) = contest.Key;
            foreach (var a in contest.Value)
            {
                if (conflictedPaths.ContainsKey(a) && conflictedPaths[a]?.Count > 0)
                {
                    var path = RerouteFromNode(conflictedPaths[a][0], conflictedPaths[a].Last(), new HashSet<Node> { node });
                    if (path?.Count > 0)
                    {
                        avoid[a] = path;
                    }
                }
            }
        }
        if (avoid.Count == allConflictAgents.Count) scenarios.Add(avoid);

        // Select best scenario efficiently
        Dictionary<string, List<Node>> best = null;
        bool foundConflictFree = false;
        int bestTotalCost = int.MaxValue;
        
        foreach (var s in scenarios)
        {
            bool hasConflict = HasConflict(s);
            int totalCost = 0;
            foreach (var path in s.Values)
                totalCost += path.Count;
                
            if (!hasConflict)
            {
                if (!foundConflictFree || totalCost < bestTotalCost)
                {
                    bestTotalCost = totalCost;
                    foundConflictFree = true;
                    best = s;
                }
            }
            else if (!foundConflictFree && totalCost < bestTotalCost)
            {
                bestTotalCost = totalCost;
                best = s;
            }
        }

        if (best != null)
        {
            foreach (var kvp in best)
            {
                nextActivePaths[kvp.Key] = kvp.Value;
            }
        }

        return nextActivePaths;
    }

    // One-liner functional implementation
    private Dictionary<string, List<Node>> ResolveRecursive_OneLiner(
        Dictionary<string, List<Node>> activePaths,
        List<KeyValuePair<(Node, int), List<string>>> contestedNodes)
    {
        if (contestedNodes.Count == 0) return activePaths;

        var allConflictAgents = contestedNodes.SelectMany(c => c.Value).Distinct().ToList();
        if (allConflictAgents.Count == 0) return activePaths;

        var conflictedPaths = allConflictAgents.Where(a => activePaths.ContainsKey(a))
                                              .ToDictionary(a => a, a => activePaths[a]);

        var scenarios = contestedNodes.SelectMany(conflict =>
        {
            var (node, step) = conflict.Key;
            var agentList = conflict.Value.ToList();
            int n = agentList.Count;
            int k = Mathf.Max(step + 3, 5);

            return Enumerable.Range(1, (1 << n) - 1)
                           .Where(mask => {
                               var subset = Enumerable.Range(0, n).Where(i => (mask & (1 << i)) != 0).ToList();
                               return subset.Count > 0 && subset.Count < n;
                           })
                           .SelectMany(mask => {
                               var subset = Enumerable.Range(0, n).Where(i => (mask & (1 << i)) != 0).ToList();
                               return GetPermutations(Enumerable.Range(1, k).ToArray(), subset.Count)
                                      .Select(waitCombo => {
                                          var scenario = agentList.ToDictionary(a => a, a => new List<Node>(conflictedPaths[a]));
                                          for (int j = 0; j < subset.Count; j++)
                                          {
                                              int agentIdx = subset[j];
                                              string agentName = agentList[agentIdx];
                                              int wait = waitCombo[j];
                                              var path = scenario[agentName];
                                              for (int w = 0; w < wait; w++) path.Insert(0, path[0]);
                                          }
                                          return scenario;
                                      });
                           });
        }).ToList();

        // Add avoidance scenarios
        var avoid = contestedNodes.SelectMany(contest => {
            var (node, step) = contest.Key;
            return contest.Value.Where(a => conflictedPaths.ContainsKey(a))
                              .Select(a => new { Agent = a, Path = RerouteFromNode(conflictedPaths[a][0], conflictedPaths[a].Last(), new HashSet<Node> { node }) })
                              .Where(x => x.Path?.Count > 0);
        })
        .GroupBy(x => x.Agent)
        .ToDictionary(g => g.Key, g => g.First().Path);

        if (avoid.Count == allConflictAgents.Count) scenarios.Add(avoid);

        // Select best scenario
        var best = scenarios.OrderBy(s => HasConflict(s))
                           .ThenBy(s => s.Values.Sum(p => p.Count))
                           .FirstOrDefault();

        if (best != null)
        {
            var result = new Dictionary<string, List<Node>>(activePaths);
            foreach (var kvp in best)
            {
                result[kvp.Key] = kvp.Value;
            }
            return result;
        }

        return activePaths;
    }

    // Functional programming approach
    private Dictionary<string, List<Node>> ResolveRecursive_Functional(
        Dictionary<string, List<Node>> activePaths,
        List<KeyValuePair<(Node, int), List<string>>> contestedNodes)
    {
        if (contestedNodes.Count == 0) return activePaths;

        var allConflictAgents = contestedNodes.SelectMany(c => c.Value).Distinct().ToList();
        if (allConflictAgents.Count == 0) return activePaths;

        var conflictedPaths = allConflictAgents.Where(a => activePaths.ContainsKey(a))
                                              .ToDictionary(a => a, a => activePaths[a]);

        // Generate all possible scenarios using functional composition
        var scenarios = contestedNodes.SelectMany(conflict => GenerateScenarios(conflict, conflictedPaths))
                                    .Concat(GenerateAvoidanceScenarios(contestedNodes, conflictedPaths))
                                    .ToList();

        // Select optimal solution using functional approach
        var best = scenarios.Where(s => s.Count == allConflictAgents.Count)
                           .OrderBy(s => HasConflict(s))
                           .ThenBy(s => s.Values.Sum(p => p.Count))
                           .FirstOrDefault();

        if (best != null)
        {
            var result = new Dictionary<string, List<Node>>(activePaths);
            foreach (var kvp in best)
            {
                result[kvp.Key] = kvp.Value;
            }
            return result;
        }

        return activePaths;
    }

    #endregion

    #region Helper Methods

    private IEnumerable<Dictionary<string, List<Node>>> GenerateScenarios(
        KeyValuePair<(Node, int), List<string>> conflict,
        Dictionary<string, List<Node>> conflictedPaths)
    {
        var (node, step) = conflict.Key;
        var agentList = conflict.Value.ToList();
        int n = agentList.Count;
        int k = Mathf.Max(step + 3, 5);

        return Enumerable.Range(1, (1 << n) - 1)
                       .Where(mask => {
                           var subset = Enumerable.Range(0, n).Where(i => (mask & (1 << i)) != 0).ToList();
                           return subset.Count > 0 && subset.Count < n;
                       })
                       .SelectMany(mask => {
                           var subset = Enumerable.Range(0, n).Where(i => (mask & (1 << i)) != 0).ToList();
                           return GetPermutations(Enumerable.Range(1, k).ToArray(), subset.Count)
                                  .Select(waitCombo => ApplyWaitScenario(agentList, conflictedPaths, subset, waitCombo));
                       });
    }

    private Dictionary<string, List<Node>> ApplyWaitScenario(
        List<string> agentList,
        Dictionary<string, List<Node>> conflictedPaths,
        List<int> subset,
        int[] waitCombo)
    {
        var scenario = agentList.ToDictionary(a => a, a => new List<Node>(conflictedPaths[a]));
        
        for (int j = 0; j < subset.Count; j++)
        {
            int agentIdx = subset[j];
            string agentName = agentList[agentIdx];
            int wait = waitCombo[j];
            var path = scenario[agentName];
            for (int w = 0; w < wait; w++) path.Insert(0, path[0]);
        }
        
        return scenario;
    }

    private IEnumerable<Dictionary<string, List<Node>>> GenerateAvoidanceScenarios(
        List<KeyValuePair<(Node, int), List<string>>> contestedNodes,
        Dictionary<string, List<Node>> conflictedPaths)
    {
        // Use GroupBy to handle duplicate agents properly
        var avoid = contestedNodes.SelectMany(contest => {
            var (node, step) = contest.Key;
            return contest.Value.Where(a => conflictedPaths.ContainsKey(a))
                              .Select(a => new { Agent = a, Path = RerouteFromNode(conflictedPaths[a][0], conflictedPaths[a].Last(), new HashSet<Node> { node }) })
                              .Where(x => x.Path?.Count > 0);
        })
        .GroupBy(x => x.Agent)
        .ToDictionary(g => g.Key, g => g.First().Path);

        if (avoid.Count > 0)
            yield return avoid;
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
        // Simple conflict detection - check if any two agents are at the same node at the same time
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
        // Simplified rerouting - just return a direct path for testing
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
    public void PerformanceComparison_AllVariants()
    {
        var results = new Dictionary<string, long>();
        
        // Test all variants
        var variants = new Dictionary<string, System.Func<Dictionary<string, List<Node>>, List<KeyValuePair<(Node, int), List<string>>>, Dictionary<string, List<Node>>>>
        {
            ["Original"] = ResolveRecursive_Original,
            ["Optimized"] = ResolveRecursive_Optimized,
            ["MaxPerformance"] = ResolveRecursive_MaxPerformance,
            ["OneLiner"] = ResolveRecursive_OneLiner,
            ["Functional"] = ResolveRecursive_Functional
        };

        foreach (var variant in variants)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = variant.Value(testPaths, testConflicts);
            stopwatch.Stop();
            results[variant.Key] = stopwatch.ElapsedTicks;
            
            Debug.Log($"{variant.Key}: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
        }

        // Find fastest variant
        var fastest = results.OrderBy(r => r.Value).First();
        Debug.Log($"Fastest variant: {fastest.Key} ({fastest.Value} ticks)");
        
        // Verify all variants produce similar results
        var firstResult = variants.First().Value(testPaths, testConflicts);
        foreach (var variant in variants.Skip(1))
        {
            var result = variant.Value(testPaths, testConflicts);
            Assert.AreEqual(firstResult.Count, result.Count, $"{variant.Key} should produce same number of paths");
        }
    }

    [Test]
    public void ScalabilityTest_LargeDataset()
    {
        // Create larger test dataset
        var largePaths = new Dictionary<string, List<Node>>();
        var largeConflicts = new List<KeyValuePair<(Node, int), List<string>>>();
        
        var nodes = new List<Node>();
        for (int i = 0; i < 20; i++)
        {
            for (int j = 0; j < 20; j++)
            {
                nodes.Add(new Node(true, new Vector3(i, 0, j), i, j));
            }
        }

        // Create 10 agents with longer paths
        for (int i = 0; i < 10; i++)
        {
            var path = new List<Node>();
            for (int j = 0; j < 15; j++)
            {
                path.Add(nodes[i * 2 + j]);
            }
            largePaths[$"Agent{i}"] = path;
        }

        // Create conflicts
        for (int i = 0; i < 5; i++)
        {
            largeConflicts.Add(new KeyValuePair<(Node, int), List<string>>(
                (nodes[i * 4], i + 2),
                new List<string> { $"Agent{i}", $"Agent{i + 1}", $"Agent{i + 2}" }
            ));
        }

        var stopwatch = Stopwatch.StartNew();
        // var result = ResolveRecursive_MaxPerformance(largePaths, largeConflicts);
        var result = ResolveRecursive_Original(largePaths, largeConflicts);
        // var result = ResolveRecursive_Optimized(largePaths, largeConflicts);
        stopwatch.Stop();
        
        Debug.Log($"Large dataset performance: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    #endregion
} 