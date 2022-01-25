using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Server.GuideGenerator;

public class ChemistryJsonGenerator
{
    public static void PublishJson(StreamWriter file)
    {
        var prototype = IoCManager.Resolve<IPrototypeManager>();
        var prototypes =
            prototype
                .EnumeratePrototypes<ReagentPrototype>()
                .Where(x => !x.Abstract)
                .Select(x => new ReagentEntry(x))
                .ToDictionary(x => x.Id, x => x);

        var reactions =
            prototype
                .EnumeratePrototypes<ReactionPrototype>()
                .Where(x => x.Products.Count != 0);

        foreach (var reaction in reactions)
        {
            foreach (var product in reaction.Products.Keys)
            {
                prototypes[product].Recipes.Add(reaction.ID);
            }
        }

        var serializeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters =
            {
                new UniversalJsonConverter<ReagentEffect>(),
                new UniversalJsonConverter<ReagentEffectCondition>(),
                new UniversalJsonConverter<ReagentEffectsEntry>(),
                new UniversalJsonConverter<DamageSpecifier>(),
                new FixedPointJsonConverter()
            }
        };

        file.Write(JsonSerializer.Serialize(prototypes, serializeOptions));
    }

    public class FixedPointJsonConverter : JsonConverter<FixedPoint2>
    {
        public override void Write(Utf8JsonWriter writer, FixedPoint2 value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value.Float());
        }

        public override FixedPoint2 Read(ref Utf8JsonReader reader, Type objectType, JsonSerializerOptions options)
        {
            // Throwing a NotSupportedException here allows the error
            // message to provide path information.
            throw new NotSupportedException();
        }
    }
}
