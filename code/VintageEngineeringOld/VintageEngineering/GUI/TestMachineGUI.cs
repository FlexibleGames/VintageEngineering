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
    public class TestMachineGUI : GuiDialogBlockEntity
    {
        private BETestMachine betestmach;
        private long lastRedrawMS;
        private ulong _currentPower;
        private ulong _maxPower;
        private float _craftProgress;

        
        public TestMachineGUI(string dialogTitle, InventoryBase inventory, BlockPos blockEntityPos, ICoreClientAPI capi, BETestMachine bentity) : base(dialogTitle, inventory, blockEntityPos, capi)
        {            
            if (base.IsDuplicate)
            {
                return;
            }
            capi.World.Player.InventoryManager.OpenInventory(inventory);
            betestmach = bentity;
            _maxPower = betestmach.MaxPower; // set this once as it doesn't/shouldn't change (for now)
            this.SetupDialog();
        }
        private void OnSlotModified(int slotid)
        {
            this.capi.Event.EnqueueMainThreadTask(new Action(this.SetupDialog), "setuptestmachdlg");
        }

        public void SetupDialog()
        {
            int titlebarheight = 31;
            ElementBounds dialogBounds = ElementBounds.Fixed(220, 124 + titlebarheight);
            ElementBounds dialog = ElementBounds.Fill.WithFixedPadding(0);

            ElementBounds powerInset = ElementBounds.Fixed(10, 10 + titlebarheight, 34, 104);
            ElementBounds powerBounds = ElementBounds.Fixed(12, 12 + titlebarheight, 30, 100);

            ElementBounds inputGrid = ElementStdBounds.SlotGrid(EnumDialogArea.None, 54, 42 + titlebarheight, 1, 1);

            ElementBounds progressBar = ElementBounds.Fixed(112, 61 + titlebarheight, 40, 10);
            ElementBounds progressText = ElementBounds.Fixed(107, 73 + titlebarheight, 45, 17);
            ElementBounds enableBtn = ElementBounds.Fixed(112, 95 + titlebarheight, 40, 21);
            ElementBounds enableBtnText = ElementBounds.Fixed(116, 98 + titlebarheight, 36, 20);

            ElementBounds outputGrid = ElementStdBounds.SlotGrid(EnumDialogArea.None, 162, 42 + titlebarheight, 1, 1);


            dialog.BothSizing = ElementSizing.FitToChildren;
            dialog.WithChildren(new ElementBounds[]
            {
                dialogBounds,
                powerInset,
                powerBounds,
                inputGrid,
                progressBar,
                progressText,
                enableBtn,
                outputGrid
                
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

            CairoFont outputFont = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal);
            double[] rgb = new double[3] { 1, 1, 0 }; // Yellow?
            outputFont.WithColor(rgb);

            this.SingleComposer = capi.Gui.CreateCompo("vetestgendlg" + (blockPos?.ToString()), window)
                .AddShadedDialogBG(dialog, true, 5)
                .AddDialogTitleBar("TestMachine", new Action(OnTitleBarClose), null, null)
                .BeginChildElements(dialog)

                .AddInset(powerInset, 2, 0.85f)
                .AddDynamicCustomDraw(powerBounds, new DrawDelegateWithBounds(this.OnPowerDraw), "powerDrawer")

                .AddItemSlotGrid(Inventory, new Action<object>(SendInvPacket), 1, new int[1], inputGrid, "inputSlot")

                .AddDynamicCustomDraw(progressBar, new DrawDelegateWithBounds(OnProgressDraw), "progressBar")
                .AddDynamicText("...", outputFont, progressText, "progressText")
                .AddSmallButton("", new ActionConsumable(EnableButtonClick), enableBtn, EnumButtonStyle.Small, "enableButton")
                .AddDynamicText(betestmach.IsEnabled ? "On" : "Off", CairoFont.WhiteDetailText(), enableBtnText, "enableBtnText")

                .AddItemSlotGrid(Inventory, new Action<object>(SendInvPacket), 1, new int[] { 1 }, outputGrid, "outputSlot")

                .EndChildElements()
                .Compose(true);
            lastRedrawMS = capi.ElapsedMilliseconds - 501;
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
            if (_craftProgress == 0) return;

            ctx.Save();
            Matrix i = ctx.Matrix;
            ctx.Matrix = i;
            VintageEngineering.GUI.IconHelper.HorizontalBar(ctx, new double[] { 0, 0, 0, 1 }, 1, true, true, 40, 10);

            // 0 -> 1
            float percentFilled = _craftProgress;
            // 40 is the width of the bar
            double percentRemaining = (double)(40D - 40D * percentFilled);

            ctx.Rectangle(0, 0, 40-percentRemaining, 10);
            ctx.Clip();
            LinearGradient gradient = new LinearGradient(0, 0, GuiElement.scaled(40), 0);
            gradient.AddColorStop(0.0, new Color(0.0, 0.4, 0.0, 1.0));
            gradient.AddColorStop(1.0, new Color(0.2, 0.6, 0.2, 1.0));
            ctx.SetSource(gradient);
            VintageEngineering.GUI.IconHelper.HorizontalBar(ctx, new double[] { 0, 0, 0, 1 }, 1, false, false, 40, 10);
            gradient.Dispose();
            ctx.Restore();
            ctx.Save();
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
            ctx.Save();
        }

        public void Update(float craftProgress, ulong curPower)
        {
            this._craftProgress = craftProgress;
            this._currentPower = curPower;
            if (!this.IsOpened()) return;

            if (capi.ElapsedMilliseconds - lastRedrawMS > 500L)
            {
                if (base.SingleComposer != null)
                {
                    float craftPercent = _craftProgress * 100;
                    string newText = $"{craftPercent:N1}%";
                    base.SingleComposer.GetDynamicText("progressText").SetNewText(newText);

                    base.SingleComposer.GetCustomDraw("powerDrawer").Redraw();
                    base.SingleComposer.GetCustomDraw("progressBar").Redraw();
                    base.SingleComposer.GetDynamicText("enableBtnText").SetNewText(betestmach.IsEnabled ? "On" : "Off");
                }
                this.lastRedrawMS = capi.ElapsedMilliseconds;
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
