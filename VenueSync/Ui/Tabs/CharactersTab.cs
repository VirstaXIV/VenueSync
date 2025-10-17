using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using OtterGui.Text;
using OtterGui.Widgets;
using VenueSync.Services;
using VenueSync.State;

namespace VenueSync.Ui.Tabs;

public class CharactersTab(StateService stateService): ITab
{
    public ReadOnlySpan<byte> Label => "Characters"u8;

    public void DrawContent()
    {
        using var child = ImUtf8.Child("MainWindowChild"u8, default);
        if (!child)
            return;

        using (ImUtf8.Child("CharactersChild"u8, default))
        {
            DrawCharactersTable();
        }
    }

    private Vector4 GetCharacterColor(UserCharacterItem character)
    {
        if (character.name == stateService.PlayerState.name)
        {
            return new Vector4(0, 1, 0, 1);
        }

        return new Vector4(1, 1, 1, 1);
    }

    private void DrawCharactersTable()
    {
        if (ImGui.BeginTable("Characters", 4))
        {
            ImGui.TableSetupColumn("Is Main");
            ImGui.TableSetupColumn("Name");
            ImGui.TableSetupColumn("Lodestone ID");
            ImGui.TableSetupColumn("World");
            ImGui.TableHeadersRow();
            
            if (stateService.HasCharacters())
            {
                foreach (var character in stateService.UserState.characters)
                {
                    var world = $"{character.world} [{character.data_center}]";
                    var color = GetCharacterColor(character);
                    
                    ImGui.TableNextColumn();
                    ImGui.TextColored(color, character.main == 1 ? "Yes" : "No");
                    ImGui.TableNextColumn();
                    ImGui.TextColored(color, character.name);
                    ImGui.TableNextColumn();
                    ImGui.TextColored(color, character.lodestone_id);
                    ImGui.TableNextColumn();
                    ImGui.TextColored(color, world);
                }
            }
            else
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.TextColored(new Vector4(1,1,1,1), "No Characters");
            }
            
            ImGui.EndTable();
        }
    }
}