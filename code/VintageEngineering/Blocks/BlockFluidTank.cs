using VintageEngineering.API;
using VintageEngineering.blockentity;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VintageEngineering.Blocks
{
    public class BlockFluidTank: BlockLiquidContainerBase
    {
  
        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            return true;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel != null && !world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }
            BEFluidTank betank = null;
            if (blockSel.Position != null)
            {
                betank = (world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEFluidTank);
            }
            if (betank == null) return false;

            if (byPlayer != null && byPlayer.InventoryManager.ActiveHotbarSlot != null && !byPlayer.InventoryManager.ActiveHotbarSlot.Empty)
            {
                ILiquidSink bucket = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible as ILiquidSink;
                if (bucket != null)
                {
                    ItemStack contents = bucket.GetContent(byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack);
                    if (contents != null)
                    {
                        DummySlot topush = new DummySlot(contents);
                        IVELiquidInterface ivel = betank as IVELiquidInterface;
                        ItemSlotLargeLiquid push = (ItemSlotLargeLiquid)ivel.GetLiquidAutoPushIntoSlot(blockSel.Face, topush);
                        if (push == null) return true;
                        WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(contents);
                        int capacityavailable = (int)(push.CapacityLitres * props.ItemsPerLitre) - (int)(push.StackSize);
                        if (capacityavailable >= contents.StackSize)
                        {
                            push.TryTakeFrom(world, topush, topush.StackSize);
                            //topush.TryPutInto(api.World, push, topush.StackSize);
                            bucket.SetContent(byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack, null);
                        }
                        else
                        {
                            int moved = push.TryTakeFrom(world, topush, topush.StackSize); // topush.TryPutInto(api.World, push, topush.StackSize);
                            contents.StackSize -= moved;
                            bucket.SetContent(byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack, contents);
                        }
                        betank.MarkDirty(true);
                        return true;
                    }
                    else
                    {
                        IVELiquidInterface ivel = betank as IVELiquidInterface;
                        ItemSlotLiquidOnly pull = ivel.GetLiquidAutoPullFromSlot(blockSel.Face);
                        if (pull == null) return true;
                        WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(pull.Itemstack);
                        int cancontain = (int)(bucket.CapacityLitres * props.ItemsPerLitre);
                        if (cancontain >= pull.Itemstack.StackSize)
                        {
                            bucket.SetContent(byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack, pull.Itemstack.Clone());
                            pull.TakeOutWhole();
                        }
                        else
                        {
                            ItemStack pulled = pull.Itemstack.Clone();
                            pulled.StackSize = cancontain;
                            bucket.SetContent(byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack, pulled.Clone());
                            pull.TakeOut(cancontain);
                        }
                        betank.MarkDirty(true);
                        return true;
                    }
                }
            }

            bool handled = base.OnBlockInteractStart(world, byPlayer, blockSel);

            return true;
        }
        
        public MeshData GenMesh(ItemStack liquidContentStack, float capacity)
        {
            if (liquidContentStack == null || api.Side == EnumAppSide.Server) return null;
            ICoreClientAPI capi = api as ICoreClientAPI;
            WaterTightContainableProps props = GetContainableProps(liquidContentStack);
            ITexPositionSource contentSource;
            float fillHeight;
            // if it is not a liquid or the liquid had no texture 
            if (props?.Texture == null)
            {
                return null;
            }

            contentSource = new ContainerTextureSource(capi, liquidContentStack, props.Texture);
            fillHeight = (liquidContentStack.StackSize / props.ItemsPerLitre) / capacity;

            if (fillHeight == 0f)
            {
                return null;
            }
            
           
            Shape shape = Vintagestory.API.Common.Shape.TryGet(capi, "vinteng:shapes/block/liquid.json");
            MeshData liquidmesh;
            capi!.Tesselator.TesselateShape("Liquid in fluid tank", shape, out liquidmesh, contentSource);
            liquidmesh.Translate(0f, 0f, 0f);
            liquidmesh.Scale(new Vec3f(1/16f, 1/16f, 1/16f),1f, fillHeight, 1f);
            return liquidmesh;
        }
    }
}
