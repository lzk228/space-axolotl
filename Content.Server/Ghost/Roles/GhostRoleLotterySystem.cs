using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Server.Ghost.Components;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Ghost.Roles.UI;
using Content.Server.Players;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Ghost.Roles;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Enums;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Ghost.Roles;

internal sealed class PlayerLotteryData
{
    public readonly HashSet<string> GhostRoles = new();
    public readonly HashSet<uint> GhostRoleGroups = new();
}

internal record GhostRoleLotteryRecord
{
    public HashSet<GhostRoleComponent> ForTake = new();
    public HashSet<GhostRoleComponent> ForLottery = new();
}

public sealed class RequestAvailableLotteryItemsMessage
{
    public readonly List<GhostRoleComponent> GhostRoles = new();
    public readonly List<uint> GhostRoleGroups = new();
}

[UsedImplicitly]
public sealed class GhostRoleLotterySystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfgManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;

    private readonly Dictionary<IPlayerSession, GhostRolesEui> _openUis = new();
    private readonly Dictionary<IPlayerSession, PlayerLotteryData> _playerLotteryData = new ();

    private readonly Dictionary<string, GhostRoleLotteryRecord> _ghostRoleComponentLotteries = new();
    private readonly HashSet<uint> _roleGroupLotteries = new();

    private bool _needsUpdateGhostRoles = true;
    private bool _needsUpdateGhostRoleCount = true;

    public TimeSpan LotteryStartTime { get; private set; } = TimeSpan.Zero;
    public TimeSpan LotteryExpiresTime { get; private set; } = TimeSpan.Zero;

    public int AvailableRolesCount { get; private set; } = 0;

    public string[] AvailableRoles => new string[] { };


    private uint _nextIdentifier;
    /// <summary>
    ///     Identifier used for groups of ghost role components and role groups.
    /// </summary>
    public uint NextIdentifier => unchecked(_nextIdentifier++);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(Reset);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);


        _playerManager.PlayerStatusChanged += PlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _playerManager.PlayerStatusChanged -= PlayerStatusChanged;
    }

    public void OpenEui(IPlayerSession session)
    {
        if (session.AttachedEntity is not {Valid: true} attached ||
            !_entityManager.HasComponent<GhostComponent>(attached))
            return;

        if(_openUis.ContainsKey(session))
            CloseEui(session);

        var eui = _openUis[session] = new GhostRolesEui();
        _euiManager.OpenEui(eui, session);
        eui.StateDirty();
    }

    private void UpdatePlayerEui(IPlayerSession session)
    {
        if (!_openUis.TryGetValue(session, out var ui))
            return;

        ui.StateDirty();
    }

    public void UpdateAllEui()
    {
        foreach (var eui in _openUis.Values)
        {
            eui.StateDirty();
        }
        // Note that this, like the EUIs, is deferred.
        // This is for roughly the same reasons, too:
        // Someone might spawn a ton of ghost roles at once.
        _needsUpdateGhostRoleCount = true;
    }

    public void CloseEui(IPlayerSession session)
    {
        if (!_openUis.ContainsKey(session))
            return;

        _openUis.Remove(session, out var eui);
        eui?.Close();
    }

    private void UpdateUi()
    {
        if (_needsUpdateGhostRoles)
        {
            _needsUpdateGhostRoles = false;
            UpdateAllEui();
        }

        if (_needsUpdateGhostRoleCount)
        {
            _needsUpdateGhostRoleCount = false;
            var response = new GhostUpdateGhostRoleCountEvent(AvailableRolesCount, AvailableRoles);
            foreach (var player in _playerManager.Sessions)
            {
                RaiseNetworkEvent(response, player.ConnectedClient);
            }
        }
    }

    public string[] GetPlayerRequestedGhostRoles(IPlayerSession player)
    {
        var requested = new List<string>();
        if (!_playerLotteryData.TryGetValue(player, out var data))
            return requested.ToArray();

        requested.AddRange(data.GhostRoles);

        return requested.ToArray();
    }

    public uint[] GetPlayerRequestedRoleGroups(IPlayerSession player)
    {
        var requested = new List<uint>();
        if (!_playerLotteryData.TryGetValue(player, out var data))
            return requested.ToArray();

        requested.AddRange(data.GhostRoleGroups);

        return requested.ToArray();
    }

    private void PlayerStatusChanged(object? _, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.InGame)
            return;

        var response = new GhostUpdateGhostRoleCountEvent(AvailableRolesCount, AvailableRoles);
        RaiseNetworkEvent(response, args.Session.ConnectedClient);
    }

    private void OnPlayerAttached(PlayerAttachedEvent message)
    {
        // Close the session of any player that has a ghost roles window open and isn't a ghost anymore.
        if (!_openUis.ContainsKey(message.Player))
            return;
        if (EntityManager.HasComponent<GhostComponent>(message.Entity))
            return;

        CloseEui(message.Player);
        ClearPlayerLotteryRequests(message.Player);
    }

    private void Reset(RoundRestartCleanupEvent ev)
    {
        foreach (var session in _openUis.Keys)
        {
            CloseEui(session);
        }

        _openUis.Clear();
        _playerLotteryData.Clear();
        _ghostRoleComponentLotteries.Clear();
        _roleGroupLotteries.Clear();

        LotteryStartTime = TimeSpan.Zero;
        LotteryExpiresTime = TimeSpan.Zero;
    }

    public void GhostRoleAddPlayerLotteryRequest(IPlayerSession player, string identifier)
    {
        if (!_playerLotteryData.TryGetValue(player, out var data))
            _playerLotteryData[player] = data = new PlayerLotteryData();

        if (!data.GhostRoles.Add(identifier))
            return;

        UpdatePlayerEui(player);
    }

    public void GhostRoleRemovePlayerLotteryRequest(IPlayerSession player, string identifier)
    {
        if (!_playerLotteryData.TryGetValue(player, out var data))
            return;

        if (data.GhostRoles.Remove(identifier))
            UpdatePlayerEui(player);
    }

    public void GhostRoleRemoveComponent(GhostRoleComponent component)
    {
        if (!_ghostRoleComponentLotteries.TryGetValue(component.RoleName, out var components))
            return;

        // TODO: Move components between these two if `RoleUseLottery` is modified.
        components.ForLottery.Remove(component);
        components.ForTake.Remove(component);

        _needsUpdateGhostRoles = true;
        _needsUpdateGhostRoleCount = true;
    }

    public void GhostRoleGroupAddPlayerLotteryRequest(IPlayerSession player, uint identifier)
    {
        if (!_playerLotteryData.TryGetValue(player, out var data))
            _playerLotteryData[player] = data = new PlayerLotteryData();

        if (data.GhostRoleGroups.Add(identifier))
            UpdatePlayerEui(player);
    }

    public void GhostRoleGroupRemovePlayerLotteryRequest(IPlayerSession player, uint identifier)
    {
        if (!_playerLotteryData.TryGetValue(player, out var data))
            return;

        if (data.GhostRoleGroups.Remove(identifier))
            UpdatePlayerEui(player);
    }

    public void ClearPlayerLotteryRequests(IPlayerSession player)
    {
        if (!_playerLotteryData.TryGetValue(player, out var data))
            return;

        data.GhostRoles.Clear();
        data.GhostRoleGroups.Clear();
        UpdatePlayerEui(player);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_gameTiming.CurTime < LotteryExpiresTime)
        {
            UpdateUi();
            return;
        }

        _needsUpdateGhostRoles = true;
        _needsUpdateGhostRoleCount = true;

        var successfulPlayers = new HashSet<IPlayerSession>();
        ProcessRoleGroupLottery(successfulPlayers);
        ProcessGhostRoleLottery(successfulPlayers);

        var anyAdded = false;

        var lotteryEvent = new RequestAvailableLotteryItemsMessage();
        RaiseLocalEvent(lotteryEvent);

        foreach (var component in lotteryEvent.GhostRoles)
        {
            if (!_ghostRoleComponentLotteries.TryGetValue(component.RoleName, out var components))
                _ghostRoleComponentLotteries[component.RoleName] = components = new GhostRoleLotteryRecord();

            var added = component.RoleUseLottery
                ? components.ForLottery.Add(component)
                : components.ForTake.Add(component);

            anyAdded |= added;
        }

        foreach (var group in lotteryEvent.GhostRoleGroups)
        {
            var added = _roleGroupLotteries.Add(group);
            anyAdded |= added;
        }

        var ev = new GhostRoleCountRequestedMessage();
        RaiseLocalEvent(ev);
        AvailableRolesCount = ev.Count;

        if (anyAdded)
            UpdateAllEui();

        var elapseTime = TimeSpan.FromSeconds(_cfgManager.GetCVar<float>(CCVars.GhostRoleLotteryTime));

        LotteryStartTime = _gameTiming.CurTime;
        LotteryExpiresTime = LotteryStartTime + elapseTime;
        UpdateUi();
    }

    private void ProcessGhostRoleLottery(ISet<IPlayerSession> successfulPlayers)
    {
        var sessions = new List<IPlayerSession>(_playerLotteryData.Count);
        var clearRoles = new HashSet<string>();

        foreach (var (ghostRoleIdentifier, components) in _ghostRoleComponentLotteries)
        {
            sessions.Clear();
            foreach (var (playerSession, data) in _playerLotteryData)
            {
                if(data.GhostRoles.Contains(ghostRoleIdentifier))
                    sessions.Add(playerSession);
            }

            var playerCount = sessions.Count;
            var componentCount = components.ForLottery.Count;

            if (playerCount == 0)
                continue;

            var sessionIdx = 0;
            var compIdx = 0;

            _random.Shuffle(sessions);

            while (sessionIdx < playerCount && compIdx < componentCount)
            {
                var session = sessions[sessionIdx];
                var component = components.ForLottery.First();

                if (session.Status != SessionStatus.InGame || successfulPlayers.Contains(session))
                {
                    sessionIdx++;
                    continue;
                }

                var takeoverEvent = new GhostRoleRequestTakeoverMessage(session, component);
                RaiseLocalEvent(component.Owner, takeoverEvent);
                if (!takeoverEvent.Result)
                {
                    compIdx++;
                    components.ForLottery.Remove(component);
                    continue;
                }

                if (takeoverEvent.GhostRoleTaken)
                {
                    components.ForLottery.Remove(component);
                    compIdx++;
                }


                sessionIdx++;

                successfulPlayers.Add(session);
                ClearPlayerLotteryRequests(session);
                CloseEui(session);
            }

            if (components.ForLottery.Count == 0)
                clearRoles.Add(ghostRoleIdentifier);
        }

        foreach (var (_, data) in _playerLotteryData)
        {
            data.GhostRoles.ExceptWith(clearRoles);
        }
    }

    private void ProcessRoleGroupLottery(ISet<IPlayerSession> successfulPlayers)
    {
        var sessions = new List<IPlayerSession>(_playerLotteryData.Count);
        var clearRoleGroups = new HashSet<uint>();

         foreach (var roleGroupIdentifier in _roleGroupLotteries)
         {
             foreach (var (session, data) in _playerLotteryData)
             {
                 if (data.GhostRoleGroups.Contains(roleGroupIdentifier))
                     sessions.Add(session);
             }

             var playerCount = sessions.Count;
             if (playerCount == 0)
                 return;

             var sessionIdx = 0;

             _random.Shuffle(sessions);

             while (sessionIdx < playerCount)
             {
                 var session = sessions[sessionIdx];
                 if (session.Status != SessionStatus.InGame || successfulPlayers.Contains(session))
                 {
                     sessionIdx++;
                     continue;
                 }

                 var ev = new GhostRoleGroupRequestTakeoverMessage(session, roleGroupIdentifier);
                 RaiseLocalEvent(ev);

                 if(!ev.Result)
                     continue;

                 sessionIdx++;
                 successfulPlayers.Add(session);
                 ClearPlayerLotteryRequests(session);
                 CloseEui(session);

                 if (ev.RoleGroupTaken)
                 {
                     clearRoleGroups.Add(roleGroupIdentifier);
                 }
             }

             _roleGroupLotteries.ExceptWith(clearRoleGroups);
             foreach (var (_, data) in _playerLotteryData)
             {
                 data.GhostRoleGroups.ExceptWith(clearRoleGroups);
             }
         }
    }
}

[AnyCommand]
public sealed class GhostRoles : IConsoleCommand
{
    public string Command => "ghostroles";
    public string Description => "Opens the ghost role request window.";
    public string Help => $"{Command}";
    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if(shell.Player is IPlayerSession playerSession)
            EntitySystem.Get<GhostRoleLotterySystem>().OpenEui(playerSession);
        else
            shell.WriteLine("You can only open the ghost roles UI on a client.");
    }
}

