﻿using System.Linq;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests;

[TestFixture]
public sealed class XenoArtifactTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestArtifact
  name: artifact
  components:
  - type: XenoArtifact
    doGeneration: false

- type: entity
  id: TestArtifactNode
  name: artifact node
  components:
  - type: XenoArtifactNode
    maxDurability: 3
";

    /// <summary>
    /// Checks that adding nodes and edges properly adds them into the adjacency matrix
    /// </summary>
    [Test]
    public async Task XenoArtifactAddNodeTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entManager = server.ResolveDependency<IEntityManager>();
        var artifactSystem = entManager.System<SharedXenoArtifactSystem>();

        await server.WaitPost(() =>
        {
            var artifactUid = entManager.Spawn("TestArtifact");
            var artifactEnt = (artifactUid, entManager.GetComponent<XenoArtifactComponent>(artifactUid));

            // Create 3 nodes
            Assert.That(artifactSystem.AddNode(artifactEnt, "TestArtifactNode", out var node1, false));
            Assert.That(artifactSystem.AddNode(artifactEnt, "TestArtifactNode", out var node2, false));
            Assert.That(artifactSystem.AddNode(artifactEnt, "TestArtifactNode", out var node3, false));

            Assert.That(artifactSystem.GetAllNodeIndices(artifactEnt).Count(), Is.EqualTo(3));

            // Add connection from 1 -> 2 and 2-> 3
            artifactSystem.AddEdge(artifactEnt, node1!.Value, node2!.Value, false);
            artifactSystem.AddEdge(artifactEnt, node2!.Value, node3!.Value, false);

            // Assert that successors and direct successors are counted correctly for node 1.
            Assert.That(artifactSystem.GetDirectSuccessorNodes(artifactEnt, node1!.Value).Count, Is.EqualTo(1));
            Assert.That(artifactSystem.GetSuccessorNodes(artifactEnt, node1!.Value).Count, Is.EqualTo(2));
            // Assert that we didn't somehow get predecessors on node 1.
            Assert.That(artifactSystem.GetDirectPredecessorNodes(artifactEnt, node1!.Value), Is.Empty);
            Assert.That(artifactSystem.GetPredecessorNodes(artifactEnt, node1!.Value), Is.Empty);

            // Assert that successors and direct successors are counted correctly for node 2.
            Assert.That(artifactSystem.GetDirectSuccessorNodes(artifactEnt, node2!.Value), Has.Count.EqualTo(1));
            Assert.That(artifactSystem.GetSuccessorNodes(artifactEnt, node2!.Value), Has.Count.EqualTo(1));
            // Assert that predecessors and direct predecessors are counted correctly for node 2.
            Assert.That(artifactSystem.GetDirectPredecessorNodes(artifactEnt, node2!.Value), Has.Count.EqualTo(1));
            Assert.That(artifactSystem.GetPredecessorNodes(artifactEnt, node2!.Value), Has.Count.EqualTo(1));

            // Assert that successors and direct successors are counted correctly for node 3.
            Assert.That(artifactSystem.GetDirectSuccessorNodes(artifactEnt, node3!.Value), Is.Empty);
            Assert.That(artifactSystem.GetSuccessorNodes(artifactEnt, node3!.Value), Is.Empty);
            // Assert that predecessors and direct predecessors are counted correctly for node 3.
            Assert.That(artifactSystem.GetDirectPredecessorNodes(artifactEnt, node3!.Value), Has.Count.EqualTo(1));
            Assert.That(artifactSystem.GetPredecessorNodes(artifactEnt, node3!.Value), Has.Count.EqualTo(2));
        });
        await server.WaitRunTicks(1);

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Checks to make sure that removing nodes properly cleans up all connections.
    /// </summary>
    [Test]
    public async Task XenoArtifactRemoveNodeTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entManager = server.ResolveDependency<IEntityManager>();
        var artifactSystem = entManager.System<SharedXenoArtifactSystem>();

        await server.WaitPost(() =>
        {
            var artifactUid = entManager.Spawn("TestArtifact");
            var artifactEnt = (artifactUid, entManager.GetComponent<XenoArtifactComponent>(artifactUid));

            // Create 3 nodes
            Assert.That(artifactSystem.AddNode(artifactEnt, "TestArtifactNode", out var node1, false));
            Assert.That(artifactSystem.AddNode(artifactEnt, "TestArtifactNode", out var node2, false));
            Assert.That(artifactSystem.AddNode(artifactEnt, "TestArtifactNode", out var node3, false));
            Assert.That(artifactSystem.AddNode(artifactEnt, "TestArtifactNode", out var node4, false));
            Assert.That(artifactSystem.AddNode(artifactEnt, "TestArtifactNode", out var node5, false));

            Assert.That(artifactSystem.GetAllNodeIndices(artifactEnt).Count(), Is.EqualTo(5));

            // Add connection: 1 -> 2 -> 3 -> 4 -> 5
            artifactSystem.AddEdge(artifactEnt, node1!.Value, node2!.Value, false);
            artifactSystem.AddEdge(artifactEnt, node2!.Value, node3!.Value, false);
            artifactSystem.AddEdge(artifactEnt, node3!.Value, node4!.Value, false);
            artifactSystem.AddEdge(artifactEnt, node4!.Value, node5!.Value, false);

            // Make sure we have a continuous connection between the two ends of the graph.
            Assert.That(artifactSystem.GetSuccessorNodes(artifactEnt, node1.Value), Has.Count.EqualTo(4));
            Assert.That(artifactSystem.GetPredecessorNodes(artifactEnt, node5.Value), Has.Count.EqualTo(4));

            // Remove the node and make sure it's no longer in the artifact.
            Assert.That(artifactSystem.RemoveNode(artifactEnt, node3!.Value, false));
            Assert.That(artifactSystem.TryGetIndex(artifactEnt, node3!.Value, out _), Is.False, "Node 3 still present in artifact.");

            // Check to make sure that we got rid of all the connections.
            Assert.That(artifactSystem.GetSuccessorNodes(artifactEnt, node2!.Value), Is.Empty);
            Assert.That(artifactSystem.GetPredecessorNodes(artifactEnt, node4!.Value), Is.Empty);
        });
        await server.WaitRunTicks(1);

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Sets up series of linked nodes and ensures that resizing the adjacency matrix doesn't disturb the connections
    /// </summary>
    [Test]
    public async Task XenoArtifactResizeTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entManager = server.ResolveDependency<IEntityManager>();
        var artifactSystem = entManager.System<SharedXenoArtifactSystem>();

        await server.WaitPost(() =>
        {
            var artifactUid = entManager.Spawn("TestArtifact");
            var artifactEnt = (artifactUid, entManager.GetComponent<XenoArtifactComponent>(artifactUid));

            // Create 3 nodes
            Assert.That(artifactSystem.AddNode(artifactEnt, "TestArtifactNode", out var node1, false));
            Assert.That(artifactSystem.AddNode(artifactEnt, "TestArtifactNode", out var node2, false));
            Assert.That(artifactSystem.AddNode(artifactEnt, "TestArtifactNode", out var node3, false));

            // Add connection: 1 -> 2 -> 3
            artifactSystem.AddEdge(artifactEnt, node1!.Value, node2!.Value, false);
            artifactSystem.AddEdge(artifactEnt, node2!.Value, node3!.Value, false);

            // Make sure our connection is set up
            Assert.That(artifactSystem.NodeHasEdge(artifactEnt, node1.Value, node2.Value));
            Assert.That(artifactSystem.NodeHasEdge(artifactEnt, node2.Value, node3.Value));
            Assert.That(artifactSystem.NodeHasEdge(artifactEnt, node2.Value, node1.Value), Is.False);
            Assert.That(artifactSystem.NodeHasEdge(artifactEnt, node3.Value, node2.Value), Is.False);
            Assert.That(artifactSystem.NodeHasEdge(artifactEnt, node1.Value, node3.Value), Is.False);
            Assert.That(artifactSystem.NodeHasEdge(artifactEnt, node3.Value, node1.Value), Is.False);

            Assert.That(artifactSystem.GetIndex(artifactEnt, node1!.Value), Is.EqualTo(0));
            Assert.That(artifactSystem.GetIndex(artifactEnt, node2!.Value), Is.EqualTo(1));
            Assert.That(artifactSystem.GetIndex(artifactEnt, node3!.Value), Is.EqualTo(2));

            // Add a new node, resizing the original adjacency matrix and array.
            Assert.That(artifactSystem.AddNode(artifactEnt, "TestArtifactNode", out var node4));

            // Check that our connections haven't changed.
            Assert.That(artifactSystem.NodeHasEdge(artifactEnt, node1.Value, node2.Value));
            Assert.That(artifactSystem.NodeHasEdge(artifactEnt, node2.Value, node3.Value));
            Assert.That(artifactSystem.NodeHasEdge(artifactEnt, node2.Value, node1.Value), Is.False);
            Assert.That(artifactSystem.NodeHasEdge(artifactEnt, node3.Value, node2.Value), Is.False);
            Assert.That(artifactSystem.NodeHasEdge(artifactEnt, node1.Value, node3.Value), Is.False);
            Assert.That(artifactSystem.NodeHasEdge(artifactEnt, node3.Value, node1.Value), Is.False);

            // Has our array shifted any when we resized?
            Assert.That(artifactSystem.GetIndex(artifactEnt, node1!.Value), Is.EqualTo(0));
            Assert.That(artifactSystem.GetIndex(artifactEnt, node2!.Value), Is.EqualTo(1));
            Assert.That(artifactSystem.GetIndex(artifactEnt, node3!.Value), Is.EqualTo(2));

            // Check that 4 didn't somehow end up with connections
            Assert.That(artifactSystem.GetPredecessorNodes(artifactEnt, node4!.Value), Is.Empty);
            Assert.That(artifactSystem.GetSuccessorNodes(artifactEnt, node4!.Value), Is.Empty);
        });
        await server.WaitRunTicks(1);

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Checks if removing a node and adding a new node into its place in the adjacency matrix doesn't accidentally retain extra data.
    /// </summary>
    [Test]
    public async Task XenoArtifactReplaceTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entManager = server.ResolveDependency<IEntityManager>();
        var artifactSystem = entManager.System<SharedXenoArtifactSystem>();

        await server.WaitPost(() =>
        {
            var artifactUid = entManager.Spawn("TestArtifact");
            var artifactEnt = (artifactUid, entManager.GetComponent<XenoArtifactComponent>(artifactUid));

            // Create 3 nodes
            Assert.That(artifactSystem.AddNode(artifactEnt, "TestArtifactNode", out var node1, false));
            Assert.That(artifactSystem.AddNode(artifactEnt, "TestArtifactNode", out var node2, false));
            Assert.That(artifactSystem.AddNode(artifactEnt, "TestArtifactNode", out var node3, false));

            // Add connection: 1 -> 2 -> 3
            artifactSystem.AddEdge(artifactEnt, node1!.Value, node2!.Value, false);
            artifactSystem.AddEdge(artifactEnt, node2!.Value, node3!.Value, false);

            // Make sure our connection is set up
            Assert.That(artifactSystem.NodeHasEdge(artifactEnt, node1.Value, node2.Value));
            Assert.That(artifactSystem.NodeHasEdge(artifactEnt, node2.Value, node3.Value));

            // Remove middle node, severing connections
            artifactSystem.RemoveNode(artifactEnt, node2!.Value, false);

            // Make sure our connection are properly severed.
            Assert.That(artifactSystem.GetSuccessorNodes(artifactEnt, node1.Value), Is.Empty);
            Assert.That(artifactSystem.GetPredecessorNodes(artifactEnt, node3.Value), Is.Empty);

            // Make sure our matrix is 3x3
            Assert.That(artifactEnt.Item2.NodeAdjacencyMatrixRows, Is.EqualTo(3));
            Assert.That(artifactEnt.Item2.NodeAdjacencyMatrixColumns, Is.EqualTo(3));

            Assert.That(artifactSystem.AddNode(artifactEnt, "TestArtifactNode", out var node4, false));

            // Make sure that adding in a new node didn't add a new slot but instead re-used the middle slot.
            Assert.That(artifactEnt.Item2.NodeAdjacencyMatrixRows, Is.EqualTo(3));
            Assert.That(artifactEnt.Item2.NodeAdjacencyMatrixColumns, Is.EqualTo(3));

            // Ensure that all connections are still severed
            Assert.That(artifactSystem.GetSuccessorNodes(artifactEnt, node1.Value), Is.Empty);
            Assert.That(artifactSystem.GetPredecessorNodes(artifactEnt, node3.Value), Is.Empty);
            Assert.That(artifactSystem.GetSuccessorNodes(artifactEnt, node4!.Value), Is.Empty);
            Assert.That(artifactSystem.GetPredecessorNodes(artifactEnt, node4!.Value), Is.Empty);

        });
        await server.WaitRunTicks(1);

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Checks if the active nodes are properly detected.
    /// </summary>
    [Test]
    public async Task XenoArtifactBuildActiveNodesTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entManager = server.ResolveDependency<IEntityManager>();
        var artifactSystem = entManager.System<SharedXenoArtifactSystem>();

        await server.WaitPost(() =>
        {
            var artifactUid = entManager.Spawn("TestArtifact");
            var artifactEnt = (artifactUid, entManager.GetComponent<XenoArtifactComponent>(artifactUid));

            Assert.That(artifactSystem.AddNode(artifactEnt, "TestArtifactNode", out var node1, false));
            Assert.That(artifactSystem.AddNode(artifactEnt, "TestArtifactNode", out var node2, false));
            Assert.That(artifactSystem.AddNode(artifactEnt, "TestArtifactNode", out var node3, false));
            Assert.That(artifactSystem.AddNode(artifactEnt, "TestArtifactNode", out var node4, false));
            Assert.That(artifactSystem.AddNode(artifactEnt, "TestArtifactNode", out var node5, false));
            Assert.That(artifactSystem.AddNode(artifactEnt, "TestArtifactNode", out var node6, false));
            Assert.That(artifactSystem.AddNode(artifactEnt, "TestArtifactNode", out var node7, false));
            Assert.That(artifactSystem.AddNode(artifactEnt, "TestArtifactNode", out var node8, false));

            //                       /----( 6 )
            //           /----[*3 ]-/----( 7 )----( 8 )
            //          /
            //         /           /----[*5 ]
            // [ 1 ]--/----[ 2 ]--/----( 4 )
            // Diagram of the example generation. Nodes in [brackets] are unlocked, nodes in (braces) are locked
            // and nodes with an *asterisk are supposed to be active.
            artifactSystem.AddEdge(artifactEnt, node1!.Value, node2!.Value, false);
            artifactSystem.AddEdge(artifactEnt, node1!.Value, node3!.Value, false);

            artifactSystem.AddEdge(artifactEnt, node2!.Value, node4!.Value, false);
            artifactSystem.AddEdge(artifactEnt, node2!.Value, node5!.Value, false);

            artifactSystem.AddEdge(artifactEnt, node3!.Value, node6!.Value, false);
            artifactSystem.AddEdge(artifactEnt, node3!.Value, node7!.Value, false);

            artifactSystem.AddEdge(artifactEnt, node7!.Value, node8!.Value, false);

            artifactSystem.SetNodeUnlocked(node1!.Value);
            artifactSystem.SetNodeUnlocked(node2!.Value);
            artifactSystem.SetNodeUnlocked(node3!.Value);
            artifactSystem.SetNodeUnlocked(node5!.Value);

            var activeNodes = new[] { entManager.GetNetEntity(node3!.Value.Owner), entManager.GetNetEntity(node5!.Value.Owner) };
            Assert.That(artifactEnt.Item2.CachedActiveNodes, Is.SupersetOf(activeNodes));
            Assert.That(artifactEnt.Item2.CachedActiveNodes, Has.Count.EqualTo(activeNodes.Length));

        });
        await server.WaitRunTicks(1);

        await pair.CleanReturnAsync();
    }

    // TODO: write test for segments because we'll probably need that.
}
