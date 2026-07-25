using System.Collections.Generic;

namespace HunterPie.Features.Api.Models;

internal class PartyMemberDto
{
    public string Name { get; set; } = string.Empty;
    public int MasterRank { get; set; }
    public int Damage { get; set; }
    public string Weapon { get; set; } = string.Empty;
    public int Slot { get; set; }
    public bool IsMyself { get; set; }
    public string Type { get; set; } = string.Empty;
    public PlayerStatusDto? Status { get; set; }
}

internal class PartyDto
{
    public int Size { get; set; }
    public int MaxSize { get; set; }
    public List<PartyMemberDto> Members { get; set; } = new();
}

internal class QuestDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Deaths { get; set; }
    public int MaxDeaths { get; set; }
    public string Level { get; set; } = string.Empty;
    public int Stars { get; set; }
    public double TimeLeftSeconds { get; set; }
}

internal class ChatMessageDto
{
    public string Message { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int PlayerSlot { get; set; }
}

internal class ChatDto
{
    public bool IsOpen { get; set; }
    public List<ChatMessageDto> Messages { get; set; } = new();
}
