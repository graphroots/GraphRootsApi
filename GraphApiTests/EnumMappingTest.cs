using GraphRoots.GraphApi;
using GraphRoots.GraphDb;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraphRoots.GraphApiTests;

[TestClass]
public class EnumMappingTest
{
    [TestMethod]
    public void ToStoreFilter_MapsUnknownToSentinel()
    {
        Assert.AreEqual(GraphOrigins.UnknownFilter, EnumMapping.ToStoreFilter(GqlOrigin.UNKNOWN));
        Assert.AreEqual(GraphOrigins.Grasshopper, EnumMapping.ToStoreFilter(GqlOrigin.GRASSHOPPER));
    }

    [TestMethod]
    public void ToStore_RejectsUnknown()
    {
        var ex = Assert.ThrowsExactly<GraphStoreException>(() => EnumMapping.ToStore(GqlOrigin.UNKNOWN));
        Assert.AreEqual(GraphStoreException.Codes.InvalidArgument, ex.Code);
    }
}
