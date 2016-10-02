﻿using System;
using System.Linq;
using ChestsAnywhere.Framework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace ChestsAnywhere.Components
{
    /// <summary>A UI which lets the player choose a chest and location, and transfer transfer items between a chest and their inventory.</summary>
    internal class AccessChestMenu : ChestWithInventory, IDisposable
    {
        /*********
        ** Properties
        *********/
        /****
        ** Data
        ****/
        /// <summary>The number of draw cycles since the menu was initialise.</summary>
        private int DrawCount;

        /// <summary>Whether the chest dropdown is open.</summary>
        private bool IsChestListOpen;

        /// <summary>Whether the location dropdown is open.</summary>
        private bool IsLocationListOpen;

        /// <summary>Whether the 'edit chest' UI is being displayed.</summary>
        private bool IsEditChestOpen => this.EditChestForm != null;

        /// <summary>The known chests.</summary>
        private readonly ManagedChest[] Chests;

        /// <summary>The known chest location names.</summary>
        private readonly string[] Locations;

        /// <summary>The name of the selected location.</summary>
        private string SelectedLocation => this.SelectedChest.Location;

        /// <summary>The mod configuration.</summary>
        private readonly ModConfig Config;

        /// <summary>Whether to show the location tab.</summary>
        private bool ShowLocationTab => this.Config.GroupByLocation && this.Locations.Length > 1;

        /****
        ** UI
        ****/
        /// <summary>The font with which to render text.</summary>
        private readonly SpriteFont Font = Game1.smallFont;

        /// <summary>The chest selector tab.</summary>
        private Tab ChestTab;

        /// <summary>The location selector tab.</summary>
        private Tab LocationTab;

        /// <summary>The chest selector dropdown.</summary>
        private DropList<ManagedChest> ChestSelector;

        /// <summary>The button which edits the selected chest.</summary>
        private ClickableTextureComponent EditChestButton;

        /// <summary>The location selector dropdown.</summary>
        private DropList<string> LocationSelector;

        /// <summary>The button which sorts the chest items.</summary>
        private ClickableTextureComponent OrganizeChestButton;

        /// <summary>The button which sorts the inventory items.</summary>
        private ClickableTextureComponent OrganizeInventoryButton;

        /// <summary>The 'edit chest' form (if displayed).</summary>
        private EditChestForm EditChestForm;


        /*********
        ** Accessors
        *********/
        /// <summary>An event raised when the player selects a chest.</summary>
        public event Action<ManagedChest> OnChestSelected;


        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="chests">The known chests.</param>
        /// <param name="selectedChest">The selected chest.</param>
        /// <param name="config">The mod configuration.</param>
        public AccessChestMenu(ManagedChest[] chests, ManagedChest selectedChest, ModConfig config)
        {
            this.Chests = chests;
            this.SelectedChest = selectedChest;
            this.Config = config;
            this.Locations = this.Chests.Select(p => p.Location).Distinct().ToArray();
            this.InitialiseTabs();
            this.InitialiseSelectors();
            this.InitialiseTools();
        }

        /// <summary>The method invoked when the player presses a key.</summary>
        /// <param name="input">The key that was pressed.</param>
        public override void receiveKeyPress(Keys input)
        {
            this.ReceiveKey(input, this.Config.Keyboard);
        }

        /// <summary>The method invoked when the player presses a controller button.</summary>
        /// <param name="input">The button that was pressed.</param>
        public override void receiveGamePadButton(Buttons input)
        {
            this.ReceiveKey(input, this.Config.Controller);
        }

        /// <summary>The method invoked when the player presses a key.</summary>
        /// <typeparam name="T">The key type.</typeparam>
        /// <param name="input">The key that was pressed.</param>
        /// <param name="config">The input configuration.</param>
        public void ReceiveKey<T>(T input, InputMapConfiguration<T> config)
        {
            // ignore invalid input
            if (this.DrawCount < 10)
                return;
            if (!config.IsValidKey(input))
                return;

            // 'edit chest' UI overrides input
            if (this.IsEditChestOpen)
            {
                if (input.Equals(Keys.Escape))
                    this.ToggleEditChest(false);
                return;
            }

            // chest menu keys
            if (input.Equals(Keys.Escape))
            {
                if (this.IsChestListOpen)
                    this.IsChestListOpen = false;
                else if (this.IsLocationListOpen)
                    this.IsLocationListOpen = false;
                else
                    this.exitThisMenuNoSound();
            }
            else if (input.Equals(config.Toggle))
                this.exitThisMenuNoSound();
            else if (input.Equals(config.PrevChest))
                this.SelectPreviousChest();
            else if (input.Equals(config.NextChest))
                this.SelectNextChest();
            else if (input.Equals(config.SortItems))
                this.SortChestItems();
        }

        /// <summary>The method invoked when the player scrolls the dropdown using the mouse wheel.</summary>
        /// <param name="direction">The scroll direction.</param>
        public override void receiveScrollWheelAction(int direction)
        {
            if (this.IsEditChestOpen)
                return; // 'edit chest' UI handles all input when open
            else if (this.IsChestListOpen)
                this.ChestSelector.ReceiveScrollWheelAction(direction);
            else if (this.IsLocationListOpen)
                this.LocationSelector.ReceiveScrollWheelAction(direction);
        }

        /// <summary>The method invoked when the game window is resized.</summary>
        /// <param name="oldBounds">The previous window dimensions.</param>
        /// <param name="newBounds">The new window dimensions.</param>
        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            this.ChestSelector.ReceiveGameWindowResized();
            this.LocationSelector?.ReceiveGameWindowResized();
            this.InitialiseTabs();
            this.InitialiseTools();
        }

        /// <summary>The method invoked when the player left-clicks on the control.</summary>
        /// <param name="x">The X-position of the cursor.</param>
        /// <param name="y">The Y-position of the cursor.</param>
        /// <param name="playSound">Whether to enable sound.</param>
        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            // 'edit chest' UI (handles all input when open)
            if (this.IsEditChestOpen)
            {
                this.EditChestForm.ReceiveLeftClick(x, y);
                return;
            }

            // chest dropdown
            else if (this.IsChestListOpen)
            {
                // close dropdown
                this.IsChestListOpen = false;
                this.IsDisabled = false;

                // select chest
                if (this.ChestSelector.containsPoint(x, y))
                {
                    ManagedChest chest = this.ChestSelector.Select(x, y);
                    if (chest != null)
                        this.SelectChest(chest);
                }
            }

            // location dropdown
            else if (this.IsLocationListOpen)
            {
                // close dropdown
                this.IsLocationListOpen = false;
                this.IsDisabled = false;

                // select location
                if (this.LocationSelector.containsPoint(x, y))
                {
                    string location = this.LocationSelector.Select(x, y);
                    if (location != null && location != this.SelectedLocation)
                    {
                        this.SelectChest(this.Chests.First(p => p.Location == location));
                        this.InitialiseSelectors();
                    }
                }
            }

            // edit chest tab
            else if (this.EditChestButton.containsPoint(x, y))
                this.ToggleEditChest(true);

            // chest tab
            else if (this.ChestTab.containsPoint(x, y))
            {
                this.IsChestListOpen = true;
                this.IsDisabled = true;
            }

            // location tab
            else if (this.LocationTab?.containsPoint(x, y) == true)
            {
                this.IsLocationListOpen = true;
                this.IsDisabled = true;
            }

            // organize buttons
            else if (this.OrganizeChestButton.containsPoint(x, y))
                this.SortChestItems();
            else if (this.OrganizeInventoryButton.containsPoint(x, y))
                this.SortInventory();

            // chest/inventory UI
            else
                base.receiveLeftClick(x, y, playSound);
        }

        /// <summary>Render the UI.</summary>
        /// <param name="sprites">The sprites to render.</param>
        public override void draw(SpriteBatch sprites)
        {
            this.DrawCount++;

            // organize buttons
            this.OrganizeChestButton.draw(sprites, Color.White * this.Opacity, 1);
            this.OrganizeInventoryButton.draw(sprites, Color.White * this.Opacity, 1);

            // chest with inventory menu
            base.draw(sprites);

            // tabs
            this.ChestTab.Draw(sprites);
            this.EditChestButton.draw(sprites, Color.White * this.Opacity, 1);
            if (this.ShowLocationTab)
                this.LocationTab.Draw(sprites, this.Opacity);

            // tab dropdowns
            if (this.IsChestListOpen)
                this.ChestSelector.Draw(sprites, this.Opacity);
            if (this.IsLocationListOpen)
                this.LocationSelector.Draw(sprites, this.Opacity);

            // 'edit chest' UI
            if (this.IsEditChestOpen)
            {
                if (this.EditChestForm.IsActive)
                    this.EditChestForm.Draw(sprites);
                else
                    this.ToggleEditChest(false);
            }

            // mouse
            this.drawMouse(sprites);
        }

        /// <summary>Release all resources.</summary>
        public void Dispose()
        {
            // clear all events for garbage collection
            this.OnChestSelected = null;
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Initialise the inventory tools.</summary>
        private void InitialiseTools()
        {
            // organize buttons
            int buttonHeight = Sprites.Buttons.Organize.Height * Game1.pixelZoom;
            int buttonWidth = Sprites.Buttons.Organize.Width * Game1.pixelZoom;
            int borderSize = 3 * Game1.pixelZoom; // size of menu frame
            this.OrganizeChestButton = new ClickableTextureComponent(new Rectangle(this.xPositionOnScreen + this.width, this.yPositionOnScreen + height / 2 - buttonHeight - borderSize, buttonWidth, buttonHeight), "", "Organize", Sprites.Buttons.Sheet, Sprites.Buttons.Organize, Game1.pixelZoom);
            this.OrganizeInventoryButton = new ClickableTextureComponent(new Rectangle(this.xPositionOnScreen + this.width, this.yPositionOnScreen + height - buttonHeight - borderSize, buttonWidth, buttonHeight), "", "Organize", Sprites.Buttons.Sheet, Sprites.Buttons.Organize, Game1.pixelZoom);
        }

        /// <summary>Initialise the chest and location selectors.</summary>
        private void InitialiseSelectors()
        {
            // chest selector
            {
                ManagedChest[] chests = this.Chests.Where(chest => !this.ShowLocationTab || chest.Location == this.SelectedLocation).ToArray();
                int x = this.xPositionOnScreen + Game1.tileSize / 4;
                int y = this.yPositionOnScreen;
                this.ChestSelector = new DropList<ManagedChest>(this.SelectedChest, chests, chest => chest.Name, x, y, true, this.Font);
            }

            // location selector
            {
                string[] locations = this.Chests
                    .Select(p => p.Location)
                    .Distinct()
                    .ToArray();
                int x = this.xPositionOnScreen + this.width - Game1.tileSize / 4;
                int y = this.yPositionOnScreen;
                this.LocationSelector = new DropList<string>(this.SelectedLocation, locations, location => location, x, y, false, this.Font);
            }
        }

        /// <summary>Initialise the chest and location tabs.</summary>
        private void InitialiseTabs()
        {
            // chest
            {
                int x = this.xPositionOnScreen + Game1.tileSize / 4;
                int y = this.yPositionOnScreen - Game1.tileSize - Game1.tileSize / 16;
                this.ChestTab = new Tab(this.SelectedChest.Name, x, y, true, this.Font);
            }

            // edit-chest button
            {
                Rectangle sprite = Sprites.Icons.SpeechBubble;
                float zoom = Game1.pixelZoom / 2f;
                int x = this.ChestTab.bounds.X + this.ChestTab.bounds.Width;
                int y = this.ChestTab.bounds.Y;
                int width = (int)(sprite.Width * zoom);
                int height = (int)(sprite.Height * zoom);
                this.EditChestButton = new ClickableTextureComponent(new Rectangle(x + 5, y + height / 2, width, height), "edit chest", "Edit chest", Sprites.Icons.Sheet, sprite, scale: zoom, drawLabel: false);
            }

            // location
            if (this.ShowLocationTab)
            {
                int x = this.xPositionOnScreen + this.width - Game1.tileSize / 4;
                int y = this.yPositionOnScreen - Game1.tileSize - Game1.tileSize / 16;
                this.LocationTab = new Tab(this.SelectedLocation, x, y, false, this.Font);
            }
        }

        /// <summary>Switch to the specified chest.</summary>
        /// <param name="chest">The chest to select.</param>
        private void SelectChest(ManagedChest chest)
        {
            this.SelectedChest = chest;
            this.OnChestSelected?.Invoke(this.SelectedChest);
            this.InitialiseTabs();
        }

        /// <summary>Switch to the previous chest in the list.</summary>
        private void SelectPreviousChest()
        {
            int cur = Array.IndexOf(this.Chests, this.SelectedChest);
            this.SelectChest(this.Chests[cur != 0 ? cur - 1 : this.Chests.Length - 1]);
        }

        /// <summary>Switch to the next chest in the list.</summary>
        private void SelectNextChest()
        {
            int cur = Array.IndexOf(this.Chests, this.SelectedChest);
            this.SelectChest(this.Chests[(cur + 1) % this.Chests.Length]);
        }

        /// <summary>Toggle the 'edit chest' form.</summary>
        /// <param name="enabled">Whether the form should be enabled.</param>
        private void ToggleEditChest(bool enabled)
        {
            if (enabled)
            {
                this.EditChestForm = new EditChestForm(this.SelectedChest);
                this.Opacity = 0.5f;
                this.IsDisabled = true;
            }
            else
            {
                this.EditChestForm = null;
                this.Opacity = 1;
                this.IsDisabled = false;
            }
        }
    }
}