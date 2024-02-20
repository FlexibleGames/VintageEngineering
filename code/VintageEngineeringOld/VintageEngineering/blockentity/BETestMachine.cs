using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.GameContent;
using Vintagestory.API.Datastructures;

namespace VintageEngineering
{
    public class BETestMachine : BlockEntityOpenableContainer
    {
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        private TestMachineInventory inventory;
        private TestMachineGUI clientDialog;

        // how much PPS does this machine apply to recipes?
        private ulong recipePPS = 32;
        // is the machine enabled?
        private bool isEnabled = false;
        // a bouncer to limit GUI updates
        private float updateBouncer = 0;

        // Recipe stuff, generic and hard coded for now
        #region RecipeStuff
        private string recipeInput = "ingot";
        private string recipeOutput = "metalplate";
        private AssetLocation craftingCode;
        // how much power is required to 'make' one item.
        // 32 PPS -> 512 cost = 16 seconds per craft
        private ulong  recipePowerCost = 512;
        // how much progress are we to finishing one craft? 0 -> 1
        private float recipeProgress = 0;
        private bool isCrafting = false;
        private bool isSleeping = false;
        #endregion

        public bool IsCrafting { get { return isCrafting; } }
        public float RecipeProgress { get { return recipeProgress; } }
        public bool IsEnabled { get { return isEnabled; } }
        public bool IsSleeping { get { return isSleeping; } }

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

        // Power Stuff, will be an Interface soon
        #region PowerStuff
        private ulong maxPower = 2048;
        private ulong currentPower = 0;
        // ElectricalPowerTier Enum LV
        // ElectricalEntityType Enum Producer
        public ulong MaxPower { get { return maxPower; } }
        public ulong CurrentPower { get { return currentPower; } }
        public bool CanReceivePower { get { return true; } }
        public bool CanExtractPower { get { return false; } }
        public bool IsPowerFull { get { return currentPower == maxPower; } }
        public ulong ReceivePower(ulong powerOffered)
        {
            if (currentPower < maxPower)
            {
                ulong powerNeeded = maxPower - currentPower;
                if (powerNeeded >= powerOffered)
                {
                    currentPower += powerOffered;
                    return 0;
                }
                else
                {
                    currentPower += powerNeeded;
                    powerOffered -= powerNeeded;
                }
            }            
            return powerOffered;
        }
        public ulong ExtractPower(ulong powerNeeded)
        {
            return powerNeeded;
        }
        // end power stuff
        #endregion

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
                return "Test Machine";
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
            base.Block = this.Api.World.BlockAccessor.GetBlock(this.Pos);
            this.MarkDirty(this.Api.Side == EnumAppSide.Server, null);
            if (this.Api is ICoreClientAPI && this.clientDialog != null)
            {
                clientDialog.Update(recipeProgress, currentPower);
            }
            if (slotId == 0)
            {
                // new thing in the input slot!
                if (InputSlot.Empty)
                {
                    isCrafting = false; 
                    recipeProgress = 0;
                }
                MarkDirty(false, null);
                if (clientDialog != null && clientDialog.IsOpened())
                {
                    clientDialog.SingleComposer.ReCompose();
                }
            }
            isSleeping = false;
        }

        public string GetOutputText()
        {
            float recipeProgressPercent = recipeProgress * 100;
            string onOff = isEnabled ? "On" : "Off";
            string crafting = isCrafting ? $"Craft: {recipeProgressPercent:N1}%" : "Not Crafting";
            if (isSleeping) onOff = "Sleeping";
            return $"{crafting} | {onOff} | Power: {currentPower:N0}/{maxPower:N0}";
        }

        public Item HasPlate(ItemStack itemStack)
        {
            if (itemStack.Item.FirstCodePart() == recipeInput) // is the item an ingot?
            {
                string metalType = itemStack.Item.FirstCodePart(1);
                if (metalType != null)
                {
                    Item metalPlate = Api.World.GetItem(new AssetLocation($"{recipeOutput}-{metalType}"));
                    if (metalPlate != null) return metalPlate;
                }
            }
            return null;
        }
        public void OnSimTick(float deltatime)
        {
            if (this.Api is ICoreServerAPI) // only simulates on the server!
            {
                updateBouncer += deltatime;
                
                // if we're sleeping, bounce out of here. Extremely fast updates.                
                if (isEnabled && !isSleeping) // block is enabled (on/off) 
                {
                    if (isCrafting && recipeProgress < 1f) // machine is activly crafting and recipe isn't done
                    {
                        if (currentPower == 0) return; // we have no power, there's no point in trying. bounce

                        // scale power to apply to recipe by how much time has passed
                        float powerToApply = recipePPS * deltatime;

                        if (currentPower < powerToApply) return; // we don't have enough power to continue... bounce.

                        // calculate percent of progress for this time-step.
                        float percentOfTotal = powerToApply / recipePowerCost;
                        // apply progress to recipe progress.
                        recipeProgress += percentOfTotal;
                        currentPower -= (ulong)Math.Round(powerToApply);
                    }
                    else if (!IsCrafting) // machine isn't crafting
                    {
                        // enabled but not crafting, can we start crafting?
                        if (!InputSlot.Empty)
                        {
                            // input slot has something in it, valid?
                            // while it should always be valid as we filter the input, you never know!
                            // should we check output stack to see?
                            Item outputItem = HasPlate(InputStack);
                            if (outputItem != null)
                            {
                                if (OutputSlot.Itemstack != null && OutputSlot.Itemstack.Item != outputItem)
                                {
                                    // the item we make isn't the type of thing in the output...
                                    // it's either this or sit idle while a thing is crafted...
                                    isSleeping = true;
                                }
                                else
                                {
                                    craftingCode = outputItem.Code;
                                    recipeProgress = 0f;
                                    isCrafting = true;
                                }
                            }
                        }
                        else isSleeping = true; // input is empty, go to sleep
                    }
                    if (recipeProgress > 1f)
                    {
                        // progress finished!
                        if (OutputSlot.Empty)
                        {
                            // Api.World.GetItem(craftingCode)
                            OutputSlot.Itemstack = new ItemStack(Api.World.GetItem(craftingCode), 1);
                            InputSlot.TakeOut(1);
                            isCrafting = false;
                            recipeProgress = 0f;
                        }
                        else
                        {
                            ItemStack craftedItem = new ItemStack(Api.World.GetItem(craftingCode), 1);
                            if (OutputSlot.Itemstack.Item == craftedItem.Item)
                            {
                                if (OutputSlot.Itemstack.StackSize < OutputSlot.Itemstack.Item.MaxStackSize)
                                {                                    
                                    OutputSlot.Itemstack.StackSize += 1;
                                    InputSlot.TakeOut(1);
                                    isCrafting = false;
                                    recipeProgress = 0f;
                                }
                                else
                                {
                                    // item is done, but output is full...
                                    // go to sleep, Function OnSlotModified wakes it back up
                                    isSleeping = true;
                                }
                            }
                            else
                            {
                                // item is done, but output is different type?
                                // go to sleep, Function OnSlotModified wakes it back up
                                // this shouldn't fire as it checks before starting the craft
                                isSleeping = true;
                            }
                        }
                    }
                }

                // if the machine is sleeping, update 4 times slower
                if (updateBouncer >= (isSleeping ? 2f : 0.5f)) // half second client updates... too much?
                {
                    clientDialog?.Update(recipeProgress, currentPower);
                    MarkDirty(false, null);
                    updateBouncer = 0f;
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
                    this.clientDialog.Update(recipeProgress, currentPower);
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
                isSleeping = false;
                MarkDirty(true, null);
            }    
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);
            if (clientDialog != null) clientDialog.Update(recipeProgress, currentPower);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed)
        {
            foreach (ItemSlot slot in this.inventory)
            {
                if (slot.Itemstack != null && !slot.Itemstack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve))
                {
                    slot.Itemstack = null;
                }
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            this.inventory.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;

            tree.SetFloat("recipeProgress", recipeProgress);
            tree.SetBool("isEnabled", isEnabled);
            tree.SetBool("isCrafting", isCrafting);
            tree.SetBool("isSleeping", isSleeping);
            tree.SetLong("currentPower", (long)currentPower);
            
            if (isCrafting)
            {
                tree.SetString("craftingCode", craftingCode.ToString());
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            this.inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            if (Api != null) Inventory.AfterBlocksLoaded(this.Api.World);
            recipeProgress = tree.GetFloat("recipeProgress");
            isEnabled = tree.GetBool("isEnabled");
            isCrafting = tree.GetBool("isCrafting");
            isSleeping = tree.GetBool("isSleeping");
            currentPower = (ulong)tree.GetLong("currentPower", 0);
            if (isCrafting)
            {
                craftingCode = new AssetLocation(tree.GetString("craftingCode"));
            }
            if (Api != null && Api.Side == EnumAppSide.Client)
            {
                if (this.clientDialog != null) clientDialog.Update(recipeProgress, currentPower);
                MarkDirty(true, null);
            }
        }
    }
}
