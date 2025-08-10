using WindowsLauncher.Core.Enums;
using Xunit;

namespace WindowsLauncher.Tests.Enums
{
    public class DataClearingStrategyTests
    {
        [Fact]
        public void DataClearingStrategy_HasExpectedValues()
        {
            // Assert
            Assert.Equal(0, (int)DataClearingStrategy.Immediate);
            Assert.Equal(1, (int)DataClearingStrategy.OnUserSwitch);
            Assert.Equal(2, (int)DataClearingStrategy.OnAppExit);
        }

        [Theory]
        [InlineData(DataClearingStrategy.Immediate, "Immediate")]
        [InlineData(DataClearingStrategy.OnUserSwitch, "OnUserSwitch")]
        [InlineData(DataClearingStrategy.OnAppExit, "OnAppExit")]
        public void DataClearingStrategy_ToString_ReturnsCorrectString(DataClearingStrategy strategy, string expected)
        {
            // Act
            var result = strategy.ToString();

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Immediate", DataClearingStrategy.Immediate)]
        [InlineData("OnUserSwitch", DataClearingStrategy.OnUserSwitch)]
        [InlineData("OnAppExit", DataClearingStrategy.OnAppExit)]
        public void DataClearingStrategy_Parse_ReturnsCorrectEnum(string strategyString, DataClearingStrategy expected)
        {
            // Act
            var result = Enum.Parse<DataClearingStrategy>(strategyString);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void DataClearingStrategy_Parse_CaseInsensitive_ReturnsCorrectEnum()
        {
            // Act
            var result = Enum.Parse<DataClearingStrategy>("onuserswitch", true);

            // Assert
            Assert.Equal(DataClearingStrategy.OnUserSwitch, result);
        }

        [Fact]
        public void DataClearingStrategy_TryParse_WithValidString_ReturnsTrue()
        {
            // Act
            var success = Enum.TryParse<DataClearingStrategy>("OnUserSwitch", out var result);

            // Assert
            Assert.True(success);
            Assert.Equal(DataClearingStrategy.OnUserSwitch, result);
        }

        [Fact]
        public void DataClearingStrategy_TryParse_WithInvalidString_ReturnsFalse()
        {
            // Act
            var success = Enum.TryParse<DataClearingStrategy>("InvalidStrategy", out var result);

            // Assert
            Assert.False(success);
            Assert.Equal(default(DataClearingStrategy), result);
        }

        [Fact]
        public void DataClearingStrategy_AllValuesAreDefined()
        {
            // Arrange
            var definedValues = Enum.GetValues<DataClearingStrategy>();

            // Assert
            Assert.Contains(DataClearingStrategy.Immediate, definedValues);
            Assert.Contains(DataClearingStrategy.OnUserSwitch, definedValues);
            Assert.Contains(DataClearingStrategy.OnAppExit, definedValues);
            Assert.Equal(3, definedValues.Length);
        }

        [Fact]
        public void DataClearingStrategy_GetNames_ReturnsCorrectNames()
        {
            // Act
            var names = Enum.GetNames<DataClearingStrategy>();

            // Assert
            Assert.Contains("Immediate", names);
            Assert.Contains("OnUserSwitch", names);
            Assert.Contains("OnAppExit", names);
            Assert.Equal(3, names.Length);
        }
    }
}