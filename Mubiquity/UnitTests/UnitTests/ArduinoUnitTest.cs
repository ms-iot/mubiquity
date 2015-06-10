using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mubiquity;
using Windows.Storage;
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;

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
