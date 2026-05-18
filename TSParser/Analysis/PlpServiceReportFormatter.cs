// Copyright 2021 Eldar Nizamutdinov deim.mobile<at>gmail.com
//
// Licensed under the Apache License, Version 2.0 (the "License")
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Text;

namespace TSParser.Analysis;

/// <summary>Human-readable stdout report for services discovered inside PLP inner transport streams.</summary>
public static class PlpServiceReportFormatter
{
    /// <summary>
    /// Writes a report grouped by T2-MI PID and PLP ID. Returns true when at least one PLP delivered inner TS.
    /// </summary>
    public static bool Write(TextWriter writer, PlpServiceAggregator aggregator, IReadOnlyList<ushort> t2miPids)
    {
        var anyPlpTs = false;

        foreach (var t2miPid in t2miPids.OrderBy(p => p))
        {
            writer.WriteLine($"T2-MI PID 0x{t2miPid:X4}");

            var plpKeys = aggregator.GetPlpKeys()
                .Where(k => k.T2miPid == t2miPid)
                .Select(k => k.PlpId)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            if (plpKeys.Count == 0)
            {
                writer.WriteLine("  (no PLP baseband received)");
                continue;
            }

            foreach (var plpId in plpKeys)
            {
                if (aggregator.HasReceivedPlpTs(t2miPid, plpId))
                    anyPlpTs = true;

                writer.WriteLine($"  PLP {plpId}");

                var services = aggregator.GetServices(t2miPid, plpId);
                if (services.Count == 0)
                {
                    writer.WriteLine("    (no services — PAT not received)");
                    continue;
                }

                foreach (var service in services)
                    WriteService(writer, service);
            }
        }

        return anyPlpTs;
    }

    private static void WriteService(TextWriter writer, PlpServiceInfo service)
    {
        var sb = new StringBuilder();
        sb.Append($"    Program {service.ProgramNumber}");

        if (service.ServiceId is { } sid)
            sb.Append($" (service_id {sid})");

        if (!string.IsNullOrWhiteSpace(service.ServiceName))
            sb.Append($": \"{service.ServiceName}\"");

        if (service.ServiceType is { } st)
        {
            sb.Append(" [0x");
            sb.Append(st.ToString("X2"));
            if (!string.IsNullOrWhiteSpace(service.ServiceTypeName))
            {
                sb.Append(' ');
                sb.Append(service.ServiceTypeName);
            }

            sb.Append(']');
        }

        if (service.PmtPid is { } pmtPid)
            sb.Append($" PMT 0x{pmtPid:X4}");

        if (service.PcrPid is { } pcrPid)
            sb.Append($" PCR 0x{pcrPid:X4}");

        writer.WriteLine(sb.ToString());

        foreach (var es in service.ElementaryStreams)
            WriteElementaryStream(writer, es);
    }

    private static void WriteElementaryStream(TextWriter writer, PlpElementaryStreamInfo es)
    {
        var sb = new StringBuilder();
        sb.Append($"      ES 0x{es.ElementaryPid:X4}");

        if (!string.IsNullOrWhiteSpace(es.TypeLabel))
        {
            sb.Append(' ');
            sb.Append(es.TypeLabel);
        }
        else if (!string.IsNullOrWhiteSpace(es.StreamTypeName))
        {
            sb.Append(' ');
            sb.Append(es.StreamTypeName);
        }

        writer.WriteLine(sb.ToString());
    }
}
