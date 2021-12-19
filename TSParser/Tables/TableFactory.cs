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

using TSParser.TransportStream;

namespace TSParser.Tables
{
    internal abstract class TableFactory
    {
        internal ushort CurrentPid { get; set; } = 0xFFFF; // max pid value 0x1FFF

        internal byte[] TableData = null!;
        private int Pointer;
        private bool isInProgresTable;
        private byte[] tempBuffer = null!;
        private int CurrentTableSectionLength;
        private int CurrentTablePointerField;
        private int TableBytes;
        internal bool IsAllTable {
            get {
                return TableBytes >= CurrentTableSectionLength + 3 && CurrentTableSectionLength > 0;
            }
        }

        internal abstract void PushTable(TsPacket tsPacket);

        internal void AddData(TsPacket tsPacket)
        {
            if (CurrentPid == 0xFFFF) CurrentPid = tsPacket.Pid;
            if (CurrentPid != tsPacket.Pid) throw new Exception($"Pid changed from: {CurrentPid} to {tsPacket.Pid}");

            if (tsPacket.PayloadUnitStartIndicator)
            {
                Pointer = tsPacket.Payload[0];

                if (Pointer > tsPacket.Payload.Length)
                {
                    throw new Exception($"Pointer field greater than packet length for pid: {tsPacket.Pid}");
                }

                if (isInProgresTable)
                {
                    if (TableBytes < 3)
                    {
                        Buffer.BlockCopy(tsPacket.Payload, 1, tempBuffer, TableBytes, 3 - TableBytes);                        
                        CurrentTableSectionLength = (((tempBuffer[1] & 0x0F) << 8) + tempBuffer[2]);
                        if (CurrentTableSectionLength > Pointer)
                        {
                            throw new Exception(" section length greater than pointer!");
                        }
                        TableData = new byte[CurrentTableSectionLength + 3];
                        Buffer.BlockCopy(tempBuffer, 0, TableData, 0, 3);
                        var offsetInPayload = 1 + 3 - TableBytes;
                        var bytesToCopyCount = Pointer - (3 - TableBytes);
                        Buffer.BlockCopy(tsPacket.Payload, offsetInPayload, TableData, 3, bytesToCopyCount);

                        // table ready
                        isInProgresTable = false;
                        return;
                    }
                    else
                    {
                        Buffer.BlockCopy(tsPacket.Payload, 1, TableData, TableBytes, Pointer);
                        TableBytes += Pointer;

                        //table ready
                        isInProgresTable = false;
                        return;
                    }
                }

                isInProgresTable = true;
                CurrentTablePointerField = Pointer;

                if (Pointer >= tsPacket.Payload.Length - 3)
                {
                    TableBytes = tsPacket.Payload.Length - Pointer - 1;
                    tempBuffer = new byte[3];
                    Buffer.BlockCopy(tsPacket.Payload, Pointer + 1, tempBuffer, 0, TableBytes);
                    return;
                }
                else
                {
                    CurrentTableSectionLength = (((tsPacket.Payload[Pointer + 2] & 0x0F) << 8) + tsPacket.Payload[Pointer + 3]);
                    TableData = new byte[CurrentTableSectionLength + 3];

                    if (CurrentTableSectionLength + 3 < tsPacket.Payload.Length - Pointer)
                    {
                        Buffer.BlockCopy(tsPacket.Payload, Pointer + 1, TableData, 0, TableData.Length);
                        TableBytes = CurrentTableSectionLength + 3;

                        // table ready
                        isInProgresTable = false;
                        return;
                    }
                    else
                    {
                        Buffer.BlockCopy(tsPacket.Payload, Pointer + 1, TableData, 0, tsPacket.Payload.Length - Pointer - 1);
                        TableBytes = tsPacket.Payload.Length - Pointer - 1;
                        return;
                    }
                }
            }

            if (isInProgresTable)
            {
                if (TableBytes < 3)
                {
                    Buffer.BlockCopy(tsPacket.Payload, 0, tempBuffer, TableBytes, 3 - TableBytes);
                    CurrentTableSectionLength = (((tempBuffer[1] & 0x0F) << 8) + tempBuffer[2]);
                    TableData = new byte[CurrentTableSectionLength + 3];

                    if (CurrentTableSectionLength + 3 <= tsPacket.Payload.Length)
                    {
                        Buffer.BlockCopy(tempBuffer, 0, TableData, 0, 3);
                        CurrentTableSectionLength = 3;
                        Buffer.BlockCopy(tsPacket.Payload, 0, TableData, 3, CurrentTableSectionLength);
                        TableBytes = CurrentTableSectionLength + 3;

                        // table ready
                        isInProgresTable = false;
                        return;
                    }
                    else
                    {
                        Buffer.BlockCopy(tempBuffer, 0, TableData, 0, 3);
                        Buffer.BlockCopy(tsPacket.Payload, 3 - TableBytes, TableData, 3, tsPacket.Payload.Length - (3 - TableBytes));
                        TableBytes += tsPacket.Payload.Length;
                        return;
                    }
                }
                else if (CurrentTableSectionLength + 3 - TableBytes < tsPacket.Payload.Length)
                {
                    Buffer.BlockCopy(tsPacket.Payload, 0, TableData, TableBytes, CurrentTableSectionLength + 3 - TableBytes);
                    TableBytes = CurrentTableSectionLength + 3;

                    //table ready
                    isInProgresTable = false;
                    return;
                }
                else
                {
                    Buffer.BlockCopy(tsPacket.Payload, 0, TableData, TableBytes, tsPacket.Payload.Length);
                    TableBytes += tsPacket.Payload.Length;
                    return;
                }
            }
            else
            {
                return;
            }
        }
    }
}
