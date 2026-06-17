using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Electrical;
using VintageEngineering.inventory;
using VintageEngineering.Renderers;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VintageEngineering
{
    public class BELVCharger : ElectricContainerBE
    {
        private ICoreClientAPI capi;
        private ICoreServerAPI sapi;
        private int _powerperdurability;
        private float _updateBouncer = 0f;
        private VELVChargerRenderer _itemRenderer;        

        private InvCharger inventory;
        public override InventoryBase Inventory => inventory;

        public ItemSlot InputSlot => inventory[0];
        public override string InventoryClassName => "InvCharger";

        public BELVCharger()
        {
            inventory = new InvCharger(null, null);
            inventory.SlotModified += OnSlotModified;
        }
        private void OnSlotModified(int slotid)
        {
            _updateBouncer = 0f;
            if (_itemRenderer != null) _itemRenderer.SetContents(InputSlot.Itemstack, true);
            if (InputSlot.Empty) SetState(EnumBEState.Sleeping);
            else SetState(EnumBEState.On);
            MarkDirty(true);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            _powerperdurability = base.Block.Attributes["powerperdurability"].AsInt(25);
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
                RegisterGameTickListener(new Action<float>(OnSimTick), 100, 0);
            }
            else
            {
                capi = api as ICoreClientAPI;
                capi.Event.RegisterRenderer(_itemRenderer = new VELVChargerRenderer(base.Block, Pos, capi, this), EnumRenderStage.Opaque, "velvcharger");                
                if (AnimUtil != null)
                {
                    AnimUtil.InitializeAnimator("velvcharger", null, null, new Vec3f(0, Electric.GetRotation(), 0f));
                }
            }
            inventory.Pos = this.Pos;
            inventory.LateInitialize($"{InventoryClassName}-{this.Pos.X}/{this.Pos.Y}/{this.Pos.Z}", api);
            _itemRenderer?.SetContents(InputSlot.Itemstack, true);
        }

        public void OnSimTick(float dt)
        {
            _clientUpdateDelay += dt;

            if (InputSlot.Empty) return;

            if (Electric.IsSleeping || Electric.MachineState == EnumBEState.Paused)
            {
                _updateBouncer += dt;
                if (_updateBouncer < 5f) return;
                 _updateBouncer = 0f;
            }
            if (Electric.RatedPower(dt, false) > Electric.CurrentPower)
            {
                if (Electric.MachineState != EnumBEState.Paused) SetState(EnumBEState.Paused);
                return; // not enough juice
            }
            // first lets check to see if it has the attribute, this is used if base-game durability
            // represents the 'charge' of the item...
            bool chargable = InputSlot.Itemstack.Collectible.Attributes["chargable"].AsBool(false);
            IChargeableItem chargeableItem = InputSlot.Itemstack.Collectible as IChargeableItem;
            
            // an example of how to USE power in your item:            
            /*
            ulong curpow = chargeableItem.CurrentPower;
            ulong tickpow = chargeableItem.RatedPower(InputSlot.Itemstack, dt, false);
            curpow -= curpow >= tickpow ? tickpow : 0;
            chargeableItem.SetPower(InputSlot.Itemstack, curpow);
            */

            if (chargeableItem == null && !chargable) return; // nothing to do with this. It shouldn't have been allowed into the inventory
            // we have something...
            if (chargable && chargeableItem == null)
            {
                // use the durability!
                int curcharge = InputSlot.Itemstack.Collectible.GetRemainingDurability(InputSlot.Itemstack);
                int maxcharge = InputSlot.Itemstack.Collectible.GetMaxDurability(InputSlot.Itemstack);
                if (curcharge < maxcharge)
                {
                    if (Electric.MachineState != EnumBEState.On) SetState(EnumBEState.On); // on and active.
                    ulong powertouse = Electric.RatedPower(dt, false);
                    // we can't restore fractional durability as its an INT,
                    // so the machine PPS _HAS_ to be >= 10*_powerperdurability, restore a minimum of 1.
                    int torestore = Math.Max(1, ((int)powertouse) / _powerperdurability);
                    curcharge += torestore;
                    if (curcharge > maxcharge) curcharge = maxcharge;
                    InputSlot.Itemstack.Collectible.SetDurability(InputSlot.Itemstack, curcharge);
                    Electric.electricpower -= powertouse;
                }
            }
            else
            {
                // use the interface!
                int curcharge = ((int)chargeableItem.CurrentPower);
                int maxcharge = ((int)chargeableItem.MaxPower);
                if (curcharge < maxcharge)
                {
                    if (Electric.MachineState != EnumBEState.On) { SetState(EnumBEState.On); }
                    ulong powertopush = chargeableItem.RatedPower(InputSlot.Itemstack, dt, false);
                    ulong powertouse = Electric.RatedPower(dt, false);
                    if (powertouse > powertopush) powertouse = powertopush;
                    ulong remaining = chargeableItem.ReceivePower(InputSlot.Itemstack, powertouse, dt, false);
                    if (remaining > 0) powertouse -= remaining;
                    Electric.electricpower -= powertouse;
                }
            }
            UpdateClient(dt);
        }
        
        /// <summary>
        /// Push updated information to client on a delay.
        /// </summary>
        /// <param name="dt">DeltaTime</param>
        private void UpdateClient(float dt)
        {
            if (Api.Side == EnumAppSide.Client) return;
            
            if (_clientUpdateDelay > 0.5f)
            {
                _clientUpdateDelay = 0f;
                MarkDirty(true);
            }
        }

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer.InventoryManager.ActiveHotbarSlot.Empty)
            {
                if (InputSlot.Empty) return true;
                //ItemSlot getfrom = inventory.GetAutoPullFromSlot(blockSel.Face);
                InputSlot.TryPutInto(Api.World, byPlayer.InventoryManager.ActiveHotbarSlot);
            }
            else
            {
                if (inventory.CanContain(InputSlot, byPlayer.InventoryManager.ActiveHotbarSlot))
                {
                    byPlayer.InventoryManager.ActiveHotbarSlot.TryFlipWith(InputSlot);
                }
            }
            return true;
        }

        public virtual void SetState(EnumBEState newstate)
        {
            bool changed = Electric.MachineState != newstate;
            Electric.MachineState = newstate;            
            if (changed) MarkDirty(true);
        }

        public override void OnBlockRemoved()
        {
            this.Dispose();
            base.OnBlockRemoved();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            this.Dispose();
        }
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            inventory.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            ItemStack prevStack = null;
            if (!InputSlot.Empty) prevStack = InputSlot.Itemstack.Clone();

            base.FromTreeAttributes(tree, worldForResolving);
            inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            if (Api != null) inventory.AfterBlocksLoaded(worldForResolving);

            if (Api != null && Api.Side == EnumAppSide.Client) SetState(Electric.MachineState);

            bool remesh = false;
            if (prevStack == null)
            {
                if (!InputSlot.Empty) remesh = true;
            }
            else
            {
                if (InputSlot.Empty) remesh = true;
                else remesh = prevStack.Collectible.Code.Path != InputSlot.Itemstack.Collectible.Code.Path;
            }
            _itemRenderer?.SetContents(InputSlot.Itemstack, remesh);
        }
    }
}
