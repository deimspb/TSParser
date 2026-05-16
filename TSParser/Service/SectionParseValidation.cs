// Copyright 2021 Eldar Nizamutdinov
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

using System.Buffers.Binary;

namespace TSParser.Service
{
    public static class SectionParseValidation
    {
        public const int MaxSectionLength = 4093;
        public const int MinSyntaxSectionBytes = 12;
        public const int MaxDescriptorLength = 255;

        public static ushort ReadSectionLength(ReadOnlySpan<byte> bytes) =>
            (ushort)(BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(1, 2)) & 0x0FFF);

        public static int GetDeclaredSectionByteCount(ushort sectionLength) => 3 + sectionLength;

        public static void ValidateSyntaxSection(ReadOnlySpan<byte> bytes)
        {
            if (!TryValidateSyntaxSection(bytes, out var reason, out _))
                throw CreateException(reason, bytes.Length, ReadSectionLength(bytes));
        }

        public static bool TryValidateSyntaxSection(ReadOnlySpan<byte> bytes, out ParseFailureReason reason, out ushort sectionLength)
        {
            reason = ParseFailureReason.None;
            sectionLength = 0;

            if (bytes.Length < 8)
            {
                reason = ParseFailureReason.BufferTooShort;
                return false;
            }

            sectionLength = ReadSectionLength(bytes);
            if (sectionLength > MaxSectionLength)
            {
                reason = ParseFailureReason.SectionLengthOutOfRange;
                return false;
            }

            var expectedLength = GetDeclaredSectionByteCount(sectionLength);
            if (bytes.Length < expectedLength)
            {
                reason = ParseFailureReason.SectionLengthMismatch;
                return false;
            }

            return true;
        }

        public static void ValidateFixedRecordLoop(
            ReadOnlySpan<byte> bytes,
            ushort sectionLength,
            int recordStartIndex,
            int recordSize,
            int crcSize = 4)
        {
            if (!TryValidateFixedRecordLoop(bytes, sectionLength, recordStartIndex, recordSize, crcSize, out var reason))
                throw CreateException(reason, bytes.Length, sectionLength);
        }

        public static bool TryValidateFixedRecordLoop(
            ReadOnlySpan<byte> bytes,
            ushort sectionLength,
            int recordStartIndex,
            int recordSize,
            int crcSize,
            out ParseFailureReason reason)
        {
            reason = ParseFailureReason.None;

            if (recordSize <= 0)
            {
                reason = ParseFailureReason.InvalidRecordLoop;
                return false;
            }

            var recordAreaLength = sectionLength - (recordStartIndex - 3) - crcSize;
            if (recordAreaLength < 0 || recordAreaLength % recordSize != 0)
            {
                reason = ParseFailureReason.InvalidRecordLoop;
                return false;
            }

            var requiredBytes = recordStartIndex + recordAreaLength + crcSize;
            if (bytes.Length < requiredBytes)
            {
                reason = ParseFailureReason.SectionLengthMismatch;
                return false;
            }

            return true;
        }

        public static bool TryGetDescriptorTotalLength(ReadOnlySpan<byte> bytes, out int totalLength, out ParseFailureReason reason)
        {
            totalLength = 0;
            reason = ParseFailureReason.None;

            if (bytes.Length < 2)
            {
                reason = ParseFailureReason.BufferTooShort;
                return false;
            }

            var headerLength = 2;
            var lengthIndex = 1;
            var tag = bytes[0];

            if ((tag == 0x89 || tag == 0x90) && bytes.Length > 2 && bytes[1] == 0x15)
            {
                headerLength = 3;
                lengthIndex = 2;
                if (bytes.Length < 3)
                {
                    reason = ParseFailureReason.BufferTooShort;
                    return false;
                }
            }

            var descriptorLength = bytes[lengthIndex];
            if (descriptorLength > MaxDescriptorLength)
            {
                reason = ParseFailureReason.InvalidDescriptorLength;
                return false;
            }

            totalLength = headerLength + descriptorLength;
            if (totalLength < headerLength || totalLength > bytes.Length)
            {
                reason = ParseFailureReason.InvalidDescriptorLength;
                return false;
            }

            return true;
        }

        public static void ValidateSpanBounds(ReadOnlySpan<byte> bytes, int offset, int length)
        {
            if (!TryValidateSpanBounds(bytes, offset, length, out var reason))
                throw new SectionParseException(reason, $"Span offset {offset} length {length} exceeds buffer length {bytes.Length}.");
        }

        public static bool TryValidateSpanBounds(ReadOnlySpan<byte> bytes, int offset, int length, out ParseFailureReason reason)
        {
            reason = ParseFailureReason.None;
            if (offset < 0 || length < 0 || offset > bytes.Length || length > bytes.Length - offset)
            {
                reason = ParseFailureReason.SectionLengthMismatch;
                return false;
            }

            return true;
        }

        public static void ValidateDescriptorBuffer(ReadOnlySpan<byte> bytes)
        {
            if (!TryGetDescriptorTotalLength(bytes, out var totalLength, out var reason))
                throw new SectionParseException(reason, $"Invalid descriptor header (length {bytes.Length} bytes).");

            if (totalLength != bytes.Length)
                throw new SectionParseException(
                    ParseFailureReason.InvalidDescriptorLength,
                    $"Descriptor buffer length {bytes.Length} does not match declared total length {totalLength}.");
        }

        internal static SectionParseException CreateException(ParseFailureReason reason, int actualLength, ushort sectionLength) =>
            new(reason, reason switch
            {
                ParseFailureReason.BufferTooShort =>
                    $"Section buffer too short ({actualLength} bytes, need at least 8).",
                ParseFailureReason.SectionLengthOutOfRange =>
                    $"Section length {sectionLength} exceeds maximum {MaxSectionLength}.",
                ParseFailureReason.SectionLengthMismatch =>
                    $"Section buffer length {actualLength} is less than declared section size {GetDeclaredSectionByteCount(sectionLength)}.",
                ParseFailureReason.InvalidRecordLoop =>
                    $"Section length {sectionLength} does not align with the fixed-size record loop.",
                ParseFailureReason.InvalidDescriptorLength =>
                    $"Descriptor length field is invalid for a {actualLength}-byte buffer.",
                ParseFailureReason.DescriptorLoopStall =>
                    "Descriptor loop did not advance.",
                _ => "Section parse failed.",
            });
    }
}
