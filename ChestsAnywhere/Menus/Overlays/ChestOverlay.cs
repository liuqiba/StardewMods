using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Pathoschild.Stardew.ChestsAnywhere.Framework;
using Pathoschild.Stardew.ChestsAnywhere.Menus.Components;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using System.Linq;
using Pathoschild.Stardew.ChestsAnywhere.Framework.Containers;
using StardewValley.Objects;

namespace Pathoschild.Stardew.ChestsAnywhere.Menus.Overlays
{
    /// <summary>An <see cref="ItemGrabMenu"/> overlay which lets the player navigate and edit chests.</summary>
    internal class ChestOverlay : BaseChestOverlay
    {
        /*********
        ** Fields
        *********/
        /// <summary>The underlying chest menu.</summary>
        private readonly ItemGrabMenu Menu;

        /// <summary>The underlying menu's player inventory submenu.</summary>
        private readonly InventoryMenu MenuInventoryMenu;

        /// <summary>The default highlight function for the chest items.</summary>
        private readonly InventoryMenu.highlightThisItem DefaultChestHighlighter;

        /// <summary>The default highlight function for the player inventory items.</summary>
        private readonly InventoryMenu.highlightThisItem DefaultInventoryHighlighter;

        /// <summary>The button which sorts the player inventory.</summary>
        private ClickableTextureComponent SortInventoryButton;
        /// <summary>The button which sorts the player all chest.</summary>
        private ClickableTextureComponent SortAllChestButton;
        private IMonitor monitor;

        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="menu">The underlying chest menu.</param>
        /// <param name="chest">The selected chest.</param>
        /// <param name="chests">The available chests.</param>
        /// <param name="config">The mod configuration.</param>
        /// <param name="keys">The configured key bindings.</param>
        /// <param name="events">The SMAPI events available for mods.</param>
        /// <param name="input">An API for checking and changing input state.</param>
        /// <param name="reflection">Simplifies access to private code.</param>
        /// <param name="translations">Provides translations stored in the mod's folder.</param>
        /// <param name="showAutomateOptions">Whether to show Automate options.</param>
        public ChestOverlay(ItemGrabMenu menu, ManagedChest chest, ManagedChest[] chests, ModConfig config, ModConfigKeys keys, IModEvents events, IInputHelper input, IReflectionHelper reflection, ITranslationHelper translations, bool showAutomateOptions, IMonitor monitor)
            : base(menu, chest, chests, config, keys, events, input, reflection, translations, showAutomateOptions, keepAlive: () => Game1.activeClickableMenu is ItemGrabMenu, topOffset: -Game1.pixelZoom * 9 ,monitor)
        {
            this.Menu = menu;
            this.MenuInventoryMenu = menu.ItemsToGrabMenu;
            this.DefaultChestHighlighter = menu.inventory.highlightMethod;
            this.DefaultInventoryHighlighter = this.MenuInventoryMenu.highlightMethod;
            this.monitor = monitor;
        }


        /*********
        ** Protected methods
        *********/
        /// <summary>The method invoked when the player left-clicks.</summary>
        /// <param name="x">The X-position of the cursor.</param>
        /// <param name="y">The Y-position of the cursor.</param>
        /// <returns>Whether the event has been handled and shouldn't be propagated further.</returns>
        protected override bool ReceiveLeftClick(int x, int y)
        {
            if (this.IsInitialized)
            {

                switch (this.ActiveElement)
                {
                    case Element.EditForm:
                    case Element.ChestList:
                    case Element.CategoryList:
                        break;

                    default:
                        bool canNavigate = this.CanCloseChest;
                        if (canNavigate && this.Menu.okButton.containsPoint(x, y))
                        {
                            this.Exit(); // in some cases the game won't handle this correctly (e.g. Stardew Valley Fair fishing minigame)
                            return true;
                        }
                        else if (this.SortInventoryButton?.containsPoint(x, y) == true)
                        {
                            
                            this.SortInventory();
                            //this.monitor.Log("asdfafafsafsafa",LogLevel.Debug);
                            return true;
                        }
                        else if (this.SortAllChestButton?.containsPoint(x, y) == true)
                        {
                            //获取所有物品
                            var chestArray = this.GetAllChest();
                            var chests = new List<ManagedChest>(chestArray);
                            chests.RemoveAt(chests.Count - 1);
                            
                            List<Item> allItem = new List<Item>();
                            //this.monitor.Log("chest length" + chests.Count, LogLevel.Debug);
                            foreach (var chest in chests)
                            {
                                this.monitor.Log("chest" + chest.DisplayName, LogLevel.Debug);
                                ChestContainer c = chest.Container as ChestContainer;
                                var list = from item in c.Items
                                           where item.Stack > 0
                                            select item;
                                allItem.AddRange(list);
                                c.Items.Clear();
                                
                            }
                            //排序
                            allItem.Sort((a,b)=> {

                                return a.CompareTo(b);

                            });
                            //重置所有箱子中物品,并且命名

                            string SetName(List<Item> _list)
                            {
                                string name = "";
                                Item lastItem = null;
                                foreach (var item in _list)
                                {
                                    if (lastItem == null || lastItem.Category != item.Category)
                                    {
                                        name += "&" + item.getCategoryName();
                                        lastItem = item;
                                    }
                                }
                                return name;
                            }
                            int num = 0;
                            foreach (var chest in chests)
                            {
                                ChestContainer chestContainer = chest.Container as ChestContainer;
                                int capacity = Chest.capacity;
                                //this.monitor.Log("capacity" + capacity, LogLevel.Debug);
                                List<Item> _list;
                              


                                if (allItem.Count>=( num+capacity))
                                {
                                    _list = allItem.GetRange(num, capacity);
                                    chestContainer.Items.AddRange(_list);
                                    num += capacity;
                                    chestContainer.Chest.name = SetName(_list);
                                }
                                else
                                {
                                    _list = allItem.GetRange(num, allItem.Count - num);
                                    chestContainer.Items.AddRange(_list);
                                    chestContainer.Chest.name = SetName(_list);
                                    break;
                                }
                            }
                            //this.monitor.Log("SortAllChestButton", LogLevel.Debug);
                            return true;
                        }
                        break;
                }
            }

            return base.ReceiveLeftClick(x, y);
        }

        /// <summary>The method invoked when the cursor is hovered.</summary>
        /// <param name="x">The cursor's X position.</param>
        /// <param name="y">The cursor's Y position.</param>
        /// <returns>Whether the event has been handled and shouldn't be propagated further.</returns>
        protected override bool ReceiveCursorHover(int x, int y)
        {
            if (this.IsInitialized)
            {
                if (this.ActiveElement == Element.Menu)
                {
                    this.SortInventoryButton?.tryHover(x, y);
                    this.SortAllChestButton?.tryHover(x, y);
                }
                    
            }

            return base.ReceiveCursorHover(x, y);
        }

        /// <summary>Draw the overlay to the screen.</summary>
        /// <param name="batch">The sprite batch being drawn.</param>
        protected override void Draw(SpriteBatch batch)
        {
            if (!this.ActiveElement.HasFlag(Element.EditForm))
            {
                float navOpacity = this.CanCloseChest ? 1f : 0.5f;
                this.SortInventoryButton?.draw(batch, Color.White * navOpacity, 1f);
                this.SortAllChestButton?.draw(batch, Color.Green * navOpacity, 1f);
            }

            base.Draw(batch); // run base logic last, to draw cursor over everything else
        }

        /// <summary>Initialize the edit-chest overlay for rendering.</summary>
        protected override void ReinitializeComponents()
        {
            base.ReinitializeComponents();

            // sort inventory button
            if (this.Config.AddOrganizePlayerInventoryButton)
            {
                // add button
                Rectangle sprite = Sprites.Buttons.Organize;
                ClickableTextureComponent okButton = this.Menu.okButton;
                float zoom = Game1.pixelZoom;
                Rectangle buttonBounds = new Rectangle(okButton.bounds.X, (int)(okButton.bounds.Y - sprite.Height * zoom - 1 * zoom), (int)(sprite.Width * zoom), (int)(sprite.Height * zoom/2));
                this.SortInventoryButton = new ClickableTextureComponent("sort-inventory", buttonBounds, "sdfsf", this.Translations.Get("button.sort-inventory"), Sprites.Icons.Sheet, sprite, zoom);
                Rectangle buttonBounds2 = new Rectangle(okButton.bounds.X, (int)(buttonBounds.Y - sprite.Height * zoom - 1 * zoom), (int)(sprite.Width * zoom), (int)(sprite.Height * zoom/2));
                this.SortAllChestButton = new ClickableTextureComponent("sort-inventory55", buttonBounds2, null, this.Translations.Get("button.sort-all"), Sprites.Icons.Sheet, sprite, zoom);
                //this.SortInventoryButton = new ClickableTextureComponent("sort-inventory", buttonBounds, null, this.Translations.Get("button.save"), Sprites.Icons.Sheet, sprite, zoom);

                // adjust menu to fit
                this.Menu.trashCan.bounds.Y = this.SortAllChestButton.bounds.Y - this.Menu.trashCan.bounds.Height - 2 * Game1.pixelZoom;
            }
            else
            {
                this.SortInventoryButton = null;
                this.SortAllChestButton = null;
            }
                
        }

        /// <summary>Set whether the chest or inventory items should be clickable.</summary>
        /// <param name="clickable">Whether items should be clickable.</param>
        protected override void SetItemsClickable(bool clickable)
        {
            if (clickable)
            {
                this.Menu.inventory.highlightMethod = this.DefaultChestHighlighter;
                this.MenuInventoryMenu.highlightMethod = this.DefaultInventoryHighlighter;
            }
            else
            {
                this.Menu.inventory.highlightMethod = item => false;
                this.MenuInventoryMenu.highlightMethod = item => false;
            }
        }
    }
}
