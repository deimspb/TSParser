using System.Text.Json;
using TSParser.Tables;
using TSParser.Web.Models;

namespace TSParser.Web.Services;

/// <summary>
/// Thread-safe accumulation of SI table versions for the tree UI (max 255 per group, active/stale).
/// </summary>
public sealed class TableVersionStore
{
    public const int MaxVersionsPerGroup = 255;

    private static readonly string[] CategoryTitleOrder =
    [
        "PAT",
        "PMT",
        "CAT",
        "NIT (actual)",
        "NIT (other)",
        "SDT (actual)",
        "SDT (other)",
        "BAT",
        "EIT",
        "TDT",
        "TOT",
        "MIP",
        "AIT",
        "SCTE-35",
        "EWS",
        "EEWS"
    ];

    private readonly object _sync = new();
    private readonly StreamPidCatalog _pidCatalog = new();
    private readonly Dictionary<string, TableTreeNode> _categories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TableTreeNode> _streamByKey = new(StringComparer.Ordinal);
    private readonly TableTreeNode _pidsCategory = new()
    {
        Kind = TableTreeNodeKind.Category,
        Label = "PIDs",
        IsExpanded = true
    };

    public long Revision { get; private set; }

    public Guid? SelectedNodeId { get; private set; }

    public IReadOnlyList<TableTreeNode> RootCategories
    {
        get
        {
            lock (_sync)
            {
                var list = new List<TableTreeNode>();
                if (_pidsCategory.Children.Count > 0)
                    list.Add(_pidsCategory);

                list.AddRange(CategoryTitleOrder
                    .Where(_categories.ContainsKey)
                    .Select(title => _categories[title]));

                return list;
            }
        }
    }

    public TableTreeNode? FindNode(Guid nodeId)
    {
        lock (_sync)
        {
            if (TryFindNode(_pidsCategory, nodeId, out var pidsFound))
                return pidsFound;

            foreach (var category in _categories.Values)
                if (TryFindNode(category, nodeId, out var found))
                    return found;
        }

        return null;
    }

    public void Clear()
    {
        lock (_sync)
        {
            _categories.Clear();
            _streamByKey.Clear();
            _pidCatalog.Clear();
            _pidsCategory.Children.Clear();
            SelectedNodeId = null;
            BumpRevision();
        }
    }

    public void ApplyTable(TsTableKind kind, Table table)
    {
        lock (_sync)
        {
            _pidCatalog.ApplyTable(kind, table);
            if (ShouldRebuildPidTree(kind))
                RebuildPidChildren();

            var category = GetOrCreateCategory(kind, table);
            var streamKey = $"{kind}|{TableVersionKeyBuilder.GetStreamKey(kind, table)}";
            var versionContainer = GetVersionContainer(category, kind, table, streamKey);
            AddVersion(versionContainer, kind, table);
            BumpRevision();
        }
    }

    public void SyncObservedPids(IReadOnlyList<ushort> pids)
    {
        lock (_sync)
        {
            var added = _pidCatalog.SyncObserved(pids);
            if (!added && _pidsCategory.Children.Count > 0)
                return;

            RebuildPidChildren();
            BumpRevision();
        }
    }

    public void SetSelectedNode(Guid? nodeId)
    {
        lock (_sync)
        {
            if (SelectedNodeId == nodeId)
                return;

            SelectedNodeId = nodeId;
            BumpRevision();
        }
    }

    public void SetExpanded(Guid nodeId, bool expanded)
    {
        lock (_sync)
        {
            if (FindNode(nodeId) is not { } node)
            {
                // #region agent log
                AgentDebugLog(
                    "A",
                    "TableVersionStore.SetExpanded",
                    "node not found",
                    new { nodeId, expanded, runId = "post-fix" });
                // #endregion
                return;
            }

            node.IsExpanded = expanded;
            if (expanded && node.Kind == TableTreeNodeKind.Version)
                TableDescriptorChildrenBuilder.LoadDescriptorChildren(node);

            // #region agent log
            AgentDebugLog(
                "B",
                "TableVersionStore.SetExpanded",
                "after apply",
                new { node.Id, node.IsExpanded, node.Label, node.Kind, runId = "post-fix" });
            // #endregion

            BumpRevision();
        }
    }

    private TableTreeNode GetOrCreateCategory(TsTableKind kind, Table table)
    {
        var title = TableVersionKeyBuilder.GetCategoryTitle(kind, table);
        if (_categories.TryGetValue(title, out var existing))
            return existing;

        var node = new TableTreeNode
        {
            Kind = TableTreeNodeKind.Category,
            Label = title,
            IsExpanded = true
        };
        _categories[title] = node;
        return node;
    }

    private TableTreeNode GetVersionContainer(
        TableTreeNode category,
        TsTableKind kind,
        Table table,
        string streamKey)
    {
        if (_streamByKey.TryGetValue(streamKey, out var existing))
            return existing;

        if (TableVersionKeyBuilder.UsesFlatVersions(kind))
        {
            _streamByKey[streamKey] = category;
            return category;
        }

        var stream = new TableTreeNode
        {
            Kind = TableTreeNodeKind.Stream,
            Label = TableVersionKeyBuilder.GetStreamLabel(kind, table),
            IsExpanded = true,
            Payload = streamKey
        };

        _streamByKey[streamKey] = stream;
        category.Children.Add(stream);
        return stream;
    }

    private static void AddVersion(TableTreeNode stream, TsTableKind kind, Table table)
    {
        var versions = stream.Children;
        if (versions.Count > 0 && versions[^1].Payload is Table last && last.CRC32 == table.CRC32)
            return;

        foreach (var v in versions)
            v.IsActive = false;

        var prefix = TableVersionKeyBuilder.GetVersionPrefix(kind);
        var index = versions.Count + 1;
        var versionNode = new TableTreeNode
        {
            Kind = TableTreeNodeKind.Version,
            Label = $"{prefix}_{index}",
            IsActive = true,
            Payload = table
        };

        versions.Add(versionNode);

        if (versions.Count > MaxVersionsPerGroup)
        {
            versions.RemoveAt(0);
            RenumberVersionLabels(versions, prefix);
        }
    }

    private static void RenumberVersionLabels(List<TableTreeNode> versions, string prefix)
    {
        for (var i = 0; i < versions.Count; i++)
            versions[i].Label = $"{prefix}_{i + 1}";
    }

    private static bool TryFindNode(TableTreeNode node, Guid id, out TableTreeNode? found)
    {
        if (node.Id == id)
        {
            found = node;
            return true;
        }

        foreach (var child in node.Children)
        {
            if (TryFindNode(child, id, out found))
                return true;
        }

        found = null;
        return false;
    }

    private static bool ShouldRebuildPidTree(TsTableKind kind) => kind switch
    {
        TsTableKind.Pat or TsTableKind.Pmt or TsTableKind.Sdt or TsTableKind.Cat
            or TsTableKind.Ews or TsTableKind.Eews or TsTableKind.Ait
            or TsTableKind.Mip or TsTableKind.Scte35 => true,
        _ => false
    };

    private void RebuildPidChildren()
    {
        var expandedBefore = _pidsCategory.IsExpanded;
        var entries = _pidCatalog.GetSortedEntries();
        var existing = _pidsCategory.Children
            .Where(n => n.Payload is ushort)
            .ToDictionary(n => (ushort)n.Payload!, n => n);

        _pidsCategory.Children.Clear();

        foreach (var entry in entries)
        {
            if (existing.TryGetValue(entry.Pid, out var node))
            {
                node.Label = entry.Label;
                node.IsMissingFromStream = entry.IsMissingFromStream;
                _pidsCategory.Children.Add(node);
            }
            else
            {
                _pidsCategory.Children.Add(new TableTreeNode
                {
                    Kind = TableTreeNodeKind.Pid,
                    Label = entry.Label,
                    Payload = entry.Pid,
                    IsMissingFromStream = entry.IsMissingFromStream
                });
            }
        }

        // #region agent log
        AgentDebugLog(
            "C",
            "TableVersionStore.RebuildPidChildren",
            "rebuilt pid list",
            new
            {
                expandedBefore,
                expandedAfter = _pidsCategory.IsExpanded,
                childCount = _pidsCategory.Children.Count
            });
        // #endregion
    }

    private void BumpRevision() => Revision++;

    // #region agent log
    private static void AgentDebugLog(string hypothesisId, string location, string message, object data)
    {
        try
        {
            var line = JsonSerializer.Serialize(new
            {
                sessionId = "93273f",
                hypothesisId,
                location,
                message,
                data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            File.AppendAllText(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "debug-93273f.log"),
                line + Environment.NewLine);
        }
        catch
        {
        }
    }
    // #endregion
}
