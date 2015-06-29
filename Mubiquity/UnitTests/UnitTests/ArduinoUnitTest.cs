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

using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
using Mubiquity;
using System.IO;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class ArduinoUnitTest
    {
        [TestMethod]
        public async Task TestFindArduino()
        {
            var arduinoList = await Arduino.FindArduino();
        }

        [TestMethod]
        public async Task TestConnectToArduino()
        {
            var arduinoList = await Arduino.FindArduino();
            if (arduinoList.Count > 0)
            {
                var arduino = arduinoList[0];
                await arduino.connect();
            }
        }

        [TestMethod]
        public async Task TestHexFileParseLine()
        {
            byte[] lineExpected = new byte[] { 0x0C, 0x94, 0x67, 0x01, 0x0C, 0x94, 0x8F, 0x01, 0x0C, 0x94, 0x8F, 0x01, 0x0C, 0x94, 0x8F, 0x01 };
            string lineToParse = ":100000000C9467010C948F010C948F010C948F0158\n";
            Stream lineStream = streamFromString(lineToParse);
            ArduinoHexFile hexFile = new ArduinoHexFile((uint)lineExpected.Length);


            await hexFile.Parse(lineStream);

            Assert.AreEqual(hexFile.Contents.Length, lineExpected.Length);
            CollectionAssert.AreEqual(hexFile.Contents, lineExpected);
        }

        [TestMethod]
        public async Task TestHexFileParseMultiLine()
        {
            byte[] lineExpected = new byte[] { 0x0C, 0x94, 0x67, 0x01, 0x0C, 0x94, 0x8F, 0x01, 0x0C, 0x94, 0x8F, 0x01, 0x0C, 0x94, 0x8F, 0x01,
                                               0x0C, 0x94, 0x8F, 0x01, 0x0C, 0x94, 0x8F, 0x01, 0x0C, 0x94, 0x8F, 0x01, 0x0C, 0x94, 0x8F, 0x01,
                                               0x0C, 0x94, 0x8F, 0x01, 0x0C, 0x94, 0x8F, 0x01, 0x0C, 0x94, 0x84, 0x06, 0x0C, 0x94, 0x50, 0x05};
            string lineToParse = ":100000000C9467010C948F010C948F010C948F0158\n:100010000C948F010C948F010C948F010C948F0120\n:100020000C948F010C948F010C9484060C94500551\n";
            Stream lineStream = streamFromString(lineToParse);
            ArduinoHexFile hexFile = new ArduinoHexFile((uint)lineExpected.Length);


            await hexFile.Parse(lineStream);

            Assert.AreEqual(hexFile.Contents.Length, lineExpected.Length);
            CollectionAssert.AreEqual(hexFile.Contents, lineExpected);
        }

        [TestMethod]
        public async Task TestHexFileParseShortLine()
        {
            byte[] lineExpected = new byte[] { 0x05, 0x90, 0xF4, 0x91, 0xE0, 0x2D, 0x09, 0x94, 0xF8, 0x94, 0xFF, 0xCF };
            string lineToParse = ":0C1280000590F491E02D0994F894FFCF44\n";
            Stream lineStream = streamFromString(lineToParse);
            ArduinoHexFile hexFile = new ArduinoHexFile((uint)lineExpected.Length);


            await hexFile.Parse(lineStream);

            Assert.AreEqual(hexFile.Contents.Length, lineExpected.Length);
            CollectionAssert.AreEqual(hexFile.Contents, lineExpected);
        }

        [TestMethod]
        public async Task TestHexFileParseEndOfFile()
        {
            byte[] lineExpected = new byte[] { };
            string lineToParse = ":00000001FF\n";
            Stream lineStream = streamFromString(lineToParse);
            ArduinoHexFile hexFile = new ArduinoHexFile((uint)lineExpected.Length);


            await hexFile.Parse(lineStream);

            Assert.AreEqual(hexFile.Contents.Length, lineExpected.Length);
            CollectionAssert.AreEqual(hexFile.Contents, lineExpected);
        }

        [TestMethod]
        public async Task TestHexFileLoader()
        {
            var arduinoList = await Arduino.FindArduino();
            if (arduinoList.Count > 0)
            {
                var arduino = arduinoList[0];
                await arduino.connect();

                var programmer = arduino.GetProgrammer();

                ArduinoHexFile hexFile = await ArduinoHexFile.LoadFirmwareFromResource("ms-appx:///Assets/Blink.cpp.hex", 28672);

                Assert.AreEqual(hexFile.Contents[0], 0x0C);
            }
        }

        [TestMethod]
        public async Task TestProgrammingLiveArduino()
        {
            var arduinoList = await Arduino.FindArduino();
            if (arduinoList.Count > 0)
            {
                var arduino = arduinoList[0];
                await arduino.connect();
                Assert.IsTrue(arduino.IsConnected);
                var programmer = arduino.GetProgrammer();

                ArduinoHexFile hexFile = await ArduinoHexFile.LoadFirmwareFromResource("ms-appx:///Assets/Blink.cpp.hex", 28672);

                await programmer.program(hexFile);
                await Task.Delay(5000);
            }
        }

        private Stream streamFromString(string s)
        {
            MemoryStream ms = new MemoryStream();
            StreamWriter sw = new StreamWriter(ms);
            sw.Write(s);
            sw.Flush();
            ms.Position = 0;

            return ms;
        }
    }
}
