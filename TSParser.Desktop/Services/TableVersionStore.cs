using TSParser.Analysis;
using TSParser.Tables;
using TSParser.Desktop.Models;

namespace TSParser.Desktop.Services;

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
    private readonly TableTreeNode _plpSectionSeparator = new()
    {
        Kind = TableTreeNodeKind.Category,
        Label = "── PLP Services ──",
        IsExpanded = true
    };
    private readonly Dictionary<string, TableTreeNode> _plpNodes = new(StringComparer.Ordinal);
    private readonly TableTreeNode _pidsCategory = new()
    {
        Kind = TableTreeNodeKind.Category,
        Label = "PIDs",
        IsExpanded = false
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
                list.AddRange(CategoryTitleOrder
                    .Where(_categories.ContainsKey)
                    .Select(title => _categories[title]));

                if (_pidsCategory.Children.Count > 0)
                    list.Add(_pidsCategory);

                if (_plpNodes.Count > 0)
                {
                    list.Add(_plpSectionSeparator);
                    list.AddRange(_plpNodes.Values
                        .OrderBy(n => PlpSortKey(n)));
                }

                return list;
            }
        }
    }

    public (int Services, int Pids, int TableGroups) GetOverviewCounts()
    {
        lock (_sync)
        {
            var services = _categories.TryGetValue("PMT", out var pmt)
                ? pmt.Children.Count
                : 0;
            return (services, _pidsCategory.Children.Count, _categories.Count);
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

            foreach (var plpNode in _plpNodes.Values)
                if (TryFindNode(plpNode, nodeId, out var plpFound))
                    return plpFound;
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
            _plpNodes.Clear();
            SelectedNodeId = null;
            BumpRevision();
        }
    }

    public void ApplyTable(TsTableKind kind, Table table, ulong? pcrValue = null)
    {
        lock (_sync)
        {
            _pidCatalog.ApplyTable(kind, table);
            if (ShouldRebuildPidTree(kind))
                RebuildPidChildren();

            var category = GetOrCreateCategory(kind, table);
            var streamKey = $"{kind}|{TableVersionKeyBuilder.GetStreamKey(kind, table)}";
            var versionContainer = GetVersionContainer(category, kind, table, streamKey);
            AddVersion(versionContainer, kind, table, pcrValue);
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
            SelectedNodeId = nodeId;
    }

    public void SetExpanded(Guid nodeId, bool expanded)
    {
        lock (_sync)
        {
            if (FindNode(nodeId) is not { } node)
                return;

            node.IsExpanded = expanded;
            if (expanded && node.Kind == TableTreeNodeKind.Version)
                TableDescriptorChildrenBuilder.LoadDescriptorChildren(node);
        }
    }

    public void ApplyPlpServices(ushort t2miPid, byte plpId, IReadOnlyList<PlpServiceInfo> services)
    {
        lock (_sync)
        {
            var plpKey = PlpNodeKey(t2miPid, plpId);

            if (!_plpNodes.TryGetValue(plpKey, out var plpNode))
            {
                plpNode = new TableTreeNode
                {
                    Kind = TableTreeNodeKind.Category,
                    Label = $"PLP {plpId} [T2-MI: 0x{t2miPid:X4}]",
                    IsExpanded = true,
                    Payload = (t2miPid, plpId)
                };
                _plpNodes[plpKey] = plpNode;
            }

            plpNode.Children.Clear();

            if (services.Count == 0)
            {
                plpNode.Children.Add(new TableTreeNode
                {
                    Kind = TableTreeNodeKind.Stream,
                    Label = "(no services — waiting for PAT)",
                    Payload = null
                });
            }
            else
            {
                foreach (var svc in services)
                {
                    var svcLabel = !string.IsNullOrWhiteSpace(svc.ServiceName)
                        ? $"Program {svc.ProgramNumber}: \"{svc.ServiceName}\""
                        : $"Program {svc.ProgramNumber} (service_id {svc.ServiceId})";

                    var svcNode = new TableTreeNode
                    {
                        Kind = TableTreeNodeKind.Stream,
                        Label = svcLabel,
                        IsExpanded = true,
                        Payload = svc
                    };

                    // Service metadata as child nodes
                    if (svc.PmtPid.HasValue)
                        svcNode.Children.Add(InfoNode($"PMT PID: 0x{svc.PmtPid.Value:X4}"));
                    if (svc.PcrPid.HasValue)
                        svcNode.Children.Add(InfoNode($"PCR PID: 0x{svc.PcrPid.Value:X4}"));
                    if (svc.ServiceType.HasValue)
                        svcNode.Children.Add(InfoNode($"Type: {svc.ServiceTypeName ?? svc.ServiceType.Value.ToString()}"));

                    // Elementary streams
                    if (svc.ElementaryStreams is { Count: > 0 })
                    {
                        foreach (var es in svc.ElementaryStreams)
                        {
                            svcNode.Children.Add(InfoNode(
                                $"ES PID 0x{es.ElementaryPid:X4} ({es.StreamTypeName ?? $"0x{es.StreamType:X2}"})"));
                        }
                    }

                    plpNode.Children.Add(svcNode);
                }
            }

            BumpRevision();
        }
    }

    private static string PlpNodeKey(ushort t2miPid, byte plpId) =>
        $"{t2miPid:X4}:{plpId}";

    private static (ushort, byte) PlpSortKey(TableTreeNode plpNode)
    {
        if (plpNode.Payload is ValueTuple<ushort, byte> key)
            return (key.Item1, key.Item2);

        return (0, 0);
    }

    private static TableTreeNode InfoNode(string label) => new()
    {
        Kind = TableTreeNodeKind.Descriptor,
        Label = label
    };

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
            IsExpanded = false,
            Payload = streamKey
        };

        _streamByKey[streamKey] = stream;
        category.Children.Add(stream);
        return stream;
    }

    private static void AddVersion(TableTreeNode stream, TsTableKind kind, Table table, ulong? pcrValue = null)
    {
        var versions = stream.Children;
        if (kind is TsTableKind.Tdt or TsTableKind.Tot)
        {
            versions.Clear();
        }
        else if (versions.Count > 0 && versions[^1].Payload is Table last && last.CRC32 == table.CRC32)
        {
            return;
        }

        // Capture the previously active version before marking it stale.
        TableTreeNode? superseded = null;
        if (pcrValue.HasValue && versions.Count > 0)
        {
            superseded = versions[^1];
        }

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

        if (superseded is not null)
            superseded.Label = $"{superseded.Label} ({FormatPcrTime(pcrValue!.Value)})";

        if (versions.Count > MaxVersionsPerGroup)
        {
            versions.RemoveAt(0);
            RenumberVersionLabels(versions, prefix);
        }
    }

    private static string FormatPcrTime(ulong pcr)
    {
        var ts = TimestampMath.PcrToTimeSpan(pcr);
        var hours = (int)Math.Min(ts.TotalHours, 99);
        return $"{hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
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
    }

    private void BumpRevision() => Revision++;
}
