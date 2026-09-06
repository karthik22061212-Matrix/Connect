using System;

namespace Connect.Infrastructure.Configuration;

public class TurnSettings
{
    public const string SectionName = "Turn";
    public string SharedSecret { get; set; } = string.Empty;
    public string[] Uris { get; set; } = Array.Empty<string>();
    public int TtlSeconds { get; set; } = 3600;
}
