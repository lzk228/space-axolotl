using Content.Shared.Input;
using Content.Shared.Movement.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Players;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Movement.Systems;

/// <summary>
/// Lets specific sessions scroll and set their zoom directly.
/// </summary>
public abstract class SharedContentEyeSystem : EntitySystem
{
    protected static readonly Vector2 MinZoom = new(MathF.Pow(1.5f, -6), MathF.Pow(1.5f, -6));
    protected static readonly Vector2 MaxZoom = new(MathF.Pow(1.5f, 6), MathF.Pow(1.5f, 6));

    protected ISawmill Sawmill = Logger.GetSawmill("ceye");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ContentEyeComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<ContentEyeComponent, ComponentHandleState>(OnHandleState);
        SubscribeLocalEvent<ContentEyeComponent, ComponentStartup>(OnContentEyeStartup);

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.ZoomIn,  new ScrollInputCmdHandler(true, this))
            .Bind(ContentKeyFunctions.ZoomOut, new ScrollInputCmdHandler(false, this))
            .Bind(ContentKeyFunctions.ResetZoom, new ResetZoomInputCmdHandler(this))
            .Register<SharedContentEyeSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<SharedContentEyeSystem>();
    }

    private void OnContentEyeStartup(EntityUid uid, ContentEyeComponent component, ComponentStartup args)
    {
        if (!TryComp<SharedEyeComponent>(uid, out var eyeComp))
            return;

        component.TargetZoom = eyeComp.Zoom;
        Dirty(component);
    }

    private void OnGetState(EntityUid uid, ContentEyeComponent component, ref ComponentGetState args)
    {
        args.State = new ContentEyeComponentState()
        {
            TargetZoom = component.TargetZoom,
        };
    }

    private void OnHandleState(EntityUid uid, ContentEyeComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not ContentEyeComponentState state)
            return;

        component.TargetZoom = state.TargetZoom;
    }

    protected void UpdateEye(EntityUid uid, ContentEyeComponent content, SharedEyeComponent eye, float frameTime)
    {
        var diff = content.TargetZoom - eye.Zoom;

        if (diff.LengthSquared < 0.00001f)
        {
            eye.Zoom = content.TargetZoom;
            Dirty(eye);
            RemComp<ActiveContentEyeComponent>(uid);
            return;
        }

        var change = diff * 10f * frameTime;

        eye.Zoom += change;
        Dirty(eye);
    }

    private bool CanZoom(EntityUid uid, ContentEyeComponent? component = null)
    {
        return Resolve(uid, ref component, false);
    }

    private void ResetZoom(EntityUid uid, ContentEyeComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.TargetZoom.Equals(Vector2.One))
            return;

        component.TargetZoom = Vector2.One;
        Dirty(component);
    }

    private void Zoom(EntityUid uid, bool zoomIn, ContentEyeComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var actual = component.TargetZoom;

        if (zoomIn)
        {
            actual /= 1.2f;
        }
        else
        {
            actual *= 1.2f;
        }

        actual = Vector2.ComponentMax(MinZoom, actual);
        actual = Vector2.ComponentMin(MaxZoom, actual);

        if (actual.Equals(component.TargetZoom))
            return;

        component.TargetZoom = actual;
        EnsureComp<ActiveContentEyeComponent>(uid);
        Dirty(component);
        Sawmill.Debug($"Set target zoom to {actual}");
    }

    [Serializable, NetSerializable]
    private sealed class ContentEyeComponentState : ComponentState
    {
        public Vector2 TargetZoom;
    }

    private sealed class ResetZoomInputCmdHandler : InputCmdHandler
    {
        private readonly SharedContentEyeSystem _system;

        public ResetZoomInputCmdHandler(SharedContentEyeSystem system)
        {
            _system = system;
        }

        public override bool HandleCmdMessage(ICommonSession? session, InputCmdMessage message)
        {
            ContentEyeComponent? component = null;

            if (message is not FullInputCmdMessage full || session?.AttachedEntity == null ||
                full.State != BoundKeyState.Down ||
                !_system.CanZoom(session.AttachedEntity.Value, component))
            {
                return false;
            }

            _system.ResetZoom(session.AttachedEntity.Value, component);
            return false;
        }
    }

    private sealed class ScrollInputCmdHandler : InputCmdHandler
    {
        private readonly bool _zoomIn;
        private readonly SharedContentEyeSystem _system;

        public ScrollInputCmdHandler(bool zoomIn, SharedContentEyeSystem system)
        {
            _zoomIn = zoomIn;
            _system = system;
        }

        public override bool HandleCmdMessage(ICommonSession? session, InputCmdMessage message)
        {
            ContentEyeComponent? component = null;

            if (message is not FullInputCmdMessage full || session?.AttachedEntity == null ||
                full.State != BoundKeyState.Down ||
                !_system.CanZoom(session.AttachedEntity.Value, component))
            {
                return false;
            }

            _system.Zoom(session.AttachedEntity.Value, _zoomIn, component);
            return false;
        }
    }
}
