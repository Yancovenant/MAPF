/**
* === Scripts/Node.cs ===
* Node class for pathfinding.
*/

using UnityEngine;

public class Node {
    public bool walkable;
    public Vector3 worldPosition;
    public int gridX, gridY;

    public Node(bool walkable, Vector3 worldPos, int x, int y) {
        this.walkable = walkable;
        worldPosition = worldPos;
        gridX = x;
        gridY = y;
    }

    public override bool Equals(object obj) {
        if (obj is Node other)
            return gridX == other.gridX && gridY == other.gridY;
        return false;
    }

    public override int GetHashCode() => gridX * 397 ^ gridY;

    // Add equality operators for proper value-based comparison
    public static bool operator ==(Node left, Node right) {
        if (ReferenceEquals(left, null))
            return ReferenceEquals(right, null);
        return left.Equals(right);
    }

    public static bool operator !=(Node left, Node right) {
        return !(left == right);
    }
}
