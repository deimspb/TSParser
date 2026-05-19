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

using System;
using TSParser.Enums;

namespace TSParser;

/// <summary>Base exception for TSParser-specific failures.</summary>
public class TsParserException : Exception
{
    public TsParserException(string message) : base(message)
    {
    }

    public TsParserException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>Thrown when parser options or input source configuration is invalid.</summary>
public sealed class TsParserConfigurationException : TsParserException
{
    public TsParserConfigurationException(string message) : base(message)
    {
    }

    public TsParserConfigurationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>Thrown when a transport stream cannot be synchronized.</summary>
public sealed class TsSyncException : TsParserException
{
    public TsSyncException(string message) : base(message)
    {
    }
}

/// <summary>Thrown when a selected transport-stream mode is not implemented.</summary>
public sealed class UnsupportedTsModeException : TsParserException
{
    public UnsupportedTsModeException(TsMode mode)
        : base($"TS mode {mode} is not supported.")
    {
        Mode = mode;
    }

    public TsMode Mode { get; }
}
