﻿using System;
using System.Collections.Generic;
using System.Linq;
using Content.Client.GameObjects.Components.Construction;
using Content.Shared.Construction;
using Robust.Client.Graphics;
using Robust.Client.Interfaces.Placement;
using Robust.Client.Interfaces.ResourceManagement;
using Robust.Client.Placement;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.Utility;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Construction
{
    public class ConstructionMenu : SS14Window
    {
#pragma warning disable CS0649
        [Dependency] readonly IPrototypeManager PrototypeManager;
        [Dependency] readonly IResourceCache ResourceCache;
#pragma warning restore
        public ConstructorComponent Owner { get; set; }
        private readonly Button BuildButton;
        private readonly Button EraseButton;
        private readonly LineEdit SearchBar;
        private readonly Tree RecipeList;
        private readonly TextureRect InfoIcon;
        private readonly Label InfoLabel;
        private readonly ItemList StepList;

        private CategoryNode RootCategory;

        // This list is flattened in such a way that the top most deepest category is first.
        private List<CategoryNode> FlattenedCategories;
        private readonly PlacementManager Placement;

        public ConstructionMenu()
        {
            Size = (500, 350);

            IoCManager.InjectDependencies(this);
            Placement = (PlacementManager)IoCManager.Resolve<IPlacementManager>();
            Placement.PlacementCanceled += OnPlacementCanceled;

            Title = "Construction";

            var hSplitContainer = new HSplitContainer();

            // Left side
            var recipes = new VBoxContainer { CustomMinimumSize = new Vector2(150.0f, 0.0f) };
            SearchBar = new LineEdit { PlaceHolder = "Search" };
            RecipeList = new Tree { SizeFlagsVertical = SizeFlags.FillExpand, HideRoot = true };
            recipes.AddChild(SearchBar);
            recipes.AddChild(RecipeList);
            hSplitContainer.AddChild(recipes);

            // Right side
            var guide = new VBoxContainer();
            var info = new HBoxContainer();
            InfoIcon = new TextureRect();
            InfoLabel = new Label
            {
                SizeFlagsHorizontal = SizeFlags.FillExpand,
                SizeFlagsVertical = SizeFlags.ShrinkCenter
            };
            info.AddChild(InfoIcon);
            info.AddChild(InfoLabel);
            guide.AddChild(info);

            var stepsLabel = new Label
            {
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
                Text = "Steps"
            };
            guide.AddChild(stepsLabel);

            StepList = new ItemList
            {
                SizeFlagsVertical = SizeFlags.FillExpand,
                SelectMode = ItemList.ItemListSelectMode.None
            };
            guide.AddChild(StepList);

            var buttonsContainer = new HBoxContainer();
            BuildButton = new Button
            {
                SizeFlagsHorizontal = SizeFlags.FillExpand,
                TextAlign = Button.AlignMode.Center,
                Text = "Build!",
                Disabled = true,
                ToggleMode = false
            };
            EraseButton = new Button
            {
                TextAlign = Button.AlignMode.Center,
                Text = "Clear Ghosts",
                ToggleMode = true
            };
            buttonsContainer.AddChild(BuildButton);
            buttonsContainer.AddChild(EraseButton);
            guide.AddChild(buttonsContainer);

            hSplitContainer.AddChild(guide);
            Contents.AddChild(hSplitContainer);

            BuildButton.OnPressed += OnBuildPressed;
            EraseButton.OnToggled += OnEraseToggled;
            SearchBar.OnTextChanged += OnTextEntered;
            RecipeList.OnItemSelected += OnItemSelected;

            PopulatePrototypeList();
            PopulateTree();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                Placement.PlacementCanceled -= OnPlacementCanceled;
            }
        }

        void OnItemSelected()
        {
            var prototype = (ConstructionPrototype)RecipeList.Selected.Metadata;

            if (prototype == null)
            {
                InfoLabel.Text = "";
                InfoIcon.Texture = null;
                StepList.Clear();
                BuildButton.Disabled = true;
            }
            else
            {
                BuildButton.Disabled = false;
                InfoLabel.Text = prototype.Description;
                InfoIcon.Texture = prototype.Icon.Frame0();

                StepList.Clear();

                foreach (var forward in prototype.Stages.Select(a => a.Forward))
                {
                    if (forward == null)
                    {
                        continue;
                    }

                    Texture icon = null;
                    string text = "";
                    switch (forward)
                    {
                        case ConstructionStepEntityPrototype entityProto:
                            var entityPrototype = PrototypeManager.Index<EntityPrototype>(entityProto.EntityID); //Get the entity specified in the step for the recipe.
                            text = entityPrototype.Name; //Text = entity's name.
                            entityPrototype.Components.TryGetValue("Icon", out YamlDotNet.RepresentationModel.YamlMappingNode node); //Try to get the Icon "Component". Since we are working with yaml and not a real Entity or real Components, this gets messy.
                            if (node.Children.TryGetValue("sprite", out var spriteString)) //Try to grab the value of the sprite node
                            {
                                node.Children.TryGetValue("state", out var stateString); //If we have a sprite node we probably have a state node, hopefully.
                                icon = ResourceCache.GetResource<RSIResource>("/Textures/" + spriteString.ToString()).RSI[stateString.ToString()].Frame0; //Fuck you, it works.
                            }
                            break;
                    }

                    StepList.AddItem(text, icon, false);
                }
            }
        }

        void OnTextEntered(LineEdit.LineEditEventArgs args)
        {
            var str = args.Text;
            PopulateTree(string.IsNullOrWhiteSpace(str) ? null : str.ToLowerInvariant());
        }

        void OnBuildPressed(BaseButton.ButtonEventArgs args)
        {
            var prototype = (ConstructionPrototype)RecipeList.Selected.Metadata;
            if (prototype == null)
            {
                return;
            }

            if (prototype.Type != ConstructionType.Structure)
            {
                // In-hand attackby doesn't exist so this is the best alternative.
                var loc = Owner.Owner.GetComponent<ITransformComponent>().GridPosition;
                Owner.SpawnGhost(prototype, loc, Direction.North);
                return;
            }

            var hijack = new ConstructionPlacementHijack(prototype, Owner);
            var info = new PlacementInformation
            {
                IsTile = false,
                PlacementOption = prototype.PlacementMode,
            };


            Placement.BeginHijackedPlacing(info, hijack);
        }

        private void OnEraseToggled(BaseButton.ButtonToggledEventArgs args)
        {
            var hijack = new ConstructionPlacementHijack(null, Owner);
            Placement.ToggleEraserHijacked(hijack);
        }

        void PopulatePrototypeList()
        {
            RootCategory = new CategoryNode("", null);
            int count = 1;

            foreach (var prototype in PrototypeManager.EnumeratePrototypes<ConstructionPrototype>())
            {
                var currentNode = RootCategory;

                foreach (var category in prototype.CategorySegments)
                {
                    if (!currentNode.ChildCategories.TryGetValue(category, out var subNode))
                    {
                        count++;
                        subNode = new CategoryNode(category, currentNode);
                        currentNode.ChildCategories.Add(category, subNode);
                    }

                    currentNode = subNode;
                }

                currentNode.Prototypes.Add(prototype);
            }

            // Do a pass to sort the prototype lists and flatten the hierarchy.
            void Recurse(CategoryNode node)
            {
                // I give up we're using recursion to flatten this.
                // There probably IS a way to do it.
                // I'm too stupid to think of what that way is.
                foreach (var child in node.ChildCategories.Values)
                {
                    Recurse(child);
                }

                node.Prototypes.Sort(ComparePrototype);
                FlattenedCategories.Add(node);
                node.FlattenedIndex = FlattenedCategories.Count - 1;
            }

            FlattenedCategories = new List<CategoryNode>(count);
            Recurse(RootCategory);
        }

        void PopulateTree(string searchTerm = null)
        {
            RecipeList.Clear();

            var categoryItems = new Tree.Item[FlattenedCategories.Count];
            categoryItems[RootCategory.FlattenedIndex] = RecipeList.CreateItem();

            // Yay more recursion.
            Tree.Item ItemForNode(CategoryNode node)
            {
                if (categoryItems[node.FlattenedIndex] != null)
                {
                    return categoryItems[node.FlattenedIndex];
                }

                var item = RecipeList.CreateItem(ItemForNode(node.Parent));
                item.Text = node.Name;
                item.Selectable = false;
                categoryItems[node.FlattenedIndex] = item;
                return item;
            }

            foreach (var node in FlattenedCategories)
            {
                foreach (var prototype in node.Prototypes)
                {
                    if (searchTerm != null)
                    {
                        var found = false;
                        // TODO: don't run ToLowerInvariant() constantly.
                        if (prototype.Name.ToLowerInvariant().IndexOf(searchTerm, StringComparison.Ordinal) != -1)
                        {
                            found = true;
                        }
                        else
                        {
                            foreach (var keyw in prototype.Keywords.Concat(prototype.CategorySegments))
                            {
                                // TODO: don't run ToLowerInvariant() constantly.
                                if (keyw.ToLowerInvariant().IndexOf(searchTerm, StringComparison.Ordinal) != -1)
                                {
                                    found = true;
                                    break;
                                }
                            }
                        }

                        if (!found)
                        {
                            continue;
                        }
                    }

                    var subItem = RecipeList.CreateItem(ItemForNode(node));
                    subItem.Text = prototype.Name;
                    subItem.Metadata = prototype;
                }
            }
        }

        private void OnPlacementCanceled(object sender, EventArgs e)
        {
            EraseButton.Pressed = false;
        }

        private static int ComparePrototype(ConstructionPrototype x, ConstructionPrototype y)
        {
            return String.Compare(x.Name, y.Name, StringComparison.Ordinal);
        }

        class CategoryNode
        {
            public readonly string Name;
            public readonly CategoryNode Parent;

            public SortedDictionary<string, CategoryNode>
                ChildCategories = new SortedDictionary<string, CategoryNode>();

            public List<ConstructionPrototype> Prototypes = new List<ConstructionPrototype>();
            public int FlattenedIndex = -1;

            public CategoryNode(string name, CategoryNode parent)
            {
                Name = name;
                Parent = parent;
            }
        }
    }
}
