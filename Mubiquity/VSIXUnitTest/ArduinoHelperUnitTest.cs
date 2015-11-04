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
using System.IO;
using System.Text;
using Mubiquity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VSIXUnitTest
{
    [TestClass]
    public class ArduinoHelperUnitTest
    {
        [TestMethod]
        [TestCategory("Automated")]
        public void TestTemplateExtraction()
        {
            var assembly = typeof(ArduinoProjectHelper).Assembly;
            var resources = assembly.GetManifestResourceNames();
            var streamOfTemplate = assembly.GetManifestResourceStream("VSIXUnitTest.Resources.Arduino_Makefile_Template.nmake");
            Assert.IsNotNull(streamOfTemplate);
            Assert.IsTrue(streamOfTemplate.Length > 0);
        }

        [TestMethod]
        [TestCategory("Automated")]
        public void TestTemplateProcessing()
        {
            ArduinoProjectHelper helper = new ArduinoProjectHelper("MakefileTest", "");
            helper.ManifestPrefix = "VSIXUnitTest.Resources";

            helper.regenerateMakefile();

            StreamReader reader = new StreamReader("MakefileTest");
            Assert.IsTrue(reader.BaseStream.Length > 0);
        }

        private StreamReader readerFromString(string s)
        {
            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(s));
            return new StreamReader(ms);
        }

        private List<string> includesFromString(string includeString)
        {
            ArduinoProjectHelper helper = new ArduinoProjectHelper("MakefileTest", "");

            var sr = readerFromString(includeString);
            return helper.extractLibrariesFromStream(sr);
        }

        struct IncludeTestCases
        {
            public string include;
            public int expected;
        };


        [TestMethod]
        [TestCategory("Automated")]
        public void TestLibraryExtraction()
        {
            IncludeTestCases[] cases = new IncludeTestCases[]
            {
                new IncludeTestCases() { include = "#include<hello.h>", expected = 1 },
                new IncludeTestCases() { include = "#include<hello.h>\n#include<hello1.h>", expected = 2 },
                new IncludeTestCases() { include = "\t#include<hello.h>\n\t#include<hello1.h>", expected = 2 },
                new IncludeTestCases() { include = "\t#include<\t \thello.h>\n\t#include<hello1.h \t>", expected = 2 },
                new IncludeTestCases() { include = "\t#include<\t.h>\n\t#include<.h \t>", expected = 2 },
                new IncludeTestCases() { include = "\t#include<\t.h>\n\t#include<.h \t>\n\n\n\n\n#include<.h \t>", expected = 3 },
            };

            foreach (var c in cases)
            {
                var i = includesFromString(c.include);
                Assert.IsTrue(i.Count == c.expected);
            }
        }

        [TestMethod]
        [TestCategory("Automated")]
        public void TestLibraryPropertyParser()
        {
            var multiarch = readerFromString("name=Test\nversion=1.2.2\narchitectures=sam,avr");
            var allarch = readerFromString("name=Test\nversion=1.2.2\narchitectures=*");

            var multi = new LibraryProperties();
            multi.load(multiarch);

            var all = new LibraryProperties();
            all.load(allarch);

            Assert.IsTrue(multi.Architectures.Count == 2);
            Assert.IsTrue(all.Architectures.Count == 1);
        }

        [TestMethod]
        [TestCategory("Automated")]
        public void TestLibraryLoadFromINO()
        {
            var sr = readerFromString("#include<Servo.h>");
            ArduinoProjectHelper helper = new ArduinoProjectHelper("MakefileTest", sr);
            var libraries = helper.extractLibrariesFromIno(sr);

            Assert.IsTrue(libraries.Count == 1);
            Assert.IsTrue(string.CompareOrdinal(libraries[0].properties.Name, "Servo") == 0);

            libraries[0].loadSources("avr");

            Assert.IsTrue(libraries[0].CPPSourceFiles.Count > 0);
        }
    }
}
