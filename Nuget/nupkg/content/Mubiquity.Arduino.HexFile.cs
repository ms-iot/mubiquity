/*
    Copyright(c) Microsoft Corp. All rights reserved.
    
    The MIT License(MIT)
    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files(the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions :
    The above copyright notice and this permission notice shall be included in
    all copies or substantial portions of the Software.
    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
    THE SOFTWARE.
*/

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;


namespace Mubiquity
{

    class ArduinoHexFile
    {
        public uint flashSize { get; private set; } = 0;

        public byte[] Contents { get; private set; } = null;

        public uint StartOfDataRange { get; private set; } = 0;

        public uint EndOfDataRange { get { return (uint)Contents.Length; } }

        enum RecordType
        {
            Data,
            EndOfFile,
            ExtendedSegmentAddress,
            StartSegmentAddress,
            ExtendedLinearAddress,
            StartLinearAddress
        };

        byte parseByte(string line, ref int index, ref uint checksum)
        {
            string s = line.Substring(index, 2);
            index += 2;
            byte ret = byte.Parse(s, NumberStyles.HexNumber | NumberStyles.AllowHexSpecifier);
            checksum += ret;
            return ret;
        }
        ushort parseWord(string line, ref int index, ref uint checksum)
        {
            byte upper = parseByte(line, ref index, ref checksum);
            byte lower = parseByte(line, ref index, ref checksum);
            return (ushort)((upper << (ushort)8) | (ushort)lower);
        }

        public ArduinoHexFile(uint fs)
        {
            flashSize = fs;
            Contents = new byte[flashSize];
        }

        public byte this[uint i]
        {
            get
            {
                return Contents[i];
            }
            internal set
            {
                Contents[i] = value;
            }
        }

        public async Task Parse(Stream stream)
        {
            using (StreamReader sr = new StreamReader(stream))
            {
                uint baseAddress = 0;

                while (sr.Peek() >= 0)
                {
                    string line = await sr.ReadLineAsync();

                    // :100000001BC100003AC1000038C1000036C1000029
                    // :    -   Line start indicator colon
                    // 10   -   Byte count in hex (here it is 16 bytes)
                    // 0000 -   Address 4 hex digits (16 bit address)
                    // 00   -   Record type (00 - 05). We will be using 00 (data record) and 01 (end of file record)
                    // Data -   n bytes in 2n hex digits
                    // 29   -   Checksum in 2s complement

                    int lineIndex = 0;
                    uint accumulatedChecksum = 0;
                    if (line[lineIndex++] != ':')
                    {
                        throw new InvalidDataException("Missing expected token");
                    }

                    byte byteCount = parseByte(line, ref lineIndex, ref accumulatedChecksum);
                    ushort offset = parseWord(line, ref lineIndex, ref accumulatedChecksum);
                    RecordType recordType = (RecordType)parseByte(line, ref lineIndex, ref accumulatedChecksum);

                    switch (recordType)
                    {
                        case RecordType.Data:
                            {
                                int byteIndex = 0;
                                while (byteIndex < byteCount && lineIndex < line.Length)
                                {
                                    Contents[baseAddress + offset + byteIndex] = parseByte(line, ref lineIndex, ref accumulatedChecksum);

                                    byteIndex++;
                                }
                            }
                            break;

                        case RecordType.ExtendedSegmentAddress:
                            {
                                var high = parseByte(line, ref lineIndex, ref accumulatedChecksum);
                                var low = parseByte(line, ref lineIndex, ref accumulatedChecksum);

                                baseAddress = (uint)(((high << 8) | low) << 4);
                            }
                            break;

                        case RecordType.ExtendedLinearAddress:
                            {
                                var high = parseByte(line, ref lineIndex, ref accumulatedChecksum);
                                var low = parseByte(line, ref lineIndex, ref accumulatedChecksum);

                                baseAddress = (uint)(((high << 8) | low) << 16);
                            }
                            break;

                        case RecordType.EndOfFile:
                            // no data in this marker 
                            break;
                    }

                    // parse checksum
                    uint ignoredChecksum = 0;
                    byte specifiedCheckSum = parseByte(line, ref lineIndex, ref ignoredChecksum);

                    var twosComplimentChecksum = (uint)((~specifiedCheckSum + 1) & 0xFF);

                    // Looking for the least significant byte
                    accumulatedChecksum &= 0xFF;
                    accumulatedChecksum -= twosComplimentChecksum;
                    if (accumulatedChecksum != 0)
                    {
                        throw new Exception("Image appears to be corrupt");
                    }
                }
            }
        }

        public static async Task<ArduinoHexFile> LoadFirmwareFromResource(string uriToHexFile, uint flashSize)
        {
            var uri = new Uri(uriToHexFile);
            var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            var stream = await file.OpenStreamForReadAsync();

            ArduinoHexFile hexFile = new ArduinoHexFile(flashSize);

            await hexFile.Parse(stream);

            return hexFile;
        }

    }
}
