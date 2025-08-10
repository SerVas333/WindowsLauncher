using WindowsLauncher.Core.Enums;
using Xunit;

namespace WindowsLauncher.Tests.Enums
{
    public class ApplicationTypeTests
    {
        [Fact]
        public void ApplicationType_HasExpectedValues()
        {
            // Assert
            Assert.Equal(1, (int)ApplicationType.Desktop);
            Assert.Equal(2, (int)ApplicationType.Web);
            Assert.Equal(3, (int)ApplicationType.Folder);
            Assert.Equal(4, (int)ApplicationType.ChromeApp);
            Assert.Equal(5, (int)ApplicationType.Android);
        }

        [Theory]
        [InlineData(ApplicationType.Desktop, "Desktop")]
        [InlineData(ApplicationType.Web, "Web")]
        [InlineData(ApplicationType.Folder, "Folder")]
        [InlineData(ApplicationType.ChromeApp, "ChromeApp")]
        [InlineData(ApplicationType.Android, "Android")]
        public void ApplicationType_ToString_ReturnsCorrectString(ApplicationType applicationType, string expected)
        {
            // Act
            var result = applicationType.ToString();

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Desktop", ApplicationType.Desktop)]
        [InlineData("Web", ApplicationType.Web)]
        [InlineData("Folder", ApplicationType.Folder)]
        [InlineData("ChromeApp", ApplicationType.ChromeApp)]
        [InlineData("Android", ApplicationType.Android)]
        public void ApplicationType_Parse_ReturnsCorrectEnum(string typeString, ApplicationType expected)
        {
            // Act
            var result = Enum.Parse<ApplicationType>(typeString);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ApplicationType_Parse_CaseInsensitive_ReturnsCorrectEnum()
        {
            // Act
            var result = Enum.Parse<ApplicationType>("android", true);

            // Assert
            Assert.Equal(ApplicationType.Android, result);
        }

        [Fact]
        public void ApplicationType_TryParse_WithValidString_ReturnsTrue()
        {
            // Act
            var success = Enum.TryParse<ApplicationType>("Android", out var result);

            // Assert
            Assert.True(success);
            Assert.Equal(ApplicationType.Android, result);
        }

        [Fact]
        public void ApplicationType_TryParse_WithInvalidString_ReturnsFalse()
        {
            // Act
            var success = Enum.TryParse<ApplicationType>("InvalidType", out var result);

            // Assert
            Assert.False(success);
            Assert.Equal(default(ApplicationType), result);
        }

        [Fact]
        public void ApplicationType_AllValuesAreDefined()
        {
            // Arrange
            var definedValues = Enum.GetValues<ApplicationType>();

            // Assert
            Assert.Contains(ApplicationType.Desktop, definedValues);
            Assert.Contains(ApplicationType.Web, definedValues);
            Assert.Contains(ApplicationType.Folder, definedValues);
            Assert.Contains(ApplicationType.ChromeApp, definedValues);
            Assert.Contains(ApplicationType.Android, definedValues);
            Assert.Equal(5, definedValues.Length);
        }

        [Fact]
        public void ApplicationType_GetNames_ReturnsCorrectNames()
        {
            // Act
            var names = Enum.GetNames<ApplicationType>();

            // Assert
            Assert.Contains("Desktop", names);
            Assert.Contains("Web", names);
            Assert.Contains("Folder", names);
            Assert.Contains("ChromeApp", names);
            Assert.Contains("Android", names);
            Assert.Equal(5, names.Length);
        }

        [Fact]
        public void ApplicationType_Android_HasCorrectValue()
        {
            // Assert
            Assert.Equal(5, (int)ApplicationType.Android);
            Assert.Equal("Android", ApplicationType.Android.ToString());
        }
    }
}