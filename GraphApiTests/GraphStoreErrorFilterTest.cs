using GraphRoots.GraphApi;
using GraphRoots.GraphDb;
using HotChocolate;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraphRoots.GraphApiTests;

[TestClass]
public class GraphStoreErrorFilterTest
{
    [TestMethod]
    public void MapsGraphStoreExceptionCode()
    {
        var mapped = new GraphStoreErrorFilter().OnError(
            ErrorBuilder.New()
                .SetMessage("Unexpected Execution Error")
                .SetException(GraphStoreException.NotFound("missing"))
                .Build());
        Assert.AreEqual(GraphStoreException.Codes.NotFound, mapped.Code);
        Assert.AreEqual("missing", mapped.Message);
    }

    [TestMethod]
    public void MapsNonGraphStoreExceptionToStoreError()
    {
        var mapped = new GraphStoreErrorFilter().OnError(
            ErrorBuilder.New()
                .SetMessage("Unexpected Execution Error")
                .SetException(new System.InvalidOperationException("Invalid input 'WHERE'"))
                .Build());
        Assert.AreEqual(GraphStoreException.Codes.StoreError, mapped.Code);
        Assert.AreEqual("Invalid input 'WHERE'", mapped.Message);
    }

    [TestMethod]
    public void MapsWrappedGraphStoreException()
    {
        var mapped = new GraphStoreErrorFilter().OnError(
            ErrorBuilder.New()
                .SetMessage("Unexpected Execution Error")
                .SetException(new System.Exception("wrapper", GraphStoreException.InvalidPattern("bad")))
                .Build());
        Assert.AreEqual(GraphStoreException.Codes.InvalidPattern, mapped.Code);
        Assert.AreEqual("bad", mapped.Message);
    }
}
