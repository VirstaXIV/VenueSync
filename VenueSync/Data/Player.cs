using System;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace VenueSync.Data;

[Serializable]
public class Player
{
    public string Name { get; set; } = "";
    public uint HomeWorld { get; set; } = 0;
    public bool InHouse { get; set; } = false;
    public bool IsFriend { get; set; } = false; 
    public ulong ObjectId { get; set; } = 0;
    public DateTime FirstSeen;
    public DateTime LastSeen;
    public DateTime LatestEntry;
    public DateTime TimeCursor;
    public double MilisecondsInHouse { get; set; } = 0;
    public int EntryCount { get; set; } = 0;
    public int LastRoll { get; set; } = 0;
    public int LastRollMax { get; set; } = 1000;
    public string WorldName { get; set; } = "";
    
    public static Player FromCharacter(IPlayerCharacter character) {
        Player player = new Player
        {
            Name = character.Name.TextValue,
            HomeWorld = character.HomeWorld.Value.RowId,
            WorldName = character.HomeWorld.Value.Name.ToString(),
            InHouse = true,
            IsFriend = character.StatusFlags.HasFlag(StatusFlags.Friend),
            ObjectId = character.GameObjectId,
            EntryCount = 1,
            FirstSeen = DateTime.Now,
            LastSeen = DateTime.Now,
            LatestEntry = DateTime.Now,
            LastRoll = 0,
            LastRollMax = 1000,
            TimeCursor = DateTime.Now
        };
        return player;
    }
    
    public void OnLeaveHouse() {
        InHouse = false;
    }
    
    public void OnAccumulateTime() {
        DateTime now = DateTime.Now;
        TimeSpan timeDiff = DateTime.Now  - TimeCursor;
        MilisecondsInHouse += timeDiff.TotalMilliseconds;
        TimeCursor = now;

    }

    public string GetTimeInHouse() {
        double secondsInVenue = MilisecondsInHouse / 1000;

        // Convert to semi human readable format 
        int minutes = (int)(secondsInVenue) / 60;
        int second = (int)(secondsInVenue) % 60;
        if (second < 10) 
            return minutes + ":0" + second;
        return minutes + ":" + second;
    }
}
