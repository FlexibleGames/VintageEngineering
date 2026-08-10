using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
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
    public class BETrashCan : BlockEntityOpenableContainer
    {
        private string _dialogTitleLangCode = "trashcantitle";
        private InventoryGeneric _inventory;
        public string type = "normal";

        public string DialogTitle => Lang.Get("vinteng:" + _dialogTitleLangCode);

        public override InventoryBase Inventory => _inventory;

        public override string InventoryClassName => "trashcan";

        private BlockEntityAnimationUtil animationUtil
        {
            get
            {
                return this.GetBehavior<BEBehaviorAnimatable>()?.animUtil;
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            if (_inventory == null)
            {
                InitInventory(this.Block);
            }

            base.Initialize(api);
            LateInitInventory();

            if (api.Side == EnumAppSide.Client)
            {
                animationUtil.InitializeAnimator("trashcan", null, null, new Vec3f(0f, this.Block.Shape.rotateY, 0f));
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
        }

        public virtual void InitInventory(Block block)
        {
            if (((block != null) ? block.Attributes : null) != null)
            {
                _dialogTitleLangCode = block.Attributes["dialogTitleLangCode"]["normal"].AsString(_dialogTitleLangCode);
            }
            _inventory = new InventoryGeneric(1, null, null, null);
            _inventory.BaseWeight = 1f;
            _inventory.OnGetAutoPullFromSlot = new GetAutoPullFromSlotDelegate(GetAutoPullFromSlot);
            _inventory.OnGetAutoPushIntoSlot = new GetAutoPushIntoSlotDelegate(GetAutoPushIntoSlot);
            this.container.Reset();            
            _inventory.OnInventoryOpened += OnInvOpened;
            _inventory.OnInventoryClosed += OnInvClosed;            
            
        }

        public virtual void LateInitInventory()
        {
            _inventory.LateInitialize($"{InventoryClassName}-{Pos}", Api);
            _inventory.Pos = Pos;
            container.LateInit();
            _inventory.SlotModified += TrashItem;
            MarkDirty(false);
        }

        public virtual void TrashItem(int slotid)
        {
            if (!_inventory.Empty)
            {
                _inventory[0].TakeOutWhole();
                MarkDirty(true);
            }
        }

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (this.Api.World is IServerWorldAccessor)
            {
                byte[] data = BlockEntityContainerOpen.ToBytes("BlockEntityInventory", this.DialogTitle, (byte)1, this._inventory);
                ((ICoreServerAPI)this.Api).Network.SendBlockEntityPacket((IServerPlayer)byPlayer, this.Pos, 5000, data);
                byPlayer.InventoryManager.OpenInventory(this._inventory);
                data = SerializerUtil.Serialize<OpenContainerLidPacket>(new OpenContainerLidPacket(byPlayer.Entity.EntityId, this.LidOpenEntityId.Count > 0));
                ((ICoreServerAPI)this.Api).Network.BroadcastBlockEntityPacket(this.Pos, 5001, data, new IServerPlayer[]
                {
                    (IServerPlayer)byPlayer
                });
            }
            return true;
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            if (((byItemStack != null) ? byItemStack.Attributes : null) != null)
            {
                InitInventory(base.Block);
                LateInitInventory();
            }
            base.OnBlockPlaced(null);
        }

        protected virtual void OnInvOpened(IPlayer player)
        {
            base.OnInventoryOpened(player);
            if (Api.Side == EnumAppSide.Client)
            {
                OpenLid();
            }
        }

        protected virtual void OnInvClosed(IPlayer player)
        {
            base.OnInventoryClosed(player);
            if (this.LidOpenEntityId.Count == 0)
            {
                this.CloseLid();
            }            
            GuiDialogBlockEntity inv = this.invDialog;
            this.invDialog = null;
            if (inv != null && inv.IsOpened() && inv != null)
            {
                inv.TryClose();
            }
            if (inv != null)
            {
                inv.Dispose();
            }
        }

        public virtual void OpenLid()
        {
            BlockEntityAnimationUtil animUtil = this.animationUtil;
            if (animUtil != null && !animUtil.activeAnimationsByAnimCode.ContainsKey("lidopen"))
            {                
                animUtil.StartAnimation(new AnimationMetaData
                {
                    Animation = "lidopen",
                    Code = "lidopen",
                    AnimationSpeed = 1.8f,
                    EaseOutSpeed = 6f,
                    EaseInSpeed = 15f
                });
            }
        }

        public virtual void CloseLid()
        {
            BlockEntityAnimationUtil animUtil = this.animationUtil;
            if (animUtil != null && animUtil.activeAnimationsByAnimCode.ContainsKey("lidopen"))
            {
                animUtil.StopAnimation("lidopen");
            }
        }


        public ItemSlot GetAutoPullFromSlot(BlockFacing atFace)
        {
            return null;
        }

        public ItemSlot GetAutoPushIntoSlot(BlockFacing atFace, ItemSlot fromSlot)
        {
            return _inventory[0];
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            if (_inventory == null) InitInventory(base.Block);
            base.FromTreeAttributes(tree, worldForResolving);            
            if (Api != null && Api.Side == EnumAppSide.Client) MarkDirty(true);
        }
    }
}
