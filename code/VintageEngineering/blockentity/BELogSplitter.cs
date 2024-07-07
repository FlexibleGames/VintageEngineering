using System;
using System.Collections.Generic;
using System.Text;
using VintageEngineering.Electrical;
using VintageEngineering.GUI;
using VintageEngineering.RecipeSystem;
using VintageEngineering.RecipeSystem.Recipes;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;


namespace VintageEngineering
{
    public class BELogSplitter : ElectricBE
    {
        private ICoreClientAPI capi;
        private ICoreServerAPI sapi;
        private float updateBouncer = 0f;
        private GUILogSplitter clientDialog;

        public string DialogTitle
        {
            get
            {
                return Lang.Get("vinteng:gui-title-logsplitter");
            }
        }

        public BELogSplitter()
        {
            inv = new InvLogSplitter(null, null);
            inv.SlotModified += OnSlotModified;
        }

        public override bool CanExtractPower => false;
        public override bool CanReceivePower => true;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
                RegisterGameTickListener(new Action<float>(OnSimTick), 100, 0);
            }
            else
            {
                capi = api as ICoreClientAPI;
                if (AnimUtil != null)
                {
                    AnimUtil.InitializeAnimator("velogsplitter", null, null, new Vec3f(0, GetRotation(), 0f));
                }
            }
            inv.Pos = this.Pos;
            inv.LateInitialize($"{InventoryClassName}-{this.Pos.X}/{this.Pos.Y}/{this.Pos.Z}", api);
            if (!inv[0].Empty) FindMatchingRecipe();
        }

        #region RecipeAndInventoryStuff
        private InvLogSplitter inv;
        private RecipeLogSplitter currentRecipe;
        private ulong recipePowerApplied;
        private bool isCrafting = false;
        public float RecipeProgress
        {
            get
            {
                if (currentRecipe == null) return 0f;
                return (float)recipePowerApplied / (float)currentRecipe.PowerPerCraft;
            }
        }

        public bool IsCrafting { get { return isCrafting; } }

        public ItemSlot InputSlot {  get { return inv[0]; } }
        public ItemSlot OutputSlot { get { return inv[1]; } }
        public ItemSlot ExtraOutputSlot { get { return inv[2]; } }

        public override string InventoryClassName { get { return "VELogSplitterInv"; } }

        public override InventoryBase Inventory { get { return inv; } }

        public void OnSlotModified(int slotid)
        {
            if (slotid == 0)
            {
                // something changed with the input slot
                if (InputSlot.Empty)
                {
                    isCrafting = false;                    
                    currentRecipe = null;
                    recipePowerApplied = 0;
                    SetState(EnumBEState.Sleeping);
                }
                else
                {
                    FindMatchingRecipe();
                    MarkDirty(true, null);
                }
                if (clientDialog != null && clientDialog.IsOpened())
                {
                    clientDialog.Update(RecipeProgress, CurrentPower, currentRecipe);
                }
            }
        }

        /// <summary>
        /// Output slots IDs are slotid 1 for primary and 2 for secondary
        /// </summary>
        /// <param name="slotid">Index of ItemSlot inventory</param>
        /// <returns>True if there is room.</returns>
        public bool HasRoomInOutput(int slotid)
        {
            if (slotid < 1 || slotid > 2) return false;
            if (inv[slotid].Empty) return true;
            if (inv[slotid].StackSize < inv[slotid].Itemstack.Collectible.MaxStackSize) return true;

            return false;
        }

        /// <summary>
        /// Find a matching Log Splitter Recipe given the Blocks inventory.
        /// </summary>
        /// <returns>True if recipe found that matches ingredient.</returns>
        public bool FindMatchingRecipe()
        {
            if (Api == null) return false; // we're running this WAY too soon, bounce.
            if (MachineState == EnumBEState.Off) // if the machine is off, bounce.
            {
                return false;
            }
            if (InputSlot.Empty)
            {
                currentRecipe = null;
                isCrafting = false;                
                SetState(EnumBEState.Sleeping);
                return false;
            }

            currentRecipe = null;            
            List<RecipeLogSplitter> mprecipes = Api?.ModLoader?.GetModSystem<VERecipeRegistrySystem>(true)?.LogSplitterRecipes;

            if (mprecipes == null) return false;

            foreach (RecipeLogSplitter mprecipe in mprecipes)
            {
                if (mprecipe.Enabled && mprecipe.Matches(InputSlot))
                {
                    currentRecipe = mprecipe;
                    isCrafting = true;                    
                    SetState(EnumBEState.On);
                    return true;
                }
            }
            currentRecipe = null;
            isCrafting = false;
            recipePowerApplied = 0;
            SetState(EnumBEState.Sleeping);
            return false;
        }
        #endregion

        public void OnSimTick(float dt)
        {
            if (Api.Side == EnumAppSide.Client) return; // only tick on the server
            if (IsSleeping)
            {
                // A sleeping machine runs this routine every 2 seconds instead of 10 times a second.
                updateBouncer += dt;
                if (updateBouncer < 2f) return;
                updateBouncer = 0f;
            }
            if (MachineState == EnumBEState.On) // machine is on and actively crafting something
            {
                if (isCrafting && RecipeProgress < 1f)
                {
                    if (CurrentPower == 0 || CurrentPower < (MaxPPS*dt)) return; // we don't have any power to progress.
                    if (!HasRoomInOutput(1) && !HasRoomInOutput(2)) return; // no room in output slots, stop
                    if (currentRecipe == null) return; // how the heck did this happen?

                    float powerpertick = MaxPPS * dt;
                    float percentprogress = powerpertick / currentRecipe.PowerPerCraft; // power to apply this tick

                    if (CurrentPower < powerpertick) return; // last check for our power requirements.

                    // round to the nearest whole number
                    recipePowerApplied += (ulong)Math.Round(powerpertick);
                    electricpower -= (ulong)Math.Round(powerpertick);
                }
                else if (!isCrafting) SetState(EnumBEState.Sleeping);
                else if (RecipeProgress >= 1f)
                {
                    // recipe crafting complete
                    ItemStack outputprimary = currentRecipe.Outputs[0].ResolvedItemstack.Clone();
                    if (HasRoomInOutput(1))
                    {
                        // primary output is empty, set the stack.
                        if (OutputSlot.Empty) inv[1].Itemstack = outputprimary;
                        else
                        {
                            // how much space is left in primary?
                            int capleft = inv[1].Itemstack.Collectible.MaxStackSize - inv[1].Itemstack.StackSize;
                            if (capleft <= 0) Api.World.SpawnItemEntity(outputprimary, Pos.UpCopy(1).ToVec3d()); // should never fire
                            else if (capleft >= outputprimary.StackSize) inv[1].Itemstack.StackSize += outputprimary.StackSize;
                            else
                            {
                                inv[1].Itemstack.StackSize += capleft;
                                outputprimary.StackSize -= capleft;
                                Api.World.SpawnItemEntity(outputprimary, Pos.UpCopy(1).ToVec3d());
                            }
                        }
                        OutputSlot.MarkDirty();
                    }
                    else
                    {
                        Api.World.SpawnItemEntity(outputprimary, Pos.UpCopy(1).ToVec3d());
                    }
                    if (currentRecipe.Outputs.Length > 1)
                    {
                        // recipe has a secondary output
                        int variableoutput = currentRecipe.Outputs[1].VariableResolve(Api.World, "VintEng: LogSplitter Craft output");
                        if (variableoutput > 0)
                        {
                            ItemStack secondOuput = currentRecipe.Outputs[1].ResolvedItemstack.Clone();
                            secondOuput.StackSize = variableoutput;
                            if (HasRoomInOutput(2))
                            {
                                if (ExtraOutputSlot.Empty) ExtraOutputSlot.Itemstack = secondOuput;
                                else
                                {
                                    // deja vu
                                    int capleft = inv[2].Itemstack.Collectible.MaxStackSize - inv[2].Itemstack.StackSize;
                                    if (capleft <= 0) Api.World.SpawnItemEntity(secondOuput, Pos.UpCopy(1).ToVec3d());
                                    else if (capleft >= secondOuput.StackSize) inv[2].Itemstack.StackSize += secondOuput.StackSize;
                                    else
                                    {
                                        inv[2].Itemstack.StackSize += capleft;
                                        secondOuput.StackSize -= capleft;
                                        Api.World.SpawnItemEntity(secondOuput, Pos.UpCopy(1).ToVec3d());
                                    }
                                }
                            }
                            else
                            {
                                Api.World.SpawnItemEntity(secondOuput, Pos.UpCopy(1).ToVec3d());
                            }
                            ExtraOutputSlot.MarkDirty();
                        }
                    }
                    InputSlot.TakeOut(currentRecipe.Ingredients[0].Quantity);
                    InputSlot.MarkDirty();

                    if (InputSlot.Empty || !FindMatchingRecipe())
                    {
                        SetState(EnumBEState.Sleeping);
                        isCrafting = false;
                    }
                    recipePowerApplied = 0;
                    MarkDirty(true, null);
                    Api.World.BlockAccessor.MarkBlockEntityDirty(this.Pos);
                }
            }
        }

        protected virtual void SetState(EnumBEState newstate)
        {                    
            MachineState = newstate;
            
            if (MachineState == EnumBEState.On)
            {
                if (AnimUtil != null && base.Block.Attributes["craftinganimcode"].Exists)
                {
                    AnimUtil.StartAnimation(new AnimationMetaData
                    {
                        Animation = base.Block.Attributes["craftinganimcode"].AsString(),
                        Code = base.Block.Attributes["craftinganimcode"].AsString(),
                        AnimationSpeed = 1f,
                        EaseOutSpeed = 4f,
                        EaseInSpeed = 1f
                    });
                }
            }
            else
            {
                if (AnimUtil != null && AnimUtil.activeAnimationsByAnimCode.Count > 0)
                {
                    AnimUtil.StopAnimation(base.Block.Attributes["craftinganimcode"].AsString());
                }
            }
            if (Api != null && Api.Side == EnumAppSide.Client && clientDialog != null && clientDialog.IsOpened())
            {
                clientDialog.Update(RecipeProgress, CurrentPower, currentRecipe);
            }
            MarkDirty(true);
        }


        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (this.Api != null && Api.Side == EnumAppSide.Client)
            {
                base.toggleInventoryDialogClient(byPlayer, delegate
                {
                    clientDialog = new GUILogSplitter(DialogTitle, Inventory, this.Pos, capi, this);
                    clientDialog.Update(RecipeProgress, CurrentPower, currentRecipe);
                    return this.clientDialog;
                });
            }
            return true;
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            if (clientDialog != null)
            {
                clientDialog.TryClose();
                GUILogSplitter gUILog = clientDialog;
                if (gUILog != null) { gUILog.Dispose(); }
                clientDialog = null;
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            float recipeProgressPercent = RecipeProgress * 100;
            dsc.AppendLine(isCrafting ? $"{Lang.Get("vinteng:gui-word-crafting")}: {recipeProgressPercent:N1}%" : $"{Lang.Get("vinteng:gui-machine-notcrafting")}");
        }

        #region ServerClientStuff
        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(player, packetid, data);
            if (packetid == 1002) // Enable Button
            {
                if (IsEnabled) SetState(EnumBEState.Off); // turn off
                else
                {
                    SetState(IsCrafting ? EnumBEState.On : EnumBEState.Sleeping);
                }
                MarkDirty(true, null);
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);
            if (clientDialog != null && clientDialog.IsOpened()) clientDialog.Update(RecipeProgress, CurrentPower, currentRecipe);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            inv.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;
            tree.SetLong("recipepowerapplied", (long)recipePowerApplied);
            tree.SetBool("iscrafting", isCrafting);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            inv.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            if (Api != null) inv.AfterBlocksLoaded(Api.World);
            recipePowerApplied = (ulong)tree.GetLong("recipepowerapplied");
            isCrafting = tree.GetBool("iscrafting", false);
            if (!inv[0].Empty) FindMatchingRecipe();

            if (Api != null && Api.Side == EnumAppSide.Client) { SetState(MachineState); }
            if (clientDialog != null && clientDialog.IsOpened())
            {
                clientDialog.Update(RecipeProgress, CurrentPower, currentRecipe);
            }            
            //if (Api != null && Api.Side == EnumAppSide.Client) MarkDirty(true, null);
        }

        #endregion
    }
}
