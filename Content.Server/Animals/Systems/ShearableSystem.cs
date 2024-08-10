using Content.Server.Stack;
using Content.Shared.Animals;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Shearing;
using Robust.Shared.Prototypes;

namespace Content.Server.Animals;

public sealed class ShearableSystem : EntitySystem
{
    [Dependency]
    private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    [Dependency]
    private readonly IPrototypeManager _prototypeManager = default!;

    [Dependency]
    private readonly SharedPopupSystem _popup = default!;

    [Dependency]
    private readonly StackSystem _stackSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShearableComponent, ShearingDoAfterEvent>(OnSheared);
    }

    /// <summary>
    ///     Called by the ShearingDoAfter event.
    ///     Checks the action hasn't been cancelled, already handled, and that there's an item in the player's hand.
    ///     Checks that the target shearable creature contains a shearable solution.
    ///     Performs solution calcuations, then creates a corresponding pop-up message, and if successful spawns shearedProductID under the shearable creature.
    /// </summary>
    private void OnSheared(Entity<ShearableComponent> ent, ref ShearingDoAfterEvent args)
    {
        // Check the action hasn't been cancelled, or hasn't already been handled, or that the player's hand is empty.
        if (args.Cancelled || args.Handled || args.Args.Used == null)
            return;

        // Resolves the targetSolutionName as a solution inside the shearable creature. Outputs the "solution" variable.
        if (
            !_solutionContainer.ResolveSolution(
                ent.Owner,
                ent.Comp.TargetSolutionName,
                ref ent.Comp.Solution,
                out var solution
            )
        )
            return;

        // Mark as handled so we don't duplicate.
        args.Handled = true;

        // Store solution.Volume in a variable to make calculations a bit clearer.
        var targetSolutionQuantity = solution.Volume;

        // Create a stack object so we can reference its name in localisation.
        _prototypeManager.TryIndex(ent.Comp.ShearedProductID, out var shearedProductStack);
        if (shearedProductStack == null)
        {
            Log.Error(
                $"Could not resolve ShearedProductID \"{ent.Comp.ShearedProductID}\" to a StackPrototype while shearing. Does this item exist?"
            );
            return;
        }

        // Solution is measured in units but the actual value for 1u is 1000 reagent, so multiply it by 100.
        // Then, divide by 1 because it's the reagent needed for 1 product.
        var productsPerSolution = (int)(1 / ent.Comp.ProductsPerSolution * 100);

        // Work out the maxium stack size of the product.
        var maxProductsToSpawnValue = 0;
        var maxProductsToSpawn = _prototypeManager.Index(ent.Comp.ShearedProductID).MaxCount;
        if (maxProductsToSpawn.HasValue)
        {
            maxProductsToSpawnValue = maxProductsToSpawn.Value;
        }

        // Modulas the targetSolutionQuantity so no solution is wasted if it can't be divided evenly.
        // Everything is divided by 100, because for fixedPoint2 multiplies everything by 100.
        // Math.Min ensures that no more solution than what is needed for the maximum stack is used, shear the entity multiple times if you want the rest of the product.
        var solutionToRemove = FixedPoint2.New(
            Math.Min(
                (targetSolutionQuantity.Value - targetSolutionQuantity.Value % productsPerSolution) / 100,
                maxProductsToSpawnValue * productsPerSolution / 100
            )
        );

        // Failure message, if the shearable creature has no targetSolutionName to be sheared.
        if (solutionToRemove == 0)
        {
            _popup.PopupEntity(
                Loc.GetString(
                    "shearable-system-no-product",
                    ("target", Identity.Entity(ent.Owner, EntityManager)),
                    ("product", shearedProductStack.Name)
                ),
                ent.Owner,
                args.Args.User
            );
            return;
        }

        // Split the solution inside the creature by solutionToRemove, return what was removed.
        var removedSolution = _solutionContainer.SplitSolution(ent.Comp.Solution.Value, solutionToRemove);

        // Target the creature's location.
        var spawnCoordinates = Transform(ent).Coordinates;

        // Spawn cotton.
        _stackSystem.Spawn(
            removedSolution.Volume.Value / productsPerSolution,
            ent.Comp.ShearedProductID,
            spawnCoordinates
        );

        // Success message.
        _popup.PopupEntity(
            Loc.GetString(
                "shearable-system-success",
                ("target", Identity.Entity(ent.Owner, EntityManager)),
                ("product", shearedProductStack.Name)
            ),
            ent.Owner,
            args.Args.User,
            PopupType.Medium
        );
    }
}
