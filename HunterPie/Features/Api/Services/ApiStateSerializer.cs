using System.Collections.Generic;
using HunterPie.Features.Api.Server;
using HunterPie.Features.Api.Session;

namespace HunterPie.Features.Api.Services;

/// <summary>
/// Serializes snapshot sections into WebSocket protocol messages.
/// Sections are serialized as a dictionary so that null sections
/// (e.g. no active quest) are sent as explicit nulls, letting clients
/// clear their state. Must be called with the snapshot lock held.
/// </summary>
internal static class ApiStateSerializer
{
    public static readonly string[] AllSections = ["game", "player", "party", "monsters", "quest", "chat"];

    /// <summary>Full state sent to a client right after connecting.</summary>
    public static string SerializeFullSnapshot(GameSessionSnapshot snapshot)
    {
        object? data = snapshot.HasSession
            ? SectionDictionary(snapshot, AllSections)
            : null;

        return ApiJson.Serialize(new { type = "snapshot", data });
    }

    /// <summary>Partial update containing only the given sections.</summary>
    public static string SerializeStateUpdate(GameSessionSnapshot snapshot, string[] sections)
    {
        return ApiJson.Serialize(new
        {
            type = "state",
            data = SectionDictionary(snapshot, sections)
        });
    }

    private static Dictionary<string, object?> SectionDictionary(GameSessionSnapshot snapshot, IEnumerable<string> sections)
    {
        var data = new Dictionary<string, object?>();

        foreach (string section in sections)
            data[section] = section switch
            {
                "game" => snapshot.Game,
                "player" => snapshot.Player,
                "party" => snapshot.Party,
                "monsters" => snapshot.Monsters,
                "quest" => snapshot.Quest,
                "chat" => snapshot.Chat,
                _ => null
            };

        return data;
    }
}
