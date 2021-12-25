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

namespace TSParser.Buffers
{
    internal class CircularBuffer
    {
        private byte[][] m_buffer = null!;
        private int[] m_dataLength = null!;
        private int m_nextAddPosition;
        private int m_lastRemPosition;
        private bool m_wrapped;
        private bool m_allowOverflow;

        private readonly object m_lock = new object();
        private readonly int m_packetSize = 1500;

        private static EventWaitHandle m_waitHandle = new AutoResetEvent(false);

        public int BufferSize { get; } = 5000;
        public int BufferFullnes
        {
            get
            {
                lock (m_lock)
                {
                    var fullness = m_nextAddPosition - m_lastRemPosition;
                    if (fullness > -1)
                    {
                        return fullness;
                    }

                    fullness = fullness + BufferSize + 1;
                    return fullness;

                }
            }
        }
        private void ResetBuffers()
        {
            lock (m_lock)
            {
                m_buffer = new byte[BufferSize + 1][];
                m_dataLength = new int[BufferSize + 1];
                for (int i = 0; i <= BufferSize; i++)
                {
                    m_buffer[i] = new byte[m_packetSize];
                }
            }
        }

        public CircularBuffer()
        {
            ResetBuffers();
        }

        public CircularBuffer(int buffSize, int packetSize, bool allowOverflow = false)
        {
            BufferSize = buffSize;
            m_packetSize = packetSize;
            m_allowOverflow = allowOverflow;
            ResetBuffers();
        }

        public void Add(byte[] data)
        {
            lock (m_lock)
            {
                if (BufferFullnes == BufferSize)
                {
                    if (!m_allowOverflow)
                    {
                        throw new OverflowException("Ringbuffer has overflowed");
                    }
                    else
                    {
                        m_lastRemPosition++;
                    }
                }

                if (data.Length <= m_packetSize)
                {
                    Buffer.BlockCopy(data, 0, m_buffer[m_nextAddPosition], 0, data.Length);

                    m_dataLength[m_nextAddPosition++] = data.Length;

                    if (m_nextAddPosition > BufferSize)
                    {
                        m_nextAddPosition = (m_nextAddPosition % BufferSize - 1);
                        m_wrapped = true;
                    }

                    m_waitHandle.Set();
                }
                else
                {
                    throw new OverflowException("large data packet");
                }
            }
        }

        public byte[] Remove()
        {
            while (true)
            {
                lock (m_lock)
                {
                    if (m_lastRemPosition > BufferSize)
                    {
                        m_lastRemPosition = m_lastRemPosition % BufferSize - 1;
                    }

                    if (m_lastRemPosition != m_nextAddPosition || m_wrapped)
                    {
                        if (m_wrapped)
                        {
                            m_wrapped = false;
                        }

                        var dataLength = m_dataLength[m_lastRemPosition];

                        byte[] bytes = new byte[dataLength];

                        Buffer.BlockCopy(m_buffer[m_lastRemPosition++], 0, bytes, 0, dataLength);

                        return bytes;

                    }
                }
                m_waitHandle.WaitOne();
            }
        }
    }
}
