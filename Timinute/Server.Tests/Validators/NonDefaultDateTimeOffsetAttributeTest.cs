using System;
using Timinute.Shared.Validators;
using Xunit;

namespace Timinute.Server.Tests.Validators
{
    public class NonDefaultDateTimeOffsetAttributeTest
    {
        private readonly NonDefaultDateTimeOffsetAttribute _attr = new();

        [Fact]
        public void IsValid_DefaultDateTimeOffset_ReturnsFalse()
        {
            Assert.False(_attr.IsValid(default(DateTimeOffset)));
        }

        [Fact]
        public void IsValid_RealDateTimeOffset_ReturnsTrue()
        {
            Assert.True(_attr.IsValid(new DateTimeOffset(2026, 5, 17, 12, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void IsValid_Null_ReturnsTrue()
        {
            // Null handling deferred to [Required]; this attribute should not
            // double-reject null and inflate the error count.
            Assert.True(_attr.IsValid(null));
        }

        [Fact]
        public void IsValid_WrongType_ReturnsTrue()
        {
            // Pass-through for non-DateTimeOffset inputs; let other attributes catch.
            Assert.True(_attr.IsValid("not a date"));
        }
    }
}
