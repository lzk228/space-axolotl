﻿using Content.Shared.Examine;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Server.FlavorText
{
    public sealed class DetailedExaminableSystem : EntitySystem
    {
        [Dependency] private readonly ExamineSystemShared _examineSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<FlavorTextComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
        }

        private void OnGetExamineVerbs(EntityUid uid, FlavorTextComponent component, GetVerbsEvent<ExamineVerb> args)
        {
            // TODO: Hide if identity isn't visible (when identity is merged)
            var detailsRange = _examineSystem.IsInDetailsRange(args.User, uid);

            var verb = new ExamineVerb()
            {
                Act = () =>
                {
                    var markup = new FormattedMessage();
                    markup.AddMarkup(component.Content);
                    _examineSystem.SendExamineTooltip(args.User, uid, markup, false, false);
                },
                Text = Loc.GetString("flavortext-examinable-verb-text"),
                Category = VerbCategory.Examine,
                Disabled = !detailsRange,
                Message = Loc.GetString("flavortext-examinable-verb-disabled"),
                IconTexture = "/Textures/Interface/VerbIcons/examine.svg.192dpi.png"
            };

            args.Verbs.Add(verb);
        }
    }
}
