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
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;

namespace Mubiquity
{
    internal struct ArduinoData
    {
        public string name;
        public Type programmer;
        public uint flashSize;      // from spec sheet
        public uint pageSize;      // from spec sheet
        public ushort programmerPid;
    };

    interface IArduinoProgrammer
    {
        Task program(ArduinoHexFile file);
    }

    class ArduinoCDCProgrammer : IArduinoProgrammer
    {
        // This class implements the avr109 flash protocol
        // http://www.atmel.com/images/doc1644.pdf

        internal enum FlashByteType
        {
            High,
            Low
        }
        private ArduinoData arduinoData;
        Arduino arduino;
        Arduino bootloader;
        const int kProgrammingDelay = 2000;
        const uint kProgrammingBaudRate = 57600;
        const int kSignatureByteCount = 3;

        const char kAVR109Command_CommitPage = 'm';
        const char kAVR109Command_WriteLowByte = 'c';
        const char kAVR109Command_WriteHighByte = 'C';
        const char kAVR109Command_SetAddressLow = 'A';
        const char kAVR109Command_SetAddressHigh = 'H';
        const char kAVR109Command_WriteBlockLength = 'B';
        const char kAVR109Command_WriteBlock = 'F';
        const char kAVR109Command_ExitBootloader = 'E';
        const char kAVR109Command_CheckBlockSupport = 'E';
        const char kAVR109Command_ReadSignature = 's';

        const char kAVR109Response_Yes = 'Y';

        const char kAVR109Response_Success = '\r';

        byte[][] validSignatures = new byte[][]
        {
            new byte[] { 0x87, 0x95, 0x1E}

        };

        public ArduinoCDCProgrammer(Arduino a, ArduinoData ad)
        {
            arduino = a;
            arduinoData = ad;
        }

        internal void verifySignature(byte[] signature)
        {
            bool foundValid = false;
            // compare to known signatures
            foreach (var s in validSignatures)
            {
                if (signature[0] == s[0] &&
                    signature[1] == s[1] &&
                    signature[2] == s[2])
                {
                    foundValid = true;
                    break;
                }
            }

            if (!foundValid)
            {
                throw new Exception(string.Format("Valid signature not found for {0} {1}, {2}", 
                    signature[0].ToString("X2"), signature[0].ToString("X2"), signature[0].ToString("X2")));
            }
        }

        internal async Task<byte> getByte()
        {
            byte[] byteReader = new byte[1];
            await bootloader.reader.LoadAsync(1);

            bootloader.reader.ReadBytes(byteReader);

            return byteReader[0];
        }
        internal async Task<byte[]> getBytes(uint count)
        {
            byte[] byteReader = new byte[count];
            await bootloader.reader.LoadAsync(count);

            bootloader.reader.ReadBytes(byteReader);

            return byteReader;
        }
        internal async Task sendBytes(byte[] bytesToSend)
        {
            bootloader.writer.WriteBytes(bytesToSend);
            await bootloader.writer.StoreAsync();
        }
        internal async Task sendBytesFromArray(byte[] bytesToSend, uint start, uint length)
        {
            byte[] bytesToWrite = new byte[length];

            // Yep, cast to int. Why does C# allow negative indexes?
            Array.Copy(bytesToSend, (int)start, bytesToWrite, 0, (int)length);

            bootloader.writer.WriteBytes(bytesToSend);
            await bootloader.writer.StoreAsync();
        }

        internal async Task sendByte(FlashByteType type, byte byteToSend)
        {
            await sendCommand((type == FlashByteType.High)? kAVR109Command_WriteHighByte : kAVR109Command_WriteLowByte);
            await sendBytes(new byte[] { byteToSend });
            byte ret = await getByte();
            if (ret != kAVR109Response_Success)
            {
                throw new Exception("Did not get expected return from Flashing " + ret.ToString("X2"));
            }
        }

        internal async Task sendCommand(char command)
        {
            byte[] commandBytes =
            {
                Convert.ToByte(command)
            };

            bootloader.writer.WriteBytes(commandBytes);
            await bootloader.writer.StoreAsync();
        }

        internal async Task setAddress(uint address)
        {
            if (address < 0x1000)
            {
                await sendCommand(kAVR109Command_SetAddressLow);
                await sendBytes(new byte[] { (byte)((address >> 8) & 0xff), (byte)((address) & 0xff) });
            }
            else
            {
                await sendCommand(kAVR109Command_SetAddressHigh);
                await sendBytes(new byte[] { (byte)((address >> 16) & 0xff), (byte)((address >> 8) & 0xff), (byte)((address) & 0xff) });
            }

            byte ret = await getByte();
            if (ret != kAVR109Response_Success)
            {
                throw new Exception("Did not get expected return from Flashing " + ret.ToString("X2"));
            }
        }

        internal async Task writeBlock(ArduinoHexFile file, uint address, uint byteCount)
        {
            await setAddress(address >> 1);
            await sendCommand(kAVR109Command_WriteBlockLength);

            await sendBytes(new byte[] { (byte)((byteCount >> 8) & 0xFF), (byte)(byteCount & 0xFF) });

            await sendCommand(kAVR109Command_WriteBlock);

            await sendBytesFromArray(file.Contents, address, byteCount);
            byte ret = await getByte();
            if (ret != kAVR109Response_Success)
            {
                throw new Exception("Failed to write page " + address.ToString("X") + " " + ret.ToString("X2"));
            }
        }

        internal async Task writeFile(ArduinoHexFile file)
        {
            uint blockSize = ((uint)await getByte()) << 8 | await getByte();
            uint address = file.StartOfDataRange;

            // address is odd
            if ((address & 1) == 1)
            {
                await setAddress(address >> 1);
                await sendByte(FlashByteType.Low, 0xFF);
                await sendByte(FlashByteType.High, file[address]);

                if ((address % arduinoData.pageSize) == 0 ||
                    (address > file.EndOfDataRange))
                {
                    await setAddress((address - 2) >> 1);       // Move the address into the write page so atmel chip knows what to commit.
                    await sendCommand(kAVR109Command_CommitPage);
                    await setAddress(address  >> 1);
                }

                address++;
            }

            // Middle of first Block
            if ((address % blockSize) > 0)
            {
                uint byteCount = blockSize - (address % blockSize);

                // Writing past end of page?
                if ((address + byteCount - 1) > file.EndOfDataRange)
                {
                    byteCount = file.EndOfDataRange - address + 1;
                    byteCount &= ~(uint)0x1;  // make word
                }

                if (byteCount > 0)
                {
                    await writeBlock(file, address, byteCount);

                    address += byteCount;
                }
            }

            while (file.EndOfDataRange - address + 1 >= blockSize)
            {
                await writeBlock(file, address, blockSize);

                address += blockSize;
            }


            // any remaining bytes at the end?
            if (file.EndOfDataRange - address + 1 >= 1)
            {
                uint byteCount = file.EndOfDataRange - address + 1;
                await writeBlock(file, address, byteCount);
                address += blockSize;

                // handle odd end byte
                if ((byteCount & 0x1) == 1)
                {
                    await sendBytes(new byte[] { 0xFF });
                    address++;
                }
            }
        }

        public async Task program(ArduinoHexFile file)
        {
            if (!arduino.IsConnected)
            {
                throw new Exception("Arduino is not connected");
            }
            // The Arduino bootloader will identify a baud rate change 
            // from normal (i.e. 57,600) to 1,200 then to close
            // as a signal to enter the bootloader.
            // We execute this jiggle here, then wait for the reboot and reopen the Arduino.
            // we have less than 5 seconds to reopen it.

            arduino.serialDevice.BaudRate = 1200;
            arduino.close();
            await Task.Delay(kProgrammingDelay);

            List<Arduino> arduinoInBootloader = await Arduino.FindArduino(arduino.VID, arduino.ProgrammerPID);
            if (arduinoInBootloader.Count == 1)
            {
                bootloader = arduinoInBootloader[0];
                await bootloader.connect(kProgrammingBaudRate, false);

                // First fetch the signature

                await sendCommand(kAVR109Command_ReadSignature);

                byte[] signature = await getBytes(kSignatureByteCount);

                verifySignature(signature); // throws

                await sendCommand(kAVR109Command_CheckBlockSupport);

                byte blockModeSupported = await getByte();
                if (Convert.ToChar(blockModeSupported) == kAVR109Response_Yes)
                {
                    await writeFile(file);
                }
                else
                {
                    // shrug
                }

                await sendCommand(kAVR109Command_ExitBootloader);

                // This should reboot; rendering the serial device invalid.
                await Task.Delay(kProgrammingDelay);

                // Lastly reconnect
                await arduino.connect();
            }
        }
    }

    class Arduino
    {
        static Dictionary<string, ArduinoData> knownArduinoVID = new Dictionary<string, ArduinoData>
        {
            { "VID_2341", new ArduinoData() { name = "Leonardo", programmer = typeof(ArduinoCDCProgrammer), programmerPid = 0x36, flashSize = 28672, pageSize = 2560 } },
            { "VID_2A03", new ArduinoData() { name = "Leonardo", programmer = typeof(ArduinoCDCProgrammer), programmerPid = 0x03, flashSize = 28672, pageSize = 2560 } },
        };

        private ArduinoData arduinoData;

        public SerialDevice serialDevice { get; private set; }  = null;
        public string DeviceId { get; private set; }  = "";

        public DataWriter writer { get; private set; } = null;
        public DataReader reader { get; private set; } = null;


        private Arduino(string id, ArduinoData data)
        {
            DeviceId = id;
            arduinoData = data;
        }

        private Arduino(string id)
        {
            DeviceId = id;
        }

        public bool IsConnected
        {
            get { return serialDevice != null; }
        }

        public ushort VID { get; private set; } = 0;

        public ushort PID { get; private set; } = 0;

        public ushort ProgrammerPID { get { return arduinoData.programmerPid; } }

        public IArduinoProgrammer GetProgrammer()
        {
            var programmer = Activator.CreateInstance(arduinoData.programmer, new object[] { this, arduinoData }) as IArduinoProgrammer;
            return programmer;
        }

        public async Task connect(uint baud = 57600, bool enableDTR = true)
        {
            serialDevice = await Arduino.CreateSerialDevice(DeviceId, baud);
            if (serialDevice != null)
            {
                // Save these off.
                this.VID = serialDevice.UsbVendorId;
                this.PID = serialDevice.UsbProductId;
                writer = new DataWriter(serialDevice.OutputStream);
                reader = new DataReader(serialDevice.InputStream);
                if (enableDTR)
                {
                    // don't set this to enableDTR - it throws if you touch it during bootloader.
                    serialDevice.IsDataTerminalReadyEnabled = true;
                }
            }
        }

        public void close()
        {
            reader.Dispose();
            reader = null;
            writer.Dispose();
            writer = null;

            serialDevice.Dispose();
            serialDevice = null;
        }


        static async public Task<List<Arduino>> FindArduino(ushort svid = 0, ushort spid = 0)
        {
            string vid = svid.ToString("X2");
            string pid = spid.ToString("X2");

            List<Arduino> foundArduino = new List<Arduino>();
            string selector = SerialDevice.GetDeviceSelector();
            var deviceCollection = await DeviceInformation.FindAllAsync(selector);

            if (deviceCollection.Count == 0)
                return foundArduino;

            for (int i = 0; i < deviceCollection.Count; ++i)
            {
                if (svid == 0 || spid == 0)
                {
                    foreach (var known in knownArduinoVID)
                    {
                        // Device Id is of the form 
                        // "\\\\?\\USB#VID_2341&PID_8036&MI_00#7&175bcd40&0&0000#{86e0d1e0-8089-11d0-9ce4-08003e301f73}"
                        // Here, we're looking for an Id which contains a VID like "VID_2341"
                        if (deviceCollection[i].Id.Contains(known.Key))
                        {
                            var a = new Arduino(deviceCollection[i].Id, knownArduinoVID[known.Key]);
                            foundArduino.Add(a);
                            break;
                        }
                    }
                }
                else if (deviceCollection[i].Id.Contains(vid) && (string.IsNullOrEmpty(pid) || deviceCollection[i].Id.Contains(pid)))
                {
                    var a = new Arduino(deviceCollection[i].Id);
                    foundArduino.Add(a);
                }
            }
            return foundArduino;
        }

        static public async Task<SerialDevice> CreateSerialDevice(string deviceId, uint baud = 57600)
        {
            SerialDevice serialDevice = await SerialDevice.FromIdAsync(deviceId);
            if (serialDevice != null)
            {
                serialDevice.BaudRate = baud;
                serialDevice.Parity = SerialParity.None;
                serialDevice.DataBits = 8;
                serialDevice.StopBits = SerialStopBitCount.One;
                serialDevice.Handshake = SerialHandshake.None;
                serialDevice.ReadTimeout = TimeSpan.FromSeconds(5);
                serialDevice.WriteTimeout = TimeSpan.FromSeconds(5);
                serialDevice.Handshake = SerialHandshake.RequestToSendXOnXOff;
            }

            return serialDevice;
        }
    }
}
