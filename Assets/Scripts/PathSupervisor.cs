/**
* PathSupervisor.cs
* This script is responsible for managing the path planning and execution,
* and coordinating between agents.
*/

/**
* <summary>
* <c>Scripts/PathSupervisor.cs</c>
* This script is responsible for managing the path planning and execution,
* and coordinating between agents.
* </summary>
*/

using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine.Assertions;


public class PathSupervisor : MonoBehaviour {
    public static PathSupervisor Instance { get; private set; }

    public AUGV[] agents;
    // public CameraCapture[] cameraCaptures;
    public Color[] agentColors;
    
    private GridManager grid;

    public Dictionary<string, List<Node>> activePaths = new Dictionary<string, List<Node>>();
    public Dictionary<string, Queue<Vector3>> agentWaypoints = new();

    // Used for lockstep simulation
    private HashSet<string> activeAgents = new();
    private bool lockstep = false;
    private int globalStepIndex = 0;

    // used for obstacle and dynamic obstacle detection.
    private float last_update_time = 0;
    private Dictionary<Node, float> yoloObstacles = new();
    private HashSet<Node> occupiedNodes = new();
    
    // For each agent, map Node to a count of unique detections
    private Dictionary<string, Dictionary<Node, int>> agentObstacleCounts = new();
    private const int DETECTION_CONFIRM_FRAMES = 5; // Number of frames to confirm
    private const float NODE_POSITION_THRESHOLD = 0.2f; // World distance to consider same node
    private Dictionary<string, float> agentStopList = new();

    IEnumerator Start() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            yield break;
        }
        Instance = this;

        var mapGen = FindAnyObjectByType<MapGenerator>();
        grid = FindAnyObjectByType<GridManager>();
        var spawner = FindAnyObjectByType<AUGVSpawner>();

        if (!mapGen || !grid || !spawner) {
            Debug.LogError("PathSupervisor: MapGenerator, GridManager, or AUGVSpawner not found");
            yield break;
        }

        mapGen.GenerateMap(); yield return null;
        grid.CreateGrid(); yield return null;
        spawner.SpawnAgents(); yield return null;

        agents = FindObjectsByType<AUGV>(FindObjectsSortMode.None);
        // cameraCaptures = FindObjectsByType<CameraCapture>(FindObjectsSortMode.None);

        if (GlobalConfig.Instance != null) {
            foreach (var agent in agents) {
                if (!GlobalConfig.Instance.agentYoloConfigs.Exists(a => a.agentId == agent.name)) {
                    GlobalConfig.Instance.agentYoloConfigs.Add(new GlobalConfig.AgentYoloConfig {
                        agentId = agent.name,
                        useYolo = false
                    });
                }
            }
        }
    }

    public void AssignRouteFromJSON(Dictionary<string, object> parsed) {
        foreach (var (agentId, waypoints) in parsed) {
            if (string.IsNullOrEmpty(agentId) ||
                !(waypoints is List<object>wpList)) continue;

            agentWaypoints[agentId] = new Queue<Vector3>(
                wpList.Select(w => GameObject.Find(w.ToString()))
                      .Where(obj => obj != null)
                      .Select(obj => obj.transform.position));

            _assignNewPathsToIdleAgents();
        }
    }

    public void AssignObstacleFromJSON(Dictionary<string, object> parsed) {
        if (!parsed.TryGetValue("agent_id", out var agentId) ||
            !parsed.TryGetValue("feet", out var feetList) ||
            !(feetList is List<object> pixelList)) return;
        
        var agent = agents.FirstOrDefault(a => a.name == agentId.ToString());
        if (agent == null) return;

        var cam = agent.GetComponentInChildren<CameraCapture>();
        if (cam == null) return;

        _processAgentDetections(agent, cam, pixelList);

        // if (agent != null) {
        //     float currentTime = Time.time;

        //     foreach (var off in offsetLists.OfType<List<object>>()) {
        //         if (off.Count < 2) continue;
        //         if (int.TryParse(off[0].ToString(), out var dx) &&
        //             int.TryParse(off[1].ToString(), out var dy)) {
        //                 if (dx == 0 && dy == 0) continue;
                    
        //             Vector3 worldPosition = agent.transform.position +
        //                 agent.transform.forward * dy +
        //                 agent.transform.right * dx;
        //             Debug.Log($"PathSupervisor: Offset {dx}, {dy}");
        //             Node node = grid.NodeFromWorldPoint(worldPosition);
        //             Node agentNode = grid.NodeFromWorldPoint(agent.transform.position);
        //             if (node != null && // if the node is not null and has a value.
        //                 node != agentNode && // if the node is not the same as the agent node.
        //                 !yoloObstacles.ContainsKey(node) && // if the node is not in the yoloObstacles dictionary.
        //                 node.walkable && // if the node is walkable.
        //                 currentTime - last_update_time > 0.5f) { // debounce the update.
        //                     yoloObstacles[node] = currentTime;
        //                     last_update_time = currentTime;
        //                     node.walkable = false; // to make sure any new path find will not use this node.
        //                     Debug.Log($"PathSupervisor: Agent {agent.name} detected obstacle at {node.worldPosition.x}, {node.worldPosition.z}");
        //                 }
        //         }
        //     }
        // }
    }

    private void _processAgentDetections(AUGV agent, CameraCapture cam, List<object> pixelList) {
        if (agent == null || cam == null || pixelList == null) return;
        float now = Time.time;
        float camHeight = GlobalConfig.Instance.resolutionHeight;

        // Local for each agent.
        var localNodeCounts = new Dictionary<Node, int>();
        var localDetectedPositions = new List<Vector3>();

        foreach (var item in pixelList) {
            if (!(item is List<object> coords) || coords.Count < 2) continue;
            if (!float.TryParse(coords[0].ToString(), out float feet_x)) continue;
            if (!float.TryParse(coords[1].ToString(), out float feet_y)) continue;
            feet_y = camHeight - feet_y;

            Ray ray = cam.cam.ScreenPointToRay(new Vector3(feet_x, feet_y, 0));
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, LayerMask.GetMask("Road"))) {

                // DEBUG AREA
                Debug.DrawRay(ray.origin, ray.direction * hit.distance, Color.blue, 2f);
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.transform.position = hit.point + Vector3.up * 0.1f; // Slightly above ground
                marker.transform.localScale = Vector3.one * 0.2f;
                GameObject.Destroy(marker, 2f);
                // DEBUG AREA

                // Convert the hit point because of our grid is not aligned with the world.
                Vector3 trueGridHitpoint = new Vector3(
                    (hit.point.x - grid.transform.position.x) / grid.nodeDiameter,
                    0,
                    (hit.point.z - grid.transform.position.z) / grid.nodeDiameter
                );
                Node node = grid.NodeFromWorldPoint(trueGridHitpoint);
                if (node == null) continue;
                Node agentNode = grid.NodeFromWorldPoint(agent.transform.position);
                if (node.walkable == false || node == agentNode || yoloObstacles.ContainsKey(node)) continue;
                localDetectedPositions.Add(node.worldPosition);
            }
        }
    
        var localUniqueNodes = new List<Node>();
        foreach (var pos in localDetectedPositions) {
            if (!localUniqueNodes.Any(n => Vector3.Distance(n.worldPosition, pos) < NODE_POSITION_THRESHOLD)) {
                Node node = grid.NodeFromWorldPoint(pos);
                localUniqueNodes.Add(node);
            }
        }
        lock (agentObstacleCounts) {
            if (!agentObstacleCounts.ContainsKey(agent.name))
                agentObstacleCounts[agent.name] = new Dictionary<Node, int>();
            var nodeCounts = agentObstacleCounts[agent.name];

            foreach (var node in localUniqueNodes) {
                Node existing = nodeCounts.Keys.FirstOrDefault(n => Vector3.Distance(n.worldPosition, node.worldPosition) < NODE_POSITION_THRESHOLD);
                if (existing == null || existing != node) existing = node;
                if (!nodeCounts.ContainsKey(existing)) nodeCounts[existing] = 0;
                nodeCounts[existing] += 1;
                if (nodeCounts[existing] > DETECTION_CONFIRM_FRAMES) {
                    lock (yoloObstacles) {
                        if (!yoloObstacles.ContainsKey(existing)) {
                            yoloObstacles[existing] = now;
                            if (existing != null) existing.walkable = false;
                        }
                    }
                    lock (agentStopList) {
                        if (agentStopList.ContainsKey(agent.name)) {
                            agentStopList.Remove(agent.name);
                        }
                    }
                } else {
                    lock (agentStopList) {
                        if (!agentStopList.ContainsKey(agent.name)) {
                            agentStopList[agent.name] = now;
                        }
                    }
                }
            }
        }
    }

    void Update() {
        _updateOccupiedNodes();

        // Only send images for agents with YOLO enabled
        foreach (var agent in agents) {
            var camCap = agent.GetComponentInChildren<CameraCapture>();
            if (camCap != null && GlobalConfig.Instance != null) {
                camCap.TrySendImageAndResponse();
            }
        }

        var waitingAdvance = agents.Where(a => a.State == AUGV.AgentState.WaitingForStep).ToArray();

        // ? This is at the start where no lockstep is set to true.
        // ? And path is assigned to each agent.
        // ? then before we start moving, we need to try resolve conflict.
        // ? and make the agent active (because they are contributing to the environment)
        if (!lockstep && waitingAdvance.All(a => a.IsReadyToStep()) && waitingAdvance.Length > 0) {
            lockstep = true;
            activeAgents.Clear();
            foreach (var a in waitingAdvance) activeAgents.Add(a.name);
            _resolveContestedNodes();
        }

        // ? Once the lockstep is set, we need to advance the agents.
        // ? and make sure to keep doing the advance in lockstep.
        if (lockstep && waitingAdvance.Length == activeAgents.Count && waitingAdvance.Length > 0) {
            globalStepIndex++;
            _resolveContestedNodes();
            activeAgents.Clear();
            foreach (var ag in waitingAdvance) {
                if (!activePaths.TryGetValue(ag.name, out var path) || path == null || path.Count == 0) continue;
                var occupied = occupiedNodes.Except(new[] { path.Last()}).ToHashSet();

                // TODO: Avoid using Linq, multiple times.
                // TODO: fix it for performance.
                // @PARAM: occupied => occupiedNodes -> except the path.last() of this agent.
                // @PARAM: occupiedNodes => all the occupied nodes in the environment.
                bool hasOccupied = false;
                for (int i = 1; i < path.Count; i++) {
                    if (occupied.Contains(path[i])) {
                        hasOccupied = true;
                        break;
                    }
                }
                if (hasOccupied) {
                    var newPath = _rerouteFromNode(path[0], path.Last(), occupied);
                    if (newPath != null && newPath.Count > 0) {
                        activePaths[ag.name] = newPath;
                        ag.PathChanged();
                    }
                }
                bool hasOccupiedNodes = false;
                if (path.Count > 2) {
                    for (int i = 1; i < path.Count; i++) {
                        if (occupiedNodes.Contains(path[i])) {
                            hasOccupiedNodes = true;
                            break;
                        }
                    }
                }
                if (hasOccupiedNodes) {
                    ag.State = AUGV.AgentState.WaitingForStep;
                    activeAgents.Add(ag.name);
                    continue;
                }
                // if (path.Skip(1).Any(n => occupied.Contains(n))) {
                //     var newPath = _rerouteFromNode(path[0], path.Last(), occupied);
                //     if (newPath != null && newPath.Count > 0) {
                //         activePaths[ag.name] = newPath;
                //         ag.PathChanged();
                //     }
                // } else if (path.Skip(1).Any(n => occupiedNodes.Contains(n) && path.Count > 2)) {
                //     // ? this is to make sure that any occupied node that is,
                //     // ? also the last node of the agent, will be waiting for step.
                //     // ? above is the case where it doesnt include the agent own end node.
                //     ag.State = AUGV.AgentState.WaitingForStep;
                //     activeAgents.Add(ag.name);
                //     // Debug.Log($"_test: the agent is set to waiting for step if any occupied nodes is in the path more than 2");
                //     continue;
                // }

                // PURPOSE: Package the code smaller.
                // TODO: combine both statement into one.
                // TODO: but still keep it in order, to avoid any bug,
                // TODO: or bug changing the Dictionary in the middle of the code.
                if (agentStopList.ContainsKey(ag.name) && Time.time - agentStopList[ag.name] > .5f) {
                    // Debug.Log($"_test: on update, now removing the agent from the agent stop list after sometime (0.5f)");
                    agentStopList.Remove(ag.name);
                }
                if (agentStopList.ContainsKey(ag.name)) {
                    ag.State = AUGV.AgentState.WaitingForStep;
                    activeAgents.Add(ag.name);
                    // Debug.Log($"_test: the agent is set to waiting for step if it is in the agent stop list");
                    continue;
                } else {
                    ag.Advance();
                }
            }
        }
        _trimPaths();
    }

    private void _trimPaths() {
        foreach (var a in agents) {
            if (activePaths.TryGetValue(a.name, out var path) && path != null && path.Count > 0) {
                float closestDist = float.MaxValue;
                int closestIndex = -1;
                var agentPos = a.transform.position;
                for (int i = 0; i < path.Count; i++) {
                    float dist = Vector3.Distance(
                        new Vector3(path[i].worldPosition.x, agentPos.y, path[i].worldPosition.z),
                        agentPos
                    );
                    if (dist < closestDist) {
                        closestDist = dist;
                        closestIndex = i;
                    }
                }
                if (closestDist < .1f && closestIndex > 0) {
                    activePaths[a.name] = path.GetRange(closestIndex, path.Count - closestIndex);
                }
                if (path.Count == 0) path.Clear();
                if (path.Count == 0) activePaths.Remove(a.name);
            }
        }
    }

    // ** Dynamic Obstacle, Occupied Nodes **
    // =========================================
    private void _updateOccupiedNodes() {
        occupiedNodes.Clear();
        foreach (var a in agents) {
            if (activePaths.TryGetValue(a.name, out var path) && path != null && path.Count > 0) {
                float dist = Vector3.Distance(
                    new Vector3(a.transform.position.x, 0, a.transform.position.z),
                    path.Last().worldPosition
                );
                if (dist <= 1f) {
                    occupiedNodes.Add(path.Last());
                    path.Last().walkable = false;
                } else {
                    foreach (var n in path) {
                        // fixed the bug where loop happen within small gap,
                        // that the yolo obstacle is being set to walkable,
                        // thus all the rest of this file method will goes on a loop.
                        // and all the agent will be stucked in a deadlock.
                        if (!yoloObstacles.ContainsKey(n)) n.walkable = true;
                    }
                }
            }
        }

        // ? This is to remove the expired yolo obstacles after timeout.
        float currentTime = Time.time;
        foreach (var expired in yoloObstacles
            .Where(t => currentTime - t.Value > GlobalConfig.Instance.YOLO_OBSTACLE_TIMEOUT)
            .Select(t => t.Key)
            .ToList()) {
                yoloObstacles.Remove(expired);
                expired.walkable = true;
            }

        occupiedNodes.UnionWith(yoloObstacles.Keys);
    }

    #region Conflict Resolve

    // ** CONFLICT LOGIC + RESOLUTION **
    // Optimized for performance.
    // =========================================

    private void _resolveContestedNodes() => __resolveRecursive(0);

    private void __resolveRecursive(int depth, int maxDepth = 10) {
        if (depth >= maxDepth) return;
        
        var contestedNodes = _getConflictNodes(activePaths);
        if (contestedNodes.Count == 0) return;
        
        var nextActivePaths = new Dictionary<string, List<Node>>(activePaths);
        
        // var allConflictAgents = contestedNodes.SelectMany(c => c.Value).Distinct().ToList();
        var allConflictAgents = new HashSet<string>();
        foreach (var conflict in contestedNodes) {
            foreach (var agent in conflict.Value) {
                allConflictAgents.Add(agent);
            }
        }
        if (allConflictAgents.Count == 0) return;

        var conflictedPaths = new Dictionary<string, List<Node>>();
        foreach (var agent in allConflictAgents) if (nextActivePaths.ContainsKey(agent)) conflictedPaths[agent] = nextActivePaths[agent];
        
        var scenarios = new List<Dictionary<string, List<Node>>>();
        
        foreach (var conflict in contestedNodes) {
            var (node, step) = conflict.Key;
            var agentList = conflict.Value.ToArray();
            int n = agentList.Length; // agent how many
            int k = Mathf.Max(step + 3, 5); // maximum step used for wait permutations.

            // DEBUG AREA
            // Debug.Log($"_test active paths: {node.worldPosition}");
            // Debug.Log($"_test next active : {string.Join(", ", nextActivePaths.SelectMany(p => p.Key + " " + string.Join(", ", p.Value.Select(n => n.worldPosition.ToString())).ToArray()))}");
            // foreach (var kvp in nextActivePaths) {
            //     if (kvp.Key == "AUGV_2" || kvp.Key == "AUGV_5") {
            //         Debug.Log($"_test next active : {kvp.Key} {string.Join(", ", kvp.Value.Select(n => n.worldPosition.ToString()).ToArray())}");
            //     }
            // }
            Debug.DrawRay(node.worldPosition, Vector3.up * 100f, Color.red, 2f);
            // if (!nextActivePaths.Select(p => p.Value.Any(n => n.walkable)).Any()) return;
            // DEBUG AREA

            // ? mask is the subset of agents that will wait.
            // ? where mask < (1 left shift n) - 1;
            // ? and n is the number of agents in the conflict.
            // ? so it will produce (e.g =>)  
            for (int mask = 1; mask < (1 << n) - 1; mask++) {
                // var subset = Enumerable.Range(1, n).Where(i => (mask & (1 << i)) != 0).ToList();
                var subset = new List<int>();
                for (int i = 0; i < n; i++) {
                    if ((mask & (1 << i)) != 0) subset.Add(i);
                }
                if (subset.Count == 0 || subset.Count == n) continue; // non empty subset except

                /** <summary>
                // proceed at your own risk.
                // if your device resouces is high enough to calculate,
                // combinatorial permutations.
                // it will give's the best result. but at the cost of performance.
                // some time this code only could run for about 100k permutations.
                // </summary> 
                */
                bool highLevelResolve = true;
                if (highLevelResolve) {
                    foreach (var waitCombo in __getPermutations(k, subset.Count)) {
                        var s = new Dictionary<string, List<Node>>();
                        for (int i = 0; i < n; i++) {
                            if (nextActivePaths.ContainsKey(agentList[i])) {
                                var path = nextActivePaths[agentList[i]];
                                s[agentList[i]] = new List<Node>(path);
                            }
                        }
                        for (int j = 0; j < subset.Count; j++) {
                            int agentIdx = subset[j];
                            string agentName = agentList[agentIdx];
                            int wait = waitCombo[j];
                            if (s.ContainsKey(agentName)) {
                                var path = s[agentName];
                                // i think having it pre-allocated is better for performance.
                                var waitPath = new List<Node>(path.Count + wait);
                                for (int w = 0; w < wait; w++) waitPath.Add(path[0]);
                                waitPath.AddRange(path);
                                s[agentName] = waitPath;
                            }
                        }
                        Debug.Log($"test__waitCombo: {s.Count}, {n}");
                        if (s.Count == n) scenarios.Add(s);
                    }
                } else {
                    /** <summary>
                    * else we only want to wait for the time till conflict.
                    * </summary>
                    */
                    int timeTillConflict = step + 1;
                    var s = new Dictionary<string, List<Node>>();
                    for (int i = 0; i < subset.Count; i++) {
                        int agentIdx = subset[i];
                        string agentName = agentList[agentIdx];
                        var path = nextActivePaths[agentName];
                        if (path?.Count > 0) {
                            var waitPath = new List<Node>(path);
                            for (int w = 0; w < timeTillConflict; w++) waitPath.Insert(0, waitPath[0]);
                            s[agentName] = waitPath;
                        }
                    }
                    if (s.Count == subset.Count) scenarios.Add(s);
                }
                
            }
        }

        var avoid = new Dictionary<string, List<Node>>();
        foreach (var contest in contestedNodes) {
            var (node, step) = contest.Key;
            foreach (var a in contest.Value) {
                if (conflictedPaths[a] == null || conflictedPaths[a].Count == 0) continue;
                var path = _rerouteFromNode(conflictedPaths[a][0], conflictedPaths[a].Last(), new HashSet<Node> { node });
                if (path != null && path.Count > 0) {
                    avoid[a] = path;
                }
            }
        }
        if (avoid.Count == allConflictAgents.Count) scenarios.Add(avoid);

        var oneAllowed = new Dictionary<string, List<Node>>();
        foreach (var contest in contestedNodes) {
            var (node, step) = contest.Key;
            var agentList = contest.Value.ToArray();
            System.Array.Sort(agentList);
            for (int i = 1; i < agentList.Length; i++) {
                var agent = agentList[i];
                if (conflictedPaths.ContainsKey(agent)) {
                    var path = conflictedPaths[agent];
                    if (path?.Count > 0) {
                        var newPath = _rerouteFromNode(path[0], path.Last(), new HashSet<Node> { node });
                        if (newPath?.Count > 0) {
                            oneAllowed[agent] = newPath;
                        }
                    }
                }
            }
        }
        if (oneAllowed.Count == allConflictAgents.Count) scenarios.Add(oneAllowed);

        Debug.Log($"test__scenarios: {scenarios.Count} for total conflict {contestedNodes.Count} at depth {depth}");
        Dictionary<string, List<Node>> best2 = null;
        bool foundConflictFree = false;
        int bestTotalCost = int.MaxValue;
        foreach (var s in scenarios) {
            bool hasConflict = __hasConflict(s);
            int totalCost = s.Values.Sum(p => p.Count);
            if (!hasConflict) {
                if (!foundConflictFree || totalCost < bestTotalCost) {
                    bestTotalCost = totalCost;
                    foundConflictFree = true;
                    best2 = s;
                    // Debug.Log($"resolved at {depth} path => {string.Join(", ", s.Keys.ToArray())}, {string.Join(", ", s.Values.SelectMany(p => p.Select(n => n.worldPosition.ToString())).ToArray())}");
                }
            } else if (!foundConflictFree && totalCost < bestTotalCost) {
                bestTotalCost = totalCost;
                best2 = s;
            }
        }
        if (best2 != null) {
            // DEBUG AREA
            // foreach (var kvp in best2) {
            //     var path = kvp.Value;
                // Debug.Log($"__test every of those path is : {string.Join(", ", path.Select(n => n.worldPosition.ToString()).ToArray())} agent = {kvp.Key}");
                // Debug.Log($"path before {path.Count}");
                // if (kvp.Key == "AUGV_2") Debug.Log($"path 0 = {path[0].worldPosition}, path 1 = {path[1].worldPosition}");
                // if (path.Count > 1 && path[0] == path[1]) {
                //     path.Insert(0, path[0]);
                //     Debug.Log($"_forcing new path for {kvp.Key}");
                // }
                // Debug.Log($"path after {path.Count}");
            // }
            // END DEBUG AREA
            foreach (var kvp in best2) {
                nextActivePaths[kvp.Key] = kvp.Value;
            }
            if (activePaths != nextActivePaths || activePaths.Count != nextActivePaths.Count) {
                // activePaths.Clear();
                foreach (var a in agents) {
                    a.PathChanged();
                }
            }
        }
        activePaths = nextActivePaths;
        __resolveRecursive(depth + 1);
    }

    #endregion

    #region Conflict Nodes

    public List<KeyValuePair<(Node, int), List<string>>> _getConflictNodes(Dictionary<string, List<Node>> paths) {
        if (paths?.Count == 0) return new List<KeyValuePair<(Node, int), List<string>>>();

        // Get Cost of each path
        var costPaths = __getCost(paths);

        // get full cost nodes.
        // Maximum Performance.
        var fullCostNodes = __getFullCostNodes(costPaths);

        // See if any constrained nodes.
        var conflictNodes = __getConflictNodes(fullCostNodes);

        if (conflictNodes.Count == 0) return new List<KeyValuePair<(Node, int), List<string>>>();

        return conflictNodes.OrderBy(c => c.Key.Item2).ToList();
    }

    private Dictionary<string, List<(Node node, int step)>> __getCost(Dictionary<string, List<Node>> paths) {
        /** <summary>
        * max perform.
        * </summary>
        */
        var result = new Dictionary<string, List<(Node node, int step)>>();
        foreach (var kvp in paths) {
            if (kvp.Value?.Count > 0) {
                var costPath = new List<(Node node, int step)>(kvp.Value.Count);
                for (int i = 0; i < kvp.Value.Count; i++) {
                    costPath.Add((kvp.Value[i], i + 1));
                }
                result[kvp.Key] = costPath;
            }
        }
        return result;
    }

    public Dictionary<(Node, int), List<string>> __getFullCostNodes(Dictionary<string, List<(Node node, int step)>> costPaths) {
        /**
        * <summary>
        * This is using the functional programming.
        * to make sure the code is fast and efficient.
        * Description:
        * 1. Where(p => p.Value?.Any() == true) -> if the path is not empty.
        * 2. SelectMany(p => p.Value.Select(n => (p.Key, n.node, n.step))) -> select many of its node, step, and key = agent name.
        * 3. GroupBy(x => (x.node, x.step)) -> group by node and step,
        * so that we could get the node and step that has more than 1 agent.
        * 4. ToDictionary(g => g.Key, g => g.Select(x => x.Key).ToList()) -> convert the group to dictinary,
        * key = (node, step)
        * value = list of agent names.
        * </summary>
        */
        if (costPaths?.Any() != true) return new Dictionary<(Node, int), List<string>>();

        // Build base result with all nodes and their agents (handles meeting conflicts)
        // ? Cost Paths = ( "Agent" : ( (node, step) );
        var result = new Dictionary<(Node, int), List<string>>();
        foreach (var kvp in costPaths) {
            if (kvp.Value?.Count > 0) {
                foreach (var (node, step) in kvp.Value) {
                    var key = (node, step);
                    if (!result.ContainsKey(key)) result[key] = new List<string>();
                    if (!result[key].Contains(kvp.Key)) result[key].Add(kvp.Key);
                }
            }
        }
        // Add swap conflicts
        var agentKeys = costPaths.Keys.ToArray();
        for (int i = 0; i < agentKeys.Length; i++) {
            for (int j = i + 1; j < agentKeys.Length; j++) {
                var path1 = costPaths[agentKeys[i]];
                var path2 = costPaths[agentKeys[j]];
                if (path1?.Count == 0 || path2?.Count == 0) continue;

                int minStep = Mathf.Min(path1.Count, path2.Count);
                for (int k = 1; k < minStep; k++) {
                    // Check for swap conflict
                    if (path1[k].node == path2[k - 1].node && path2[k].node == path1[k - 1].node) {
                        // Add conflicts for the nodes involved in the swap
                        var swapNodes = new HashSet<Node> { path1[k - 1].node, path1[k].node, path2[k - 1].node, path2[k].node };
                        foreach (var node in swapNodes) {
                            // Debug.Log($"detected at node {node.gridX}, {node.gridY}");
                            // Add conflicts for all relevant steps
                            var maxStep = Mathf.Max(path1.Count, path2.Count);
                            for (int step = 1; step <= maxStep; step++) {
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

    private Dictionary<(Node, int), List<string>> __getConflictNodes(Dictionary<(Node, int), List<string>> fullCostNodes) {
        return fullCostNodes.Where(n => n.Value.Count > 1).ToDictionary(n => n.Key, n => n.Value);
    }

    private bool __hasConflict(Dictionary<string, List<Node>> paths) {
        return _getConflictNodes(paths).Count > 0;
    }

    #endregion

    // Optimized for performance.
    private IEnumerable<int[]> __getPermutations(int maxValue, int count) {
        var arr = new int[maxValue];
        for (int i = 0; i < maxValue; i++) arr[i] = i + 1;
        return __permute(arr, 0, count);
    }

    private IEnumerable<int[]> __permute(int[] arr, int start, int m) {
        if (start == m) yield return arr.Take(m).ToArray();
        else {
            for (int i = start; i < arr.Length; i++) {
                (arr[start], arr[i]) = (arr[i], arr[start]);
                foreach (var p in __permute(arr, start + 1, m)) yield return p;
                (arr[start], arr[i]) = (arr[i], arr[start]);
            }
        }
    }

    #region path management.

    // ** BASIC PATH MANAGEMENT + ASSIGNMENT **
    // =========================================

    public void ReportAgentActive(string agentId) => activeAgents.Add(agentId);

    // ? this means we need to assign new paths to the,
    // ? agent that is not already have a path.
    // ? otherwise it will wait until the path is empty (e.g reached waypoint)
    // ? and then follow the new path.
    private void _assignNewPathsToIdleAgents() {
        foreach (var ag in agents.Where(a =>
            (!activePaths.ContainsKey(a.name) ||
             activePaths[a.name]?.Count == 0) &&
             agentWaypoints.TryGetValue(a.name, out var wps) && wps.Count > 0)) {
                _computePath(ag.name);
             }
    }

    public void AssignNextPathToAgent(string agentId) {
        if (!_hasWaypoints(agentId)) {
            activePaths.Remove(agentId);
            return;
        }
        _computePath(agentId);
    }

    private void _computePath(string agentId) {
        var a = agents.FirstOrDefault(a => a.name == agentId);
        if (a == null) return;
        // make the other agent to stop first.
        Vector3 start = a.transform.position;
        Vector3 end = agentWaypoints[agentId].Peek();
        var path = _rerouteFromNode(grid.NodeFromWorldPoint(start), grid.NodeFromWorldPoint(end), new HashSet<Node>(occupiedNodes));
        _resolveContestedNodes();
        if (path?.Count > 0) {
            activePaths[agentId] = path;
            a.AssignPath(path);
            if (agentWaypoints[agentId].Count > 0) {
                agentWaypoints[agentId].Dequeue();
            }
        } else {
            a.StartCoroutine(a.WaitAndRequestNextPath());
        }
    }

    private bool _hasWaypoints(string agentId) => agentWaypoints.TryGetValue(agentId, out var q) && q.Count > 0;

    private List<Node> _rerouteFromNode(Node start, Node end, HashSet<Node> blocked) {
        blocked.Remove(end);

        var backup = blocked.ToDictionary(n => n, n => n.walkable);
        foreach (var n in blocked) n.walkable = false;
        var path = AStarPathfinder.FindPath(grid, start.worldPosition, end.worldPosition);
        foreach (var n in blocked) n.walkable = backup[n];
        return path;
    }

    #endregion

    void OnDrawGizmos() {
        if (agents == null) return;

        foreach (var n in yoloObstacles.Keys) {
            // Need to be here, otherwise it would be bypassed by the other gizmos return logic.
            Gizmos.color = Color.red;
            Gizmos.DrawCube(n.worldPosition + Vector3.up * 0.1f, new Vector3(1f, 0.1f, 1f));
        }

        foreach (var agent in agents) {
            var isYolo = GlobalConfig.Instance != null && GlobalConfig.Instance.GetAgentYolo(agent.name);
            Gizmos.color = isYolo ? Color.green : Color.white;
            Gizmos.DrawWireSphere(agent.transform.position + Vector3.up * 1.5f, 0.5f);
        }
        if (activePaths == null || activePaths.Count == 0) return;

        int colorIdx = 0;
        foreach (var kvp in activePaths) {
            var path = kvp.Value;
            Color pathColor = agentColors[colorIdx % agentColors.Length];
            Gizmos.color = pathColor;
            for (int i = 0; i < path.Count; i++) {
                Gizmos.DrawSphere(path[i].worldPosition, 0.1f);
                if (i < path.Count - 1) {
                    Gizmos.DrawLine(path[i].worldPosition, path[i + 1].worldPosition);
                }
            }
            colorIdx++;
        }

        foreach (var n in occupiedNodes) {
            Gizmos.color = Color.red;
            Gizmos.DrawCube(n.worldPosition + Vector3.up * 0.1f, new Vector3(1f, 0.1f, 1f));
        }
    }
}
