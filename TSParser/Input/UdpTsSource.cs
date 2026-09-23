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

using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using TSParser.Service;

namespace TSParser.Input;

internal sealed class UdpTsSource : ITsInputSource
{
    private readonly UdpDatagramRecorder _recorder = new();
    private readonly IPAddress _multicastGroup;
    private readonly IPAddress _incomingIpInterface;
    private readonly int _multicastPort;
    private readonly int _socketTimeOut;
    private readonly object _socketLock = new();
    private int _connectionAttempts;
    private Socket? _socket;
    private Channel<byte[]>? _channel;
    private Task? _bufferReaderTask;

    public UdpTsSource(UdpSourceOptions source, int socketTimeout = 5000, int connectionAttempts = 5)
    {
        _recorder.Completed += result => RecordingCompleted?.Invoke(result);
        if (string.IsNullOrWhiteSpace(source.MulticastGroup))
        {
            throw new TsParserConfigurationException("UDP multicast group must be set.");
        }

        if (!IPAddress.TryParse(source.MulticastGroup, out var multicastGroup))
        {
            throw new TsParserConfigurationException($"Invalid multicast group: {source.MulticastGroup}");
        }

        IPAddress? incomingIpInterface = null;
        if (!string.IsNullOrWhiteSpace(source.IncomingIp) && !IPAddress.TryParse(source.IncomingIp, out incomingIpInterface))
        {
            throw new TsParserConfigurationException($"Invalid incoming IP address: {source.IncomingIp}");
        }

        _multicastGroup = multicastGroup;
        _incomingIpInterface = incomingIpInterface ?? IPAddress.Any;

        _multicastPort = source.MulticastPort ?? 1234;
        if (_multicastPort <= 1 || _multicastPort >= 65535)
        {
            throw new TsParserConfigurationException("Invalid port number.");
        }

        _socketTimeOut = socketTimeout;
        _connectionAttempts = connectionAttempts;
    }

    public event Action<UdpRecordingResult>? RecordingCompleted;

    public bool IsRecording => _recorder.IsRecording;

    public UdpRecordingStatus? RecordingStatus => _recorder.Status;

    public void StartRecording(UdpRecordingOptions options) => _recorder.Start(options);

    public void StopRecording() => _recorder.Stop();

    public void Run(TsInputSourceContext context)
    {
        var channel = CreateUdpChannel();
        _channel = channel;
        Exception? producerException = null;

        try
        {
            var bytesCount = 0;
            var udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            lock (_socketLock)
            {
                _socket = udpSocket;
            }

            IPEndPoint endPoint = new(_incomingIpInterface, _multicastPort);
            udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpSocket.Bind(endPoint);
            udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(_multicastGroup, _incomingIpInterface));
            udpSocket.ReceiveBufferSize = 1316 * 1000;
            udpSocket.ReceiveTimeout = _socketTimeOut;

            var bytes = new byte[1500];

            while (!context.CancellationToken.IsCancellationRequested)
            {
                if (_connectionAttempts <= 0)
                {
                    return;
                }

                try
                {
                    bytesCount = udpSocket.Receive(bytes);
                    break;
                }
                catch (Exception ex) when (context.IsExpectedShutdown(ex))
                {
                    return;
                }
                catch (Exception ex)
                {
                    Logger.Send(LogStatus.EXCEPTION, $"Receive exception attempts left: {_connectionAttempts}", ex);
                    _connectionAttempts--;
                }
            }

            if (context.CancellationToken.IsCancellationRequested)
            {
                return;
            }

            _recorder.Record(bytes.AsSpan(0, bytesCount));

            if (!TsPacketLengthDetector.TryResolveUdpTsPacketLength(bytesCount, out var packetLength))
            {
                Logger.Send(
                    LogStatus.EXCEPTION,
                    $"UDP datagram length {bytesCount} is not a valid multiple of 188 or 204 byte TS packets");
                return;
            }

            context.SourceStarted();
            Logger.Send(LogStatus.INFO, $"Start with network {_multicastGroup}:{_multicastPort} ts packet length: {packetLength}, network packet lenght: {bytesCount}");

            _bufferReaderTask = Task.Run(() => ReadFromBuffer(channel.Reader, packetLength, context), context.CancellationToken);

            WriteUdpDatagram(channel.Writer, bytes, bytesCount, context.CancellationToken);
            ThrowIfBufferReaderFaulted();

            _connectionAttempts = 5;
            while (!context.CancellationToken.IsCancellationRequested)
            {
                if (_connectionAttempts <= 0)
                {
                    return;
                }

                try
                {
                    ThrowIfBufferReaderFaulted();
                    var bytesLength = udpSocket.Receive(bytes);
                    RecordAndQueueDatagram(channel.Writer, bytes, bytesLength, context.CancellationToken);
                }
                catch (Exception ex) when (context.IsExpectedShutdown(ex))
                {
                    return;
                }
                catch (Exception ex)
                {
                    ThrowIfBufferReaderFaulted();
                    Logger.Send(LogStatus.EXCEPTION, $"Receive exception attempts left: {_connectionAttempts}", ex);
                    _connectionAttempts--;
                }
            }
        }
        catch (Exception ex) when (context.IsExpectedShutdown(ex))
        {
        }
        catch (Exception ex)
        {
            producerException = ex;
            Logger.Send(LogStatus.EXCEPTION, "Exception in Run UDP parser", ex);
            throw;
        }
        finally
        {
            channel.Writer.TryComplete(producerException);
            try
            {
                if (producerException == null)
                {
                    WaitForBufferReaderTask(context);
                }
                else
                {
                    ObserveBufferReaderTask(context);
                }
            }
            finally
            {
                _recorder.Stop(UdpRecordingStopReason.SourceStopped);
                if (ReferenceEquals(_channel, channel))
                {
                    _channel = null;
                }

                CloseSocket();
            }
        }
    }

    public void Stop()
    {
        _recorder.Stop(UdpRecordingStopReason.SourceStopped);
        _channel?.Writer.TryComplete();
        CloseSocket();
    }

    public void Dispose()
    {
        Stop();
        _recorder.Dispose();
    }

    internal static bool TryResolveUdpTsPacketLength(int datagramByteCount, out int packetLength)
    {
        return TsPacketLengthDetector.TryResolveUdpTsPacketLength(datagramByteCount, out packetLength);
    }

    private static Channel<byte[]> CreateUdpChannel()
    {
        return Channel.CreateBounded<byte[]>(new BoundedChannelOptions(5000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        });
    }

    private static void WriteUdpDatagram(ChannelWriter<byte[]> writer, byte[] bytes, int bytesCount, CancellationToken cancellationToken)
    {
        var datagram = new byte[bytesCount];
        Buffer.BlockCopy(bytes, 0, datagram, 0, bytesCount);
        writer.WriteAsync(datagram, cancellationToken).AsTask().GetAwaiter().GetResult();
    }

    private void RecordAndQueueDatagram(ChannelWriter<byte[]> writer, byte[] bytes, int bytesCount, CancellationToken cancellationToken)
    {
        _recorder.Record(bytes.AsSpan(0, bytesCount));
        WriteUdpDatagram(writer, bytes, bytesCount, cancellationToken);
    }

    private static async Task ReadFromBuffer(ChannelReader<byte[]> reader, int packetLength, TsInputSourceContext context)
    {
        await foreach (var datagram in reader.ReadAllAsync(context.CancellationToken).ConfigureAwait(false))
        {
            context.Publish(datagram, packetLength);
        }
    }

    private void ThrowIfBufferReaderFaulted()
    {
        if (_bufferReaderTask?.IsFaulted == true)
        {
            _bufferReaderTask.GetAwaiter().GetResult();
        }
    }

    private void WaitForBufferReaderTask(TsInputSourceContext context)
    {
        if (_bufferReaderTask == null)
        {
            return;
        }

        try
        {
            _bufferReaderTask.GetAwaiter().GetResult();
        }
        catch (Exception ex) when (context.IsExpectedShutdown(ex))
        {
        }
    }

    private void ObserveBufferReaderTask(TsInputSourceContext context)
    {
        try
        {
            WaitForBufferReaderTask(context);
        }
        catch (Exception ex)
        {
            Logger.Send(LogStatus.EXCEPTION, "Exception while waiting for UDP buffer reader", ex);
        }
    }

    private void CloseSocket()
    {
        Socket? socket;
        lock (_socketLock)
        {
            socket = _socket;
            _socket = null;
        }

        if (socket == null)
        {
            return;
        }

        try
        {
            socket.Close();
        }
        catch (Exception ex)
        {
            Logger.Send(LogStatus.EXCEPTION, "Exception while closing UDP socket", ex);
        }
    }
}
