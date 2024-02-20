using System;
using System.Collections.Generic;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace VintageEngineering
{
    public class TestGenGUI : GuiDialogBlockEntity
    {
        private BETestGen betestgen;
        private long lastRedrawMS;
        private ulong _currentPower;
        private ulong _maxPower;

        public TestGenGUI(string dialogTitle, InventoryBase inventory, BlockPos blockEntityPos, ICoreClientAPI capi, BETestGen bentity) : base(dialogTitle, inventory, blockEntityPos, capi)
        {
            if (base.IsDuplicate)
            {
                return;
            }
            capi.World.Player.InventoryManager.OpenInventory(inventory);
            betestgen = bentity;
            _currentPower = betestgen.CurrentPower;
            _maxPower = betestgen.MaxPower;

            this.SetupDialog();
        }
        private void OnSlotModified(int slotid)
        {
            this.capi.Event.EnqueueMainThreadTask(new Action(this.SetupDialog), "setuptestgendlg");
        }

        public void SetupDialog()
        {
            int titlebarheight = 31;
            ElementBounds dialogBounds = ElementBounds.Fixed(250, 124+titlebarheight);
            ElementBounds dialog = ElementBounds.Fill.WithFixedPadding(0);

            ElementBounds powerInset = ElementBounds.Fixed(10, 10+titlebarheight, 34, 104);
            ElementBounds powerBounds = ElementBounds.Fixed(12, 12+titlebarheight, 30, 100);

            ElementBounds fuelGrid = ElementStdBounds.SlotGrid(EnumDialogArea.None, 54, 38+titlebarheight, 1, 1);

            ElementBounds textInset = ElementBounds.Fixed(113, 10+titlebarheight, 125, 104);
            ElementBounds textBounds = ElementBounds.Fixed(115, 12+titlebarheight, 121, 100);

            dialog.BothSizing = ElementSizing.FitToChildren;
            dialog.WithChildren(new ElementBounds[]        
            {
                dialogBounds,
                powerInset,
                powerBounds,
                fuelGrid,
                textInset,
                textBounds
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

            CairoFont outputText = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal);            
            double[] rgb = new double[3] { 1, 1, 0 }; // Yellow?
            outputText.WithColor(rgb);

            this.SingleComposer = capi.Gui.CreateCompo("vetestgendlg" + (blockPos?.ToString()), window)
                .AddShadedDialogBG(dialog, true, 5)
                .AddDialogTitleBar("TestGen", new Action(OnTitleBarClose), null, null)
                .BeginChildElements(dialog)

                .AddInset(powerInset, 2, 0.85f)
                .AddDynamicCustomDraw(powerBounds, new DrawDelegateWithBounds(this.OnDialogDraw), "powerDrawer")

                .AddItemSlotGrid(Inventory, new Action<object>(SendInvPacket), 1, new int[1], fuelGrid, "fuelSlot")

                .AddInset(textInset, 2, 0)
                .AddDynamicText("updating...", outputText, textBounds, "outputText")
                
                .EndChildElements()
                .Compose(true);
            lastRedrawMS = capi.ElapsedMilliseconds-501;
        }

        private void SendInvPacket(object packet)
        {
            this.capi.Network.SendBlockEntityPacket(BlockEntityPosition.X, BlockEntityPosition.Y, BlockEntityPosition.Z, packet);
        }

        private void OnDialogDraw(Context ctx, ImageSurface surface, ElementBounds currentBounds)
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
            ctx.Save();
        }

        public void Update(float gentemp, float burntime, ulong curpower)
        {
            if (!this.IsOpened()) return; // no need to update when the dialog isn't open.

            _currentPower = curpower;
            string newText = $"{gentemp:N1}°C \n{burntime:N1} seconds\n-----------\n  Power\n{_currentPower:N0} of\n{_maxPower:N0} Max";            
            if (capi.ElapsedMilliseconds - this.lastRedrawMS > 500L)
            {
                if (base.SingleComposer != null)
                {
                    base.SingleComposer.GetDynamicText("outputText").SetNewText(newText);
                    base.SingleComposer.GetCustomDraw("powerDrawer").Redraw();
                }
                this.lastRedrawMS = this.capi.ElapsedMilliseconds;
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
            base.SingleComposer.GetSlotGrid("fuelSlot").OnGuiClosed(this.capi);            
            base.OnGuiClosed();
        }
    }
}
