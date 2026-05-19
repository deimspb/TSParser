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

namespace TSParser.Input;

internal sealed class TsInputSourceContext
{
    private readonly TsBytesReceived _bytesReceived;
    private readonly Action<long?> _setStreamByteOffset;
    private readonly Action _sourceStarted;
    private readonly Func<Exception, bool> _isExpectedShutdown;

    public TsInputSourceContext(
        CancellationToken cancellationToken,
        TsBytesReceived bytesReceived,
        Action sourceStarted,
        Action<long?> setStreamByteOffset,
        Func<Exception, bool> isExpectedShutdown)
    {
        CancellationToken = cancellationToken;
        _bytesReceived = bytesReceived;
        _sourceStarted = sourceStarted;
        _setStreamByteOffset = setStreamByteOffset;
        _isExpectedShutdown = isExpectedShutdown;
    }

    public CancellationToken CancellationToken { get; }

    public void Publish(ReadOnlySpan<byte> bytes, int packetLength)
    {
        _bytesReceived(bytes, packetLength);
    }

    public void SourceStarted()
    {
        _sourceStarted();
    }

    public void SetStreamByteOffset(long? offset)
    {
        _setStreamByteOffset(offset);
    }

    public bool IsExpectedShutdown(Exception exception)
    {
        return _isExpectedShutdown(exception);
    }
}
