using System.Collections.Generic;

namespace HunterPie.Features.Api.Models;

internal class MonsterPartDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public float Health { get; set; }
    public float MaxHealth { get; set; }
    public float Flinch { get; set; }
    public float MaxFlinch { get; set; }
    public float Sever { get; set; }
    public float MaxSever { get; set; }
    public float Tenderize { get; set; }
    public float MaxTenderize { get; set; }
    public int BreakCount { get; set; }
}

internal class MonsterAilmentDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Counter { get; set; }
    public float Timer { get; set; }
    public float MaxTimer { get; set; }
    public float BuildUp { get; set; }
    public float MaxBuildUp { get; set; }
}

internal class MonsterDto
{
    /// <summary>Runtime index of this monster in the current session (spawn order).</summary>
    public int Index { get; set; }

    /// <summary>In-game monster id. Multiple monsters may share the same id.</summary>
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Variant { get; set; } = string.Empty;
    public string Crown { get; set; } = string.Empty;
    public float Health { get; set; }
    public float MaxHealth { get; set; }
    public float Stamina { get; set; }
    public float MaxStamina { get; set; }
    public bool IsEnraged { get; set; }
    public float CaptureThreshold { get; set; }
    public string Target { get; set; } = string.Empty;
    public string ManualTarget { get; set; } = string.Empty;

    /// <summary>True when the player is inferred to be engaged with this monster
    /// (recent damage, proximity and health heuristics).</summary>
    public bool IsEngaged { get; set; }
    public PositionDto? Position { get; set; }
    public List<string> Weaknesses { get; set; } = new();
    public List<string> Types { get; set; } = new();
    public List<MonsterPartDto> Parts { get; set; } = new();
    public List<MonsterAilmentDto> Ailments { get; set; } = new();
}
