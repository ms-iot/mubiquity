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
using System.Text.RegularExpressions;
using System.Diagnostics;

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

    class LibraryProperties
    {
        public string Name = "";
        public string Version = "";
        public string BasePath = "";

        public List<string> Architectures = new List<string>();

        public string GetSourcePath(string architecture)
        {
            if (Architectures.Contains("*"))
            {
                return Path.Combine(BasePath, "src");
            }
            else if (Architectures.Contains(architecture))
            {
                return Path.Combine(BasePath, "src", architecture);
            }

            return null;
        }

        public LibraryProperties()
        {
        }

        public void load(string directory)
        {
            BasePath = directory;

            var path = Path.Combine(directory, "library.properties");
            if (File.Exists(path))
            {

                using (StreamReader libProps = new StreamReader(path, false))
                {
                    load(libProps);
                }
            }
            else
            {
                // Legacy library; sources are in src
                Architectures.Add("*");
            }
        }

        public void load(StreamReader reader)
        {
            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine();
                string[] kvp = line.Split(new char[] { '=' }, 2);

                if (kvp.Length > 1)
                {
                    if (line.Contains("name"))
                    {
                        Name = kvp[1];
                    }
                    else if (line.Contains("version"))
                    {
                        Version = kvp[1];
                    }
                    else if (line.Contains("architectures"))
                    {
                        var arch = kvp[1].Split(',');
                        foreach (var a in arch)
                        {
                            Architectures.Add(a);
                        }
                    }
                }
            }
        }
    }

    class ArduinoLibrary
    {
        public LibraryProperties properties = new LibraryProperties();
        public string libraryPath;
        public string headerPath;
        public List<string> CPPSourceFiles = new List<string>();
        public List<string> CSourceFiles = new List<string>();

        public ArduinoLibrary(string path)
        {
            libraryPath = path;

            properties.load(libraryPath);

            headerPath = Path.Combine(libraryPath, "src");
        }

        public void loadSources(string architecture)
        {
            try
            {
                var sourcePath = properties.GetSourcePath(architecture);
                var rawFiles = Directory.EnumerateFiles(sourcePath, "*.c", SearchOption.TopDirectoryOnly);
                foreach (var file in rawFiles)
                {
                    CSourceFiles.Add(file);
                }

                rawFiles = Directory.EnumerateFiles(sourcePath, "*.cpp", SearchOption.TopDirectoryOnly);
                foreach (var file in rawFiles)
                {
                    CPPSourceFiles.Add(file);
                }
            }
            catch (FileNotFoundException)
            {
                // Nothing found
            }
        }

        static public ArduinoLibrary createFromIncludeFile(string includeFilename)
        {
            // Notes:
            // We process the ino to extract header files.
            // The .h is removed from filename, and used to locate the library in the following locations:
            // Documents\Arduino\Libraries
            // C:\Program Files (x86)\Arduino\libraries
            // C:\Program Files (x86)\Arduino\hardware\arduino\avr\libraries
            // If the Library itself has a library.properties file, it needs to be parsed 
            // to see which architectures are supported.
            //  * - sources and header are in Library\src
            //  avr or sam - header is in Library\src, sources are in Library\src\$arch\

            ArduinoLibrary lib = null;
            var libraryName = Path.GetFileNameWithoutExtension(includeFilename);
            var libraryPath = ArduinoLibrary.findLibraryPath(libraryName);
            if (!string.IsNullOrEmpty(libraryPath))
            {
                lib = new ArduinoLibrary(libraryPath);

            }

            return lib;
        }

        static private string findLibraryPathInPath(string name, string path)
        {
            string libraryPath = Path.Combine(path, name);
            if (Directory.Exists(libraryPath))
            {
                return libraryPath;
            }

            return null;
        }

        static private string findLibraryPath(string name)
        {
            string libraryPath = findLibraryPathInPath(name, ArduinoPathHelper.ArduinoUserLibraryPath);
            if (string.IsNullOrEmpty(libraryPath))
            {
                libraryPath = findLibraryPathInPath(name, ArduinoPathHelper.ArduinoSystemLibraryPath);
                if (string.IsNullOrEmpty(libraryPath))
                {
                    libraryPath = findLibraryPathInPath(name, ArduinoPathHelper.ArduinoSystemAVRLibraryPath);
                }
            }

            return libraryPath;
        }
    }


    class ArduinoProjectHelper
    {
        string _pathToMakeFile = null;
        string _pathToIno = null;
        string _variant = "Leonardo";
        StreamReader _inoReader = null;

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


        public ArduinoProjectHelper(string pathToMakeFile, string pathToIno)
        {
            _pathToMakeFile = pathToMakeFile;
            _pathToIno = pathToIno;
        }
        public ArduinoProjectHelper(string pathToMakeFile, StreamReader inoReader)
        {
            _pathToMakeFile = pathToMakeFile;
            _inoReader = inoReader;
        }

        public Dictionary<string, ArduinoBoardInfo> getBoardInfo()
        {
            var dict = new Dictionary<string, ArduinoBoardInfo>();

            ArduinoBoardInfo currentBoard = new ArduinoBoardInfo();
            using (StreamReader boardTextFile = new StreamReader(ArduinoPathHelper.ArduinoBoardPath, false))
            {
                while (!boardTextFile.EndOfStream)
                {
                    string line = boardTextFile.ReadLine();
                    if (!string.IsNullOrEmpty(line))
                    {
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
                        else
                        {
                            string[] kvp = line.Split('=');
                            if (kvp.Length > 1)
                            { 
                                if (line.Contains("mcu"))
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
            var rawFiles = Directory.EnumerateFiles(ArduinoPathHelper.ArduinoFirmwarePath, filter, SearchOption.TopDirectoryOnly);
            foreach(var file in rawFiles)
            {
                files.Add(file);
            }
            return files;
        }

        public List<string> extractLibrariesFromStream(StreamReader inoReader)
        {
            List<string> libraries = new List<string>();
            string includePattern = "^\\s*\\#include\\s*[\"<]\\s*([^\">]+)\\s*[\">]";

            while (!inoReader.EndOfStream)
            {
                string readLine = inoReader.ReadLine();

                var match = Regex.Match(readLine, includePattern);
                if (match.Groups.Count > 1)
                {
                    // Group 1 is the whole match; Group 2 is the inner capture
                    libraries.Add(match.Groups[1].Value);
                }
            }

            return libraries;
        }

        public List<ArduinoLibrary> extractLibrariesFromIno(StreamReader inoReader)
        {
            var arduinoLibraries = new List<ArduinoLibrary>();
            var libraryNames = extractLibrariesFromStream(inoReader);
            foreach (var libName in libraryNames)
            {
                arduinoLibraries.Add(ArduinoLibrary.createFromIncludeFile(libName));
            }

            return arduinoLibraries;
        }

        public List<ArduinoLibrary> extractLibrariesFromIno()
        {
            if (_inoReader != null)
            {
                return extractLibrariesFromIno(_inoReader);
            }
            else if (!string.IsNullOrEmpty(_pathToIno))
            {
                using (StreamReader inoReader = new StreamReader(_pathToIno))
                {
                    return extractLibrariesFromIno(inoReader);
                }
            }

            return new List<ArduinoLibrary>();
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
                newMakeFile.WriteLine("ARDUINO_INSTALL=" + ArduinoPathHelper.ArduinoPathShort);
                newMakeFile.WriteLine("ARDUINO_FIRMWARE=" + ArduinoPathHelper.ArduinoFirmwarePathShort);
                newMakeFile.WriteLine("ARDUINO_TOOLS=" + ArduinoPathHelper.ArduinoToolsPathShort);
                newMakeFile.WriteLine("VARIANT_PATH=" + ArduinoPathHelper.GetArduinoVariantPathShort(ArduinoVariant));

                // Write out CPP & c files
                var cppFiles = getArduinoFiles("*.cpp");
                var cFiles = getArduinoFiles("*.c");

                // Find libraries
                var libraries = extractLibrariesFromIno();

                foreach (var lib in libraries)
                {
                    foreach (var cppFile in lib.CPPSourceFiles)
                    {
                        cppFiles.Add(cppFile);
                    }

                    foreach (var cFile in lib.CSourceFiles)
                    {
                        cFiles.Add(cFile);
                    }

                    newMakeFile.WriteLine("CFLAGS= $(CFLAGS) -I " + ArduinoPathHelper.GetShortPath(lib.headerPath));
                    newMakeFile.WriteLine("CXXFLAGS= $(CXXFLAGS) -I " + ArduinoPathHelper.GetShortPath(lib.headerPath));
                }

                newMakeFile.Write("CXXSRC=");
                foreach (var cppFile in cppFiles)
                {
                    newMakeFile.WriteLine("\t\\");
                    newMakeFile.Write(string.Format("\t{0}", ArduinoPathHelper.GetShortPath(cppFile)));
                }
                newMakeFile.WriteLine("");

                newMakeFile.Write("CSRC=");

                foreach (var cFile in cFiles)
                {
                    newMakeFile.WriteLine("\t\\");
                    newMakeFile.Write(string.Format("\t{0}", ArduinoPathHelper.GetShortPath(cFile)));
                }
                newMakeFile.WriteLine("");

                if (_pathToIno != null)
                {
                    newMakeFile.Write("INOSRC=\\");
                    newMakeFile.Write(string.Format("\t{0}", ArduinoPathHelper.GetShortPath(_pathToIno)));

                    newMakeFile.WriteLine("");
                }

                // Write out OBJs

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

    }

    class ArduinoPathHelper
    {
        public static string ArduinoPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Arduino");
        public static string ArduinoPathShort => ArduinoPathHelper.GetShortPath(ArduinoPathHelper.ArduinoPath);

        public static string ArduinoToolsPath => Path.Combine(ArduinoPathHelper.ArduinoPath, "hardware\\tools\\avr\\bin");
        public static string ArduinoToolsPathShort => ArduinoPathHelper.GetShortPath(ArduinoPathHelper.ArduinoToolsPath);

        public static string ArduinoFirmwarePath => Path.Combine(ArduinoPathHelper.ArduinoPath, "hardware\\arduino\\avr\\cores\\arduino");
        public static string ArduinoFirmwarePathShort => ArduinoPathHelper.GetShortPath(ArduinoPathHelper.ArduinoFirmwarePath);

        public static string ArduinoBoardPath => Path.Combine(ArduinoPathHelper.ArduinoPath, "hardware\\arduino\\avr\\boards.txt");
        public static string ArduinoBoardPathShort => ArduinoPathHelper.GetShortPath(ArduinoPathHelper.ArduinoBoardPath);

        public static string ArduinoSystemLibraryPath => Path.Combine(ArduinoPathHelper.ArduinoPath, "libraries");
        public static string ArduinoSystemAVRLibraryPath => Path.Combine(ArduinoPathHelper.ArduinoPath, "hardware\\arduino\\avr\\libraries");
        public static string ArduinoUserLibraryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Arduino\\libraries");

        public static string GetArduinoVariantPath(string variant)
        {
            return Path.Combine(ArduinoPathHelper.ArduinoPath, "hardware\\arduino\\avr\\variants", variant);
        }
        public static string GetArduinoVariantPathShort(string variant)
        {
            return ArduinoPathHelper.GetShortPath(ArduinoPathHelper.GetArduinoVariantPath(variant));
        }

        public static string GetShortPath(string longPath)
        {
            // Nmake - 1980s technology...
            StringBuilder shortPath = new StringBuilder(longPath.Length + 1);

            if (0 == ArduinoPathHelper.GetShortPathName(longPath, shortPath, shortPath.Capacity))
            {
                return longPath;
            }

            return shortPath.ToString();
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern Int32 GetShortPathName(String path, StringBuilder shortPath, Int32 shortPathLength);
    }
}
