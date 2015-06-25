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
