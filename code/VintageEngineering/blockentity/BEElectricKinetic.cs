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
        private float _resistanceSetting = 0.0f;

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
        public float ResistanceSetting { get { return _resistanceSetting; } set { _resistanceSetting = value; } }

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

        private string DialogTitle = Lang.Get("vinteng:block-velvek-motor-*");
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
                    clientDialog.Update(Electric.CurrentPower, _speedSetting, _resistanceSetting);
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
                    float powermade = consBhv.ProducePower();
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
                    Single powerwanted = genBhv.GetMechanicalPowerRequired();
                    if (Electric.CurrentPower < powerwanted)
                    {
                        // if this is out of power, then it should just stop providing mechanical power
                        genBhv.ConsumePower(Electric.CurrentPower);
                        Electric.electricpower = 0;
                    }
                    else
                    {
                        Single consumed = genBhv.GetMechanicalPowerRequired();
                        genBhv.ConsumePower(consumed);
                        Electric.electricpower -= (ulong)Math.Round(consumed);
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
                clientDialog.Update(Electric.CurrentPower, _speedSetting, _resistanceSetting);
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
                    SetState((Electric.CurrentPower > 0) ? EnumBEState.On : EnumBEState.Sleeping);
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
                int newresist = SerializerUtil.Deserialize<int>(data);
                _resistanceSetting = (float)newresist / 100;
                MarkDirty(true);
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);
            if (clientDialog != null && clientDialog.IsOpened()) clientDialog.Update(Electric.CurrentPower, _speedSetting, _resistanceSetting);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetFloat("speed", _speedSetting);
            tree.SetFloat("resistance", _resistanceSetting);
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            try
            {
                base.FromTreeAttributes(tree, worldAccessForResolve);
                _speedSetting = tree.GetFloat("speed", 0.0f);
                _resistanceSetting = tree.GetFloat("resistance", 0.0f);
            }
            catch (Exception e) { }
        }
        #endregion
    }
}
