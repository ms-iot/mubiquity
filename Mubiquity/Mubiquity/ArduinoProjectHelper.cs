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
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Mubiquity
{
    struct ArduinoBoardInfo
    {
        public string VARIANT;
        public string FriendlyName;
        public string MCU;
        public string F_CPU;
        public string DEF_CPU;
        public string USB_VID;
        public string USB_PID;
    }


    class ArduinoProjectHelper
    {
        string _pathToMakeFile;
        string _pathToIno;
        string _variant = "Leonardo";

        const string kManifestStringName = "Arduino_Makefile_Template.nmake";
        public string ManifestPrefix { get; set; } = "";
        public string Target { get; set; } = "Arduino.hex";
        public string Intermediate { get; set; } = "Obj";

        public string ArduinoVariant
        {
            get
            {
                return _variant;
            }

            set
            {
                _variant = value;

            }
        }

        public ArduinoBoardInfo ArduinoVariantInfo
        {
            get
            {
                var info = getBoardInfo();
                return info[ArduinoVariant.ToLower()];
            }
        }

        public List<string> ArduinoVariants
        {
            get
            {
                List<string> variants = new List<string>();

                var info = getBoardInfo();

                foreach (var v in info.Keys)
                {
                    variants.Add(v);
                }

                return variants;
            }
        }


        public string ArduinoPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Arduino");
            }
        }

        public string ArduinoVariantPath
        {
            get
            {
                return Path.Combine(ArduinoPath, "hardware\\arduino\\avr\\variants", ArduinoVariant);
            }
        }

        public string ArduinoToolsPath
        {
            get
            {
                return Path.Combine(ArduinoPath, "hardware\\tools\\avr\\bin");
            }
        }

        public string ArduinoFirmwarePath
        {
            get
            {
                return Path.Combine(ArduinoPath, "hardware\\arduino\\avr\\cores\\arduino");
            }
        }

        public string ArduinoBoardPath
        {
            get
            {
                return Path.Combine(ArduinoPath, "hardware\\arduino\\avr\\boards.txt");
            }
        }

        public ArduinoProjectHelper(string pathToMakeFile, string pathToIno)
        {
            _pathToMakeFile = pathToMakeFile;
            _pathToIno = pathToIno;
        }

        public Dictionary<string, ArduinoBoardInfo> getBoardInfo()
        {
            var dict = new Dictionary<string, ArduinoBoardInfo>();

            ArduinoBoardInfo currentBoard = new ArduinoBoardInfo();
            using (StreamReader boardTextFile = new StreamReader(ArduinoBoardPath, false))
            {
                while (!boardTextFile.EndOfStream)
                {
                    string line = boardTextFile.ReadLine();
                    string[] kvp = line.Split('=');

                    if (line.Contains(".name="))
                    {
                        string[] processed = line.Split('.', '=');

                        if (currentBoard.VARIANT != null)
                        {
                            dict.Add(currentBoard.VARIANT.ToLower(), currentBoard);
                        }

                        currentBoard = new ArduinoBoardInfo();
                        currentBoard.VARIANT = processed[0];
                        currentBoard.FriendlyName = processed[processed.Length - 1];
                    }
                    else if (line.Contains("mcu"))
                    {
                        currentBoard.MCU = kvp[kvp.Length - 1];
                    }
                    else if (line.Contains("build.f_cpu"))
                    {
                        currentBoard.F_CPU = kvp[kvp.Length - 1];
                    }
                    else if (line.Contains("build.board"))
                    {
                        currentBoard.DEF_CPU = kvp[kvp.Length - 1];
                    }
                    else if (line.Contains("build.vid"))
                    {
                        currentBoard.USB_VID = kvp[kvp.Length - 1];
                    }
                    else if (line.Contains("build.pid"))
                    {
                        currentBoard.USB_PID = kvp[kvp.Length - 1];
                    }
                }

            }

            if (!dict.ContainsKey(currentBoard.VARIANT.ToLower()))
            { 
                dict.Add(currentBoard.VARIANT.ToLower(), currentBoard);
            }

            return dict;
        }


        public List<string> getArduinoFiles(string filter = "*.*")
        {
            var files = new List<string>();
            var rawFiles = Directory.EnumerateFiles(ArduinoFirmwarePath, filter, SearchOption.TopDirectoryOnly);
            foreach(var file in rawFiles)
            {
                files.Add(file);
            }
            return files;
        }

        public string GetShortPath(string longPath)
        {
            // Nmake - 1980s technology...
            StringBuilder shortPath = new StringBuilder(longPath.Length + 1);

            if (0 == ArduinoProjectHelper.GetShortPathName(longPath, shortPath, shortPath.Capacity))
            {
                return longPath;
            }

            return shortPath.ToString();
        }

        public void regenerateMakefile()
        {
            var names = typeof(ArduinoProjectHelper).Assembly.GetManifestResourceNames();
            var streamOfTemplate = typeof(ArduinoProjectHelper).Assembly.GetManifestResourceStream(ManifestPrefix + "." + kManifestStringName);
            if (streamOfTemplate == null || streamOfTemplate.Length == 0)
            {
                throw new Exception("Expecting template in resources");
            }

            ArduinoBoardInfo currentVariant = ArduinoVariantInfo;

            using (StreamWriter newMakeFile = new StreamWriter(_pathToMakeFile, false))
            {

                // squirt out the basics
                newMakeFile.WriteLine("ARDUINO_INSTALL=" + GetShortPath(ArduinoPath));
                newMakeFile.WriteLine("ARDUINO_FIRMWARE=" + GetShortPath(ArduinoFirmwarePath));
                newMakeFile.WriteLine("ARDUINO_TOOLS=" + GetShortPath(ArduinoToolsPath));
                newMakeFile.WriteLine("VARIANT_PATH=" + GetShortPath(ArduinoVariantPath));
                newMakeFile.Write("CXXSRC=");

                var cppFiles = getArduinoFiles("*.cpp");
                var cFiles = getArduinoFiles("*.c");

                foreach (var cppFile in cppFiles)
                {
                    newMakeFile.WriteLine("\t\\");
                    newMakeFile.Write(string.Format("\t{0}", GetShortPath(cppFile)));
                }
                newMakeFile.WriteLine("");

                newMakeFile.Write("CSRC=");

                foreach (var cFile in cFiles)
                {
                    newMakeFile.WriteLine("\t\\");
                    newMakeFile.Write(string.Format("\t{0}", GetShortPath(cFile)));
                }
                newMakeFile.WriteLine("");

                if (_pathToIno != null)
                {
                    newMakeFile.Write("INOSRC=\\");
                    newMakeFile.Write(string.Format("\t{0}", GetShortPath(_pathToIno)));

                    newMakeFile.WriteLine("");
                }

                newMakeFile.Write("OBJ=");
                foreach (var cppFile in cppFiles)
                {
                    var objName = Path.GetFileNameWithoutExtension(cppFile) + ".obj";
                    newMakeFile.WriteLine("\t\\");
                    newMakeFile.Write(string.Format("\t$(O)\\{0}", objName));
                }
                foreach (var cFile in cFiles)
                {
                    var objName = Path.GetFileNameWithoutExtension(cFile) + ".obj";

                    newMakeFile.WriteLine("\t\\");
                    newMakeFile.Write(string.Format("\t$(O)\\{0}", objName));
                }

                if (_pathToIno != null)
                {
                    var objName = Path.GetFileNameWithoutExtension(_pathToIno) + ".obj";

                    newMakeFile.WriteLine("\t\\");
                    newMakeFile.Write(string.Format("\t$(O)\\{0}", objName));
                }

                newMakeFile.WriteLine("");
                newMakeFile.WriteLine("");

                newMakeFile.WriteLine(string.Format("TARGET={0}", Target));
                newMakeFile.WriteLine(string.Format("O={0}", Intermediate));
                newMakeFile.WriteLine("");
                newMakeFile.WriteLine(string.Format("MCU={0}", currentVariant.MCU));
                newMakeFile.WriteLine(string.Format("F_CPU={0}", currentVariant.F_CPU));
                newMakeFile.WriteLine(string.Format("VARIANT={0}", currentVariant.VARIANT));
                newMakeFile.WriteLine(string.Format("DEF_CPU={0}", currentVariant.DEF_CPU));
                newMakeFile.WriteLine(string.Format("D_USB_VID=USB_VID={0}", currentVariant.USB_VID));
                newMakeFile.WriteLine(string.Format("D_USB_PID=USB_PID={0}", currentVariant.USB_PID));
                newMakeFile.WriteLine("");


                using (StreamReader templateReader = new StreamReader(streamOfTemplate))
                {
                    while (!templateReader.EndOfStream)
                    {
                        string readLine = templateReader.ReadLine();
                        newMakeFile.WriteLine(readLine);
                    }
                }
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern Int32 GetShortPathName(String path, StringBuilder shortPath, Int32 shortPathLength);
    }
}
