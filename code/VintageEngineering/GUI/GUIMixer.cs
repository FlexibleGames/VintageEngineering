using Cairo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Datastructures;
using System.Threading.Tasks;
using VintageEngineering.RecipeSystem.Recipes;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VintageEngineering
{
    public class GUIMixer: GuiDialogBlockEntity
    {
        private BEMixer bemixer;

        private RecipeMixer _recipemixer;        

        private ulong _currentPower;
        private ulong _maxPower;
        private float _craftProgress;

        public GUIMixer(string dialogTitle, InventoryBase inventory, BlockPos blockEntityPos, ICoreClientAPI capi, BEMixer bentity) : base(dialogTitle, inventory, blockEntityPos, capi)
        {
            if (base.IsDuplicate) return;

            capi.World.Player.InventoryManager.OpenInventory(inventory);
            bemixer = bentity;
            _currentPower = bemixer.Electric.CurrentPower;
            _maxPower = bemixer.Electric.MaxPower;
            _craftProgress = bemixer.RecipeProgress;
            SetupDialog();
        }

        private void OnSlotModified(int slotid)
        {
            capi.Event.EnqueueMainThreadTask(new Action(SetupDialog), "setupmixerdlg");
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
            int fluidtankpadding = 2;
            double slotpadding = GuiElementItemSlotGridBase.unscaledSlotPadding;

            ElementBounds dialogBounds = ElementBounds.Fixed(383, 248 + titlebarheight);
            ElementBounds dialog = ElementBounds.Fill.WithFixedPadding(0);
            dialog.BothSizing = ElementSizing.FitToChildren;

            ElementBounds powerInset = ElementBounds.Fixed(10, 10 + titlebarheight, 34, 104);
            ElementBounds powerBounds = ElementBounds.Fixed(12, 12 + titlebarheight, 30, 100);

            ElementBounds inputGrid = ElementStdBounds.SlotGrid(EnumDialogArea.None, 53, 12 + titlebarheight, 2, 2);

            ElementBounds fluidinput1 = ElementBounds.Fixed(160 + fluidtankpadding, 10 + titlebarheight + fluidtankpadding, 40, 200);
            ElementBounds fluidinput2 = ElementBounds.Fixed(208 + fluidtankpadding, 10 + titlebarheight + fluidtankpadding, 40, 200);


            ElementBounds progressBar = ElementBounds.Fixed(161, 219 + titlebarheight, 90, 23);
            ElementBounds progressText = ElementBounds.Fixed(163, 221 + titlebarheight, 90, 23);

            ElementBounds enableBtn = ElementStdBounds.ToggleButton(36, 121 + titlebarheight, 92, 21);
            ElementBounds enableBtnText = ElementBounds.Fixed(38, 124 + titlebarheight, 87, 17);

            ElementBounds outputtext = ElementBounds.Fixed(280, 10 + titlebarheight, 96, 15);
            ElementBounds outputGrid = ElementStdBounds.SlotGrid(EnumDialogArea.None, 280, 27 + titlebarheight, 1, 1);
            ElementBounds fluidoutput = ElementBounds.Fixed(332 + fluidtankpadding, 27 + titlebarheight + fluidtankpadding, 40, 200);

            ElementBounds outputtxtinset = ElementBounds.Fixed(10, 147 + titlebarheight, 145, 44);
            ElementBounds outputtextbnds = ElementBounds.Fixed(12, 149 + titlebarheight, 141, 40);


            dialog.WithChildren(new ElementBounds[]
            {
                dialogBounds,
                powerInset,
                powerBounds,
                inputGrid,
                fluidinput1,
                fluidinput2,
                progressBar,
                progressText,
                enableBtn,
                enableBtnText,
                outputtext,
                outputGrid,
                fluidoutput,
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
            string enablebtnstring = bemixer.Electric.IsEnabled ? Lang.Get("vinteng:gui-turn-off") : Lang.Get("vinteng:gui-turn-on");

            CairoFont centerwhite = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Center);
            double[] yellow = new double[3] { 1, 1, 0 };
            CairoFont leftyellow = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Left).WithColor(yellow);
            CairoFont rightwhite = CairoFont.WhiteDetailText().WithWeight(FontWeight.Normal).WithOrientation(EnumTextOrientation.Right);

            this.SingleComposer = capi.Gui.CreateCompo("vemixerdlg" + blockPos?.ToString(), window)
                .AddShadedDialogBG(dialog, true, 5)
                .AddDialogTitleBar(Lang.Get("vinteng:gui-title-mixer"), new Action(OnTitleBarClosed), null, null)
                .BeginChildElements(dialog)

                .AddInset(powerInset, 2, 0.85f)
                .AddDynamicCustomDraw(powerBounds, new DrawDelegateWithBounds(OnPowerDraw), "powerDrawer")

                .AddItemSlotGrid(Inventory, new Action<object>(SendInvPacket), 2, new int[] { 0, 1, 2, 3 }, inputGrid, "inputSlot")

                // TANK INPUTS FOR THE MIXER
                .AddInset(fluidinput1.ForkBoundingParent(2,2,2,2),2, 0.85f)
                .AddDynamicCustomDraw(fluidinput1, new DrawDelegateWithBounds(DrawTankOne), "fluidtank1")
                .AddInset(fluidinput2.ForkBoundingParent(2,2,2,2), 2, 0.85f)
                .AddDynamicCustomDraw(fluidinput2, new DrawDelegateWithBounds(DrawTankTwo), "fluidtank2")

                .AddDynamicCustomDraw(progressBar, new DrawDelegateWithBounds(OnProgressDraw), "progressBar")

                .AddDynamicText(GetProgressText(), centerwhite, progressText, "progressText")

                .AddSmallButton("", new ActionConsumable(EnableButtonClick), enableBtn, EnumButtonStyle.Small, "enableButton")
                .AddDynamicText(enablebtnstring, centerwhite, enableBtnText, "enableBtnText")

                .AddItemSlotGrid(Inventory, new Action<object>(SendInvPacket), 1, new int[] { 6 }, outputGrid, "outputSlots")

                .AddInset(fluidoutput.ForkBoundingParent(2,2,2,2),2,0.85f)
                .AddDynamicCustomDraw(fluidoutput, new DrawDelegateWithBounds(DrawOutputTank), "fluidtankoutput")

                .AddInset(outputtxtinset, 2, 0f)
                .AddDynamicText(GetHelpText(), leftyellow, outputtextbnds, "outputText")
                
                .AddStaticText(Lang.Get("vinteng:gui-word-output"), centerwhite, outputtext, "output")

                .EndChildElements()
                .Compose(true);
        }

        private void DrawTankOne(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        {
            DrawTank(ctx, surface, currentBounds, 4);
        }
        private void DrawTankTwo(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        {
            DrawTank(ctx, surface, currentBounds, 5);
        }
        private void DrawOutputTank(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        {
            DrawTank(ctx, surface, currentBounds, 7);
        }

        private void DrawTank(Context ctx, ImageSurface surface, ElementBounds currentBounds, int slotnum)
        {
            ItemSlot liquidslot = Inventory[slotnum];
            if (liquidslot == null || liquidslot.Empty) return;

            float itemsPerLiter = 1f;
            int capacity = (int)(Inventory[slotnum] as ItemSlotLiquidOnly).CapacityLitres;
            WaterTightContainableProps wprops = BlockLiquidContainerBase.GetContainableProps(liquidslot.Itemstack);
            if (wprops != null)
            {
                itemsPerLiter = wprops.ItemsPerLitre;
                capacity = Math.Max(capacity, wprops.MaxStackSize);
            }
            float fullnessRelative = (float)liquidslot.StackSize / itemsPerLiter / (float)capacity;
            double offY = (double)(1f - fullnessRelative) * currentBounds.InnerHeight;
            ctx.Rectangle(0.0, offY, currentBounds.InnerWidth, currentBounds.InnerHeight - offY);
            CompositeTexture compositeTexture;
            if ((compositeTexture = ((wprops != null) ? wprops.Texture : null)) == null)
            {
                JsonObject attributes = liquidslot.Itemstack.Collectible.Attributes;
                compositeTexture = (attributes != null) ? attributes["inContainerTexture"].AsObject<CompositeTexture>(null,
                    liquidslot.Itemstack.Collectible.Code.Domain) : null;
            }
            if (compositeTexture != null)
            {
                ctx.Save();
                Matrix i = ctx.Matrix;
                i.Scale(GuiElement.scaled(3.0), GuiElement.scaled(3.0));
                ctx.Matrix = i;
                AssetLocation loc = compositeTexture.Base.Clone().WithPathAppendixOnce(".png");
                GuiElement.fillWithPattern(capi, ctx, loc, true, false, compositeTexture.Alpha, 1f);
                ctx.Restore();
            }
        }

        public void Update(float craftProgress, ulong curPower, RecipeMixer recipeMixer = null)
        {            
            _craftProgress = craftProgress;
            _currentPower = curPower;
            _recipemixer = recipeMixer;

            if (!IsOpened()) return;

            if (base.SingleComposer != null)
            {
                SingleComposer.GetDynamicText("progressText").SetNewText(GetProgressText());
                SingleComposer.GetCustomDraw("powerDrawer").Redraw();
                SingleComposer.GetCustomDraw("progressBar").Redraw();
                SingleComposer.GetCustomDraw("fluidtank1").Redraw();
                SingleComposer.GetCustomDraw("fluidtank2").Redraw();
                SingleComposer.GetCustomDraw("fluidtankoutput").Redraw();
                SingleComposer.GetDynamicText("enableBtnText").SetNewText(bemixer.Electric.IsEnabled ? Lang.Get("vinteng:gui-turn-off") : Lang.Get("vinteng:gui-turn-on"));
                SingleComposer.GetDynamicText("outputText").SetNewText(GetHelpText());
            }
        }



        private bool EnableButtonClick()
        {
            capi.Network.SendBlockEntityPacket(base.BlockEntityPosition, 1002, null);
            return true;
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

        private string GetProgressText()
        {
            string outputstring = "";
            float craftPercent = _craftProgress * 100;
            if (bemixer.Electric.IsSleeping) // machine is sleeping if on and not crafting
            {
                outputstring = Lang.Get("vinteng:gui-is-sleeping-short");
            }
            else
            {
                outputstring = $"{craftPercent:N1}%";
            }
            if (!bemixer.Electric.IsSleeping && !bemixer.IsCrafting) // these SHOULD be mutually exclusive
            {
                outputstring = $"Error";
            }
            if (!bemixer.Electric.IsEnabled)
            {
                outputstring = Lang.Get("vinteng:gui-word-off");
            }
            return outputstring;
        }

        private string GetHelpText()
        {
            string outputhelptext = "";
            if (_recipemixer != null)
            {
                ItemStack outputstack = _recipemixer.Outputs[0].ResolvedItemstack;
                outputhelptext = $"{Lang.Get("vinteng:gui-word-crafting")} {outputstack.Collectible.GetHeldItemName(outputstack)}";
            }
            else
            {
                if (bemixer.Electric.IsSleeping)
                {
                    outputhelptext = Lang.Get("vinteng:gui-no-valid-recipe");    // third is a VALID recipe
                }
                if (!bemixer.HasRoomInOutput(0, null))
                {
                    outputhelptext = Lang.Get("vinteng:gui-machine-isfull");   // an output is full...                    
                }
                if (Inventory[0].Empty)
                {
                    outputhelptext = Lang.Get("vinteng:gui-machine-ingredients");// second priority is an ingredient
                }
            }
            if (!bemixer.Electric.IsEnabled)
            {
                outputhelptext = Lang.Get("vinteng:gui-machine-off");
            }
            return outputhelptext;
        }

        private void SendInvPacket(object obj)
        {
            this.capi.Network.SendBlockEntityPacket(BlockEntityPosition.X, BlockEntityPosition.Y, BlockEntityPosition.Z, obj);
        }

        private void OnTitleBarClosed()
        {
            this.TryClose();
        }

        private void OnPowerDraw(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        {
            ctx.Save();
            Matrix i = ctx.Matrix;

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
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            Inventory.SlotModified += OnSlotModified;
        }

        public override void OnGuiClosed()
        {
            Inventory.SlotModified -= OnSlotModified;
            SingleComposer.GetSlotGrid("inputSlot").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("outputSlots").OnGuiClosed(capi);
            base.OnGuiClosed();
        }
    }
}
