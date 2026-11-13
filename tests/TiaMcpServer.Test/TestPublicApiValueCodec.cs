using System;
using System.Text.Json;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    [TestClass]
    public class TestPublicApiValueCodec
    {
        [Flags]
        private enum Modes { One = 1, Two = 2 }
        private delegate int RefCallback(ref int input, out string output);

        private static object Resolve(string reference) => throw new ArgumentException("Unknown reference.");

        [TestMethod]
        public void ConvertsFlagNamesAndRectangularArrays()
        {
            using (var flags = JsonDocument.Parse("\"One, Two\""))
                Assert.AreEqual(Modes.One | Modes.Two, PublicApiValueCodec.Convert(flags.RootElement, typeof(Modes), Resolve));
            using (var json = JsonDocument.Parse("[[1,2],[3,4]]"))
            {
                var result = (int[,])PublicApiValueCodec.Convert(json.RootElement, typeof(int[,]), Resolve)!;
                Assert.AreEqual(2, result.GetLength(0));
                Assert.AreEqual(4, result[1, 1]);
            }
            using (var invalid = JsonDocument.Parse("[[1],[2,3]]"))
                Assert.ThrowsException<ArgumentException>(() => PublicApiValueCodec.Convert(invalid.RootElement, typeof(int[,]), Resolve));
        }

        [TestMethod]
        public void CallbackAppliesRefAndOutValues()
        {
            using (var json = JsonDocument.Parse("{\"$callback\":{\"return\":5,\"out\":{\"input\":12,\"output\":\"done\"}}}"))
            {
                var callback = (RefCallback)PublicApiValueCodec.Convert(json.RootElement, typeof(RefCallback), Resolve)!;
                var input = 1;
                Assert.AreEqual(5, callback(ref input, out var output));
                Assert.AreEqual(12, input);
                Assert.AreEqual("done", output);
            }
        }

        [TestMethod]
        public void CallbackCasesRejectEmptyAndNonSiemensTypes()
        {
            using (var empty = JsonDocument.Parse("{\"$callback\":{\"cases\":[]}}"))
                Assert.ThrowsException<ArgumentException>(() => PublicApiValueCodec.Convert(empty.RootElement, typeof(Action<object>), Resolve, name => typeof(int)));
            using (var nonSiemens = JsonDocument.Parse("{\"$callback\":{\"cases\":[{\"type\":\"System.Int32\",\"policy\":{}}]}}"))
                Assert.ThrowsException<ArgumentException>(() => PublicApiValueCodec.Convert(nonSiemens.RootElement, typeof(Action<object>), Resolve, name => typeof(int)));
        }

        [TestMethod]
        public void CultureAndVersionUseStableStringValues()
        {
            using (var culture = JsonDocument.Parse("\"en-GB\""))
            {
                var value = PublicApiValueCodec.Convert(culture.RootElement, typeof(System.Globalization.CultureInfo), Resolve);
                Assert.AreEqual("en-GB", PublicApiValueCodec.Project(value, reference => throw new ArgumentException()));
            }
            using (var version = JsonDocument.Parse("\"1.2.3\""))
            {
                var value = PublicApiValueCodec.Convert(version.RootElement, typeof(Version), Resolve);
                Assert.AreEqual("1.2.3", PublicApiValueCodec.Project(value, reference => throw new ArgumentException()));
            }
        }

        [TestMethod]
        public void ReferenceAcceptsMatchingProjectedTypeAndRejectsOtherFields()
        {
            var target = new object();
            using (var valid = JsonDocument.Parse("{\"$ref\":\"id\",\"type\":\"System.Object\"}"))
                Assert.AreSame(target, PublicApiValueCodec.Convert(valid.RootElement, typeof(object), reference => target));
            using (var wrong = JsonDocument.Parse("{\"$ref\":\"id\",\"type\":\"System.String\"}"))
                Assert.ThrowsException<ArgumentException>(() => PublicApiValueCodec.Convert(wrong.RootElement, typeof(object), reference => target));
            using (var extra = JsonDocument.Parse("{\"$ref\":\"id\",\"other\":true}"))
                Assert.ThrowsException<ArgumentException>(() => PublicApiValueCodec.Convert(extra.RootElement, typeof(object), reference => target));
        }

        [TestMethod]
        public void ExpectedValuesCompareCollectionsStructurally()
        {
            Assert.IsTrue(PublicApiValueCodec.ValueEquals(new[] { 1, 2 }, new[] { 1, 2 }));
            Assert.IsFalse(PublicApiValueCodec.ValueEquals(new[] { 1, 2 }, new[] { 1, 3 }));
            Assert.IsTrue(PublicApiValueCodec.ValueEquals(new System.Collections.Generic.List<int> { 1, 2 }, new System.Collections.Generic.List<int> { 1, 2 }));
            Assert.IsFalse(PublicApiValueCodec.ValueEquals(new System.Collections.Generic.List<int> { 1 }, new System.Collections.Generic.List<int> { 2 }));
            var left = new System.Collections.Generic.Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
            var right = new System.Collections.Generic.Dictionary<string, int> { ["b"] = 2, ["a"] = 1 };
            Assert.IsTrue(PublicApiValueCodec.ValueEquals(left, right));
            right["a"] = 3;
            Assert.IsFalse(PublicApiValueCodec.ValueEquals(left, right));
        }

        [TestMethod]
        public void TypedObjectUsesOnlyAllowedScalarOrSiemensType()
        {
            using (var json = JsonDocument.Parse("{\"$type\":\"System.Int32\",\"value\":7}"))
                Assert.AreEqual(7, PublicApiValueCodec.Convert(json.RootElement, typeof(object), Resolve, name => name == "System.Int32" ? typeof(int) : throw new ArgumentException()));
            using (var json = JsonDocument.Parse("{\"$type\":\"System.Uri\",\"value\":\"https://example.com\"}"))
                Assert.ThrowsException<ArgumentException>(() => PublicApiValueCodec.Convert(json.RootElement, typeof(object), Resolve, name => typeof(Uri)));
        }

        [TestMethod]
        public void ComparisonBudgetAcceptsMaximumFlatCollections()
        {
            var left = new System.Collections.Generic.Dictionary<int, int>();
            var right = new System.Collections.Generic.Dictionary<int, int>();
            for (var i = 0; i < 1024; i++) left.Add(i, i);
            for (var i = 1023; i >= 0; i--) right.Add(i, i);
            Assert.IsTrue(PublicApiValueCodec.ValueEquals(left, right));
            Assert.IsTrue(PublicApiValueCodec.ValueEquals(new int[1024], new int[1024]));
        }
    }
}
