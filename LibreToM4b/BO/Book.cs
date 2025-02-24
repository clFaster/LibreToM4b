namespace LibreToM4b.BO;

using System.Collections.Generic;
using System.Text.Json.Serialization;

public class Book
{
    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("description")]
    public Description Description { get; set; }

    [JsonPropertyName("coverUrl")]
    public string CoverUrl { get; set; }

    [JsonPropertyName("creator")]
    public List<Creator> Creators { get; set; }

    [JsonPropertyName("spine")]
    public List<Spine> Spine { get; set; }

    [JsonPropertyName("chapters")]
    public List<Chapter> Chapters { get; set; }
}

public class Description
{
    [JsonPropertyName("full")]
    public string Full { get; set; }

    [JsonPropertyName("short")]
    public string Short { get; set; }
}

public class Creator
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; }

    [JsonPropertyName("bio")]
    public string Bio { get; set; }
}

public class Spine
{
    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("bitrate")]
    public int Bitrate { get; set; }
}

public class Chapter
{
    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("spine")]
    public int Spine { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }
}
