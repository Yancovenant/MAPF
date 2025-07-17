/**
* === Scripts/AStarPathfinder.cs ===
* A* pathfinding algorithm.
*/

using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class AStarPathfinder {
    private class PathNode {
        public Node node;
        public int gCost, hCost;
        public PathNode parent;
        public int fCost => gCost + hCost;
        
        public PathNode(Node node, int gCost, int hCost, PathNode parent) {
            this.node = node;
            this.gCost = gCost;
            this.hCost = hCost;
            this.parent = parent;
        }
    }

    public static List<Node> FindPath(GridManager grid, Vector3 startPos, Vector3 targetPos) {
        var startNode = grid.NodeFromWorldPoint(startPos);
        var targetNode = grid.NodeFromWorldPoint(targetPos);
        
        var openSet = new List<PathNode>();
        var closedSet = new HashSet<Node>();
        var allNodes = new Dictionary<Node, PathNode>();
        
        var startPathNode = new PathNode(startNode, 0, GetDistance(startNode, targetNode), null);
        openSet.Add(startPathNode);
        allNodes[startNode] = startPathNode;
        
        while (openSet.Count > 0) {
            var current = openSet.OrderBy(p => p.fCost).ThenBy(p => p.hCost).First();
            openSet.Remove(current);
            
            if (current.node == targetNode) return RetracePath(current);
            
            closedSet.Add(current.node);
            
            foreach (var neighbor in grid.GetNeighbours(current.node)) {
                if (!neighbor.walkable || closedSet.Contains(neighbor)) continue;
                
                var gCost = current.gCost + 1;
                if (allNodes.TryGetValue(neighbor, out var existing) && existing.gCost <= gCost) continue;
                
                var neighborPathNode = new PathNode(neighbor, gCost, GetDistance(neighbor, targetNode), current);
                if (!openSet.Contains(neighborPathNode)) openSet.Add(neighborPathNode);
                allNodes[neighbor] = neighborPathNode;
            }
        }
        if (openSet.Count == 0) {
            
        }
        Debug.LogWarning($"No path found between {startPos} and {targetPos}");
        return null;
    }
    
    private static List<Node> RetracePath(PathNode endNode) {
        var path = new List<Node>();
        var current = endNode;
        while (current != null) {
            path.Add(current.node);
            current = current.parent;
        }
        path.Reverse();
        return path;
    }
    
    private static int GetDistance(Node a, Node b) {
        var dx = Mathf.Abs(a.gridX - b.gridX);
        var dy = Mathf.Abs(a.gridY - b.gridY);
        return dx > dy ? 14 * dy + 10 * (dx - dy) : 14 * dx + 10 * (dy - dx);
    }
}