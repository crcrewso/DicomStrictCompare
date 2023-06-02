using Microsoft.VisualStudio.TestTools.UnitTesting;
using DSClibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSClibrary.Tests;
[TestClass()]
public class SingleComparisonTests
{

    [TestMethod()]
    public void CanInitialize()
    {
        var dta = new Dta(false, 0, 0);
        var ret = new SingleComparison(dta, 0, 0, 0);
        Assert.IsNotNull(ret);
    }

    [TestMethod()]
    public void EqualsTrueTest()
    {
        var dta = new Dta(false, 0, 0);
        var ret = new SingleComparison(dta, 0, 0, 0);

        Assert.IsTrue(ret.Equals(ret));
    }

    [TestMethod()]
    public void EqualsFalseTest1()
    {
        var dta = new Dta(false, 0, 0);
        var ret = new SingleComparison(dta, 0, 0, 0);
        var ret2 = new SingleComparison(dta, 1, 0, 0);
        Assert.IsFalse(ret2.Equals(ret));
    }

    [TestMethod()]
    public void CreateWithNullExceptionTest()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new SingleComparison(null, 0, 0, 0));
    }

    [TestMethod()]
    public void CompareWithNullException()
    {
        var dta = new Dta(false, 0, 0);
        var ret = new SingleComparison(dta, 0, 0, 0);
        Assert.ThrowsException<ArgumentNullException>(() => ret.Equals(null));
    }

    [TestMethod()]
    public void InitializationTestTotalCount()
    {
        var dta = new Dta(false, 0, 0);
        var ret = new SingleComparison(dta, 100, 10, 1);
        Assert.AreEqual(100, ret.TotalCount);

    }

    [TestMethod()]
    public void InitializationTestTotalCompared()
    {
        var dta = new Dta(false, 0, 0);
        var ret = new SingleComparison(dta, 100, 10, 1);
        Assert.AreEqual(10, ret.TotalCompared);

    }

    [TestMethod()]
    public void InitializationTestTotalFailed()
    {
        var dta = new Dta(false, 0, 0);
        var ret = new SingleComparison(dta, 100, 10, 1);
        Assert.AreEqual(1, ret.TotalFailed);
    }

    [TestMethod()]
    public void InvalidComparedException()
    {
        var dta = new Dta(false, 0, 0);
        Assert.ThrowsException<ArgumentException>(() => new SingleComparison(dta, 100, 101, 1));
    }

    [TestMethod()]
    public void InvalidFailedException()
    {
        var dta = new Dta(false, 0, 0);
        Assert.ThrowsException<ArgumentException>(() => new SingleComparison(dta, 100, 10, 1000));
    }

}
