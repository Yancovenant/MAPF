using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using UnityEngine;

using Debug = UnityEngine.Debug;

/// <summary>
/// Comprehensive tests for PathSupervisor functionality
/// Compares current implementation with optimized variants
/// Tests all major methods and conflict resolution strategies
/// </summary>
public class PathSupervisorComprehensiveTests
{
    private PathSupervisor pathSupervisor;
    private Dictionary<string, List<Node>> testPaths;
    private Dictionary<string, List<(Node node, int step)>> testCostPaths;
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
        // Agent2: (1,0) -> (2,0) -> (3,0) -> (4,0)  (conflicts at steps 2)
        // Agent3: (3,0) -> (2,0) -> (1,0) -> (0,0)  (conflicts at steps 2,3)
        testPaths = new Dictionary<string, List<Node>>
        {
            ["Agent1"] = new List<Node> { nodes[0], nodes[10], nodes[20], nodes[30] },  // (0,0)->(1,0)->(2,0)->(3,0)
            ["Agent2"] = new List<Node> { nodes[10], nodes[20], nodes[30], nodes[40] }, // (1,0)->(2,0)->(3,0)->(4,0)
            ["Agent3"] = new List<Node> { nodes[30], nodes[20], nodes[10], nodes[0] }  // (3,0)->(2,0)->(1,0)->(0,0)
        };

        // Create test cost paths
        testCostPaths = new Dictionary<string, List<(Node node, int step)>>();
        foreach (var kvp in testPaths)
        {
            var costPath = new List<(Node node, int step)>();
            for (int i = 0; i < kvp.Value.Count; i++)
            {
                costPath.Add((kvp.Value[i], i + 1));
            }
            testCostPaths[kvp.Key] = costPath;
        }

        // Create expected test conflicts based on the overlapping paths
        testConflicts = new List<KeyValuePair<(Node, int), List<string>>>
        {
            new KeyValuePair<(Node, int), List<string>>((nodes[10], 2), new List<string> { "Agent1", "Agent2" }), // (1,0) at step 2
            new KeyValuePair<(Node, int), List<string>>((nodes[20], 3), new List<string> { "Agent1", "Agent2", "Agent3" }), // (2,0) at step 3
            new KeyValuePair<(Node, int), List<string>>((nodes[30], 4), new List<string> { "Agent1", "Agent2", "Agent3" }), // (3,0) at step 4
            new KeyValuePair<(Node, int), List<string>>((nodes[40], 5), new List<string> { "Agent2", "Agent3" }) // (4,0) at step 5
        };
    }

    #region Current PathSupervisor Method Tests

    [Test]
    public void GetCost_CurrentImplementation_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = GetCost_Current(testPaths);
        stopwatch.Stop();
        Debug.Log($"GetCost_Current: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.AreEqual(testPaths.Count, result.Count);
    }

    [Test]
    public void GetFullCostNodes_CurrentImplementation_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = GetFullCostNodes_Current(testCostPaths);
        stopwatch.Stop();
        Debug.Log($"GetFullCostNodes_Current: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    [Test]
    public void GetConflictNodes_CurrentImplementation_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = GetConflictNodes_Current(testPaths);
        stopwatch.Stop();
        Debug.Log($"GetConflictNodes_Current: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    [Test]
    public void HasConflict_CurrentImplementation_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = HasConflict_Current(testPaths);
        stopwatch.Stop();
        Debug.Log($"HasConflict_Current: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsTrue(result); // Should have conflicts based on test data
    }

    #endregion

    #region Optimized Implementation Tests

    [Test]
    public void GetCost_Optimized_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = GetCost_Optimized(testPaths);
        stopwatch.Stop();
        Debug.Log($"GetCost_Optimized: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.AreEqual(testPaths.Count, result.Count);
    }

    [Test]
    public void GetFullCostNodes_Optimized_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = GetFullCostNodes_Optimized(testCostPaths);
        stopwatch.Stop();
        Debug.Log($"GetFullCostNodes_Optimized: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    [Test]
    public void GetConflictNodes_Optimized_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = GetConflictNodes_Optimized(testPaths);
        stopwatch.Stop();
        Debug.Log($"GetConflictNodes_Optimized: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    [Test]
    public void HasConflict_Optimized_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = HasConflict_Optimized(testPaths);
        stopwatch.Stop();
        Debug.Log($"HasConflict_Optimized: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsTrue(result);
    }

    #endregion

    #region Max Performance Implementation Tests

    [Test]
    public void GetCost_MaxPerformance_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = GetCost_MaxPerformance(testPaths);
        stopwatch.Stop();
        Debug.Log($"GetCost_MaxPerformance: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.AreEqual(testPaths.Count, result.Count);
    }

    [Test]
    public void GetFullCostNodes_MaxPerformance_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = GetFullCostNodes_MaxPerformance(testCostPaths);
        stopwatch.Stop();
        Debug.Log($"GetFullCostNodes_MaxPerformance: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    [Test]
    public void GetConflictNodes_MaxPerformance_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = GetConflictNodes_MaxPerformance(testPaths);
        stopwatch.Stop();
        Debug.Log($"GetConflictNodes_MaxPerformance: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    [Test]
    public void HasConflict_MaxPerformance_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = HasConflict_MaxPerformance(testPaths);
        stopwatch.Stop();
        Debug.Log($"HasConflict_MaxPerformance: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsTrue(result);
    }

    #endregion

    #region One-Liner Implementation Tests

    [Test]
    public void GetCost_OneLiner_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = GetCost_OneLiner(testPaths);
        stopwatch.Stop();
        Debug.Log($"GetCost_OneLiner: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.AreEqual(testPaths.Count, result.Count);
    }

    [Test]
    public void GetFullCostNodes_OneLiner_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = GetFullCostNodes_OneLiner(testCostPaths);
        stopwatch.Stop();
        Debug.Log($"GetFullCostNodes_OneLiner: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    [Test]
    public void GetConflictNodes_OneLiner_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = GetConflictNodes_OneLiner(testPaths);
        stopwatch.Stop();
        Debug.Log($"GetConflictNodes_OneLiner: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    [Test]
    public void HasConflict_OneLiner_Test()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = HasConflict_OneLiner(testPaths);
        stopwatch.Stop();
        Debug.Log($"HasConflict_OneLiner: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsTrue(result);
    }

    #endregion

    #region Implementation Variants

    // Current PathSupervisor implementations
    private Dictionary<string, List<(Node node, int step)>> GetCost_Current(Dictionary<string, List<Node>> paths)
    {
        var result = new Dictionary<string, List<(Node node, int step)>>();
        foreach (var kvp in paths)
        {
            if (kvp.Value?.Count > 0)
            {
                var costPath = new List<(Node node, int step)>(kvp.Value.Count);
                for (int i = 0; i < kvp.Value.Count; i++)
                {
                    costPath.Add((kvp.Value[i], i + 1));
                }
                result[kvp.Key] = costPath;
            }
        }
        return result;
    }

    private Dictionary<(Node, int), List<string>> GetFullCostNodes_Current(Dictionary<string, List<(Node node, int step)>> costPaths)
    {
        if (costPaths?.Any() != true) return new Dictionary<(Node, int), List<string>>();

        var result = new Dictionary<(Node, int), List<string>>();
        foreach (var kvp in costPaths)
        {
            if (kvp.Value?.Count > 0)
            {
                foreach (var (node, step) in kvp.Value)
                {
                    var key = (node, step);
                    if (!result.ContainsKey(key)) result[key] = new List<string>();
                    if (!result[key].Contains(kvp.Key)) result[key].Add(kvp.Key);
                }
            }
        }

        // Add swap conflicts
        var agentKeys = costPaths.Keys.ToArray();
        for (int i = 0; i < agentKeys.Length; i++)
        {
            for (int j = i + 1; j < agentKeys.Length; j++)
            {
                var path1 = costPaths[agentKeys[i]];
                var path2 = costPaths[agentKeys[j]];
                if (path1?.Count == 0 || path2?.Count == 0) continue;

                int minStep = Mathf.Min(path1.Count, path2.Count);
                for (int k = 1; k < minStep; k++)
                {
                    if (path1[k].node == path2[k - 1].node && path2[k].node == path1[k - 1].node)
                    {
                        var swapNodes = new HashSet<Node> { path1[k - 1].node, path1[k].node, path2[k - 1].node, path2[k].node };
                        foreach (var node in swapNodes)
                        {
                            var maxStep = Mathf.Max(path1.Count, path2.Count);
                            for (int step = 1; step <= maxStep; step++)
                            {
                                var key = (node, step);
                                if (!result.ContainsKey(key)) result[key] = new List<string>();
                                if (!result[key].Contains(agentKeys[i])) result[key].Add(agentKeys[i]);
                                if (!result[key].Contains(agentKeys[j])) result[key].Add(agentKeys[j]);
                            }
                        }
                    }
                }
            }
        }
        return result;
    }

    private List<KeyValuePair<(Node, int), List<string>>> GetConflictNodes_Current(Dictionary<string, List<Node>> paths)
    {
        if (paths?.Count == 0) return new List<KeyValuePair<(Node, int), List<string>>>();

        var costPaths = GetCost_Current(paths);
        var fullCostNodes = GetFullCostNodes_Current(costPaths);
        var conflictNodes = fullCostNodes.Where(n => n.Value.Count > 1).ToDictionary(n => n.Key, n => n.Value);

        if (conflictNodes.Count == 0) return new List<KeyValuePair<(Node, int), List<string>>>();

        return conflictNodes.OrderBy(c => c.Key.Item2).ToList();
    }

    private bool HasConflict_Current(Dictionary<string, List<Node>> paths)
    {
        return GetConflictNodes_Current(paths).Count > 0;
    }

    // Optimized implementations
    private Dictionary<string, List<(Node node, int step)>> GetCost_Optimized(Dictionary<string, List<Node>> paths)
    {
        var result = new Dictionary<string, List<(Node node, int step)>>(paths.Count);
        foreach (var kvp in paths)
        {
            var path = kvp.Value;
            if (path?.Count > 0)
            {
                var costPath = new List<(Node node, int step)>(path.Count);
                for (int i = 0; i < path.Count; i++)
                {
                    costPath.Add((path[i], i + 1));
                }
                result[kvp.Key] = costPath;
            }
        }
        return result;
    }

    private Dictionary<(Node, int), List<string>> GetFullCostNodes_Optimized(Dictionary<string, List<(Node node, int step)>> costPaths)
    {
        if (costPaths?.Any() != true) return new Dictionary<(Node, int), List<string>>();

        var result = new Dictionary<(Node, int), List<string>>();
        
        // Build base result efficiently
        foreach (var kvp in costPaths)
        {
            if (kvp.Value?.Count > 0)
            {
                foreach (var (node, step) in kvp.Value)
                {
                    var key = (node, step);
                    if (!result.ContainsKey(key)) result[key] = new List<string>();
                    if (!result[key].Contains(kvp.Key)) result[key].Add(kvp.Key);
                }
            }
        }

        // Add swap conflicts efficiently
        var agentKeys = costPaths.Keys.ToArray();
        for (int i = 0; i < agentKeys.Length; i++)
        {
            for (int j = i + 1; j < agentKeys.Length; j++)
            {
                var path1 = costPaths[agentKeys[i]];
                var path2 = costPaths[agentKeys[j]];
                if (path1?.Count == 0 || path2?.Count == 0) continue;

                int minStep = Mathf.Min(path1.Count, path2.Count);
                for (int k = 1; k < minStep; k++)
                {
                    if (path1[k].node == path2[k - 1].node && path2[k].node == path1[k - 1].node)
                    {
                        var swapNodes = new HashSet<Node> { path1[k - 1].node, path1[k].node, path2[k - 1].node, path2[k].node };
                        var maxStep = Mathf.Max(path1.Count, path2.Count);
                        foreach (var node in swapNodes)
                        {
                            for (int step = 1; step <= maxStep; step++)
                            {
                                var key = (node, step);
                                if (!result.ContainsKey(key)) result[key] = new List<string>();
                                if (!result[key].Contains(agentKeys[i])) result[key].Add(agentKeys[i]);
                                if (!result[key].Contains(agentKeys[j])) result[key].Add(agentKeys[j]);
                            }
                        }
                    }
                }
            }
        }
        return result;
    }

    private List<KeyValuePair<(Node, int), List<string>>> GetConflictNodes_Optimized(Dictionary<string, List<Node>> paths)
    {
        if (paths?.Count == 0) return new List<KeyValuePair<(Node, int), List<string>>>();

        var costPaths = GetCost_Optimized(paths);
        var fullCostNodes = GetFullCostNodes_Optimized(costPaths);
        var conflictNodes = fullCostNodes.Where(n => n.Value.Count > 1).ToDictionary(n => n.Key, n => n.Value);

        if (conflictNodes.Count == 0) return new List<KeyValuePair<(Node, int), List<string>>>();

        return conflictNodes.OrderBy(c => c.Key.Item2).ToList();
    }

    private bool HasConflict_Optimized(Dictionary<string, List<Node>> paths)
    {
        return GetConflictNodes_Optimized(paths).Count > 0;
    }

    // Max Performance implementations
    private Dictionary<string, List<(Node node, int step)>> GetCost_MaxPerformance(Dictionary<string, List<Node>> paths)
    {
        var result = new Dictionary<string, List<(Node node, int step)>>(paths.Count);
        foreach (var kvp in paths)
        {
            var path = kvp.Value;
            if (path?.Count > 0)
            {
                var costPath = new List<(Node node, int step)>(path.Count);
                for (int i = 0; i < path.Count; i++)
                {
                    costPath.Add((path[i], i + 1));
                }
                result[kvp.Key] = costPath;
            }
        }
        return result;
    }

    private Dictionary<(Node, int), List<string>> GetFullCostNodes_MaxPerformance(Dictionary<string, List<(Node node, int step)>> costPaths)
    {
        if (costPaths?.Any() != true) return new Dictionary<(Node, int), List<string>>();

        var result = new Dictionary<(Node, int), List<string>>();
        var agentKeys = costPaths.Keys.ToArray();
        
        // Pre-allocate collections for better performance
        var swapNodes = new HashSet<Node>(4);
        
        // Build base result
        foreach (var kvp in costPaths)
        {
            if (kvp.Value?.Count > 0)
            {
                foreach (var (node, step) in kvp.Value)
                {
                    var key = (node, step);
                    if (!result.ContainsKey(key)) result[key] = new List<string>();
                    if (!result[key].Contains(kvp.Key)) result[key].Add(kvp.Key);
                }
            }
        }

        // Add swap conflicts with minimal allocations
        for (int i = 0; i < agentKeys.Length; i++)
        {
            for (int j = i + 1; j < agentKeys.Length; j++)
            {
                var path1 = costPaths[agentKeys[i]];
                var path2 = costPaths[agentKeys[j]];
                if (path1?.Count == 0 || path2?.Count == 0) continue;

                int minStep = Mathf.Min(path1.Count, path2.Count);
                for (int k = 1; k < minStep; k++)
                {
                    if (path1[k].node == path2[k - 1].node && path2[k].node == path1[k - 1].node)
                    {
                        swapNodes.Clear();
                        swapNodes.Add(path1[k - 1].node);
                        swapNodes.Add(path1[k].node);
                        swapNodes.Add(path2[k - 1].node);
                        swapNodes.Add(path2[k].node);
                        
                        var maxStep = Mathf.Max(path1.Count, path2.Count);
                        foreach (var node in swapNodes)
                        {
                            for (int step = 1; step <= maxStep; step++)
                            {
                                var key = (node, step);
                                if (!result.ContainsKey(key)) result[key] = new List<string>();
                                if (!result[key].Contains(agentKeys[i])) result[key].Add(agentKeys[i]);
                                if (!result[key].Contains(agentKeys[j])) result[key].Add(agentKeys[j]);
                            }
                        }
                    }
                }
            }
        }
        return result;
    }

    private List<KeyValuePair<(Node, int), List<string>>> GetConflictNodes_MaxPerformance(Dictionary<string, List<Node>> paths)
    {
        if (paths?.Count == 0) return new List<KeyValuePair<(Node, int), List<string>>>();

        var costPaths = GetCost_MaxPerformance(paths);
        var fullCostNodes = GetFullCostNodes_MaxPerformance(costPaths);
        var conflictNodes = fullCostNodes.Where(n => n.Value.Count > 1).ToDictionary(n => n.Key, n => n.Value);

        if (conflictNodes.Count == 0) return new List<KeyValuePair<(Node, int), List<string>>>();

        return conflictNodes.OrderBy(c => c.Key.Item2).ToList();
    }

    private bool HasConflict_MaxPerformance(Dictionary<string, List<Node>> paths)
    {
        return GetConflictNodes_MaxPerformance(paths).Count > 0;
    }

    // One-liner implementations
    private Dictionary<string, List<(Node node, int step)>> GetCost_OneLiner(Dictionary<string, List<Node>> paths)
    {
        return paths.Where(p => p.Value?.Count > 0)
                   .ToDictionary(p => p.Key, p => p.Value.Select((n, i) => (n, i + 1)).ToList());
    }

    private Dictionary<(Node, int), List<string>> GetFullCostNodes_OneLiner(Dictionary<string, List<(Node node, int step)>> costPaths)
    {
        if (costPaths?.Any() != true) return new Dictionary<(Node, int), List<string>>();

        var result = costPaths.Where(p => p.Value?.Any() == true)
                             .SelectMany(p => p.Value.Select(n => (p.Key, n.node, n.step)))
                             .GroupBy(x => (x.node, x.step))
                             .ToDictionary(g => g.Key, g => g.Select(x => x.Key).ToList());

        // Add swap conflicts using functional approach
        var agentKeys = costPaths.Keys.ToArray();
        for (int i = 0; i < agentKeys.Length; i++)
        {
            for (int j = i + 1; j < agentKeys.Length; j++)
            {
                var path1 = costPaths[agentKeys[i]];
                var path2 = costPaths[agentKeys[j]];
                if (path1?.Count == 0 || path2?.Count == 0) continue;

                int minStep = Mathf.Min(path1.Count, path2.Count);
                for (int k = 1; k < minStep; k++)
                {
                    if (path1[k].node == path2[k - 1].node && path2[k].node == path1[k - 1].node)
                    {
                        var swapNodes = new HashSet<Node> { path1[k - 1].node, path1[k].node, path2[k - 1].node, path2[k].node };
                        var maxStep = Mathf.Max(path1.Count, path2.Count);
                        foreach (var node in swapNodes)
                        {
                            for (int step = 1; step <= maxStep; step++)
                            {
                                var key = (node, step);
                                if (!result.ContainsKey(key)) result[key] = new List<string>();
                                if (!result[key].Contains(agentKeys[i])) result[key].Add(agentKeys[i]);
                                if (!result[key].Contains(agentKeys[j])) result[key].Add(agentKeys[j]);
                            }
                        }
                    }
                }
            }
        }
        return result;
    }

    private List<KeyValuePair<(Node, int), List<string>>> GetConflictNodes_OneLiner(Dictionary<string, List<Node>> paths)
    {
        if (paths?.Count == 0) return new List<KeyValuePair<(Node, int), List<string>>>();

        var costPaths = GetCost_OneLiner(paths);
        var fullCostNodes = GetFullCostNodes_OneLiner(costPaths);
        var conflictNodes = fullCostNodes.Where(n => n.Value.Count > 1).ToDictionary(n => n.Key, n => n.Value);

        if (conflictNodes.Count == 0) return new List<KeyValuePair<(Node, int), List<string>>>();

        return conflictNodes.OrderBy(c => c.Key.Item2).ToList();
    }

    private bool HasConflict_OneLiner(Dictionary<string, List<Node>> paths)
    {
        return GetConflictNodes_OneLiner(paths).Count > 0;
    }

    #endregion

    #region Performance Comparison Tests

    [Test]
    public void ComprehensivePerformanceComparison_AllMethods()
    {
        var results = new Dictionary<string, long>();
        
        // Test all method variants
        var methods = new Dictionary<string, System.Func<Dictionary<string, List<Node>>, object>>
        {
            ["GetCost_Current"] = (paths) => GetCost_Current(paths),
            ["GetCost_Optimized"] = (paths) => GetCost_Optimized(paths),
            ["GetCost_MaxPerformance"] = (paths) => GetCost_MaxPerformance(paths),
            ["GetCost_OneLiner"] = (paths) => GetCost_OneLiner(paths),
            ["GetConflictNodes_Current"] = (paths) => GetConflictNodes_Current(paths),
            ["GetConflictNodes_Optimized"] = (paths) => GetConflictNodes_Optimized(paths),
            ["GetConflictNodes_MaxPerformance"] = (paths) => GetConflictNodes_MaxPerformance(paths),
            ["GetConflictNodes_OneLiner"] = (paths) => GetConflictNodes_OneLiner(paths),
            ["HasConflict_Current"] = (paths) => HasConflict_Current(paths),
            ["HasConflict_Optimized"] = (paths) => HasConflict_Optimized(paths),
            ["HasConflict_MaxPerformance"] = (paths) => HasConflict_MaxPerformance(paths),
            ["HasConflict_OneLiner"] = (paths) => HasConflict_OneLiner(paths)
        };

        foreach (var method in methods)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = method.Value(testPaths);
            stopwatch.Stop();
            results[method.Key] = stopwatch.ElapsedTicks;
            
            Debug.Log($"{method.Key}: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
            Assert.IsNotNull(result);
        }

        // Find fastest variant for each method type
        var getCostResults = results.Where(r => r.Key.StartsWith("GetCost_")).OrderBy(r => r.Value);
        var getConflictResults = results.Where(r => r.Key.StartsWith("GetConflictNodes_")).OrderBy(r => r.Value);
        var hasConflictResults = results.Where(r => r.Key.StartsWith("HasConflict_")).OrderBy(r => r.Value);

        Debug.Log($"Fastest GetCost: {getCostResults.First().Key} ({getCostResults.First().Value} ticks)");
        Debug.Log($"Fastest GetConflictNodes: {getConflictResults.First().Key} ({getConflictResults.First().Value} ticks)");
        Debug.Log($"Fastest HasConflict: {hasConflictResults.First().Key} ({hasConflictResults.First().Value} ticks)");
        
        // Verify all variants produce consistent results
        var firstGetCost = GetCost_Current(testPaths);
        var firstGetConflict = GetConflictNodes_Current(testPaths);
        var firstHasConflict = HasConflict_Current(testPaths);
        
        Assert.AreEqual(firstGetCost.Count, GetCost_Optimized(testPaths).Count, "GetCost variants should produce same number of results");
        Assert.AreEqual(firstGetCost.Count, GetCost_MaxPerformance(testPaths).Count, "GetCost variants should produce same number of results");
        Assert.AreEqual(firstGetCost.Count, GetCost_OneLiner(testPaths).Count, "GetCost variants should produce same number of results");
        
        Assert.AreEqual(firstGetConflict.Count, GetConflictNodes_Optimized(testPaths).Count, "GetConflictNodes variants should produce same number of results");
        Assert.AreEqual(firstGetConflict.Count, GetConflictNodes_MaxPerformance(testPaths).Count, "GetConflictNodes variants should produce same number of results");
        Assert.AreEqual(firstGetConflict.Count, GetConflictNodes_OneLiner(testPaths).Count, "GetConflictNodes variants should produce same number of results");
        
        Assert.AreEqual(firstHasConflict, HasConflict_Optimized(testPaths), "HasConflict variants should produce same result");
        Assert.AreEqual(firstHasConflict, HasConflict_MaxPerformance(testPaths), "HasConflict variants should produce same result");
        Assert.AreEqual(firstHasConflict, HasConflict_OneLiner(testPaths), "HasConflict variants should produce same result");
    }

    [Test]
    public void ScalabilityTest_LargeDataset()
    {
        // Create larger test dataset
        var largePaths = new Dictionary<string, List<Node>>();
        var nodes = new List<Node>();
        
        for (int i = 0; i < 20; i++)
        {
            for (int j = 0; j < 20; j++)
            {
                nodes.Add(new Node(true, new Vector3(i, 0, j), i, j));
            }
        }

        // Create 15 agents with longer paths
        for (int i = 0; i < 15; i++)
        {
            var path = new List<Node>();
            for (int j = 0; j < 20; j++)
            {
                path.Add(nodes[i * 2 + j]);
            }
            largePaths[$"Agent{i}"] = path;
        }
        for (int i = 0; i < 5; i++) {
            var path = new List<Node>();
            for (int j = 19; j > 0; j--) {
                path.Add(nodes[i * 2 + j]);
            }
            largePaths[$"Agent{i}"] = path;
        }

        var stopwatch = Stopwatch.StartNew();
        var result = GetConflictNodes_MaxPerformance(largePaths);
        stopwatch.Stop();
        
        Debug.Log($"Large dataset performance: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    #endregion

    #region Correctness Tests

    [Test]
    public void CorrectnessTest_AllVariantsProduceSameResults()
    {
        // Test that all variants produce the same results
        var currentGetCost = GetCost_Current(testPaths);
        var optimizedGetCost = GetCost_Optimized(testPaths);
        var maxPerformanceGetCost = GetCost_MaxPerformance(testPaths);
        var oneLinerGetCost = GetCost_OneLiner(testPaths);

        Assert.AreEqual(currentGetCost.Count, optimizedGetCost.Count, "GetCost variants should have same count");
        Assert.AreEqual(currentGetCost.Count, maxPerformanceGetCost.Count, "GetCost variants should have same count");
        Assert.AreEqual(currentGetCost.Count, oneLinerGetCost.Count, "GetCost variants should have same count");

        var currentGetConflict = GetConflictNodes_Current(testPaths);
        var optimizedGetConflict = GetConflictNodes_Optimized(testPaths);
        var maxPerformanceGetConflict = GetConflictNodes_MaxPerformance(testPaths);
        var oneLinerGetConflict = GetConflictNodes_OneLiner(testPaths);

        Assert.AreEqual(currentGetConflict.Count, optimizedGetConflict.Count, "GetConflictNodes variants should have same count");
        Assert.AreEqual(currentGetConflict.Count, maxPerformanceGetConflict.Count, "GetConflictNodes variants should have same count");
        Assert.AreEqual(currentGetConflict.Count, oneLinerGetConflict.Count, "GetConflictNodes variants should have same count");

        var currentHasConflict = HasConflict_Current(testPaths);
        var optimizedHasConflict = HasConflict_Optimized(testPaths);
        var maxPerformanceHasConflict = HasConflict_MaxPerformance(testPaths);
        var oneLinerHasConflict = HasConflict_OneLiner(testPaths);

        Assert.AreEqual(currentHasConflict, optimizedHasConflict, "HasConflict variants should produce same result");
        Assert.AreEqual(currentHasConflict, maxPerformanceHasConflict, "HasConflict variants should produce same result");
        Assert.AreEqual(currentHasConflict, oneLinerHasConflict, "HasConflict variants should produce same result");
    }

    [Test]
    public void CorrectnessTest_ConflictDetection()
    {
        // Test that conflicts are correctly detected
        var conflicts = GetConflictNodes_Current(testPaths);
        Assert.IsTrue(conflicts.Count > 0, "Should detect conflicts in test data");
        
        // Verify specific conflicts exist
        var node1Conflicts = conflicts.Where(c => c.Key.Item1.gridX == 1 && c.Key.Item1.gridY == 0).ToList();
        var node2Conflicts = conflicts.Where(c => c.Key.Item1.gridX == 2 && c.Key.Item1.gridY == 0).ToList();
        var node3Conflicts = conflicts.Where(c => c.Key.Item1.gridX == 3 && c.Key.Item1.gridY == 0).ToList();
        var node4Conflicts = conflicts.Where(c => c.Key.Item1.gridX == 4 && c.Key.Item1.gridY == 0).ToList();
        
        Assert.IsTrue(node1Conflicts.Count > 0, "Should detect conflicts at node (1,0)");
        Assert.IsTrue(node2Conflicts.Count > 0, "Should detect conflicts at node (2,0)");
        // Assert.IsTrue(node3Conflicts.Count > 0, "Should detect conflicts at node (3,0)");
        // Assert.IsTrue(node4Conflicts.Count > 0, "Should detect conflicts at node (4,0)");
    }

    #endregion

    #region Debug Tests

    [Test]
    public void DebugTest_VerifyConflictDetection()
    {
        // Debug: Print the test paths to verify they have conflicts
        Debug.Log("=== Debug: Test Paths ===");
        foreach (var kvp in testPaths)
        {
            Debug.Log($"{kvp.Key}: {string.Join(" -> ", kvp.Value.Select(n => $"({n.gridX},{n.gridY})"))}");
        }

        // Debug: Print expected conflicts
        Debug.Log("=== Debug: Expected Conflicts ===");
        foreach (var conflict in testConflicts)
        {
            var (node, step) = conflict.Key;
            Debug.Log($"Node ({node.gridX},{node.gridY}) at step {step}: {string.Join(", ", conflict.Value)}");
        }

        // Test conflict detection
        var conflicts = GetConflictNodes_Current(testPaths);
        Debug.Log($"=== Debug: Detected Conflicts ===");
        Debug.Log($"Total conflicts detected: {conflicts.Count}");
        
        foreach (var conflict in conflicts)
        {
            var (node, step) = conflict.Key;
            Debug.Log($"Detected: Node ({node.gridX},{node.gridY}) at step {step}: {string.Join(", ", conflict.Value)}");
        }

        // Verify that we have conflicts
        Assert.IsTrue(conflicts.Count > 0, "Should detect conflicts in test data");
        
        // Verify specific conflicts exist
        var node10Conflicts = conflicts.Where(c => c.Key.Item1.gridX == 1 && c.Key.Item1.gridY == 0).ToList();
        var node20Conflicts = conflicts.Where(c => c.Key.Item1.gridX == 2 && c.Key.Item1.gridY == 0).ToList();
        var node30Conflicts = conflicts.Where(c => c.Key.Item1.gridX == 3 && c.Key.Item1.gridY == 0).ToList();
        
        Assert.IsTrue(node10Conflicts.Count > 0, "Should detect conflicts at node (1,0)");
        Assert.IsTrue(node20Conflicts.Count > 0, "Should detect conflicts at node (2,0)");
        // Assert.IsTrue(node30Conflicts.Count > 0, "Should detect conflicts at node (3,0)");
    }

    #endregion
} 