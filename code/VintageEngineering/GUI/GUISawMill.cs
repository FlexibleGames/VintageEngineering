﻿using Cairo;
using System;
using VintageEngineering.RecipeSystem.Recipes;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace VintageEngineering
{
    public class GUISawMill : GuiDialogBlockEntity
    {
        private BESawmill bemachine;
        private RecipeSawMill currentRecipe;
        private ulong _currentPower;
        private ulong _maxPower;
        private float _craftProgress;


        public GUISawMill(string dialogTitle, InventoryBase inventory, BlockPos blockEntityPos, ICoreClientAPI capi, BESawmill bentity) : base(dialogTitle, inventory, blockEntityPos, capi)
        {
            if (base.IsDuplicate)
            {
                return;
            }
            capi.World.Player.InventoryManager.OpenInventory(inventory);
            bemachine = bentity;
            _craftProgress = bemachine.RecipeProgress;
            _currentPower = bemachine.Electric.CurrentPower;
            _maxPower = bemachine.Electric.MaxPower; // set this once as it doesn't/shouldn't change (for now)
            this.SetupDialog();
        }
        private void OnSlotModified(int slotid)
        {
            this.capi.Event.EnqueueMainThreadTask(new Action(this.SetupDialog), "setupsawmilldlg");
        }

        public void SetupDialog()
        {
            int titlebarheight = 31;
            double slotpadding = GuiElementItemSlotGridBase.unscaledSlotPadding; // typically 3

            ElementBounds dialogBounds = ElementBounds.Fixed(365, 150 + titlebarheight);
            ElementBounds dialog = ElementBounds.Fill.WithFixedPadding(0);
            dialog.BothSizing = ElementSizing.FitToChildren;

            ElementBounds powerInset = ElementBounds.Fixed(10, 8 + titlebarheight, 34, 104);
            ElementBounds powerBounds = ElementBounds.Fixed(12, 10 + titlebarheight, 30, 100);

            ElementBounds inputGrid = ElementStdBounds.SlotGrid(EnumDialogArea.None, 54, 50 + titlebarheight, 1, 1);

            // NEW:
            ElementBounds moldinset = ElementBounds.Fixed(117, 8 + titlebarheight, 74, 74);
            ElementBounds moldtext = ElementBounds.Fixed(117, 10 + titlebarheight, 70, 16);
            ElementBounds moldslot = ElementStdBounds.SlotGrid(EnumDialogArea.None, 130, 29 + titlebarheight, 1, 1);

            ElementBounds progressBar = ElementBounds.Fixed(109, 88 + titlebarheight, 90, 23);

            // NEW:
            ElementBounds progressinset = ElementBounds.Fixed(117, 117 + titlebarheight, 74, 21);

            ElementBounds progressText = ElementBounds.Fixed(117, 117 + titlebarheight, 74, 21);

            ElementBounds enableBtn = ElementStdBounds.ToggleButton(10, 117 + titlebarheight, 92, 21);
            ElementBounds enableBtnText = ElementBounds.Fixed(10, 118 + titlebarheight, 92, 20);

            ElementBounds outputGrid = ElementStdBounds.SlotGrid(EnumDialogArea.None, 206, 8 + titlebarheight, 3, 1);
            ElementBounds outputtxtinset = ElementBounds.Fixed(206, 62 + titlebarheight, 147, 76);
            ElementBounds outputtextbnds = ElementBounds.Fixed(208, 60 + titlebarheight, 143, 74);

            dialog.WithChildren(new ElementBounds[]
            {
                dialogBounds,
                powerInset,
                powerBounds,
                inputGrid,
                moldinset,
                moldtext,
                moldslot,
                progressBar,
                progressinset,
                progressText,
                enableBtn,
                enableBtnText,
                outputGrid,
                outputtxtinset,
                outputtextbnds
            });
            ElementBounds window = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle)
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

            CairoFont outputFont = CairoFont.WhiteSmallText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Center);
            CairoFont centerwhite = CairoFont.WhiteSmallText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Center);
            double[] yellow = new double[3] { 1, 1, 0 }; // Yellow?
            outputFont.WithColor(yellow);
            string enablebuttontext = this.bemachine.Electric.IsEnabled ? Lang.Get("vinteng:gui-turn-off") : Lang.Get("vinteng:gui-turn-on");

            CairoFont outputfont = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Left);


            this.SingleComposer = capi.Gui.CreateCompo("vesawmilldlg" + (blockPos?.ToString()), window)
                .AddShadedDialogBG(dialog, true, 5)
                .AddDialogTitleBar(Lang.Get("vinteng:gui-title-sawmill"), new Action(OnTitleBarClose), null, null)
                .BeginChildElements(dialog)

                .AddInset(powerInset, 2, 0.85f)
                .AddDynamicCustomDraw(powerBounds, new DrawDelegateWithBounds(this.OnPowerDraw), "powerDrawer")

                .AddItemSlotGrid(Inventory, new Action<object>(SendInvPacket), 1, new int[] { 0 }, inputGrid, "inputSlot")

                .AddInset(moldinset, 2, 0f)
                .AddStaticText(Lang.Get("vinteng:gui-sawblade-text"), centerwhite, EnumTextOrientation.Center, moldtext)
                .AddItemSlotGrid(Inventory, new Action<object>(SendInvPacket), 1, new int[] { 1 }, moldslot, "moldSlot")

                .AddDynamicCustomDraw(progressBar, new DrawDelegateWithBounds(OnProgressDraw), "progressBar")

                .AddInset(progressinset, 2, 0f)
                .AddDynamicText(GetProgressText(), outputFont, progressText, "progressText")

                .AddSmallButton("", new ActionConsumable(EnableButtonClick), enableBtn, EnumButtonStyle.Small, "enableButton")
                .AddDynamicText(enablebuttontext, centerwhite, enableBtnText, "enableBtnText")

                .AddItemSlotGrid(Inventory, new Action<object>(SendInvPacket), 3, new int[] { 2, 3, 4 }, outputGrid, "outputSlot")
                .AddInset(outputtxtinset, 2, 0f)
                .AddDynamicText(GetHelpText(), outputfont, outputtextbnds, "outputText")

                .EndChildElements()
                .Compose(true);
        }

        private string GetHelpText()
        {
            string outputhelptext = "";
            if (currentRecipe != null)
            {
                ItemStack outputstack = currentRecipe.Outputs[0].ResolvedItemstack;
                string langcode = outputstack.Collectible.Code.Domain != null ? outputstack.Collectible.Code.Domain : "";
                langcode += ":" + outputstack.Collectible.ItemClass.ToString().ToLowerInvariant();
                langcode += "-" + outputstack.Collectible.Code.Path;
                outputhelptext = $"{Lang.Get("vinteng:gui-word-crafting")} {Lang.Get(langcode)}";

            }
            else
            {
                if (bemachine.Electric.IsSleeping || currentRecipe == null)
                {
                    outputhelptext = Lang.Get("vinteng:gui-no-valid-recipe");    // third is a VALID recipe
                }
                if (Inventory[0].Empty)
                {
                    outputhelptext = Lang.Get("vinteng:gui-machine-ingredients");// second priority is an ingredient
                }
                if (!bemachine.HasRoomInOutput(2) || !bemachine.HasRoomInOutput(3) || !bemachine.HasRoomInOutput(4))
                {
                    outputhelptext = Lang.Get("vinteng:gui-machine-isfull");   // an output is full...                    
                }
                if (Inventory[1].Empty)
                {
                    outputhelptext = Lang.Get("vinteng:gui-sawblade");   // first priority is a blade                    
                }
            }
            if (!bemachine.Electric.IsEnabled)
            {
                outputhelptext = Lang.Get("vinteng:gui-machine-off");
            }
            return outputhelptext;
        }

        private string GetProgressText()
        {
            string outputstring = "";
            float craftPercent = _craftProgress * 100;
            if (bemachine.Electric.IsSleeping) // machine is sleeping if on and not crafting
            {
                outputstring = Lang.Get("vinteng:gui-is-sleeping-short");
            }
            else
            {
                outputstring = $"{craftPercent:N1}%";
            }
            if (!bemachine.Electric.IsSleeping && !bemachine.IsCrafting) // these SHOULD be mutually exclusive
            {
                outputstring = $"Error";
            }
            if (!bemachine.Electric.IsEnabled)
            {
                outputstring = Lang.Get("vinteng:gui-word-off");
            }
            return outputstring;
        }

        private bool EnableButtonClick()
        {
            capi.Network.SendBlockEntityPacket(base.BlockEntityPosition, 1002, null);
            return true;
        }

        private void SendInvPacket(object packet)
        {
            this.capi.Network.SendBlockEntityPacket(BlockEntityPosition.X, BlockEntityPosition.Y, BlockEntityPosition.Z, packet);
        }

        private void OnProgressDraw(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        {
            //if (_craftProgress == 0) return;

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
            //            ctx.Save();
        }

        private void OnPowerDraw(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        {
            //            double top = 0;
            ctx.Save();
            Matrix i = ctx.Matrix;
            //            i.Translate(GuiElement.scaled(0.0), GuiElement.scaled(top));
            //            i.Scale(1, 1);
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
            //           ctx.Save();
        }

        public void Update(float craftProgress, ulong curPower, RecipeSawMill mprecipe = null)
        {
            this._craftProgress = craftProgress;
            this._currentPower = curPower;
            if (!this.IsOpened()) return;

            if (base.SingleComposer != null)
            {
                base.SingleComposer.GetDynamicText("progressText").SetNewText(GetProgressText());
                base.SingleComposer.GetCustomDraw("powerDrawer").Redraw();
                base.SingleComposer.GetCustomDraw("progressBar").Redraw();
                base.SingleComposer.GetDynamicText("enableBtnText").SetNewText(bemachine.Electric.IsEnabled ? Lang.Get("vinteng:gui-turn-off") : Lang.Get("vinteng:gui-turn-on"));
                currentRecipe = mprecipe;
                base.SingleComposer.GetDynamicText("outputText").SetNewText(GetHelpText());
            }
        }

        private void OnTitleBarClose()
        {
            this.TryClose();
        }
        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            base.Inventory.SlotModified += this.OnSlotModified;
        }
        public override void OnGuiClosed()
        {
            base.Inventory.SlotModified -= this.OnSlotModified;
            base.SingleComposer.GetSlotGrid("inputSlot").OnGuiClosed(this.capi);
            base.SingleComposer.GetSlotGrid("outputSlot").OnGuiClosed(capi);
            base.OnGuiClosed();
        }
    }
}
