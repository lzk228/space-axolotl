using System.Linq;
using Content.Server.EUI;
using Content.Server.Ghost.Components;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Ghost.Roles.UI;
using Content.Shared.Administration;
using Content.Shared.CCVar;
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

/// <summary>
///     Stores the requests by identifier for ghost roles and ghost role groups.
/// </summary>
public sealed class PlayerLotteryData
{
    public readonly HashSet<string> GhostRoles = new();
    public readonly HashSet<uint> GhostRoleGroups = new();
}

public record GhostRoleData
{
    public string RoleName { get; init; } = "";
    public string RoleDescription { get; init; } = "";
    public string RoleRules { get; init; } = "";
    public HashSet<GhostRoleComponent> Components { get; init; } = new();
    public int AvailableTakeovers { get; private set; }
    public int AvailableLotteries { get; private set; }

    /// <summary>
    /// Add the ghost role component and updates the available count depending on the lottery flag.
    /// </summary>
    /// <param name="comp"></param>
    public void Add(GhostRoleComponent comp)
    {
        if (!Components.Add(comp))
            return;

        if (comp.RoleLotteryEnabled)
            AvailableLotteries += comp.AvailableTakeovers;
        else
            AvailableTakeovers += comp.AvailableTakeovers;
    }

    /// <summary>
    /// Remove the component and reduces the appropriate count if the component was present.
    /// </summary>
    /// <param name="comp"></param>
    /// <param name="wasLottery"></param>
    /// <returns></returns>
    public bool Remove(GhostRoleComponent comp, bool wasLottery)
    {
        var removed = Components.Remove(comp);
        if (!removed)
            return removed;

        if (wasLottery)
            AvailableLotteries--;
        else
            AvailableTakeovers--;

        return removed;
    }
}

/// <summary>
/// Handles the logic for the ghost roles EUI and manages lotteries for <see cref="GhostRoleSystem"/> and
/// <see cref="GhostRoleGroupSystem"/>.
/// <para/>
///
/// Lotteries share a single expiration timer, and will all be run simultaneously. The available ghost roles
/// and ghost role groups are snapshot at the beginning of each lottery period. Removals are reflected immediately
/// but additions will be part of the next lottery snapshot.
/// </summary>
[UsedImplicitly]
public sealed class GhostRoleSelectionSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfgManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;

    private GhostRoleSystem? _ghostRoles;
    private GhostRoleGroupSystem? _ghostRoleGroups;

    /// <summary>
    /// Open EUIs per-player.
    /// </summary>
    private readonly Dictionary<IPlayerSession, GhostRolesEui> _openUis = new();

    /// <summary>
    /// Lottery requests per-player.
    /// </summary>
    private readonly Dictionary<IPlayerSession, PlayerLotteryData> _playerLotteryData = new ();

    /// <summary>
    /// Ghost roles put up for the current lottery.
    /// </summary>
    private readonly Dictionary<string, GhostRoleData> _ghostRoleComponentLotteries = new();

    /// <summary>
    /// Ghost role groups put up for the current lottery.
    /// </summary>
    private readonly HashSet<uint> _roleGroupLotteries = new();

    /// <summary>
    /// List of unique available roles. Used by the UI so it can highlight the ghost roles button
    /// when a brand-new role is added.
    /// </summary>
    private readonly HashSet<string> _availableRoles = new();

    private bool _needsUpdateGhostRoles = true;
    private bool _needsUpdateGhostRoleCount = true;

    /// <summary>
    /// Time the lottery started. Uses synced time.
    /// </summary>
    public TimeSpan LotteryStartTime { get; private set; } = TimeSpan.Zero;

    /// <summary>
    /// Time the lotteries will expire at. Uses synced time.
    /// </summary>
    public TimeSpan LotteryExpiresTime { get; private set; } = TimeSpan.Zero;

    /// <summary>
    /// Cached available role count, representing the total number of available spawns. Be aware that certain
    /// items (see <see cref="GhostRoleMobSpawnerComponent"/>) can be worth multiple spawns.
    /// </summary>
    private int AvailableRolesCount { get; set; }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(Reset);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);

        SubscribeLocalEvent<GhostRoleModifiedEvent>(OnGhostRoleModified);
        SubscribeLocalEvent<GhostRoleAvailabilityChangedEvent>(OnGhostRoleAvailabilityChanged);
        SubscribeLocalEvent<GhostRoleGroupEntityDetachedEvent>(OnGhostRoleGroupEntityDetached);
        SubscribeLocalEvent<GhostRoleGroupDeletedEvent>(OnGhostRoleGroupDeleted);

        _playerManager.PlayerStatusChanged += PlayerStatusChanged;
    }

    private void OnGhostRoleAvailabilityChanged(GhostRoleAvailabilityChangedEvent ev)
    {
        if (ev.Available)
            return; // Available ghost role components will be part of the next lottery snapshot.

        // Ghost role became unavailable. Remove if it's currently in a lottery.
        var removed = false;
        if (_ghostRoleComponentLotteries.TryGetValue(ev.GhostRole.RoleIdentifier, out var data))
            removed = data.Remove(ev.GhostRole, ev.GhostRole.RoleLotteryEnabled);

        _needsUpdateGhostRoles |= removed;
        _needsUpdateGhostRoleCount |= removed;
    }

    private void OnGhostRoleModified(GhostRoleModifiedEvent ev)
    {
        var removed = false;
        var roleIdentifier = ev.PreviousRoleIdentifier ?? ev.GhostRole.RoleIdentifier;
        var lotteryEnabled = ev.PreviousRoleLotteryEnabled ?? ev.GhostRole.RoleLotteryEnabled;

        if (ev.PreviousRoleIdentifier != null || ev.PreviousRoleLotteryEnabled != null)
        {
            // Remove the ghost role and update counts, based off if role name and role lottery flag was modified.
            if(_ghostRoleComponentLotteries.TryGetValue(roleIdentifier, out var data))
                removed |= data.Remove(ev.GhostRole, lotteryEnabled);
        }

        _needsUpdateGhostRoles |= removed;
        _needsUpdateGhostRoleCount |= removed;
    }

    private void OnGhostRoleGroupDeleted(GhostRoleGroupDeletedEvent ev)
    {
        if (!_roleGroupLotteries.Remove(ev.RoleGroupIdentifier))
            return;

        _needsUpdateGhostRoles = true;
        _needsUpdateGhostRoleCount = true;
    }

    private void OnGhostRoleGroupEntityDetached(GhostRoleGroupEntityDetachedEvent ev)
    {
        if (!_roleGroupLotteries.Contains(ev.RoleGroup))
            return;

        _needsUpdateGhostRoles = true;
        _needsUpdateGhostRoleCount = true;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _playerManager.PlayerStatusChanged -= PlayerStatusChanged;
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

    private void PlayerStatusChanged(object? _, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.InGame)
            return;

        var response = new GhostUpdateGhostRoleCountEvent(AvailableRolesCount, _availableRoles.ToArray());
        RaiseNetworkEvent(response, args.Session.ConnectedClient);
    }

    #region UI
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

    private void UpdateAllEui()
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

        if (!_needsUpdateGhostRoleCount)
            return;

        _needsUpdateGhostRoleCount = false;
        _ghostRoleGroups ??= Get<GhostRoleGroupSystem>();

        AvailableRolesCount = 0;
        foreach (var (_, data) in _ghostRoleComponentLotteries)
        {
            AvailableRolesCount += data.AvailableLotteries;
            AvailableRolesCount += data.AvailableTakeovers;
        }

        foreach (var groupIdentifier in _roleGroupLotteries)
        {
            AvailableRolesCount += _ghostRoleGroups.GetAvailableCount(groupIdentifier);
        }

        var response = new GhostUpdateGhostRoleCountEvent(AvailableRolesCount, _availableRoles.ToArray());
        foreach (var player in _playerManager.Sessions)
        {
            RaiseNetworkEvent(response, player.ConnectedClient);
        }
    }

    /// <summary>
    /// Retrieves the ghost roles requested by the player.
    /// </summary>
    /// <param name="player">The player to retrieve requests for.</param>
    /// <returns>Identifiers of ghost roles requested by the player.</returns>
    public string[] GetPlayerRequestedGhostRoles(IPlayerSession player)
    {
        var requested = new List<string>();
        if (!_playerLotteryData.TryGetValue(player, out var data))
            return requested.ToArray();

        requested.AddRange(data.GhostRoles);

        return requested.ToArray();
    }

    /// <summary>
    /// Retrieves the ghost role groups requested by the player.
    /// </summary>
    /// <param name="player"></param>
    /// <returns></returns>
    public uint[] GetPlayerRequestedRoleGroups(IPlayerSession player)
    {
        var requested = new List<uint>();
        if (!_playerLotteryData.TryGetValue(player, out var data))
            return requested.ToArray();

        requested.AddRange(data.GhostRoleGroups);

        return requested.ToArray();
    }
    #endregion

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

    /// <summary>
    /// Removes every type of request for a single player.
    /// </summary>
    /// <param name="player">The player to clear all requests for.</param>
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

        _ghostRoles ??= Get<GhostRoleSystem>();
        _ghostRoleGroups ??= Get<GhostRoleGroupSystem>();

        _needsUpdateGhostRoles = true;
        _needsUpdateGhostRoleCount = true;

        var successfulPlayers = new HashSet<IPlayerSession>();
        ProcessRoleGroupLottery(successfulPlayers);
        ProcessGhostRoleLottery(successfulPlayers);

        // Prepare to start the next lottery.
        var elapseTime = TimeSpan.FromSeconds(_cfgManager.GetCVar<float>(CCVars.GhostRoleLotteryTime));
        LotteryStartTime = _gameTiming.CurTime;
        LotteryExpiresTime = LotteryStartTime + elapseTime;

        BuildNewGhostRoleLottery();
        BuildNewRoleGroupLottery();

        // Update all available ghost roles.
        _availableRoles.Clear();
        _availableRoles.UnionWith(_ghostRoleComponentLotteries.Keys);
        foreach (var roleGroupIdentifier in _roleGroupLotteries)
        {
            _availableRoles.Add($"GhostRoleGroup:{roleGroupIdentifier}");
        }

        UpdateUi();
    }

    #region Ghost Role
    /// <summary>
    /// Adds the player request for a lottery-enabled ghost role.
    /// </summary>
    /// <param name="player">The player to add the lottery request for.</param>
    /// <param name="identifier">Identifier of the group of ghost roles.</param>
    public void GhostRoleAddPlayerLotteryRequest(IPlayerSession player, string identifier)
    {
        if (!_playerLotteryData.TryGetValue(player, out var data))
            _playerLotteryData[player] = data = new PlayerLotteryData();

        if (!data.GhostRoles.Add(identifier))
            return;

        UpdatePlayerEui(player);
    }

    /// <summary>
    /// Removes the player request for a lottery-enabled ghost role.
    /// </summary>
    /// <param name="player">The player to remove the lottery request for.</param>
    /// <param name="identifier">Identifier of the group of ghost roles.</param>
    public void GhostRoleRemovePlayerLotteryRequest(IPlayerSession player, string identifier)
    {
        if (!_playerLotteryData.TryGetValue(player, out var data))
            return;

        if (data.GhostRoles.Remove(identifier))
            UpdatePlayerEui(player);
    }

    private void BuildNewGhostRoleLottery()
    {
        _ghostRoles ??= Get<GhostRoleSystem>();
        var components = _ghostRoles.GetAvailableGhostRoles();

        _ghostRoleComponentLotteries.Clear();
        foreach (var comp in components)
        {
            if (!_ghostRoleComponentLotteries.TryGetValue(comp.RoleIdentifier, out var data))
            {
                _ghostRoleComponentLotteries[comp.RoleIdentifier] = data = new GhostRoleData()
                {
                    RoleName = comp.RoleName,
                    RoleDescription = comp.RoleDescription,
                    RoleRules = comp.RoleRules,
                };
            }

            data.Add(comp);
        }

        // Clear player requests that no longer exist.
        foreach (var (_, lotteryRequests) in _playerLotteryData)
        {
            lotteryRequests.GhostRoles.IntersectWith(_ghostRoleComponentLotteries.Keys);
        }
    }

    /// <summary>
    /// Run's the lottery for all lottery enabled ghost roles.
    /// </summary>
    /// <param name="successfulPlayers">Player's that have already been given a ghost role. These should be skipped.</param>
    private void ProcessGhostRoleLottery(ISet<IPlayerSession> successfulPlayers)
    {
        _ghostRoles ??= Get<GhostRoleSystem>();
        var sessions = new List<IPlayerSession>(_playerLotteryData.Count);
        var clearRoles = new HashSet<string>();

        foreach (var (ghostRoleIdentifier, data) in _ghostRoleComponentLotteries)
        {
            sessions.Clear();
            foreach (var (playerSession, lotteryData) in _playerLotteryData)
            {
                if(lotteryData.GhostRoles.Contains(ghostRoleIdentifier))
                    sessions.Add(playerSession);
            }

            if (sessions.Count == 0)
                continue;

            _random.Shuffle(sessions);

            var roleComponents = data.Components;
            var sessionCount = sessions.Count;
            var sessionIdx = 0;
            var componentIdx = 0;

            foreach(var component in roleComponents)
            {
                if (!component.Available || !component.RoleLotteryEnabled)
                {
                    componentIdx++;
                    continue;
                }

                if (sessionIdx >= sessionCount)
                    break; // All sessions have been used.

                var session = sessions[sessionIdx];
                if (!_ghostRoles.PerformTakeover(session, component))
                {
                    componentIdx++;
                    continue;
                }

                if (component.Taken)
                    componentIdx++;

                sessionIdx++;
                successfulPlayers.Add(session);
                ClearPlayerLotteryRequests(session);
                CloseEui(session);
            }

            if(componentIdx >= roleComponents.Count)
                clearRoles.Add(ghostRoleIdentifier); // All available components got used.
        }

        foreach (var (_, data) in _playerLotteryData)
        {
            data.GhostRoles.ExceptWith(clearRoles);
        }
    }

    public GhostRoleInfo[] GetGhostRolesInfo()
    {
        var ghostRoleInfo = new List<GhostRoleInfo>(_ghostRoleComponentLotteries.Count);

        foreach (var (roleIdentifier, data) in _ghostRoleComponentLotteries)
        {
            var (lotteryCount, takeoverCount) = (data.AvailableLotteries, data.AvailableTakeovers);
            if(lotteryCount == 0 && takeoverCount == 0)
                continue;

            var role = new GhostRoleInfo()
            {
                Identifier = roleIdentifier,
                Name = data.RoleName,
                Description = data.RoleDescription,
                Rules = data.RoleRules,
                AvailableLotteryRoleCount = lotteryCount,
                AvailableImmediateRoleCount = takeoverCount,
            };

            ghostRoleInfo.Add(role);
        }

        return ghostRoleInfo.ToArray();
    }
    #endregion

    #region Ghost Role Group
    /// <summary>
    /// Adds the player request for a ghost role group.
    /// </summary>
    /// <param name="player">The player to add the request for.</param>
    /// <param name="identifier">The identifier of the role group.</param>
    public void GhostRoleGroupAddPlayerLotteryRequest(IPlayerSession player, uint identifier)
    {
        if (!_playerLotteryData.TryGetValue(player, out var data))
            _playerLotteryData[player] = data = new PlayerLotteryData();

        if (data.GhostRoleGroups.Add(identifier))
            UpdatePlayerEui(player);
    }

    /// <summary>
    /// Removes the player request for a ghost role group.
    /// </summary>
    /// <param name="player">The player to remove the request for.</param>
    /// <param name="identifier">The identifier of the role group.</param>
    public void GhostRoleGroupRemovePlayerLotteryRequest(IPlayerSession player, uint identifier)
    {
        if (!_playerLotteryData.TryGetValue(player, out var data))
            return;

        if (data.GhostRoleGroups.Remove(identifier))
            UpdatePlayerEui(player);
    }

    private void BuildNewRoleGroupLottery()
    {
        _ghostRoleGroups ??= Get<GhostRoleGroupSystem>();
        var roleGroups = _ghostRoleGroups.GetAvailableRoleGroups();

        _roleGroupLotteries.Clear();
        foreach (var groupIdentifier in roleGroups)
        {
            if(!_ghostRoleGroups.TryGetRoleGroup(groupIdentifier, out var group))
                continue;

            _roleGroupLotteries.Add(groupIdentifier);
        }

        foreach (var (_, requests) in _playerLotteryData)
        {
            requests.GhostRoleGroups.IntersectWith(_roleGroupLotteries);
        }
    }

    /// <summary>
    /// Run's the lottery for all released ghost role groups.
    /// </summary>
    /// <param name="successfulPlayers">Player that have already been given a role, these should be skipped.</param>
    private void ProcessRoleGroupLottery(ISet<IPlayerSession> successfulPlayers)
    {
        _ghostRoleGroups ??= Get<GhostRoleGroupSystem>();

        var sessions = new List<IPlayerSession>(_playerLotteryData.Count);
        var clearRoleGroups = new HashSet<uint>();

        foreach (var roleGroupIdentifier in _roleGroupLotteries)
        {
            if(!_ghostRoleGroups.TryGetRoleGroup(roleGroupIdentifier, out var group))
                continue;

            foreach (var (session, data) in _playerLotteryData)
            {
                if (data.GhostRoleGroups.Contains(roleGroupIdentifier))
                    sessions.Add(session);
            }

            var sessionIdx = 0;
            var sessionCount = sessions.Count;

            if (sessionCount == 0)
                continue;

            _random.Shuffle(sessions);

            var componentIdx = 0;
            var componentCount = group.ComponentLookup.Count;

            foreach (var component in group.ComponentLookup)
            {
                if (sessionIdx >= sessionCount)
                    break; // All sessions have been used.

                var session = sessions[sessionIdx];
                if (!_ghostRoleGroups.PerformTakeover(session, component, out var taken))
                {
                    componentIdx++;
                    continue;
                }

                if (taken)
                    componentIdx++;


                sessionIdx++;
                successfulPlayers.Add(session);
                ClearPlayerLotteryRequests(session);
                CloseEui(session);
            }

            if (componentIdx >= componentCount)
                clearRoleGroups.Add(roleGroupIdentifier);
        }

        foreach (var (_, data) in _playerLotteryData)
        {
            data.GhostRoleGroups.ExceptWith(clearRoleGroups);
        }
    }

    /// <summary>
    /// Retrieves the ghost role group data used for ghost roles UI.
    /// </summary>
    /// <returns></returns>
    public GhostRoleGroupInfo[] GetGhostRoleGroupsInfo()
    {
        _ghostRoleGroups ??= Get<GhostRoleGroupSystem>();

        var groups = new List<GhostRoleGroupInfo>(_roleGroupLotteries.Count);
        foreach (var roleGroupIdentifier in _roleGroupLotteries)
        {
            if(!_ghostRoleGroups.TryGetRoleGroup(roleGroupIdentifier, out var group))
                continue;


            if (group.Status != GhostRoleGroupStatus.Released)
                continue;


            groups.Add(new GhostRoleGroupInfo()
            {
                Identifier = group.Identifier,
                AvailableCount = EntityQuery<GhostRoleGroupComponent>().Count(c => c.Identifier == group.Identifier),
                Name = group.RoleName,
                Description = group.RoleDescription,
                Status = group.Status
            });
        }

        return groups.ToArray();
    }
    #endregion
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
            EntitySystem.Get<GhostRoleSelectionSystem>().OpenEui(playerSession);
        else
            shell.WriteLine("You can only open the ghost roles UI on a client.");
    }
}

