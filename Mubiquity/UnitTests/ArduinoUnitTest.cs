using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mubiquity;
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;

namespace UnitTests
{
    [TestClass]
    public class ArduinoUnitTest
    {
        [TestMethod]
        public void TestFindArduino()
        {
            var arduino = Arduino.FindArduino();

        }
    }
}
