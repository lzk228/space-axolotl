using Content.Shared.Examine;
using Content.Shared.Labels.Components;
using Content.Shared.Renamer.EntitySystems;
using Robust.Shared.Utility;

namespace Content.Shared.Labels.EntitySystems;

public abstract partial class SharedLabelSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LabelComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<LabelComponent, RefreshNameModifiersEvent>(OnRefreshNameModifiers);
    }

    public virtual void Label(EntityUid uid, string? text, MetaDataComponent? metadata = null, LabelComponent? label = null){}

    private void OnExamine(EntityUid uid, LabelComponent? label, ExaminedEvent args)
    {
        if (!Resolve(uid, ref label))
            return;

        if (label.CurrentLabel == null)
            return;

        var message = new FormattedMessage();
        message.AddText(Loc.GetString("hand-labeler-has-label", ("label", label.CurrentLabel)));
        args.PushMessage(message);
    }

    private void OnRefreshNameModifiers(Entity<LabelComponent> entity, ref RefreshNameModifiersEvent args)
    {
        if (!string.IsNullOrEmpty(entity.Comp.CurrentLabel))
            args.AddPostfix($"({entity.Comp.CurrentLabel})");
    }
}
