﻿using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.GameContent;
using Vintagestory.API.Datastructures;
using VintageEngineering.Electrical;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using System.Text;

namespace VintageEngineering
{
    public class BELVGenerator : ElectricContainerBE
    {
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        private InvLVGenerator inventory;
        private GUILVGenerator clientDialog;
                
        private float tempToGen = 100;
        private float prevGenTemp = 20f;
        private float genTemp = 20f;
        
        private int maxTemp;
        private float fuelBurnTime;
        private float maxBurnTime;
        private float sleepTimer = 0;

        /// <summary>
        /// N E S W
        /// </summary>
        private bool[] faceHasMachine = new bool[4];

        public float FuelBurnTime { get { return fuelBurnTime; } }
        public float GenTemp { get { return genTemp; } }

        public bool IsBurning
        {
            get
            {
                return fuelBurnTime > 0f;
            }
        }

        private ItemSlot FuelSlot
        {
            get
            {
                return this.inventory[0];
            }
        }
        private ItemStack FuelStack
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
                return Lang.Get("vinteng:gui-title-lvgenerator");
            }
        }

        public override string InventoryClassName { get { return "VELVGeneratorInv"; } }

        public BELVGenerator()
        {
            this.inventory = new InvLVGenerator(null, null);
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
                GUILVGenerator testGenGUI = this.clientDialog;
                if (testGenGUI != null) testGenGUI.Dispose();
                this.clientDialog = null;
            }
        }

        public void OnSlotModified(int slotId)
        {
            if (slotId == 0)
            {
                if (Inventory[0].Itemstack != null && !Inventory[0].Empty && Inventory[0].Itemstack.Collectible.CombustibleProps != null)
                {
                    sleepTimer += 5; // ensure the machine gets a full upate next tick
                    if (fuelBurnTime == 0) CanDoBurn(); // update burn time if we have no burn time left.
                    SetState(EnumBEState.On);
                }
                else
                {
                    SetState(EnumBEState.Sleeping);
                }
            }
            base.Block = this.Api.World.BlockAccessor.GetBlock(this.Pos);
            this.MarkDirty(this.Api.Side == EnumAppSide.Server, null);
            if (this.Api is ICoreClientAPI && this.clientDialog != null)
            {
                clientDialog.Update(genTemp, fuelBurnTime, Electric.CurrentPower);
            }
            IWorldChunk chunkatPos = this.Api.World.BlockAccessor.GetChunkAtBlockPos(this.Pos);
            if (chunkatPos == null) return;
            chunkatPos.MarkModified();
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            dsc.AppendLine($"{GenTemp:N1}°C {FuelBurnTime:N1} {Lang.Get("vinteng:gui-word-seconds")}");
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
                clientDialog.Update(GenTemp, fuelBurnTime, Electric.CurrentPower);
            }
            MarkDirty(true, null);
        }

        public void OnBurnTick(float deltatime)
        {
            if (this.Api is ICoreServerAPI)
            {
                if (!Electric.IsEnabled) return;
                if (Electric.IsSleeping)
                {
                    if (genTemp != 20f) genTemp = ChangeTemperature(genTemp, 20f, deltatime);
                    sleepTimer += deltatime;
                    if (sleepTimer < 2f) return;
                    else sleepTimer = 0;
                }

                if (Electric.CurrentPower != Electric.MaxPower)
                {
                    if (fuelBurnTime > 0f || genTemp >= tempToGen) // now will generate additional power as long as it's hot enough.
                    {
                        // we have space for power AND we have fuel
                        SetState(EnumBEState.On); // turn it on!
                        genTemp = ChangeTemperature(genTemp, maxTemp, deltatime);
                        fuelBurnTime -= deltatime; // burn!
                        if (fuelBurnTime <= 0f) 
                        { 
                            fuelBurnTime = 0f;
                            maxBurnTime = 0f;
                            maxTemp = 20; // important
                            if (!Inventory[0].Empty) CanDoBurn();
                        }

                        if (genTemp >= tempToGen)
                        {
                            // we're hot enough to generate power, currenly hard coded to 100
                            Electric.electricpower += ((ulong)Math.Round(Electric.MaxPPS * deltatime));
                            if (Electric.CurrentPower > Electric.MaxPower) Electric.electricpower = Electric.MaxPower;
                        }
                    }
                    else
                    {
                        // we have space for power and no burn time
                        if (genTemp != 20f) genTemp = ChangeTemperature(genTemp, 20f, deltatime);
                        SetState(EnumBEState.Sleeping); // go to sleep... zzzz
                        CanDoBurn(); // check for fuel for the next tick
                    }
                }
                else
                {
                    // power is full
                    if (genTemp != 20f) genTemp = ChangeTemperature(genTemp, 20f, deltatime); // cool it down
                    SetState(EnumBEState.Sleeping); // go to sleep... zzzz
                }
                prevGenTemp = genTemp;
                if ((faceHasMachine[0] || faceHasMachine[1] || faceHasMachine[2] || faceHasMachine[3]) && Electric.CurrentPower > 0)
                {
                    if (GiveNeighborsPower(deltatime)) SetState(EnumBEState.On);
                }
                MarkDirty(true, null);
            }
            if (Api != null && Api.Side == EnumAppSide.Client)
            {
                if (this.clientDialog != null) clientDialog.Update(genTemp, fuelBurnTime, Electric.CurrentPower);
            }
        }

        public void CanDoBurn()
        {            
            CombustibleProperties fuelProps = FuelSlot.Itemstack?.Collectible.CombustibleProps;
            if (fuelProps == null) return;
            if (fuelBurnTime > 0) return; // we're already burning
            if (fuelProps.BurnTemperature > 0f && fuelProps.BurnDuration > 0f)
            {
                maxBurnTime = fuelBurnTime = fuelProps.BurnDuration;
                maxTemp = fuelProps.BurnTemperature;
                FuelStack.StackSize--;
                if (FuelStack.StackSize <= 0)
                {
                    FuelStack = null;
                }
                SetState(EnumBEState.On); // ensure we're on!
                FuelSlot.MarkDirty();
                MarkDirty(true);
            }
        }

        public bool GiveNeighborsPower(float dt)
        {
            // a temporary routine to push power into a machine, will be an electric network eventually
            IElectricalBlockEntity beElectricalMachine;
            ulong ratedpower = Electric.RatedPower(dt);
            ulong ratedpowerbackup = ratedpower;
            if (ratedpower > Electric.CurrentPower) ratedpower = Electric.CurrentPower;

            for (int x = 0; x < 4; x++)
            {
                if (ratedpower > 0)
                {
                    if (faceHasMachine[x])
                    {                        
                        // we have power and this face has a machine... lets give some power
                        switch(x)
                        {
                            case 0: 
                                beElectricalMachine = this.Api.World.BlockAccessor.GetBlockEntity(this.Pos.NorthCopy()) as IElectricalBlockEntity;
                                if (beElectricalMachine != null)
                                {
                                    if (!beElectricalMachine.CanReceivePower) break;
                                    ratedpower = beElectricalMachine.ReceivePower(ratedpower, dt);
                                    if (ratedpower == 0) break;
                                }
                                break;
                            case 1:
                                beElectricalMachine = this.Api.World.BlockAccessor.GetBlockEntity(this.Pos.EastCopy()) as IElectricalBlockEntity;
                                if (beElectricalMachine != null)
                                {
                                    if (!beElectricalMachine.CanReceivePower) break;
                                    ratedpower = beElectricalMachine.ReceivePower(ratedpower, dt);
                                    if (ratedpower == 0) break;
                                }
                                break;
                            case 2:
                                beElectricalMachine = this.Api.World.BlockAccessor.GetBlockEntity(this.Pos.SouthCopy()) as IElectricalBlockEntity;
                                if (beElectricalMachine != null)
                                {
                                    if (!beElectricalMachine.CanReceivePower) break;
                                    ratedpower = beElectricalMachine.ReceivePower(ratedpower, dt);
                                    if (ratedpower == 0) break;
                                }
                                break;
                            case 3:
                                beElectricalMachine = this.Api.World.BlockAccessor.GetBlockEntity(this.Pos.WestCopy()) as IElectricalBlockEntity;
                                if (beElectricalMachine != null)
                                {
                                    if (!beElectricalMachine.CanReceivePower) break;
                                    ratedpower = beElectricalMachine.ReceivePower(ratedpower, dt);
                                    if (ratedpower == 0) break;
                                }
                                break;
                        }
                    }
                }
            }
            ulong usedpower = ratedpowerbackup - ratedpower;
            if (usedpower > 0)
            {
                Electric.electricpower -= usedpower;
                return true;
            }
            else return false;
        }

        public void NeighborUpdate(IWorldAccessor world)
        {
            // checks neighbor blocks searching for a machine.
            // for the test, only check horizontally N E S W
            faceHasMachine[0] = false;
            if (world.BlockAccessor.GetBlockEntity(this.Pos.NorthCopy()) is IElectricalBlockEntity)
            {
                faceHasMachine[0] = true;
            }

            faceHasMachine[1] = false;
            if (world.BlockAccessor.GetBlockEntity(this.Pos.EastCopy()) is IElectricalBlockEntity)
            {
                faceHasMachine[1] = true;
            }

            faceHasMachine[2] = false;
            if (world.BlockAccessor.GetBlockEntity(this.Pos.SouthCopy()) is IElectricalBlockEntity)
            {
                faceHasMachine[2] = true;
            }

            faceHasMachine[3] = false;
            if (world.BlockAccessor.GetBlockEntity(this.Pos.WestCopy()) is IElectricalBlockEntity)
            {
                faceHasMachine[3] = true;
            }
        }
        

        public float ChangeTemperature(float fromTemp, float toTemp, float deltaTime)
        {
            float diff = Math.Abs(fromTemp - toTemp);
            deltaTime += deltaTime * (diff / 28f);
            if (diff < deltaTime)
            {
                return toTemp;
            }
            if (fromTemp > toTemp)
            {
                deltaTime = -deltaTime;
            }
            if (Math.Abs(fromTemp - toTemp) < 1f)
            {
                return toTemp;
            }
            return fromTemp + deltaTime;
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
                if (AnimUtil != null)
                {
                    AnimUtil.InitializeAnimator("velvgenerator", null, null, new Vec3f(0f, Electric.GetRotation(), 0f));
                }
            }
            this.inventory.Pos = this.Pos;
            this.inventory.LateInitialize($"{InventoryClassName}-{this.Pos.X}/{this.Pos.Y}/{this.Pos.Z}", api);
            this.RegisterGameTickListener(new Action<float>(OnBurnTick), 100, 0);
            CanDoBurn();
        }

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (this.Api.Side == EnumAppSide.Client)
            {
                base.toggleInventoryDialogClient(byPlayer, delegate
                {
                    this.clientDialog = new GUILVGenerator(DialogTitle, Inventory, this.Pos, this.Api as ICoreClientAPI, this);
                    clientDialog.Update(genTemp, fuelBurnTime, Electric.CurrentPower);
                    return this.clientDialog;
                });
            }
            return true;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            this.inventory.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;
            
            ITreeAttribute facetree = new TreeAttribute();
            ToFaceTree(facetree);
            tree["faceConnections"] = facetree;

            tree.SetFloat("genTemp", genTemp);
            tree.SetInt("maxTemp", maxTemp);
            tree.SetFloat("fuelBurnTime", fuelBurnTime);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            this.inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            FromFaceTree(tree.GetTreeAttribute("faceConnections"));
            if (Api != null) Inventory.AfterBlocksLoaded(this.Api.World);
            genTemp = tree.GetFloat("genTemp", 0);
            maxTemp = tree.GetInt("maxTemp", 0);
            fuelBurnTime = tree.GetFloat("fuelBurnTime", 0);
            if (Api != null && Api.Side == EnumAppSide.Client)
            {
                SetState(Electric.MachineState);
                if (this.clientDialog != null) clientDialog.Update(genTemp, fuelBurnTime, Electric.CurrentPower);
                MarkDirty(true, null);
            }
        }

        private void ToFaceTree(ITreeAttribute tree)
        {
            tree.SetBool("faceNorth", faceHasMachine[0]);
            tree.SetBool("faceEast", faceHasMachine[1]);
            tree.SetBool("faceSouth", faceHasMachine[2]);
            tree.SetBool("faceWest", faceHasMachine[3]);
        }
        private void FromFaceTree(ITreeAttribute tree)
        {
            if (tree == null)
            {
                // failsafe in case of a missing tree attribute.
                faceHasMachine[0] = faceHasMachine[1] = faceHasMachine[2] = faceHasMachine[3] = false;
                return;
            }
            faceHasMachine[0] = tree.GetBool("faceNorth");
            faceHasMachine[1] = tree.GetBool("faceEast");
            faceHasMachine[2] = tree.GetBool("faceSouth");
            faceHasMachine[3] = tree.GetBool("faceWest");
        }
    }
}
