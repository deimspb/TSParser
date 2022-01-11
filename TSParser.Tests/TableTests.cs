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
using System.IO;
using NUnit.Framework;
using TSParser.Tables;
using TSParser.Tables.DvbTables;

namespace TSParser.Tests
{
    public class TableTests
    {
        private Table GetTable(string fileName)
        {
            TsParser parser = new();
            var filePath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"..\..\..\", fileName);
            byte[] bytes = File.ReadAllBytes(filePath);
            return TsParser.GetOneTableFromBytes(bytes);
        }
        [Test]
        public void Test_PAT_129_services()
        {
            var table = GetTable(@"TestResources\Tables\PAT_0_202201071904221245.tbl");

            Assert.IsNotNull(table);
            Assert.IsInstanceOf<PAT>(table);

            var pat = (table as PAT)!;
            Assert.AreEqual(525, pat.SectionLength);
            Assert.IsTrue(pat.SectionSyntaxIndicator);
            Assert.AreEqual(8,pat.VersionNumber);
            Assert.IsTrue(pat.CurrentNextIndicator);
            Assert.AreEqual(0,pat.SectionNumber);
            Assert.AreEqual(0,pat.LastSectionNumber);
            Assert.AreEqual(27,pat.TransportStreamId);
            Assert.AreEqual(129,pat.PatRecords.Length);
            Assert.AreEqual(0,pat.PatRecords[0].ProgramNumber);
            Assert.AreEqual(16,pat.PatRecords[0].Pid);
            Assert.AreEqual(0x48734C7, pat.CRC32);
        }
        [Test]
        public void Test_PMT()
        {
            var table = GetTable(@"TestResources\Tables\PMT_0_202201072036483826.tbl");

            Assert.IsNotNull(table);
            Assert.IsInstanceOf<PMT>(table);

            var pmt = (table as PMT)!;
            Assert.AreEqual(30104, pmt.ProgramNumber);
            Assert.AreEqual(581,pmt.SectionLength);
            Assert.AreEqual(19, pmt.EsInfoList.Count);
            Assert.AreEqual(8191,pmt.PcrPid);
            Assert.AreEqual(0,pmt.ProgramInfoLength);
            Assert.AreEqual(0x63517EFD, pmt.CRC32);

            var esInfo = pmt.EsInfoList[0];
            Assert.IsNotNull(esInfo);

            Assert.AreEqual(400,esInfo.ElementaryPid);
            Assert.AreEqual(4,esInfo.StreamType);
            Assert.AreEqual(6,esInfo.EsInfoLength);
            Assert.AreEqual(1,esInfo.EsDescriptorList.Count);
        }
    }
}
