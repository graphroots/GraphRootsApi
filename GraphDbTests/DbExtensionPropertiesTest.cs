using System.Collections.Generic;
using GraphRoots.GraphDb;
using GraphRoots.Grasshopper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraphRoots.GraphDbTests
{
    [TestClass]
    public class DbExtensionPropertiesTest
    {
        [TestMethod]
        public void Flatten_WritesPrefixedKeys()
        {
            var core = DbExtensionProperties.CorePropertyNames(typeof(Node));
            var target = new Dictionary<string, object>();
            var extensions = new Dictionary<string, object?>
            {
                [GhExtensionKeys.ComponentGuid] = "abc",
                [GhExtensionKeys.ScriptSource] = null,
            };

            DbExtensionProperties.ValidateAndFlatten(extensions, core, target);

            Assert.AreEqual("abc", target[GhExtensionKeys.ComponentGuid]);
            Assert.IsFalse(target.ContainsKey(GhExtensionKeys.ScriptSource));
        }

        [TestMethod]
        public void Flatten_RejectsKeyWithoutPrefix()
        {
            var core = DbExtensionProperties.CorePropertyNames(typeof(Node));
            var target = new Dictionary<string, object>();

            Assert.ThrowsExactly<GraphStoreException>(() =>
                DbExtensionProperties.ValidateAndFlatten(
                    new Dictionary<string, object?> { ["Name"] = "x" },
                    core,
                    target));
        }

        [TestMethod]
        public void Flatten_RejectsCollisionWithCoreProperty()
        {
            var core = DbExtensionProperties.CorePropertyNames(typeof(Node));
            var target = new Dictionary<string, object>();

            Assert.ThrowsExactly<GraphStoreException>(() =>
                DbExtensionProperties.ValidateAndFlatten(
                    new Dictionary<string, object?> { ["gh.Name"] = "x" },
                    core,
                    target));
        }

        [TestMethod]
        public void Rehydrate_CollectsPrefixedKeysOnly()
        {
            var core = DbExtensionProperties.CorePropertyNames(typeof(Node));
            var properties = new Dictionary<string, object>
            {
                ["Name"] = "Sphere",
                [GhExtensionKeys.ComponentGuid] = "guid-1",
                ["schemaVersion"] = 2,
            };

            var extensions = DbExtensionProperties.Rehydrate(properties, core);

            Assert.AreEqual(1, extensions.Count);
            Assert.AreEqual("guid-1", extensions[GhExtensionKeys.ComponentGuid]);
        }

        [TestMethod]
        public void Flatten_RejectsHostileKeys()
        {
            var core = DbExtensionProperties.CorePropertyNames(typeof(Node));
            foreach (var key in new[] { "gh.`inject", "GH.foo", "gh.", "n.TypeId} REMOVE n", "gh.foo bar" })
            {
                var ex = Assert.ThrowsExactly<GraphStoreException>(() =>
                    DbExtensionProperties.ValidateAndFlatten(
                        new Dictionary<string, object?> { [key] = "x" },
                        core,
                        new Dictionary<string, object>()));
                Assert.AreEqual(GraphStoreException.Codes.InvalidArgument, ex.Code);
            }
        }

        [TestMethod]
        public void NormalizeValue_AcceptsScalarsAndScalarArrays()
        {
            Assert.AreEqual("hi", DbExtensionProperties.NormalizeValue("hi"));
            Assert.AreEqual(true, DbExtensionProperties.NormalizeValue(true));
            Assert.AreEqual(3d, DbExtensionProperties.NormalizeValue(3));
            CollectionAssert.AreEqual(new object[] { 1d, 2d }, (List<object>)DbExtensionProperties.NormalizeValue(new[] { 1, 2 }));
        }

        [TestMethod]
        public void NormalizeValue_RejectsObjectsAndNestedArrays()
        {
            Assert.ThrowsExactly<GraphStoreException>(() =>
                DbExtensionProperties.NormalizeValue(new Dictionary<string, object?> { ["a"] = 1 }));
            Assert.ThrowsExactly<GraphStoreException>(() =>
                DbExtensionProperties.NormalizeValue(new object[] { new[] { 1 } }));
        }

        [TestMethod]
        public void CypherDynamicProperty_UsesParameterNotInterpolation()
        {
            Assert.AreEqual("n[$extKey]", DbExtensionProperties.CypherDynamicProperty("n", "extKey"));
        }
    }
}
