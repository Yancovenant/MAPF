using UnityEngine;

/**
<summary>
    <c>Scripts/Node.cs</c>
    Node class for pathfinding.
</summary>
<returns>
Node
    <param name="walkable">bool: Whether the node is walkable.</param>
    <param name="worldPosition">Vector3: The world position of the node.</param>
    <param name="gridX">int: The x coordinate of the node in the grid.</param>
    <param name="gridY">int: The y coordinate of the node in the grid.</param>
</returns>
*/

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
