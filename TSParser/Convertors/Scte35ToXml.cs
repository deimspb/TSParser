﻿// Copyright 2021 Eldar Nizamutdinov deim.mobile<at>gmail.com 
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

using TSParser.Descriptors;
using TSParser.Descriptors.Scte35Descriptors;
using TSParser.Service;
using TSParser.Tables.DvbTables;
using TSParser.Tables.Scte35;
using System.Xml.Serialization;

namespace TSParser.Convertors
{
    public static class Scte35ToXml
    {
        public static SpliceInfoSectionType Convert(SCTE35 scte35)
        {
            return new SpliceInfoSectionType(scte35);           
        }       

    }
}