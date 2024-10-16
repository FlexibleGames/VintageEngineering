using Cairo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.RecipeSystem.Recipes;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VintageEngineering.GUI
{
    public class GUIBlastFurnace: GuiDialogBlockEntity
    {
        private BEBlastFurnace _bentity;
        private float _craftProgress;
        private float _currenttemp;
        private RecipeBlastFurnace _recipe;
        public GUIBlastFurnace(string dialogTitle, InventoryBase inventory, BlockPos blockEntityPos, ICoreClientAPI capi, BEBlastFurnace bentity) : base(dialogTitle, inventory, blockEntityPos, capi)
        {
            _bentity = bentity;
            _craftProgress = _bentity.RecipeProgress;
            _recipe = _bentity.CurrentRecipe;
            _currenttemp = _bentity.CurrentTemp;
            this.SetupDialog();
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

            ElementBounds dialogBounds = ElementBounds.Fixed(284, 214 + titlebarheight);
            ElementBounds dialog = ElementBounds.Fill.WithFixedPadding(0);
            dialog.BothSizing = ElementSizing.FitToChildren;

            ElementBounds inputGrid = ElementStdBounds.SlotGrid(EnumDialogArea.None, 5, 5 + titlebarheight, 4, 1);

            ElementBounds flames = ElementBounds.Fixed(10, 57 + titlebarheight, 37, 52);

            ElementBounds inputFuelGrid = ElementStdBounds.SlotGrid(EnumDialogArea.None, 5, 113 + titlebarheight, 1, 1);

            ElementBounds progressBar = ElementBounds.Fixed(122, 71 + titlebarheight, 90, 23);
            ElementBounds progressText = ElementBounds.Fixed(120, 73 + titlebarheight, 90, 23);

            ElementBounds outputGrid = ElementStdBounds.SlotGrid(EnumDialogArea.None, 231, 59 + titlebarheight, 1, 1);            

            ElementBounds outputtxtinset = ElementBounds.Fixed(58, 113 + titlebarheight, 221, 96);
            ElementBounds outputtextbnds = ElementBounds.Fixed(60, 115 + titlebarheight, 217, 92);


            dialog.WithChildren(new ElementBounds[]
            {
                dialogBounds,
                inputGrid,
                flames,
                inputFuelGrid,
                progressBar,
                progressText,
                outputGrid,                
                outputtxtinset,
                outputtextbnds
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

            CairoFont centerwhite = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Center);
            double[] yellow = new double[3] { 1, 1, 0 };
            CairoFont leftyellow = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Left).WithColor(yellow);

            this.SingleComposer = capi.Gui.CreateCompo("veblastfurnacedlg" + blockPos?.ToString(), window)
                .AddShadedDialogBG(dialog, true, 5)
                .AddDialogTitleBar(Lang.Get("vinteng:gui-title-blastfurnace"), new Action(OnTitleBarClosed), null, null)
                .BeginChildElements(dialog)

                .AddItemSlotGrid(Inventory, new Action<object>(SendInvPacket), 4, new int[] { 0,1,2,3 }, inputGrid, "inputSlot")
                .AddDynamicCustomDraw(flames, new DrawDelegateWithBounds(OnFlameDraw), "flameDrawer")
                .AddItemSlotGrid(Inventory, new Action<object>(SendInvPacket), 1, new int[] { 4 }, inputFuelGrid, "inputFuelSlot")

                .AddDynamicCustomDraw(progressBar, new DrawDelegateWithBounds(OnProgressDraw), "progressBar")
                .AddDynamicText(GetProgressText(), centerwhite, progressText, "progressText")

                .AddItemSlotGrid(Inventory, new Action<object>(SendInvPacket), 1, new int[] { 5 }, outputGrid, "outputSlots")

                .AddInset(outputtxtinset, 2, 0f)
                .AddDynamicText(GetHelpText(), leftyellow, outputtextbnds, "outputText")

                .EndChildElements()
                .Compose(true);
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

        private void OnFlameDraw(Context ctx, ImageSurface surface, ElementBounds currentbounds)
        {
            ctx.Save();
            Matrix i = ctx.Matrix;
            i.Scale(GuiElement.scaled(0.25), GuiElement.scaled(0.25));
            ctx.Matrix = i;
            capi.Gui.Icons.DrawFlame(ctx, 3, true, true); // draws the outline            

            if (_bentity.IsHeating)
            {
                float curTime = _bentity.RemainingFuel;
                float totalTime = _bentity.FuelTotalBurnTime;
                double percentFilled = (double)curTime / (double)totalTime;
                // 52 is the height of the flames
                double dy = (double)(52f - (52f * percentFilled)); // should give you the remaining height based on 

                ctx.Rectangle(0, dy * 4, 37 * 4, (52 - dy) * 4);
                ctx.Clip();

                LinearGradient gradient = new LinearGradient(0.0, GuiElement.scaled(52 * 4), 0.0, 0.0);
                gradient.AddColorStop(0.0, new Color(1.0, 1.0, 0.0, 1.0));
                gradient.AddColorStop(1.0, new Color(1.0, 0.0, 0.0, 1.0));
                ctx.SetSource(gradient);
                this.capi.Gui.Icons.DrawFlame(ctx, 0.0, false, false); // draws the colors
                gradient.Dispose();
            }
            ctx.Restore();
        }
        private string GetProgressText()
        {
            string outputstring = "";
            float craftPercent = _craftProgress * 100;
            if (_bentity.IsSleeping) // machine is sleeping if on and not crafting
            {
                outputstring = Lang.Get("vinteng:gui-is-sleeping-short");
            }
            else
            {
                outputstring = $"{craftPercent:N1}%";
            }
            if (!_bentity.IsSleeping && !_bentity.IsCrafting) // these SHOULD be mutually exclusive
            {
                outputstring = $"Error";
            }
            return outputstring;
        }
        private string GetHelpText()
        {
            string outputhelptext = "";
            outputhelptext += $"{Lang.Get("vinteng:gui-word-temp")}{_currenttemp:N1}°C";
            outputhelptext += Environment.NewLine;
            float craftstacksize = 0f;
            if (_bentity.CurrentRecipe != null)
            { 
                outputhelptext += $"{Lang.Get("vinteng:gui-word-crafting")}:{_bentity.CurrentRecipe.Outputs[0].ResolvedItemstack.StackSize} {_bentity.CurrentRecipe.Outputs[0].ResolvedItemstack.GetName()}";
                craftstacksize = _bentity.CurrentRecipe.Outputs[0].ResolvedItemstack.StackSize;
            }
            if (_bentity.CurrentAlloyRecipe != null)
            {
                craftstacksize = (float)_bentity.CurrentAlloyRecipe.GetTotalOutputQuantity(_bentity.GetIngredientStacks(capi.World, _bentity.InputSlots));
                outputhelptext += $"{Lang.Get("vinteng:gui-word-crafting")}:{craftstacksize} {_bentity.CurrentAlloyRecipe.Output.ResolvedItemstack.GetName()}";
            }
            if (_bentity.SmeltableStackRecipe != null)
            {
                craftstacksize = (float)_bentity.SmeltableStackRecipe.stackSize;
                outputhelptext += $"{Lang.Get("vinteng:gui-word-crafting")}:{craftstacksize} {_bentity.SmeltableStackRecipe.output.GetName()}";
            }

            if (_bentity.CurrentRecipe != null || _bentity.CurrentAlloyRecipe != null || _bentity.SmeltableStackRecipe != null)
            {
                bool isfraction = (craftstacksize * 100) % 100 > 0;
                if (!isfraction)
                {
                    outputhelptext += Environment.NewLine;
                    outputhelptext += $"{Lang.Get("vinteng:Smelting")} {Lang.Get("vinteng:gui-word-temp")} {_bentity.GetMeltingPoint(capi.World, _bentity.InputSlots)}";
                    outputhelptext += Environment.NewLine;
                    outputhelptext += $"{Lang.Get("vinteng:Smelting")} {Lang.Get("vinteng:gui-word-time")}: {_bentity.GetMeltingDuration(capi.World, _bentity.InputSlots)}";
                }                
                else
                {
                    outputhelptext += Environment.NewLine + $"{Lang.Get("vinteng:gui-error-unbalancedinput")}";
                }
            }
            else
            {
                if (_bentity.IsSleeping || _recipe == null)
                {
                    outputhelptext += Lang.Get("vinteng:gui-no-valid-recipe");    // third is a VALID recipe
                }
                else if (_bentity.InputsEmpty)
                {
                    outputhelptext += Lang.Get("vinteng:gui-machine-ingredients");// second priority is an ingredient
                }
                else if (_bentity.FuelSlot.Empty)
                {
                    outputhelptext += Lang.Get("vinteng:gui-machine-nofuel");// third priority is fuel
                }
                else if (!_bentity.HasRoomInOutput(0, null))
                {
                    outputhelptext += Lang.Get("vinteng:gui-machine-isfull");   // an output is full...
                }
            }
            return outputhelptext;
        }

        public void Update(float craftProgress, float curTemp, RecipeBlastFurnace recipe = null)
        {
            _craftProgress = craftProgress;
            _currenttemp = curTemp;
            _recipe = recipe;

            if (!IsOpened()) return;

            if (base.SingleComposer != null)
            {
                SingleComposer.GetDynamicText("progressText").SetNewText(GetProgressText());
                SingleComposer.GetCustomDraw("progressBar").Redraw();
                SingleComposer.GetCustomDraw("flameDrawer").Redraw();                
                SingleComposer.GetDynamicText("outputText").SetNewText(GetHelpText());
            }
        }

        private void SendInvPacket(object obj)
        {
            this.capi.Network.SendBlockEntityPacket(BlockEntityPosition.X, BlockEntityPosition.Y, BlockEntityPosition.Z, obj);
        }
        private void OnTitleBarClosed()
        {
            this.TryClose();
        }
        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            Inventory.SlotModified += OnSlotModified;
        }
        private void OnSlotModified(int slotid)
        {
            capi.Event.EnqueueMainThreadTask(new Action(SetupDialog), "setupblastfurnacedlg");
        }

        public override void OnGuiClosed()
        {
            Inventory.SlotModified -= OnSlotModified;
            SingleComposer.GetSlotGrid("inputSlot").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("inputFuelSlot").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("outputSlots").OnGuiClosed(capi);
            base.OnGuiClosed();
        }
    }
}
