using Cairo;
using System;
using VintageEngineering.Transport.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VintageEngineering.Transport
{
    public class GUIPipeInsertNode : GuiDialogGeneric
    {
        private BEPipeBaseNew _pipeBE;
        private PipeInsertNode _node;
        private string _subnet; // THIS IS NOT AN ITEM FILTER
        private int _faceindex;
        private BlockPos _pos;
        public IPlayer _byPlayer;

        //private bool _isCustom; // allows the player to edit the subnet
        public override bool PrefersUngrabbedMouse => false;


        public GUIPipeInsertNode(string dialogTitle, BlockPos blockEntityPos, ICoreClientAPI capi, BEPipeBaseNew bentity, PipeInsertNode node, int faceindex) : base(dialogTitle, capi)
        {
            //_subnetItem = new DummyInventory(capi, 1);
            //_subnetItem[0].MaxSlotStackSize = 1;
            _pipeBE = bentity;
            _node = node;
            _faceindex = faceindex;
            _subnet = node.SubNet;
            _pos = blockEntityPos;
            _byPlayer = capi.World.Player;
            //_isCustom = _subnet.Contains(":custom1"); // is this custom
            //if (_isCustom) _subnet = _subnet.Replace(":custom1", ""); // remove custom flag

            //if (!_isCustom && _subnet != string.Empty)
            //{
            //    ItemStack subnetstack;
            //    if (capi.World.Collectibles.Exists(x => x.Code.Path == _subnet)) // how expensive is this?
            //    {
            //        CollectibleObject match = capi.World.Collectibles.Find(x => x.Code.Path == _subnet); // and this?
            //        subnetstack = new ItemStack(match);
            //        _subnetItem[0].Itemstack = subnetstack;
            //    }
            //}            
            SetupDialog();
        }

        public override bool TryOpen()
        {
            return !this.IsOpened() && base.TryOpen(true);
        }

        private void OnSlotModified(int slotid)
        {
            if (slotid == 0)
            {
                capi.Event.EnqueueMainThreadTask(new Action(SetupDialog), "setuppipedlg");
            }
        }

        public void SetupDialog()
        {
            int titlebarheight = 31;
            double slotpadding = GuiElementItemSlotGridBase.unscaledSlotPadding;

            ElementBounds dialogBounds = ElementBounds.Fixed(310, 116 + titlebarheight);
            ElementBounds dialog = ElementBounds.Fill.WithFixedPadding(0);
            dialog.BothSizing = ElementSizing.FitToChildren;

            //ElementBounds subnetInset   = ElementBounds.Fixed(6, 6 + titlebarheight, 74, 74);
            //ElementBounds subnetText    = ElementBounds.Fixed(8, 8 + titlebarheight, 70, 18);
            //ElementBounds subnetGrid    = ElementStdBounds.SlotGrid(EnumDialogArea.None, 18, 28 + titlebarheight, 1, 1);

            //ElementBounds switchCustom  = ElementBounds.Fixed(86, 8 + titlebarheight, 18, 18);
            //ElementBounds switchText    = ElementBounds.Fixed(109, 8 + titlebarheight, 195, 18);

            ElementBounds customTextLbl = ElementBounds.Fixed(6, 6 + titlebarheight, 298, 18);
            ElementBounds customTextBox = ElementBounds.Fixed(6, 26 + titlebarheight, 298, 21);

            ElementBounds helptxtInset  = ElementBounds.Fixed(6, 53 + titlebarheight, 298, 58);
            ElementBounds helptxtText = ElementBounds.Fixed(8, 55 + titlebarheight, 294, 54);

            dialog.WithChildren(
                [
                dialogBounds,
                //subnetInset,
                //subnetText,
                //subnetGrid,
                //switchCustom,
                //switchText,
                customTextLbl,
                customTextBox,
                helptxtInset,
                helptxtText
                ]);
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
            BlockPos blockPos = _pos;
            CairoFont centerwhite = CairoFont.WhiteSmallText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Center);
            double[] yellow = new double[3] { 1, 1, 0 };
            CairoFont leftyellow = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Left).WithColor(yellow);
            CairoFont rightwhite = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Right);
            CairoFont leftwhite = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Left);

            this.SingleComposer = capi.Gui.CreateCompo("vepipeinsertdlg" + blockPos?.ToString() + ":" + _node.Facing.Code, window)
                .AddShadedDialogBG(dialog, true, 5)
                .AddDialogTitleBar(DialogTitle, new Action(OnTitleBarClosed), null, null)
                .BeginChildElements(dialog)

                // filter slot
                //.AddIf(!_isCustom)
                //.AddInset(subnetInset, 2, 0f)
                //.AddStaticText(Lang.Get("vinteng:gui-word-subnet"), centerwhite, subnetText, "subnettext")
                //.AddItemSlotGrid(_subnetItem, new Action<object>(SendInvPacket), 1, [0], subnetGrid, "subnetslot")
                //.EndIf()

                //.AddSwitch(new Action<bool>(OnCustomToggle), switchCustom, "switchcustom", 18, 0)
                //.AddStaticText(Lang.Get("vinteng:gui-editsubnet"), leftwhite, switchText, "switchtext")

                //.AddIf(_isCustom)
                .AddStaticText(Lang.Get("vinteng:gui-word-subnet"), leftwhite, customTextLbl, "customtextlabel")
                .AddTextInput(customTextBox, new Action<string>(OnCustomChanged), leftwhite, "customtext")
                //.EndIf()

                .AddInset(helptxtInset, 2, 0f)
                .AddDynamicText(GetHelpText(), leftyellow, helptxtText, "helptext")
                .EndChildElements()
                .Compose(true);
            //SingleComposer.GetSwitch("switchcustom").SetValue(_isCustom);
            SingleComposer.GetTextInput("customtext").SetValue(_subnet, true);
        }

        protected string GetHelpText()
        {
            string outputtext = "";
            //if (_isCustom)
            //{
                outputtext = Lang.Get("vinteng:gui-help-customuse");
            //}
            //else
            //{
            //    outputtext = Lang.Get("vinteng:gui-help-subnet");
            //}
            return outputtext;
        }

        public void Update()
        {
            if (!IsOpened()) return;
            if (SingleComposer != null)
            {
                SingleComposer.GetDynamicText("helptext").SetNewText(GetHelpText());
            }
        }

        protected void OnTitleBarClosed()
        {
            this.TryClose();
        }

        public override void OnGuiClosed()
        {
            //_subnetItem.SlotModified -= OnSlotModified;            
            base.OnGuiClosed();
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            //SingleComposer.FocusElement(SingleComposer.GetTextInput("customtext").TabIndex);
            //_subnetItem.SlotModified += OnSlotModified;
        }

        private void SendInvPacket(object obj)
        {
            //TreeAttribute custompacket = new TreeAttribute();
            //custompacket.SetInt("faceindex", _faceindex);
            ////string addon = _isCustom ? ":custom1" : "";
            //custompacket.SetString("code", _subnetItem[0].Empty ? "empty" : _subnetItem[0].Itemstack.Collectible.Code.Path);
            //custompacket.SetString("nodetype", "insert");
            //this.capi.Network.SendBlockEntityPacket(BlockEntityPosition, 1006, custompacket.ToBytes());
        }
        private void OnCustomToggle(bool toggle)
        {
            //_isCustom = toggle;
            //if (!toggle) _subnet = _subnetItem[0].Empty ? string.Empty : _subnetItem[0].Itemstack.Collectible.Code.Path;

            //if (toggle) 
            //{
            //    _subnet = _subnetItem[0].Empty ? "Subnet" : _subnetItem[0].Itemstack.Collectible.Code.Path;
            //    _subnet += ":custom1"; // encode a custom flag into the code
            //    _subnetItem.Clear();
            //}
            //SingleComposer.GetSwitch("switchcustom").SetValue(_isCustom);
            //OnCustomChanged(_subnet);
            //capi.Event.EnqueueMainThreadTask(new Action(SetupDialog), "setuppipeinsdlg");
        }

        private void OnCustomChanged(string change)
        {
            TreeAttribute custompacket = new TreeAttribute();
            custompacket.SetInt("faceindex", _faceindex);
            custompacket.SetString("customsubnet", change);
            custompacket.SetString("nodetype", "insert");
            capi.Network.SendBlockEntityPacket(_pos, 5006, custompacket.ToBytes());
            
        }
    }
}
