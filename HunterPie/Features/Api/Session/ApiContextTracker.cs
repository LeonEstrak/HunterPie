using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HunterPie.Core.Game;
using HunterPie.Core.Game.Entity;
using HunterPie.Core.Game.Entity.Enemy;
using HunterPie.Core.Game.Entity.Game;
using HunterPie.Core.Game.Entity.Game.Chat;
using HunterPie.Core.Game.Entity.Game.Quest;
using HunterPie.Core.Game.Entity.Party;
using HunterPie.Core.Game.Entity.Player;
using HunterPie.Core.Game.Entity.Player.Classes;
using HunterPie.Core.Game.Events;
using HunterPie.Core.Game.Services.Monster;
using HunterPie.Core.Observability.Logging;
using HunterPie.Domain.Interfaces;
using HunterPie.Features.Api.Models;
using HunterPie.Integrations.Datasources.Common.Monster;
using HunterPie.Integrations.Datasources.MonsterHunterWorld;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Entity.Player;
using HunterPie.Integrations.Datasources.MonsterHunterWilds.Entity.Player;
using HunterPie.Integrations.Datasources.MonsterHunterWorld.Entity.Player;

namespace HunterPie.Features.Api.Session;

/// <summary>
/// Hooks every relevant event of the live game context and mirrors the
/// state into the <see cref="GameSessionSnapshot"/>. Registered as an
/// <see cref="IContextInitializer"/>: initialized when a game starts and
/// disposed when it exits.
/// </summary>
internal class ApiContextTracker(GameSessionSnapshot snapshot) : IContextInitializer, IDisposable
{
    private readonly ILogger _logger = LoggerFactory.Create();
    private readonly GameSessionSnapshot _snapshot = snapshot;

    private IContext? _context;
    private IWeapon? _hookedWeapon;
    private IQuest? _hookedQuest;
    private IChat? _hookedChat;
    private WeightedTargetDetectionService? _targetDetectionService;
    private IMonster? _engagedMonster;

    private readonly Dictionary<IMonster, MonsterDto> _monsters = new();
    private readonly Dictionary<IMonsterPart, MonsterPartDto> _parts = new();
    private readonly Dictionary<IMonsterAilment, MonsterAilmentDto> _ailments = new();
    private readonly Dictionary<IAbnormality, AbnormalityDto> _abnormalities = new();
    private readonly Dictionary<IPartyMember, PartyMemberDto> _partyMembers = new();
    private readonly Dictionary<ISpecializedTool, SpecializedToolDto> _tools = new();
    private readonly Dictionary<MHRWirebug, WirebugDto> _wirebugs = new();

    private int _nextMonsterIndex;

    public Task InitializeAsync(IContext context)
    {
        _context = context;

        Safe(() =>
        {
            _snapshot.ExecuteLocked(snapshot =>
            {
                snapshot.GameType = context.Process.Type.ToString();
                snapshot.GameProcessName = context.Process.Name;
                snapshot.GameProcessId = context.Process.SystemProcess.Id;

                snapshot.Player = EntityMapper.MapPlayer(context.Game.Player);
                snapshot.Party = EntityMapper.MapParty(context.Game.Player.Party);

                foreach (IMonster monster in context.Game.Monsters)
                    AddMonster(snapshot, monster);

                if (context.Game.Quest is { } quest)
                    snapshot.Quest = EntityMapper.MapQuest(quest);

                if (context.Game.Chat is { } chat)
                {
                    snapshot.Chat.IsOpen = chat.IsChatOpen;
                    snapshot.Chat.Messages.AddRange(chat.Messages.Select(EntityMapper.MapChatMessage));
                }

                snapshot.MarkDirty("game");
                snapshot.MarkDirty("player");
                snapshot.MarkDirty("party");
                snapshot.MarkDirty("monsters");
                snapshot.MarkDirty("quest");
                snapshot.MarkDirty("chat");

                snapshot.PendingEvents.Enqueue(new
                {
                    type = "event",
                    @event = "session.start",
                    data = new { game = snapshot.GameType }
                });
            });

            HookGame(context);
            HookPlayer(context.Game.Player);
            HookParty(context.Game.Player.Party);
            HookWeapon(context.Game.Player.Weapon);

            foreach (IMonster monster in context.Game.Monsters)
                HookMonster(monster);

            if (context.Game.Quest is { } quest)
                HookQuest(quest);

            if (context.Game.Chat is { } chat)
                HookChat(chat);

            HookTools(context.Game.Player);
            HookWirebugs(context.Game.Player);
            InitializeTargetDetection(context);
        }, "session initialization");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Sets up the same target inference the monster overlay widget uses:
    /// flags the monster the player is engaged with based on recent damage,
    /// proximity and health heuristics.
    /// </summary>
    private void InitializeTargetDetection(IContext context)
    {
        DistanceFunc distanceFunc = context switch
        {
            MHWContext => static (System.Numerics.Vector3 playerPosition, System.Numerics.Vector3 monsterPosition) =>
                System.Numerics.Vector3.Distance(playerPosition, monsterPosition) / 100.0f,
            _ => System.Numerics.Vector3.Distance
        };

        _targetDetectionService = new WeightedTargetDetectionService(context, distanceFunc);
        _targetDetectionService.Initialize();
        _targetDetectionService.OnTargetChanged += OnEngagedTargetChanged;
    }

    private void OnEngagedTargetChanged(object? sender, HunterPie.Core.Game.Services.Monster.Events.InferTargetChangedEventArgs e) => Safe(() =>
    {
        _engagedMonster = e.Target;

        _snapshot.ExecuteLocked(snapshot =>
        {
            foreach ((IMonster monster, MonsterDto dto) in _monsters)
                dto.IsEngaged = monster == _engagedMonster;

            snapshot.MarkDirty("monsters");

            if (_engagedMonster is not null && _monsters.TryGetValue(_engagedMonster, out MonsterDto? engaged))
                snapshot.PendingEvents.Enqueue(new
                {
                    type = "event",
                    @event = "monster.engaged",
                    data = new { index = engaged.Index, id = engaged.Id, name = engaged.Name }
                });
        });
    }, "engaged target change");

    private void HookGame(IContext context)
    {
        context.Game.OnMonsterSpawn += OnMonsterSpawn;
        context.Game.OnMonsterDespawn += OnMonsterDespawn;
        context.Game.OnHudStateChange += OnHudStateChange;
        context.Game.OnTimeElapsedChange += OnTimeElapsedChange;
        context.Game.OnWorldTimeChange += OnWorldTimeChange;
        context.Game.OnQuestStart += OnQuestStart;
        context.Game.OnQuestEnd += OnQuestEnd;
    }

    private void UnhookGame(IContext context)
    {
        context.Game.OnMonsterSpawn -= OnMonsterSpawn;
        context.Game.OnMonsterDespawn -= OnMonsterDespawn;
        context.Game.OnHudStateChange -= OnHudStateChange;
        context.Game.OnTimeElapsedChange -= OnTimeElapsedChange;
        context.Game.OnWorldTimeChange -= OnWorldTimeChange;
        context.Game.OnQuestStart -= OnQuestStart;
        context.Game.OnQuestEnd -= OnQuestEnd;
    }

    #region Game events
    private void OnHudStateChange(object? sender, IGame game) => Safe(() =>
        _snapshot.ExecuteLocked(snapshot =>
        {
            snapshot.Game.IsHudOpen = game.IsHudOpen;
            snapshot.MarkDirty("game");
        }), "hud state change");

    private void OnTimeElapsedChange(object? sender, TimeElapsedChangeEventArgs e) => Safe(() =>
    {
        if (_context is null)
            return;

        _snapshot.ExecuteLocked(snapshot =>
        {
            snapshot.Game.TimeElapsed = _context.Game.TimeElapsed;
            snapshot.MarkDirty("game");
        });
    }, "time elapsed change");

    private void OnWorldTimeChange(object? sender, SimpleValueChangeEventArgs<TimeOnly> e) => Safe(() =>
    {
        if (_context is null)
            return;

        _snapshot.ExecuteLocked(snapshot =>
        {
            snapshot.Game.WorldTime = _context.Game.WorldTime.ToString("HH:mm");
            snapshot.MarkDirty("game");
        });
    }, "world time change");
    #endregion

    #region Player events
    private void HookPlayer(IPlayer player)
    {
        player.OnLogin += OnPlayerLogin;
        player.OnLogout += OnPlayerLogout;
        player.OnDeath += OnPlayerDeath;
        player.OnStageUpdate += OnPlayerStageUpdate;
        player.OnLevelChange += OnPlayerLevelChange;
        player.OnWeaponChange += OnWeaponChange;
        player.OnAbnormalityStart += OnAbnormalityStart;
        player.OnAbnormalityEnd += OnAbnormalityEnd;
        player.PositionChange += OnPlayerPositionChange;
        player.Health.OnHealthChange += OnVitalsChange;
        player.Health.OnHeal += OnVitalsChange;
        player.Stamina.OnStaminaChange += OnStaminaChange;

        if (player.Status is { } status)
        {
            status.AffinityChanged += OnPlayerStatusChanged;
            status.RawDamageChanged += OnPlayerStatusChanged;
            status.ElementalDamageChanged += OnPlayerStatusChanged;
        }

        foreach (IAbnormality abnormality in player.Abnormalities)
            abnormality.OnTimerUpdate += OnAbnormalityTimerUpdate;

        _snapshot.ExecuteLocked(snapshot =>
        {
            if (snapshot.Player is not { } playerDto)
                return;

            foreach (IAbnormality abnormality in player.Abnormalities)
            {
                AbnormalityDto? abnormalityDto = playerDto.Abnormalities.FirstOrDefault(it => it.Id == abnormality.Id);

                if (abnormalityDto is null)
                {
                    abnormalityDto = EntityMapper.MapAbnormality(abnormality);
                    playerDto.Abnormalities.Add(abnormalityDto);
                }

                _abnormalities[abnormality] = abnormalityDto;
            }
        });
    }

    private void UnhookPlayer(IPlayer player)
    {
        player.OnLogin -= OnPlayerLogin;
        player.OnLogout -= OnPlayerLogout;
        player.OnDeath -= OnPlayerDeath;
        player.OnStageUpdate -= OnPlayerStageUpdate;
        player.OnLevelChange -= OnPlayerLevelChange;
        player.OnWeaponChange -= OnWeaponChange;
        player.OnAbnormalityStart -= OnAbnormalityStart;
        player.OnAbnormalityEnd -= OnAbnormalityEnd;
        player.PositionChange -= OnPlayerPositionChange;
        player.Health.OnHealthChange -= OnVitalsChange;
        player.Health.OnHeal -= OnVitalsChange;
        player.Stamina.OnStaminaChange -= OnStaminaChange;

        if (player.Status is { } status)
        {
            status.AffinityChanged -= OnPlayerStatusChanged;
            status.RawDamageChanged -= OnPlayerStatusChanged;
            status.ElementalDamageChanged -= OnPlayerStatusChanged;
        }

        foreach (IAbnormality abnormality in _abnormalities.Keys)
            abnormality.OnTimerUpdate -= OnAbnormalityTimerUpdate;
    }

    private void RefreshPlayer(Action<GameSessionSnapshot> mutation)
    {
        if (_context is null)
            return;

        _snapshot.ExecuteLocked(snapshot =>
        {
            if (snapshot.Player is null)
                return;

            mutation(snapshot);
            snapshot.MarkDirty("player");
        });
    }

    private void OnPlayerLogin(object? sender, EventArgs e) => Safe(() =>
    {
        if (_context is null)
            return;

        RefreshPlayer(snapshot => EntityMapper.UpdatePlayer(_context.Game.Player, snapshot.Player!));

        _snapshot.ExecuteLocked(snapshot => snapshot.PendingEvents.Enqueue(new
        {
            type = "event",
            @event = "player.login",
            data = new { name = _context.Game.Player.Name }
        }));
    }, "player login");

    private void OnPlayerLogout(object? sender, EventArgs e) => Safe(() =>
        _snapshot.ExecuteLocked(snapshot => snapshot.PendingEvents.Enqueue(new
        {
            type = "event",
            @event = "player.logout",
            data = new { }
        })), "player logout");

    private void OnPlayerDeath(object? sender, EventArgs e) => Safe(() =>
        _snapshot.ExecuteLocked(snapshot => snapshot.PendingEvents.Enqueue(new
        {
            type = "event",
            @event = "player.death",
            data = new { }
        })), "player death");

    private void OnPlayerStageUpdate(object? sender, EventArgs e) => Safe(() =>
    {
        if (_context is null)
            return;

        RefreshPlayer(snapshot =>
        {
            snapshot.Player!.StageId = _context.Game.Player.StageId;
            snapshot.Player.InHuntingZone = _context.Game.Player.InHuntingZone;
        });
    }, "player stage update");

    private void OnPlayerLevelChange(object? sender, LevelChangeEventArgs e) => Safe(() =>
    {
        if (_context is null)
            return;

        RefreshPlayer(snapshot =>
        {
            snapshot.Player!.HighRank = _context.Game.Player.HighRank;
            snapshot.Player.MasterRank = _context.Game.Player.MasterRank;
        });
    }, "player level change");

    private void OnPlayerPositionChange(object? sender, SimpleValueChangeEventArgs<System.Numerics.Vector3> e) => Safe(() =>
    {
        if (_context is null)
            return;

        RefreshPlayer(snapshot => snapshot.Player!.Position = EntityMapper.MapPosition(_context.Game.Player.Position));
    }, "player position change");

    private void RefreshVitals() => Safe(() =>
    {
        if (_context is null)
            return;

        RefreshPlayer(snapshot => EntityMapper.UpdateVitals(_context.Game.Player, snapshot.Player!));
    }, "vitals change");

    private void OnVitalsChange(object? sender, HealthChangeEventArgs e) => RefreshVitals();

    private void OnStaminaChange(object? sender, StaminaChangeEventArgs e) => RefreshVitals();

    private void OnPlayerStatusChanged(object? sender, SimpleValueChangeEventArgs<double> e) => Safe(() =>
    {
        if (_context is null)
            return;

        RefreshPlayer(snapshot => EntityMapper.UpdateStatus(_context.Game.Player, snapshot.Player!));
    }, "player status change");

    private void OnAbnormalityStart(object? sender, IAbnormality abnormality) => Safe(() =>
    {
        abnormality.OnTimerUpdate += OnAbnormalityTimerUpdate;

        RefreshPlayer(snapshot =>
        {
            var dto = EntityMapper.MapAbnormality(abnormality);
            _abnormalities[abnormality] = dto;
            snapshot.Player!.Abnormalities.Add(dto);
        });
    }, "abnormality start");

    private void OnAbnormalityEnd(object? sender, IAbnormality abnormality) => Safe(() =>
    {
        abnormality.OnTimerUpdate -= OnAbnormalityTimerUpdate;

        RefreshPlayer(snapshot =>
        {
            if (_abnormalities.Remove(abnormality, out AbnormalityDto? dto))
                _ = snapshot.Player!.Abnormalities.Remove(dto);
        });
    }, "abnormality end");

    private void OnAbnormalityTimerUpdate(object? sender, IAbnormality abnormality) => Safe(() =>
        RefreshPlayer(snapshot =>
        {
            if (_abnormalities.TryGetValue(abnormality, out AbnormalityDto? dto))
                EntityMapper.UpdateAbnormality(abnormality, dto);
        }), "abnormality timer update");
    #endregion

    #region Weapon events
    private void HookWeapon(IWeapon weapon)
    {
        _hookedWeapon = weapon;

        if (weapon is IMeleeWeapon meleeWeapon)
        {
            meleeWeapon.OnSharpnessChange += OnWeaponStateChange;
            meleeWeapon.OnSharpnessLevelChange += OnWeaponStateChange;
        }

        switch (weapon)
        {
            case ILongSword longSword:
                longSword.OnSpiritLevelChange += OnWeaponStateChange;
                longSword.OnSpiritBuildUpChange += OnWeaponStateChange;
                longSword.OnSpiritRegenerationChange += OnWeaponStateChange;
                longSword.OnSpiritLevelTimerChange += OnWeaponStateChange;
                break;
            case IChargeBlade chargeBlade:
                chargeBlade.OnShieldBuffTimerChange += OnWeaponStateChange;
                chargeBlade.OnSwordBuffTimerChange += OnWeaponStateChange;
                chargeBlade.OnAxeBuffTimerChange += OnWeaponStateChange;
                chargeBlade.OnChargeBuildUpChange += OnWeaponStateChange;
                chargeBlade.OnPhialsChange += OnWeaponStateChange;
                break;
            case IDualBlades dualBlades:
                dualBlades.OnDemonModeStateChange += OnWeaponStateChange;
                dualBlades.OnArchDemonModeStateChange += OnWeaponStateChange;
                dualBlades.OnDemonBuildUpChange += OnWeaponStateChange;
                dualBlades.OnPiercingBindTimerChange += OnWeaponStateChange;
                break;
            case IInsectGlaive insectGlaive:
                insectGlaive.OnPrimaryExtractChange += OnWeaponStateChange;
                insectGlaive.OnSecondaryExtractChange += OnWeaponStateChange;
                insectGlaive.OnAttackTimerChange += OnWeaponStateChange;
                insectGlaive.OnSpeedTimerChange += OnWeaponStateChange;
                insectGlaive.OnDefenseTimerChange += OnWeaponStateChange;
                insectGlaive.OnKinsectStaminaChange += OnWeaponStateChange;
                insectGlaive.OnChargeChange += OnWeaponStateChange;
                break;
            case ISwitchAxe switchAxe:
                switchAxe.OnBuildUpChange += OnWeaponStateChange;
                switchAxe.OnChargeTimerChange += OnWeaponStateChange;
                switchAxe.OnChargeBuildUpChange += OnWeaponStateChange;
                switchAxe.OnSlamBuffTimerChange += OnWeaponStateChange;
                break;
        }
    }

    private void UnhookWeapon(IWeapon weapon)
    {
        if (weapon is IMeleeWeapon meleeWeapon)
        {
            meleeWeapon.OnSharpnessChange -= OnWeaponStateChange;
            meleeWeapon.OnSharpnessLevelChange -= OnWeaponStateChange;
        }

        switch (weapon)
        {
            case ILongSword longSword:
                longSword.OnSpiritLevelChange -= OnWeaponStateChange;
                longSword.OnSpiritBuildUpChange -= OnWeaponStateChange;
                longSword.OnSpiritRegenerationChange -= OnWeaponStateChange;
                longSword.OnSpiritLevelTimerChange -= OnWeaponStateChange;
                break;
            case IChargeBlade chargeBlade:
                chargeBlade.OnShieldBuffTimerChange -= OnWeaponStateChange;
                chargeBlade.OnSwordBuffTimerChange -= OnWeaponStateChange;
                chargeBlade.OnAxeBuffTimerChange -= OnWeaponStateChange;
                chargeBlade.OnChargeBuildUpChange -= OnWeaponStateChange;
                chargeBlade.OnPhialsChange -= OnWeaponStateChange;
                break;
            case IDualBlades dualBlades:
                dualBlades.OnDemonModeStateChange -= OnWeaponStateChange;
                dualBlades.OnArchDemonModeStateChange -= OnWeaponStateChange;
                dualBlades.OnDemonBuildUpChange -= OnWeaponStateChange;
                dualBlades.OnPiercingBindTimerChange -= OnWeaponStateChange;
                break;
            case IInsectGlaive insectGlaive:
                insectGlaive.OnPrimaryExtractChange -= OnWeaponStateChange;
                insectGlaive.OnSecondaryExtractChange -= OnWeaponStateChange;
                insectGlaive.OnAttackTimerChange -= OnWeaponStateChange;
                insectGlaive.OnSpeedTimerChange -= OnWeaponStateChange;
                insectGlaive.OnDefenseTimerChange -= OnWeaponStateChange;
                insectGlaive.OnKinsectStaminaChange -= OnWeaponStateChange;
                insectGlaive.OnChargeChange -= OnWeaponStateChange;
                break;
            case ISwitchAxe switchAxe:
                switchAxe.OnBuildUpChange -= OnWeaponStateChange;
                switchAxe.OnChargeTimerChange -= OnWeaponStateChange;
                switchAxe.OnChargeBuildUpChange -= OnWeaponStateChange;
                switchAxe.OnSlamBuffTimerChange -= OnWeaponStateChange;
                break;
        }
    }

    private void OnWeaponChange(object? sender, WeaponChangeEventArgs e) => Safe(() =>
    {
        if (_context is null)
            return;

        if (_hookedWeapon is { } oldWeapon)
            UnhookWeapon(oldWeapon);

        IWeapon weapon = _context.Game.Player.Weapon;
        HookWeapon(weapon);

        RefreshPlayer(snapshot => EntityMapper.UpdateWeapon(weapon, snapshot.Player!.Weapon!));
    }, "weapon change");

    private void OnWeaponStateChange(object? sender, EventArgs e) => Safe(() =>
    {
        if (_context is null)
            return;

        RefreshPlayer(snapshot => EntityMapper.UpdateWeapon(_context.Game.Player.Weapon, snapshot.Player!.Weapon!));
    }, "weapon state change");
    #endregion

    #region Tools & wirebugs
    private void HookTools(IPlayer player)
    {
        IEnumerable<ISpecializedTool>? tools = player switch
        {
            MHWPlayer mhwPlayer => mhwPlayer.Tools.Cast<ISpecializedTool>(),
            MHWildsPlayer wildsPlayer => wildsPlayer.Tools,
            _ => null
        };

        if (tools is null)
            return;

        _snapshot.ExecuteLocked(snapshot =>
        {
            foreach ((ISpecializedTool tool, SpecializedToolDto dto) in tools.Zip(snapshot.Player?.Tools ?? Enumerable.Empty<SpecializedToolDto>()))
                _tools[tool] = dto;
        });

        foreach (ISpecializedTool tool in tools)
        {
            tool.OnChange += OnToolChange;
            tool.OnTimerUpdate += OnToolChange;
            tool.OnCooldownUpdate += OnToolChange;
        }
    }

    private void UnhookTools()
    {
        foreach (ISpecializedTool tool in _tools.Keys)
        {
            tool.OnChange -= OnToolChange;
            tool.OnTimerUpdate -= OnToolChange;
            tool.OnCooldownUpdate -= OnToolChange;
        }
    }

    private void OnToolChange(object? sender, ISpecializedTool tool) => Safe(() =>
        RefreshPlayer(snapshot =>
        {
            if (_tools.TryGetValue(tool, out SpecializedToolDto? dto))
                EntityMapper.UpdateTool(tool, dto);
        }), "tool change");

    private void HookWirebugs(IPlayer player)
    {
        if (player is not MHRPlayer risePlayer)
            return;

        _snapshot.ExecuteLocked(snapshot =>
        {
            foreach ((MHRWirebug wirebug, WirebugDto dto) in risePlayer.Wirebugs.Zip(snapshot.Player?.Wirebugs ?? Enumerable.Empty<WirebugDto>()))
                _wirebugs[wirebug] = dto;
        });

        foreach (MHRWirebug wirebug in risePlayer.Wirebugs)
        {
            wirebug.OnAvailableChange += OnWirebugChange;
            wirebug.OnTimerUpdate += OnWirebugChange;
            wirebug.OnCooldownUpdate += OnWirebugChange;
        }
    }

    private void UnhookWirebugs()
    {
        foreach (MHRWirebug wirebug in _wirebugs.Keys)
        {
            wirebug.OnAvailableChange -= OnWirebugChange;
            wirebug.OnTimerUpdate -= OnWirebugChange;
            wirebug.OnCooldownUpdate -= OnWirebugChange;
        }
    }

    private void OnWirebugChange(object? sender, MHRWirebug wirebug) => Safe(() =>
        RefreshPlayer(snapshot =>
        {
            if (_wirebugs.TryGetValue(wirebug, out WirebugDto? dto))
                EntityMapper.UpdateWirebug(wirebug, dto);
        }), "wirebug change");
    #endregion

    #region Monster events
    private void AddMonster(GameSessionSnapshot snapshot, IMonster monster)
    {
        MonsterDto dto = EntityMapper.MapMonster(monster, _nextMonsterIndex++);
        _monsters[monster] = dto;
        snapshot.Monsters.Add(dto);

        foreach ((IMonsterPart part, MonsterPartDto partDto) in monster.Parts.Zip(dto.Parts))
            _parts[part] = partDto;

        foreach ((IMonsterAilment ailment, MonsterAilmentDto ailmentDto) in monster.Ailments.Zip(dto.Ailments))
            _ailments[ailment] = ailmentDto;
    }

    private void HookMonster(IMonster monster)
    {
        monster.OnHealthChange += OnMonsterScalarsChange;
        monster.OnStaminaChange += OnMonsterScalarsChange;
        monster.OnCrownChange += OnMonsterScalarsChange;
        monster.OnEnrageStateChange += OnMonsterEnrageStateChange;
        monster.OnTargetChange += OnMonsterScalarsChange;
        monster.OnCaptureThresholdChange += OnMonsterScalarsChange;
        monster.OnWeaknessesChange += OnMonsterScalarsChange;
        monster.PositionChange += OnMonsterScalarsChange;
        monster.OnDeath += OnMonsterDeath;
        monster.OnCapture += OnMonsterCapture;
        monster.OnNewPartFound += OnNewPartFound;
        monster.OnNewAilmentFound += OnNewAilmentFound;

        foreach (IMonsterPart part in monster.Parts)
            HookPart(part);

        foreach (IMonsterAilment ailment in monster.Ailments)
            HookAilment(ailment);
    }

    private void UnhookMonster(IMonster monster)
    {
        monster.OnHealthChange -= OnMonsterScalarsChange;
        monster.OnStaminaChange -= OnMonsterScalarsChange;
        monster.OnCrownChange -= OnMonsterScalarsChange;
        monster.OnEnrageStateChange -= OnMonsterEnrageStateChange;
        monster.OnTargetChange -= OnMonsterScalarsChange;
        monster.OnCaptureThresholdChange -= OnMonsterScalarsChange;
        monster.OnWeaknessesChange -= OnMonsterScalarsChange;
        monster.PositionChange -= OnMonsterScalarsChange;
        monster.OnDeath -= OnMonsterDeath;
        monster.OnCapture -= OnMonsterCapture;
        monster.OnNewPartFound -= OnNewPartFound;
        monster.OnNewAilmentFound -= OnNewAilmentFound;

        foreach (IMonsterPart part in monster.Parts)
            UnhookPart(part);

        foreach (IMonsterAilment ailment in monster.Ailments)
            UnhookAilment(ailment);
    }

    private void HookPart(IMonsterPart part)
    {
        part.OnHealthUpdate += OnPartChange;
        part.OnFlinchUpdate += OnPartChange;
        part.OnSeverUpdate += OnPartChange;
        part.OnTenderizeUpdate += OnPartChange;
        part.OnBreakCountUpdate += OnPartChange;
        part.OnPartTypeChange += OnPartChange;
    }

    private void UnhookPart(IMonsterPart part)
    {
        part.OnHealthUpdate -= OnPartChange;
        part.OnFlinchUpdate -= OnPartChange;
        part.OnSeverUpdate -= OnPartChange;
        part.OnTenderizeUpdate -= OnPartChange;
        part.OnBreakCountUpdate -= OnPartChange;
        part.OnPartTypeChange -= OnPartChange;
    }

    private void HookAilment(IMonsterAilment ailment)
    {
        ailment.OnTimerUpdate += OnAilmentChange;
        ailment.OnBuildUpUpdate += OnAilmentChange;
        ailment.OnCounterUpdate += OnAilmentChange;
    }

    private void UnhookAilment(IMonsterAilment ailment)
    {
        ailment.OnTimerUpdate -= OnAilmentChange;
        ailment.OnBuildUpUpdate -= OnAilmentChange;
        ailment.OnCounterUpdate -= OnAilmentChange;
    }

    private void RefreshMonster(IMonster monster, Action<GameSessionSnapshot, MonsterDto> mutation)
    {
        _snapshot.ExecuteLocked(snapshot =>
        {
            if (!_monsters.TryGetValue(monster, out MonsterDto? dto))
                return;

            mutation(snapshot, dto);
            snapshot.MarkDirty("monsters");
        });
    }

    private void OnMonsterSpawn(object? sender, IMonster monster) => Safe(() =>
    {
        _snapshot.ExecuteLocked(snapshot =>
        {
            AddMonster(snapshot, monster);
            _monsters[monster].IsEngaged = monster == _engagedMonster;
            snapshot.MarkDirty("monsters");

            MonsterDto dto = _monsters[monster];
            snapshot.PendingEvents.Enqueue(new
            {
                type = "event",
                @event = "monster.spawn",
                data = new { index = dto.Index, id = dto.Id, name = dto.Name }
            });
        });

        HookMonster(monster);
    }, "monster spawn");

    private void OnMonsterDespawn(object? sender, IMonster monster) => Safe(() =>
    {
        UnhookMonster(monster);

        _snapshot.ExecuteLocked(snapshot =>
        {
            if (!_monsters.Remove(monster, out MonsterDto? dto))
                return;

            _ = snapshot.Monsters.Remove(dto);

            foreach (IMonsterPart part in monster.Parts)
                _parts.Remove(part);

            foreach (IMonsterAilment ailment in monster.Ailments)
                _ailments.Remove(ailment);

            snapshot.MarkDirty("monsters");
        });
    }, "monster despawn");

    private void HandleMonsterScalarsChange(object? sender) => Safe(() =>
    {
        if (sender is IMonster monster)
            RefreshMonster(monster, (_, dto) => EntityMapper.UpdateMonsterScalars(monster, dto));
    }, "monster scalars change");

    private void OnMonsterScalarsChange(object? sender, EventArgs e) => HandleMonsterScalarsChange(sender);

    private void OnMonsterScalarsChange(object? sender, IMonster monster) => HandleMonsterScalarsChange(sender);

    private void OnMonsterScalarsChange(object? sender, HunterPie.Core.Game.Enums.Element[] weaknesses) => HandleMonsterScalarsChange(sender);

    private void OnMonsterScalarsChange(object? sender, MonsterTargetEventArgs e) => HandleMonsterScalarsChange(sender);

    private void OnMonsterScalarsChange(object? sender, SimpleValueChangeEventArgs<System.Numerics.Vector3> e) => HandleMonsterScalarsChange(sender);

    private void OnMonsterEnrageStateChange(object? sender, EventArgs e) => Safe(() =>
    {
        if (sender is not IMonster monster)
            return;

        RefreshMonster(monster, (snapshot, dto) =>
        {
            EntityMapper.UpdateMonsterScalars(monster, dto);

            if (monster.IsEnraged)
                snapshot.PendingEvents.Enqueue(new
                {
                    type = "event",
                    @event = "monster.enrage",
                    data = new { index = dto.Index, id = dto.Id, name = dto.Name }
                });
        });
    }, "monster enrage state change");

    private void OnMonsterDeath(object? sender, EventArgs e) => Safe(() =>
    {
        if (sender is IMonster monster)
            RefreshMonster(monster, (snapshot, dto) =>
            {
                EntityMapper.UpdateMonsterScalars(monster, dto);
                snapshot.PendingEvents.Enqueue(new
                {
                    type = "event",
                    @event = "monster.death",
                    data = new { index = dto.Index, id = dto.Id, name = dto.Name }
                });
            });
    }, "monster death");

    private void OnMonsterCapture(object? sender, EventArgs e) => Safe(() =>
    {
        if (sender is IMonster monster)
            RefreshMonster(monster, (snapshot, dto) =>
            {
                EntityMapper.UpdateMonsterScalars(monster, dto);
                snapshot.PendingEvents.Enqueue(new
                {
                    type = "event",
                    @event = "monster.capture",
                    data = new { index = dto.Index, id = dto.Id, name = dto.Name }
                });
            });
    }, "monster capture");

    private void OnNewPartFound(object? sender, IMonsterPart part) => Safe(() =>
    {
        HookPart(part);

        if (sender is IMonster monster)
            RefreshMonster(monster, (_, dto) =>
            {
                var partDto = EntityMapper.MapMonsterPart(part);
                _parts[part] = partDto;
                dto.Parts.Add(partDto);
            });
    }, "new part found");

    private void OnNewAilmentFound(object? sender, IMonsterAilment ailment) => Safe(() =>
    {
        HookAilment(ailment);

        if (sender is IMonster monster)
            RefreshMonster(monster, (_, dto) =>
            {
                var ailmentDto = EntityMapper.MapMonsterAilment(ailment);
                _ailments[ailment] = ailmentDto;
                dto.Ailments.Add(ailmentDto);
            });
    }, "new ailment found");

    private void OnPartChange(object? sender, IMonsterPart part) => Safe(() =>
        _snapshot.ExecuteLocked(snapshot =>
        {
            if (_parts.TryGetValue(part, out MonsterPartDto? dto))
            {
                EntityMapper.UpdateMonsterPart(part, dto);
                snapshot.MarkDirty("monsters");
            }
        }), "part change");

    private void OnAilmentChange(object? sender, IMonsterAilment ailment) => Safe(() =>
        _snapshot.ExecuteLocked(snapshot =>
        {
            if (_ailments.TryGetValue(ailment, out MonsterAilmentDto? dto))
            {
                EntityMapper.UpdateMonsterAilment(ailment, dto);
                snapshot.MarkDirty("monsters");
            }
        }), "ailment change");
    #endregion

    #region Party events
    private void HookParty(IParty party)
    {
        party.OnMemberJoin += OnPartyMemberJoin;
        party.OnMemberLeave += OnPartyMemberLeave;

        _snapshot.ExecuteLocked(_ =>
        {
            if (_snapshot.Party is null)
                return;

            foreach ((IPartyMember member, PartyMemberDto dto) in party.Members.Zip(_snapshot.Party.Members))
                _partyMembers[member] = dto;
        });

        foreach (IPartyMember member in party.Members)
            HookPartyMember(member);
    }

    private void UnhookParty(IParty party)
    {
        party.OnMemberJoin -= OnPartyMemberJoin;
        party.OnMemberLeave -= OnPartyMemberLeave;

        foreach (IPartyMember member in _partyMembers.Keys)
            UnhookPartyMember(member);
    }

    private void HookPartyMember(IPartyMember member)
    {
        member.OnDamageDealt += OnPartyMemberChange;
        member.OnWeaponChange += OnPartyMemberChange;
    }

    private void UnhookPartyMember(IPartyMember member)
    {
        member.OnDamageDealt -= OnPartyMemberChange;
        member.OnWeaponChange -= OnPartyMemberChange;
    }

    private void OnPartyMemberJoin(object? sender, IPartyMember member) => Safe(() =>
    {
        HookPartyMember(member);

        _snapshot.ExecuteLocked(snapshot =>
        {
            if (snapshot.Party is null || _context is null)
                return;

            var dto = EntityMapper.MapPartyMember(member);
            _partyMembers[member] = dto;
            snapshot.Party.Members.Add(dto);
            snapshot.Party.Size = _context.Game.Player.Party.Size;
            snapshot.MarkDirty("party");

            snapshot.PendingEvents.Enqueue(new
            {
                type = "event",
                @event = "party.member.join",
                data = new { name = dto.Name, slot = dto.Slot }
            });
        });
    }, "party member join");

    private void OnPartyMemberLeave(object? sender, IPartyMember member) => Safe(() =>
    {
        UnhookPartyMember(member);

        _snapshot.ExecuteLocked(snapshot =>
        {
            if (snapshot.Party is null || _context is null)
                return;

            if (_partyMembers.Remove(member, out PartyMemberDto? dto))
            {
                _ = snapshot.Party.Members.Remove(dto);
                snapshot.Party.Size = _context.Game.Player.Party.Size;
                snapshot.MarkDirty("party");

                snapshot.PendingEvents.Enqueue(new
                {
                    type = "event",
                    @event = "party.member.leave",
                    data = new { name = dto.Name, slot = dto.Slot }
                });
            }
        });
    }, "party member leave");

    private void OnPartyMemberChange(object? sender, IPartyMember member) => Safe(() =>
        _snapshot.ExecuteLocked(snapshot =>
        {
            if (_partyMembers.TryGetValue(member, out PartyMemberDto? dto))
            {
                EntityMapper.UpdatePartyMember(member, dto);
                snapshot.MarkDirty("party");
            }
        }), "party member change");
    #endregion

    #region Quest events
    private void HookQuest(IQuest quest)
    {
        _hookedQuest = quest;
        quest.OnQuestStatusChange += OnQuestChange;
        quest.OnDeathCounterChange += OnQuestChange;
        quest.OnTimeLeftChange += OnQuestChange;
    }

    private void UnhookQuest(IQuest quest)
    {
        quest.OnQuestStatusChange -= OnQuestChange;
        quest.OnDeathCounterChange -= OnQuestChange;
        quest.OnTimeLeftChange -= OnQuestChange;
        _hookedQuest = null;
    }

    private void OnQuestStart(object? sender, IQuest quest) => Safe(() =>
    {
        HookQuest(quest);

        _snapshot.ExecuteLocked(snapshot =>
        {
            snapshot.Quest = EntityMapper.MapQuest(quest);
            snapshot.MarkDirty("quest");
            snapshot.PendingEvents.Enqueue(new
            {
                type = "event",
                @event = "quest.start",
                data = new { id = quest.Id, name = quest.Name }
            });
        });
    }, "quest start");

    private void OnQuestEnd(object? sender, QuestEndEventArgs e) => Safe(() =>
    {
        if (_hookedQuest is { } quest)
            UnhookQuest(quest);

        _snapshot.ExecuteLocked(snapshot =>
        {
            snapshot.Quest = null;
            snapshot.MarkDirty("quest");
            snapshot.PendingEvents.Enqueue(new
            {
                type = "event",
                @event = "quest.end",
                data = new { status = e.Status.ToString(), timeElapsedSeconds = e.TimeElapsed.TotalSeconds }
            });
        });
    }, "quest end");

    private void OnQuestChange(object? sender, EventArgs e) => Safe(() =>
    {
        if (_hookedQuest is null)
            return;

        IQuest quest = _hookedQuest;

        _snapshot.ExecuteLocked(snapshot =>
        {
            if (snapshot.Quest is { } dto)
                EntityMapper.UpdateQuest(quest, dto);
            else
                snapshot.Quest = EntityMapper.MapQuest(quest);

            snapshot.MarkDirty("quest");
        });
    }, "quest change");
    #endregion

    #region Chat events
    private void HookChat(IChat chat)
    {
        _hookedChat = chat;
        chat.OnNewChatMessage += OnNewChatMessage;
        chat.OnChatOpen += OnChatOpen;
    }

    private void UnhookChat(IChat chat)
    {
        chat.OnNewChatMessage -= OnNewChatMessage;
        chat.OnChatOpen -= OnChatOpen;
        _hookedChat = null;
    }

    private void OnNewChatMessage(object? sender, IChatMessage message) => Safe(() =>
        _snapshot.ExecuteLocked(snapshot =>
        {
            ChatMessageDto dto = EntityMapper.MapChatMessage(message);
            snapshot.Chat.Messages.Add(dto);

            if (snapshot.Chat.Messages.Count > GameSessionSnapshot.CHAT_HISTORY_LIMIT)
                snapshot.Chat.Messages.RemoveRange(0, snapshot.Chat.Messages.Count - GameSessionSnapshot.CHAT_HISTORY_LIMIT);

            snapshot.MarkDirty("chat");
            snapshot.PendingEvents.Enqueue(new
            {
                type = "event",
                @event = "chat.message",
                data = dto
            });
        }), "new chat message");

    private void OnChatOpen(object? sender, IChat chat) => Safe(() =>
        _snapshot.ExecuteLocked(snapshot =>
        {
            snapshot.Chat.IsOpen = chat.IsChatOpen;
            snapshot.MarkDirty("chat");
        }), "chat open");
    #endregion

    public void Dispose()
    {
        Safe(() =>
        {
            if (_context is null)
                return;

            UnhookGame(_context);
            UnhookPlayer(_context.Game.Player);
            UnhookParty(_context.Game.Player.Party);

            if (_hookedWeapon is { } weapon)
                UnhookWeapon(weapon);

            foreach (IMonster monster in _monsters.Keys.ToArray())
                UnhookMonster(monster);

            if (_hookedQuest is { } quest)
                UnhookQuest(quest);

            if (_hookedChat is { } chat)
                UnhookChat(chat);

            UnhookTools();
            UnhookWirebugs();

            if (_targetDetectionService is not null)
            {
                _targetDetectionService.OnTargetChanged -= OnEngagedTargetChanged;
                _targetDetectionService.Dispose();
                _targetDetectionService = null;
            }

            _engagedMonster = null;

            _monsters.Clear();
            _parts.Clear();
            _ailments.Clear();
            _abnormalities.Clear();
            _partyMembers.Clear();
            _tools.Clear();
            _wirebugs.Clear();
            _hookedWeapon = null;
            _nextMonsterIndex = 0;
            _context = null;

            _snapshot.Reset();
            _snapshot.ExecuteLocked(snapshot => snapshot.PendingEvents.Enqueue(new
            {
                type = "event",
                @event = "session.end",
                data = new { }
            }));
        }, "session dispose");
    }

    private void Safe(Action action, string operation)
    {
        try
        {
            action();
        }
        catch (Exception err)
        {
            _logger.Error($"API tracker failed on {operation}: {err}");
        }
    }
}
