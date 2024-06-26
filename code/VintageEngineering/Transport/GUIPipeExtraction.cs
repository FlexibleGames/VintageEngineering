using Cairo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.RecipeSystem.Recipes;
using VintageEngineering.Transport.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace VintageEngineering.Transport
{
    public class GUIPipeExtraction: GuiDialogBlockEntity
    {
        private BEPipeBase bepipe;
        private PipeExtractionNode _node;
        private int _faceIndex;

        public GUIPipeExtraction(string dialogTitle, InventoryBase inventory, BlockPos blockEntityPos, ICoreClientAPI capi, BEPipeBase bentity, PipeExtractionNode node, int faceindex) : base(dialogTitle, inventory, blockEntityPos, capi)
        {
            if (base.IsDuplicate) return;            

            capi.World.Player.InventoryManager.OpenInventory(inventory);
            _node = node;
            bepipe = bentity;
            _faceIndex = faceindex;

            SetupDialog();
        }

        private void OnSlotModified(int slotid)
        {
            capi.Event.EnqueueMainThreadTask(new Action(SetupDialog), "setuppipedlg");
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

            ElementBounds upgradeInset = ElementBounds.Fixed(6, 6 + titlebarheight, 74, 74);
            ElementBounds upgradeText = ElementBounds.Fixed(8, 8 + titlebarheight, 70, 18);
            ElementBounds upgradeGrid = ElementStdBounds.SlotGrid(EnumDialogArea.None, 18, 28 + titlebarheight, 1, 1);

            ElementBounds filterInset = ElementBounds.Fixed(92, 6 + titlebarheight, 74, 74);
            ElementBounds filterText = ElementBounds.Fixed(94, 8 + titlebarheight, 70, 18);
            ElementBounds filterGrid = ElementStdBounds.SlotGrid(EnumDialogArea.None, 105, 28 + titlebarheight, 1, 1);

            ElementBounds distroText = ElementBounds.Fixed(172, 6 + titlebarheight, 132, 18);
            ElementBounds dropdownbounds = ElementBounds.Fixed(172, 28 + titlebarheight, 132, 25);

            ElementBounds outputtxtinset = ElementBounds.Fixed(6, 86 + titlebarheight, 298, 58);
            ElementBounds outputtextbnds = ElementBounds.Fixed(8, 88 + titlebarheight, 294, 54);

            dialog.WithChildren(new ElementBounds[]
            {
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
            CairoFont centerwhite = CairoFont.WhiteSmallText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Center);
            double[] yellow = new double[3] { 1, 1, 0 };
            CairoFont leftyellow = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Left).WithColor(yellow);
            CairoFont rightwhite = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Right);

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

                .EndChildElements()
                .Compose(true);
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

        public void Update()
        {
            // TODO THINGS IN HERE?

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
            }
            else
            {
                ItemPipeUpgrade upgrade = _node.Upgrade.Itemstack.Collectible as ItemPipeUpgrade;
                string can_filter = upgrade.CanFilter ? Lang.Get("vinteng:gui-word-on") : Lang.Get("vinteng:gui-word-off");
                string can_distro = upgrade.CanChangeDistro ? Lang.Get("vinteng:gui-word-on") : Lang.Get("vinteng:gui-word-off");
                outputhelptext = $"{Lang.Get("vinteng:gui-word-filter")} : {can_filter}{System.Environment.NewLine}";
                outputhelptext += $"{Lang.Get("vinteng:gui-distro-text")} : {can_distro}";
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

            this.capi.Network.SendBlockEntityPacket(BlockEntityPosition.X, BlockEntityPosition.Y, BlockEntityPosition.Z, 1005, custompacket.ToBytes());
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

        public override void OnGuiClosed()
        {
            Inventory.SlotModified -= OnSlotModified;
            SingleComposer.GetSlotGrid("upgradeslot").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("filterslot")?.OnGuiClosed(capi);
            SingleComposer.GetDropDown("distromode")?.Dispose();
            base.OnGuiClosed();
        }
    }
}
