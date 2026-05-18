// Copyright 2021 Eldar Nizamutdinov deim.mobile<at>gmail.com
//
// Licensed under the Apache License, Version 2.0 (the "License")
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace TSParser.Diagnostics;

public static class T2miDebugCounters
{
    public static int TsPacketsPushed;
    public static int TsPacketsSkippedRaw;
    public static int T2miPacketsCompleted;
    public static int BasebandCrcValid;
    public static int BasebandCrcInvalid;
    public static int DeencapBasebandAttempts;
    public static int BbReceiveCalls;
    public static int BbRejectedDflZero;
    public static int BbRejectedHeader;
    public static int PlpTsEmissions;
    public static int PlpTsBytesEmitted;
    public static int TypeDvbT2Timestamp;
    public static int TypeL1Current;
    public static int TypeBaseband;
    public static int TypeOther;
    public static int AssemblerPusiStarts;
    public static int AssemblerCcResets;
    public static int AssemblerBasebandCrcFail;
    public static int AssemblerPusiAbortedIncomplete;

    public static void Reset()
    {
        TsPacketsPushed = 0;
        TsPacketsSkippedRaw = 0;
        T2miPacketsCompleted = 0;
        BasebandCrcValid = 0;
        BasebandCrcInvalid = 0;
        DeencapBasebandAttempts = 0;
        BbReceiveCalls = 0;
        BbRejectedDflZero = 0;
        BbRejectedHeader = 0;
        PlpTsEmissions = 0;
        PlpTsBytesEmitted = 0;
        TypeDvbT2Timestamp = 0;
        TypeL1Current = 0;
        TypeBaseband = 0;
        TypeOther = 0;
        AssemblerPusiStarts = 0;
        AssemblerCcResets = 0;
        AssemblerBasebandCrcFail = 0;
        AssemblerPusiAbortedIncomplete = 0;
    }

    public static object Snapshot() => new
    {
        TsPacketsPushed,
        TsPacketsSkippedRaw,
        T2miPacketsCompleted,
        BasebandCrcValid,
        BasebandCrcInvalid,
        DeencapBasebandAttempts,
        BbReceiveCalls,
        BbRejectedDflZero,
        BbRejectedHeader,
        PlpTsEmissions,
        PlpTsBytesEmitted,
        TypeDvbT2Timestamp,
        TypeL1Current,
        TypeBaseband,
        TypeOther,
        AssemblerPusiStarts,
        AssemblerCcResets,
        AssemblerBasebandCrcFail,
        AssemblerPusiAbortedIncomplete,
    };
}
