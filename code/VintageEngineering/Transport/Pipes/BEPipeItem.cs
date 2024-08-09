using VintageEngineering.Transport.API;
using VintageEngineering.Transport.Handlers;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VintageEngineering.Transport.Pipes
{
    public class BEPipeItem : BEPipeBase
    {
        private static ItemTransportHandler itemHandler = new();

        public override void Initialize(ICoreAPI api)
        {            
            base.Initialize(api);
        }

        public override ITransportHandler GetHandler()
        {
            if (Api == null || Api.Side == EnumAppSide.Client) return null;
            return itemHandler;
        }

        public override bool CanConnectTo(IWorldAccessor world, BlockPos pos, BlockFacing toFace = null)
        {            
            IBlockEntityContainer bec = world.BlockAccessor.GetBlock(pos).GetInterface<IBlockEntityContainer>(world, pos);            
            if (bec != null)
            {
                // TODO check and load config blacklist of assemblies and types to ignore

                // now all the special vanilla block conditions, ugh, is there a better way to do this?
                // these are all BlockEntities that have inventories that should NOT be interacted with at all using pipes.
                BlockEntity entity = world.BlockAccessor.GetBlockEntity(pos);
                if (entity == null) return false;
                if (entity is BECheese) return false;
                if (entity is BECheeseCurdsBundle) return false;
                if (entity is BlockEntityBarrel) { if (toFace != null) { if (toFace == BlockFacing.UP) return true; else return false; } else return false; }
                if (entity is BlockEntityBucket) return false;
                if (entity is BlockEntityBoiler) return false;
                if (entity is BlockEntityCondenser) return false;
                if (entity is BlockEntityAnimalBasket) return false;
                if (entity is BlockEntityAnvilPart) return false;
                if (entity is BlockEntityBlockRandomizer) return false;
                if (entity is BlockEntityCookedContainer) return false;
                if (entity is BlockEntityCrock) return false;
                if (entity is BlockEntityDeadCrop) return false;
                if (entity is BlockEntityDisplay) return false;
                if (entity is BlockEntityFruitPress) return false;
                if (entity is BlockEntityMeal) return false;
                if (entity is BlockEntityPie) return false;
                if (entity is BlockEntityPlantContainer) return false;
                if (entity is BlockEntityResonator) return false;
                if (entity is BlockEntityStoneCoffin) return false;
                if (entity is BlockEntityTrough) return false;

                bool onlyFluid = true;
                foreach (ItemSlot slot in bec.Inventory)
                {
                    if (slot is not ItemSlotLiquidOnly) onlyFluid = false;
                }
                return !onlyFluid; // if an inventory is only fluid, don't connect (like the bucket)
            }
            return false;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
        }
    }
}
