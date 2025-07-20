using UnityEngine;
using NUnit.Framework;

/// <summary>
/// Test to verify Node equality comparison works correctly
/// </summary>
public class NodeEqualityTest
{
    [Test]
    public void TestNodeEquality()
    {
        // Create nodes with same coordinates but different instances
        var node1 = new Node(true, new Vector3(1, 0, 0), 1, 0);
        var node2 = new Node(true, new Vector3(1, 0, 0), 1, 0);
        var node3 = new Node(true, new Vector3(2, 0, 0), 2, 0);

        // Test that nodes with same coordinates are equal
        Assert.IsTrue(node1 == node2, "Nodes with same coordinates should be equal");
        Assert.IsTrue(node1.Equals(node2), "Equals method should work correctly");
        Assert.AreEqual(node1.GetHashCode(), node2.GetHashCode(), "Hash codes should be equal for equal nodes");

        // Test that nodes with different coordinates are not equal
        Assert.IsFalse(node1 == node3, "Nodes with different coordinates should not be equal");
        Assert.IsFalse(node1.Equals(node3), "Equals method should work correctly for different nodes");

        // Test null handling
        Assert.IsFalse(node1 == null, "Node should not equal null");
        Assert.IsFalse(null == node1, "Null should not equal node");
        Assert.IsTrue((Node)null == (Node)null, "Null should equal null");

        Debug.Log("Node equality test passed!");
    }

    [Test]
    public void TestNodeEqualityInConflictDetection()
    {
        // Create test data that should trigger swap conflict detection
        var node1 = new Node(true, new Vector3(1, 0, 0), 1, 0);
        var node2 = new Node(true, new Vector3(2, 0, 0), 2, 0);
        var node3 = new Node(true, new Vector3(1, 0, 0), 1, 0); // Same as node1
        var node4 = new Node(true, new Vector3(2, 0, 0), 2, 0); // Same as node2

        // Test the swap conflict detection logic
        bool isSwapConflict = (node1 == node3) && (node2 == node4);
        
        Debug.Log($"node1: ({node1.gridX}, {node1.gridY})");
        Debug.Log($"node2: ({node2.gridX}, {node2.gridY})");
        Debug.Log($"node3: ({node3.gridX}, {node3.gridY})");
        Debug.Log($"node4: ({node4.gridX}, {node4.gridY})");
        Debug.Log($"node1 == node3: {node1 == node3}");
        Debug.Log($"node2 == node4: {node2 == node4}");
        Debug.Log($"Is swap conflict: {isSwapConflict}");

        // This should now work correctly
        Assert.IsTrue(node1 == node3, "node1 should equal node3 (same coordinates)");
        Assert.IsTrue(node2 == node4, "node2 should equal node4 (same coordinates)");
        Assert.IsTrue(isSwapConflict, "Should detect swap conflict with proper equality");
    }
} 