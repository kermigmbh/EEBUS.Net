using EEBUS.StateMachines;

namespace TestProject1
{
    public class EffectiveLimitTests
    {
        #region Factory Method Tests

        [Fact]
        public void Unlimited_ShouldCreateUnlimitedLimit()
        {
            // Act
            var limit = EffectiveLimit.Unlimited(LimitState.UnlimitedControlled);

            // Assert
            Assert.False(limit.IsLimited);
            Assert.Equal(long.MaxValue, limit.Value);
            Assert.Equal(LimitState.UnlimitedControlled, limit.State);
            Assert.Equal("none", limit.Source);
            Assert.Null(limit.ExpiresAt);
        }

        [Fact]
        public void FromFailsafe_ShouldCreateFailsafeLimit()
        {
            // Act
            var limit = EffectiveLimit.FromFailsafe(1500, LimitState.Failsafe);

            // Assert
            Assert.True(limit.IsLimited);
            Assert.Equal(1500, limit.Value);
            Assert.Equal(LimitState.Failsafe, limit.State);
            Assert.Equal("failsafe", limit.Source);
            Assert.Null(limit.ExpiresAt);
        }

        [Fact]
        public void FromActive_ShouldCreateActiveLimit()
        {
            // Arrange
            var expiresAt = DateTimeOffset.UtcNow.AddHours(1);

            // Act
            var limit = EffectiveLimit.FromActive(5000, LimitState.Limited, expiresAt);

            // Assert
            Assert.True(limit.IsLimited);
            Assert.Equal(5000, limit.Value);
            Assert.Equal(LimitState.Limited, limit.State);
            Assert.Equal("active", limit.Source);
            Assert.Equal(expiresAt, limit.ExpiresAt);
        }

        [Fact]
        public void FromActive_WithoutExpiry_ShouldHaveNullExpiresAt()
        {
            // Act
            var limit = EffectiveLimit.FromActive(5000, LimitState.Limited);

            // Assert
            Assert.Null(limit.ExpiresAt);
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_ShouldSetAllProperties()
        {
            // Arrange
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(30);

            // Act
            var limit = new EffectiveLimit(true, 2500, LimitState.Limited, "active", expiresAt);

            // Assert
            Assert.True(limit.IsLimited);
            Assert.Equal(2500, limit.Value);
            Assert.Equal(LimitState.Limited, limit.State);
            Assert.Equal("active", limit.Source);
            Assert.Equal(expiresAt, limit.ExpiresAt);
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_WhenLimited_ShouldIncludeValue()
        {
            // Arrange
            var limit = EffectiveLimit.FromActive(5000, LimitState.Limited);

            // Act
            var str = limit.ToString();

            // Assert
            Assert.Contains("5000W", str);
            Assert.Contains("Limited", str);
            Assert.Contains("active", str);
        }

        [Fact]
        public void ToString_WhenUnlimited_ShouldIndicateUnlimited()
        {
            // Arrange
            var limit = EffectiveLimit.Unlimited(LimitState.UnlimitedControlled);

            // Act
            var str = limit.ToString();

            // Assert
            Assert.Contains("Unlimited", str);
        }

        [Fact]
        public void ToString_WithExpiry_ShouldIncludeExpiresInfo()
        {
            // Arrange
            var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
            var limit = EffectiveLimit.FromActive(5000, LimitState.Limited, expiresAt);

            // Act
            var str = limit.ToString();

            // Assert
            Assert.Contains("expires", str);
        }

        #endregion

        #region State-Specific Tests

        [Theory]
        [InlineData(LimitState.Init)]
        [InlineData(LimitState.Failsafe)]
        public void FailsafeStates_ShouldBeIdentifiedBySource(LimitState state)
        {
            // Act
            var limit = EffectiveLimit.FromFailsafe(1000, state);

            // Assert
            Assert.Equal("failsafe", limit.Source);
            Assert.True(limit.IsLimited);
        }

        [Theory]
        [InlineData(LimitState.UnlimitedControlled)]
        [InlineData(LimitState.UnlimitedAutonomous)]
        public void UnlimitedStates_ShouldHaveNoSource(LimitState state)
        {
            // Act
            var limit = EffectiveLimit.Unlimited(state);

            // Assert
            Assert.Equal("none", limit.Source);
            Assert.False(limit.IsLimited);
        }

        #endregion

        #region Edge Case Tests

        [Fact]
        public void ZeroLimit_ShouldStillBeLimited()
        {
            // Act
            var limit = EffectiveLimit.FromActive(0, LimitState.Limited);

            // Assert
            Assert.True(limit.IsLimited);
            Assert.Equal(0, limit.Value);
        }

        [Fact]
        public void LargeLimit_ShouldBeHandledCorrectly()
        {
            // Act
            var limit = EffectiveLimit.FromActive(long.MaxValue - 1, LimitState.Limited);

            // Assert
            Assert.True(limit.IsLimited);
            Assert.Equal(long.MaxValue - 1, limit.Value);
        }

        #endregion
    }
}
