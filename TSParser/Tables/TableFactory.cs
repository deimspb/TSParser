// Copyright 2021 Eldar Nizamutdinov 
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

using TSParser.Service;
using TSParser.TransportStream;
using System.Runtime.InteropServices;

namespace TSParser.Tables
{
    internal abstract class TableFactory
    {
        internal ushort CurrentPid { get; set; } = 0xFFFF; // max pid value 0x1FFF

        internal byte[] TableData = null!;
        private readonly Dictionary<ushort, PsiSectionAssembler> assemblers = new();

        internal abstract void PushTable(TsPacket tsPacket);
        protected abstract void ProcessCurrentSection();

        internal void ResetFactory(ushort? pid = null)
        {
            if (pid.HasValue)
            {
                if (assemblers.TryGetValue(pid.Value, out var assembler))
                {
                    assembler.Reset();
                }
            }
            else
            {
                foreach (var assembler in assemblers.Values)
                {
                    assembler.Reset();
                }
            }

            TableData = null!;
        }

        internal bool TryParseAssembledTable(Action parseAction, string tableName)
        {
            try
            {
                parseAction();
                return true;
            }
            catch (SectionParseException ex)
            {
                Logger.Send(
                    LogStatus.EXCEPTION,
                    $"Failed to parse {tableName} section on PID 0x{CurrentPid:X4}: {ex.Message}",
                    ex);
                ResetFactory();
                return false;
            }
        }

        internal void ProcessAssembledSections(TsPacket tsPacket)
        {
            foreach (var section in PushPacketForSections(tsPacket))
            {
                TableData = GetSectionArray(section);
                ProcessCurrentSection();
            }
        }

        internal IReadOnlyList<ReadOnlyMemory<byte>> PushPacketForSections(TsPacket tsPacket)
        {
            if (!tsPacket.HasPayload || tsPacket.Payload.Length == 0)
            {
                return Array.Empty<ReadOnlyMemory<byte>>();
            }

            if (tsPacket.TransportScramblingControl != 0x00)
            {
                Logger.Send(LogStatus.WARNING, $"Atempt to bulid table from scrambled packet with pid: {tsPacket.Pid}");
                return Array.Empty<ReadOnlyMemory<byte>>();
            }

            CurrentPid = tsPacket.Pid;
            if (!assemblers.TryGetValue(CurrentPid, out var assembler))
            {
                assembler = new PsiSectionAssembler(CurrentPid);
                assemblers[CurrentPid] = assembler;
            }

            return assembler.PushPacket(tsPacket);
        }

        private static byte[] GetSectionArray(ReadOnlyMemory<byte> section)
        {
            if (MemoryMarshal.TryGetArray(section, out var segment)
                && segment.Offset == 0
                && segment.Array != null
                && segment.Count == segment.Array.Length)
            {
                return segment.Array;
            }

            return section.ToArray();
        }
    }
}
