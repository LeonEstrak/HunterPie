namespace HunterPie.Features.Api.Models;

internal class SharpnessDto
{
    public string Level { get; set; } = string.Empty;
    public int Current { get; set; }
    public int Max { get; set; }
    public int Threshold { get; set; }
    public int[]? Thresholds { get; set; }
}

internal class LongSwordDto
{
    public int SpiritLevel { get; set; }
    public float SpiritBuildUp { get; set; }
    public float MaxSpiritBuildUp { get; set; }
    public float SpiritRegenerationTimer { get; set; }
    public float MaxSpiritRegenerationTimer { get; set; }
    public float SpiritLevelTimer { get; set; }
    public float MaxSpiritLevelTimer { get; set; }
}

internal class ChargeBladeDto
{
    public float ShieldBuff { get; set; }
    public float SwordBuff { get; set; }
    public float AxeBuff { get; set; }
    public float ChargeBuildUp { get; set; }
    public float MaxChargeBuildUp { get; set; }
    public string Charge { get; set; } = string.Empty;
    public int Phials { get; set; }
    public int MaxPhials { get; set; }
}

internal class DualBladesDto
{
    public bool IsDemonMode { get; set; }
    public bool IsArchDemonMode { get; set; }
    public float DemonBuildUp { get; set; }
    public float MaxDemonBuildUp { get; set; }
    public float PiercingBindTimer { get; set; }
    public float MaxPiercingBindTimer { get; set; }
}

internal class InsectGlaiveDto
{
    public string PrimaryExtract { get; set; } = string.Empty;
    public string SecondaryExtract { get; set; } = string.Empty;
    public string ChargeType { get; set; } = string.Empty;
    public float AttackTimer { get; set; }
    public float SpeedTimer { get; set; }
    public float DefenseTimer { get; set; }
    public float KinsectStamina { get; set; }
    public float KinsectMaxStamina { get; set; }
    public float KinsectCharge { get; set; }
}

internal class SwitchAxeDto
{
    public float BuildUp { get; set; }
    public float MaxBuildUp { get; set; }
    public float LowBuildUp { get; set; }
    public float ChargeTimer { get; set; }
    public float MaxChargeTimer { get; set; }
    public float ChargeBuildUp { get; set; }
    public float MaxChargeBuildUp { get; set; }
    public float SlamBuffTimer { get; set; }
    public float MaxSlamBuffTimer { get; set; }
}

/// <summary>
/// Player weapon state. <see cref="Id"/> is the weapon type name; the
/// class-specific sub-objects are only present for the matching weapon type.
/// </summary>
internal class WeaponDto
{
    public string Id { get; set; } = string.Empty;
    public SharpnessDto? Sharpness { get; set; }
    public LongSwordDto? LongSword { get; set; }
    public ChargeBladeDto? ChargeBlade { get; set; }
    public DualBladesDto? DualBlades { get; set; }
    public InsectGlaiveDto? InsectGlaive { get; set; }
    public SwitchAxeDto? SwitchAxe { get; set; }
}
