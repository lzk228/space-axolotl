using Content.Shared.PlantAnalyzer;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.XAML;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using FancyWindow = Content.Client.UserInterface.Controls.FancyWindow;

namespace Content.Client.PlantAnalyzer.UI;

[GenerateTypedNameReferences]
public sealed partial class PlantAnalyzerWindow : FancyWindow
{
    private readonly IEntityManager _entityManager;
    private readonly SpriteSystem _spriteSystem;
    private readonly IPrototypeManager _prototypes;
    private readonly IResourceCache _cache;
    private readonly ButtonGroup _buttonGroup = new();

    public PlantAnalyzerWindow(PlantAnalyzerBoundUserInterface owner)
    {
        RobustXamlLoader.Load(this);

        var dependencies = IoCManager.Instance!;
        _entityManager = dependencies.Resolve<IEntityManager>();
        _spriteSystem = _entityManager.System<SpriteSystem>();
        _prototypes = dependencies.Resolve<IPrototypeManager>();
        _cache = dependencies.Resolve<IResourceCache>();

        OnButton.Group = _buttonGroup;
        OffButton.Group = _buttonGroup;

        OnButton.OnPressed += _ => owner.AdvPressed(true); //-> PABUI.cs
        OffButton.OnPressed += _ => owner.AdvPressed(false);
    }
    public void Populate(PlantAnalyzerScannedSeedPlantInformation msg)
    {
        var target = _entityManager.GetEntity(msg.TargetEntity);
        Title = Loc.GetString("plant-analyzer-interface-title");
        
        if (target == null)
        {
            NoData.Visible = true;
            return;
        }
        NoData.Visible = false;

        if (msg.ScanMode)   //switch display to the settings of the received msg
        {
            OnButton.ToggleMode = true;
            OnButton.Pressed = true;
        }
        else
        {
            OffButton.ToggleMode = true;
            OffButton.Pressed = true;
        }
        PlantName.Text = Loc.GetString("plant-analyzer-window-label-name-scanned-seed", ("seedName", msg.SeedName));
        if (msg.IsTray) PlantName.Text = Loc.GetString("plant-analyzer-window-label-name-scanned-plant", ("seedName", msg.SeedName));

        //Basics
        Yield.Text = Loc.GetString("plant-analyzer-plant-yield-text", ("seedYield", msg.SeedYield));
        Potency.Text = Loc.GetString("plant-analyzer-plant-potency-text", ("seedPotency", msg.SeedPotency));
        Repeat.Text = Loc.GetString("plant-analyzer-plant-harvest-text", ("plantHarvestType", msg.Repeat));
        Endurance.Text = Loc.GetString("plant-analyzer-plant-endurance-text", ("seedEndurance", msg.Endurance));
        Chemicals.Text = Loc.GetString("plant-analyzer-plant-chemistry-text", ("seedChem", msg.SeedChem));
        Gases.Text = Loc.GetString("plant-analyzer-plant-exude-text", ("exudeGases", msg.ExudeGases));
        Lifespan.Text = Loc.GetString("plant-analyzer-plant-lifespan-text", ("lifespan", msg.Lifespan));
        Maturation.Text = Loc.GetString("plant-analyzer-plant-maturation-text", ("maturation", msg.Maturation));
        GrowthStages.Text = Loc.GetString("plant-analyzer-plant-growthstages-text", ("growthStages", msg.GrowthStages));
        //Tolerances
        NutrientUsage.Text = Loc.GetString("plant-analyzer-tolerance-nutrientusage", ("nutrientUsage", msg.NutrientConsumption));
        WaterUsage.Text = Loc.GetString("plant-analyzer-tolerance-waterusage", ("waterUsage", msg.WaterConsumption));
        IdealHeat.Text = Loc.GetString("plant-analyzer-tolerance-idealheat", ("idealHeat", msg.IdealHeat));
        HeatTolerance.Text = Loc.GetString("plant-analyzer-tolerance-heattoler", ("heatTolerance", msg.HeatTolerance));
        IdealLight.Text = Loc.GetString("plant-analyzer-tolerance-ideallight", ("idealLight", msg.IdealLight));
        LightTolerance.Text = Loc.GetString("plant-analyzer-tolerance-lighttoler", ("lighttolerance", msg.LightTolerance));
        ToxinsTolerance.Text = Loc.GetString("plant-analyzer-tolerance-toxinstoler", ("toxinsTolerance", msg.ToxinsTolerance));
        LowPressureTolerance.Text = Loc.GetString("plant-analyzer-tolerance-lowpress", ("lowPressureTolerance", msg.LowPresssureTolerance));
        HighPressureTolerance.Text = Loc.GetString("plant-analyzer-tolerance-highpress", ("highPressureTolerance", msg.HighPressureTolerance));
        PestTolerance.Text = Loc.GetString("plant-analyzer-tolerance-pesttoler", ("pestTolerance", msg.PestTolerance));
        WeedTolerance.Text = Loc.GetString("plant-analyzer-tolerance-weedtoler", ("weedTolerance", msg.WeedTolerance));
        //Misc
        Traits.Text = Loc.GetString("plant-analyzer-plant-mutations-text", ("traits", msg.SeedMutations));
        PlantSpeciation.Text = Loc.GetString("plant-analyzer-plant-speciation-text", ("speciation", msg.PlantSpeciation));
        ExtraInfo.Text = "";
    }

}
