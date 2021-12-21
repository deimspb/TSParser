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

using System.Diagnostics;
using TSParser;
using TSParser.Tables.DvbTables;

namespace AppTest
{
    internal class SingleFileTest
    {
        private TsParser parser = null!;

        public void Run()
        {
            string tsFile = @"E:\dvb\dvb_lib\9.ts";
            parser = new TsParser(tsFile);

            //parser.OnPatReady += Parser_OnPatReady;
            parser.OnPmtReady += Parser_OnPmtReady;
            //parser.OnEitReady += Parser_OnEitReady;
            //parser.OnTdtReady += Parser_OnTdtReady;
            //parser.OnSdtReady += Parser_OnSdtReady;
            //parser.OnBatReady += Parser_OnBatReady;
            //parser.OnCatReady += Parser_OnCatReady;
            //parser.OnNitReady += Parser_OnNitReady;

            Stopwatch sw = new Stopwatch();

            sw.Start();

            parser.RunParser();

            sw.Stop();

            Debug.WriteLine($"Done in {sw.ElapsedMilliseconds} ms");


        }

        private void Parser_OnNitReady(NIT nit)
        {
            Console.WriteLine(nit);
        }

        private void Parser_OnCatReady(CAT cat)
        {
            Console.WriteLine(cat);
        }

        private void Parser_OnBatReady(BAT bat)
        {
            Console.WriteLine(bat);
        }

        private void Parser_OnSdtReady(SDT sdt)
        {
            Console.WriteLine(sdt);
        }

        private void Parser_OnTdtReady(TDT tdt)
        {
            Console.WriteLine(tdt);
        }

        private void Parser_OnEitReady(EIT eit)
        {
            Console.WriteLine(eit);
        }

        private void Parser_OnPmtReady(PMT pmt)
        {
            Console.WriteLine(pmt);
        }

        private void Parser_OnPatReady(PAT pat)
        {
            Console.WriteLine(pat);
        }
    }
}
