﻿using Content.Shared.GameObjects.Components.Botany;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Interfaces.GameObjects.Components;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.GameObjects.Components.Botany
{
    [UsedImplicitly]
    public class PlantHolderVisualizer : AppearanceVisualizer
    {
        public override void InitializeEntity(IEntity entity)
        {
            base.InitializeEntity(entity);

            var sprite = entity.GetComponent<ISpriteComponent>();

            sprite.LayerMapReserveBlank(PlantHolderLayers.Plant);
            sprite.LayerMapReserveBlank(PlantHolderLayers.HealthLight);
            sprite.LayerMapReserveBlank(PlantHolderLayers.WaterLight);
            sprite.LayerMapReserveBlank(PlantHolderLayers.NutritionLight);
            sprite.LayerMapReserveBlank(PlantHolderLayers.AlertLight);
            sprite.LayerMapReserveBlank(PlantHolderLayers.HarvestLight);

            sprite.LayerSetSprite(PlantHolderLayers.HealthLight,
                new SpriteSpecifier.Rsi(new ResourcePath("Constructible/Hydroponics/hydro_tools.rsi"), "over_lowhealth3"));
            sprite.LayerSetSprite(PlantHolderLayers.WaterLight,
                new SpriteSpecifier.Rsi(new ResourcePath("Constructible/Hydroponics/hydro_tools.rsi"), "over_lowwater3"));
            sprite.LayerSetSprite(PlantHolderLayers.NutritionLight,
                new SpriteSpecifier.Rsi(new ResourcePath("Constructible/Hydroponics/hydro_tools.rsi"), "over_lownutri3"));
            sprite.LayerSetSprite(PlantHolderLayers.AlertLight,
                new SpriteSpecifier.Rsi(new ResourcePath("Constructible/Hydroponics/hydro_tools.rsi"), "over_alert3"));
            sprite.LayerSetSprite(PlantHolderLayers.HarvestLight,
                new SpriteSpecifier.Rsi(new ResourcePath("Constructible/Hydroponics/hydro_tools.rsi"), "over_harvest3"));

            sprite.LayerSetVisible(PlantHolderLayers.Plant, false);
            sprite.LayerSetVisible(PlantHolderLayers.HealthLight, false);
            sprite.LayerSetVisible(PlantHolderLayers.WaterLight, false);
            sprite.LayerSetVisible(PlantHolderLayers.NutritionLight, false);
            sprite.LayerSetVisible(PlantHolderLayers.AlertLight, false);
            sprite.LayerSetVisible(PlantHolderLayers.HarvestLight, false);
        }

        public override void OnChangeData(AppearanceComponent component)
        {
            base.OnChangeData(component);

            var sprite = component.Owner.GetComponent<ISpriteComponent>();

            if (component.TryGetData<SpriteSpecifier>(PlantHolderVisuals.Plant, out var specifier))
            {
                var valid = !specifier.Equals(SpriteSpecifier.Invalid);
                sprite.LayerSetVisible(PlantHolderLayers.Plant, valid);
                if(valid)
                    sprite.LayerSetSprite(PlantHolderLayers.Plant, specifier);
            }

            if (component.TryGetData<bool>(PlantHolderVisuals.HealthLight, out var health))
            {
                sprite.LayerSetVisible(PlantHolderLayers.HealthLight, health);
            }

            if (component.TryGetData<bool>(PlantHolderVisuals.WaterLight, out var water))
            {
                sprite.LayerSetVisible(PlantHolderLayers.WaterLight, water);
            }

            if (component.TryGetData<bool>(PlantHolderVisuals.NutritionLight, out var nutrition))
            {
                sprite.LayerSetVisible(PlantHolderLayers.NutritionLight, nutrition);
            }

            if (component.TryGetData<bool>(PlantHolderVisuals.AlertLight, out var alert))
            {
                sprite.LayerSetVisible(PlantHolderLayers.AlertLight, alert);
            }

            if (component.TryGetData<bool>(PlantHolderVisuals.HarvestLight, out var harvest))
            {
                sprite.LayerSetVisible(PlantHolderLayers.HarvestLight, harvest);
            }
        }
    }

    public enum PlantHolderLayers
    {
        Plant,
        HealthLight,
        WaterLight,
        NutritionLight,
        AlertLight,
        HarvestLight,
    }
}
