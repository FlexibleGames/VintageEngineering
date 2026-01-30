using System.Reflection.Metadata.Ecma335;
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
            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

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

            if (byPlayer != null && hotbarSlot != null && !hotbarSlot.Empty)
            {
                if (hotbarSlot.Itemstack.Collectible.Code.Path.Contains("stick"))
                {                    
                    ItemSlotLiquidOnly pull = betank?.GetLiquidAutoPullFromSlot(blockSel.Face);
                    if (pull != null)
                    {
                        pull.TakeOutWhole(); // void the tank
                    }
                }
                ILiquidSource source = hotbarSlot.Itemstack.Collectible as ILiquidSource;
                if (source != null)
                {
                    if (!source.AllowHeldLiquidTransfer) return false;
                    ItemStack contentstomove = source.GetContent(hotbarSlot.Itemstack);
                    if (contentstomove != null && contentstomove.StackSize > 0)
                    {
                        DummySlot topush; // = new(contentstomove);
                        if (hotbarSlot.Itemstack.StackSize > 1)
                        {
                            // we are holding more than one bucket
                            ItemStack singlebucket = hotbarSlot.Itemstack.Clone();
                            singlebucket.StackSize = 1;
                            topush = new((singlebucket.Collectible as ILiquidSource).GetContent(singlebucket));
                        }
                        else
                        {
                            // just one bucket
                            topush = new(contentstomove);
                        }
                        IVELiquidInterface ivel = betank as IVELiquidInterface;
                        ItemSlotLargeLiquid pushto = (ItemSlotLargeLiquid)ivel.GetLiquidAutoPushIntoSlot(blockSel.Face, topush);
                        if (pushto == null) return true;
                        WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(contentstomove);
                        int capacityavailable = (int)(pushto.CapacityLitres * props.ItemsPerLitre) - (int)(pushto.StackSize);

                        int nummoved = pushto.TryTakeFrom(world, topush, topush.StackSize);
                        
                        if (nummoved > 0)
                        {
                            SplitStackAndPerformAction(byPlayer.Entity, hotbarSlot, delegate (ItemStack stack)
                            {
                                source.TryTakeContent(stack, nummoved);
                                return nummoved;
                            });
                            DoLiquidMovedEffects(byPlayer, contentstomove, nummoved, EnumLiquidDirection.Pour);
                            betank.MarkDirty(true);
                            return true;
                        }
                    }
                }

                ILiquidSink sink = hotbarSlot.Itemstack.Collectible as ILiquidSink;
                if (sink != null)
                {
                    if (!sink.AllowHeldLiquidTransfer) return false;
                    ItemStack owncontentstack = GetContent(blockSel.Position);
                    if (owncontentstack == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);
                    ItemStack liquidstackforparticles = owncontentstack.Clone();

                    float liters = GameMath.Max(sink.TransferSizeLitres, sink.CapacityLitres);
                    int moved2 = SplitStackAndPerformAction(byPlayer.Entity, hotbarSlot, (ItemStack stack) => sink.TryPutLiquid(stack, owncontentstack, liters));
                    if (moved2 > 0)
                    {
                        TryTakeContent(blockSel.Position, moved2);
                        DoLiquidMovedEffects(byPlayer, liquidstackforparticles, moved2, EnumLiquidDirection.Fill);
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
            fillHeight -= fillHeight == 1 ? 0.001f : 0f; // hopefully prevents the top from z-fighting
            if (fillHeight == 0f)
            {
                return null;
            }
            
           
            Shape shape = Vintagestory.API.Common.Shape.TryGet(capi, "vinteng:shapes/block/fluidtankliquid.json");
            MeshData liquidmesh;
            capi!.Tesselator.TesselateShape("Liquid in fluid tank", shape, out liquidmesh, contentSource);
            liquidmesh.Translate(0f, 0f, 0f);
            liquidmesh.Scale(new Vec3f(0.5f, 0.0625f, 0.5f),1f, fillHeight, 1f);
            return liquidmesh;
        }
    }
}
