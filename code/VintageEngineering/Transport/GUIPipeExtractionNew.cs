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
    public class GUIPipeExtractionNew : GuiDialogBlockEntity
    {
        private BEPipeBaseNew bepipe;

        private PipeExtractionNode _node;
        private int _faceIndex;
        private string _subnet; // THIS IS NOT AN ITEM FILTER
        //private bool _isCustom; // allows the player to edit the subnet
        private BlockPos _pos;
        //public DummyInventory _subnetItem;

        public GUIPipeExtractionNew(string dialogTitle, InventoryBase inventory, BlockPos blockEntityPos, ICoreClientAPI capi, BEPipeBaseNew bentity, PipeExtractionNode node, int faceindex) : base(dialogTitle, inventory, blockEntityPos, capi)
        {
            if (base.IsDuplicate) return;
            //_subnetItem = new DummyInventory(_capi, 1);
            //_subnetItem[0].MaxSlotStackSize = 1;
            
            //_capi.World.Player.InventoryManager.OpenInventory(inventory);
            _node = node;
            bepipe = bentity;
            _faceIndex = faceindex;
            _subnet = node.SubNet;
            _pos = blockEntityPos;
            //_isCustom = _subnet.Contains(":custom1");
            //if (_isCustom) _subnet = _subnet.Replace(":custom1", "");

            //if (!_isCustom && _subnet != string.Empty)
            //{
            //    ItemStack subnetstack;
            //    if (_capi.World.Collectibles.Exists(x => x.Code.Path == _subnet)) // how expensive is this?
            //    {
            //        CollectibleObject match = _capi.World.Collectibles.Find(x => x.Code.Path == _subnet); // and this?
            //        subnetstack = new ItemStack(match);
            //        _subnetItem[0].Itemstack = subnetstack;
            //    }
            //}
            SetupDialog();
        }

        private void OnSlotModified(int slotid)
        {
            if (slotid == 0 || slotid == 1)
            {
                capi.Event.EnqueueMainThreadTask(new Action(SetupDialog), "setuppipedlg");
            }
        }

        private void OnSubnetSlotModified(int slotid)
        {
            if (slotid == 0)
            {
                capi.Event.EnqueueMainThreadTask(new Action(SetupDialog), "setuppipedlg");
            }
        }

        public void SetupDialog()
        {
            //ItemSlot hoveredSlot = _capi.World.Player.InventoryManager.CurrentHoveredSlot;
            //if (hoveredSlot != null && hoveredSlot.Inventory == base.Inventory)
            //{
            //   // _capi.Input.TriggerOnMouseLeaveSlot(hoveredSlot);
            //}
            //else hoveredSlot = null;

            int titlebarheight = 31;
            double slotpadding = GuiElementItemSlotGridBase.unscaledSlotPadding;

            ElementBounds dialogBounds = ElementBounds.Fixed(315, 230 + titlebarheight);
            ElementBounds dialog = ElementBounds.Fill.WithFixedPadding(0);
            dialog.BothSizing = ElementSizing.FitToChildren;

            ElementBounds upgradeInset = ElementBounds.Fixed(6, 6 + titlebarheight, 74, 74);
            ElementBounds upgradeText = ElementBounds.Fixed(8, 8 + titlebarheight, 70, 18);
            ElementBounds upgradeGrid = ElementStdBounds.SlotGrid(EnumDialogArea.None, 18, 28 + titlebarheight, 1, 1);

            ElementBounds filterInset = ElementBounds.Fixed(92, 6 + titlebarheight, 74, 74);
            ElementBounds filterText = ElementBounds.Fixed(94, 8 + titlebarheight, 70, 18);
            ElementBounds filterGrid = ElementStdBounds.SlotGrid(EnumDialogArea.None, 105, 28 + titlebarheight, 1, 1);

            ElementBounds distroText = ElementBounds.Fixed(172, 6 + titlebarheight, 132, 18);
            ElementBounds dropdownbounds = ElementBounds.Fixed(172, 28 + titlebarheight, 132, 25);

            ElementBounds outputtxtinset = ElementBounds.Fixed(6, 86 + titlebarheight, 298, 72);
            ElementBounds outputtextbnds = ElementBounds.Fixed(8, 88 + titlebarheight, 294, 68);

            // Subnet Bounds
            int subnetstarty = 155;
            //ElementBounds subnetInset = ElementBounds.Fixed(6, 6 + titlebarheight + subnetstarty, 74, 74);
            //ElementBounds subnetText = ElementBounds.Fixed(8, 8 + titlebarheight + subnetstarty, 70, 18);
            //ElementBounds subnetGrid = ElementStdBounds.SlotGrid(EnumDialogArea.None, 18, 28 + titlebarheight + subnetstarty, 1, 1);

            //ElementBounds switchCustom = ElementBounds.Fixed(86, 8 + titlebarheight + subnetstarty, 18, 18);
            //ElementBounds switchText = ElementBounds.Fixed(109, 8 + titlebarheight + subnetstarty, 195, 18);

            ElementBounds customTextLbl = ElementBounds.Fixed(6, 6 + titlebarheight + subnetstarty, 298, 18);
            ElementBounds customTextBox = ElementBounds.Fixed(6, 26 + titlebarheight + subnetstarty, 298, 21);

            dialog.WithChildren(
            [
                dialogBounds,
                upgradeInset,
                upgradeText,
                upgradeGrid,
                filterInset,
                filterText,
                filterGrid,
                distroText,
                dropdownbounds,
                outputtxtinset,
                outputtextbnds,
                //subnetInset,
                //subnetText,
                //subnetGrid,
                //switchCustom,
                //switchText,
                customTextLbl,
                customTextBox
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
            BlockPos blockPos = base.BlockEntityPosition;
            CairoFont centerwhite = CairoFont.WhiteSmallText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Center);
            double[] yellow = new double[3] { 1, 1, 0 };
            CairoFont leftyellow = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Left).WithColor(yellow);
            CairoFont rightwhite = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Right);
            CairoFont leftwhite = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Left);

            this.SingleComposer = capi.Gui.CreateCompo("vepipedlg" + blockPos?.ToString() + ":" + _node.FaceCode, window)
                .AddShadedDialogBG(dialog, true, 5)
                .AddDialogTitleBar(DialogTitle, new Action(OnTitleBarClosed), null, null)
                .BeginChildElements(dialog)

                // Upgrade Slot
                .AddInset(upgradeInset, 2, 0.85f)
                .AddStaticText(Lang.Get("vinteng:gui-word-upgrade"), centerwhite, upgradeText, "upgradetext")
                .AddItemSlotGrid(Inventory, new Action<object>(SendInvPacket), 1, new int[] { 0 }, upgradeGrid, "upgradeslot")

                // Filter slot
                .AddIf(_node.CanFilter)
                .AddInset(filterInset, 2, 0.85f)
                .AddStaticText(Lang.Get("vinteng:gui-word-filter"), centerwhite, filterText, "filtertext")
                .AddItemSlotGrid(Inventory, new Action<object>(SendInvPacket), 1, new int[] { 1 }, filterGrid, "filterslot")
                .EndIf()

                // Distro Drop Down
                .AddIf(_node.CanChangeDistro)
                .AddStaticText(Lang.Get("vinteng:gui-distro-text") + ":", rightwhite, distroText, "distroText")
                .AddDropDown(
                            new string[] { "nearest", "farthest", "robin", "random" },
                            new string[] { Lang.Get("vinteng:gui-distromode-nearest"), Lang.Get("vinteng:gui-distromode-farthest"), Lang.Get("vinteng:gui-distromode-robin"), Lang.Get("vinteng:gui-distromode-random") },
                            GetDistroIndex(), OnSelectionChanged, dropdownbounds, "distromode")
                .EndIf()

                .AddInset(outputtxtinset, 2, 0f)
                .AddDynamicText(GetHelpText(), leftyellow, outputtextbnds, "outputText")

                // New Subnet System!! \o/ Hopefully people like this feature, added about 20 hours onto this process.
                //.AddIf(!_isCustom)
                //.AddInset(subnetInset, 2, 0f)
                //.AddStaticText(Lang.Get("vinteng:gui-word-subnet"), centerwhite, subnetText, "subnettext")
                //.AddItemSlotGrid(_subnetItem, new Action<object>(SendSubnetInvPacket), 1, [0], subnetGrid, "subnetslot")
                //.EndIf()

                //.AddSwitch(new Action<bool>(OnCustomToggle), switchCustom, "switchcustom", 18, 0)
                //.AddStaticText(Lang.Get("vinteng:gui-editsubnet"), leftwhite, switchText, "switchtext")

                //.AddIf(_isCustom)
                .AddStaticText(Lang.Get("vinteng:gui-word-subnet"), leftwhite, customTextLbl, "customtextlabel")
                .AddTextInput(customTextBox, new Action<string>(OnCustomChanged), leftwhite, "customtext")
                //.EndIf()

                .EndChildElements()
                .Compose(true);
            //SingleComposer.GetSwitch("switchcustom").SetValue(_isCustom);
            SingleComposer.GetTextInput("customtext").SetValue(_subnet, true);
        }
        
        private void OnSelectionChanged(string code, bool selected)
        {
            TreeAttribute tree = new TreeAttribute();
            tree.SetString("distro", code);
            tree.SetString("face", _node.FaceCode);
            tree.SetBlockPos("position", _node.BlockPosition);

            byte[] testbytes = tree.ToBytes();
            capi.Network.SendBlockEntityPacket(base.BlockEntityPosition, 1003, testbytes);
        }
        private void OnCustomToggle(bool toggle)
        {
            //_isCustom = toggle;
            //if (!toggle) _subnet = _subnetItem[0].Empty ? string.Empty : _subnetItem[0].Itemstack.Collectible.Code.Path;

            //if (toggle)
            //{
            //    _subnet = _subnetItem[0].Empty ? Lang.Get("vinteng:gui-editsubnet") : _subnetItem[0].Itemstack.Collectible.Code.Path;
            //    _subnet += ":custom1"; // encode a custom flag into the code
            //    _subnetItem.Clear();
            //}
            //SingleComposer.GetSwitch("switchcustom").SetValue(_isCustom);
            //OnCustomChanged(_subnet);
            //_capi.Event.EnqueueMainThreadTask(new Action(SetupDialog), "setuppipeinsdlg");
        }
        private void OnCustomChanged(string change)
        {
            TreeAttribute custompacket = new TreeAttribute();
            custompacket.SetInt("faceindex", _faceIndex);
            custompacket.SetString("customsubnet", change);
            custompacket.SetString("nodetype", "extract");
            capi.Network.SendBlockEntityPacket(_pos, 5005, custompacket.ToBytes());
            Update();
        }

        public void Update()
        {
            if (!IsOpened()) return;

            if (base.SingleComposer != null)
            {
                SingleComposer.GetDynamicText("outputText").SetNewText(GetHelpText());
            }
        }

        private int GetDistroIndex()
        {
            switch (_node.PipeDistribution)
            {
                case EnumPipeDistribution.Nearest: return 0;
                case EnumPipeDistribution.Farthest: return 1;
                case EnumPipeDistribution.RoundRobin: return 2;
                case EnumPipeDistribution.Random: return 3;
                default: return 0;
            }
        }

        private string GetHelpText()
        {
            string outputhelptext = "";
            if (_node.Upgrade.Empty)
            {
                outputhelptext = Lang.Get("vinteng:gui-help-pipeupgrade") + System.Environment.NewLine;
                outputhelptext += $"{Lang.Get("vinteng:gui-word-filter")} : {Lang.Get("vinteng:gui-word-off")}{System.Environment.NewLine}";
                outputhelptext += $"{Lang.Get("vinteng:gui-distro-text")} : {Lang.Get("vinteng:gui-word-off")}";
                outputhelptext += Environment.NewLine + $"{Lang.Get("vinteng:gui-word-rate")} : {_node.UpgradeRate} / {Lang.Get("vinteng:gui-word-delay")} : 1000ms";
            }
            else
            {
                ItemPipeUpgrade upgrade = _node.Upgrade.Itemstack.Collectible as ItemPipeUpgrade;
                string can_filter = upgrade.CanFilter ? Lang.Get("vinteng:gui-word-on") : Lang.Get("vinteng:gui-word-off");
                string can_distro = upgrade.CanChangeDistro ? Lang.Get("vinteng:gui-word-on") : Lang.Get("vinteng:gui-word-off");
                outputhelptext = $"{Lang.Get("vinteng:gui-word-filter")} : {can_filter}{System.Environment.NewLine}";
                outputhelptext += $"{Lang.Get("vinteng:gui-distro-text")} : {can_distro}";
                string rate = (upgrade.Rate == -1) ? Lang.Get("vinteng:gui-word-stack") : upgrade.Rate.ToString("N0");
                outputhelptext += Environment.NewLine + $"{Lang.Get("vinteng:gui-word-rate")} : {rate} / {Lang.Get("vinteng:gui-word-delay")} : {upgrade.Delay}ms";
            }
            if (SingleComposer != null && SingleComposer.GetTextInput("customtext").HasFocus)
            {
                // Subnet Override
                outputhelptext = Lang.Get("vinteng:gui-help-customuse");
            }
            return outputhelptext;
        }

        private void SendInvPacket(object obj)
        {
            // might have to capture the packet and encode the face index in it.
            TreeAttribute custompacket = new TreeAttribute();
            custompacket.SetInt("faceindex", _faceIndex);
            custompacket.SetBytes("packet", Packet_ClientSerializer.SerializeToBytes(obj as Packet_Client));
            custompacket.SetInt("pid", (obj as Packet_Client).Id);

            this.capi.Network.SendBlockEntityPacket(BlockEntityPosition, 1005, custompacket.ToBytes());
        }
        private void SendSubnetInvPacket(object obj)
        {
            //TreeAttribute custompacket = new TreeAttribute();
            //custompacket.SetInt("faceindex", _faceIndex);
            //string addon = _isCustom ? ":custom1" : "";
            //custompacket.SetString("code", _subnetItem[0].Empty ? "empty" : _subnetItem[0].Itemstack.Collectible.Code.Path);
            //custompacket.SetString("nodetype", "extract");
            //this._capi.Network.SendBlockEntityPacket(BlockEntityPosition, 1006, custompacket.ToBytes());
        }
        private void OnTitleBarClosed()
        {
            this.TryClose();
        }

        public override void OnGuiOpened()
        {
            //this.OpenSound ??= new AssetLocation("game:sounds/block/chestopen");
            base.OnGuiOpened();
            Inventory.SlotModified += OnSlotModified;
            //_subnetItem.SlotModified += OnSubnetSlotModified;
        }

        public override void OnGuiClosed()
        {
            Inventory.SlotModified -= OnSlotModified;
            //_subnetItem.SlotModified -= OnSubnetSlotModified;
            SingleComposer.GetSlotGrid("upgradeslot").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("filterslot")?.OnGuiClosed(capi);
            SingleComposer.GetDropDown("distromode")?.Dispose();
            base.OnGuiClosed();
        }
    }
}
