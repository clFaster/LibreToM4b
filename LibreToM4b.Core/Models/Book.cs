namespace LibreToM4b.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Represents an audiobook with its metadata and structure.
/// </summary>
public class Book
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public Description Description { get; set; } = new();

    [JsonPropertyName("coverUrl")]
    public string CoverUrl { get; set; } = string.Empty;

    [JsonPropertyName("creator")]
    public List<Creator> Creators { get; set; } = [];

    [JsonPropertyName("spine")]
    public List<Spine> Spine { get; set; } = [];

    [JsonPropertyName("chapters")]
    public List<Chapter> Chapters { get; set; } = [];
}

/// <summary>
/// Represents the description of an audiobook.
/// </summary>
public class Description
{
    [JsonPropertyName("full")]
    public string Full { get; set; } = string.Empty;

    [JsonPropertyName("short")]
    public string Short { get; set; } = string.Empty;
}

/// <summary>
/// Represents a creator (author, narrator, etc.) of an audiobook.
/// </summary>
public class Creator
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("bio")]
    public string Bio { get; set; } = string.Empty;
}

/// <summary>
/// Represents a segment of the audiobook's audio content.
/// </summary>
public class Spine
{
    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("bitrate")]
    public int Bitrate { get; set; }
}

/// <summary>
/// Represents a chapter in the audiobook.
/// </summary>
public class Chapter
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("spine")]
    public int Spine { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonIgnore]
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
}
