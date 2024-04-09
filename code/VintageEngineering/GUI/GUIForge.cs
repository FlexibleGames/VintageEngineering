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
    public class GUIForge : GuiDialogBlockEntity
    {
        private BEForge betestmach;
        
        private ulong _currentPower;
        private ulong _maxPower;
        private float _craftProgress;
        private float _currentTemp;

        private int _tempGoal;
        private int _currentTempGoal;


        public GUIForge(string dialogTitle, InventoryBase inventory, BlockPos blockEntityPos, ICoreClientAPI capi, BEForge bentity) : base(dialogTitle, inventory, blockEntityPos, capi)
        {
            if (base.IsDuplicate)
            {
                return;
            }
            capi.World.Player.InventoryManager.OpenInventory(inventory);
            betestmach = bentity;
            _tempGoal = betestmach.tempGoal;
            _craftProgress = betestmach.RecipeProgress;
            _currentPower = betestmach.CurrentPower;
            _maxPower = betestmach.MaxPower; // set this once as it doesn't/shouldn't change (for now)
            
            this.SetupDialog();
        }
        private void OnSlotModified(int slotid)
        {
            this.capi.Event.EnqueueMainThreadTask(new Action(this.SetupDialog), "setupforgedlg");
        }

        public void SetupDialog()
        {
            int titlebarheight = 31;
            double slotpadding = GuiElementItemSlotGridBase.unscaledSlotPadding; // typically 3

            ElementBounds dialogBounds = ElementBounds.Fixed(315, 150 + titlebarheight);
            ElementBounds dialog = ElementBounds.Fill.WithFixedPadding(0);
            dialog.BothSizing = ElementSizing.FitToChildren;

            ElementBounds powerInset = ElementBounds.Fixed(10, 8 + titlebarheight, 34, 104);
            ElementBounds powerBounds = ElementBounds.Fixed(12, 10 + titlebarheight, 30, 100);

            ElementBounds inputGrid = ElementStdBounds.SlotGrid(EnumDialogArea.None, 54, 17 + titlebarheight, 1, 1);

            // Flames!
            ElementBounds flames = ElementBounds.Fixed(58, 67 + titlebarheight, 37, 52);

            ElementBounds progressBar = ElementBounds.Fixed(109, 30 + titlebarheight, 90, 23);
            ElementBounds progressText = ElementBounds.Fixed(111, 32 + titlebarheight, 86, 21);

            ElementBounds enableBtn = ElementStdBounds.ToggleButton(10, 121 + titlebarheight, 92, 21);
            ElementBounds enableBtnText = ElementBounds.Fixed(10, 124 + titlebarheight, 92, 20);

            ElementBounds outputGrid = ElementStdBounds.SlotGrid(EnumDialogArea.None, 206, 17 + titlebarheight, 1, 1);

            ElementBounds sliderbounds = ElementBounds.Fixed(109, 71 + titlebarheight, 196, 21);

            ElementBounds tempinset = ElementBounds.Fixed(257, 17 + titlebarheight, 48, 48);
            ElementBounds tempwords = ElementBounds.Fixed(259, 19 + titlebarheight, 46, 46);

            ElementBounds outputtxtinset = ElementBounds.Fixed(109, 95 + titlebarheight, 196, 47);
            ElementBounds outputtextbnds = ElementBounds.Fixed(111, 97 + titlebarheight, 192, 43);



            dialog.WithChildren(new ElementBounds[]
            {
                dialogBounds,
                powerInset,
                powerBounds,
                inputGrid,
                flames,
                progressBar,                
                progressText,
                enableBtn,
                enableBtnText,
                outputGrid,
                sliderbounds,
                tempinset,
                tempwords,
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
            CairoFont centerwhite = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Center);
            double[] yellow = new double[3] { 1, 1, 0 }; // Yellow?
            outputFont.WithColor(yellow);
            string enablebuttontext = this.betestmach.IsEnabled ? Lang.Get("vinteng:gui-turn-off") : Lang.Get("vinteng:gui-turn-on");

            CairoFont outputfont = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Left);


            this.SingleComposer = capi.Gui.CreateCompo("veforgedlg" + (blockPos?.ToString()), window)
                .AddShadedDialogBG(dialog, true, 5)
                .AddDialogTitleBar(Lang.Get("vinteng:gui-title-forge"), new Action(OnTitleBarClose), null, null)
                .BeginChildElements(dialog)

                .AddInset(powerInset, 2, 0.85f)
                .AddDynamicCustomDraw(powerBounds, new DrawDelegateWithBounds(this.OnPowerDraw), "powerDrawer")

                .AddItemSlotGrid(Inventory, new Action<object>(SendInvPacket), 1, new int[] { 0 }, inputGrid, "inputSlot")

                .AddDynamicCustomDraw(flames, new DrawDelegateWithBounds(OnFlameDraw), "flameDrawer")

                .AddDynamicCustomDraw(progressBar, new DrawDelegateWithBounds(OnProgressDraw), "progressBar")
                .AddDynamicText(GetProgressText(), outputFont, progressText, "progressText")

                .AddSmallButton("", new ActionConsumable(EnableButtonClick), enableBtn, EnumButtonStyle.Small, "enableButton")
                .AddDynamicText(enablebuttontext, centerwhite, enableBtnText, "enableBtnText")

                .AddItemSlotGrid(Inventory, new Action<object>(SendInvPacket), 1, new int[] { 1 }, outputGrid, "outputSlot")

                .AddInset(tempinset, 2, 0f)
                .AddDynamicText(GetTempText(), centerwhite, tempwords, "tempText")

                .AddSlider(new ActionConsumable<int>(OnTempChange), sliderbounds, "tempslider")

                .AddInset(outputtxtinset, 2, 0f)
                .AddDynamicText(GetHelpText(), outputfont, outputtextbnds, "outputText")

                .EndChildElements();
            SingleComposer.GetSlider("tempslider").SetValues(_tempGoal, 0, 1500, 25, "");
            SingleComposer.Compose(true);
        }

        private bool OnTempChange(int t1)
        {
            _tempGoal = t1;
            capi.Network.SendBlockEntityPacket(base.BlockEntityPosition, 1004, t1);
            return true;
        }

        private string GetTempText()
        {
            string output = Lang.Get("vinteng:gui-word-temp") + System.Environment.NewLine;
            if (_tempGoal == 0) output += Lang.Get("vinteng:gui-word-auto");
            else output += _tempGoal.ToString() + "°";
            return output;
        }

        private string GetHelpText()
        {
            string outputhelptext = "";
            if (_currentTempGoal != 0 && !betestmach.InputSlot.Empty)
            {
                ItemStack outputstack = betestmach.InputSlot.Itemstack.Clone();
                string langcode = outputstack.Collectible.Code.Domain != null ? outputstack.Collectible.Code.Domain : "";
                langcode += ":" + outputstack.Collectible.ItemClass.ToString().ToLowerInvariant();
                langcode += "-" + outputstack.Collectible.Code.Path;
                outputhelptext = $"{Lang.Get("vinteng:gui-word-heating")} {Lang.Get(langcode)}";

            }
            else
            {
                if (betestmach.IsSleeping)
                {
                    outputhelptext = Lang.Get("vinteng:gui-no-valid-recipe");    // third is a VALID recipe
                }
                if (Inventory[0].Empty)
                {
                    outputhelptext = Lang.Get("vinteng:gui-machine-ingredients");// second priority is an ingredient
                }
                if (!betestmach.OutputSlot.Empty)
                {
                    outputhelptext = Lang.Get("vinteng:gui-machine-isfull");   // an output is full...                    
                }
            }
            if (!betestmach.IsEnabled)
            {
                outputhelptext = Lang.Get("vinteng:gui-machine-off");
            }
            return outputhelptext;
        }

        private string GetProgressText()
        {
            string outputstring = "";
            float craftPercent = _craftProgress * 100;
            if (betestmach.IsSleeping) // machine is sleeping if on and not crafting
            {
                outputstring = Lang.Get("vinteng:gui-is-sleeping-short");
            }
            else
            {
                outputstring = $"{craftPercent:N1}%";
            }
            if (!betestmach.IsSleeping && !betestmach.IsCrafting) // these SHOULD be mutually exclusive
            {
                outputstring = $"Error";
            }
            if (!betestmach.IsEnabled)
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

        private void OnFlameDraw(Context ctx, ImageSurface surface, ElementBounds currentbounds)
        {
            ctx.Save();
            Matrix i = ctx.Matrix;
            i.Scale(GuiElement.scaled(0.25), GuiElement.scaled(0.25));
            ctx.Matrix = i;
            capi.Gui.Icons.DrawFlame(ctx, 3, true, true); // draws the outline

            if (betestmach.IsHeating)
            {
                LinearGradient gradient = new LinearGradient(0.0, GuiElement.scaled(60), 0.0, 0.0);
                gradient.AddColorStop(0.0, new Color(1.0, 1.0, 0.0, 1.0));
                gradient.AddColorStop(1.0, new Color(1.0, 0.0, 0.0, 1.0));
                ctx.SetSource(gradient);
                this.capi.Gui.Icons.DrawFlame(ctx, 0.0, false, false); // draws the colors
                gradient.Dispose();
            }
            ctx.Restore();
        }

        public void Update(float craftProgress, ulong curPower, float curTemp, int curTempGoal, int tempGoal)
        {
            _craftProgress = craftProgress;
            _currentPower = curPower;
            _currentTemp = curTemp;
            _currentTempGoal = curTempGoal;
            _tempGoal = tempGoal;

            if (!this.IsOpened()) return;

            if (base.SingleComposer != null)
            {
                base.SingleComposer.GetDynamicText("progressText").SetNewText(GetProgressText());
                base.SingleComposer.GetCustomDraw("powerDrawer").Redraw();
                base.SingleComposer.GetCustomDraw("progressBar").Redraw();
                base.SingleComposer.GetCustomDraw("flameDrawer").Redraw();
                base.SingleComposer.GetDynamicText("enableBtnText").SetNewText(betestmach.IsEnabled ? Lang.Get("vinteng:gui-turn-off") : Lang.Get("vinteng:gui-turn-on"));                
                base.SingleComposer.GetDynamicText("outputText").SetNewText(GetHelpText());
                base.SingleComposer.GetDynamicText("tempText").SetNewText(GetTempText());
                base.SingleComposer.GetSlider("tempslider").SetValue(_tempGoal);
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
