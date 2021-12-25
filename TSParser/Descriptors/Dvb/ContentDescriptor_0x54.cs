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

using TSParser.Service;

namespace TSParser.Descriptors.Dvb
{
    public record ContentDescriptor_0x54:Descriptor
    {
        public ContentNibble[] ContentNibbles { get; } //TODO: implement content nibble descriptions + to string
        public ContentDescriptor_0x54(ReadOnlySpan<byte> bytes) : base(bytes)
        {
            ContentNibbles = new ContentNibble[DescriptorLength / 2];
            for (int i = 0; i < ContentNibbles.Length; i++)
            {
                ContentNibbles[i] = new ContentNibble(bytes.Slice(i * 2, 2));
            }
        }

        public override string ToString()
        {
            string str= $"         Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            foreach(var b in ContentNibbles)
            {
                str += $"         {b}";
            }
            return str;
        }
        public override string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            string str = $"{headerPrefix}Descriptor tag: 0x{DescriptorTag:X2}, {DescriptorName}\n";
            foreach (var b in ContentNibbles)
            {
                str += b.Print(prefixLen + 2);
            }
            return str;
        }
    }
    public struct ContentNibble
    {
        public byte ContentNibble1 { get; }
        public byte ContentNibble2 { get; }
        public byte UserByte { get; }

        public ContentNibble(ReadOnlySpan<byte> bytes)
        {
            ContentNibble1 = (byte)(bytes[0] >> 4);
            ContentNibble2 = (byte)(bytes[0] & 0x0F);
            UserByte = bytes[1];
        }

        public override string ToString()
        {
            return $"            {GetContentString()}";
        }
        public string Print(int prefixLen)
        {
            string headerPrefix = Utils.HeaderPrefix(prefixLen);
            return $"{headerPrefix}{GetContentString()}\n";
        }

        private string GetContentString()
        {
            switch (ContentNibble1)
            {
                case 0x0:
                    {
                        switch (ContentNibble2)
                        {
                            case byte n when n >= 0x0 && n <= 0xF: return "undefined content";                            
                        }
                        break;
                    }
                case 0x1:
                    {
                        switch (ContentNibble2)
                        {
                            case 0x0: return "movie/drama (general)";
                            case 0x1: return "detective/thriller";
                            case 0x2: return "adventure/western/war";
                            case 0x3: return "science fiction/fantasy/horror";
                            case 0x4: return "comedy";
                            case 0x5: return "soap/melodrama/folkloric";
                            case 0x6: return "romance";
                            case 0x7: return "serious/classical/religious/historical movie/drama";
                            case 0x8: return "adult movie/drama";
                            case byte n when n>=9 && n<=0xF: return "reserved for future use";
                            case 0xF: return "user defined";
                        }
                        break;
                    }
                case 0x2:
                    {
                        switch (ContentNibble2)
                        {
                            case 0x0: return "news/current affairs (general)";
                            case 0x1: return "news/weather report";
                            case 0x2: return "news magazine";
                            case 0x3: return "documentary";
                            case 0x4: return "discussion/interview/debate";
                            case byte n when n>=0x5 && n<=0xE: return "reserved for future use";
                            case 0xF: return "user defined";
                        }
                        break ;
                    }
                case 0x3:
                    {
                        switch (ContentNibble2)
                        {
                            case 0x0: return "show/game show (general)";
                            case 0x1: return "game show/quiz/contest";
                            case 0x2: return "variety show";
                            case 0x3: return "talk show";
                            case byte n when n >= 0x4 && n <= 0xE: return"reserved for future use";
                            case 0xF: return "user defined";
                        }
                        break;
                    }
                case 0x4:
                    {
                        switch (ContentNibble2)
                        {
                            case 0x0: return "sports (general)";
                            case 0x1: return "special events (Olympic Games, World Cup, etc.)";
                            case 0x2: return "sports magazines";
                            case 0x3: return "football/soccer";
                            case 0x4: return "tennis/squash";
                            case 0x5: return "team sports (excluding football)";
                            case 0x6: return "athletics";
                            case 0x7: return "motor sport";
                            case 0x8: return "water sport";
                            case 0x9: return "winter sports";
                            case 0xA: return "equestrian";
                            case 0xB: return "martial sports";
                            case byte n when n >= 0xC && n <= 0xE: return "reserved for future use";
                            case 0xF: return "user defined";
                        }
                        break;
                    }
                case 0x5:
                    {
                        switch (ContentNibble2)
                        {
                            case 0x0: return "children's/youth programmes (general)";
                            case 0x1: return "pre-school children's programmes";
                            case 0x2: return "entertainment programmes for 6 to14";
                            case 0x3: return "entertainment programmes for 10 to 16";
                            case 0x4: return "informational/educational/school programmes";
                            case 0x5: return "cartoons/puppets";
                            case byte n when n >= 0x6 && n <= 0xE: return "reserved for future use";
                            case 0xF: return "user defined";
                        }
                        break;
                    }
                case 0x6:
                    {
                        switch (ContentNibble2)
                        {
                            case 0x0: return "music/ballet/dance (general)";
                            case 0x1: return "rock/pop";
                            case 0x2: return "serious music/classical music";
                            case 0x3: return "folk/traditional music";
                            case 0x4: return "jazz";
                            case 0x5: return "musical/opera";
                            case 0x6: return "ballet";
                            case byte n when n >= 0x7 && n <= 0xE: return "reserved for future use";
                            case 0xF: return "user defined";
                        }
                        break;
                    }
                case 0x7:
                    {
                        switch (ContentNibble2)
                        {
                            case 0x0: return "arts/culture (without music, general)";
                            case 0x1: return "performing arts";
                            case 0x2: return "fine arts";
                            case 0x3: return "religion";
                            case 0x4: return "popular culture/traditional arts";
                            case 0x5: return "literature";
                            case 0x6: return "film/cinema";
                            case 0x7: return "experimental film/video";
                            case 0x8: return "broadcasting/press";
                            case 0x9: return "new media";
                            case 0xA: return "arts/culture magazines";
                            case 0xB: return "fashion";
                            case byte n when n >= 0xC && n <= 0xE: return "reserved for future use";
                            case 0xF: return "user defined";
                        }
                        break;
                    }
                case 0x8:
                    {
                        switch (ContentNibble2)
                        {
                            case 0x0: return "social/political issues/economics (general)";
                            case 0x1: return "magazines/reports/documentary";
                            case 0x2: return "economics/social advisory";
                            case 0x3: return "remarkable people";
                            case byte n when n >= 0x4 && n <= 0xE: return "reserved for future use";
                            case 0xF: return "user defined";
                        }
                        break;
                    }
                case 0x9:
                    {
                        switch (ContentNibble2)
                        {
                            case 0x0: return "education/science/factual topics (general)";
                            case 0x1: return "nature/animals/environment";
                            case 0x2: return "technology/natural sciences";
                            case 0x3: return "medicine/physiology/psychology";
                            case 0x4: return "foreign countries/expeditions";
                            case 0x5: return "social/spiritual sciences";
                            case 0x6: return "further education";
                            case 0x7: return "languages";
                            case byte n when n >= 0x8 && n <= 0xE: return "reserved for future use";
                            case 0xF: return "user defined";
                        }
                        break;
                    }
                case 0xA:
                    {
                        switch (ContentNibble2)
                        {
                            case 0x0: return "leisure hobbies (general)";
                            case 0x1: return "tourism/travel";
                            case 0x2: return "handicraft";
                            case 0x3: return "motoring";
                            case 0x4: return "fitness and health";
                            case 0x5: return "cooking";
                            case 0x6: return "advertisement/shopping";
                            case 0x7: return "gardening";
                            case byte n when n >= 0x8 && n <= 0xE: return "to 0xE reserved for future use";
                            case 0xF: return "user defined";
                        }
                        break;
                    }
                case 0xB:
                    {
                        switch (ContentNibble2)
                        {
                            case 0x0: return "original language";
                            case 0x1: return "black and white";
                            case 0x2: return "unpublished";
                            case 0x3: return "live broadcast";
                            case 0x4: return "plano-stereoscopic";
                            case 0x5: return "local or regional";
                            case byte n when n >= 0x6 && n <= 0xE: return "reserved for future use";
                            case 0xF: return "user defined";
                        }
                        break;
                    }
                case 0xC:
                    {
                        switch (ContentNibble2)
                        {
                            case 0x0: return "adult (general)";
                            case byte n when n >= 0x1 && n <= 0xE: return "reserved for future use";
                            case 0xF: return "user defined";
                        }
                        break;
                    }
                case byte n when n >= 0xD && n <= 0xE: return "reserved for future use";
                case 0xF: return "user defined";                
            }
            throw new Exception("Niblle shall be only half of byte");
        }
        
    }
}
