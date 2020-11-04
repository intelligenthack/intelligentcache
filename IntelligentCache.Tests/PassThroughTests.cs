using IntelligentHack.IntelligentCache;
using System;
using Xunit;

namespace IntelligentCache.Tests
{
    public class PassThroughTests
    {
        [Fact]
        public void GetSet_lambda_is_always_called()
        {
            // Arrange
            var sut = new PassThroughCache();
            var count = 0;

            // Act
            var result = sut.GetSet("testKey", () => { count++; return "41"; }, TimeSpan.FromSeconds(10));

            result = sut.GetSet("testKey", () => { count++; return "42"; }, TimeSpan.FromSeconds(10));

            // Assert
            Assert.Equal("42", result);
            Assert.Equal(2, count);
        }
    }
}
