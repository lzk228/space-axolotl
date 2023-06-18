using System.Text;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.MedicalScanner;
using Content.Shared.Mobs.Components;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.HealthAnalyzer.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class HealthAnalyzerWindow : DefaultWindow
    {
        private readonly IEntityManager _entityManager;
        private readonly SpriteSystem _spriteSystem;

        private bool _isSettledWidth = false;

        public HealthAnalyzerWindow()
        {
            RobustXamlLoader.Load(this);

            _entityManager = IoCManager.Resolve<IEntityManager>();
            _spriteSystem = _entityManager.System<SpriteSystem>();

            RootContainer.OnResized += OnRootResized;
        }

        public void Populate(HealthAnalyzerScannedUserMessage msg)
        {
            NoPatientDataText.Visible = true;
            GroupsContainer.RemoveAllChildren();

            if (msg.TargetEntity == null
                || !_entityManager.TryGetComponent<DamageableComponent>(msg.TargetEntity, out var damageable))
            {
                return;
            }

            var entityName = "Unknown";
            if (msg.TargetEntity != null &&
                _entityManager.TryGetComponent<MetaDataComponent>(msg.TargetEntity.Value, out var metaData))
                entityName = Identity.Name(msg.TargetEntity.Value, _entityManager);

            NoPatientDataText.Visible = false;

            PatientName.Text = Loc.GetString(
                "health-analyzer-window-entity-health-text",
                ("entityName", entityName)
            );
            patientDamageAmount.Text = Loc.GetString(
                "health-analyzer-window-entity-damage-total-text",
                ("amount", damageable.TotalDamage)
            );

            HashSet<string> shownTypes = new();
            IReadOnlyDictionary<string, FixedPoint2> damagePerGroup = damageable.DamagePerGroup;
            IReadOnlyDictionary<string, FixedPoint2> damagePerType = damageable.Damage.DamageDict;

            var protos = IoCManager.Resolve<IPrototypeManager>();

            var groupsInRow = 2;

            var diagnosticGroupsCounter = groupsInRow;

            BoxContainer? diagnosticGroupRow = null;

            // Show the total damage and type breakdown for each damage group.
            foreach (var (damageGroupId, damageAmount) in damagePerGroup)
            {
                if ((diagnosticGroupsCounter) % groupsInRow == 0)
                {
                    diagnosticGroupRow = new BoxContainer
                    {
                        Margin = new Thickness(0, 5),
                        HorizontalExpand = true,
                        Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    };

                    GroupsContainer.AddChild(diagnosticGroupRow);
                }

                if (diagnosticGroupRow is not BoxContainer groupRow)
                    continue;

                var groupTitleText = $"\n{Loc.GetString(
                    "health-analyzer-window-damage-group-text",
                    ("damageGroup", Loc.GetString("health-analyzer-window-damage-group-" + damageGroupId)),
                    ("amount", damageAmount)
                )}";

                var groupContainer = new BoxContainer
                {
                    Margin = new Thickness(0, 0, 30, 0),
                    Align = BoxContainer.AlignMode.Begin,
                    Orientation = BoxContainer.LayoutOrientation.Vertical
                };

                groupContainer.AddChild(CreateDiagnosticGroupTitle(groupTitleText, damageGroupId, (int) damageAmount));
                groupContainer.AddChild(new PanelContainer { StyleClasses = { "LowDivider" } });

                // Show the damage for each type in that group.
                var group = protos.Index<DamageGroupPrototype>(damageGroupId);

                foreach (var type in group.DamageTypes)
                {
                    if (damagePerType.TryGetValue(type, out var typeAmount))
                    {
                        // If damage types are allowed to belong to more than one damage group,
                        // they may appear twice here. Mark them as duplicate.
                        if (!shownTypes.Contains(type))
                        {
                            shownTypes.Add(type);

                            var damageString = Loc.GetString(
                                "health-analyzer-window-damage-type-text",
                                ("damageType", Loc.GetString("health-analyzer-window-damage-type-" + type)),
                                ("amount", typeAmount)
                            );

                            groupContainer.AddChild(CreateDiagnosticItemLabel(damageString, (int) typeAmount == 0));
                        }
                    }
                }

                diagnosticGroupRow.AddChild(groupContainer);
                diagnosticGroupsCounter++;
            }

            SetHeight = 430;
        }

        private Texture GetTexture(string texture)
        {
            var sprite = new SpriteSpecifier.Rsi(new("/Textures/Objects/Devices/health_analyzer.rsi"), texture);
            return _spriteSystem.Frame0(sprite);
        }

        private static Label CreateDiagnosticItemLabel(string text, bool isThin = true)
        {
            var labelStyle = isThin ? "LabelSubText" : "";

            return new Label
            {
                Margin = new Thickness(2, 2),
                Text = text,
                StyleClasses = { labelStyle },
            };
        }

        private BoxContainer CreateDiagnosticGroupTitle(string text, string id, int damageAmount)
        {
            var rootContainer = new BoxContainer
            {
                VerticalAlignment = VAlignment.Bottom,
                Orientation = BoxContainer.LayoutOrientation.Horizontal
            };

            rootContainer.AddChild(new TextureRect
            {
                SetSize = new Vector2(30, 20),
                Texture = GetTexture(id.ToLower())
            });

            rootContainer.AddChild(CreateDiagnosticItemLabel(text, damageAmount == 0));

            return rootContainer;
        }

        private void OnRootResized()
        {
            if (_isSettledWidth)
                return;

            _isSettledWidth = true;

            if (RootContainer.Width > 500)
                SetWidth = 500;
            else
                SetWidth = RootContainer.Width + 30;
        }
    }
}
