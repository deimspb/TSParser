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

namespace TSParser.Input;

internal sealed class FileTsSource : ITsInputSource
{
    private const int ProbeBytes = 2040;
    private readonly string _fileName;

    public FileTsSource(string fileName)
    {
        if (!File.Exists(fileName))
        {
            throw new TsParserConfigurationException($"Invalid file name: {fileName}");
        }

        if (new FileInfo(fileName).Length < ProbeBytes)
        {
            throw new TsParserConfigurationException("File length is less than 2040 bytes.");
        }

        _fileName = fileName;
    }

    public void Run(TsInputSourceContext context)
    {
        using FileStream fileStream = new(_fileName, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 348 * 188, FileOptions.SequentialScan);
        using BinaryReader binaryReader = new(fileStream);

        try
        {
            var firstFileBytes = binaryReader.ReadBytes(ProbeBytes);
            var packetLength = TsPacketLengthDetector.GetPacketLength(firstFileBytes, out var syncByte);

            if (syncByte == -1)
            {
                throw new TsSyncException("Cannot sync with TS.");
            }

            var maxBuffer = 22 * packetLength;
            Span<byte> buffer = new byte[maxBuffer];

            if (syncByte > 0)
            {
                fileStream.Seek(syncByte, SeekOrigin.Begin);
            }
            else
            {
                fileStream.Seek(0, SeekOrigin.Begin);
            }

            context.SourceStarted();
            Logger.Send(LogStatus.INFO, $"Start ts file {_fileName} parsing, ts packet length: {packetLength}");

            long globalOffset = 0;
            int bytesRead;
            while ((bytesRead = fileStream.Read(buffer)) > 0 && !context.CancellationToken.IsCancellationRequested)
            {
                var offset = 0;
                _ = TsPacketLengthDetector.GetPacketLength(buffer, out offset);

                if (offset > 0)
                {
                    fileStream.Seek(offset + globalOffset, SeekOrigin.Begin);
                }

                context.SetStreamByteOffset(globalOffset);
                context.Publish(buffer[..bytesRead], packetLength);
                context.SetStreamByteOffset(null);
                globalOffset += bytesRead;
            }
        }
        catch (Exception ex) when (context.IsExpectedShutdown(ex))
        {
        }
        catch (Exception ex)
        {
            Logger.Send(LogStatus.EXCEPTION, $"Exception catch in file reader method {ex}", ex);
            throw;
        }
        finally
        {
            context.SetStreamByteOffset(null);
        }
    }

    public void Stop()
    {
    }

    public void Dispose()
    {
    }
}
