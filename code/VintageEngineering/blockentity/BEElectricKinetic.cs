using System;
using VintageEngineering.blockBhv;
using VintageEngineering.Electrical;
using VintageEngineering.GUI;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent.Mechanics;


namespace VintageEngineering.blockentity
{
    public class BEElectricKinetic : ElectricContainerBE
    {

        public bool isGenerator { get { return Block.Code.Path.Contains("alternator"); } }

        //public ElectricBEBehavior Electricity;

        private long _clientUpdateMS = 0L;

        private ElectricKineticMotorBhv genBhv;
        private ElectricKineticAlternatorBhv consBhv;

        private float sleepTimer = 0;

        private float _speedSetting = 0.0f;
        private float _torqueSetting = 0.0f;

        GUILVMotor clientDialog;

        /// <summary>
        /// What the current Speed is set to for this Motor<br/>
        /// Not used for the Alternator
        /// </summary>
        public float SpeedSetting { get { return _speedSetting; } set { _speedSetting = value; } }
        /// <summary>
        /// What the Resistance is set to for this Motor<br/>
        /// Not used for the Alternator
        /// </summary>
        public float TorqueSetting { get { return _torqueSetting; } set { _torqueSetting = value; } }

        public BEBehaviorMPBase Mechanical
        {
            get
            {
                if (isGenerator)
                {
                    return consBhv;
                }
                else
                {
                    return genBhv;
                }
            }
        }

        private string DialogTitle = Lang.Get("vinteng:gui-title-motor");
        private InventoryGeneric inventory;
        public override InventoryBase Inventory => inventory;
        public BEElectricKinetic()
        {
            inventory = new InventoryGeneric(1, null, null, null);
        }
        public override string InventoryClassName => "VintEngElectricKinetic";
        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!isGenerator && this.Api != null && Api.Side == EnumAppSide.Client)
            {
                base.toggleInventoryDialogClient(byPlayer, delegate
                {
                    clientDialog = new GUILVMotor(DialogTitle, Inventory, this.Pos, base.Api as ICoreClientAPI, this);
                    clientDialog.Update(Electric.CurrentPower, _speedSetting, _torqueSetting);
                    return this.clientDialog;
                });
            }
            return true;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(OnSimTick, 100, 0);
            }
            inventory.Pos = this.Pos.Copy();
            inventory.LateInitialize($"{InventoryClassName}-{this.Pos.X}/{this.Pos.Y}/{this.Pos.Z}", api);

            _clientUpdateMS = api.World.ElapsedMilliseconds;
        }
        public void OnSimTick(float dt)
        {
            //if(Api.Side == EnumAppSide.Client) { return; }
            if (!Electric.IsEnabled) return;
            if (Electric.IsSleeping)
            {
                if (sleepTimer < 2f) { sleepTimer += dt; return; }
                else sleepTimer = 0;
            }

            if (isGenerator)
            {
                if (consBhv != null)
                {
                    float powermade = consBhv.GetPowerProduced();
                    Electric.electricpower += (ulong)powermade;
                    if (Electric.CurrentPower > Electric.MaxPower) Electric.electricpower = Electric.MaxPower;
                }
            }
            else
            {
                float PPT = Electric.MaxPPS * dt;
                if (Electric.CurrentPower == 0 || Electric.CurrentPower < PPT) { return; }
                if(genBhv != null)
                {
                    Single powerwanted = genBhv.GetElectricalPowerRequired();
                    if (Electric.CurrentPower >= powerwanted)
                    {
                        // if this is out of power, then it should just stop providing mechanical power                        
                        Electric.electricpower -= (ulong)Math.Round(powerwanted);
                    }
                }
            }
            // update client values every half second
            if (Api.World.ElapsedMilliseconds - _clientUpdateMS > 500L)
            {
                _clientUpdateMS = Api.World.ElapsedMilliseconds;
                MarkDirty(true);
            }
        }

        public override void CreateBehaviors(Block block, IWorldAccessor worldForResolve)
        {
            base.CreateBehaviors(block, worldForResolve);
            consBhv = GetBehavior<ElectricKineticAlternatorBhv>();
            genBhv = GetBehavior<ElectricKineticMotorBhv>();
        }
        protected virtual void SetState(EnumBEState newstate)
        {
            //if (MachineState == newstate) return; // no change, nothing to see here.
            Electric.MachineState = newstate;

            if (Api != null && Api.Side == EnumAppSide.Client && clientDialog != null && clientDialog.IsOpened())
            {
                clientDialog.Update(Electric.CurrentPower, _speedSetting, _torqueSetting);
            }
            MarkDirty(true);
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
                    SetState(EnumBEState.On);
                }
                MarkDirty(true, null);
            }
            if (packetid == 1004)
            {
                // new Speed setting
                int newspeed = SerializerUtil.Deserialize<int>(data);
                _speedSetting = (float)newspeed / 100;
                MarkDirty(true);
            }
            if (packetid == 1005)
            {
                // new Resistance setting
                int newtorque = SerializerUtil.Deserialize<int>(data);
                _torqueSetting = (float)newtorque / 10;
                MarkDirty(true);
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);
            if (clientDialog != null && clientDialog.IsOpened()) clientDialog.Update(Electric.CurrentPower, _speedSetting, _torqueSetting);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetFloat("speed", _speedSetting);
            tree.SetFloat("resistance", _torqueSetting);
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            try
            {
                base.FromTreeAttributes(tree, worldAccessForResolve);
                _speedSetting = tree.GetFloat("speed", 0.0f);
                _torqueSetting = tree.GetFloat("resistance", 0.0f);
                if (Api != null && Api.Side == EnumAppSide.Client) { SetState(Electric.MachineState); }
                if (clientDialog != null)
                {
                    clientDialog.Update(Electric.CurrentPower, _speedSetting, _torqueSetting);
                }
            }
            catch (Exception) { }
        }
        #endregion
    }
}
