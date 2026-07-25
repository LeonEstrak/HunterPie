using System.Collections.Generic;

namespace HunterPie.Features.Api.Models;

internal class GameDto
{
    public float TimeElapsed { get; set; }
    public string WorldTime { get; set; } = "00:00";
    public bool IsHudOpen { get; set; }
}

internal class GameStateDto
{
    public GameDto? Game { get; set; }
    public PlayerDto? Player { get; set; }
    public PartyDto? Party { get; set; }
    public List<MonsterDto>? Monsters { get; set; }
    public QuestDto? Quest { get; set; }
    public ChatDto? Chat { get; set; }
}
