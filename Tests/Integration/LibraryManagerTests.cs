using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Kinopub.Plugin.Library;
using Kinopub.Plugin.Tests.Helpers;
using Xunit;

namespace Kinopub.Plugin.Tests.Integration
{
    public class LibraryManagerTests : IDisposable
    {
        private readonly string _tempPath;
        private readonly LibraryManager _libraryManager;

        public LibraryManagerTests()
        {
            _tempPath = TestHelpers.CreateTempDirectory();
            var config = TestHelpers.CreateTestConfig(_tempPath);
            var logger = TestHelpers.CreateMockLogger();
            _libraryManager = new LibraryManager(logger, config);
        }

        public void Dispose()
        {
            TestHelpers.CleanupTempDirectory(_tempPath);
        }

        [Fact]
        public void EnsureLibraryStructure_ShouldCreateMoviesAndTVShowsDirectories()
        {
            // Act
            _libraryManager.EnsureLibraryStructure();

            // Assert
            var moviesPath = Path.Combine(_tempPath, "Movies");
            var tvShowsPath = Path.Combine(_tempPath, "TV Shows");
            
            Directory.Exists(moviesPath).Should().BeTrue();
            Directory.Exists(tvShowsPath).Should().BeTrue();
        }

        [Fact]
        public void GetMovieFilePath_ShouldReturnCorrectPath()
        {
            // Arrange
            var itemId = "12345";

            // Act
            var filePath = _libraryManager.GetMovieFilePath(itemId);

            // Assert
            filePath.Should().Contain("Movies");
            filePath.Should().Contain($"kinopub-{itemId}.strm");
            Path.GetExtension(filePath).Should().Be(".strm");
        }

        [Fact]
        public void GetEpisodeFilePath_ShouldReturnCorrectPath()
        {
            // Arrange
            var seriesId = "67890";
            var episodeId = "123";
            var seasonNumber = 2;

            // Act
            var filePath = _libraryManager.GetEpisodeFilePath(seriesId, episodeId, seasonNumber);

            // Assert
            filePath.Should().Contain("TV Shows");
            filePath.Should().Contain($"kinopub-{seriesId}");
            filePath.Should().Contain("Season 02");
            filePath.Should().Contain($"kinopub-{seriesId}-{episodeId}.strm");
        }

        [Fact]
        public void GetSeriesDirectoryPath_ShouldReturnCorrectPath()
        {
            // Arrange
            var seriesId = "67890";

            // Act
            var dirPath = _libraryManager.GetSeriesDirectoryPath(seriesId);

            // Assert
            dirPath.Should().Contain("TV Shows");
            dirPath.Should().Contain($"kinopub-{seriesId}");
        }

        [Fact]
        public void GetSeasonDirectoryPath_ShouldReturnCorrectPath()
        {
            // Arrange
            var seriesId = "67890";
            var seasonNumber = 3;

            // Act
            var dirPath = _libraryManager.GetSeasonDirectoryPath(seriesId, seasonNumber);

            // Assert
            dirPath.Should().Contain("TV Shows");
            dirPath.Should().Contain($"kinopub-{seriesId}");
            dirPath.Should().Contain("Season 03");
        }

        [Fact]
        public void MovieFileExists_ShouldReturnFalseWhenFileDoesNotExist()
        {
            // Arrange
            var itemId = "99999";

            // Act
            var exists = _libraryManager.MovieFileExists(itemId);

            // Assert
            exists.Should().BeFalse();
        }

        [Fact]
        public void MovieFileExists_ShouldReturnTrueWhenFileExists()
        {
            // Arrange
            var itemId = "12345";
            _libraryManager.EnsureLibraryStructure();
            var filePath = _libraryManager.GetMovieFilePath(itemId);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, "test content");

            // Act
            var exists = _libraryManager.MovieFileExists(itemId);

            // Assert
            exists.Should().BeTrue();
        }

        [Fact]
        public void ValidateLibraryPath_ShouldReturnValidWhenPathIsSet()
        {
            // Act
            var result = _libraryManager.ValidateLibraryPath();

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public void GetLibraryStatistics_ShouldReturnCorrectCounts()
        {
            // Arrange
            _libraryManager.EnsureLibraryStructure();
            
            // Create some test files
            var moviesPath = Path.Combine(_tempPath, "Movies");
            File.WriteAllText(Path.Combine(moviesPath, "kinopub-111.strm"), "test1");
            File.WriteAllText(Path.Combine(moviesPath, "kinopub-222.strm"), "test2");
            
            var tvShowsPath = Path.Combine(_tempPath, "TV Shows", "kinopub-333", "Season 01");
            Directory.CreateDirectory(tvShowsPath);
            File.WriteAllText(Path.Combine(tvShowsPath, "kinopub-333-e1.strm"), "test3");

            // Act
            var stats = _libraryManager.GetLibraryStatistics();

            // Assert
            stats.MovieFiles.Should().Be(2);
            stats.SeriesDirectories.Should().Be(1);
            stats.EpisodeFiles.Should().Be(1);
        }

        [Fact]
        public void CleanupEmptyDirectories_ShouldRemoveEmptySeasonFolders()
        {
            // Arrange
            _libraryManager.EnsureLibraryStructure();
            var emptySeasonPath = Path.Combine(_tempPath, "TV Shows", "kinopub-444", "Season 01");
            Directory.CreateDirectory(emptySeasonPath);

            // Act
            _libraryManager.CleanupEmptyDirectories();

            // Assert
            Directory.Exists(emptySeasonPath).Should().BeFalse();
        }

        [Fact]
        public void CleanupEmptyDirectories_ShouldNotRemoveNonEmptyFolders()
        {
            // Arrange
            _libraryManager.EnsureLibraryStructure();
            var seasonPath = Path.Combine(_tempPath, "TV Shows", "kinopub-555", "Season 01");
            Directory.CreateDirectory(seasonPath);
            File.WriteAllText(Path.Combine(seasonPath, "test.strm"), "content");

            // Act
            _libraryManager.CleanupEmptyDirectories();

            // Assert
            Directory.Exists(seasonPath).Should().BeTrue();
        }
    }
}
