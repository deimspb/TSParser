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

using TSParser.Enums;
using TSParser.TransportStream;

namespace TSParser
{
    public class Decoder
    {
        private delegate void DecoderDelegate(TsPacket tsPacket);
        private DecoderDelegate m_currentDecoder = null!;

        #region public methods
        public Decoder(DecoderMode mode)
        {
            if(mode == DecoderMode.SCTE35)
            {
                m_currentDecoder = Scte35Decoder;
            }
        }
        public void RunDecoder()
        {
            
        }
        public void RunDecoderAsync()
        {

        }
        public void StopDecoder()
        {

        }
        public void PushTable(TsPacket tsPacket)
        {
            m_currentDecoder(tsPacket);
        }
        #endregion
        #region private methods
        private void Scte35Decoder(TsPacket tsPacket)
        {

        }
        #endregion
    }
}
