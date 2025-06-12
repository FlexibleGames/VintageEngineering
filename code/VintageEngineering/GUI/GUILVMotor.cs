using Cairo;
using System;
using VintageEngineering.blockBhv;
using VintageEngineering.blockentity;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace VintageEngineering.GUI
{
    public class GUILVMotor : GuiDialogBlockEntity
    {
        private BEElectricKinetic _bentity;

        private ulong _currentPower;
        private ulong _maxPower;

        private float _mechSpeed;
        private float _mechResistance;

        private ulong _powerRequired;

        public GUILVMotor(string dialogTitle, InventoryBase inventory, BlockPos blockEntityPos, ICoreClientAPI capi, BEElectricKinetic p_bentity) : base(dialogTitle, inventory, blockEntityPos, capi)
        {
            if (base.IsDuplicate)
            {
                return;
            }
            capi.World.Player.InventoryManager.OpenInventory(inventory);
            _bentity = p_bentity;

            _currentPower = _bentity.Electric.CurrentPower;
            _maxPower = _bentity.Electric.MaxPower; // set this once as it doesn't/shouldn't change (for now)

            _mechSpeed = _bentity.SpeedSetting;
            _mechResistance = _bentity.ResistanceSetting;

            this.SetupDialog();
        }

        public void SetupDialog()
        {
            int titlebarheight = 31;
            double slotpadding = GuiElementItemSlotGridBase.unscaledSlotPadding; // typically 3

            ElementBounds dialogBounds = ElementBounds.Fixed(280, 180 + titlebarheight);
            ElementBounds dialog = ElementBounds.Fill.WithFixedPadding(0);
            dialog.BothSizing = ElementSizing.FitToChildren;
            
            ElementBounds powerInset = ElementBounds.Fixed(12, 12 + titlebarheight, 34, 104);
            ElementBounds powerBounds = ElementBounds.Fixed(14, 14 + titlebarheight, 30, 100);

            ElementBounds enableBtn = ElementStdBounds.ToggleButton(5, 123 + titlebarheight, 48, 21);
            ElementBounds enableBtnText = ElementBounds.Fixed(6, 124 + titlebarheight, 46, 20);
             

            //ElementBounds speedtextinset = ElementBounds.Fixed(60, 9 + titlebarheight, 48, 16);
            ElementBounds speedtext = ElementBounds.Fixed(61, 9 + titlebarheight, 196, 16);
            ElementBounds speedsliderbounds = ElementBounds.Fixed(61, 30 + titlebarheight, 196, 21);

            ElementBounds resistancetext = ElementBounds.Fixed(61, 56 + titlebarheight, 196, 16);
            ElementBounds resistancesliderbounds = ElementBounds.Fixed(61, 77 + titlebarheight, 196, 21);

            ElementBounds outputtxtinset = ElementBounds.Fixed(61, 107 + titlebarheight, 196, 47);
            ElementBounds outputtextbnds = ElementBounds.Fixed(63, 109 + titlebarheight, 192, 43);



            dialog.WithChildren(new ElementBounds[]
            {
                dialogBounds,
                powerInset, powerBounds,
                enableBtn, enableBtnText,
                speedtext, speedsliderbounds,
                resistancetext, resistancesliderbounds,
                outputtxtinset, outputtextbnds
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
            string enablebuttontext = this._bentity.Electric.IsEnabled ? Lang.Get("vinteng:gui-word-on") : Lang.Get("vinteng:gui-word-off");

            CairoFont outputfont = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Left);


            this.SingleComposer = capi.Gui.CreateCompo("velvmotordlg" + (blockPos?.ToString()), window)
                .AddShadedDialogBG(dialog, true, 5)
                .AddDialogTitleBar(Lang.Get("vinteng:gui-title-motor"), new Action(OnTitleBarClose), null, null)
                .BeginChildElements(dialog)

                .AddInset(powerInset, 2, 0.85f)
                .AddDynamicCustomDraw(powerBounds, new DrawDelegateWithBounds(this.OnPowerDraw), "powerDrawer")

                .AddSmallButton("", new ActionConsumable(EnableButtonClick), enableBtn, EnumButtonStyle.Small, "enableButton")
                .AddDynamicText(enablebuttontext, centerwhite, enableBtnText, "enableBtnText")

                //.AddInset(tempinset, 2, 0f)
                .AddDynamicText(Lang.Get("vinteng:gui-motor-targetspeed") + _mechSpeed.ToString("F2"), outputfont, speedtext, "speedText")
                .AddSlider(new ActionConsumable<int>(OnSpeedChange), speedsliderbounds, "speedslider")

                .AddDynamicText(Lang.Get("vinteng:gui-motor-targetres") + _mechResistance.ToString("F2"), outputfont, resistancetext, "resistanceText")
                .AddSlider(new ActionConsumable<int>(OnResistanceChange), resistancesliderbounds, "resistanceslider")

                .AddInset(outputtxtinset, 2, 0f)
                .AddDynamicText(GetHelpText(), outputfont, outputtextbnds, "outputText")

                .EndChildElements();
            SingleComposer.GetSlider("speedslider").SetValues(((int)(_mechSpeed*100)), 0, 100, 1, "");
            SingleComposer.GetSlider("resistanceslider").SetValues(((int)(_mechResistance * 100)), 0, 100, 1, "");            
            SingleComposer.Compose(true);
        }
        private void OnTitleBarClose()
        {
            this.TryClose();
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
        private bool EnableButtonClick()
        {
            capi.Network.SendBlockEntityPacket(base.BlockEntityPosition, 1002, null);
            return true;
        }
        private bool OnSpeedChange(int t1)
        {
            float newvalue = (float)t1 / 100;
            _mechSpeed = newvalue;
            capi.Network.SendBlockEntityPacket(base.BlockEntityPosition, 1004, t1);
            base.SingleComposer.GetDynamicText("speedText").SetNewText($"{Lang.Get("vinteng:gui-motor-targetspeed")} {_mechSpeed:F2}");
            return true;
        }
        private bool OnResistanceChange(int t1)
        {
            float newvalue = (float)t1 / 100;
            _mechResistance = newvalue;
            capi.Network.SendBlockEntityPacket(base.BlockEntityPosition, 1005, t1);
            base.SingleComposer.GetDynamicText("resistanceText").SetNewText($"{Lang.Get("vinteng:gui-motor-targetres")} {_mechResistance:F2}");
            return true;
        }
        private string GetHelpText()
        {
            string outputhelptext = "";
            ElectricKineticMotorBhv motorbeh = _bentity.Mechanical as ElectricKineticMotorBhv;            
            if (motorbeh != null) 
            {
                _powerRequired = motorbeh.ElectricalPowerRequired;
            }

            if (_bentity.Electric.MachineState == EnumBEState.On)
            {
                outputhelptext = $"{Lang.Get("vinteng:gui-powerrequired")} {_powerRequired}";
                if (_currentPower <= 10)
                {
                    outputhelptext = System.Environment.NewLine + $"{Lang.Get("vinteng:gui-machine-lowpower")}";
                }
            }
            else
            {
                if (_bentity.Electric.IsSleeping)
                {
                    outputhelptext = Lang.Get("vinteng:gui-word-sleeping");    // third is a VALID recipe
                }
            }
            if (!_bentity.Electric.IsEnabled)
            {
                outputhelptext = Lang.Get("vinteng:gui-machine-off");
            }
            return outputhelptext;
        }

        public void Update(ulong p_curpower, float p_speed, float p_resist)
        {
            _currentPower = p_curpower;
            _mechSpeed = p_speed;
            _mechResistance = p_resist;

            if (!this.IsOpened()) return;

            if (base.SingleComposer != null)
            {
                base.SingleComposer.GetDynamicText("speedText").SetNewText($"{Lang.Get("vinteng:gui-motor-targetspeed")} {_mechSpeed:F2}");
                base.SingleComposer.GetDynamicText("resistanceText").SetNewText($"{Lang.Get("vinteng:gui-motor-targetres")} {_mechResistance:F2}");
                base.SingleComposer.GetDynamicText("enableBtnText").SetNewText(_bentity.Electric.IsEnabled ? Lang.Get("vinteng:gui-word-on") : Lang.Get("vinteng:gui-word-off"));
                base.SingleComposer.GetDynamicText("outputText").SetNewText(GetHelpText());                
            }
        }
    }
}
