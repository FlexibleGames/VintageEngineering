using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace VintageEngineering.TemporalStorage
{
    public class GUITemporalFrequency : GuiDialogBlockEntity
    {
        private BlockEntityTemporalContainer _betc;
        private int _frequency;
        private bool _isChest = true;

        public GUITemporalFrequency(string p_dialogTitle, InventoryBase p_inventory, BlockPos p_pos, ICoreClientAPI p_capi, BlockEntityTemporalContainer p_be, bool p_isChest = true) : base(p_dialogTitle, p_pos, p_capi)
        {
            if (base.IsDuplicate)
            {
                return;
            }
            p_capi.World.Player.InventoryManager.OpenInventory(p_inventory);
            _betc = p_be;
            _frequency = _betc.TemporalFrequency;
            _isChest = p_isChest;
            SetupDialog();
        }

        public void SetupDialog()
        {
            int titlebarheight = 31;

            ElementBounds dialogBounds = ElementBounds.Fixed(220, 95 + titlebarheight);
            ElementBounds dialog = ElementBounds.Fill.WithFixedPadding(0);
            dialog.BothSizing = ElementSizing.FitToChildren;

            ElementBounds freqtext = ElementBounds.Fixed(12, 8 + titlebarheight, 107, 20);
            ElementBounds freqinput = ElementBounds.Fixed(119, 8 + titlebarheight, 80, 20);

            ElementBounds freqslider = ElementBounds.Fixed(12, 36 + titlebarheight, 196, 21);

            ElementBounds cancelbtn = ElementStdBounds.ToggleButton(12, 65 + titlebarheight, 80, 21);
            ElementBounds cancelbtntext = ElementBounds.Fixed(12, 65 + titlebarheight, 80, 21);

            ElementBounds savebtn = ElementStdBounds.ToggleButton(128, 65 + titlebarheight, 80, 21);
            ElementBounds savebtntext = ElementBounds.Fixed(128, 65 + titlebarheight, 80, 21);

            dialog.WithChildren(new ElementBounds[]
            {
                dialogBounds, freqtext, freqinput, freqslider, cancelbtn, cancelbtntext, savebtn, savebtntext
            });
            ElementBounds window = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

            if (capi.Settings.Bool["immersiveMouseMode"])
            {
                window.WithAlignment(EnumDialogArea.RightMiddle).WithFixedAlignmentOffset(-12, 0);
            }

            CairoFont rightwhite = CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Normal).WithOrientation(EnumTextOrientation.Right);
            CairoFont centerwhite = CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Normal).WithOrientation(EnumTextOrientation.Center);
            CairoFont centerblack = CairoFont.TextInput().WithOrientation(EnumTextOrientation.Center);


            string dlgtitle = _isChest ? "vinteng:gui-title-tempchest" : "vinteng:gui-title-temptank";

            this.SingleComposer = capi.Gui.CreateCompo("vetempstorage-" + _frequency, window)
                .AddShadedDialogBG(dialog, true, 5)
                .AddDialogTitleBar(Lang.Get(dlgtitle), new Action(OnTitleBarClosed), null, null)
                .BeginChildElements(dialog)
                .AddStaticText(Lang.Get("vinteng:gui-word-frequency"), rightwhite, freqtext, "freqtext")
                .AddNumberInput(freqinput, new Action<string>(OnFreqTextChanged), centerblack, "freqinput")
                .AddSlider(new ActionConsumable<int>(OnFreqSliderChanged), freqslider, "freqslider")

                .AddButton(Lang.Get("vinteng:gui-cancel"), new ActionConsumable(CancelBtnClicked), cancelbtn, centerwhite, EnumButtonStyle.Normal, "cancelbtn")
                .AddButton(Lang.Get("vinteng:gui-save"), new ActionConsumable(SaveBtnClicked), savebtn, centerwhite, EnumButtonStyle.Normal, "savebtn")

                .EndChildElements();

            SingleComposer.GetNumberInput("freqinput").IntMode = true;
            SingleComposer.GetNumberInput("freqinput").SetValue(_frequency);
            SingleComposer.GetSlider("freqslider").SetValues(_frequency, 1, 4000, 1, "");
            SingleComposer.Compose(true);
        }

        private void OnFreqTextChanged(string newtext)
        {
            if (int.TryParse(newtext, NumberStyles.Integer, GlobalConstants.DefaultCultureInfo, out _frequency))
            {
                _frequency = GameMath.Clamp(_frequency, 1, 4000);

                SingleComposer.GetSlider("freqslider").SetValue(_frequency);
            }
        }

        private bool OnFreqSliderChanged(int newvalue)
        {
            _frequency = newvalue;
            return true;
        }

        private bool SaveBtnClicked()
        {
            capi.Network.SendBlockEntityPacket(base.BlockEntityPosition, 1006, _frequency);
            this.TryClose();
            return true;
        }

        private bool CancelBtnClicked()
        {
            this.TryClose();
            return true;
        }

        private void OnTitleBarClosed()
        {
            CancelBtnClicked();
        }

    }
}
