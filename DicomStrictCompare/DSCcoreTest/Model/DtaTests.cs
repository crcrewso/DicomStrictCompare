﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using DCSCore.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DCSCore.Model.Tests
{
    [TestClass()]
    public class DtaTests
    {
        [TestMethod()]
        public void ExceptionInitializationTest()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new Dta(null));
        }

        [TestMethod()]
        public void DtaTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void DtaTest1()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void ShortToStringTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void ToStringTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void TitlesTest()
        {
            Assert.Fail();
        }
    }
}