using System.Linq;
using System.Numerics;
using HunterPie.Core.Game.Entity;
using HunterPie.Core.Game.Entity.Enemy;
using HunterPie.Core.Game.Entity.Game.Chat;
using HunterPie.Core.Game.Entity.Game.Quest;
using HunterPie.Core.Game.Entity.Party;
using HunterPie.Core.Game.Entity.Player;
using HunterPie.Core.Game.Entity.Player.Classes;
using HunterPie.Features.Api.Models;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Entity.Player;
using HunterPie.Integrations.Datasources.MonsterHunterWilds.Entity.Player;
using HunterPie.Integrations.Datasources.MonsterHunterWorld.Entity.Player;

namespace HunterPie.Features.Api.Session;

/// <summary>
/// Maps live game entities to snapshot DTOs. All methods are pure reads;
/// the caller is responsible for holding the snapshot lock.
/// </summary>
internal static class EntityMapper
{
    public static PositionDto MapPosition(Vector3 position) => new()
    {
        X = position.X,
        Y = position.Y,
        Z = position.Z
    };

    #region Player
    public static PlayerDto MapPlayer(IPlayer player)
    {
        var dto = new PlayerDto();
        UpdatePlayer(player, dto);
        return dto;
    }

    public static void UpdatePlayer(IPlayer player, PlayerDto dto)
    {
        dto.Name = player.Name;
        dto.HighRank = player.HighRank;
        dto.MasterRank = player.MasterRank;
        dto.StageId = player.StageId;
        dto.InHuntingZone = player.InHuntingZone;
        dto.Position = MapPosition(player.Position);

        UpdateVitals(player, dto);
        UpdateStatus(player, dto);

        var weaponDto = new WeaponDto();
        UpdateWeapon(player.Weapon, weaponDto);
        dto.Weapon = weaponDto;

        dto.Tools = player switch
        {
            MHWPlayer mhwPlayer => mhwPlayer.Tools.Select(tool => MapTool(tool)).ToList(),
            MHWildsPlayer wildsPlayer => wildsPlayer.Tools.Select(tool => MapTool(tool)).ToList(),
            _ => null
        };

        dto.Wirebugs = player is MHRPlayer risePlayer
            ? risePlayer.Wirebugs.Select(MapWirebug).ToList()
            : null;

        dto.Abnormalities = player.Abnormalities.Select(MapAbnormality).ToList();
    }

    public static void UpdateVitals(IPlayer player, PlayerDto dto)
    {
        dto.Health = new HealthDto
        {
            Current = player.Health.Current,
            Max = player.Health.Max,
            Heal = player.Health.Heal,
            Recoverable = player.Health.RecoverableHealth,
            MaxPossible = player.Health.MaxPossibleHealth
        };

        dto.Stamina = new StaminaDto
        {
            Current = player.Stamina.Current,
            Max = player.Stamina.Max,
            MaxPossible = player.Stamina.MaxPossibleStamina
        };
    }

    public static void UpdateStatus(IPlayer player, PlayerDto dto)
    {
        dto.Status = player.Status is { } status
            ? MapPlayerStatus(status)
            : null;
    }

    public static PlayerStatusDto MapPlayerStatus(IPlayerStatus status) => new()
    {
        Raw = status.RawDamage,
        Elemental = status.ElementalDamage,
        Affinity = status.Affinity
    };

    public static void UpdateWeapon(IWeapon weapon, WeaponDto dto)
    {
        dto.Id = weapon.Id.ToString();
        dto.Sharpness = null;
        dto.LongSword = null;
        dto.ChargeBlade = null;
        dto.DualBlades = null;
        dto.InsectGlaive = null;
        dto.SwitchAxe = null;

        if (weapon is IMeleeWeapon meleeWeapon)
            dto.Sharpness = new SharpnessDto
            {
                Level = meleeWeapon.Sharpness.ToString(),
                Current = meleeWeapon.CurrentSharpness,
                Max = meleeWeapon.MaxSharpness,
                Threshold = meleeWeapon.Threshold,
                Thresholds = meleeWeapon.SharpnessThresholds
            };

        switch (weapon)
        {
            case ILongSword longSword:
                dto.LongSword = new LongSwordDto
                {
                    SpiritLevel = longSword.SpiritLevel,
                    SpiritBuildUp = longSword.SpiritBuildUp,
                    MaxSpiritBuildUp = longSword.MaxSpiritBuildUp,
                    SpiritRegenerationTimer = longSword.SpiritRegenerationTimer,
                    MaxSpiritRegenerationTimer = longSword.MaxSpiritRegenerationTimer,
                    SpiritLevelTimer = longSword.SpiritLevelTimer,
                    MaxSpiritLevelTimer = longSword.MaxSpiritLevelTimer
                };
                break;

            case IChargeBlade chargeBlade:
                dto.ChargeBlade = new ChargeBladeDto
                {
                    ShieldBuff = chargeBlade.ShieldBuff,
                    SwordBuff = chargeBlade.SwordBuff,
                    AxeBuff = chargeBlade.AxeBuff,
                    ChargeBuildUp = chargeBlade.ChargeBuildUp,
                    MaxChargeBuildUp = chargeBlade.MaxChargeBuildUp,
                    Charge = chargeBlade.Charge.ToString(),
                    Phials = chargeBlade.Phials,
                    MaxPhials = chargeBlade.MaxPhials
                };
                break;

            case IDualBlades dualBlades:
                dto.DualBlades = new DualBladesDto
                {
                    IsDemonMode = dualBlades.IsDemonMode,
                    IsArchDemonMode = dualBlades.IsArchDemonMode,
                    DemonBuildUp = dualBlades.DemonBuildUp,
                    MaxDemonBuildUp = dualBlades.MaxDemonBuildUp,
                    PiercingBindTimer = dualBlades.PiercingBindTimer,
                    MaxPiercingBindTimer = dualBlades.MaxPiercingBindTimer
                };
                break;

            case IInsectGlaive insectGlaive:
                dto.InsectGlaive = new InsectGlaiveDto
                {
                    PrimaryExtract = insectGlaive.PrimaryExtract.ToString(),
                    SecondaryExtract = insectGlaive.SecondaryExtract.ToString(),
                    ChargeType = insectGlaive.ChargeType.ToString(),
                    AttackTimer = insectGlaive.AttackTimer,
                    SpeedTimer = insectGlaive.SpeedTimer,
                    DefenseTimer = insectGlaive.DefenseTimer,
                    KinsectStamina = insectGlaive.Stamina,
                    KinsectMaxStamina = insectGlaive.MaxStamina,
                    KinsectCharge = insectGlaive.Charge
                };
                break;

            case ISwitchAxe switchAxe:
                dto.SwitchAxe = new SwitchAxeDto
                {
                    BuildUp = switchAxe.BuildUp,
                    MaxBuildUp = switchAxe.MaxBuildUp,
                    LowBuildUp = switchAxe.LowBuildUp,
                    ChargeTimer = switchAxe.ChargeTimer,
                    MaxChargeTimer = switchAxe.MaxChargeTimer,
                    ChargeBuildUp = switchAxe.ChargeBuildUp,
                    MaxChargeBuildUp = switchAxe.MaxChargeBuildUp,
                    SlamBuffTimer = switchAxe.SlamBuffTimer,
                    MaxSlamBuffTimer = switchAxe.MaxSlamBuffTimer
                };
                break;
        }
    }

    public static AbnormalityDto MapAbnormality(IAbnormality abnormality) => new()
    {
        Id = abnormality.Id,
        Name = abnormality.Name,
        Icon = abnormality.Icon,
        Type = abnormality.Type.ToString(),
        Timer = abnormality.Timer,
        MaxTimer = abnormality.MaxTimer,
        IsInfinite = abnormality.IsInfinite,
        Level = abnormality.Level,
        IsBuildUp = abnormality.IsBuildup
    };

    public static void UpdateAbnormality(IAbnormality abnormality, AbnormalityDto dto)
    {
        dto.Timer = abnormality.Timer;
        dto.MaxTimer = abnormality.MaxTimer;
        dto.Level = abnormality.Level;
    }

    public static SpecializedToolDto MapTool(ISpecializedTool tool) => new()
    {
        Id = tool.Id.ToString(),
        Cooldown = tool.Cooldown,
        MaxCooldown = tool.MaxCooldown,
        Timer = tool.Timer,
        MaxTimer = tool.MaxTimer
    };

    public static void UpdateTool(ISpecializedTool tool, SpecializedToolDto dto)
    {
        dto.Id = tool.Id.ToString();
        dto.Cooldown = tool.Cooldown;
        dto.MaxCooldown = tool.MaxCooldown;
        dto.Timer = tool.Timer;
        dto.MaxTimer = tool.MaxTimer;
    }

    public static WirebugDto MapWirebug(MHRWirebug wirebug) => new()
    {
        IsAvailable = wirebug.IsAvailable,
        IsTemporary = wirebug.IsTemporary,
        Timer = wirebug.Timer,
        MaxTimer = wirebug.MaxTimer,
        Cooldown = wirebug.Cooldown,
        MaxCooldown = wirebug.MaxCooldown
    };

    public static void UpdateWirebug(MHRWirebug wirebug, WirebugDto dto)
    {
        dto.IsAvailable = wirebug.IsAvailable;
        dto.IsTemporary = wirebug.IsTemporary;
        dto.Timer = wirebug.Timer;
        dto.MaxTimer = wirebug.MaxTimer;
        dto.Cooldown = wirebug.Cooldown;
        dto.MaxCooldown = wirebug.MaxCooldown;
    }
    #endregion

    #region Monsters
    public static MonsterDto MapMonster(IMonster monster, int index)
    {
        var dto = new MonsterDto { Index = index };
        UpdateMonsterScalars(monster, dto);
        dto.Parts = monster.Parts.Select(MapMonsterPart).ToList();
        dto.Ailments = monster.Ailments.Select(MapMonsterAilment).ToList();
        return dto;
    }

    public static void UpdateMonsterScalars(IMonster monster, MonsterDto dto)
    {
        dto.Id = monster.Id;
        dto.Name = monster.Name;
        dto.Variant = monster.Variant.ToString();
        dto.Crown = monster.Crown.ToString();
        dto.Health = monster.Health;
        dto.MaxHealth = monster.MaxHealth;
        dto.Stamina = monster.Stamina;
        dto.MaxStamina = monster.MaxStamina;
        dto.IsEnraged = monster.IsEnraged;
        dto.CaptureThreshold = monster.CaptureThreshold;
        dto.Target = monster.Target.ToString();
        dto.ManualTarget = monster.ManualTarget.ToString();
        dto.Position = MapPosition(monster.Position);
        dto.Weaknesses = monster.Weaknesses.Select(weakness => weakness.ToString()).ToList();
        dto.Types = monster.Types.ToList();
    }

    public static MonsterPartDto MapMonsterPart(IMonsterPart part) => new()
    {
        Id = part.Id,
        Name = part.Definition.String,
        Type = part.Type.ToString(),
        Health = part.Health,
        MaxHealth = part.MaxHealth,
        Flinch = part.Flinch,
        MaxFlinch = part.MaxFlinch,
        Sever = part.Sever,
        MaxSever = part.MaxSever,
        Tenderize = part.Tenderize,
        MaxTenderize = part.MaxTenderize,
        BreakCount = part.Count
    };

    public static void UpdateMonsterPart(IMonsterPart part, MonsterPartDto dto)
    {
        dto.Type = part.Type.ToString();
        dto.Health = part.Health;
        dto.MaxHealth = part.MaxHealth;
        dto.Flinch = part.Flinch;
        dto.MaxFlinch = part.MaxFlinch;
        dto.Sever = part.Sever;
        dto.MaxSever = part.MaxSever;
        dto.Tenderize = part.Tenderize;
        dto.MaxTenderize = part.MaxTenderize;
        dto.BreakCount = part.Count;
    }

    public static MonsterAilmentDto MapMonsterAilment(IMonsterAilment ailment) => new()
    {
        Id = ailment.Id,
        Name = ailment.Definition.String,
        Counter = ailment.Counter,
        Timer = ailment.Timer,
        MaxTimer = ailment.MaxTimer,
        BuildUp = ailment.BuildUp,
        MaxBuildUp = ailment.MaxBuildUp
    };

    public static void UpdateMonsterAilment(IMonsterAilment ailment, MonsterAilmentDto dto)
    {
        dto.Counter = ailment.Counter;
        dto.Timer = ailment.Timer;
        dto.MaxTimer = ailment.MaxTimer;
        dto.BuildUp = ailment.BuildUp;
        dto.MaxBuildUp = ailment.MaxBuildUp;
    }
    #endregion

    #region Party
    public static PartyDto MapParty(IParty party)
    {
        var dto = new PartyDto();
        UpdateParty(party, dto);
        return dto;
    }

    public static void UpdateParty(IParty party, PartyDto dto)
    {
        dto.Size = party.Size;
        dto.MaxSize = party.MaxSize;
        dto.Members = party.Members.Select(MapPartyMember).ToList();
    }

    public static PartyMemberDto MapPartyMember(IPartyMember member) => new()
    {
        Name = member.Name,
        MasterRank = member.MasterRank,
        Damage = member.Damage,
        Weapon = member.Weapon.ToString(),
        Slot = member.Slot,
        IsMyself = member.IsMyself,
        Type = member.Type.ToString(),
        Status = member.Status is { } status ? MapPlayerStatus(status) : null
    };

    public static void UpdatePartyMember(IPartyMember member, PartyMemberDto dto)
    {
        dto.Name = member.Name;
        dto.MasterRank = member.MasterRank;
        dto.Damage = member.Damage;
        dto.Weapon = member.Weapon.ToString();
        dto.Slot = member.Slot;
        dto.IsMyself = member.IsMyself;
        dto.Type = member.Type.ToString();
        dto.Status = member.Status is { } status ? MapPlayerStatus(status) : null;
    }
    #endregion

    #region Quest & chat
    public static QuestDto MapQuest(IQuest quest) => new()
    {
        Id = quest.Id,
        Name = quest.Name,
        Type = quest.Type.ToString(),
        Status = quest.Status.ToString(),
        Deaths = quest.Deaths,
        MaxDeaths = quest.MaxDeaths,
        Level = quest.Level.ToString(),
        Stars = quest.Stars,
        TimeLeftSeconds = quest.TimeLeft.TotalSeconds
    };

    public static void UpdateQuest(IQuest quest, QuestDto dto)
    {
        dto.Id = quest.Id;
        dto.Name = quest.Name;
        dto.Type = quest.Type.ToString();
        dto.Status = quest.Status.ToString();
        dto.Deaths = quest.Deaths;
        dto.MaxDeaths = quest.MaxDeaths;
        dto.Level = quest.Level.ToString();
        dto.Stars = quest.Stars;
        dto.TimeLeftSeconds = quest.TimeLeft.TotalSeconds;
    }

    public static ChatMessageDto MapChatMessage(IChatMessage message) => new()
    {
        Message = message.Message,
        Author = message.Author,
        Type = message.Type.ToString(),
        PlayerSlot = message.PlayerSlot
    };
    #endregion
}
