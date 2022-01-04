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

        private readonly Lazy<TsPacketFactory> packetFactory = new Lazy<TsPacketFactory>();
        private readonly Lazy<TdtTotFactory> tdtTotFactory = new Lazy<TdtTotFactory>();
        private readonly Lazy<SdtBatFactory> sdtBatFactory = new Lazy<SdtBatFactory>();
        private readonly Lazy<CatFactory> catFactory = new Lazy<CatFactory>();
        private readonly Lazy<NitFactory> nitFactory = new Lazy<NitFactory>();
        private readonly Lazy<PatFactory> patFactory = new Lazy<PatFactory>();
        private readonly Lazy<EitFactory> eitFactory = new Lazy<EitFactory>();
        private readonly Lazy<MipFactory> mipFactory = new Lazy<MipFactory> ();
        private TsPacketFactory m_tsPacketFactory => packetFactory.Value;
        private TdtTotFactory m_TdtTotFactory => tdtTotFactory.Value;
        private SdtBatFactory m_SdtBatFactory => sdtBatFactory.Value;
        private CatFactory m_CatFactory => catFactory.Value;
        private NitFactory m_NitFactory => nitFactory.Value;
        private PatFactory m_PatFactory => patFactory.Value;
        private EitFactory m_EitFactory => eitFactory.Value;
        private MipFactory m_MipFactory => mipFactory.Value;

        public readonly byte[] PacketSize = new byte[] { 188, 204 };

        private string m_tsFileName = null!;
        private IPAddress m_multicastGroup = null!;
        private IPAddress m_incomingIpInterface = null!;
        private int m_multicastPort;
        private Socket socket = null!;

        private static CancellationTokenSource m_cts = new CancellationTokenSource();
        private CancellationToken m_ct = m_cts.Token;

        private CircularBuffer buffer = null!;        

        private Task m_parserTask = null!;
        private Task m_bufferReaderTask = null!;        

        private PmtFactory[] m_pmtFactories = null!;
        private ushort[] m_pmtPids = null!;

        private readonly Lazy<List<AitFactory>> aitFactories = new Lazy<List<AitFactory>>();
        private List<AitFactory> m_aitFactories => aitFactories.Value;
        private List<ushort> m_aitPids = new List<ushort>();

        private readonly Lazy<List<Scte35Factory>> scte35Factories = new Lazy<List<Scte35Factory>>();
        private List<Scte35Factory> m_scte35Factories=>scte35Factories.Value;
        private List<ushort> m_scte35Pids = new List<ushort>();

        private int m_connectionAttempts = 5;
        private int m_socketTimeOut = 5000;
        private int? m_parserRunTimeIn_ms = null;
        private System.Timers.Timer m_timer = null!;        
        public List<ushort> PidList
        {
            get => m_analyzer.PidList;            
        }

        private Analyzer m_analyzer = new Analyzer();
        #endregion
        #region Public methods
        /// <summary>
        /// Maximum run time for parser in milliseconds. minimum value 100 ms.
        /// </summary>
        public int? MaxParserRunTime
        {
            get => m_parserRunTimeIn_ms;
            set
            {
                if (value < 100) throw new Exception("Too short max run time for parser ");
                m_parserRunTimeIn_ms = value;
            }
        }
        /// <summary>
        /// Init parser to push bytes in it directly. Default parser mode - DVB, Decode mode - table
        /// </summary>
        /// <param name="mode">Parser Mode DVB, ISDB, ATSC</param>
        /// <param name="pmode">Decode mode, return via events Tables or Packets</param>
        public TsParser(TsMode mode = TsMode.DVB, DecodeMode pmode = DecodeMode.Table)
        {
            SetTableFactory(mode, pmode);
        }
        /// <summary>
        /// Init parser with transport stream file. Default parser mode - DVB, Decode mode - table. File size shall be more than 2040 bytes
        /// </summary>
        /// <param name="tsFile">Transport stream file</param>
        /// <param name="mode">Parser Mode DVB, ISDB, ATSC</param>
        /// <param name="pmode">Decode mode, return via events Tables or Packets</param>
        /// <exception cref="Exception">when file size less than 2040 bytes or invalid file name</exception>
        public TsParser(string tsFile, TsMode mode = TsMode.DVB, DecodeMode pmode = DecodeMode.Table)
        {
            SetTableFactory(mode, pmode);
            if (File.Exists(tsFile))
            {
                if (new FileInfo(tsFile).Length < 2040) throw new Exception("File length is less then 2040 bytes");
                m_tsFileName = tsFile;
                RunParserDel = RunFileParser;
            }
            else
            {
                throw new Exception($"Invalid file name: {tsFile}");
            }
        }
        /// <summary>
        /// Init parser with udp stream source. source_address,port,local_pc_address, Default parser mode - DVB, Decode mode - table, default connect attempts 5, timeout 5000 ms
        /// </summary>
        /// <param name="multicastGroup"></param>
        /// <param name="multicastPort"></param>
        /// <param name="incomingIpInterface"></param>
        /// <param name="mode"></param>
        /// <param name="pmode"></param>
        /// <exception cref="Exception">when invalid network port or address</exception>
        public TsParser(string multicastGroup, int multicastPort, string incomingIpInterface = "", TsMode mode = TsMode.DVB, DecodeMode pmode = DecodeMode.Table)
        {
            SetTableFactory(mode,pmode);
            m_incomingIpInterface = string.IsNullOrEmpty(incomingIpInterface) ? IPAddress.Any : IPAddress.Parse(incomingIpInterface);
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
        /// <summary>
        /// Run parser in synchronous mode
        /// </summary>
        public void RunParser()
        {
            InitEvents();            
            m_parserTask = Task.Run(() => RunParserDel(), m_ct).ContinueWith(AfterParserComplete);
            m_parserTask.Wait();
        }
        /// <summary>
        /// Run parser in async mode
        /// </summary>
        /// <returns></returns>
        public async Task RunParserAsync()
        {
            InitEvents();

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
        }
        /// <summary>
        /// Push bytes to parser from other source. Dektec or RTP stream  etc.
        /// </summary>
        /// <param name="bytes"></param>
        /// <exception cref="Exception">when can not sync to stream</exception>
        public void PushBytes(ReadOnlySpan<byte> bytes)
        {
            var packetLength = GetPacketLength(bytes[..2040], out int syncByteOffset);
            if (syncByteOffset == -1) throw new Exception("Can not sync to transport stream");
            ParserModeDel(bytes, packetLength);
        }
        /// <summary>
        /// Push bytes with known ts packet size. 188 or 204 bytes
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="packetLength"></param>
        public void PushBytes(ReadOnlySpan<byte> bytes, int packetLength)
        {
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
        public Table GetOneTableFromBytes(ReadOnlySpan<byte> bytes)
        {
            switch (bytes[0])
            {
                case 0x00: return new PAT(bytes);
                case 0x01: return new CAT(bytes);
                case 0x02: return new PMT(bytes);
                case 0x74: return new AIT(bytes,0);//TODO: fix these
                case 0x4A: return new BAT(bytes);
                case 0x70: return new TDT(bytes);
                case 0x73: return new TOT(bytes);
                case byte n when n == 0x42 || n == 0x46: return new SDT(bytes);
                case byte n when n == 0x40 || n == 0x41: return new NIT(bytes);
                case byte n when n == 0x4F || n == 0x4E || (n >= 0x50 && n <= 0x5F) || (n >= 0x60 && n <= 0x6F): return new EIT(bytes);
                default: throw new Exception($"Unknown table id: 0x{bytes[0]:X2}");
            }
        }
        /// <summary>
        /// Return ONE descriptor from incoming bytes. Bytes length shall be less than 255 bytes. use for tests or in lab
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public Descriptor GetOneDescriptorFromBytes(ReadOnlySpan<byte> bytes)
        {
            return DescriptorFactory.GetDescriptor(bytes);
        }
        #endregion
        #region Private methods
        private void ParserRunTimer()
        {
            if (m_parserRunTimeIn_ms != null)
            {
                m_timer = new System.Timers.Timer();
                m_timer.Interval = (double)m_parserRunTimeIn_ms;
                m_timer.Elapsed += Timer_Elapsed;
                m_timer.AutoReset = true;
                m_timer.Enabled = true;
            }
        }
        private void SetTableFactory(TsMode mode, DecodeMode parserMode)
        {
            switch (mode)
            {
                case TsMode.DVB: SelectedTableFactory = DvbTableFactory; break;
                case TsMode.ATSC: SelectedTableFactory = AtscTableFactory; break;
                case TsMode.ISDB: SelectedTableFactory = IsdbTableFactory; break;
            }
            switch (parserMode)
            {
                case DecodeMode.Packet: ParserModeDel = ParseBytesToPackets; break;
                case DecodeMode.Table: ParserModeDel = ParseBytesToTables; break;
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
                m_pmtFactories[i] = new PmtFactory();
                m_pmtFactories[i].CurrentPid = m_pmtPids[i];
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
                    var aitFactory = new AitFactory();
                    aitFactory.CurrentPid = aitPid;
                    aitFactory.OnAitReady += AitFactory_OnAitReady;
                    m_aitFactories.Add(aitFactory);
                }

            }
            var scte35Idx = pmt.EsInfoList.FindIndex(es => es.StreamType == 0x86);            
            if(scte35Idx >= 0)
            {
                var scte35Pid = pmt.EsInfoList[scte35Idx].ElementaryPid;
                if (!m_scte35Pids.Contains(scte35Pid))
                {
                    m_scte35Pids.Add(scte35Pid);
                    var scte35Factory = new Scte35Factory();
                    scte35Factory.CurrentPid = scte35Pid;
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
            m_analyzer.PushPacket(tsPacket);

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
            if(idx >= 0)
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
        private int GetPacketLength(ReadOnlySpan<byte> byteArray, out int syncByteOffset)
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
                SelectedTableFactory(tsPackets[i]);
            }
        }
        private void ParseBytesToPackets(ReadOnlySpan<byte> bytes, int packetLength)
        {
            var tsPackets = m_tsPacketFactory.GetTsPackets(bytes, packetLength);

            for (int i = 0; i < tsPackets.Length; i++)
            {
                if (tsPackets[i].Pid == 0xFFFF) continue; // if here we catch tspacket with pid 0xFFFF drop it because this packet generate only when something goes wrong
                OnTsPacketReady?.Invoke(tsPackets[i]);                
            }
        }
        private void RunFileParser()
        {
            Logger.Send(LogStatus.INFO, $"Start ts file {m_tsFileName} parsing");            
            using FileStream fileStream = new FileStream(m_tsFileName, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 348 * 188, FileOptions.SequentialScan);
            using BinaryReader binaryReader = new BinaryReader(fileStream);

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

                ParserRunTimer();

                while ((BytesRead = fileStream.Read(Buffer, 0, MAX_BUFFER)) != 0 && !m_ct.IsCancellationRequested)
                {
                    ReadOnlySpan<byte> BufferSpan = new ReadOnlySpan<byte>(Buffer);
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
                IPEndPoint endPoint = new IPEndPoint(m_incomingIpInterface, m_multicastPort);
                socket.Bind(endPoint);
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(m_multicastGroup, m_incomingIpInterface));
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                socket.ReceiveBufferSize = 1316 * 1000;
                socket.ReceiveTimeout = m_socketTimeOut;

                byte[] bytes = new byte[1500];
                

                while(!m_ct.IsCancellationRequested)
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

                ParserRunTimer();

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
            Logger.Send(LogStatus.INFO, $"Parser complete working");

            m_cts = new CancellationTokenSource();
            OnParserComplete?.Invoke();
            m_timer?.Dispose();

        }        
        #endregion
    }
}
