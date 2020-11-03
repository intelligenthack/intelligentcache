using IntelligentHack.IntelligentCache;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace IntelligentCache.Tests
{
    public partial class CompositeCacheTests
    {
        [Fact]
        public void GetSet_when_l1_hits_then_l2_not_called()
        {
            // Arrange
            var l1 = false;
            var l2 = false;
            var level1 = new InspectableCache((key)=>{l1=true;});
            var level2 = new InspectableCache((key)=>{l2=true;});

            var sut = new CompositeCache(level1, level2);

            // Act
            var result = sut.GetSet("a", ()=>"", TimeSpan.Zero);

            // Assert
            Assert.True(l1);
            Assert.False(l2);
        }

        [Fact]
        public void GetSet_when_l1_misses_then_l2_called()
        {
            // Arrange
            var l1 = false;
            var l2 = false;
            var level1 = new InspectableCache((key)=>{l1=true;}, cacheMiss: true);
            var level2 = new InspectableCache((key)=>{l2=true;});

            var sut = new CompositeCache(level1, level2);

            // Act
            var result = sut.GetSet("a", ()=>"", TimeSpan.Zero);

            // Assert
            Assert.True(l1);
            Assert.True(l2);
        }

        [Fact]
        public async Task InvalidateAsync_when_l2_called_then_l1_not_called_yet()
        {
            // Arrange
            var l1 = false;
            var l2 = false;
            var level1 = new InspectableCache((key)=>{l1=true;});
            var level2 = new InspectableCache((key)=>{l2=true;});

            var sut = new CompositeCache(level1, level2);

            // Act
            await sut.InvalidateAsync("a");

            // Assert
            Assert.True(l1);
            Assert.True(l2);
        }
                [Fact]
        public async Task InvalidateAsync_when_called_l1_and_l2_called()
        {
            // Arrange
            var l1First = false;
            var l2First = false;
            var level1 = new InspectableCache((key)=>{l1First=!l2First;});
            var level2 = new InspectableCache((key)=>{l2First=!l1First;});

            var sut = new CompositeCache(level1, level2);

            // Act
            await sut.InvalidateAsync("a");

            // Assert
            Assert.True(l2First);
            Assert.False(l1First);
        }
    }
}
