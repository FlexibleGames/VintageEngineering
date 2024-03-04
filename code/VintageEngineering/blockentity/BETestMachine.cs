using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.GameContent;
using Vintagestory.API.Datastructures;
using VintageEngineering.Electrical;
using VintageEngineering.RecipeSystem.Recipes;
using VintageEngineering.RecipeSystem;

namespace VintageEngineering
{
    public class BETestMachine : ElectricBEGUI
    {
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        private TestMachineInventory inventory;
        private TestMachineGUI clientDialog;
                
       
        // a bouncer to limit GUI updates
        private float updateBouncer = 0;

        // Recipe stuff, generic and hard coded for now
        #region RecipeStuff
        /// <summary>
        /// Current Recipe (if any) that the machine can or is crafting.
        /// </summary>
        public MetalPressRecipe currentPressRecipe;

        /// <summary>
        /// Current power applied to the current recipe.
        /// </summary>
        public ulong recipePowerApplied;

        /// <summary>
        /// 0 -> 1 float of recipe progress
        /// </summary>
        public float RecipeProgress
        {
            get 
            { 
                if (currentPressRecipe == null) { return 0f; }
                return (float)recipePowerApplied / (float)currentPressRecipe.PowerPerCraft;
            }
        }
        private bool isCrafting = false;
        
        #endregion

        /// <summary>
        /// Is this machine currently working on something?
        /// </summary>
        public bool IsCrafting { get { return isCrafting; } }        

        public override bool CanExtractPower => false;
        public override bool CanReceivePower => true;

        private ItemSlot InputSlot
        {
            get
            {
                return this.inventory[0];
            }
        }
        private ItemSlot OutputSlot
        {
            get { return this.inventory[1]; }
        }

        private ItemSlot ExtraOutputSlot
        { get { return this.inventory[2]; } }

        private ItemSlot MoldSlot
        {
            get { return this.inventory[3]; }
        }

        private ItemStack InputStack
        {
            get
            {
                return this.inventory[0].Itemstack;
            }
            set
            {
                this.inventory[0].Itemstack = value;
                this.inventory[0].MarkDirty();
            }
        }

        public override InventoryBase Inventory
        {
            get
            {
                return inventory;
            }
        }

        public string DialogTitle
        {
            get
            {
                return "Metal Press";
            }
        }

        public override string InventoryClassName { get { return "TestMachineInventory"; } }

        public BETestMachine()
        {
            this.inventory = new TestMachineInventory(null, null);
            this.inventory.SlotModified += OnSlotModified;
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(null);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            if (this.clientDialog != null)
            {
                this.clientDialog.TryClose();
                TestMachineGUI testGenGUI = this.clientDialog;
                if (testGenGUI != null) testGenGUI.Dispose();
                this.clientDialog = null;
            }
        }

        public void OnSlotModified(int slotId)
        {
//            base.Block = this.Api.World.BlockAccessor.GetBlock(this.Pos);
//            this.MarkDirty(this.Api.Side == EnumAppSide.Server, null);
            if (slotId == 0 || slotId == 3)
            {
                // new thing in the input or mold slot!
                if (InputSlot.Empty)
                {
                    isCrafting = false;
                    isSleeping = true;
                    currentPressRecipe = null;
                    recipePowerApplied = 0;
                }
                else
                {
                    FindMatchingRecipe();
                }
                MarkDirty(true, null);
                if (clientDialog != null && clientDialog.IsOpened())
                {
                    clientDialog.Update(RecipeProgress, CurrentPower, currentPressRecipe);
                }
            }            
        }

        /// <summary>
        /// Find a matching Metal Press Recipe given the Blocks inventory.
        /// </summary>        
        /// <returns>True if recipe found that matches ingredient and mold.</returns>
        public bool FindMatchingRecipe()
        {
            if (InputSlot.Empty)
            {
                currentPressRecipe = null;
                isCrafting = false;
                isSleeping = true;                
                return false;
            }

            this.currentPressRecipe = null;
            List<MetalPressRecipe> mprecipes = Api.ModLoader.GetModSystem<VERecipeRegistrySystem>(true).MetalPressRecipes;            
            
            foreach (MetalPressRecipe mprecipe in mprecipes)
            {
                if (mprecipe.Enabled && mprecipe.Matches(InputSlot, MoldSlot))
                {
                    currentPressRecipe = mprecipe;
                    isCrafting = true;
                    isSleeping = false;                    
                    return true;
                }
            }
            currentPressRecipe = null;
            isCrafting = false;
            isSleeping = true;
            return false;
        }

        public string GetOutputText()
        {
            float recipeProgressPercent = RecipeProgress * 100;
            string onOff = isEnabled ? "On" : "Off";
            string crafting = isCrafting ? $"Craft: {recipeProgressPercent:N1}%" : "Not Crafting";
            if (isSleeping) onOff = "Sleeping";
            return $"{crafting} | {onOff} | Power: {CurrentPower:N0}/{MaxPower:N0}";
        }

        /// <summary>
        /// Check whether the output inventory is full. Index of 0 is main output, index of 1 is the optional additional output.
        /// </summary>
        /// <param name="outputslotid">0 or 1, any other values might cause a blackhole and ruin the universe.</param>
        /// <returns>True if there is room in that inventory slot.</returns>
        public bool HasRoomInOutput(int outputslotid = 0)
        {
            if (currentPressRecipe != null && !InputSlot.Empty) // active recipe
            {
                if (InputSlot.Itemstack.Satisfies(currentPressRecipe.Ingredients[0].ResolvedItemstack) )
                {
                    // input stack is valid for active recipe, the recipe might be valid, but the outputs are from another recipe
                    // machine needs to be emptied for new recipe to start
                    if (!Inventory[outputslotid+1].Empty)
                    {                        
                        // if the output slot has something in it, is it the same thing we make?
                        if (Inventory[outputslotid+1].Itemstack.Collectible.Code == currentPressRecipe.Outputs[outputslotid].ResolvedItemstack.Collectible.Code)
                        {
                            // the same thing is in the output as we make, so can make more...?
                            if (Inventory[outputslotid + 1].Itemstack.StackSize < Inventory[outputslotid+1].Itemstack.Collectible.MaxStackSize)
                            {
                                return true;
                            }                        
                        }

                    }
                    else
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        public void OnSimTick(float deltatime)
        {
            if (this.Api is ICoreServerAPI) // only simulates on the server!
            {
                // if the machine is ON but not crafting, it's sleeping, tick slower
                if (isSleeping)
                {
                    updateBouncer += deltatime;
                    if (updateBouncer < 2f) return;                    
                }
                updateBouncer = 0;
                
                // if we're sleeping, bounce out of here. Extremely fast updates.                
                if (isEnabled && !isSleeping) // block is enabled (on/off) 
                {
                    if (isCrafting && RecipeProgress < 1f) // machine is activly crafting and recipe isn't done
                    {
                        if (CurrentPower == 0) return; // we have no power, there's no point in trying. bounce
                        if (!HasRoomInOutput(0)) return; // output is full... bounce

                        // scale power to apply to recipe by how much time has passed
                        float powerToApply = MaxPPS * deltatime;

                        if (CurrentPower < powerToApply) return; // we don't have enough power to continue... bounce.

                        // calculate percent of progress for this time-step.
                        float percentOfTotal = powerToApply / currentPressRecipe.PowerPerCraft;
                        // apply progress to recipe progress.
                        recipePowerApplied += (ulong)Math.Round(powerToApply);
                        electricpower -= (ulong)Math.Round(powerToApply);
                    }
                    else if (!IsCrafting) // machine isn't crafting
                    {
                        // enabled but not crafting means we have no valid recipe
                        isSleeping = true; // go to sleep
                    }
                    if (RecipeProgress >= 1f)
                    {
                        // progress finished! 
                        ItemStack outputstack = new ItemStack(Api.World.GetItem(currentPressRecipe.Outputs[0].ResolvedItemstack.Collectible.Code),
                                                       currentPressRecipe.Outputs[0].ResolvedItemstack.StackSize);
                        if (HasRoomInOutput(0))
                        {
                            // output is empty! need a new stack
                            // Api.World.GetItem(craftingCode)
                            if (OutputSlot.Empty)
                            {
                                Inventory[1].Itemstack = outputstack.Clone();
                            }
                            else
                            {
                                int capleft = Inventory[1].Itemstack.Collectible.MaxStackSize - Inventory[1].Itemstack.StackSize;

                                if (capleft <= 0) Api.World.SpawnItemEntity(outputstack, Pos.UpCopy(1).ToVec3d());
                                else if (capleft >= outputstack.StackSize) Inventory[1].Itemstack.StackSize += outputstack.StackSize;
                                else
                                {
                                    Inventory[1].Itemstack.StackSize += capleft;
                                    outputstack.StackSize -= capleft;
                                    Api.World.SpawnItemEntity(outputstack, Pos.UpCopy(1).ToVec3d());
                                }

                            }
                        }
                        else
                        {
                            // no room in main output, how'd we get in here, machine should stop when full...
                            Api.World.SpawnItemEntity(outputstack, Pos.UpCopy(1).ToVec3d());
                        }
                        if (currentPressRecipe.Outputs.Length > 1)
                        {
                            // this recipe has a second output
                            int varoutput = currentPressRecipe.Outputs[1].VariableResolve(Api.World, "VintEng: Metal Press Craft output");

                            // depending on Variable set in output stacksize COULD be 0.
                            ItemStack extraoutputstack = new ItemStack(Api.World.GetItem(currentPressRecipe.Outputs[1].ResolvedItemstack.Collectible.Code),
                                                                       varoutput);
                            if (extraoutputstack.StackSize > 0 && HasRoomInOutput(1))
                            {
                                if (ExtraOutputSlot.Empty)
                                {
                                    Inventory[2].Itemstack = extraoutputstack.Clone();
                                }
                                else
                                {
                                    // drop extras on the ground
                                    int capremaining = Inventory[2].Itemstack.Collectible.MaxStackSize - Inventory[2].Itemstack.StackSize;
                                    if (capremaining >= extraoutputstack.StackSize)
                                    {
                                        Inventory[2].Itemstack.StackSize += extraoutputstack.StackSize;
                                    }
                                    else
                                    {
                                        Inventory[2].Itemstack.StackSize += capremaining;
                                        extraoutputstack.StackSize -= capremaining;
                                        Api.World.SpawnItemEntity(extraoutputstack, Pos.UpCopy(1).ToVec3d());
                                        // spawn what we can't fit
                                    }
                                }
                            }
                            else
                            {
                                // no room in output, drop on ground
                                // TODO Drop in FRONT of the block, or some predetermined place.
                                Api.World.SpawnItemEntity(extraoutputstack, this.Pos.UpCopy(1).ToVec3d());
                            }

                        }
                        // remove used ingredients from input

                        InputSlot.TakeOut(currentPressRecipe.Ingredients[0].Quantity);
                        if (!FindMatchingRecipe())
                        {
                            isSleeping = true;
                            isCrafting = false;
                        }
                        recipePowerApplied = 0;
                        MarkDirty(true, null);
                        Api.World.BlockAccessor.MarkBlockEntityDirty(this.Pos);
                    }
                }
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
            }
            else
            {
                capi = api as ICoreClientAPI;
            }
            this.inventory.Pos = this.Pos;
            this.inventory.LateInitialize($"{InventoryClassName}-{this.Pos.X}/{this.Pos.Y}/{this.Pos.Z}", api);
            this.RegisterGameTickListener(new Action<float>(OnSimTick), 100, 0);
        }

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (this.Api.Side == EnumAppSide.Client)
            {
                base.toggleInventoryDialogClient(byPlayer, delegate
                {
                    this.clientDialog = new TestMachineGUI(DialogTitle, Inventory, this.Pos, this.Api as ICoreClientAPI, this);
                    this.clientDialog.Update(RecipeProgress, CurrentPower, currentPressRecipe);
                    return this.clientDialog;
                });
            }
            return true;
        }

        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(player, packetid, data);
            if (packetid == 1002) // Enable button pressed
            {
                isEnabled = !isEnabled;                               
                MarkDirty(true, null);
            }    
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);
            if (clientDialog != null) clientDialog.Update(RecipeProgress, CurrentPower, currentPressRecipe);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            this.inventory.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;

            tree.SetLong("recipepowerapplied", (long)recipePowerApplied);
            
            tree.SetBool("isCrafting", isCrafting);            
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            this.inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            if (Api != null) Inventory.AfterBlocksLoaded(this.Api.World);
            recipePowerApplied = (ulong)tree.GetLong("recipepowerapplied");            
            isCrafting = tree.GetBool("isCrafting");            

            FindMatchingRecipe();
            if (Api != null && Api.Side == EnumAppSide.Client)
            {
                MarkDirty(true, null);
                if (this.clientDialog != null)
                {
                    clientDialog.Update(RecipeProgress, CurrentPower, currentPressRecipe);                    
                }                
            }
        }
    }
}
