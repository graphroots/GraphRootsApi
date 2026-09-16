using System;
using GraphRoots.GraphDb;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo4j.Driver;

namespace GraphRoots.GraphDbTests
{
    [TestClass]
    public class DbValueConversionTest
    {
        [TestMethod]
        public void FromZonedDateTime_ReturnsUtc()
        {
            var instant = new DateTime(2024, 6, 15, 12, 30, 45, 123, DateTimeKind.Utc);
            var zoned = new ZonedDateTime(new DateTimeOffset(instant, TimeSpan.Zero));

            var converted = DbValueConversion.FromZonedDateTime(zoned);
            var nullable = DbValueConversion.FromZonedDateTimeNullable(zoned);

            Assert.AreEqual(instant, converted);
            Assert.AreEqual(DateTimeKind.Utc, converted.Kind);
            Assert.AreEqual(instant, nullable);
        }

        [TestMethod]
        public void FromZonedDateTime_NormalizesOffsetToUtc()
        {
            var utc = new DateTime(2024, 6, 15, 12, 30, 45, DateTimeKind.Utc);
            var offset = new DateTimeOffset(2024, 6, 15, 14, 30, 45, TimeSpan.FromHours(2));
            var zoned = new ZonedDateTime(offset);

            var converted = DbValueConversion.FromZonedDateTime(zoned);

            Assert.AreEqual(utc, converted);
            Assert.AreEqual(DateTimeKind.Utc, converted.Kind);
        }

        [TestMethod]
        public void FromLocalDateTime_PreservesClockValues()
        {
            var local = new DateTime(2024, 6, 15, 12, 30, 45, 123, DateTimeKind.Unspecified);
            var neo = new LocalDateTime(local);

            var converted = DbValueConversion.FromLocalDateTime(neo);
            var nullable = DbValueConversion.FromLocalDateTimeNullable(neo);

            Assert.AreEqual(local, converted);
            Assert.AreEqual(local, nullable);
        }
    }
}
