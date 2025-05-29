using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.RecipeSystem.Recipes;
using VintageEngineering.RecipeSystem;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using VintageEngineering.Electrical;
using VintageEngineering.inventory;
using Vintagestory.API.Util;
using System.Threading;

namespace VintageEngineering
{
    public class BECNC : ElectricContainerBE
    {
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        private InvCNC inventory;
        private GUICNC clientDialog;
        // a bouncer to limit GUI updates
        private float updateBouncer = 0;

        // Recipe stuff, generic and hard coded for now
        #region RecipeStuff
        /// <summary>
        /// Current Recipe (if any) that the machine can or is crafting.
        /// </summary>
        public ClayFormingRecipe currentRecipe;

        /// <summary>
        /// Current power applied to the current recipe.
        /// </summary>
        public ulong recipePowerApplied;

        /// <summary>
        /// Amount of power needed to craft one instance of the current recipe.
        /// </summary>
        public int recipeMaxPowerNeeded;

        /// <summary>
        /// Amount of Clay required to duplicate raw clay item in ProgramSlot.
        /// </summary>
        public int recipeClayNeeded;

        /// <summary>
        /// Mapped to powercostperinput from JSON
        /// </summary>
        public int recipePowerPerVoxel;

        /// <summary>
        /// 0 -> 1 float of recipe progress
        /// </summary>
        public float RecipeProgress
        {
            get
            {
                if (currentRecipe == null) { return 0f; }
                if (recipeMaxPowerNeeded == 0) { return 0f; }
                return (float)recipePowerApplied / (float)recipeMaxPowerNeeded;
            }
        }
        private bool isCrafting = false;

        #endregion

        /// <summary>
        /// Is this machine currently working on something?
        /// </summary>
        public bool IsCrafting { get { return isCrafting; } }

        private ItemSlot InputSlot
        {
            get
            {
                return this.inventory[0];
            }
        }
        private ItemSlot OutputSlot
        {
            get { return this.inventory[2]; }
        }

        private ItemSlot ExtraOutputSlot
        { get { return this.inventory[3]; } }

        private ItemSlot ProgramSlot
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
                return Lang.Get("vinteng:gui-title-cnc");
            }
        }

        public override string InventoryClassName { get { return "VECNCInv"; } }

        public BECNC()
        {
            this.inventory = new InvCNC(null, null);
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
                GUICNC testGenGUI = this.clientDialog;
                if (testGenGUI != null) testGenGUI.Dispose();
                this.clientDialog = null;
            }
        }

        public void OnSlotModified(int slotId)
        {
            //            base.Block = this.Api.World.BlockAccessor.GetBlock(this.Pos);
            //            this.MarkDirty(this.Api.Side == EnumAppSide.Server, null);
            if (slotId == 0 || slotId == 1)
            {
                // new thing in the input or mold slot!
                if (InputSlot.Empty || ProgramSlot.Empty)
                {
                    isCrafting = false;
                    SetState(EnumBEState.Sleeping);
                    currentRecipe = null;
                    recipePowerApplied = 0;
                }
                else
                {
                    FindMatchingRecipe();
                }
                MarkDirty(true, null);
                if (clientDialog != null && clientDialog.IsOpened())
                {
                    clientDialog.Update(RecipeProgress, Electric.CurrentPower, currentRecipe);
                }
            }
        }

        /// <summary>
        /// Find a matching Clay Forming Recipe given the Program inventory.<br/>
        /// If Program is present, check its values.
        /// </summary>
        /// <returns>True if recipe found that matches clay item or program.</returns>
        public bool FindMatchingRecipe()
        {
            if (Api == null) return false; // we're running this WAY too soon, bounce.
            if (Electric.MachineState == EnumBEState.Off) // if the machine is off, bounce.
            {
                return false;
            }
            if (ProgramSlot.Empty)
            {
                currentRecipe = null;
                isCrafting = false;
                recipeClayNeeded = 0;
                recipeMaxPowerNeeded = 0;
                SetState(EnumBEState.Sleeping);
                return false;
            }

            if (!ProgramSlot.Itemstack.Collectible.Code.Path.Contains("vecncprogram"))
            {
                List<ClayFormingRecipe> clayrecipes = Api.GetClayformingRecipes();
                foreach (ClayFormingRecipe cf in clayrecipes)
                {
                    if (cf.Output.ResolvedItemstack.Collectible.Code.Path == ProgramSlot.Itemstack.Collectible.Code.Path)
                    {
                        currentRecipe = cf.Clone();
                        isCrafting = true;
                        int voxels = CountClayVoxels();
                        recipeMaxPowerNeeded = voxels * recipePowerPerVoxel;
                        recipeClayNeeded = (int)(voxels / 25);
                        recipeClayNeeded = Math.Max(1, recipeClayNeeded);
                        SetState(EnumBEState.On);
                        break;
                    }
                }
                if (currentRecipe == null)
                {
                    return false;
                }
                return true;
            }
            else
            {
                // we have a vecncprogram, but none of that is implemented yet.
                if (!ProgramSlot.Itemstack.Collectible.Code.Path.Contains("encoded"))
                {
                    // cncprogram is blank
                    currentRecipe = null;
                    isCrafting = false;
                    SetState(EnumBEState.Sleeping);
                    return false;
                }
                else
                {
                    // cncprogram is encoded, time to parse what it has...
                    // not yet implemented
                    currentRecipe = null;
                    isCrafting = false;
                    SetState(EnumBEState.Sleeping);
                    return false;
                }
            }
        }

        /// <summary>
        /// Check currentRecipe input against input slot contents
        /// </summary>
        /// <returns>True if valid</returns>
        public bool ValidateInput()
        {
            if (InputSlot.Empty) return false;
            if (currentRecipe == null) return false;

            if (currentRecipe.Ingredient.SatisfiesAsIngredient(InputStack, false)) return true;
            
            return false;
        }

        /// <summary>
        /// Returns number of clay voxels needed to craft the current clay item.
        /// </summary>
        /// <returns>Int clay voxel count</returns>
        public int CountClayVoxels()
        {
            int outputvox = 0;

            for (int layer = 0; layer < currentRecipe.Pattern.Length; layer++)
            {

                for (int row = 0; row < currentRecipe.Pattern[layer].Length; row++)
                {
                    outputvox += currentRecipe.Pattern[layer][row].CountChars('#');
                }
            }

            return outputvox;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            float recipeProgressPercent = RecipeProgress * 100;
            dsc.AppendLine(isCrafting ? $"{Lang.Get("vinteng:gui-word-crafting")}: {recipeProgressPercent:N1}%" : $"{Lang.Get("vinteng:gui-machine-notcrafting")}");
        }

        /// <summary>
        /// Output slots IDs are slotid 2 and 3 (ID 1 is the program)<br/>
        /// Pass in slotid = 0 and forStack = null to return if ANY slot has room.
        /// </summary>
        /// <param name="slotid">Index of ItemSlot inventory</param>
        /// <returns>True if there is room.</returns>
        public bool HasRoomInOutput(int slotid, ItemStack forStack)
        {
            if (slotid == 0 && forStack == null)
            {
                // a special case to check if any output is full                
                for (int i = 2; i < 4; i++)
                {
                    if (inventory[i].Empty) return true;
                    else
                    {
                        if (currentRecipe != null)
                        {
                            if (inventory[i].Itemstack.Collectible.Code.Path != currentRecipe.Output.Code.Path) return false;
                        }
                        if (inventory[i].Itemstack.StackSize < inventory[i].Itemstack.Collectible.MaxStackSize) return true;
                    }
                }
                return false;
            }
            if (slotid < 2 || slotid > 3) return false; // not output slots
            if (inventory[slotid].Empty) return true;

            // check equality by code
            if (inventory[slotid].Itemstack.Collectible.Code != forStack.Collectible.Code) return false;

            // check stack size held versus max
            int numinslot = inventory[slotid].StackSize;
            if (numinslot >= forStack.Collectible.MaxStackSize) return false;

            return true;
        }

         public void OnSimTick(float deltatime)
        {
            if (this.Api is ICoreServerAPI) // only simulates on the server!
            {
                // if the machine is ON but not crafting, it's sleeping, tick slower
                if (Electric.IsSleeping)
                {
                    updateBouncer += deltatime;
                    if (updateBouncer < 2f) return;
                }
                updateBouncer = 0;

                // if we're sleeping, bounce out of here. Extremely fast updates.
                if (Electric.MachineState == EnumBEState.On) // block is enabled (on/off)
                {
                    if (RecipeProgress < 1f) // machine is activly crafting and recipe isn't done
                    {
                        if (Electric.CurrentPower == 0) return; // we have no power, there's no point in trying. bounce
                        if (!HasRoomInOutput(0, null)) return; // output is full... bounce

                        // verify input type AND stacksize is what we need to craft
                        if (currentRecipe != null)
                        {
                            if (InputSlot.Empty) { return; } // no input items, bounce
                            if (InputSlot.Itemstack.StackSize < recipeClayNeeded) { return; } // we do not have enough clay, bounce

                            // now we need to verify the actual ingredient to our input stack...
                            if (!ValidateInput()) {  return; }
                            if (InputStack.StackSize < recipeClayNeeded) { return; }
                        }

                        // scale power to apply to recipe by how much time has passed
                        float powerToApply = Electric.MaxPPS * deltatime;

                        if (Electric.CurrentPower < powerToApply) return; // we don't have enough power to continue... bounce.

                        // apply progress to recipe progress.
                        recipePowerApplied += (ulong)Math.Round(powerToApply);
                        Electric.electricpower -= (ulong)Math.Round(powerToApply);
                    }
                    if (RecipeProgress >= 1f)
                    {
                        // progress finished!
                        ItemStack outputstack = currentRecipe.Output.ResolvedItemstack.Clone();

                        for (int i = 2; i < 4; i++)
                        {
                            if (HasRoomInOutput(i, outputstack))
                            {
                                // output is empty! need a new stack
                                // Api.World.GetItem(craftingCode)
                                if (Inventory[i].Empty)
                                {
                                    Inventory[i].Itemstack = outputstack;
                                    Inventory[i].MarkDirty();
                                    break;
                                }
                                else
                                {
                                    int capleft = Inventory[i].Itemstack.Collectible.MaxStackSize - Inventory[i].Itemstack.StackSize;
                                    if (capleft <= 0)
                                    {
                                        if (i == 2) continue;
                                        Api.World.SpawnItemEntity(outputstack, Pos.UpCopy(1).ToVec3d());
                                        break;
                                    }
                                    else if (capleft >= outputstack.StackSize)
                                    {
                                        // we have enough space for the entire new stack
                                        ItemStackMergeOperation merge = new ItemStackMergeOperation(Api.World, EnumMouseButton.Left, (EnumModifierKey)0,
                                            EnumMergePriority.ConfirmedMerge, outputstack.StackSize);
                                        merge.SourceSlot = new DummySlot(outputstack);
                                        merge.SinkSlot = Inventory[i];

                                        // calling this will "merge" the temperatures
                                        Inventory[i].Itemstack.Collectible.TryMergeStacks(merge);
                                        Inventory[i].MarkDirty();
                                        break;
                                        //                                    Inventory[1].Itemstack.StackSize += outputstack.StackSize;
                                    }
                                    else
                                    {
                                        ItemStackMergeOperation merge = new ItemStackMergeOperation(Api.World, EnumMouseButton.Left, (EnumModifierKey)0,
                                            EnumMergePriority.ConfirmedMerge, capleft);
                                        int toSpawn = outputstack.StackSize - capleft;
                                        outputstack.StackSize -= toSpawn;
                                        merge.SourceSlot = new DummySlot(outputstack);
                                        merge.SinkSlot = Inventory[i];
                                        Inventory[i].Itemstack.Collectible.TryMergeStacks(merge);
                                        Inventory[i].MarkDirty();

                                        //Inventory[1].Itemstack.StackSize += capleft;
                                        // spawn what's left
                                        outputstack.StackSize = toSpawn;
                                        if (i == 2) continue;
                                        Api.World.SpawnItemEntity(outputstack, Pos.UpCopy(1).ToVec3d());
                                        break;
                                    }

                                }                                
                            }
                            else
                            {
                                // no room in main output, how'd we get in here, machine should stop when full...
                                if (i == 2) continue;
                                Api.World.SpawnItemEntity(outputstack, Pos.UpCopy(1).ToVec3d());
                            }
                        }                                                

                        // remove used ingredients from input
                        InputSlot.TakeOut(recipeClayNeeded);
                        InputSlot.MarkDirty();

                        if (!FindMatchingRecipe())
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
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
                this.RegisterGameTickListener(new Action<float>(OnSimTick), 100, 0);
            }
            else
            {
                capi = api as ICoreClientAPI;
                if (AnimUtil != null)
                {
                    AnimUtil.InitializeAnimator("vecnc", null, null, new Vec3f(0f, Electric.GetRotation(), 0f));
                }
            }
            recipePowerPerVoxel = base.Block.Attributes["powercostperinput"].AsInt(1);
            this.inventory.Pos = this.Pos;
            this.inventory.LateInitialize($"{InventoryClassName}-{this.Pos.X}/{this.Pos.Y}/{this.Pos.Z}", api);
            if (!inventory[1].Empty) FindMatchingRecipe();
        }

        protected virtual void SetState(EnumBEState newstate)
        {
            Electric.MachineState = newstate;

            if (Electric.MachineState == EnumBEState.On)
            {
                if (AnimUtil != null)
                {
                    if (base.Block.Attributes["craftinganimcode"].Exists)
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
                clientDialog.Update(RecipeProgress, Electric.CurrentPower, currentRecipe);
            }
            MarkDirty(true, null);
        }

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (this.Api.Side == EnumAppSide.Client)
            {
                base.toggleInventoryDialogClient(byPlayer, delegate
                {
                    this.clientDialog = new GUICNC(DialogTitle, Inventory, this.Pos, this.Api as ICoreClientAPI, this);
                    this.clientDialog.Update(RecipeProgress, Electric.CurrentPower, currentRecipe);
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
                if (Electric.IsEnabled) // we're enabled, we need to turn off
                {
                    SetState(EnumBEState.Off);
                }
                else
                {
                    SetState(isCrafting ? EnumBEState.On : EnumBEState.Sleeping);
                }
                MarkDirty(true, null);
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);
            if (clientDialog != null && clientDialog.IsOpened()) clientDialog.Update(RecipeProgress, Electric.CurrentPower, currentRecipe);
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
                SetState(Electric.MachineState);
                if (this.clientDialog != null && clientDialog.IsOpened())
                {
                    clientDialog.Update(RecipeProgress, Electric.CurrentPower, currentRecipe);
                }
                MarkDirty(true, null);
            }
        }
    }
}
