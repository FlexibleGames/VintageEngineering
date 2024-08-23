using System.Text;
using VintageEngineering.API;
using VintageEngineering.Blocks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VintageEngineering.blockentity
{
    public class BEFluidTank : BlockEntityLiquidContainer, IVELiquidInterface
    {
        private int _capacityLitres;        

        public int CapacityLitres => _capacityLitres;
        
        public override string InventoryClassName => "vefluidtank";
        
        private MeshData _liquidmesh;
        
        private BlockFluidTank _ownTankBlock;

        public BEFluidTank()
        {            
            inventory = new InventoryGeneric(1, null, null, delegate (int id, InventoryGeneric self)
            {
                return new ItemSlotLargeLiquid(self, 999999);
            });
            inventory.BaseWeight = 1f;
            inventory.SlotModified += OnSlotModified;
            
        }

        public void SetFluidOnPlace(ItemStack fluid)
        {
            if (inventory != null && inventory[0] != null && fluid != null)
            {
                if (inventory[0].Empty) inventory[0].Itemstack = fluid;
                else inventory[0].Itemstack.SetFrom(fluid);
                
            }
        }

        private void OnSlotModified(int slotid)
        {
            if (Api.Side == EnumAppSide.Client)
            {
                _liquidmesh = this.GenFluidMesh();
                if (_liquidmesh == null) return;
            }
            MarkDirty(true, null);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            //base.GetBlockInfo(forPlayer, dsc);
            
            if (!inventory[0].Empty) 
            {
                WaterTightContainableProps wprops = BlockLiquidContainerBase.GetContainableProps(inventory[0].Itemstack);
                float perliter = wprops != null ? wprops.ItemsPerLitre : 100f;
                dsc.AppendLine($"{inventory[0].Itemstack.GetName()}: {inventory[0].Itemstack.StackSize / perliter}L"); 
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            _ownTankBlock = (Block as BlockFluidTank);
            _capacityLitres = base.Block.Attributes["capacity"].AsInt(1000);

            (inventory[0] as ItemSlotLargeLiquid).SetCapacity(_capacityLitres);

            // client stuff for rendering content mesh?? Anyone want to tackle that for me?? :)

            if (api.Side != EnumAppSide.Client || _liquidmesh != null) return;
            _liquidmesh = GenFluidMesh();
            if (_liquidmesh == null) return;
            MarkDirty(true, null); // possible to markdirty only the mesh ?
        }

        private MeshData GenFluidMesh()
        {
            MeshData mesh = _ownTankBlock?.GenMesh(this.inventory[0].Itemstack, (inventory[0] as ItemSlotLiquidOnly).CapacityLitres);
            return mesh;
        }

        protected override ItemSlot GetAutoPushIntoSlot(BlockFacing atBlockFace, ItemSlot fromSlot)
        {
            return GetLiquidAutoPushIntoSlot(atBlockFace, fromSlot);
        }

        #region IVELiquidInterface
        public int[] InputLiquidContainerSlotIDs => new[] { 0 };

        public int[] OutputLiquidContainerSlotIDs => new[] { 0 };

        public bool AllowPipeLiquidTransfer => true;

        public bool AllowHeldLiquidTransfer => true;

        public float TransferSizeLitresPerSecond => 1;

        public ItemSlotLiquidOnly GetLiquidAutoPullFromSlot(BlockFacing blockFacing)
        {
            foreach (int slot in OutputLiquidContainerSlotIDs)
            {
                if (!Inventory[slot].Empty) return Inventory[slot] as ItemSlotLiquidOnly;
            }
            return null;
        }

        public ItemSlotLiquidOnly GetLiquidAutoPushIntoSlot(BlockFacing blockFacing, ItemSlot fromSlot)
        {
            foreach (int slot in InputLiquidContainerSlotIDs)
            {
                if (Inventory[slot].Empty) continue;
                if (fromSlot.Itemstack.Equals(Api.World, Inventory[slot].Itemstack, GlobalConstants.IgnoredStackAttributes))
                {
                    return Inventory[slot] as ItemSlotLiquidOnly;
                }
            }
            foreach (int slot in InputLiquidContainerSlotIDs)
            {
                if (Inventory[slot].Empty) return Inventory[slot] as ItemSlotLiquidOnly;
            }
            return null;
        }
        #endregion

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            if (Api != null && Api.Side == EnumAppSide.Client)
            {
                _liquidmesh = GenFluidMesh();
                if (_liquidmesh == null) return;
                MarkDirty(true, null);
            }
        }
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            base.OnTesselation(mesher, tessThreadTesselator);
            mesher.AddMeshData(_liquidmesh, 1);
            return false;
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            if (Api.Side == EnumAppSide.Client)
            {
                _liquidmesh.Dispose();
            }
        }
    }
}
