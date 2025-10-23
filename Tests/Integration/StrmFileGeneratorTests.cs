using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Kinopub.Plugin.Library;
using Kinopub.Plugin.Tests.Helpers;
using Xunit;

namespace Kinopub.Plugin.Tests.Integration
{
    public class StrmFileGeneratorTests : IDisposable
    {
        private readonly string _tempPath;
        private readonly LibraryManager _libraryManager;
        private readonly StrmFileGenerator _strmGenerator;

        public StrmFileGeneratorTests()
        {
            _tempPath = TestHelpers.CreateTempDirectory();
            var config = TestHelpers.CreateTestConfig(_tempPath);
            var logger = TestHelpers.CreateMockLogger();
            _libraryManager = new LibraryManager(logger, config);
            _strmGenerator = new StrmFileGenerator(logger, _libraryManager, config);
        }

        public void Dispose()
        {
            TestHelpers.CleanupTempDirectory(_tempPath);
        }

        [Fact]
        public void GetStreamUrl_ForMovie_ShouldReturnCorrectUrl()
        {
            // Arrange
            var itemId = "12345";

            // Act
            var url = _strmGenerator.GetStreamUrl(itemId);

            // Assert
            url.Should().Contain("/Kinopub/Stream/12345");
            url.Should().Contain("ApiKey={api_key}");
            url.Should().NotContain("EpisodeId");
        }

        [Fact]
        public void GetStreamUrl_ForEpisode_ShouldReturnCorrectUrl()
        {
            // Arrange
            var itemId = "12345";
            var episodeId = "67890";

            // Act
            var url = _strmGenerator.GetStreamUrl(itemId, episodeId);

            // Assert
            url.Should().Contain("/Kinopub/Stream/12345");
            url.Should().Contain("EpisodeId=67890");
            url.Should().Contain("ApiKey={api_key}");
        }

        [Fact]
        public async Task GenerateMovieStrmFileAsync_ShouldCreateFile()
        {
            // Arrange
            _libraryManager.EnsureLibraryStructure();
            var itemId = "12345";

            // Act
            var result = await _strmGenerator.GenerateMovieStrmFileAsync(itemId);

            // Assert
            result.FilesCreated.Should().Be(1);
            result.Errors.Should().BeEmpty();
            
            var filePath = _libraryManager.GetMovieFilePath(itemId);
            File.Exists(filePath).Should().BeTrue();
            
            var content = await File.ReadAllTextAsync(filePath);
            content.Should().Contain($"/Kinopub/Stream/{itemId}");
        }

        [Fact]
        public async Task GenerateMovieStrmFileAsync_WithoutForceRecreate_ShouldSkipExistingFile()
        {
            // Arrange
            _libraryManager.EnsureLibraryStructure();
            var itemId = "12345";
            await _strmGenerator.GenerateMovieStrmFileAsync(itemId);

            // Act
            var result = await _strmGenerator.GenerateMovieStrmFileAsync(itemId, forceRecreate: false);

            // Assert
            result.FilesCreated.Should().Be(0);
            result.FilesSkipped.Should().Be(1);
        }

        [Fact]
        public async Task GenerateMovieStrmFileAsync_WithForceRecreate_ShouldRecreateFile()
        {
            // Arrange
            _libraryManager.EnsureLibraryStructure();
            var itemId = "12345";
            await _strmGenerator.GenerateMovieStrmFileAsync(itemId);

            // Act
            var result = await _strmGenerator.GenerateMovieStrmFileAsync(itemId, forceRecreate: true);

            // Assert
            result.FilesCreated.Should().Be(0);
            result.FilesUpdated.Should().Be(1);
        }

        [Fact]
        public async Task GenerateSeriesFilesAsync_ShouldCreateEpisodeFiles()
        {
            // Arrange
            _libraryManager.EnsureLibraryStructure();
            var seriesId = "67890";
            var episodes = new List<EpisodeInfo>
            {
                new EpisodeInfo { EpisodeId = "e1", SeasonNumber = 1, EpisodeNumber = 1, Title = "Episode 1" },
                new EpisodeInfo { EpisodeId = "e2", SeasonNumber = 1, EpisodeNumber = 2, Title = "Episode 2" },
                new EpisodeInfo { EpisodeId = "e3", SeasonNumber = 2, EpisodeNumber = 1, Title = "Episode 3" }
            };

            // Act
            var result = await _strmGenerator.GenerateSeriesFilesAsync(seriesId, episodes);

            // Assert
            result.FilesCreated.Should().Be(3);
            result.Errors.Should().BeEmpty();
            
            // Verify season directories exist
            var season1Path = _libraryManager.GetSeasonDirectoryPath(seriesId, 1);
            var season2Path = _libraryManager.GetSeasonDirectoryPath(seriesId, 2);
            Directory.Exists(season1Path).Should().BeTrue();
            Directory.Exists(season2Path).Should().BeTrue();
            
            // Verify episode files exist
            var ep1Path = _libraryManager.GetEpisodeFilePath(seriesId, "e1", 1);
            File.Exists(ep1Path).Should().BeTrue();
        }

        [Fact]
        public async Task GenerateSeriesFilesAsync_WithoutForceRecreate_ShouldSkipExistingFiles()
        {
            // Arrange
            _libraryManager.EnsureLibraryStructure();
            var seriesId = "67890";
            var episodes = new List<EpisodeInfo>
            {
                new EpisodeInfo { EpisodeId = "e1", SeasonNumber = 1, EpisodeNumber = 1, Title = "Episode 1" }
            };
            
            // Create file first
            await _strmGenerator.GenerateSeriesFilesAsync(seriesId, episodes);

            // Act
            var result = await _strmGenerator.GenerateSeriesFilesAsync(seriesId, episodes, forceRecreate: false);

            // Assert
            result.FilesCreated.Should().Be(0);
            result.FilesSkipped.Should().Be(1);
        }

        [Fact]
        public async Task GenerateSeriesFilesAsync_WithMultipleSeasons_ShouldCreateCorrectStructure()
        {
            // Arrange
            _libraryManager.EnsureLibraryStructure();
            var seriesId = "67890";
            var episodes = new List<EpisodeInfo>();
            
            // Add 3 seasons with 2 episodes each
            for (int season = 1; season <= 3; season++)
            {
                for (int episode = 1; episode <= 2; episode++)
                {
                    episodes.Add(new EpisodeInfo
                    {
                        EpisodeId = $"s{season}e{episode}",
                        SeasonNumber = season,
                        EpisodeNumber = episode,
                        Title = $"S{season}E{episode}"
                    });
                }
            }

            // Act
            var result = await _strmGenerator.GenerateSeriesFilesAsync(seriesId, episodes);

            // Assert
            result.FilesCreated.Should().Be(6);
            
            // Verify all season directories exist
            for (int season = 1; season <= 3; season++)
            {
                var seasonPath = _libraryManager.GetSeasonDirectoryPath(seriesId, season);
                Directory.Exists(seasonPath).Should().BeTrue();
                
                // Verify episode count per season
                var episodeFiles = Directory.GetFiles(seasonPath, "*.strm");
                episodeFiles.Length.Should().Be(2);
            }
        }

        [Fact]
        public async Task GenerateMovieStrmFileAsync_ShouldUseAtomicWrite()
        {
            // Arrange
            _libraryManager.EnsureLibraryStructure();
            var itemId = "12345";

            // Act
            var result = await _strmGenerator.GenerateMovieStrmFileAsync(itemId);

            // Assert
            result.FilesCreated.Should().Be(1);
            
            var filePath = _libraryManager.GetMovieFilePath(itemId);
            var tempFilePath = filePath + ".tmp";
            
            // Temp file should not exist after operation
            File.Exists(tempFilePath).Should().BeFalse();
            File.Exists(filePath).Should().BeTrue();
        }

        [Fact]
        public void ValidateConfiguration_ShouldReturnValid()
        {
            // Act
            var result = _strmGenerator.ValidateConfiguration();

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }
    }
}
