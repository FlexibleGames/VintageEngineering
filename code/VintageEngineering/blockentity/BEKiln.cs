using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Electrical;
using VintageEngineering.GUI;
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
    public class BEKiln : ElectricContainerBE
    {
        private ICoreClientAPI capi;
        private ICoreServerAPI sapi;
        private float updateBouncer = 0f;        
        private GUIKiln clientDialog;

        public string DialogTitle
        {
            get
            {
                return Lang.Get("vinteng:gui-title-kiln");
            }
        }

        public BEKiln()
        {
            inv = new InvKiln(null, null);
            inv.SlotModified += OnSlotModified;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
                RegisterGameTickListener(new Action<float>(OnSimTick), 100, 0);
                HeatPerSecondBase = base.Block.Attributes["heatpersecond"].AsInt(0);
                if (environmentTemp == 0f)
                {
                    environmentTemp = api.World.BlockAccessor.GetClimateAt(this.Pos, EnumGetClimateMode.NowValues).Temperature;
                }
            }
            else
            {
                capi = api as ICoreClientAPI;
                if (AnimUtil != null)
                {
                    AnimUtil.InitializeAnimator("vekiln", null, null, new Vec3f(0, Electric.GetRotation(), 0f));
                }
            }
            inv.Pos = this.Pos;
            inv.LateInitialize($"{InventoryClassName}-{this.Pos.X}/{this.Pos.Y}/{this.Pos.Z}", api);
            if (!inv[0].Empty) FindMatchingRecipe();
        }

        #region RecipeAndInventoryStuff
        private InvKiln inv;
        private RecipeKiln currentRecipe;
        private CombustibleProperties _cproperties;
        private float _burntimeelapsed;
        private ulong recipePowerApplied;
        private bool isHeating = false;
        private bool isCrafting = false;
        private int HeatPerSecondBase;
        private float currentTemp;
        public float environmentTemp; 
        private float environmentTempDelay = 0f;
        public float CurrentTemp { get => currentTemp; }
        public float RecipeProgress
        {
            get
            {
                if (currentRecipe == null)
                {
                    if (_cproperties != null)
                    {
                        return _burntimeelapsed / _cproperties.MeltingDuration;
                    }
                    return 0f;
                }
                if (currentRecipe.RequiresTime != 0f) return _burntimeelapsed / currentRecipe.RequiresTime;

                return (float)recipePowerApplied / (float)currentRecipe.PowerPerCraft;
            }
        }
        /// <summary>
        /// If we're crafting we're always heating, even when we're at the right temp
        /// </summary>
        public bool IsCrafting { get { return isCrafting; } }
        /// <summary>
        /// If we're heating we may not be crafting (yet)
        /// </summary>
        public bool IsHeating { get { return isHeating; } }

        public ItemSlot InputSlot { get { return inv[0]; } }
        public ItemSlot OutputSlot { get { return inv[1]; } }

        /// <summary>
        /// Slotid's 1 - 9 are ExtraOutputSlots
        /// </summary>
        /// <param name="slotid">1 to 9</param>
        /// <returns>ItemSlot</returns>
        public ItemSlot OutputSlots(int slotid)
        {
            if (slotid < 1 || slotid > 9) return null;
            return inv[slotid];
        }

        public override string InventoryClassName { get { return "InvKiln"; } }

        public override InventoryBase Inventory { get { return inv; } }

        public void OnSlotModified(int slotid)
        {
            if (slotid == 0)
            {
                // something changed with the input slot
                FindMatchingRecipe();
                MarkDirty(true, null);
                
                if (clientDialog != null && clientDialog.IsOpened())
                {
                    clientDialog.Update(RecipeProgress, Electric.CurrentPower, currentTemp, currentRecipe, _cproperties);
                }
            }
        }

        /// <summary>
        /// Output slots IDs are slotid 1 to 9<br/>
        /// Pass in slotid = 0 and forStack = null to return if ANY slot has room.
        /// </summary>
        /// <param name="slotid">Index of ItemSlot inventory</param>
        /// <returns>True if there is room.</returns>
        public bool HasRoomInOutput(int slotid, ItemStack forStack)
        {
            if (slotid == 0 && forStack == null)
            {
                // a special case to check if any output is full                
                for (int i = 1; i < 10; i++)
                {
                    if (inv[i].Empty) return true;
                    else
                    {
                        if (inv[i].Itemstack.StackSize < inv[i].Itemstack.Collectible.MaxStackSize) return true;
                    }
                }
                return false;
            }
            if (slotid < 1 || slotid > 9) return false; // not output slots
            if (inv[slotid].Empty) return true;

            // check equality by code
            if (inv[slotid].Itemstack.Collectible.Code != forStack.Collectible.Code) return false;

            // check stack size held versus max
            int numinslot = inv[slotid].StackSize;
            if (numinslot >= forStack.Collectible.MaxStackSize) return false;

            return true;
        }

        /// <summary>
        /// Find a matching Kiln Recipe or CombustableProps given the Blocks inventory.
        /// </summary>
        /// <returns>True if recipe found that matches ingredient.</returns>
        public bool FindMatchingRecipe()
        {
            if (Api == null) return false; // we're running this WAY too soon, bounce.            
            if (Electric.MachineState == EnumBEState.Off) // if the machine is off, bounce.
            {
                return false;
            }
            if (InputSlot.Empty)
            {
                currentRecipe = null;
                _cproperties = null;
                _burntimeelapsed = 0;
                recipePowerApplied = 0;
                isHeating = false;
                isCrafting = false;
                SetState(EnumBEState.Sleeping);
                return false;
            }

            currentRecipe = null;
            List<RecipeKiln> mprecipes = Api?.ModLoader?.GetModSystem<VERecipeRegistrySystem>(true)?.KilnRecipes;

            _cproperties = InputSlot.Itemstack.Collectible.CombustibleProps;

            if (_cproperties == null && mprecipes == null) return false;

            foreach (RecipeKiln mprecipe in mprecipes)
            {
                if (mprecipe.Enabled && mprecipe.Matches(InputSlot))
                {
                    currentRecipe = mprecipe;
                    if (mprecipe.RequiresTemp > currentTemp) isHeating = true;
                    else isCrafting = true;
                    SetState(EnumBEState.On);
                    return true;
                }
            }
            if (_cproperties != null && _cproperties.SmeltingType != EnumSmeltType.Cook)
            {
                BakingProperties bakingProperties = BakingProperties.ReadFrom(InputSlot.Itemstack);
                
                if (bakingProperties == null && _cproperties.SmeltedStack != null) 
                {
                    if (InputSlot.Itemstack.StackSize >= _cproperties.SmeltedRatio)
                    {
                        if (currentTemp < _cproperties.MeltingPoint) isHeating = true;
                        else isCrafting = true;
                        SetState(EnumBEState.On);
                        return true;
                    }
                }
                _cproperties = null; // a baking thing or not enough items, ignore the combustable props
            }

            currentRecipe = null;
            isCrafting = false;
            isHeating = false;
            _burntimeelapsed = 0f;
            recipePowerApplied = 0;
            SetState(EnumBEState.Sleeping);
            return false;
        }

        /// <summary>
        /// Returns an adjusted fromTemp temperature 
        /// </summary>
        /// <param name="fromTemp">Starting Temp</param>
        /// <param name="toTemp">Temp Goal</param>
        /// <param name="deltatime">Time Step</param>
        /// <returns>New Temp</returns>
        private float ChangeTemperature(float fromTemp, float toTemp, float deltatime)
        {
            float basechange = 0f;
            if (fromTemp < 350) basechange = HeatPerSecondBase * deltatime;
            else
            {
                float diff = Math.Abs(fromTemp - toTemp);
                basechange = deltatime + deltatime * (diff / 30);
                if (diff < basechange) return toTemp;
            }
            if (fromTemp > toTemp) basechange = -basechange;
            if (Math.Abs(fromTemp - toTemp) < 1f) return toTemp;
            float newtemp = fromTemp + basechange;
            if (newtemp < -273) return toTemp; // something odd happened, can't go below absolute 0.
            return newtemp;
        }


        //private float ChangeTemperature(float fromTemp, float toTemp, float deltatime)
        //{
        //    float basechange = 0f;
        //    if (fromTemp <= 600) basechange = HeatPerSecondBase;
        //    else basechange = (1 / (fromTemp - fromTemp / 2)) * 30000; // base change per second
        //    basechange = Math.Min(basechange, HeatPerSecondBase); // ensure change is <= HeatPerSecondBase
        //    float tickchange = basechange * deltatime;
        //    if (fromTemp > toTemp) tickchange = -tickchange;
        //    if (Math.Abs(fromTemp - toTemp) < 1f) return toTemp; // if it's within a degree, return totemp
        //    return fromTemp + tickchange;
        //}

        #endregion

        public void OnSimTick(float dt)
        {
            if (Api.Side == EnumAppSide.Client) return; // only tick on the server
            environmentTempDelay += dt;
            if (environmentTempDelay > 300) // a weather pull every 5 minutes seems reasonable
            {
                environmentTemp = Api.World.BlockAccessor.GetClimateAt(this.Pos, EnumGetClimateMode.NowValues).Temperature;
                environmentTempDelay = 0f;
            }

            if (Electric.IsSleeping || Electric.MachineState == EnumBEState.Paused)
            {
                // A sleeping machine runs this routine every 2 seconds instead of 10 times a second.
                updateBouncer += dt;
                currentTemp = ChangeTemperature(currentTemp, environmentTemp, dt);
                if (updateBouncer < 2f) return;
                updateBouncer = 0f;
            }
            if (Electric.MachineState == EnumBEState.On) // machine is on and actively crafting something
            {
                float powerpertick = Electric.MaxPPS * dt;
                if (Electric.CurrentPower == 0 || Electric.CurrentPower < powerpertick) { return; } // power is low!

                if (isHeating) // if we're heating, we may not be ready to craft yet
                {
                    if (currentRecipe != null) 
                    { 
                        currentTemp = ChangeTemperature(currentTemp, currentRecipe.RequiresTemp, dt); 
                        if (currentTemp >= currentRecipe.RequiresTemp) { isCrafting = true; isHeating = false; }
                    }
                    else if (_cproperties != null) 
                    { 
                        currentTemp = ChangeTemperature(currentTemp, _cproperties.MeltingPoint, dt);
                        if (currentTemp >= _cproperties.MeltingPoint) { isCrafting = true; isHeating = false; }
                    }
                    else
                    {
                        // Something odd happened, we're heating but our recipes are null...
                        if (!inv[0].Empty) FindMatchingRecipe();
                    }
                    Electric.electricpower -= (ulong)Math.Round(powerpertick); // consume power when heating up
                }

                if (isCrafting && RecipeProgress < 1f)
                {                    
                    if (!HasRoomInOutput(0, null)) return; // no room in output slots, stop
                    if (currentRecipe == null && _cproperties == null) return; // how the heck did this happen? should not be possible                    

                    // round to the nearest whole number
                    if (currentRecipe != null)
                    {
                        if (currentRecipe.RequiresTime == 0f) recipePowerApplied += (ulong)Math.Round(powerpertick);
                        else _burntimeelapsed += dt;
                    }
                    else if (_cproperties != null)
                    {
                        _burntimeelapsed += dt;
                    }
                    Electric.electricpower -= (ulong)Math.Round(powerpertick); // we're always costing power while crafting
                }                
                else if (RecipeProgress >= 1f)
                {
                    if (currentRecipe != null)
                    {
                        // recipe crafting complete
                        for (int x = 0; x < currentRecipe.Outputs.Length; x++)
                        {
                            ItemStack output = currentRecipe.Outputs[x].ResolvedItemstack.Clone();
                            output.StackSize = currentRecipe.Outputs[x].VariableResolve(Api.World, "Kiln Recipe Output");
                            if (output.StackSize == 0) continue; // variable output put out 0, bounce

                            for (int o = 1; o < inv.Count - 1; o++) // should go from 1 to 9
                            {
                                if (HasRoomInOutput(o, output))
                                {
                                    if (inv[o].Empty)
                                    {
                                        inv[o].Itemstack = output.Clone();
                                        output.StackSize = 0;
                                        break;
                                    }
                                    else
                                    {
                                        int capleft = inv[o].Itemstack.Collectible.MaxStackSize - inv[o].Itemstack.StackSize;
                                        if (capleft >= output.StackSize)
                                        {
                                            inv[o].Itemstack.StackSize += output.StackSize;
                                            output.StackSize = 0;
                                            break;
                                        }
                                        else
                                        {
                                            inv[o].Itemstack.StackSize += capleft;
                                            output.StackSize -= capleft;
                                            if (output.StackSize == 0) break;
                                        }
                                    }
                                    inv[o].MarkDirty();
                                }
                            }
                            if (output.StackSize > 0) Api.World.SpawnItemEntity(output, Pos.UpCopy(1).ToVec3d());
                        }
                        InputSlot.TakeOut(currentRecipe.Ingredients[0].Quantity);
                        InputSlot.MarkDirty();
                    }  
                    else if (_cproperties != null)
                    {
                        // we're combusting something using CombustableProps, process the output

                        ItemStack output = _cproperties.SmeltedStack.ResolvedItemstack.Clone();
                        output.StackSize = _cproperties.SmeltedStack.ResolvedItemstack.StackSize;                        

                        // lets find a place for this output
                        for (int o = 1; o < inv.Count - 1; o++) // should go from 1 to 9
                        {
                            if (HasRoomInOutput(o, output))
                            {
                                if (inv[o].Empty)
                                {
                                    inv[o].Itemstack = output.Clone();
                                    output.StackSize = 0;
                                    break;
                                }
                                else
                                {
                                    int capleft = inv[o].Itemstack.Collectible.MaxStackSize - inv[o].Itemstack.StackSize;
                                    if (capleft >= output.StackSize)
                                    {
                                        inv[o].Itemstack.StackSize += output.StackSize;
                                        output.StackSize = 0;
                                        break;
                                    }
                                    else
                                    {
                                        inv[o].Itemstack.StackSize += capleft;
                                        output.StackSize -= capleft;
                                        if (output.StackSize == 0) break;
                                    }
                                }
                                inv[o].MarkDirty();
                            }
                        }
                        // we have no other choice, everything is full, drop it, should stop the machine from processing more
                        if (output.StackSize > 0) Api.World.SpawnItemEntity(output, Pos.UpCopy(1).ToVec3d());
                        
                        InputSlot.TakeOut(_cproperties.SmeltedRatio);
                        InputSlot.MarkDirty();
                    }

                    if (InputSlot.Empty || !FindMatchingRecipe())
                    {                        
                        isCrafting = false;
                        isHeating = false;
                        SetState(EnumBEState.Sleeping);
                    }
                    _burntimeelapsed = 0;
                    recipePowerApplied = 0;                    
                    //Api.World.BlockAccessor.MarkBlockEntityDirty(this.Pos);
                }
            }
            this.MarkDirty(true, null);
        }

        protected virtual void SetState(EnumBEState newstate)
        {
            //if (MachineState == newstate) return; // no change, nothing to see here.            
            Electric.MachineState = newstate;

            if (Electric.MachineState == EnumBEState.On)
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
                clientDialog.Update(RecipeProgress, Electric.CurrentPower, currentTemp, currentRecipe, _cproperties);
            }
            MarkDirty(true);
        }


        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (this.Api != null && Api.Side == EnumAppSide.Client)
            {
                base.toggleInventoryDialogClient(byPlayer, delegate
                {
                    clientDialog = new GUIKiln(DialogTitle, Inventory, this.Pos, capi, this);
                    clientDialog.Update(RecipeProgress, Electric.CurrentPower, currentTemp, currentRecipe, _cproperties);
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
                GUIKiln gUILog = clientDialog;
                if (gUILog != null) { gUILog.Dispose(); }
                clientDialog = null;
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            float recipeProgressPercent = RecipeProgress * 100;
            dsc.AppendLine(isCrafting ? $"{Lang.Get("vinteng:gui-word-crafting")}: {recipeProgressPercent:N1}%" : $"{Lang.Get("vinteng:gui-machine-notcrafting")}");
            dsc.Append(isHeating ? $"{Lang.Get("vinteng:gui-word-heating")}: " : "");
            dsc.AppendLine($"{currentTemp:N1}°");
        }

        #region ServerClientStuff
        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(player, packetid, data);
            if (packetid == 1002) // Enable Button
            {
                if (Electric.IsEnabled) SetState(EnumBEState.Off); // turn off
                else
                {
                    SetState((IsCrafting || IsHeating) ? EnumBEState.On : EnumBEState.Sleeping);
                }
                MarkDirty(true, null);
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);
            if (clientDialog != null && clientDialog.IsOpened()) clientDialog.Update(RecipeProgress, Electric.CurrentPower, currentTemp, currentRecipe, _cproperties);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            inv.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;
            tree.SetLong("recipepowerapplied", (long)recipePowerApplied);
            tree.SetFloat("combustedtime", _burntimeelapsed);
            tree.SetBool("iscrafting", isCrafting);
            tree.SetBool("isheating", isHeating);
            tree.SetFloat("currenttemp", currentTemp);
            tree.SetFloat("worldtemp", environmentTemp);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            inv.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            if (!inv[0].Empty) FindMatchingRecipe();
            if (Api != null) inv.AfterBlocksLoaded(Api.World);
            recipePowerApplied = (ulong)tree.GetLong("recipepowerapplied");
            isCrafting = tree.GetBool("iscrafting", false);
            isHeating = tree.GetBool("isheating", false);
            _burntimeelapsed = tree.GetFloat("combustedtime", 0);
            environmentTemp = tree.GetFloat("worldtemp", 20);
            currentTemp = tree.GetFloat("currenttemp", environmentTemp);

            if (Api != null && Api.Side == EnumAppSide.Client) { SetState(Electric.MachineState); }
            if (clientDialog != null)
            {
                clientDialog.Update(RecipeProgress, Electric.CurrentPower, currentTemp, currentRecipe, _cproperties);
            }            
        }

        #endregion
    }
}
