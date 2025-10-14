using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using OtterGui;
using OtterGui.Services;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;
using Penumbra.GameData.Structs;
using VenueSync.State;

namespace VenueSync.Data;

public record ManipulationImcRecord
{
    public ImcEntry Entry { get; set; } = new();
    public string ObjectType { get; set; } = "Equipment";
    public PrimaryId PrimaryId { get; set; } = 0;
    public SecondaryId SecondaryId { get; set; } = 0;
    public Variant Variant { get; set; } = 1;
    public string EquipSlot { get; set; } = Penumbra.GameData.Enums.EquipSlot.Body.ToName();
    public string BodySlot { get; set; } = "Unknown";
}

public record ManipulationRecord
{
    public string Type { get; set; } = "Imc";
    public ManipulationImcRecord Manipulation { get; set; } = new();
}

public record ManipulationDataRecord
{
    public List<string> Paths { get; set; } = [];
    public string ManipulationString { get; set; } = "";
}

public class ManipulationDataManager: IService
{

    private ManipulationImcRecord GetMannequinSlot(KeyValuePair<ActorIdentifier, ActorData> mannequin, EquipSlot slot)
    {
        var actor = mannequin.Value.Objects[0];
        var armor = actor.GetArmor(slot);
        
        return new ManipulationImcRecord() {
            PrimaryId = armor.Set,
            Variant = armor.Variant,
            EquipSlot = slot.ToName(),
            Entry = new ImcEntry() {
                VfxId = 1
            }
        };
    }

    private string GetPath(ManipulationImcRecord record)
    {
        return $"chara/equipment/e{record.PrimaryId.Id:D4}/vfx/eff/ve{record.Entry.VfxId:D4}.avfx";
    }
    
    public ManipulationDataRecord BuildManipulationData(KeyValuePair<ActorIdentifier, ActorData> mannequin, List<MannequinModItem> mods)
    {
        var paths = new List<string>();
        var imcs = new List<ManipulationRecord>();
        List<EquipSlot> availableSlots = [EquipSlot.Head, EquipSlot.Body, EquipSlot.Hands, EquipSlot.Legs, EquipSlot.Feet];

        foreach (var mod in mods)
        {
            var activeSlot = availableSlots.FirstOrDefault();
            if (activeSlot != EquipSlot.Nothing)
            {
                var useSlot = GetMannequinSlot(mannequin, activeSlot);
                paths.Add(GetPath(useSlot));
                imcs.Add(new ManipulationRecord() {
                    Manipulation = useSlot
                });
                
                availableSlots.Remove(activeSlot);
            }
        }
        
        VenueSync.Log.Debug($"Setting IMC: {JsonConvert.SerializeObject(imcs)}");

        return new ManipulationDataRecord() {
            Paths = paths,
            ManipulationString = GetManipulationString(imcs)
        };
    }

    private string GetManipulationString(List<ManipulationRecord> records)
    {
        return Functions.ToCompressedBase64(records, 0);
    }
}
