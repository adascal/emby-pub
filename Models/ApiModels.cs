using System;
using System.Collections.Generic;

namespace ServiceKP.Plugin.Models
{
    public class ApiResponse
    {
        public int? Status { get; set; }
        public string? Error { get; set; }
        public string? ErrorDescription { get; set; }
    }

    public class DeviceCodeResponse : ApiResponse
    {
        public string Code { get; set; } = string.Empty;
        public string UserCode { get; set; } = string.Empty;
        public string VerificationUri { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public int Interval { get; set; }
    }

    public class TokensResponse : ApiResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class User
    {
        public string Username { get; set; } = string.Empty;
        public long RegDate { get; set; }
        public UserSubscription Subscription { get; set; } = new();
        public UserSettings Settings { get; set; } = new();
        public UserProfile Profile { get; set; } = new();
    }

    public class UserSubscription
    {
        public bool Active { get; set; }
        public long EndTime { get; set; }
        public int Days { get; set; }
    }

    public class UserSettings
    {
        public bool ShowErotic { get; set; }
        public bool ShowUncertain { get; set; }
    }

    public class UserProfile
    {
        public string Name { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
    }

    public class UserResponse : ApiResponse
    {
        public User User { get; set; } = new();
    }

    public class DeviceInfo
    {
        public string Title { get; set; } = string.Empty;
        public string Hardware { get; set; } = string.Empty;
        public string Software { get; set; } = string.Empty;
    }

    public class Type
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
    }

    public class TypesResponse : ApiResponse
    {
        public List<Type> Items { get; set; } = new();
    }

    public class Genre
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    public class GenresResponse : ApiResponse
    {
        public List<Genre> Items { get; set; } = new();
    }

    public class Country
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
    }

    public class CountriesResponse : ApiResponse
    {
        public List<Country> Items { get; set; } = new();
    }

    public class Posters
    {
        public string Small { get; set; } = string.Empty;
        public string Medium { get; set; } = string.Empty;
        public string Big { get; set; } = string.Empty;
        public string? Wide { get; set; }
    }

    public class Trailer
    {
        public string Id { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public List<FileInfo>? Files { get; set; }
    }

    public class Item
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Subtype { get; set; }
        public int Year { get; set; }
        public string Cast { get; set; } = string.Empty;
        public string Director { get; set; } = string.Empty;
        public string Voice { get; set; } = string.Empty;
        public long CreatedAt { get; set; }
        public long UpdatedAt { get; set; }
        public Duration Duration { get; set; } = new();
        public int Langs { get; set; }
        public int Ac3 { get; set; }
        public int Subtitles { get; set; }
        public int Quality { get; set; }
        public bool PoorQuality { get; set; }
        public List<Genre> Genres { get; set; } = new();
        public List<Country> Countries { get; set; } = new();
        public string Plot { get; set; } = string.Empty;
        public double Imdb { get; set; }
        public double ImdbRating { get; set; }
        public int ImdbVotes { get; set; }
        public double Kinopoisk { get; set; }
        public double KinopoiskRating { get; set; }
        public int KinopoiskVotes { get; set; }
        public double Rating { get; set; }
        public int RatingVotes { get; set; }
        public int RatingPercentage { get; set; }
        public int Views { get; set; }
        public int Comments { get; set; }
        public bool Finished { get; set; }
        public bool Advert { get; set; }
        public Posters Posters { get; set; } = new();
        public Trailer Trailer { get; set; } = new();
        public int? Total { get; set; }
        public int? Watched { get; set; }
        public int? New { get; set; }
    }

    public class Duration
    {
        public int Average { get; set; }
        public int Total { get; set; }
    }

    public class ItemDetails : Item
    {
        public List<Video>? Videos { get; set; }
        public List<Season>? Seasons { get; set; }
    }

    public class Pagination
    {
        public int Total { get; set; }
        public int Current { get; set; }
        public int Perpage { get; set; }
        public int? TotalItems { get; set; }
    }

    public class ItemsResponse : ApiResponse
    {
        public List<Item> Items { get; set; } = new();
        public Pagination Pagination { get; set; } = new();
    }

    public class ItemMediaResponse : ApiResponse
    {
        public ItemDetails Item { get; set; } = new();
    }

    public class Season
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int Number { get; set; }
        public List<Video> Episodes { get; set; } = new();
        public int Watched { get; set; }
        public WatchingInfo Watching { get; set; } = new();
    }

    public class Video
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Thumbnail { get; set; } = string.Empty;
        public int Number { get; set; }
        public int Snumber { get; set; }
        public int Duration { get; set; }
        public int Watched { get; set; }
        public WatchingInfo Watching { get; set; } = new();
        public List<string> Tracks { get; set; } = new();
        public List<Subtitle> Subtitles { get; set; } = new();
        public List<Audio> Audios { get; set; } = new();
        public List<FileInfo> Files { get; set; } = new();
        public int Ac3 { get; set; }
    }

    public class WatchingInfo
    {
        public int Status { get; set; }
        public int Time { get; set; }
    }

    public class Subtitle
    {
        public string Lang { get; set; } = string.Empty;
        public int Shift { get; set; }
        public bool Embed { get; set; }
        public bool Forced { get; set; }
        public string File { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }

    public class Audio
    {
        public string Id { get; set; } = string.Empty;
        public int Index { get; set; }
        public string Codec { get; set; } = string.Empty;
        public int Channels { get; set; }
        public string Lang { get; set; } = string.Empty;
        public AudioType? Type { get; set; }
        public AudioAuthor? Author { get; set; }
    }

    public class AudioType
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? ShortTitle { get; set; }
    }

    public class AudioAuthor
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? ShortTitle { get; set; }
    }

    public class FileInfo
    {
        public int W { get; set; }
        public int H { get; set; }
        public string Codec { get; set; } = string.Empty;
        public string Quality { get; set; } = string.Empty;
        public string QualityId { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
        public FileUrls Url { get; set; } = new();
    }

    public class FileUrls
    {
        public string Http { get; set; } = string.Empty;
        public string? Hls { get; set; }
        public string? Hls2 { get; set; }
        public string? Hls4 { get; set; }
    }

    public class ItemMediaLinksResponse : ApiResponse
    {
        public List<FileInfo> Files { get; set; } = new();
        public List<Subtitle> Subtitles { get; set; } = new();
    }

    public class SearchResponse : ApiResponse
    {
        public List<Item> Items { get; set; } = new();
        public Pagination Pagination { get; set; } = new();
    }

    public class Bookmark
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int Views { get; set; }
        public int Count { get; set; }
        public long Created { get; set; }
        public long Updated { get; set; }
    }

    public class BookmarksResponse : ApiResponse
    {
        public List<Bookmark> Items { get; set; } = new();
    }

    public class BookmarkItemsResponse : ApiResponse
    {
        public Bookmark Folder { get; set; } = new();
        public List<Item> Items { get; set; } = new();
        public Pagination Pagination { get; set; } = new();
    }

    public class Collection
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int Watchers { get; set; }
        public int Views { get; set; }
        public long Created { get; set; }
        public long Updated { get; set; }
        public Posters Posters { get; set; } = new();
    }

    public class CollectionsResponse : ApiResponse
    {
        public List<Collection> Items { get; set; } = new();
        public Pagination Pagination { get; set; } = new();
    }

    public class CollectionItemsResponse : ApiResponse
    {
        public Collection Collection { get; set; } = new();
        public List<Item> Items { get; set; } = new();
    }

    public class HistoryItem
    {
        public int Time { get; set; }
        public int Counter { get; set; }
        public long FirstSeen { get; set; }
        public long LastSeen { get; set; }
        public Item Item { get; set; } = new();
        public Video Media { get; set; } = new();
    }

    public class HistoryResponse : ApiResponse
    {
        public List<HistoryItem> History { get; set; } = new();
        public Pagination Pagination { get; set; } = new();
    }

    public class WatchingItemsResponse : ApiResponse
    {
        public List<Item> Items { get; set; } = new();
    }

    public class ChannelLogos
    {
        public string S { get; set; } = string.Empty;
        public string M { get; set; } = string.Empty;
    }

    public class Channel
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public ChannelLogos? Logos { get; set; }
        public string Stream { get; set; } = string.Empty;
    }

    public class ChannelsResponse : ApiResponse
    {
        public List<Channel> Channels { get; set; } = new();
    }

    public class WatchingToggleResponse : ApiResponse
    {
        public int Watched { get; set; }
    }

    public class WatchingToggleWatchlistResponse : ApiResponse
    {
        public bool Watching { get; set; }
    }
}
