using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using OtterGui.Services;
using VenueSync.Data;

namespace VenueSync.Services;

public class ChatService: IService
{
    private readonly IChatGui _chatGui;
    
    public ChatService(IChatGui chatGui)
    {
        _chatGui = chatGui;
    }
    
    public void ChatPlayerLink(Player player, string? message = null)
    {

        var messageBuilder = new SeStringBuilder();
        messageBuilder.Add(new PlayerPayload(player.Name, player.HomeWorld));
        if (message != null)
        {
            messageBuilder.AddText(message);
        }

        var entry = new XivChatEntry() { Message = messageBuilder.Build() };
        _chatGui.Print(entry);
    }
}
