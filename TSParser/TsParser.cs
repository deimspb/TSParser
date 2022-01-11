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
using TSParser.Analysis;
using TSParser.Buffers;
using TSParser.Comparer;
using TSParser.Descriptors;
using TSParser.Enums;
using TSParser.Service;
using TSParser.Tables;
using TSParser.Tables.DvbTableFactory;
using TSParser.Tables.DvbTables;
using TSParser.Tables.Mip;
using TSParser.TransportStream;

namespace TSParser
{
    public struct ParserConfig
    {
        /// <summary>
        /// Allow analyze packets
        /// </summary>
        public bool AllowAnalyzer;
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

    public class TsParser
    {
        #region Private fields

        private delegate void m_currentTableFactory(TsPacket tsPacket);
        private delegate void ParserDelegate();
        private delegate void ParserModeDelefate(ReadOnlySpan<byte> bytes, int packetLen);

        private m_currentTableFactory SelectedTableFactory = null!;
        private ParserDelegate RunParserDel = null!;
        private ParserModeDelefate ParserModeDel = null!;

        public event ParserComplete OnParserComplete = null!;
        public event PatReady OnPatReady = null!;
        public event PmtReady OnPmtReady = null!;
        public event EitReady OnEitReady = null!;
        public event TdtReady OnTdtReady = null!;
        public event TotReady OnTotready = null!;
        public event SdtReady OnSdtReady = null!;
        public event BatReady OnBatReady = null!;
        public event CatReady OnCatReady = null!;
        public event NitReady OnNitReady = null!;
        public event AitReady OnAitReady = null!;
        public event MipReady OnMipReady = null!;
        public event Scte35Ready OnScte35Ready = null!;
        public event TsPacketReady OnTsPacketReady = null!;
        public event RateDelegate OnRate = null!;

        private readonly Lazy<TsPacketFactory> packetFactory = new();
        private readonly Lazy<TdtTotFactory> tdtTotFactory = new();
        private readonly Lazy<SdtBatFactory> sdtBatFactory = new();
        private readonly Lazy<CatFactory> catFactory = new();
        private readonly Lazy<NitFactory> nitFactory = new();
        private readonly Lazy<PatFactory> patFactory = new();
        private readonly Lazy<EitFactory> eitFactory = new();
        private readonly Lazy<MipFactory> mipFactory = new();
        private readonly Lazy<Analyzer> analyzer = new();
        private readonly Lazy<Compare> compare = new();
        private readonly Lazy<List<AitFactory>> aitFactories = new();
        private readonly Lazy<List<Scte35Factory>> scte35Factories = new();
        private PmtFactory[] m_pmtFactories = null!;

        private TsPacketFactory m_tsPacketFactory => packetFactory.Value;
        private TdtTotFactory m_TdtTotFactory => tdtTotFactory.Value;
        private SdtBatFactory m_SdtBatFactory => sdtBatFactory.Value;
        private CatFactory m_CatFactory => catFactory.Value;
        private NitFactory m_NitFactory => nitFactory.Value;
        private PatFactory m_PatFactory => patFactory.Value;
        private EitFactory m_EitFactory => eitFactory.Value;
        private MipFactory m_MipFactory => mipFactory.Value;
        private Analyzer m_analyzer => analyzer.Value;
        private Compare m_compare => compare.Value;
        private List<AitFactory> m_aitFactories => aitFactories.Value;
        private List<Scte35Factory> m_scte35Factories => scte35Factories.Value;

        public readonly byte[] PacketSize = new byte[] { 188, 204 };

        private string m_tsFileName = null!;
        private IPAddress m_multicastGroup = null!;
        private IPAddress m_incomingIpInterface = null!;
        private int m_multicastPort;
        private Socket socket = null!;

        private static CancellationTokenSource m_cts = new();
        private CancellationToken m_ct = m_cts.Token;

        private CircularBuffer buffer = null!;

        private Task m_parserTask = null!;
        private Task m_bufferReaderTask = null!;


        private ushort[] m_pmtPids = null!;
        private List<ushort> m_aitPids = new();
        private List<ushort> m_scte35Pids = new();

        private int m_connectionAttempts = 5;
        private int m_socketTimeOut = 5000;
        private int? m_parserRunTimeIn_ms = null;
        private bool m_allowAnalyzer;
        private System.Timers.Timer m_timer = null!;
        private int? MaxParserRunTime
        {
            get => m_parserRunTimeIn_ms;
            set
            {
                if (value < 100) throw new Exception("Too short max run time for parser ");
                m_parserRunTimeIn_ms = value;
            }
        }
        #endregion
        #region Public methods        
        /// <summary>
        /// Return pid list from analyzer
        /// </summary>
        public List<ushort> PidList
        {
            get => m_analyzer.PidList;
        }
        /// <summary>
        /// Maximum run time for parser in milliseconds. minimum value 100 ms.
        /// </summary>        
        public TsParser(ParserConfig config)
        {
            switch (config.CurrentTsMode)
            {
                case TsMode.DVB: SelectedTableFactory = DvbTableFactory; break;
                case TsMode.ATSC: SelectedTableFactory = AtscTableFactory; break;
                case TsMode.ISDB: SelectedTableFactory = IsdbTableFactory; break;
            }
            switch (config.CurrentDecodeMode)
            {
                case DecodeMode.Packet: ParserModeDel = ParseBytesToPackets; break;
                case DecodeMode.Table: ParserModeDel = ParseBytesToTables; break;
            }

            m_allowAnalyzer = config.AllowAnalyzer;
            MaxParserRunTime = config.ParserRunTime;

            ParserRunTimer();

            InitEvents();

            if (config.TsFileName != null)
            {
                FileParser(config.TsFileName);
                return;
            }

            if (config.MulticastGroup != null && config.MulticastPort != null)
            {
                UdpParser(config.MulticastGroup, config.MulticastPort, config.MulticastIncomingIp);
                return;
            }
        }
        /// <summary>
        /// Run parser in synchronous mode
        /// </summary>
        public void RunParser()
        {
            m_parserTask = Task.Run(() => RunParserDel(), m_ct).ContinueWith(AfterParserComplete);
            m_parserTask.Wait();
        }
        /// <summary>
        /// Run parser in async mode
        /// </summary>
        /// <returns></returns>
        public async Task RunParserAsync()
        {
            m_parserTask = Task.Run(() => RunParserDel(), m_ct).ContinueWith(AfterParserComplete);
            try
            {
                await m_parserTask;
            }
            catch (AggregateException ae)
            {
                foreach (var ex in ae.InnerExceptions)
                {
                    Logger.Send(LogStatus.EXCEPTION, $"Innser exception while Run parser async", ex);
                }
            }
        }
        /// <summary>
        /// Stop parser
        /// </summary>
        public void StopParser()
        {
            m_cts.Cancel();

            if (m_parserTask == null)
            {
                ParserComplete();
            }
        }       
        /// <summary>
        /// Push bytes with known ts packet size. 188 or 204 bytes
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="packetLength"></param>
        public void PushBytes(byte[] bytes, int packetLength)
        {
            if (m_timer != null && !m_timer.Enabled) m_timer.Enabled = true;

            ParserModeDel(bytes, packetLength);
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
        /// <exception cref="Exception"></exception>
        public TsPacket GetOneTsPacketFromBytes(ReadOnlySpan<byte> bytes, int packetLength)
        {
            if (bytes.Length > 204 && bytes.Length < 188) throw new Exception("bytes length shall be 188 or 204 bytes");
            if (packetLength != bytes.Length) throw new Exception("Not equal bytes length and packet length");
            return m_tsPacketFactory.GetTsPacket(bytes, packetLength);
        }
        /// <summary>
        /// Return ONE table from incoming bytes. Bytes length shall be less than 4093 bytes. Use it for tests or in lab
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static Table GetOneTableFromBytes(ReadOnlySpan<byte> bytes)
        {
            return bytes[0] switch
            {
                0x00 => new PAT(bytes),
                0x01 => new CAT(bytes),
                0x02 => new PMT(bytes),
                0x74 => new AIT(bytes),
                0x4A => new BAT(bytes),
                0x70 => new TDT(bytes),
                0x73 => new TOT(bytes),
                byte n when n == 0x42 || n == 0x46 => new SDT(bytes),
                byte n when n == 0x40 || n == 0x41 => new NIT(bytes),
                byte n when n == 0x4F || n == 0x4E || (n >= 0x50 && n <= 0x5F) || (n >= 0x60 && n <= 0x6F) => new EIT(bytes),
                _ => throw new Exception($"Unknown table id: 0x{bytes[0]:X2}"),
            };
        }
        /// <summary>
        /// Return ONE descriptor from incoming bytes. Bytes length shall be less than 255 bytes. use for tests or in lab
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static Descriptor GetOneDescriptorFromBytes(ReadOnlySpan<byte> bytes)
        {
            return DescriptorFactory.GetDescriptor(bytes);
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
        #endregion
        #region Private methods
        private void FileParser(string filePath)
        {
            if (File.Exists(filePath))
            {
                if (new FileInfo(filePath).Length < 2040) throw new Exception("File length is less then 2040 bytes");
                m_tsFileName = filePath;
                RunParserDel = RunFileParser;
            }
            else
            {
                throw new Exception($"Invalid file name: {filePath}");
            }
        }
        private void UdpParser(string multicastGroup, int? m_port, string? incomingIpInterface)
        {
            var multicastPort = (int)(m_port == null ? 1234 : m_port);
            m_incomingIpInterface = incomingIpInterface == null ? IPAddress.Any : IPAddress.Parse(incomingIpInterface);

            if (multicastPort > 1 && multicastPort < 65535)
            {
                m_multicastPort = multicastPort;
            }
            else
            {
                throw new Exception("Invalid port number");
            }
            m_multicastGroup = IPAddress.Parse(multicastGroup);

            RunParserDel = RunUdpParser;
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
            m_PatFactory.OnPatReady += PatFactory_OnPatReady;
            m_EitFactory.OnEitReady += EitFactory_OnEitReady;
            m_CatFactory.OnCatReady += CatFactory_OnCatReady;
            m_NitFactory.OnNitReady += NitFactory_OnNitReady;
            m_MipFactory.OnMipReady += MipFactory_OnMipReady;
            m_TdtTotFactory.OnTdtReady += TdtTotFactory_OnTdtReady;
            m_TdtTotFactory.OnTotready += TdtTotFactory_OnTotready;
            m_SdtBatFactory.OnSdtReady += SdtBatFactory_OnSdtReady;
            m_SdtBatFactory.OnBatReady += SdtBatFactory_OnBatReady;
            m_analyzer.OnRate += Analyzer_OnRate;
        }
        private void Analyzer_OnRate(ushort pid, ulong deltaPackets, ulong deltaTime)
        {
            OnRate?.Invoke(pid, deltaPackets, deltaTime);
        }
        private void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            StopParser();
        }
        private void MipFactory_OnMipReady(MIP mip)
        {
            OnMipReady?.Invoke(mip);
        }
        private void TdtTotFactory_OnTotready(TOT tot)
        {
            OnTotready?.Invoke(tot);
        }
        private void NitFactory_OnNitReady(NIT nit)
        {
            OnNitReady?.Invoke(nit);
        }
        private void CatFactory_OnCatReady(CAT cat)
        {
            OnCatReady?.Invoke(cat);
        }
        private void SdtBatFactory_OnBatReady(BAT bat)
        {
            OnBatReady?.Invoke(bat);
        }
        private void SdtBatFactory_OnSdtReady(SDT sdt)
        {
            OnSdtReady?.Invoke(sdt);
        }
        private void TdtTotFactory_OnTdtReady(TDT tdt)
        {
            OnTdtReady?.Invoke(tdt);
        }
        private void EitFactory_OnEitReady(EIT eit)
        {
            OnEitReady?.Invoke(eit);
        }
        private void PatFactory_OnPatReady(PAT pat)
        {
            OnPatReady?.Invoke(pat);

            m_pmtPids = (from pr in pat.PatRecords where pr.Pid != 0x16 select pr.Pid).ToArray();

            Array.Sort(m_pmtPids);

            m_pmtFactories = new PmtFactory[m_pmtPids.Length];

            for (int i = 0; i < m_pmtPids.Length; i++)
            {
                m_pmtFactories[i] = new PmtFactory
                {
                    CurrentPid = m_pmtPids[i]
                };
                m_pmtFactories[i].OnPmtReady += PmtFactory_OnPmtReady;
            }

        }
        private void PmtFactory_OnPmtReady(PMT pmt)
        {
            OnPmtReady?.Invoke(pmt);

            var aitIdx = pmt.EsInfoList.FindIndex(es => es.StreamType == 0x05);
            if (aitIdx >= 0 && pmt.EsInfoList[aitIdx].EsDescriptorList.Exists(desc => desc.DescriptorTag == 0x6F))
            {
                var aitPid = pmt.EsInfoList[aitIdx].ElementaryPid;
                // prevent to add already added ait table after pmt update ??? 
                if (!m_aitPids.Contains(aitPid))
                {
                    m_aitPids.Add(aitPid);
                    var aitFactory = new AitFactory
                    {
                        CurrentPid = aitPid
                    };
                    aitFactory.OnAitReady += AitFactory_OnAitReady;
                    m_aitFactories.Add(aitFactory);
                }

            }
            var scte35Idx = pmt.EsInfoList.FindIndex(es => es.StreamType == 0x86);
            if (scte35Idx >= 0)
            {
                var scte35Pid = pmt.EsInfoList[scte35Idx].ElementaryPid;
                if (!m_scte35Pids.Contains(scte35Pid))
                {
                    m_scte35Pids.Add(scte35Pid);
                    var scte35Factory = new Scte35Factory
                    {
                        CurrentPid = scte35Pid
                    };
                    scte35Factory.OnScte35Ready += Scte35Factory_OnScte35Ready;
                    m_scte35Factories.Add(scte35Factory);
                }
            }
        }
        private void Scte35Factory_OnScte35Ready(SCTE35 scte35)
        {
            OnScte35Ready?.Invoke(scte35);
        }
        private void AitFactory_OnAitReady(AIT ait)
        {
            OnAitReady?.Invoke(ait);
        }
        private void DvbTableFactory(TsPacket tsPacket)
        {
            if (tsPacket.TransportErrorIndicator) return; // drop tei packets
            if (tsPacket.Pid == (short)ReservedPids.NullPacket) return;  // drop null packets 

            switch (tsPacket.Pid)
            {
                case (ushort)ReservedPids.PAT:
                    {
                        m_PatFactory.PushTable(tsPacket);
                        break;
                    }
                case (ushort)ReservedPids.CAT:
                    {
                        m_CatFactory.PushTable(tsPacket);
                        break;
                    }
                case (ushort)ReservedPids.NIT:
                    {
                        m_NitFactory.PushTable(tsPacket);
                        break;
                    }
                case (ushort)ReservedPids.SDT:
                    {
                        m_SdtBatFactory.PushTable(tsPacket);
                        break;
                    }
                case (ushort)ReservedPids.EIT:
                    {
                        m_EitFactory.PushTable(tsPacket);
                        break;
                    }
                case (ushort)ReservedPids.RST:
                    {
                        Logger.Send(LogStatus.INFO, $"Not implement RST table");
                        break;
                    }
                case (ushort)ReservedPids.TDT:
                    {
                        m_TdtTotFactory.PushTable(tsPacket);
                        break;
                    }
                case (ushort)ReservedPids.NetworkSync:
                    {
                        m_MipFactory.PushTable(tsPacket);
                        break;
                    }
                case (ushort)ReservedPids.RNT:
                    {
                        Logger.Send(LogStatus.INFO, $"Not implement RNT table");
                        break;
                    }
                case (ushort)ReservedPids.LLinbandSignalink:
                    {
                        Logger.Send(LogStatus.INFO, $"Not implement L lindband signal link table");
                        break;
                    }
                case (ushort)ReservedPids.Measurement:
                    {
                        Logger.Send(LogStatus.INFO, $"Not implement Measurmrnt table");
                        break;
                    }
                case (ushort)ReservedPids.DIT:
                    {
                        Logger.Send(LogStatus.INFO, $"Not implement DIT table");
                        break;
                    }
                case (ushort)ReservedPids.SIT:
                    {
                        Logger.Send(LogStatus.INFO, $"Not implement SIT table");
                        break;
                    }
                default:
                    {
                        GetOtherTables(tsPacket);
                        break;
                    }
            }
        }
        private void GetOtherTables(TsPacket tsPacket)
        {
            GetPmt(tsPacket);
            GetAit(tsPacket);
            GetScte35(tsPacket);
        }
        private void GetPmt(TsPacket tsPacket)
        {
            if (m_pmtPids == null) return;

            var idx = Array.IndexOf(m_pmtPids, tsPacket.Pid);

            if (idx >= 0)
            {
                m_pmtFactories[idx].PushTable(tsPacket);
            }
        }
        private void GetAit(TsPacket tsPacket)
        {
            var idx = m_aitPids.IndexOf(tsPacket.Pid);

            if (idx >= 0)
            {
                m_aitFactories[idx].PushTable(tsPacket);
            }
        }
        private void GetScte35(TsPacket tsPacket)
        {
            var idx = m_scte35Pids.IndexOf(tsPacket.Pid);
            if (idx >= 0)
            {
                m_scte35Factories[idx].PushTable(tsPacket);
            }
        }
        private void AtscTableFactory(TsPacket tsPacket)
        {
            throw new NotImplementedException("ATSC table factory");
        }
        private void IsdbTableFactory(TsPacket tsPacket)
        {
            throw new NotImplementedException("ISDB table factory");
        }
        private static int GetPacketLength(ReadOnlySpan<byte> byteArray, out int syncByteOffset)
        {
            syncByteOffset = -1;

            for (int i = 0; i < byteArray.Length - 3 * 204; i++)
            {
                if (byteArray[i] == TsPacket.SYNC_BYTE && byteArray[204 + i] == TsPacket.SYNC_BYTE && byteArray[204 * 2 + i] == TsPacket.SYNC_BYTE && byteArray[3 * 204 + i] == TsPacket.SYNC_BYTE)
                {
                    syncByteOffset = i;
                    return 204;
                }
                else if (byteArray[i] == TsPacket.SYNC_BYTE && byteArray[188 + i] == TsPacket.SYNC_BYTE && byteArray[188 * 2 + i] == TsPacket.SYNC_BYTE && byteArray[3 * 188 + i] == TsPacket.SYNC_BYTE)
                {
                    syncByteOffset = i;
                    return 188;
                }

            }

            return 0;
        }
        private void ParseBytesToTables(ReadOnlySpan<byte> bytes, int packetLength)
        {
            var tsPackets = m_tsPacketFactory.GetTsPackets(bytes, packetLength);

            for (int i = 0; i < tsPackets.Length; i++)
            {
                if (tsPackets[i].Pid == 0xFFFF) continue; // if here we catch tspacket with pid 0xFFFF drop it because this packet generate only when something goes wrong
                if (m_allowAnalyzer) m_analyzer.PushPacket(tsPackets[i]);
                SelectedTableFactory(tsPackets[i]);
            }
        }
        private void ParseBytesToPackets(ReadOnlySpan<byte> bytes, int packetLength)
        {
            var tsPackets = m_tsPacketFactory.GetTsPackets(bytes, packetLength);

            for (int i = 0; i < tsPackets.Length; i++)
            {
                if (tsPackets[i].Pid == 0xFFFF) continue; // if here we catch tspacket with pid 0xFFFF drop it because this packet generate only when something goes wrong
                if (m_allowAnalyzer) m_analyzer.PushPacket(tsPackets[i]);
                OnTsPacketReady?.Invoke(tsPackets[i]);
            }
        }
        private void RunFileParser()
        {
            Logger.Send(LogStatus.INFO, $"Start ts file {m_tsFileName} parsing");
            using FileStream fileStream = new(m_tsFileName, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 348 * 188, FileOptions.SequentialScan);
            using BinaryReader binaryReader = new(fileStream);

            try
            {

                var firstFileBytes = binaryReader.ReadBytes(2040);
                var packLen = GetPacketLength(firstFileBytes, out int syncByte);

                if (syncByte == -1)
                {
                    throw new Exception("Cannot sync with ts");
                }

                int MAX_BUFFER = 348 * packLen; // 348 packets 188 byte each it is 64kb size. imho optimal size
                byte[] Buffer = new byte[MAX_BUFFER];
                int BytesRead;

                if (syncByte > 0)
                {
                    fileStream.Seek(syncByte, SeekOrigin.Begin); // if first bytes incorrect need to drop it
                }
                else
                {
                    fileStream.Seek(0, SeekOrigin.Begin);
                }

                if (m_timer != null) m_timer.Enabled = true;

                while ((BytesRead = fileStream.Read(Buffer, 0, MAX_BUFFER)) != 0 && !m_ct.IsCancellationRequested)
                {
                    ReadOnlySpan<byte> BufferSpan = new(Buffer);
                    ParserModeDel(BufferSpan[0..BytesRead], packLen);
                }


            }
            catch (Exception ex)
            {
                Logger.Send(LogStatus.EXCEPTION, $"Exception catch in file reader method {ex}", ex);
            }
            finally
            {
                fileStream.Close();
                binaryReader.Close();
            }
        }
        private void RunUdpParser()
        {
            try
            {
                buffer = new CircularBuffer(5000, 1500, false);

                var bytesCount = 0;
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                IPEndPoint endPoint = new(m_incomingIpInterface, m_multicastPort);
                socket.Bind(endPoint);
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(m_multicastGroup, m_incomingIpInterface));
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                socket.ReceiveBufferSize = 1316 * 1000;
                socket.ReceiveTimeout = m_socketTimeOut;

                byte[] bytes = new byte[1500];

                while (!m_ct.IsCancellationRequested)
                {
                    if (m_connectionAttempts <= 0) return;
                    try
                    {
                        bytesCount = socket.Receive(bytes);
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Send(LogStatus.EXCEPTION, $"Receive exception attempts left: {m_connectionAttempts}", ex);
                        m_connectionAttempts--;
                    }
                }

                var packetLen = -1;

                if (bytesCount % 188 == 0)
                {
                    packetLen = 188;
                }

                if (bytesCount % 204 == 0)
                {
                    packetLen = 204;
                }

                if (m_timer != null) m_timer.Enabled = true;

                Logger.Send(LogStatus.INFO, $"Start with network {m_multicastGroup}:{m_multicastPort} ts packet length: {packetLen}, network packet lenght: {bytesCount}");

                m_bufferReaderTask = Task.Run(() => ReadFromBuffer(packetLen), m_ct);

                m_connectionAttempts = 5;
                while (!m_ct.IsCancellationRequested)
                {
                    if (m_connectionAttempts <= 0) return;
                    try
                    {
                        var bytesLen = socket.Receive(bytes);
                        buffer.Add(bytes[0..bytesLen]);
                    }
                    catch (Exception ex)
                    {
                        Logger.Send(LogStatus.EXCEPTION, $"Receive exception attempts left: {m_connectionAttempts}", ex);
                        m_connectionAttempts--;
                    }

                }

                socket.Close();
            }
            catch (Exception ex)
            {
                Logger.Send(LogStatus.EXCEPTION, $"Exception in Run UDP parser", ex);
            }
        }
        private void ReadFromBuffer(int packetLen)
        {
            while (!m_ct.IsCancellationRequested)
            {
                ParserModeDel(buffer.Remove(), packetLen);
            }
        }
        private void AfterParserComplete(Task task)
        {
            ParserComplete();
        }
        private void ParserComplete()
        {
            Logger.Send(LogStatus.INFO, $"Parser complete working");
            OnParserComplete?.Invoke();
            m_cts = new CancellationTokenSource();
            m_timer?.Dispose();
            m_timer = null;
        }
        #endregion
    }
}
