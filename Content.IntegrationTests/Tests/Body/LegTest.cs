using System.Threading.Tasks;
using Content.Server.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems.Body;
using Content.Shared.Rotation;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Body
{
    [TestFixture]
    [TestOf(typeof(BodySystem))]
    public sealed class LegTest
    {
        private const string Prototypes = @"
- type: entity
  name: HumanBodyAndAppearanceDummy
  id: HumanBodyAndAppearanceDummy
  components:
  - type: Appearance
  - type: Body
    template: HumanoidTemplate
    preset: HumanPreset
  - type: StandingState
  - type: NeedsSupport
";

        [Test]
        public async Task RemoveLegsFallTest()
        {
            await using var pairTracker = await PoolManager.GetServerClient(new PoolSettings{NoClient = true, ExtraPrototypes = Prototypes});
            var server = pairTracker.Pair.Server;

            AppearanceComponent appearance = null;

            await server.WaitAssertion(() =>
            {
                var mapManager = IoCManager.Resolve<IMapManager>();

                var mapId = mapManager.CreateMap();

                var entityManager = IoCManager.Resolve<IEntityManager>();
                var human = entityManager.SpawnEntity("HumanBodyAndAppearanceDummy", new MapCoordinates(Vector2.Zero, mapId));

                Assert.That(entityManager.TryGetComponent(human, out SharedBodyComponent body));
                Assert.That(entityManager.TryGetComponent(human, out appearance));

                Assert.That(!appearance.TryGetData(RotationVisuals.RotationState, out RotationState _));

                var bodySys = EntitySystem.Get<SharedBodySystem>();

                var legs = bodySys.GetPartsOfType(human, BodyPartType.Leg, body);

                foreach (var leg in legs)
                {
                    bodySys.RemovePart(human, leg, body);
                }
            });

            await server.WaitAssertion(() =>
            {
                Assert.That(appearance.TryGetData(RotationVisuals.RotationState, out RotationState state));
                Assert.That(state, Is.EqualTo(RotationState.Horizontal));
            });
            await pairTracker.CleanReturnAsync();
        }
    }
}
