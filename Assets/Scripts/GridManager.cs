/**
* === Scripts/GridManager.cs ===
* Manage the Grid for Pathfinding.
*/

using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour {
    public LayerMask unwalkableLayer;
    public Vector2Int gridSize;
    public float nodeRadius;
    public Node[,] grid;

    private float nodeDiameter;
    private int gridSizeX, gridSizeY;

    public bool isReady = false;

    void Awake() {
        if (unwalkableLayer == 0) {
            unwalkableLayer = LayerMask.GetMask("Unwalkable");
        }
    }

    public void CreateGrid() {
        nodeDiameter = nodeRadius * 2;
        gridSizeX = Mathf.RoundToInt(gridSize.x / nodeDiameter);
        gridSizeY = Mathf.RoundToInt(gridSize.y / nodeDiameter);

        grid = new Node[gridSizeX, gridSizeY];

        for (int x = 0; x < gridSizeX; x++) {
            for (int y = 0; y < gridSizeY; y++) {
                Vector3 worldPoint = transform.position +
                    Vector3.right * (x * nodeDiameter + nodeRadius) +
                    Vector3.forward * (y * nodeDiameter + nodeRadius);
                bool walkable = !Physics.CheckSphere(worldPoint, nodeRadius, unwalkableLayer);
                grid[x, y] = new Node(walkable, worldPoint, x, y);
            }
        }

        isReady = true;
    }

    public Node NodeFromWorldPoint(Vector3 worldPosition) {
        var x = Mathf.FloorToInt(worldPosition.x);
        var y = Mathf.FloorToInt(worldPosition.z);

        x = Mathf.Clamp(x, 0, gridSizeX - 1);
        y = Mathf.Clamp(y, 0, gridSizeY - 1);

        return grid[x, y];
    }

    public List<Node> GetNeighbours(Node node) {
        var neighbours = new List<Node>();
        var directions = new[,] { {1,0}, {-1,0}, {0,1}, {0,-1} };

        for (int i = 0; i < directions.GetLength(0); i++ ) {
            var checkX = node.gridX + directions[i, 0];
            var checkY = node.gridY + directions[i, 1];

            if (checkX >= 0 &&
                checkX < gridSizeX &&
                checkY >= 0 &&
                checkY < gridSizeY) {
                    neighbours.Add(grid[checkX, checkY]);
                }
        }
        return neighbours;
    }
}
