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
            //ArduinoProjectHelper helper = new ArduinoProjectHelper("MakefileTest");
            //helper.regenerateMakefile();

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
            ArduinoProjectHelper helper = new ArduinoProjectHelper("MakefileTest", null);
            helper.ManifestPrefix = "VSIXUnitTest.Resources";

            helper.regenerateMakefile();

            StreamReader reader = new StreamReader("MakefileTest");
            Assert.IsTrue(reader.BaseStream.Length > 0);
        }

    }
}
