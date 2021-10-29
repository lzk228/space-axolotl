﻿using System.Collections.Generic;
using Content.Shared.Administration.Events;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Client.Administration
{
    internal class AdminNameOverlay : Overlay
    {
        private readonly AdminSystem _system;
        private readonly IEntityManager _entityManager;
        private readonly IEyeManager _eyeManager;
        private readonly IEntityLookup _entityLookup;
        private IReadOnlyList<PlayerListChangedEvent.PlayerInfo>? _playerInfos;
        private readonly Font _font;

        public AdminNameOverlay(AdminSystem system, IEntityManager entityManager, IEyeManager eyeManager, IResourceCache resourceCache, IEntityLookup entityLookup)
        {
            _system = system;
            _entityManager = entityManager;
            _eyeManager = eyeManager;
            _entityLookup = entityLookup;
            ZIndex = 200;
            _font = new VectorFont(resourceCache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 10);
        }

        public override OverlaySpace Space => OverlaySpace.ScreenSpace;

        public void UpdatePlayerInfo(List<PlayerListChangedEvent.PlayerInfo> playerInfos)
        {
            _playerInfos = playerInfos;
        }

        protected override void Draw(in OverlayDrawArgs args)
        {
            if (_playerInfos == null)
            {
                return;
            }

            var viewport = _eyeManager.GetWorldViewport();

            foreach (var playerInfo in _playerInfos)
            {
                // Otherwise the entity can not exist yet
                if (!_entityManager.TryGetEntity(playerInfo.EntityUid, out var entity))
                {
                    continue;
                }

                // if not on the same map, continue
                if (entity.Transform.MapID != _eyeManager.CurrentMap)
                {
                    continue;
                }

                var aabb = _entityLookup.GetWorldAabbFromEntity(entity);

                // if not on screen, continue
                if (!aabb.Intersects(in viewport))
                {
                    continue;
                }

                var lineoffset = new Vector2(0, 11f);
                var screenCoordinates = _eyeManager.WorldToScreen(aabb.TopRight + (0, -0.1f));
                if (playerInfo.Antag)
                {
                    args.ScreenHandle.DrawString(_font, screenCoordinates + (lineoffset * 2), "ANTAG", Color.OrangeRed);
                }
                args.ScreenHandle.DrawString(_font, screenCoordinates+(lineoffset), playerInfo.Username, Color.Yellow);
                args.ScreenHandle.DrawString(_font, screenCoordinates, playerInfo.CharacterName, Color.Aquamarine);
            }
        }
    }
}
