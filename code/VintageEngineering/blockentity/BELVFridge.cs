using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Electrical;
using VintageEngineering.inventory;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.API.Datastructures;

namespace VintageEngineering.blockentity
{
    public class BELVFridge : ElectricContainerBE
    {
        private ICoreServerAPI sapi;
        private ICoreClientAPI capi;

        private float _updateBouncer = 0f;

        private InvLVFridge _inventory;
        private string _doorAnimation = string.Empty;
        public override InventoryBase Inventory => _inventory;

        public string DialogTitle
        {
            get => Lang.Get("vinteng:gui-title-lvfridge");
        }

        public BELVFridge()
        {                              
            _inventory = new InvLVFridge();
        }

        public override string InventoryClassName => "InvLVFridge";

        public override void Initialize(ICoreAPI api)
        {
            if (_inventory._fridgeBE == null)
            {
                int slotnum = base.Block.Attributes["inventorynumslots"].AsInt(24);
                _inventory.Initialize(slotnum, this);
            }
            base.Initialize(api);    
            _doorAnimation = base.Block.Attributes["craftinganimcode"].AsString(string.Empty);
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
                RegisterGameTickListener(new Action<float>(OnSimTick), 100, 1000);
            }
            else
            {
                capi = api as ICoreClientAPI;
                if (AnimUtil != null)
                {
                    AnimUtil.InitializeAnimator("velvfridge", null, null, new Vec3f(0f, Electric.GetRotation(), 0f));
                }
            }
            SetState(Electric.MachineState);
            _inventory.Pos = this.Pos;
            _inventory.LateInitialize($"{InventoryClassName}-{this.Pos.X}/{this.Pos.Y}/{this.Pos.Z}", api);
            _inventory.OnInventoryOpened += OnInvOpened;
            _inventory.OnInventoryClosed += OnInvClosed;
        }

        protected void OnInvOpened(IPlayer player)
        {
            base.OnInventoryOpened(player);
            if (capi != null) OpenDoor();
        }

        protected void OnInvClosed(IPlayer player)
        {
            base.OnInventoryClosed(player);
            if (this.LidOpenEntityId.Count == 0)
            {
                CloseDoor();
            }
        }

        public void OnSimTick(float dt)
        {
            if (Electric.MachineState == EnumBEState.Sleeping)
            {
                _updateBouncer += dt;
                if (_updateBouncer < 2)
                {
                    return;
                }
                else _updateBouncer = 0f;
            }
            EnumBEState newstate = Electric.MachineState;
            if (Electric.CurrentPower > 0)
            {
                ulong rated = Electric.RatedPower(dt, true);
                if (Electric.CurrentPower >= rated)
                {
                    if (newstate != EnumBEState.On) newstate = EnumBEState.On;
                    long minpower = Math.Max(1, ((long)rated));
                    Electric.electricpower -= (ulong)minpower;
                }
                else newstate = EnumBEState.Sleeping;
            }
            else newstate = EnumBEState.Sleeping;

            if (Electric.MachineState != newstate) SetState(newstate);

            _clientUpdateDelay += dt;
            if (_clientUpdateDelay > 0.5)
            {
                MarkDirty(true);
                _clientUpdateDelay = 0f;
            }
        }

        public void OpenDoor()
        {            
            if (AnimUtil != null && _doorAnimation != string.Empty && !AnimUtil.activeAnimationsByAnimCode.ContainsKey(_doorAnimation))
            {
                AnimUtil.StartAnimation(new AnimationMetaData
                {
                    Animation = _doorAnimation,
                    Code = _doorAnimation,
                    AnimationSpeed = 1.6f,
                    EaseOutSpeed = 6f,
                    EaseInSpeed = 15f
                });
            }
        }

        public void CloseDoor()
        {
            if (AnimUtil != null && _doorAnimation != string.Empty && AnimUtil.activeAnimationsByAnimCode.ContainsKey(_doorAnimation))
            {
                AnimUtil.StopAnimation(_doorAnimation);
            }
        }

        public ItemSlot GetAutoPullFromSlot(BlockFacing atface)
        {
            if (_inventory.Empty) return null;

            return _inventory.FirstOrDefault((ItemSlot slot) => !slot.Empty);
        }

        public ItemSlot GetAutoPushSlot(BlockFacing atface, ItemSlot fromSlot)
        {
            return _inventory.GetBestSuitedSlot(fromSlot).slot;
        }

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (this.Api != null && Api.Side == EnumAppSide.Server)
            {
                byte[] data = BlockEntityContainerOpen.ToBytes("VELVFridge", DialogTitle, (byte)base.Block.Attributes["inventoryguicolumns"].AsInt(6), _inventory);
                sapi.Network.SendBlockEntityPacket(byPlayer as IServerPlayer, this.Pos, 5000, data);
                byPlayer.InventoryManager.OpenInventory(_inventory);
                data = SerializerUtil.Serialize<OpenContainerLidPacket>(new OpenContainerLidPacket(byPlayer.Entity.EntityId, LidOpenEntityId.Count > 0));
                sapi.Network.BroadcastBlockEntityPacket(Pos, 5001, data, [byPlayer as IServerPlayer]); 
            }
            return true;
        }        

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            if (packetid == 5001)
            {
                OpenContainerLidPacket containerPacket = SerializerUtil.Deserialize<OpenContainerLidPacket>(data);                
                if (containerPacket.Opened)
                {
                    this.LidOpenEntityId.Add(containerPacket.EntityId);
                    OpenDoor();
                }
                else
                {
                    this.LidOpenEntityId.Remove(containerPacket.EntityId);
                    if (this.LidOpenEntityId.Count == 0)
                    {
                        CloseDoor();
                    }
                }
            }
            else base.OnReceivedServerPacket(packetid, data);
        }        

        public void SetState(EnumBEState state)
        {
            //if (Electric.MachineState == state) return;
            Electric.MachineState = state;
            _inventory.UpdateSpoilRates(state);
            MarkDirty(true);
        }
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            _inventory.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            _inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            if (_inventory._fridgeBE == null) _inventory._fridgeBE = this;
            if (Api != null && Api.Side == EnumAppSide.Client) 
            { 
                SetState(Electric.MachineState); 
            }
        }
    }
}
