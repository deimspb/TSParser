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

using System.Threading;
using System.Net.Sockets;
using System.Threading.Channels;
using TSParser.Analysis;
using TSParser.Comparer;
using TSParser.Descriptors;
using TSParser.Enums;
using TSParser.Input;
using TSParser.Routing;
using TSParser.Service;
using TSParser.Tables;
using TSParser.Tables.DvbTables;
using TSParser.Tables.Mip;
using TSParser.TransportStream;
using TSParser.TransportStream.T2mi;

namespace TSParser
{
    public class ParserConfig
    {
        /// <summary>
        /// Allow analyze packets
        /// </summary>
        public bool AllowAnalyzer;
        /// <summary>
        /// Bitrate measurement settings. When set and <see cref="BitrateMeasurementOptions.Enabled"/> is
        /// <see langword="true"/>, the analyzer is active regardless of <see cref="AllowAnalyzer"/>.
        /// </summary>
        public BitrateMeasurementOptions? BitrateMeasurement;
        /// <summary>
        /// Set parser run time. If you read big ts file or udp mcast
        /// </summary>
        public int? ParserRunTime;
        /// <summary>
        /// Current Ts Mode. Only DVB supported yet
        /// </summary>
        public TsMode CurrentTsMode = TsMode.DVB;
        /// <summary>
        /// Current decoder mode. Table or packet
        /// </summary>
        public DecodeMode CurrentDecodeMode = DecodeMode.Packet;
        /// <summary>
        /// TS file name. Length shall be > 2048 bytes
        /// </summary>
        public string? TsFileName;
        /// <summary>
        /// Mulitast group address
        /// </summary>
        public string? MulticastGroup;
        /// <summary>
        /// Multicast destination port number
        /// </summary>
        public int? MulticastPort;
        /// <summary>
        /// Interface which listen to network with multicast
        /// </summary>
        public string? MulticastIncomingIp;
        /// <summary>When true, reassemble and parse T2-MI on configured or auto-detected PIDs.</summary>
        public bool T2miEnabled;
        /// <summary>Explicit T2-MI PID list (e.g. 0x1000). Used when <see cref="T2miEnabled"/> is true.</summary>
        public ushort[]? T2miPids;
        /// <summary>After PAT/PMT, register a PID when there is one program, one ES, and stream type 0x06.</summary>
        public bool T2miAutoDetect;
        /// <summary>When true with <see cref="T2miEnabled"/>, de-encapsulate baseband frames to MPEG-TS per PLP.</summary>
        public bool T2miDeencapsulate;

        /// <summary>TR 101 290 monitoring options. Null leaves monitoring disabled.</summary>
        public Tr101290Options? Tr101290;

    }

    public delegate void TsPacketReady(TsPacket tsPacket);
    public delegate void PatReady(PAT pat);
    public delegate void PmtReady(PMT pmt);
    public delegate void CatReady(CAT cat);
    public delegate void SdtReady(SDT sdt);
    public delegate void NitReady(NIT nit);
    public delegate void BatReady(BAT bat);
    public delegate void EitReady(EIT eit);
    public delegate void TdtReady(TDT tdt);
    public delegate void TotReady(TOT tot);
    public delegate void AitReady(AIT ait);
    public delegate void MipReady(MIP mip);
    public delegate void Scte35Ready(SCTE35 scte35);
    public delegate void ParserComplete();
    public delegate void EwsReady(EWS ews);
    public delegate void EewsReady(EEWS eews);
    public delegate void T2miPacketReady(T2miPacket packet);
    public delegate void T2miPlpDiscovered(byte plpId);
    public delegate void PlpTsReady(ushort t2miSourcePid, byte plpId, ReadOnlyMemory<byte> tsData);
    public delegate void PcrTimestampChange(ulong pcrValue);

    public class TsParser : IDisposable
    {
        #region Private fields
        private delegate void ParserModeDelefate(ReadOnlySpan<byte> bytes, int packetLen);

        private ParserModeDelefate ParserModeDel = (_, _) => throw new TsParserConfigurationException("No parser decode mode configured.");

        private readonly object _totReadyEventLock = new();
        private TotReady? _onTotReady;

        public event ParserComplete? OnParserComplete;
        public event PatReady? OnPatReady;
        public event PmtReady? OnPmtReady;
        public event EitReady? OnEitReady;
        public event TdtReady? OnTdtReady;
        public event TotReady? OnTotReady
        {
            add
            {
                lock (_totReadyEventLock)
                {
                    _onTotReady += value;
                }
            }
            remove
            {
                lock (_totReadyEventLock)
                {
                    _onTotReady -= value;
                }
            }
        }

        [Obsolete("Use OnTotReady instead.")]
        public event TotReady? OnTotready
        {
            add
            {
                lock (_totReadyEventLock)
                {
                    _onTotReady += value;
                }
            }
            remove
            {
                lock (_totReadyEventLock)
                {
                    _onTotReady -= value;
                }
            }
        }

        public event SdtReady? OnSdtReady;
        public event BatReady? OnBatReady;
        public event CatReady? OnCatReady;
        public event NitReady? OnNitReady;
        public event AitReady? OnAitReady;
        public event MipReady? OnMipReady;
        public event Scte35Ready? OnScte35Ready;
        public event TsPacketReady? OnTsPacketReady;
        public event RateDelegate? OnRate;
        /// <summary>Raised when a bitrate measurement window completes (requires <see cref="ParserOptions.BitrateMeasurement"/>).</summary>
        public event BitrateMeasuredDelegate? OnBitrateMeasured;
        public event EwsReady? OnEwsReady;
        public event EewsReady? OnEewsReady;
        public event T2miPacketReady? OnT2miPacketReady;
        public event T2miPlpDiscovered? OnT2miPlpDiscovered;
        public event PlpTsReady? OnPlpTsReady;
        /// <summary>Raised on each PCR timestamp change (same PCR-PID used for bitrate).</summary>
        public event PcrTimestampChange? OnPcrTimestampChange;
        /// <summary>Raised when a TR 101 290 indicator changes state. Requires <see cref="ParserOptions.Tr101290"/>.</summary>
        public event Action<Tr101290Event>? OnTr101290Event;
        /// <summary>Raised once when an active raw UDP recording ends.</summary>
        public event Action<UdpRecordingResult>? OnUdpRecordingCompleted;

        private Lazy<TsPacketFactory> packetFactory = new();
        private Lazy<Analyzer> analyzer;
        private readonly BitrateMeasurementOptions? m_bitrateMeasurement;
        private readonly Lazy<Compare> compare = new();
        private readonly DvbTableRouter m_tableRouter;
        private readonly PushTsSource m_pushSource = new();
        private ITsInputSource m_inputSource;

        private TsPacketFactory m_tsPacketFactory => packetFactory.Value;
        private Analyzer m_analyzer => analyzer.Value;
        private Compare m_compare => compare.Value;

        public readonly byte[] PacketSize = new byte[] { 188, 204 };

        private CancellationTokenSource m_cts = new();
        private CancellationToken m_ct;

        private bool m_disposed;

        private Task? m_parserTask;
        private readonly SemaphoreSlim m_operationGate = new(1, 1);
        private readonly AsyncLocal<bool> m_insideOperation = new();
        private int m_resourcesDisposed;

        private int? m_parserRunTimeIn_ms = null;
        private bool m_allowAnalyzer;
        private bool m_t2miEnabled;
        private readonly Tr101290Monitor? m_monitor;
        private readonly bool m_monitorUsesRouterSections;
        private long? m_fileStreamByteOffset;
        private System.Timers.Timer? m_timer;
        private int? MaxParserRunTime
        {
            get => m_parserRunTimeIn_ms;
            set
            {
                if (value < 100) throw new TsParserConfigurationException("Parser run time must be at least 100 ms.");
                m_parserRunTimeIn_ms = value;
            }
        }
        #endregion
        #region Public methods
        /// <summary>
        /// Gets or sets the list of EWS property identifiers (PIDs) as unsigned 16-bit integers.
        /// </summary>
        /// <remarks>The list represents EWS PIDs used to identify specific entities in the EWS context.
        /// When setting this property, ensure that the value is not null to avoid unexpected behavior.</remarks>
        public List<ushort> EwsPidList
        {
            set
            {
                m_tableRouter.EwsPidList = value;
            }

            get
            {
                return m_tableRouter.EwsPidList;
            }
        }
        /// <summary>
        /// Gets or sets the list of EEWS PIDs represented as unsigned 16-bit integers.
        /// </summary>
        /// <remarks>The EEWS PID list is used to identify specific entities within the system. When
        /// setting this property, ensure that the provided list is not null to avoid unexpected behavior.</remarks>
        public List<ushort> EewsPidList
        {
            set
            {
                m_tableRouter.EewsPidList = value;
            }
            get
            {
                return m_tableRouter.EewsPidList;
            }
        }
        /// <summary>
        /// Return pid list from analyzer
        /// </summary>
        public List<ushort> PidList
        {
            get => m_analyzer.PidList;
        }
        /// <summary>Creates a parser from legacy mutable configuration.</summary>
        public TsParser(ParserConfig config)
            : this(ParserOptions.FromParserConfig(config))
        {
        }

        /// <summary>Creates a parser from immutable options.</summary>
        public TsParser(ParserOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            m_ct = m_cts.Token;
            switch (options.CurrentDecodeMode)
            {
                case DecodeMode.Packet: ParserModeDel = ParseBytesToPackets; break;
                case DecodeMode.Table: ParserModeDel = ParseBytesToTables; break;
            }

            m_bitrateMeasurement = options.BitrateMeasurement;
            m_allowAnalyzer = options.AllowAnalyzer || (m_bitrateMeasurement?.Enabled ?? false);
            m_t2miEnabled = options.T2mi.Enabled;
            var monitorClockMode = options.UdpSource != null
                ? Tr101290ClockMode.Udp
                : options.TsFileName != null
                    ? Tr101290ClockMode.File
                    : Tr101290ClockMode.Push;
            m_monitor = options.Tr101290.Enabled ? new Tr101290Monitor(options.Tr101290, monitorClockMode) : null;
            m_monitorUsesRouterSections = m_monitor != null && options.CurrentDecodeMode == DecodeMode.Table;
            analyzer = new Lazy<Analyzer>(() => new Analyzer(m_bitrateMeasurement));
            m_tableRouter = new DvbTableRouter(options.CurrentTsMode, options.T2mi);
            m_inputSource = m_pushSource;
            MaxParserRunTime = GetParserRunTimeMilliseconds(options.ParserRunTime);

            ParserRunTimer();

            InitEvents();

            if (options.T2mi.Enabled && options.T2mi.Pids.Count > 0)
            {
                m_tableRouter.RegisterT2miPids(options.T2mi.Pids);
            }

            if (options.TsFileName != null)
            {
                m_inputSource = new FileTsSource(options.TsFileName);
                return;
            }

            if (options.UdpSource != null)
            {
                var udpSource = new UdpTsSource(options.UdpSource);
                udpSource.RecordingCompleted += result => OnUdpRecordingCompleted?.Invoke(result);
                m_inputSource = udpSource;
                return;
            }
        }
        /// <summary>
        /// Run parser in synchronous mode
        /// </summary>

        public TsParser()
            : this(ParserOptions.Default)
        {
        }

        /// <summary>Starts recording complete raw datagrams from the active UDP source.</summary>
        public void StartUdpRecording(UdpRecordingOptions options)
        {
            ObjectDisposedException.ThrowIf(m_disposed, this);
            if (m_inputSource is not UdpTsSource udpSource)
                throw new InvalidOperationException("UDP recording requires an active UDP source.");
            udpSource.StartRecording(options);
        }

        /// <summary>Stops the active UDP recording, if any.</summary>
        public void StopUdpRecording()
        {
            if (m_inputSource is UdpTsSource udpSource)
                udpSource.StopRecording();
        }

        public bool IsUdpRecording => m_inputSource is UdpTsSource { IsRecording: true };

        public UdpRecordingStatus? UdpRecordingStatus => (m_inputSource as UdpTsSource)?.RecordingStatus;

        public void Dispose()
        {
            if (m_disposed)
                return;

            m_disposed = true;

            try
            {
                if (!m_cts.IsCancellationRequested)
                    m_cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            m_inputSource.Stop();

            if (m_insideOperation.Value)
                return;

            WaitForParserTasks();

            m_operationGate.Wait();
            try
            {
                DisposeResources();
            }
            finally
            {
                m_operationGate.Release();
            }
        }

        public void RunParser()
        {
            RunParserAsync().GetAwaiter().GetResult();
        }
        /// <summary>
        /// Run parser in async mode
        /// </summary>
        /// <returns></returns>
        public async Task RunParserAsync()
        {
            ObjectDisposedException.ThrowIf(m_disposed, this);
            if (!m_operationGate.Wait(0))
                throw new InvalidOperationException("Another parser operation is already in progress.");

            Exception? parserException = null;
            try
            {
                ObjectDisposedException.ThrowIf(m_disposed, this);
                m_insideOperation.Value = true;
                EnsureCancellationTokenReady();
                ResetStreamStateForRun();

                var inputContext = CreateInputSourceContext();
                var parserTask = Task.Run(() => m_inputSource.Run(inputContext), m_ct);
                m_parserTask = parserTask;
                await parserTask.ConfigureAwait(false);
            }
            catch (Exception ex) when (IsExpectedParserShutdown(ex))
            {
            }
            catch (Exception ex)
            {
                parserException = ex;
                throw;
            }
            finally
            {
                try
                {
                    CompleteParserRun(parserException);
                }
                finally
                {
                    try
                    {
                        if (m_disposed)
                            DisposeResources();
                    }
                    finally
                    {
                        m_insideOperation.Value = false;
                        m_operationGate.Release();
                    }
                }
            }
        }
        /// <summary>
        /// Stop parser
        /// </summary>
        public void StopParser()
        {
            if (m_disposed)
                return;

            if (!m_cts.IsCancellationRequested)
                m_cts.Cancel();

            m_inputSource.Stop();
        }
        /// <summary>
        /// Push bytes with known ts packet size. 188 or 204 bytes
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="packetLength"></param>
        public void PushBytes(byte[] bytes, int packetLength)
        {
            ObjectDisposedException.ThrowIf(m_disposed, this);
            if (!m_operationGate.Wait(0))
                throw new InvalidOperationException("Another parser operation is already in progress.");

            try
            {
                ObjectDisposedException.ThrowIf(m_disposed, this);
                m_insideOperation.Value = true;
                m_pushSource.Push(bytes, packetLength, CreateInputSourceContext());
            }
            finally
            {
                try
                {
                    if (m_disposed)
                        DisposeResources();
                }
                finally
                {
                    m_insideOperation.Value = false;
                    m_operationGate.Release();
                }
            }
        }
        /// <summary>
        /// Return ts packet array parsed from bytes.
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="packetLength"></param>
        /// <returns></returns>
        public TsPacket[] GetTsPacketsFromBytes(ReadOnlySpan<byte> bytes, int packetLength)
        {
            return m_tsPacketFactory.GetTsPackets(bytes, packetLength);
        }
        /// <summary>
        /// Return ONE ts packet from bytes. Incoming bytes shall be 188 or 204 bytes length. Use it for tests or in lab 
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="packetLength"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public TsPacket GetOneTsPacketFromBytes(ReadOnlySpan<byte> bytes, int packetLength)
        {
            if (bytes.Length != 188 && bytes.Length != 204)
                throw new ArgumentException("Bytes length shall be 188 or 204 bytes.", nameof(bytes));

            if (packetLength != bytes.Length)
                throw new ArgumentException("Packet length must match bytes length.", nameof(packetLength));

            return m_tsPacketFactory.GetTsPacket(bytes, packetLength);
        }
        /// <summary>
        /// Return ONE table from incoming bytes. Bytes length shall be less than 4093 bytes. Use it for tests or in lab
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="mip">When true, parse as DVB-T MIP (PID 0x15); first byte is synchronization id, not MPEG table_id.</param>
        /// <returns></returns>
        /// <exception cref="TsParserException"></exception>
        public static Table GetOneTableFromBytes(ReadOnlySpan<byte> bytes, bool mip = false)
        {
            if (mip)
                return new MIP(bytes);

            return bytes[0] switch
            {
                0x00 => new PAT(bytes),
                0x01 => new CAT(bytes),
                0x02 => new PMT(bytes),
                0x74 => new AIT(bytes),
                0x4A => new BAT(bytes),
                0x70 => new TDT(bytes),
                0x73 => new TOT(bytes),
                0x93 => new EWS(bytes, 0),
                0x94 => new EEWS(bytes, 0),
                0x95 => new EEWS(bytes, 0),
                0xFC => new SCTE35(bytes),
                byte n when n == 0x42 || n == 0x46 => new SDT(bytes),
                byte n when n == 0x40 || n == 0x41 => new NIT(bytes),
                byte n when n == 0x4F || n == 0x4E || (n >= 0x50 && n <= 0x5F) || (n >= 0x60 && n <= 0x6F) => new EIT(bytes),
                _ => throw new TsParserException($"Unknown table id: 0x{bytes[0]:X2}"),
            };
        }
        /// <summary>
        /// Return ONE descriptor from incoming bytes. Bytes length shall be less than 255 bytes. use for tests or in lab
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="callerTableId">AIT (0x74) or SCTE-35 (0xFC) table_id when the tag namespace differs from DVB SI.</param>
        /// <returns></returns>
        public static Descriptor GetOneDescriptorFromBytes(ReadOnlySpan<byte> bytes, byte? callerTableId = null)
        {
            return callerTableId switch
            {
                0x74 => DescriptorFactory.GetAitDescriptor(bytes),
                0xFC => DescriptorFactory.GetSpliceDescriptor(bytes),
                _ => DescriptorFactory.GetDescriptor(bytes),
            };
        }
        /// <summary>
        /// Compare two table and return difference between them as ienumerable<string>
        /// </summary>
        /// <param name="t1"></param>
        /// <param name="t2"></param>
        /// <returns></returns>
        public IEnumerable<string> CompareTables(Table? t1, Table? t2)
        {
            return m_compare.AreEqual(t1, t2);
        }
        /// <summary>Creates a standalone T2-MI demuxer for one transport PID (lab / unit tests).</summary>
        public static T2miDemuxer CreateT2miDemuxer(ushort pid, bool deencapsulate = false) => new(pid, deencapsulate);
        #endregion
        #region Private methods
        private static int? GetParserRunTimeMilliseconds(TimeSpan? parserRunTime)
        {
            if (!parserRunTime.HasValue)
            {
                return null;
            }

            if (parserRunTime.Value.TotalMilliseconds > int.MaxValue)
            {
                throw new TsParserConfigurationException("Parser run time is too large.");
            }

            return (int)parserRunTime.Value.TotalMilliseconds;
        }

        private TsInputSourceContext CreateInputSourceContext()
        {
            return new TsInputSourceContext(
                m_ct,
                ParserModeDel.Invoke,
                StartParserTimer,
                offset => m_fileStreamByteOffset = offset,
                IsExpectedParserShutdown);
        }

        private void StartParserTimer()
        {
            if (m_timer != null && !m_timer.Enabled)
            {
                m_timer.Enabled = true;
            }
        }

        private void ParserRunTimer()
        {
            if (m_parserRunTimeIn_ms != null)
            {
                m_timer = new System.Timers.Timer
                {
                    Interval = (double)m_parserRunTimeIn_ms,
                };
                m_timer.Elapsed += Timer_Elapsed;
            }
        }
        private void InitEvents()
        {
            m_tableRouter.OnPatReady += pat => OnPatReady?.Invoke(pat);
            m_tableRouter.OnPmtReady += pmt => OnPmtReady?.Invoke(pmt);
            m_tableRouter.OnEitReady += eit => OnEitReady?.Invoke(eit);
            m_tableRouter.OnTdtReady += tdt => OnTdtReady?.Invoke(tdt);
            m_tableRouter.OnTotReady += tot => _onTotReady?.Invoke(tot);
            m_tableRouter.OnSdtReady += sdt => OnSdtReady?.Invoke(sdt);
            m_tableRouter.OnBatReady += bat => OnBatReady?.Invoke(bat);
            m_tableRouter.OnCatReady += cat => OnCatReady?.Invoke(cat);
            m_tableRouter.OnNitReady += nit => OnNitReady?.Invoke(nit);
            m_tableRouter.OnAitReady += ait => OnAitReady?.Invoke(ait);
            m_tableRouter.OnMipReady += mip => OnMipReady?.Invoke(mip);
            m_tableRouter.OnScte35Ready += scte35 => OnScte35Ready?.Invoke(scte35);
            m_tableRouter.OnEwsReady += ews => OnEwsReady?.Invoke(ews);
            m_tableRouter.OnEewsReady += eews => OnEewsReady?.Invoke(eews);
            m_tableRouter.OnT2miPacketReady += packet => OnT2miPacketReady?.Invoke(packet);
            m_tableRouter.OnT2miPlpDiscovered += plpId => OnT2miPlpDiscovered?.Invoke(plpId);
            m_tableRouter.OnPlpTsReady += (pid, plpId, data) => OnPlpTsReady?.Invoke(pid, plpId, data);
            m_analyzer.OnRate += Analyzer_OnRate;
            m_analyzer.OnBitrateMeasured += Analyzer_OnBitrateMeasured;
            m_analyzer.OnTimeStampChange += Analyzer_OnPcrTimestampChange;
            if (m_monitor != null)
                AttachMonitor();
        }

        private void AttachMonitor()
        {
            m_monitor!.OnEvent += measurement => OnTr101290Event?.Invoke(measurement);
            if (m_monitorUsesRouterSections)
                m_tableRouter.SetSectionAssembledHandler((pid, section) => m_monitor.ObserveSection(pid, section.Span));
        }

        private void ResetStreamStateForRun()
        {
            m_monitor?.Reset();
            m_tableRouter.ResetStreamState();
            packetFactory = new Lazy<TsPacketFactory>();

            if (analyzer.IsValueCreated)
            {
                m_analyzer.OnRate -= Analyzer_OnRate;
                m_analyzer.OnBitrateMeasured -= Analyzer_OnBitrateMeasured;
                m_analyzer.OnTimeStampChange -= Analyzer_OnPcrTimestampChange;
            }

            analyzer = new Lazy<Analyzer>(() => new Analyzer(m_bitrateMeasurement));
            m_analyzer.OnRate += Analyzer_OnRate;
            m_analyzer.OnBitrateMeasured += Analyzer_OnBitrateMeasured;
            m_analyzer.OnTimeStampChange += Analyzer_OnPcrTimestampChange;
            m_fileStreamByteOffset = null;
        }

        private void DisposeResources()
        {
            if (Interlocked.Exchange(ref m_resourcesDisposed, 1) != 0)
                return;

            m_inputSource.Dispose();

            if (analyzer.IsValueCreated)
            {
                m_analyzer.OnRate -= Analyzer_OnRate;
                m_analyzer.OnBitrateMeasured -= Analyzer_OnBitrateMeasured;
                m_analyzer.OnTimeStampChange -= Analyzer_OnPcrTimestampChange;
            }

            if (m_timer != null)
            {
                m_timer.Elapsed -= Timer_Elapsed;
                m_timer.Dispose();
                m_timer = null;
            }

            m_cts.Dispose();
        }
        private void Analyzer_OnRate(ushort pid, ulong deltaPackets, ulong deltaTime)
        {
            OnRate?.Invoke(pid, deltaPackets, deltaTime);
        }
        private void Analyzer_OnBitrateMeasured(BitrateSample sample)
        {
            OnBitrateMeasured?.Invoke(sample);
        }
        private void Analyzer_OnPcrTimestampChange(ulong timestamp)
        {
            OnPcrTimestampChange?.Invoke(timestamp);
        }
        private void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            StopParser();
        }
        private void ParseBytesToTables(ReadOnlySpan<byte> bytes, int packetLength)
        {
            var packetCount = bytes.Length / packetLength;

            for (var i = 0; i < packetCount; i++)
            {
                var packetOffset = i * packetLength;
                if (bytes[packetOffset] != TsPacket.SYNC_BYTE)
                {
                    m_tsPacketFactory.RecordSyncLoss();
                    m_monitor?.ObserveMissingSync();
                    continue;
                }

                var transportSpan = TsPacketHeader.GetTransportPacketSpan(bytes.Slice(packetOffset, packetLength), packetLength);
                if (!TsPacketHeader.TryReadPid(transportSpan, out var pid))
                {
                    continue;
                }

                if (pid == 0xFFFF)
                {
                    continue;
                }

                if (pid == (ushort)ReservedPids.NullPacket)
                {
                    if (m_allowAnalyzer || m_monitor != null)
                    {
                        var nullPacket = m_tsPacketFactory.GetTsPacket(
                            bytes.Slice(packetOffset, packetLength),
                            packetLength,
                            TsPacketBuildOptions.HeaderOnly);
                        if (nullPacket.Pid != 0xFFFF)
                        {
                            m_monitor?.ObservePacket(nullPacket, transportSpan, observeSections: false);
                            if (m_allowAnalyzer)
                                PushPacketWithFileOffset(nullPacket, packetLength, i);
                        }
                    }
                    else
                    {
                        m_monitor?.ObserveGoodSync();
                    }

                    continue;
                }

                var routeSi = m_tableRouter.RequiresFullPacket(pid);
                var routeT2miOnly = !routeSi && m_t2miEnabled && m_tableRouter.IsT2miPid(pid);
                if (!routeSi && !routeT2miOnly && !m_allowAnalyzer && m_monitor == null)
                {
                    continue;
                }

                TsPacketBuildOptions options;
                if (routeSi || routeT2miOnly)
                {
                    options = TsPacketBuildOptions.SiTable(captureRawPacket: m_tableRouter.RequiresRawPacket(pid));
                }
                else if (m_monitor != null && pid == (ushort)ReservedPids.RST)
                {
                    options = new TsPacketBuildOptions { IncludePayload = true };
                }
                else if (m_monitor != null)
                {
                    options = new TsPacketBuildOptions
                    {
                        CaptureRawPacket = false,
                        IncludePayload = false,
                        ParsePesHeader = false,
                    };
                }
                else
                {
                    options = TsPacketBuildOptions.HeaderOnly;
                }

                var packet = m_tsPacketFactory.GetTsPacket(bytes.Slice(packetOffset, packetLength), packetLength, options);
                if (packet.Pid == 0xFFFF)
                {
                    m_monitor?.ObserveGoodSync();
                    continue;
                }

                m_monitor?.ObservePacket(
                    packet,
                    transportSpan,
                    observeSections: !m_monitorUsesRouterSections || pid == (ushort)ReservedPids.RST);

                if (m_allowAnalyzer)
                {
                    PushPacketWithFileOffset(packet, packetLength, i);
                }

                if (routeSi)
                {
                    m_tableRouter.RouteTablePacket(packet);
                }
                else if (routeT2miOnly)
                {
                    m_tableRouter.RouteT2mi(packet);
                }
            }
        }

        private void ParseBytesToPackets(ReadOnlySpan<byte> bytes, int packetLength)
        {
            var options = m_t2miEnabled
                ? TsPacketBuildOptions.FullWithRaw
                : new TsPacketBuildOptions
                {
                    CaptureRawPacket = false,
                    IncludePayload = true,
                    ParsePesHeader = true,
                };

            var packetCount = bytes.Length / packetLength;

            for (var i = 0; i < packetCount; i++)
            {
                var packetOffset = i * packetLength;
                if (bytes[packetOffset] != TsPacket.SYNC_BYTE)
                {
                    m_tsPacketFactory.RecordSyncLoss();
                    m_monitor?.ObserveMissingSync();
                    continue;
                }

                var packet = m_tsPacketFactory.GetTsPacket(bytes.Slice(packetOffset, packetLength), packetLength, options);
                if (packet.Pid == 0xFFFF)
                {
                    m_monitor?.ObserveGoodSync();
                    continue;
                }

                var transportSpan = TsPacketHeader.GetTransportPacketSpan(bytes.Slice(packetOffset, packetLength), packetLength);
                m_monitor?.ObservePacket(packet, transportSpan, observeSections: true);

                if (m_allowAnalyzer)
                {
                    PushPacketWithFileOffset(packet, packetLength, i);
                }

                OnTsPacketReady?.Invoke(packet);
                m_tableRouter.RouteT2mi(packet);
            }
        }

        private void PushPacketWithFileOffset(TsPacket packet, int packetLength, int packetIndexInBuffer)
        {
            if (m_fileStreamByteOffset is long baseOffset)
                m_analyzer.SetStreamByteOffset(baseOffset + (long)packetIndexInBuffer * packetLength);
            else
                m_analyzer.SetStreamByteOffset(null);

            m_analyzer.PushPacket(packet, packetLength);
        }
        internal static bool TryResolveUdpTsPacketLength(int datagramByteCount, out int packetLength)
        {
            return UdpTsSource.TryResolveUdpTsPacketLength(datagramByteCount, out packetLength);
        }

        private void CompleteParserRun(Exception? parserException)
        {
            try
            {
                ParserComplete();
            }
            catch (Exception ex) when (parserException != null)
            {
                Logger.Send(LogStatus.EXCEPTION, "Exception while completing parser after failure", ex);
            }
        }

        private void ParserComplete()
        {
            Logger.Send(LogStatus.INFO, $"Parser complete working");
            try
            {
                OnParserComplete?.Invoke();
            }
            finally
            {
                m_parserTask = null;

                if (m_timer != null)
                {
                    m_timer.Elapsed -= Timer_Elapsed;
                    m_timer.Dispose();
                    m_timer = null;
                }

                if (!m_disposed)
                {
                    ResetCancellationToken();
                    if (m_parserRunTimeIn_ms != null)
                        ParserRunTimer();
                }
            }
        }

        private void EnsureCancellationTokenReady()
        {
            if (m_ct.IsCancellationRequested)
                ResetCancellationToken();
        }

        private void ResetCancellationToken()
        {
            var previous = m_cts;
            m_cts = new CancellationTokenSource();
            m_ct = m_cts.Token;
            previous.Dispose();
        }

        private bool IsExpectedParserShutdown(Exception ex)
        {
            if (ex is OperationCanceledException)
                return m_ct.IsCancellationRequested || m_disposed;

            if (!m_ct.IsCancellationRequested && !m_disposed)
                return false;

            return ex is ObjectDisposedException
                || ex is SocketException
                || ex is ChannelClosedException;
        }

        private void WaitForParserTasks()
        {
            if (m_parserTask == null)
                return;

            try
            {
                m_parserTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException ex)
            {
                foreach (var inner in ex.InnerExceptions)
                {
                    if (!IsExpectedParserShutdown(inner))
                        Logger.Send(LogStatus.EXCEPTION, "Exception while waiting for parser tasks", inner);
                }
            }
        }
        #endregion
    }
}
