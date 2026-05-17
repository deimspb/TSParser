using TSParser.Descriptors;
using TSParser.Tables;

namespace TSParser.Web.Models;

public enum TableTreeNodeKind
{
    Category,
    Stream,
    Version,
    ElementaryStream,
    Descriptor,
    Pid
}

/// <summary>Single node in the SI table tree (category → stream → version → descriptors).</summary>
public sealed class TableTreeNode
{
    public Guid Id { get; } = Guid.NewGuid();

    public TableTreeNodeKind Kind { get; init; }

    public string Label { get; set; } = "";

    /// <summary>Latest CRC version in its group; older versions are stale (grey).</summary>
    public bool IsActive { get; set; } = true;

    public bool IsExpanded { get; set; }

    public object? Payload { get; init; }

    public List<TableTreeNode> Children { get; } = [];

    /// <summary>Version nodes with table-level descriptors build children on first expand.</summary>
    public bool DescriptorsLoaded { get; set; }

    public bool HasExpandableContent =>
        Kind is TableTreeNodeKind.Category or TableTreeNodeKind.Stream or TableTreeNodeKind.Version
            or TableTreeNodeKind.ElementaryStream;

    public Table? Table => Payload as Table;

    public Descriptor? Descriptor => Payload as Descriptor;
}
