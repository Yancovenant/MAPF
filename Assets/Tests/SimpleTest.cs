using UnityEngine;
using NUnit.Framework;

/// <summary>
/// Simple test to verify assembly references are working
/// </summary>
public class SimpleTest
{
    [Test]
    public void TestNodeClassAccess()
    {
        // Test that we can access the Node class
        var node = new Node(true, new Vector3(0, 0, 0), 0, 0);
        Assert.IsNotNull(node);
        Assert.AreEqual(0, node.gridX);
        Assert.AreEqual(0, node.gridY);
        Assert.IsTrue(node.walkable);
    }

    [Test]
    public void TestPathSupervisorAccess()
    {
        // Test that we can access the PathSupervisor class
        var go = new GameObject("TestPathSupervisor");
        var pathSupervisor = go.AddComponent<PathSupervisor>();
        Assert.IsNotNull(pathSupervisor);
        
        // Clean up
        Object.DestroyImmediate(go);
    }
} 