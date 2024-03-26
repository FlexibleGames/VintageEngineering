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
    public class BECrusher : ElectricBE
    {
        private ICoreClientAPI capi;
        private ICoreServerAPI sapi;
        private float updateBouncer = 0f;
        private GUICrusher clientDialog;

        public string DialogTitle
        {
            get
            {
                return Lang.Get("vinteng:gui-title-crusher");
            }
        }

        public BECrusher()
        {
            inv = new InvCrusher(null, null);
            inv.SlotModified += OnSlotModified;
        }

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
                    AnimUtil.InitializeAnimator("vecrusher", null, null, new Vec3f(0, GetRotation(), 0f));
                }
            }
            crushingPowerCost = this.Block.Attributes["crushpowercost"].AsInt();
            inv.Pos = this.Pos;
            inv.LateInitialize($"{InventoryClassName}-{this.Pos.X}/{this.Pos.Y}/{this.Pos.Z}", api);
        }

        #region RecipeAndInventoryStuff
        private InvCrusher inv;

        private RecipeCrusher currentRecipe;
        private CrushingProperties crushingProperties;
        private ItemStack nuggetType;
        /// <summary>
        /// Baseline vanilla power cost to crush or extract nuggets. Set in JSON.<br/>
        /// Value is multiplied by the hardness value to get crushPowerCostTotal for each crafting cycle.
        /// </summary>
        private int crushingPowerCost;

        private ulong crushPowerCostTotal;
        private ulong recipePowerApplied;
        private bool isCrafting = false;
        public string craftMode = "crush";
        public float RecipeProgress
        {
            get
            {
                if (craftMode == "recipe")
                {
                    if (currentRecipe == null) return 0f;
                    return (float)recipePowerApplied / (float)currentRecipe.PowerPerCraft;
                }
                else if (craftMode == "crush")
                {
                    if (crushingProperties == null) return 0f;
                    return (float)recipePowerApplied / (float)crushPowerCostTotal;
                }
                else
                {
                    // craftmode == nugget
                    if (nuggetType == null) return 0f;
                    return (float)recipePowerApplied / (float)crushPowerCostTotal;
                }

            }
        }

        public bool IsCrafting { get { return isCrafting; } }

        public ItemSlot InputSlot { get { return inv[0]; } }

        //public ItemSlot RequiresSlot { get { return null; } }

        public ItemSlot OutputSlot { get { return inv[1]; } }
        /// <summary>
        /// Slotid's 2 - 4 are ExtraOutputSlots
        /// </summary>
        /// <param name="slotid">3 or 4</param>
        /// <returns>ItemSlot</returns>
        public ItemSlot ExtraOutputSlot(int slotid)
        {
            if (slotid < 1 || slotid > 4) return null;
            return inv[slotid];
        }

        public override string InventoryClassName { get { return "InvCrusher"; } }

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
                    StateChange(EnumBEState.Sleeping);
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
        /// Output slots IDs are slotid 1 for primary and 2, 3, and 4 for secondary
        /// </summary>
        /// <param name="slotid">Index of ItemSlot inventory</param>
        /// <returns>True if there is room.</returns>
        public bool HasRoomInOutput(int slotid, ItemStack forStack)
        {
            if (slotid == 0 && forStack == null)
            {
                // a special case to check if any output is full
                bool[] hasspace = new bool[4];
                for (int i = 1; i < 5; i++)
                {
                    if (inv[i].Empty) hasspace[i-1] = true;
                    else
                    {
                        if (inv[i].Itemstack.StackSize < inv[i].Itemstack.Collectible.MaxStackSize) hasspace[i-1] = true;
                    }
                }
                return hasspace[0] || hasspace[1] || hasspace[2] || hasspace[3];
            }
            if (slotid < 1 || slotid > 4) return false; // not output slots
            if (inv[slotid].Empty) return true;

            // check equality by code
            if (inv[slotid].Itemstack.Collectible.Code != forStack.Collectible.Code) return false;

            // check stack size held versus max
            int numinslot = inv[slotid].StackSize;
            if (numinslot >= forStack.Collectible.MaxStackSize) return false;
            
            return true;
        }

        /// <summary>
        /// Find a matching Log Splitter Recipe given the Blocks inventory.
        /// </summary>
        /// <returns>True if recipe found that matches ingredient.</returns>
        public bool FindMatchingRecipe()
        {
            // TODO CRUSHING PROPS
            if (Api == null) return false; // we're running this WAY too soon, bounce.
            if (MachineState == EnumBEState.Off) // if the machine is off, bounce.
            {
                return false;
            }
            if (InputSlot.Empty)
            {
                currentRecipe = null;
                crushingProperties = null;
                nuggetType = null;
                isCrafting = false;
                StateChange(EnumBEState.Sleeping);
                return false;
            }

            if (craftMode == "nugget")
            {
                currentRecipe = null;
                crushingProperties = null;
                if (InputSlot.Itemstack.Collectible is ItemOre &&
                    InputSlot.Itemstack.ItemAttributes["metalUnits"].Exists)
                {
                    int units = InputSlot.Itemstack.ItemAttributes["metalUnits"].AsInt(5);
                    string type = InputSlot.Itemstack.Collectible.Variant["ore"].Replace("quartz_", "").Replace("galena_", "");
                    nuggetType = new ItemStack(this.Api.World.GetItem(new AssetLocation("nugget-" + type)), 1)
                    {
                        StackSize = Math.Max(1, units / 5)
                    };
                    if (crushPowerCostTotal == 0)
                    {
                        crushPowerCostTotal = (ulong)(crushingPowerCost * 2);
                    }
                    isCrafting = true;
                    StateChange(EnumBEState.On);
                    return true;
                }
                else
                {
                    crushPowerCostTotal = 0;
                }
                nuggetType = null;
            }
            else if (craftMode == "crush")
            {
                currentRecipe = null;
                nuggetType = null;
                if (InputSlot.Itemstack.Collectible.CrushingProps != null)
                {
                    crushingProperties = InputSlot.Itemstack.Collectible.CrushingProps.Clone();
                    if (crushPowerCostTotal == 0)
                    { 
                        crushPowerCostTotal = (ulong)(crushingPowerCost * (crushingProperties.HardnessTier == 0 ? 1 : crushingProperties.HardnessTier)); 
                    }
                    isCrafting = true;
                    StateChange(EnumBEState.On);
                    return true;
                }
                crushPowerCostTotal = 0;
                crushingProperties = null;
            }
            else if (craftMode == "recipe")
            {
                currentRecipe = null;
                crushingProperties = null;
                nuggetType = null;

                List<RecipeCrusher> mprecipes = Api?.ModLoader?.GetModSystem<VERecipeRegistrySystem>(true)?.CrusherRecipes;
                if (mprecipes == null) return false;

                foreach (RecipeCrusher mprecipe in mprecipes)
                {
                    if (mprecipe.Enabled && mprecipe.Matches(InputSlot))
                    {
                        currentRecipe = mprecipe;
                        isCrafting = true;
                        StateChange(EnumBEState.On);
                        return true;
                    }
                }
            }            
            isCrafting = false;
            recipePowerApplied = 0;
            StateChange(EnumBEState.Sleeping);
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

                    if (craftMode == "recipe")
                    {
                        if (currentRecipe == null) return;
                        for (int x=0;x<currentRecipe.Outputs.Length;x++)
                        {
                            if (!HasRoomInOutput(x + 1, currentRecipe.Outputs[x].ResolvedItemstack)) return;
                        }
                    }
                    else if (craftMode == "crush")
                    {
                        if (crushingProperties == null) return;
                        for (int x = 1;x<5;x++)
                        {
                            if (!HasRoomInOutput(x, crushingProperties.CrushedStack.ResolvedItemstack)) return;
                        }
                    }
                    else
                    {
                        if (nuggetType == null) return;
                        for (int x = 1; x < 5; x++)
                        {
                            if (!HasRoomInOutput(x, nuggetType)) return;
                        }
                    }                    

                    float powerpertick = MaxPPS * dt;                    

                    if (CurrentPower < powerpertick) return; // last check for our power requirements.

                    // round to the nearest whole number
                    recipePowerApplied += (ulong)Math.Round(powerpertick);
                    electricpower -= (ulong)Math.Round(powerpertick);
                }
                else if (!isCrafting) StateChange(EnumBEState.Sleeping);
                else if (RecipeProgress >= 1f)
                {
                    // recipe crafting complete
                    if (craftMode == "recipe")
                    {
                        for (int x = 0; x < currentRecipe.Outputs.Length; x++)
                        {
                            ItemStack output = currentRecipe.Outputs[x].ResolvedItemstack.Clone();
                            output.StackSize = currentRecipe.Outputs[x].VariableResolve(Api.World, "Crusher Recipe Output");
                            if (output.StackSize == 0) continue;
                            if (HasRoomInOutput(x+1, output))
                            {
                                if (inv[x + 1].Empty) inv[x + 1].Itemstack = output;
                                else
                                {
                                    int capleft = inv[x + 1].Itemstack.Collectible.MaxStackSize - inv[x + 1].Itemstack.StackSize;
                                    if (capleft <= 0) Api.World.SpawnItemEntity(output, Pos.UpCopy(1).ToVec3d()); // this should never fire
                                    else if (capleft >= output.StackSize) inv[x + 1].Itemstack.StackSize += output.StackSize;
                                    else
                                    {
                                        inv[x + 1].Itemstack.StackSize += capleft;
                                        output.StackSize -= capleft;
                                        Api.World.SpawnItemEntity(output, Pos.UpCopy(1).ToVec3d());
                                    }
                                }
                            }
                        }
                        InputSlot.TakeOut(currentRecipe.Ingredients[0].Quantity);
                        InputSlot.MarkDirty();
                    }
                    if (craftMode == "crush" || craftMode == "nugget")
                    {
                        ItemStack output = craftMode == "crush" ? crushingProperties?.CrushedStack?.ResolvedItemstack?.Clone() : nuggetType?.Clone();
                        if (output != null)
                        {
                            if (craftMode == "crush")
                            {
                                output.StackSize = GameMath.RoundRandom(Api.World.Rand, crushingProperties.Quantity.nextFloat((float)output.StackSize, Api.World.Rand));
                            }
                            if (output.StackSize <= 0)
                            {
                                // I guess this might be possible?
                                recipePowerApplied = 0;
                            }
                            else
                            {
                                int itemstopush = output.StackSize;
                                // since this only ever produces 1 thing, we can try to use all the output slots...
                                for (int x = 0; x < 4; x++)
                                {
                                    if (itemstopush == 0) break;
                                    if (HasRoomInOutput(x+1, output))
                                    {
                                        if (inv[x + 1].Empty)
                                        {
                                            itemstopush = 0;
                                            inv[x + 1].Itemstack = output; 
                                        }
                                        else
                                        {
                                            int capleft = inv[x + 1].Itemstack.Collectible.MaxStackSize - inv[x + 1].Itemstack.StackSize;
                                            if (capleft >= output.StackSize)
                                            {
                                                inv[x + 1].Itemstack.StackSize += output.StackSize;
                                                itemstopush = 0;
                                            }
                                            else
                                            {
                                                inv[x + 1].Itemstack.StackSize += capleft;
                                                output.StackSize -= capleft;
                                                itemstopush -= capleft;                                                
                                            }
                                        }
                                    }
                                }
                                if (itemstopush > 0) Api.World.SpawnItemEntity(output, Pos.UpCopy(1).ToVec3d()); // this should never fire
                            }
                            InputSlot.TakeOut(1); // triggers onslotchanged
                            InputSlot.MarkDirty();
                        }
                    }

                    if (InputSlot.Empty || !FindMatchingRecipe())
                    {
                        StateChange(EnumBEState.Sleeping);
                        isCrafting = false;
                    }
                    recipePowerApplied = 0;
                    MarkDirty(true, null);
                    Api.World.BlockAccessor.MarkBlockEntityDirty(this.Pos);
                }
            }
        }

        public override void StateChange(EnumBEState newstate)
        {
            if (MachineState == newstate) return; // no change, nothing to see here.            
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
                    clientDialog = new GUICrusher(DialogTitle, Inventory, this.Pos, capi, this);
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
                GUICrusher gUILog = clientDialog;
                if (gUILog != null) { gUILog.Dispose(); }
                clientDialog = null;
            }
        }

        public string GetOutputText()
        {
            float recipeProgressPercent = RecipeProgress * 100;
            string onOff;
            switch (MachineState)
            {
                case EnumBEState.On: onOff = Lang.Get("vinteng:gui-word-on"); break;
                case EnumBEState.Off: onOff = Lang.Get("vinteng:gui-word-off"); break;
                case EnumBEState.Sleeping: onOff = Lang.Get("vinteng:gui-word-sleeping"); ; break;
                default: onOff = "Error"; break;
            }
            string crafting = isCrafting ? $"{Lang.Get("vinteng:gui-word-crafting")}: {recipeProgressPercent:N1}%" : $"{Lang.Get("vinteng:gui-machine-notcrafting")}";

            return $"{crafting} | {onOff} | {Lang.Get("vinteng:gui-word-power")}: {CurrentPower:N0}/{MaxPower:N0}";
        }

        #region ServerClientStuff
        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(player, packetid, data);
            if (packetid == 1002) // Enable Button
            {
                if (IsEnabled) StateChange(EnumBEState.Off); // turn off
                else
                {
                    StateChange(IsCrafting ? EnumBEState.On : EnumBEState.Sleeping);
                }
                MarkDirty(true, null);
            }
            if (packetid == 1003)
            {
                // drop down selection changed
                string mode = Encoding.ASCII.GetString(data);
                if (mode != craftMode)
                {
                    craftMode = mode;
                    FindMatchingRecipe();
                }
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
            tree.SetString("craftmode", craftMode);
            tree.SetLong("crushrecipepowertotal", (long)crushPowerCostTotal);
//            tree.SetItemstack("nuggettype", nuggetType);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            inv.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            if (Api != null) inv.AfterBlocksLoaded(Api.World);
            recipePowerApplied = (ulong)tree.GetLong("recipepowerapplied");
            isCrafting = tree.GetBool("iscrafting", false);
            if (!isCrafting) StateChange(EnumBEState.Sleeping);
            if (!IsEnabled) StateChange(EnumBEState.Off);
            craftMode = tree.GetString("craftmode", "crush");
            crushPowerCostTotal = (ulong)(tree.GetLong("crushrecipepowertotal"));
//            nuggetType = tree.GetItemstack("nuggettype");
            FindMatchingRecipe();

            if (clientDialog != null && clientDialog.IsOpened())
            {
                clientDialog.Update(RecipeProgress, CurrentPower, currentRecipe, crushingProperties, nuggetType);
            }            
        }

        #endregion
    }
}
