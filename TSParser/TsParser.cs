using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSParser.Descriptors;
using TSParser.Enums;
using TSParser.Tables;
using TSParser.TransportStream;

namespace TSParser
{
    public class TsParser
    {
        #region Private fields
        private delegate void m_currentTableFactory(TsPacket tsPacket);

        #endregion
        #region Public methods
        public TsParser(TsMode mode = TsMode.DVB)
        {

        }
        public TsParser(string tsFile, TsMode mode = TsMode.DVB)
        {

        }
        public TsParser(string mcastAddress,int port, string mcastInterface,TsMode mode = TsMode.DVB)
        {

        }
        public void RunParser()
        {

        }
        public void RunParser(int milliseconds)
        {

        }
        public async Task RunParserAsync()
        {

        }
        public async Task RunParserAsync(int milliseconds)
        {

        }
        public void StopParser()
        {

        }
        //parse bytes from source
        public void PushBytes()
        {

        }
        public TsPacket[] GetTsPacketsFromBytes(ReadOnlySpan<byte> bytes)
        {

        }
        public TsPacket GetOneTsPacketFromBytes(ReadOnlySpan<byte> bytes)
        {

        }
        public Table GetOneTableFromBytes(ReadOnlySpan<byte> bytes)
        {

        }
        public Descriptor GetOnedescriptorFromBytes(ReadOnlySpan<byte> bytes)
        {

        }
        #endregion
        #region Private methods
        private void InitEvents()
        {

        }
        private void DvbTableFactory(TsPacket tsPacket)
        {

        }
        private void AtscTableFactory(TsPacket tsPacket)
        {

        }
        private void IsdbTableFactory(TsPacket tsPacket)
        {

        }
        #endregion
    }
}
