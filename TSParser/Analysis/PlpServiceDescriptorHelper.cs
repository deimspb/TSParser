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

using TSParser.Descriptors;
using TSParser.Descriptors.Dvb;
using TSParser.Tables.DvbTables;

namespace TSParser.Analysis;

internal static class PlpServiceDescriptorHelper
{
    public static ServiceDescriptor_0x48? TryGetServiceDescriptor(IEnumerable<Descriptor>? descriptors)
    {
        if (descriptors is null)
            return null;

        foreach (var descriptor in descriptors)
        {
            if (descriptor is ServiceDescriptor_0x48 service)
                return service;
        }

        return null;
    }

    public static string? TryGetServiceName(IEnumerable<Descriptor>? descriptors)
    {
        var service = TryGetServiceDescriptor(descriptors);
        if (service is null)
            return null;

        return string.IsNullOrWhiteSpace(service.ServiceName)
            ? null
            : service.ServiceName.Trim();
    }

    public static IReadOnlyList<PlpElementaryStreamInfo> MapElementaryStreams(IEnumerable<EsInfo>? esList)
    {
        if (esList is null)
            return [];

        var result = new List<PlpElementaryStreamInfo>();
        foreach (var es in esList)
        {
            var typeLabel = HasDescriptorTag(es.EsDescriptorList, 0x6F) ? "AIT" : null;
            result.Add(new PlpElementaryStreamInfo(
                es.StreamType,
                es.StreamTypeName,
                es.ElementaryPid,
                typeLabel));
        }

        return result;
    }

    private static bool HasDescriptorTag(IEnumerable<Descriptor>? descriptors, byte tag)
    {
        if (descriptors is null)
            return false;

        foreach (var descriptor in descriptors)
        {
            if (descriptor?.DescriptorTag == tag)
                return true;
        }

        return false;
    }
}
