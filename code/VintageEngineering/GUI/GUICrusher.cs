using Cairo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.RecipeSystem.Recipes;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace VintageEngineering
{
    public class GUICrusher : GuiDialogBlockEntity
    {
        private BECrusher becrusher;

        private RecipeCrusher _recipecrusher;
        private CrushingProperties _crushingproperties;
        private GrindingProperties _grindingproperties;
        private ItemStack _nuggetType;

        private ulong _currentPower;
        private ulong _maxPower;
        private float _craftProgress;

        public GUICrusher(string dialogTitle, InventoryBase inventory, BlockPos blockEntityPos, ICoreClientAPI capi, BECrusher bentity) : base(dialogTitle, inventory, blockEntityPos, capi)
        {
            if (base.IsDuplicate) return;

            capi.World.Player.InventoryManager.OpenInventory(inventory);
            becrusher = bentity;
            _currentPower = becrusher.Electric.CurrentPower;
            _maxPower = becrusher.Electric.MaxPower;
            _craftProgress = becrusher.RecipeProgress;
            SetupDialog();
        }

        private void OnSlotModified(int slotid)
        {
            capi.Event.EnqueueMainThreadTask(new Action(SetupDialog), "setupcrusherdlg");
        }

        public void SetupDialog()
        {
            ItemSlot hoveredSlot = capi.World.Player.InventoryManager.CurrentHoveredSlot;
            if (hoveredSlot != null && hoveredSlot.Inventory == base.Inventory)
            {
                capi.Input.TriggerOnMouseLeaveSlot(hoveredSlot);
            }
            else hoveredSlot = null;

            int titlebarheight = 31;
            double slotpadding = GuiElementItemSlotGridBase.unscaledSlotPadding;

            ElementBounds dialogBounds = ElementBounds.Fixed(315, 150 + titlebarheight);
            ElementBounds dialog = ElementBounds.Fill.WithFixedPadding(0);
            dialog.BothSizing = ElementSizing.FitToChildren;

            ElementBounds powerInset = ElementBounds.Fixed(10, 12 + titlebarheight, 34, 104);
            ElementBounds powerBounds = ElementBounds.Fixed(12, 14 + titlebarheight, 30, 100);

            ElementBounds inputGrid = ElementStdBounds.SlotGrid(EnumDialogArea.None, 54, 17 + titlebarheight, 1, 1);

            ElementBounds progressBar = ElementBounds.Fixed(109, 30 + titlebarheight, 90, 23);
            ElementBounds progressText = ElementBounds.Fixed(109, 32 + titlebarheight, 90, 23);

            ElementBounds enableBtn = ElementStdBounds.ToggleButton(210, 121 + titlebarheight, 92, 21);
            ElementBounds enableBtnText = ElementBounds.Fixed(212, 124 + titlebarheight, 87, 17);

            ElementBounds outputGrid = ElementStdBounds.SlotGrid(EnumDialogArea.None, 206, 17 + titlebarheight, 2, 2);
            ElementBounds outputtxtinset = ElementBounds.Fixed(54, 72 + titlebarheight, 145, 44);
            ElementBounds outputtextbnds = ElementBounds.Fixed(56, 74 + titlebarheight, 141, 40);

            ElementBounds dropdownmodetext = ElementBounds.Fixed(10, 123 + titlebarheight, 42, 21);
            ElementBounds dropdownbounds = ElementBounds.Fixed(54, 121 + titlebarheight, 145, 25);

            dialog.WithChildren(new ElementBounds[] 
            {
                dialogBounds,
                powerInset,
                powerBounds,
                inputGrid,
                progressBar,
                progressText,
                enableBtn,
                enableBtnText,
                outputGrid,
                outputtxtinset,
                outputtextbnds,
                dropdownmodetext,
                dropdownbounds
            });
            ElementBounds window = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

            if (capi.Settings.Bool["immersiveMouseMode"])
            {
                window.WithAlignment(EnumDialogArea.RightMiddle).WithFixedAlignmentOffset(-12, 0);
            }
            else
            {
                window.WithAlignment(EnumDialogArea.CenterMiddle).WithFixedAlignmentOffset(20, 0);
            }
            BlockPos blockPos = base.BlockEntityPosition;
            string enablebtnstring = becrusher.Electric.IsEnabled ? Lang.Get("vinteng:gui-turn-off") : Lang.Get("vinteng:gui-turn-on");

            CairoFont centerwhite = CairoFont.WhiteSmallText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Center);
            double[] yellow = new double[3] { 1, 1, 0 };
            CairoFont leftyellow = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Left).WithColor(yellow);
            CairoFont rightwhite = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Right);

            this.SingleComposer = capi.Gui.CreateCompo("vecrusherdlg" + blockPos?.ToString(), window)
                .AddShadedDialogBG(dialog, true, 5)
                .AddDialogTitleBar(Lang.Get("vinteng:gui-title-crusher"), new Action(OnTitleBarClosed), null, null)
                .BeginChildElements(dialog)

                .AddInset(powerInset, 2, 0.85f)
                .AddDynamicCustomDraw(powerBounds, new DrawDelegateWithBounds(OnPowerDraw), "powerDrawer")

                .AddItemSlotGrid(Inventory, new Action<object>(SendInvPacket), 1, new int[] { 0 }, inputGrid, "inputSlot")

                .AddDynamicCustomDraw(progressBar, new DrawDelegateWithBounds(OnProgressDraw), "progressBar")

                .AddDynamicText(GetProgressText(), centerwhite, progressText, "progressText")
                
                .AddSmallButton("", new ActionConsumable(EnableButtonClick), enableBtn, EnumButtonStyle.Small, "enableButton")
                .AddDynamicText(enablebtnstring, centerwhite, enableBtnText, "enableBtnText")

                .AddItemSlotGrid(Inventory, new Action<object>(SendInvPacket), 2, new int[] { 1, 2, 3, 4 }, outputGrid, "outputSlots")
                .AddInset(outputtxtinset, 2, 0f)
                .AddDynamicText(GetHelpText(), leftyellow, outputtextbnds, "outputText")

                // NEW DROP DOWN OPTION THINGY
                .AddStaticText(Lang.Get("vinteng:gui-word-mode") + ":", rightwhite, dropdownmodetext, "crushMode")
                .AddDropDown(
                            new string[] { "crush", "nugget", "recipe", "grind"},
                            new string[] { Lang.Get("vinteng:gui-word-crush"), Lang.Get("vinteng:gui-word-nugget"), Lang.Get("vinteng:gui-word-recipe"), Lang.Get("vinteng:gui-word-grind") },
                            GetCrushModeIndex(), OnSelectionChanged, dropdownbounds, "crushmode")

                .EndChildElements()
                .Compose(true);
        }

        private void OnSelectionChanged(string code, bool selected)
        {                        
            byte[] testbytes = Encoding.ASCII.GetBytes(code);
            capi.Network.SendBlockEntityPacket(base.BlockEntityPosition, 1003, testbytes);
        }

        public void Update(float craftProgress, ulong curPower, RecipeCrusher recipeCrusher = null, 
                            CrushingProperties crushingProperties = null, ItemStack nugget = null,
                            GrindingProperties grindingProperties = null)
        {
            // TODO THINGS IN HERE
            _craftProgress = craftProgress;
            _currentPower = curPower;
            _recipecrusher = recipeCrusher;
            _crushingproperties = crushingProperties;
            _grindingproperties = grindingProperties;
            _nuggetType = nugget;

            if (!IsOpened()) return;

            if (base.SingleComposer != null)
            {
                SingleComposer.GetDynamicText("progressText").SetNewText(GetProgressText());
                SingleComposer.GetCustomDraw("powerDrawer").Redraw();
                SingleComposer.GetCustomDraw("progressBar").Redraw();
                SingleComposer.GetDynamicText("enableBtnText").SetNewText(becrusher.Electric.IsEnabled ? Lang.Get("vinteng:gui-turn-off") : Lang.Get("vinteng:gui-turn-on"));
                SingleComposer.GetDynamicText("outputText").SetNewText(GetHelpText());
            }
        }

        private int GetCrushModeIndex()
        {
            string mode = becrusher.craftMode;
            switch (mode)
            {
                case "crush": return 0;
                case "nugget": return 1;
                case "recipe": return 2;
                case "grind": return 3;
                default: return 0;
            }
        }

        private bool EnableButtonClick()
        {
            capi.Network.SendBlockEntityPacket(base.BlockEntityPosition, 1002, null);
            return true;
        }

        private void OnProgressDraw(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        {
            ctx.Save();
            Matrix i = ctx.Matrix;

            float width = 90;
            float height = 23;

            VintageEngineering.GUI.IconHelper.NewHorizontalBar(ctx, 0, 0, new double[] { 0, 0, 0, 1 }, 2, true, true, width, height);

            // 0 -> 1
            float percentFilled = _craftProgress;
            // 74 is the width of the bar
            double percentRemaining = (double)(width - width * percentFilled);

            ctx.Rectangle(0, 0, width - percentRemaining, height);
            ctx.Clip();
            LinearGradient gradient = new LinearGradient(0, 0, GuiElement.scaled(width), 0);
            gradient.AddColorStop(0.0, new Color(0.0, 0.4, 0.0, 1.0));
            gradient.AddColorStop(1.0, new Color(0.2, 0.6, 0.2, 1.0));
            ctx.SetSource(gradient);
            VintageEngineering.GUI.IconHelper.NewHorizontalBar(ctx, 0, 0, new double[] { 0, 0, 0, 1 }, 2, false, false, width, height);
            gradient.Dispose();
            ctx.Restore();
        }

        private string GetProgressText()
        {
            string outputstring = "";
            float craftPercent = _craftProgress * 100;
            if (becrusher.Electric.IsSleeping) // machine is sleeping if on and not crafting
            {
                outputstring = Lang.Get("vinteng:gui-is-sleeping-short");
            }
            else
            {
                outputstring = $"{craftPercent:N1}%";
            }
            if (!becrusher.Electric.IsSleeping && !becrusher.IsCrafting) // these SHOULD be mutually exclusive
            {
                outputstring = $"Error";
            }
            if (!becrusher.Electric.IsEnabled)
            {
                outputstring = Lang.Get("vinteng:gui-word-off");
            }
            return outputstring;
        }

        private string GetHelpText()
        {            
            string outputhelptext = "";
            if (_recipecrusher != null)
            {
                ItemStack outputstack = _recipecrusher.Outputs[0].ResolvedItemstack;
                //string langcode = outputstack.Collectible.Code.Domain != null ? outputstack.Collectible.Code.Domain : "";
                //langcode += ":" + outputstack.Collectible.ItemClass.ToString().ToLowerInvariant();
                //langcode += "-" + outputstack.Collectible.Code.Path;
                outputhelptext = $"{Lang.Get("vinteng:gui-word-crafting")} {outputstack.Collectible.GetHeldItemName(outputstack)}";

            }
            else if (_crushingproperties != null)
            {
                ItemStack outputstack = _crushingproperties.CrushedStack.ResolvedItemstack;
                //string langcode = outputstack.Collectible.Code.Domain != null ? outputstack.Collectible.Code.Domain : "";
                //langcode += ":" + outputstack.Collectible.ItemClass.ToString().ToLowerInvariant();
                //langcode += "-" + outputstack.Collectible.Code.Path;
                outputhelptext = $"{Lang.Get("vinteng:gui-word-crafting")} {outputstack.Collectible.GetHeldItemName(outputstack)}";
            }
            else if (_nuggetType != null)
            {
                ItemStack outputstack = _nuggetType;
                //string langcode = outputstack.Collectible.Code.Domain != null ? outputstack.Collectible.Code.Domain : "";
                //langcode += ":" + outputstack.Collectible.ItemClass.ToString().ToLowerInvariant();
                //langcode += "-" + outputstack.Collectible.Code.Path;
                outputhelptext = $"{Lang.Get("vinteng:gui-word-crafting")} {outputstack.StackSize} {outputstack.Collectible.GetHeldItemName(outputstack)}";
            }
            else if (_grindingproperties != null)
            {
                ItemStack outputstack = _grindingproperties.GroundStack.ResolvedItemstack;
                //string langcode = outputstack.Collectible.Code.Domain != null ? outputstack.Collectible.Code.Domain : "";
                //langcode += ":" + outputstack.Collectible.ItemClass.ToString().ToLowerInvariant();
                //langcode += "-" + outputstack.Collectible.Code.Path;
                outputhelptext = $"{Lang.Get("vinteng:gui-word-crafting")} {outputstack.Collectible.GetHeldItemName(outputstack)}";
            }
            else
            {
                if (becrusher.Electric.IsSleeping)
                {
                    outputhelptext = Lang.Get("vinteng:gui-no-valid-recipe");    // third is a VALID recipe
                }
                if (Inventory[0].Empty)
                {
                    outputhelptext = Lang.Get("vinteng:gui-machine-ingredients");// second priority is an ingredient
                }
                if (!becrusher.HasRoomInOutput(0, null))
                {
                    outputhelptext = Lang.Get("vinteng:gui-machine-isfull");   // an output is full...                    
                }
            }
            if (!becrusher.Electric.IsEnabled)
            {
                outputhelptext = Lang.Get("vinteng:gui-machine-off");
            }
            return outputhelptext;
        }

        private void SendInvPacket(object obj)
        {
            this.capi.Network.SendBlockEntityPacket(BlockEntityPosition.X, BlockEntityPosition.Y, BlockEntityPosition.Z, obj);
        }

        private void OnTitleBarClosed()
        {
            this.TryClose();
        }

        private void OnPowerDraw(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        {
            ctx.Save();
            Matrix i = ctx.Matrix;

            ctx.Matrix = i;
            VintageEngineering.GUI.IconHelper.VerticalBar(ctx, 30, 100, 2.0, true, true);
            ulong curPower = _currentPower;
            ulong maxPower = _maxPower;
            double percentFilled = (double)curPower / (double)maxPower;
            // 100 is the height of the bar
            double percentRemaining = (double)(100D - 100D * percentFilled);

            ctx.Rectangle(0, percentRemaining, 30, 100 - percentRemaining);
            ctx.Clip();
            LinearGradient gradient = new LinearGradient(0, GuiElement.scaled(100), 0, 0);
            gradient.AddColorStop(0.0, new Color(1.0, 0.0, 0, 1.0));
            gradient.AddColorStop(1.0, new Color(0.0, 1.0, 0, 1.0));
            ctx.SetSource(gradient);
            VintageEngineering.GUI.IconHelper.VerticalBar(ctx, 30, 100, 0, false, false);
            gradient.Dispose();
            ctx.Restore();
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            Inventory.SlotModified += OnSlotModified;
        }

        public override void OnGuiClosed()
        {
            Inventory.SlotModified -= OnSlotModified;
            SingleComposer.GetSlotGrid("inputSlot").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("outputSlots").OnGuiClosed(capi);
            SingleComposer.GetDropDown("crushmode").Dispose();
            base.OnGuiClosed();
        }
    }
}
