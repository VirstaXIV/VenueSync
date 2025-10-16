using System.Numerics;

namespace VenueSync.Ui;

public static class VenueColors
{
    // Headers and Titles
    public static readonly Vector4 VenueName = new(0.9f, 0.5f, 1f, 1f);
    public static readonly Vector4 SectionHeader = new(1f, 0.7f, 0.4f, 1f);
    public static readonly Vector4 ModeratorHeader = new(1f, 0.5f, 0.5f, 1f);
    
    // Text Colors
    public static readonly Vector4 LocationText = new(0.4f, 0.8f, 1f, 1f);
    public static readonly Vector4 OpenHours = new(0.6f, 1f, 0.6f, 1f);
    public static readonly Vector4 Tag = new(0.9f, 0.7f, 1f, 1f);
    public static readonly Vector4 DescriptionText = new(0.9f, 0.9f, 0.9f, 1f);
    public static readonly Vector4 StaffName = new(1f, 0.8f, 0.4f, 1f);
    public static readonly Vector4 StaffPosition = new(0.7f, 0.7f, 0.7f, 1f);
    public static readonly Vector4 ProgressText = new(1.0f, 1.0f, 1.0f, 1.0f);
    
    // Status Indicators
    public static readonly Vector4 ActiveIndicator = new(0.4f, 1f, 0.4f, 1f);
    public static readonly Vector4 WarningText = new(1f, 0.6f, 0.3f, 1f);
    
    // Backgrounds
    public static readonly Vector4 DescriptionBackground = new(0.1f, 0.1f, 0.1f, 0.3f);
    public static readonly Vector4 ProgressBackground = new(0.2f, 0.2f, 0.25f, 1.0f);
    public static readonly Vector4 ProgressBar = new(0.3f, 0.7f, 0.3f, 1.0f);
    
    // Buttons - Carrd
    public static readonly Vector4 CarrdButton = new(0.9f, 0.4f, 0.6f, 1f);
    public static readonly Vector4 CarrdButtonHover = new(1f, 0.5f, 0.7f, 1f);
    
    // Buttons - Discord
    public static readonly Vector4 DiscordButton = new(0.35f, 0.4f, 0.9f, 1f);
    public static readonly Vector4 DiscordButtonHover = new(0.45f, 0.5f, 1f, 1f);
    
    // Buttons - Twitch
    public static readonly Vector4 TwitchButton = new(0.58f, 0.29f, 0.78f, 1f);
    public static readonly Vector4 TwitchButtonHover = new(0.68f, 0.39f, 0.88f, 1f);
    
    // Buttons - Mod Controls
    public static readonly Vector4 ReloadButton = new(0.2f, 0.6f, 0.8f, 1f);
    public static readonly Vector4 ReloadButtonHover = new(0.3f, 0.7f, 0.9f, 1f);
    public static readonly Vector4 DisableButton = new(0.8f, 0.3f, 0.3f, 1f);
    public static readonly Vector4 DisableButtonHover = new(0.9f, 0.4f, 0.4f, 1f);
}