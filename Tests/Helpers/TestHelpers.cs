using System;
using System.IO;
using Kinopub.Plugin.Configuration;
using MediaBrowser.Model.Logging;
using Moq;

namespace Kinopub.Plugin.Tests.Helpers
{
    /// <summary>
    /// Helper utilities for tests
    /// </summary>
    public static class TestHelpers
    {
        /// <summary>
        /// Creates a temporary directory for testing
        /// </summary>
        public static string CreateTempDirectory()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"kinopub_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempPath);
            return tempPath;
        }

        /// <summary>
        /// Cleans up a temporary directory
        /// </summary>
        public static void CleanupTempDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    Directory.Delete(path, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        /// <summary>
        /// Creates a mock logger
        /// </summary>
        public static ILogger CreateMockLogger()
        {
            var mock = new Mock<ILogger>();
            
            mock.Setup(l => l.Info(It.IsAny<string>(), It.IsAny<object[]>()));
            mock.Setup(l => l.Debug(It.IsAny<string>(), It.IsAny<object[]>()));
            mock.Setup(l => l.Warn(It.IsAny<string>(), It.IsAny<object[]>()));
            mock.Setup(l => l.Error(It.IsAny<string>(), It.IsAny<object[]>()));
            
            return mock.Object;
        }

        /// <summary>
        /// Creates a test configuration
        /// </summary>
        public static PluginConfiguration CreateTestConfig(string? libraryPath = null, string? serverUrl = null)
        {
            return new PluginConfiguration
            {
                LibraryPath = libraryPath ?? CreateTempDirectory(),
                ServerUrl = serverUrl ?? "http://localhost:8096",
                EnableLibrarySync = true,
                SyncBookmarks = true,
                SyncCollections = true,
                SyncContinueWatching = true,
                AccessToken = "test_token",
                BatchSize = 10,
                MaxConcurrentOperations = 2
            };
        }

        /// <summary>
        /// Creates a test .strm file content
        /// </summary>
        public static string CreateTestStrmContent(string itemId, string? episodeId = null)
        {
            var baseUrl = "http://localhost:8096";
            if (string.IsNullOrEmpty(episodeId))
            {
                return $"{baseUrl}/Kinopub/Stream/{itemId}?ApiKey={{api_key}}";
            }
            return $"{baseUrl}/Kinopub/Stream/{itemId}?EpisodeId={episodeId}&ApiKey={{api_key}}";
        }

        /// <summary>
        /// Asserts that a directory exists
        /// </summary>
        public static void AssertDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException($"Expected directory to exist: {path}");
            }
        }

        /// <summary>
        /// Asserts that a file exists
        /// </summary>
        public static void AssertFileExists(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Expected file to exist: {path}");
            }
        }

        /// <summary>
        /// Asserts that a file contains specific content
        /// </summary>
        public static void AssertFileContains(string path, string expectedContent)
        {
            AssertFileExists(path);
            var content = File.ReadAllText(path);
            if (!content.Contains(expectedContent))
            {
                throw new Exception($"File does not contain expected content.\nExpected: {expectedContent}\nActual: {content}");
            }
        }

        /// <summary>
        /// Gets the number of files in a directory matching a pattern
        /// </summary>
        public static int CountFiles(string directory, string pattern = "*")
        {
            if (!Directory.Exists(directory))
            {
                return 0;
            }
            return Directory.GetFiles(directory, pattern, SearchOption.AllDirectories).Length;
        }

        /// <summary>
        /// Creates a disposable test environment
        /// </summary>
        public static IDisposable CreateTestEnvironment(out string tempPath, out PluginConfiguration config, out ILogger logger)
        {
            tempPath = CreateTempDirectory();
            config = CreateTestConfig(tempPath);
            logger = CreateMockLogger();
            
            return new TestEnvironment(tempPath);
        }

        private class TestEnvironment : IDisposable
        {
            private readonly string _tempPath;

            public TestEnvironment(string tempPath)
            {
                _tempPath = tempPath;
            }

            public void Dispose()
            {
                CleanupTempDirectory(_tempPath);
            }
        }
    }
}
