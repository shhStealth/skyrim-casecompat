namespace CaseCompat.Bethesda.Plugins;

public sealed record SkyrimRecordInventoryResult(
    string FullPath,
    string ModKey,
    int TotalMajorRecords,
    int Armors,
    int ArmorAddons,
    int Statics,
    int Weapons,
    int Npcs,
    int TextureSets
);
