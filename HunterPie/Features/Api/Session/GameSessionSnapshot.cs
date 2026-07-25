using System;
using System.Collections.Generic;
using System.Threading;
using HunterPie.Features.Api.Models;

namespace HunterPie.Features.Api.Session;

/// <summary>
/// Thread-safe snapshot of the current game session state. Mutated by the
/// <see cref="ApiContextTracker"/> from game scanner threads; serialized by
/// the REST handlers and the WebSocket broadcaster.
///
/// Convention: ALL access (read or write) to the DTO graph must happen
/// inside <see cref="ExecuteLocked(Action{GameSessionSnapshot})"/> /
/// <see cref="ExecuteLocked{T}"/>. Mutations must be followed by
/// <see cref="MarkDirty"/> for the affected section so the broadcaster
/// knows what to push.
/// </summary>
internal class GameSessionSnapshot
{
    /// <summary>Maximum number of chat messages kept in the snapshot.</summary>
    public const int CHAT_HISTORY_LIMIT = 50;

    private readonly Lock _lock = new();
    private readonly HashSet<string> _dirtySections = new(StringComparer.OrdinalIgnoreCase);

    // Session metadata (null when no game is running)
    public string? GameType { get; set; }
    public string? GameProcessName { get; set; }
    public int? GameProcessId { get; set; }
    public bool HasSession => GameType is not null;

    // Sections
    public GameDto Game { get; } = new();
    public PlayerDto? Player { get; set; }
    public PartyDto? Party { get; set; }
    public List<MonsterDto> Monsters { get; } = new();
    public QuestDto? Quest { get; set; }
    public ChatDto Chat { get; } = new();

    /// <summary>Discrete WS events waiting to be broadcast.</summary>
    public Queue<object> PendingEvents { get; } = new();

    public void ExecuteLocked(Action<GameSessionSnapshot> mutation)
    {
        lock (_lock)
            mutation(this);
    }

    public T ExecuteLocked<T>(Func<GameSessionSnapshot, T> query)
    {
        lock (_lock)
            return query(this);
    }

    /// <summary>Must be called with the lock held (inside ExecuteLocked).</summary>
    public void MarkDirty(string section) => _dirtySections.Add(section);

    /// <summary>Returns the pending dirty sections and clears them.</summary>
    public string[] ConsumeDirtySections()
    {
        lock (_lock)
        {
            var sections = new string[_dirtySections.Count];
            _dirtySections.CopyTo(sections);
            _dirtySections.Clear();
            return sections;
        }
    }

    public void Reset()
    {
        ExecuteLocked(snapshot =>
        {
            snapshot.GameType = null;
            snapshot.GameProcessName = null;
            snapshot.GameProcessId = null;
            snapshot.Player = null;
            snapshot.Party = null;
            snapshot.Quest = null;
            snapshot.Monsters.Clear();
            snapshot.Chat.Messages.Clear();
            snapshot.Chat.IsOpen = false;
            snapshot.Game.TimeElapsed = 0;
            snapshot.Game.WorldTime = "00:00";
            snapshot.Game.IsHudOpen = false;
            snapshot.PendingEvents.Clear();
        });
    }
}
