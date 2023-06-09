using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Roles;
using Content.Shared.Humanoid;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using static Content.Shared.Humanoid.HumanoidAppearanceState;

namespace Content.Shared.Zombies
{
    [DataDefinition, NetworkedComponent]
    public sealed class ZombieSettings
    {
        /// <summary>
        /// Fraction of damage subtracted when hitting another zombie
        /// </summary>
        [DataField("otherZombieDamageCoefficient"), ViewVariables]
        public float OtherZombieDamageCoefficient = 1.0f;

        /// <summary>
        /// Chance that this zombie be permanently killed (rolled once on crit->death transition)
        /// </summary>
        [DataField("permadeathChance"), ViewVariables(VVAccess.ReadWrite)]
        public float PermadeathChance = 0.50f;

        /// <summary>
        /// How many seconds it takes for a zombie to revive (min)
        /// </summary>
        [DataField("reviveTime"), ViewVariables(VVAccess.ReadWrite)]
        public float ReviveTime = 10;

        /// <summary>
        /// How many seconds it takes for a zombie to revive (max)
        /// </summary>
        [DataField("reviveTimeMax"), ViewVariables(VVAccess.ReadWrite)]
        public float ReviveTimeMax = 60;

        /// <summary>
        /// The baseline infection chance you have if you are completely nude
        /// </summary>
        [DataField("maxInfectionChance"), ViewVariables(VVAccess.ReadWrite)]
        public float MaxInfectionChance = 0.30f;

        /// <summary>
        /// The minimum infection chance possible. This is simply to prevent
        /// being invincible by bundling up.
        /// </summary>
        [DataField("minInfectionChance"), ViewVariables(VVAccess.ReadWrite)]
        public float MinInfectionChance = 0.05f;

        [DataField("movementSpeedDebuff"), ViewVariables(VVAccess.ReadWrite)]
        public float MovementSpeedDebuff = 0.70f;

        /// <summary>
        /// How long it takes our bite victims to turn in seconds (max).
        ///   Will roll 25% - 100% of this on bite.
        /// </summary>
        [DataField("infectionTurnTime"), ViewVariables(VVAccess.ReadWrite)]
        public float InfectionTurnTime = 480.0f;
        /// <summary>
        /// Minimum time a zombie victim will lie dead before rising as a zombie.
        /// </summary>
        [DataField("deadMinTurnTime"), ViewVariables(VVAccess.ReadWrite)]
        public float DeadMinTurnTime = 10.0f;

        /// <summary>
        /// The skin color of the
        /// </summary>
        [DataField("skinColor")]
        public Color SkinColor = new(0.45f, 0.51f, 0.29f);

        /// <summary>
        /// The eye color of the zombie
        /// </summary>
        [DataField("eyeColor")]
        public Color EyeColor = new(0.96f, 0.13f, 0.24f);

        /// <summary>
        /// The base layer to apply to any 'external' humanoid layers upon zombification.
        /// </summary>
        [DataField("baseLayerExternal")]
        public string BaseLayerExternal = "MobHumanoidMarkingMatchSkin";

        /// <summary>
        /// The attack arc of the zombie
        /// </summary>
        [DataField("attackArc", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
        public string AttackAnimation = "WeaponArcBite";

        /// <summary>
        /// The attack range of the zombie
        /// </summary>
        [DataField("meleeRange"), ViewVariables(VVAccess.ReadWrite)]
        public float MeleeRange = 1.5f;

        /// <summary>
        /// The role prototype of the zombie antag role
        /// </summary>
        [DataField("zombieRoleId", customTypeSerializer: typeof(PrototypeIdSerializer<AntagPrototype>))]
        public readonly string ZombieRoleId = "Zombie";

        [DataField("emoteId", customTypeSerializer: typeof(PrototypeIdSerializer<EmoteSoundsPrototype>))]
        public string? EmoteSoundsId = "Zombie";

        public EmoteSoundsPrototype? EmoteSounds;

        /// <summary>
        /// Healing each second
        /// </summary>
        [DataField("healingPerSec"), ViewVariables(VVAccess.ReadWrite)]
        public float HealingPerSec = 0.6f;

        /// <summary>
        /// How much the virus hurts you (base, scales rapidly)
        /// </summary>
        [DataField("virusDamage"), ViewVariables(VVAccess.ReadWrite)] public DamageSpecifier VirusDamage = new()
        {
            DamageDict = new ()
            {
                { "Blunt", 0.8 },
                { "Toxin", 0.2 },
            }
        };

        /// <summary>
        /// How much damage is inflicted per bite.
        /// </summary>
        [DataField("attackDamage"), ViewVariables(VVAccess.ReadWrite)] public DamageSpecifier AttackDamage = new()
        {
            DamageDict = new ()
            {
                { "Slash", 13 },
                { "Piercing", 7 },
                { "Structural", 10 },
            }
        };

        /// <summary>
        /// Infection warnings are shown as popups, times are in seconds.
        ///   -ve times shown to initial zombies (once timer counts from -ve to 0 the infection starts)
        ///   +ve warnings are in seconds after being bitten
        /// </summary>
        [DataField("infectionWarnings")]
        public Dictionary<int, string> InfectionWarnings = new()
        {
            {-45, "zombie-infection-warning"},
            {-30, "zombie-infection-warning"},
            {10, "zombie-infection-underway"},
            {25, "zombie-infection-underway"},
        };

    }

    [DataDefinition, NetworkedComponent]
    public sealed class ZombieFamily
    {
        /// <summary>
        /// Generation of this zombie (patient zero is 0, their victims are 1, etc)
        /// </summary>
        [DataField("generation"), ViewVariables(VVAccess.ReadOnly)]
        public int Generation = default!;

        /// <summary>
        /// If this zombie is not patient 0, this is the player who infected this zombie.
        /// </summary>
        [DataField("infector"), ViewVariables(VVAccess.ReadOnly)]
        public EntityUid Infector = EntityUid.Invalid;

        /// <summary>
        /// When created by a ZombieRuleComponent, this points to the entity which unleashed this zombie horde.
        /// </summary>
        [DataField("rules"), ViewVariables(VVAccess.ReadOnly)]
        public EntityUid Rules = EntityUid.Invalid;

    }

    [RegisterComponent, NetworkedComponent]
    public sealed class ZombieComponent : Component
    {
        /// <summary>
        /// Our settings (describes what the zombie can do)
        /// </summary>
        [DataField("settings"), ViewVariables(VVAccess.ReadOnly)]
        public ZombieSettings Settings = new();

        /// <summary>
        /// Settings for any victims we might have (if they are not the same as our settings)
        /// </summary>
        [DataField("victimSettings"), ViewVariables(VVAccess.ReadOnly)]
        public ZombieSettings? VictimSettings;

        /// <summary>
        /// Our family (describes how we became a zombie and where the rules are)
        /// </summary>
        [DataField("family"), ViewVariables(VVAccess.ReadOnly)]
        public ZombieFamily Family = new();

        /// <summary>
        /// The EntityName of the humanoid to restore in case of cloning
        /// </summary>
        [DataField("beforeZombifiedEntityName"), ViewVariables(VVAccess.ReadOnly)]
        public string BeforeZombifiedEntityName = String.Empty;

        /// <summary>
        /// The CustomBaseLayers of the humanoid to restore in case of cloning
        /// </summary>
        [DataField("beforeZombifiedCustomBaseLayers")]
        public Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo> BeforeZombifiedCustomBaseLayers = new ();

        /// <summary>
        /// The skin color of the humanoid to restore in case of cloning
        /// </summary>
        [DataField("beforeZombifiedSkinColor")]
        public Color BeforeZombifiedSkinColor;
    }
}
