using Microsoft.Extensions.Logging;
using Moq;
using WindowsLauncher.Core.Interfaces;
using WindowsLauncher.Core.Models;
using WindowsLauncher.Data.Migrations;
using Xunit;

namespace WindowsLauncher.Tests.Data.Migrations
{
    public class AddAndroidSupportTests
    {
        private readonly Mock<IDatabaseMigrationContext> _mockContext;
        private readonly AddAndroidSupport _migration;

        public AddAndroidSupportTests()
        {
            _mockContext = new Mock<IDatabaseMigrationContext>();
            _migration = new AddAndroidSupport();
        }

        [Fact]
        public void Migration_HasCorrectMetadata()
        {
            // Assert
            Assert.Equal("AddAndroidSupport", _migration.Name);
            Assert.Equal("1.2.0.001", _migration.Version);
            Assert.Contains("Android APK", _migration.Description);
        }

        [Fact]
        public async Task UpAsync_SQLite_AddsAndroidApplicationType()
        {
            // Arrange
            _mockContext.Setup(x => x.ExecuteScalarAsync<long>(It.IsAny<string>()))
                       .ReturnsAsync(0); // Type doesn't exist yet

            _mockContext.Setup(x => x.ColumnExistsAsync("APPLICATIONS", It.IsAny<string>()))
                       .ReturnsAsync(false); // Columns don't exist yet

            // Act
            await _migration.UpAsync(_mockContext.Object, DatabaseType.SQLite);

            // Assert
            // Verify Android application type is added
            _mockContext.Verify(x => x.ExecuteSqlAsync(
                It.Is<string>(sql => sql.Contains("INSERT INTO APPLICATION_TYPES") 
                                  && sql.Contains("'Android'") 
                                  && sql.Contains("🤖"))), 
                Times.Once);
        }

        [Fact]
        public async Task UpAsync_Firebird_AddsAndroidApplicationType()
        {
            // Arrange
            _mockContext.Setup(x => x.ExecuteScalarAsync<long>(It.IsAny<string>()))
                       .ReturnsAsync(0); // Type doesn't exist yet

            _mockContext.Setup(x => x.ColumnExistsAsync("APPLICATIONS", It.IsAny<string>()))
                       .ReturnsAsync(false); // Columns don't exist yet

            // Act
            await _migration.UpAsync(_mockContext.Object, DatabaseType.Firebird);

            // Assert
            // Verify Android application type is added
            _mockContext.Verify(x => x.ExecuteSqlAsync(
                It.Is<string>(sql => sql.Contains("INSERT INTO APPLICATION_TYPES") 
                                  && sql.Contains("'Android'") 
                                  && sql.Contains("🤖"))), 
                Times.Once);
        }

        [Fact]
        public async Task UpAsync_SQLite_AddsAllAndroidColumns()
        {
            // Arrange
            _mockContext.Setup(x => x.ExecuteScalarAsync<long>(It.IsAny<string>()))
                       .ReturnsAsync(0); // Type doesn't exist

            _mockContext.Setup(x => x.ColumnExistsAsync("APPLICATIONS", It.IsAny<string>()))
                       .ReturnsAsync(false); // No columns exist

            // Act
            await _migration.UpAsync(_mockContext.Object, DatabaseType.SQLite);

            // Assert
            // Verify all Android columns are added
            var expectedColumns = new[]
            {
                "APK_PACKAGE_NAME",
                "APK_VERSION_CODE",
                "APK_VERSION_NAME", 
                "APK_MIN_SDK",
                "APK_TARGET_SDK",
                "APK_FILE_PATH",
                "APK_FILE_HASH",
                "APK_INSTALL_STATUS"
            };

            foreach (var column in expectedColumns)
            {
                _mockContext.Verify(x => x.ExecuteSqlAsync(
                    It.Is<string>(sql => sql.Contains($"ALTER TABLE APPLICATIONS ADD COLUMN {column}"))),
                    Times.Once, 
                    $"Column {column} should be added");
            }
        }

        [Fact]
        public async Task UpAsync_Firebird_AddsAllAndroidColumns()
        {
            // Arrange
            _mockContext.Setup(x => x.ExecuteScalarAsync<long>(It.IsAny<string>()))
                       .ReturnsAsync(0);

            _mockContext.Setup(x => x.ColumnExistsAsync("APPLICATIONS", It.IsAny<string>()))
                       .ReturnsAsync(false);

            // Act
            await _migration.UpAsync(_mockContext.Object, DatabaseType.Firebird);

            // Assert
            // Verify Firebird-specific syntax for column additions
            _mockContext.Verify(x => x.ExecuteSqlAsync(
                It.Is<string>(sql => sql.Contains("ALTER TABLE APPLICATIONS ADD APK_PACKAGE_NAME VARCHAR(255)"))),
                Times.Once);

            _mockContext.Verify(x => x.ExecuteSqlAsync(
                It.Is<string>(sql => sql.Contains("ALTER TABLE APPLICATIONS ADD APK_VERSION_CODE INTEGER"))),
                Times.Once);
        }

        [Fact]
        public async Task UpAsync_CreatesAndroidIndexes()
        {
            // Arrange
            _mockContext.Setup(x => x.ExecuteScalarAsync<long>(It.IsAny<string>()))
                       .ReturnsAsync(0);

            _mockContext.Setup(x => x.ColumnExistsAsync("APPLICATIONS", It.IsAny<string>()))
                       .ReturnsAsync(false);

            // Act
            await _migration.UpAsync(_mockContext.Object, DatabaseType.SQLite);

            // Assert
            // Verify key Android indexes are created
            _mockContext.Verify(x => x.ExecuteSqlAsync(
                It.Is<string>(sql => sql.Contains("CREATE INDEX IDX_APPLICATIONS_APK_PACKAGE"))),
                Times.Once);

            _mockContext.Verify(x => x.ExecuteSqlAsync(
                It.Is<string>(sql => sql.Contains("CREATE INDEX IDX_APPLICATIONS_TYPE_ANDROID"))),
                Times.Once);

            _mockContext.Verify(x => x.ExecuteSqlAsync(
                It.Is<string>(sql => sql.Contains("CREATE INDEX IDX_APPLICATIONS_APK_STATUS"))),
                Times.Once);
        }

        [Fact]
        public async Task UpAsync_AddsAndroidCategory()
        {
            // Arrange
            _mockContext.Setup(x => x.ExecuteScalarAsync<long>(It.IsAny<string>()))
                       .ReturnsAsync(0); // Category doesn't exist

            _mockContext.Setup(x => x.ColumnExistsAsync("APPLICATIONS", It.IsAny<string>()))
                       .ReturnsAsync(false);

            // Act
            await _migration.UpAsync(_mockContext.Object, DatabaseType.SQLite);

            // Assert
            // Verify Android category is added
            _mockContext.Verify(x => x.ExecuteSqlAsync(
                It.Is<string>(sql => sql.Contains("INSERT INTO CATEGORIES") 
                                  && sql.Contains("'Android'") 
                                  && sql.Contains("🤖"))), 
                Times.Once);
        }

        [Fact]
        public async Task UpAsync_SkipsExistingAndroidType()
        {
            // Arrange
            _mockContext.Setup(x => x.ExecuteScalarAsync<long>(
                It.Is<string>(sql => sql.Contains("APPLICATION_TYPES") && sql.Contains("Android"))))
                       .ReturnsAsync(1); // Type already exists

            _mockContext.Setup(x => x.ColumnExistsAsync("APPLICATIONS", It.IsAny<string>()))
                       .ReturnsAsync(true); // Columns already exist

            // Act
            await _migration.UpAsync(_mockContext.Object, DatabaseType.SQLite);

            // Assert
            // Verify INSERT for application type is NOT called when type already exists
            _mockContext.Verify(x => x.ExecuteSqlAsync(
                It.Is<string>(sql => sql.Contains("INSERT INTO APPLICATION_TYPES") && sql.Contains("Android"))),
                Times.Never);
        }

        [Fact]
        public async Task UpAsync_SkipsExistingColumns()
        {
            // Arrange
            _mockContext.Setup(x => x.ExecuteScalarAsync<long>(It.IsAny<string>()))
                       .ReturnsAsync(0);

            _mockContext.Setup(x => x.ColumnExistsAsync("APPLICATIONS", "APK_PACKAGE_NAME"))
                       .ReturnsAsync(true); // Column already exists

            _mockContext.Setup(x => x.ColumnExistsAsync("APPLICATIONS", It.Is<string>(col => col != "APK_PACKAGE_NAME")))
                       .ReturnsAsync(false); // Other columns don't exist

            // Act
            await _migration.UpAsync(_mockContext.Object, DatabaseType.SQLite);

            // Assert
            // Verify APK_PACKAGE_NAME column is NOT added when it already exists
            _mockContext.Verify(x => x.ExecuteSqlAsync(
                It.Is<string>(sql => sql.Contains("ALTER TABLE APPLICATIONS ADD COLUMN APK_PACKAGE_NAME"))),
                Times.Never);

            // Verify other columns are still added
            _mockContext.Verify(x => x.ExecuteSqlAsync(
                It.Is<string>(sql => sql.Contains("ALTER TABLE APPLICATIONS ADD COLUMN APK_VERSION_CODE"))),
                Times.Once);
        }

        [Fact]
        public async Task DownAsync_RemovesAndroidData()
        {
            // Act
            await _migration.DownAsync(_mockContext.Object, DatabaseType.SQLite);

            // Assert
            // Verify Android applications are removed
            _mockContext.Verify(x => x.ExecuteSqlAsync(
                It.Is<string>(sql => sql.Contains("DELETE FROM APPLICATIONS") 
                                  && sql.Contains("APPLICATION_TYPES") 
                                  && sql.Contains("Android"))), 
                Times.Once);

            // Verify Android application type is removed
            _mockContext.Verify(x => x.ExecuteSqlAsync(
                It.Is<string>(sql => sql.Contains("DELETE FROM APPLICATION_TYPES") 
                                  && sql.Contains("Android"))), 
                Times.Once);

            // Verify Android category is removed
            _mockContext.Verify(x => x.ExecuteSqlAsync(
                It.Is<string>(sql => sql.Contains("DELETE FROM CATEGORIES") 
                                  && sql.Contains("Android"))), 
                Times.Once);
        }

        [Theory]
        [InlineData(DatabaseType.SQLite)]
        [InlineData(DatabaseType.Firebird)]
        public async Task DownAsync_DropsAndroidIndexes(DatabaseType databaseType)
        {
            // Act
            await _migration.DownAsync(_mockContext.Object, databaseType);

            // Assert
            // Verify indexes are dropped (at least some key ones)
            string dropPattern = databaseType switch
            {
                DatabaseType.SQLite => "DROP INDEX IF EXISTS",
                DatabaseType.Firebird => "DROP INDEX",
                _ => throw new System.NotSupportedException()
            };

            _mockContext.Verify(x => x.ExecuteSqlAsync(
                It.Is<string>(sql => sql.Contains(dropPattern) && sql.Contains("IDX_APPLICATIONS_APK_PACKAGE"))),
                Times.Once);
        }

        [Theory]
        [InlineData("Android")]
        [InlineData(null)]
        public void Migration_SupportsUnsupportedDatabaseType_ThrowsException(string? databaseTypeStr)
        {
            // Arrange
            var unsupportedType = (DatabaseType)999;

            // Act & Assert
            Assert.ThrowsAsync<System.NotSupportedException>(
                () => _migration.UpAsync(_mockContext.Object, unsupportedType));
        }
    }
}