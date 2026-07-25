using System.Collections.Generic;

namespace HunterPie.Features.Api.Models;

internal class PositionDto
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
}

internal class HealthDto
{
    public double Current { get; set; }
    public double Max { get; set; }
    public double Heal { get; set; }
    public double Recoverable { get; set; }
    public double MaxPossible { get; set; }
}

internal class StaminaDto
{
    public double Current { get; set; }
    public double Max { get; set; }
    public double MaxPossible { get; set; }
}

internal class PlayerStatusDto
{
    public double Raw { get; set; }
    public double Elemental { get; set; }
    public double Affinity { get; set; }
}

internal class AbnormalityDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public float Timer { get; set; }
    public float MaxTimer { get; set; }
    public bool IsInfinite { get; set; }
    public int Level { get; set; }
    public bool IsBuildUp { get; set; }
}

internal class SpecializedToolDto
{
    public string Id { get; set; } = string.Empty;
    public float Cooldown { get; set; }
    public float MaxCooldown { get; set; }
    public float Timer { get; set; }
    public float MaxTimer { get; set; }
}

internal class WirebugDto
{
    public bool IsAvailable { get; set; }
    public bool IsTemporary { get; set; }
    public double Timer { get; set; }
    public double MaxTimer { get; set; }
    public double Cooldown { get; set; }
    public double MaxCooldown { get; set; }
}

internal class PlayerDto
{
    public string Name { get; set; } = string.Empty;
    public int HighRank { get; set; }
    public int MasterRank { get; set; }
    public int StageId { get; set; }
    public bool InHuntingZone { get; set; }
    public PositionDto? Position { get; set; }
    public HealthDto? Health { get; set; }
    public StaminaDto? Stamina { get; set; }
    public PlayerStatusDto? Status { get; set; }
    public WeaponDto? Weapon { get; set; }
    public List<SpecializedToolDto>? Tools { get; set; }
    public List<WirebugDto>? Wirebugs { get; set; }
    public List<AbnormalityDto> Abnormalities { get; set; } = new();
}
