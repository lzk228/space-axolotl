using System.Threading.Tasks;
using Content.Server.Materials;
using Content.Server.Power.Components;
using Content.Server.Research.Components;
using Content.Server.Stack;
using Content.Server.UserInterface;
using Content.Shared.Interaction;
using Content.Shared.Lathe;
using Content.Shared.Research.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Server.GameObjects;
using Robust.Server.Player;

namespace Content.Server.Lathe.Components
{
    [RegisterComponent]
    public sealed class LatheComponent : SharedLatheComponent, IInteractUsing
    {
        [Dependency] private readonly IEntityManager _entMan = default!;

        public const int VolumePerSheet = 100;

        [ViewVariables]
        public Queue<LatheRecipePrototype> Queue { get; } = new();

        [ViewVariables]
        public bool Producing { get; private set; }

        private bool Inserting = false;

        [ViewVariables]
        private LatheRecipePrototype? _producingRecipe;
        [ViewVariables]
        private bool Powered => !_entMan.TryGetComponent(Owner, out ApcPowerReceiverComponent? receiver) || receiver.Powered;

        private static readonly TimeSpan InsertionTime = TimeSpan.FromSeconds(0.9f);

        [ViewVariables] private BoundUserInterface? UserInterface => Owner.GetUIOrNull(LatheUiKey.Key);

        protected override void Initialize()
        {
            base.Initialize();

            if (UserInterface != null)
            {
                UserInterface.OnReceiveMessage += UserInterfaceOnOnReceiveMessage;
            }
        }

        private void UserInterfaceOnOnReceiveMessage(ServerBoundUserInterfaceMessage message)
        {
            if (!Powered)
                return;

            switch (message.Message)
            {
                case LatheQueueRecipeMessage msg:
                    PrototypeManager.TryIndex(msg.ID, out LatheRecipePrototype? recipe);
                    if (recipe != null!)
                        for (var i = 0; i < msg.Quantity; i++)
                        {
                            Queue.Enqueue(recipe);
                            UserInterface?.SendMessage(new LatheFullQueueMessage(GetIdQueue()));
                        }
                    break;
                case LatheSyncRequestMessage _:
                    if (!_entMan.HasComponent<MaterialStorageComponent>(Owner)) return;
                    UserInterface?.SendMessage(new LatheFullQueueMessage(GetIdQueue()));
                    if (_producingRecipe != null)
                        UserInterface?.SendMessage(new LatheProducingRecipeMessage(_producingRecipe.ID));
                    break;

                case LatheServerSelectionMessage _:
                    if (!_entMan.TryGetComponent(Owner, out ResearchClientComponent? researchClient)) return;
                    researchClient.OpenUserInterface(message.Session);
                    break;

                case LatheServerSyncMessage _:
                    if (!_entMan.TryGetComponent(Owner, out TechnologyDatabaseComponent? database)
                    || !_entMan.TryGetComponent(Owner, out ProtolatheDatabaseComponent? protoDatabase)) return;

                    if (database.SyncWithServer())
                        protoDatabase.Sync();

                    break;
            }


        }

        internal bool Produce(LatheRecipePrototype recipe)
        {
            if (Producing || !Powered || !CanProduce(recipe) || !_entMan.TryGetComponent(Owner, out MaterialStorageComponent? storage)) return false;

            UserInterface?.SendMessage(new LatheFullQueueMessage(GetIdQueue()));

            Producing = true;
            _producingRecipe = recipe;

            foreach (var (material, amount) in recipe.RequiredMaterials)
            {
                // This should always return true, otherwise CanProduce fucked up.
                storage.RemoveMaterial(material, amount);
            }

            UserInterface?.SendMessage(new LatheProducingRecipeMessage(recipe.ID));

            SetAppearance(Powered, Producing, Inserting);

            Owner.SpawnTimer(recipe.CompleteTime, () =>
            {
                Producing = false;
                _producingRecipe = null;
                _entMan.SpawnEntity(recipe.Result, _entMan.GetComponent<TransformComponent>(Owner).Coordinates);
                UserInterface?.SendMessage(new LatheStoppedProducingRecipeMessage());
                SetAppearance(Powered, Producing, Inserting);
            });

            return true;
        }

        public void OpenUserInterface(IPlayerSession session)
        {
            UserInterface?.Open(session);
        }
        async Task<bool> IInteractUsing.InteractUsing(InteractUsingEventArgs eventArgs)
        {
            if (!_entMan.TryGetComponent(Owner, out MaterialStorageComponent? storage)
                ||  !_entMan.TryGetComponent(eventArgs.Using, out MaterialComponent? material)) return false;

            var multiplier = 1;

            if (_entMan.TryGetComponent(eventArgs.Using, out StackComponent? stack)) multiplier = stack.Count;

            var totalAmount = 0;

            // Check if it can insert all materials.
            foreach (var mat in material.MaterialIds)
            {
                // TODO: Change how MaterialComponent works so this is not hard-coded.
                if (!storage.CanInsertMaterial(mat, VolumePerSheet * multiplier)) return false;
                totalAmount += VolumePerSheet * multiplier;
            }

            // Check if it can take ALL of the material's volume.
            if (storage.CanTakeAmount(totalAmount)) return false;

            foreach (var mat in material.MaterialIds)
            {
                storage.InsertMaterial(mat, VolumePerSheet * multiplier);
            }

            Inserting = true;
            SetAppearance(Powered, Producing, Inserting);

            Owner.SpawnTimer(InsertionTime, () =>
            {
                Inserting = false;
                SetAppearance(Powered, Producing, Inserting);
            });

            _entMan.DeleteEntity(eventArgs.Using);

            return true;
        }

        private void SetAppearance(bool isOn, bool isRunning, bool isInserting)
        {
            if (!_entMan.TryGetComponent<AppearanceComponent>(Owner, out var appearance))
                return;

            appearance.SetData(LatheVisuals.IsOn, isOn);
            appearance.SetData(LatheVisuals.IsRunning, isRunning);
            appearance.SetData(LatheVisuals.IsInserting, isInserting);
        }

        private Queue<string> GetIdQueue()
        {
            var queue = new Queue<string>();
            foreach (var recipePrototype in Queue)
            {
                queue.Enqueue(recipePrototype.ID);
            }

            return queue;
        }

        private enum LatheState : byte
        {
            Base,
            Inserting,
            Producing
        }
    }
}
