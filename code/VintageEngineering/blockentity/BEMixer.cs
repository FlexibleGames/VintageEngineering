using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Electrical;
using VintageEngineering.RecipeSystem.Recipes;
using VintageEngineering.RecipeSystem;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VintageEngineering
{
    public class BEMixer : ElectricBEWithFluid, IHeatable
    {
        private ICoreClientAPI capi;
        private ICoreServerAPI sapi;
        private float updateBouncer = 0f;
        private GUIMixer clientDialog;

        public string DialogTitle
        {
            get
            {
                return Lang.Get("vinteng:gui-title-mixer");
            }
        }

        public BEMixer()
        {
            inv = new InvMixer(null, null);
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
                    AnimUtil.InitializeAnimator("vemixer", null, null, new Vec3f(0, GetRotation(), 0f));
                }
            }
            inv.Pos = this.Pos;
            inv.LateInitialize($"{InventoryClassName}-{this.Pos.X}/{this.Pos.Y}/{this.Pos.Z}", api);
            FindMatchingRecipe();
        }
        #region IVELiquidInterface
        public override int[] InputLiquidContainerSlotIDs => new int[] { 4, 5 };
        public override int[] OutputLiquidContainerSlotIDs => new int[] { 7 };
        #endregion

        #region RecipeAndInventoryStuff
        private InvMixer inv;

        private RecipeMixer currentRecipe;        

        public float basinTemperature;
                
        /// <summary>
        /// When using a Barrel Recipe, what is the power requirement total.
        /// </summary>
        private ulong recipePowerCostTotal;

        private ulong recipePowerApplied;
        private bool isCrafting = false;
        
        public float RecipeProgress
        {
            get
            {
                if (currentRecipe != null)
                {
                    return (float)recipePowerApplied / (float)currentRecipe.PowerPerCraft;
                }
                return 0f;
            }
        }

        public bool IsCrafting { get { return isCrafting; } }

        public ItemSlot[] InputSlots
        { 
            get 
            {
                return new ItemSlot[]
                {
                    inv[0],
                    inv[1],
                    inv[2],
                    inv[3],
                    inv[4] as ItemSlotLiquidOnly,
                    inv[5] as ItemSlotLiquidOnly
                };                
            }
        }

        public ItemSlot[] OutputSlots
        {
            get
            {
                return new ItemSlot[]
                {
                    inv[6],
                    inv[7] as ItemSlotLiquidOnly
                };
            }
        }

        #region IHeatable
        public float GetDesiredTemperature()
        {
            if (currentRecipe == null) return 0f;
            else return currentRecipe.RequiresTemp;
        }

        public void SetTemperature(float temperature)
        {
            basinTemperature = temperature;
        }

        public float GetTemperature()
        {
            return basinTemperature;
        }
        #endregion
        public ItemSlot OutputSlot { get { return inv[6]; } }

        public override string InventoryClassName { get { return "InvMixer"; } }

        public override InventoryBase Inventory { get { return inv; } }

        public void OnSlotModified(int slotid)
        {
            if (slotid >= 0 && slotid < 6)
            {
                // something about the inputs changed...
                FindMatchingRecipe();
                MarkDirty(true);
                if (clientDialog != null && clientDialog.IsOpened())
                {
                    clientDialog.Update(RecipeProgress, CurrentPower, currentRecipe);
                }
            }
        }

        /// <summary>
        /// Output slots IDs are slotid 6 for item, 7 for fluid<br/>
        /// Pass id = 0 and forStack = null to check both outputs.
        /// </summary>
        /// <param name="slotid">Index of ItemSlot inventory</param>
        /// <returns>True if there is room.</returns>
        public bool HasRoomInOutput(int slotid, ItemStack forStack)
        {
            if (slotid == 0 && forStack == null)
            {
                if (inv[6].Empty && inv[7].Empty) return true;
                int itemout = 0;
                int fluidout = 0;
                if (!inv[6].Empty)
                {
                    itemout = inv[6].Itemstack.Collectible.MaxStackSize - inv[6].Itemstack.StackSize;
                }
                else itemout = 64;

                if (!inv[7].Empty)
                {
                    WaterTightContainableProps wprops = BlockLiquidContainerBase.GetContainableProps(inv[7].Itemstack);
                    if (wprops == null) fluidout = 0;
                    fluidout = (int)((inv[7] as ItemSlotLiquidOnly).CapacityLitres - (inv[7].Itemstack.StackSize/wprops.ItemsPerLitre));
                }
                else fluidout = 50;

                return itemout > 0 && fluidout > 0;
            }
            else
            {
                if (slotid < 6 || slotid > 7) return false; // not an output slot
                if (forStack == null) return false; // need to compare for an actual item
                if (inv[slotid].Empty) return true; // slot is empty, good to go.
                else
                {
                    return inv[slotid].GetRemainingSlotSpace(forStack) > 0;
                }
            }
        }

        /// <summary>
        /// Find a matching Crusher Recipe given the Blocks inventory and mode.
        /// </summary>
        /// <returns>True if recipe found that matches ingredient.</returns>
        public bool FindMatchingRecipe()
        {
            if (Api == null) return false; // we're running this WAY too soon, bounce.
            if (MachineState == EnumBEState.Off) // if the machine is off, bounce.
            {
                return false;
            }
            bool noinput = true;
            foreach (ItemSlot slot in InputSlots)
            {
                if (slot.Empty) noinput &= true;
                else noinput = false;
            }
            if (noinput)
            {
                currentRecipe = null;                
                isCrafting = false;
                SetState(EnumBEState.Sleeping);
                return false;
            }

            List<RecipeMixer> mrecipes = Api?.ModLoader?.GetModSystem<VERecipeRegistrySystem>(true)?.MixerRecipes;
            if (mrecipes == null) return false;

            foreach (RecipeMixer mrecipe in mrecipes)
            {
                if (mrecipe.Enabled && mrecipe.Matches(InputSlots))
                {
                    currentRecipe = mrecipe;
                    isCrafting = true;
                    SetState(EnumBEState.On);
                    return true;
                }
            }

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
                    if (CurrentPower == 0 || CurrentPower < (MaxPPS * dt)) return; // we don't have any power to progress.                    

                    if (!HasRoomInOutput(0, null)) return;

                    if (currentRecipe.RequiresTemp > 0)
                    {
                        if (basinTemperature < currentRecipe.RequiresTemp) return;
                    }

                    float powerpertick = MaxPPS * dt;

                    if (CurrentPower < powerpertick) return; // last check for our power requirements.

                    // round to the nearest whole number
                    recipePowerApplied += (ulong)Math.Round(powerpertick);
                    electricpower -= (ulong)Math.Round(powerpertick);
                }                
                else if (RecipeProgress >= 1f)
                {                  
                    
                    if (currentRecipe != null)
                    {
                        // used a Mixer Recipe
                        currentRecipe.TryCraftNow(Api, InputSlots, OutputSlots);
                    }
                    
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
                    clientDialog = new GUIMixer(DialogTitle, Inventory, this.Pos, capi, this);
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
                GUIMixer gUILog = clientDialog;
                if (gUILog != null) { gUILog.Dispose(); }
                clientDialog = null;
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            float recipeProgressPercent = RecipeProgress * 100;
            dsc.AppendLine(isCrafting ? $"{Lang.Get("vinteng:gui-word-crafting")}: {recipeProgressPercent:N1}%" : $"{Lang.Get("vinteng:gui-machine-notcrafting")}");
            dsc.AppendLine($"{Lang.Get("vinteng:gui-word-temp")} {basinTemperature:N1}°");
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
            tree.SetLong("recipepowercosttotal", (long)recipePowerCostTotal);
            tree.SetFloat("basintemp", basinTemperature);
            //            tree.SetItemstack("nuggettype", nuggetType);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            inv.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            if (Api != null) inv.AfterBlocksLoaded(Api.World);
            recipePowerApplied = (ulong)tree.GetLong("recipepowerapplied");
            isCrafting = tree.GetBool("iscrafting", false);
            basinTemperature = tree.GetFloat("basintemp", 20f);
            recipePowerCostTotal = (ulong)(tree.GetLong("recipepowercosttotal"));            
            FindMatchingRecipe();
            if (Api != null && Api.Side == EnumAppSide.Client) { SetState(MachineState); }
            if (clientDialog != null && clientDialog.IsOpened())
            {
                clientDialog.Update(RecipeProgress, CurrentPower, currentRecipe);
            }
        }

        #endregion
    }
}
