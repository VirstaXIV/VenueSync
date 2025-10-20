using System;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using OtterGui.Text;
using OtterGui.Widgets;
using VenueSync.Services;
using VenueSync.Services.Api;
using VenueSync.State;

namespace VenueSync.Ui.Tabs;

public class CharactersTab(StateService stateService, CharacterApi characterApi): ITab
{
    public ReadOnlySpan<byte> Label => "Characters"u8;
    private bool _openVerifyConfirm;

    public void DrawContent()
    {
        using var child = ImUtf8.Child("MainWindowChild"u8, default);
        if (!child)
            return;

        DrawVerifyButtonAndConfirm();

        using (ImUtf8.Child("CharactersChild"u8, default))
        {
            DrawCharactersTable();
        }
    }

    private void DrawVerifyButtonAndConfirm()
    {
        if (ImUtf8.Button("Verify Characters"))
        {
            _openVerifyConfirm = true;
        }

        if (_openVerifyConfirm)
        {
            ImGui.OpenPopup("Verify Characters Confirmation");
            _openVerifyConfirm = false;
        }

        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

        var open = true;
        if (ImGui.BeginPopupModal("Verify Characters Confirmation", ref open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 40f);
            ImGui.TextUnformatted(
                "This will attempt to verify your characters.\n\n" +
                "Characters must be shared in XIVAuth."
            );
            ImGui.PopTextWrapPos();

            ImGui.Spacing();

            if (ImGui.Button("Continue"))
            {
                _ = TriggerCharacterVerificationAsync();
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
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

    private async Task TriggerCharacterVerificationAsync()
    {
        try
        {
            var reply = await characterApi.VerifyCharacters().ConfigureAwait(false);
            if (!reply.Success)
            {
                VenueSync.Log.Warning($"Character verification failed: {reply.ErrorMessage}");
            }
            else
            {
                VenueSync.Log.Debug("Character verification submitted successfully.");
            }
        }
        catch (Exception ex)
        {
            VenueSync.Log.Warning($"Character verification error: {ex.Message}");
        }
    }
}