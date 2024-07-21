using System.Linq;
using Content.Shared.Random.Helpers;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Content.Shared.Xenoarchaeology.Artifact.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Xenoarchaeology.Artifact;

public sealed partial class XenoArtifactSystem
{
    private readonly List<XenoArchTriggerPrototype> _triggerPool = new();

    private void GenerateArtifactStructure(Entity<XenoArtifactComponent> ent)
    {
        var nodeCount = ent.Comp.NodeCount.Next(RobustRandom);
        CreateTriggerPool(ent, ref nodeCount);
        ResizeNodeGraph(ent, nodeCount);
        while (nodeCount > 0)
        {
            GenerateArtifactSegment(ent, ref nodeCount);
        }

        RebuildXenoArtifactMetaData((ent, ent));
    }

    private void CreateTriggerPool(Entity<XenoArtifactComponent> ent, ref int size)
    {
        _triggerPool.Clear();
        var weightsProto = PrototypeManager.Index(ent.Comp.TriggerWeights);
        var weights = new Dictionary<string, float>(weightsProto.Weights);

        while (_triggerPool.Count < size)
        {
            // OOPS! We ran out of triggers.
            if (weights.Count == 0)
            {
                Log.Error($"Insufficient triggers for generating {ToPrettyString(ent)}! Needed {size} but had {_triggerPool.Count}");
                size = _triggerPool.Count;
                return;
            }

            var triggerId = RobustRandom.Pick(weights);
            weights.Remove(triggerId);
            var trigger = PrototypeManager.Index<XenoArchTriggerPrototype>(triggerId);
            if (_entityWhitelist.IsWhitelistFail(trigger.Whitelist, ent))
                continue;

            _triggerPool.Add(trigger);
        }
    }

    private void GenerateArtifactSegment(Entity<XenoArtifactComponent> ent, ref int nodeCount)
    {
        var segmentSize = GetArtifactSegmentSize(ent, nodeCount);
        nodeCount -= segmentSize;
        var populatedNodes = PopulateArtifactSegmentRecursive(ent, ref segmentSize);

        var segments = GetSegmentsFromNodes(ent, populatedNodes).ToList();

        // We didn't connect all of our nodes: do extra work to make sure there's a connection.
        if (segments.Count > 1)
        {
            var parent = segments.MaxBy(s => s.Count)!;
            var minP = parent.Min(n => n.Comp.Depth);
            var maxP = parent.Max(n => n.Comp.Depth);

            segments.Remove(parent);
            foreach (var segment in segments)
            {
                // calculate the range of the depth of the nodes in the segment
                var minS = segment.Min(n => n.Comp.Depth);
                var maxS = segment.Max(n => n.Comp.Depth);

                // Figure out the range of depths that allows for a connection between these two.
                // The range is essentially the lower values + 1 on each side.
                var min = Math.Max(minS, minP) - 1;
                var max = Math.Min(maxS, maxP) + 1;

                // how the fuck did you do this? you don't even deserve to get a parent. fuck you.
                if (min > max || min == max)
                    continue;

                var node1 = RobustRandom.Pick(segment
                    .Where(n => n.Comp.Depth >= min && n.Comp.Depth <= max)
                    .ToList());
                var node1Depth = node1.Comp.Depth;

                var node2 = RobustRandom.Pick(parent
                    .Where(n => n.Comp.Depth >= node1Depth - 1 && n.Comp.Depth <= node1Depth + 1 && n.Comp.Depth != node1Depth)
                    .ToList());

                if (node1.Comp.Depth < node2.Comp.Depth)
                {
                    AddEdge((ent, ent.Comp), node1, node2, false);
                }
                else
                {
                    AddEdge((ent, ent.Comp), node2, node1, false);
                }
            }
        }
    }

    private List<Entity<XenoArtifactNodeComponent>> PopulateArtifactSegmentRecursive(
        Entity<XenoArtifactComponent> ent,
        ref int segmentSize,
        int iteration = 0)
    {
        if (segmentSize == 0)
            return new();

        // Try and get larger as the we create more layers. Prevents excessive layers.
        var mod = RobustRandom.Next((int) (iteration / 1.5f), iteration + 1);

        var layerMin = Math.Min(ent.Comp.NodesPerSegmentLayer.Min + mod, segmentSize);
        var layerMax = Math.Min(ent.Comp.NodesPerSegmentLayer.Max + mod, segmentSize);

        // Default to one node if we had shenanigans and ended up with weird layer counts.
        var nodeCount = 1;
        if (layerMax >= layerMin)
            nodeCount = RobustRandom.Next(layerMin, layerMax + 1); // account for non-inclusive max

        segmentSize -= nodeCount;
        var nodes = new List<Entity<XenoArtifactNodeComponent>>();
        for (var i = 0; i < nodeCount; i++)
        {
            var trigger = RobustRandom.PickAndTake(_triggerPool);
            nodes.Add(CreateNode(ent, trigger, iteration));
        }

        var successors = PopulateArtifactSegmentRecursive(
            ent,
            ref segmentSize,
            iteration: iteration + 1);

        if (successors.Count == 0)
            return nodes;

        foreach (var successor in successors)
        {
            var node = RobustRandom.Pick(nodes);
            AddEdge((ent, ent), node, successor, dirty: false);
        }

        // randomly add in some extra edges for variance.
        var scatterCount = ent.Comp.ScatterPerLayer.Next(RobustRandom);
        for (var i = 0; i < scatterCount; i++)
        {
            var node = RobustRandom.Pick(nodes);
            var successor = RobustRandom.Pick(successors);
            AddEdge((ent, ent), node, successor, dirty: false);
        }

        return nodes;
    }

    private int GetArtifactSegmentSize(Entity<XenoArtifactComponent> ent, int nodeCount)
    {
        // Make sure we can't generate a single segment artifact.
        // We always want to have at least 2 segments. For variety.
        var segmentMin = ent.Comp.SegmentSize.Min;
        var segmentMax = Math.Min(ent.Comp.SegmentSize.Max, Math.Max(nodeCount / 2, segmentMin));

        var segmentSize = RobustRandom.Next(segmentMin, segmentMax + 1); // account for non-inclusive max
        var remainder = nodeCount - segmentSize;

        // If our next segment is going to be undersized, then we just absorb it into this segment.
        if (remainder < ent.Comp.SegmentSize.Min)
            segmentSize += remainder;

        // Sanity check to make sure we don't exceed the node count. (it shouldn't happen prior anyway but oh well)
        segmentSize = Math.Min(nodeCount, segmentSize);

        return segmentSize;
    }
}
