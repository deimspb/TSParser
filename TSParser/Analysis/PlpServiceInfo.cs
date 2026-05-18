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

namespace TSParser.Analysis;

/// <summary>Aggregated DVB service (PAT + SDT + PMT) inside one PLP inner transport stream.</summary>
public sealed class PlpServiceInfo
{
    public ushort ProgramNumber { get; init; }
    public ushort? PmtPid { get; init; }
    public ushort? ServiceId { get; init; }
    public string? ServiceName { get; init; }
    public byte? ServiceType { get; init; }
    public string? ServiceTypeName { get; init; }
    public string? ServiceProviderName { get; init; }
    public ushort? PcrPid { get; init; }
    public IReadOnlyList<PlpElementaryStreamInfo> ElementaryStreams { get; init; } = [];
}
