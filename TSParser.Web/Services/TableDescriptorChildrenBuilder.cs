using TSParser.Descriptors;
using TSParser.Tables;
using TSParser.Tables.DvbTables;
using TSParser.Web.Models;

namespace TSParser.Web.Services;

internal static class TableDescriptorChildrenBuilder
{
    public static void LoadDescriptorChildren(TableTreeNode versionNode)
    {
        if (versionNode.Kind != TableTreeNodeKind.Version || versionNode.DescriptorsLoaded)
            return;

        if (versionNode.Table is not { } table)
        {
            versionNode.DescriptorsLoaded = true;
            return;
        }

        versionNode.Children.Clear();
        AppendTableDescriptors(versionNode.Children, table);
        versionNode.DescriptorsLoaded = true;
    }

    private static void AppendTableDescriptors(List<TableTreeNode> parent, Table table)
    {
        switch (table)
        {
            case PMT pmt:
                AppendDescriptorList(parent, pmt.PmtDescriptorList, "Program descriptors");
                if (pmt.EsInfoList is { Count: > 0 } esList)
                {
                    foreach (var es in esList)
                    {
                        var esNode = new TableTreeNode
                        {
                            Kind = TableTreeNodeKind.ElementaryStream,
                            Label = $"ES 0x{es.ElementaryPid:X4} ({es.StreamTypeName})",
                            Payload = es
                        };
                        AppendDescriptorList(esNode.Children, es.EsDescriptorList, null);
                        parent.Add(esNode);
                    }
                }
                break;

            case CAT cat:
                AppendDescriptorList(parent, cat.CatDescriptorList, "CAT descriptors");
                break;

            case NIT nit:
                AppendDescriptorList(parent, nit.NitDescriptorList, "Network descriptors");
                break;

            case BAT bat:
                AppendDescriptorList(parent, bat.BatDescriptorList, "Bouquet descriptors");
                break;

            case TOT tot:
                AppendDescriptorList(parent, tot.TotDescriptors, "TOT descriptors");
                break;

            case AIT ait:
                AppendDescriptorList(parent, ait.AitDescriptorsList, "Common descriptors");
                break;

            case SCTE35 scte:
                AppendDescriptorList(parent, scte.SpliceDescriptorItems, "Splice descriptors");
                break;

            case EWS ews:
                AppendDescriptorList(parent, ews.EwsDescriptorList, "EWS descriptors");
                break;

            case EEWS eews:
                AppendDescriptorList(parent, eews.EewsDescriptorList, "EEWS descriptors");
                break;
        }
    }

    private static void AppendDescriptorList(
        List<TableTreeNode> parent,
        List<Descriptor>? descriptors,
        string? groupLabel)
    {
        if (descriptors is not { Count: > 0 })
            return;

        var target = parent;
        if (!string.IsNullOrEmpty(groupLabel))
        {
            var folder = new TableTreeNode
            {
                Kind = TableTreeNodeKind.Descriptor,
                Label = $"{groupLabel} ({descriptors.Count})",
                IsExpanded = true
            };
            parent.Add(folder);
            target = folder.Children;
        }

        for (var i = 0; i < descriptors.Count; i++)
        {
            var desc = descriptors[i];
            target.Add(new TableTreeNode
            {
                Kind = TableTreeNodeKind.Descriptor,
                Label = $"0x{desc.DescriptorTag:X2} {desc.DescriptorName}",
                Payload = desc
            });
        }
    }
}
