using VintageEngineering.Transport.API;
using VintageEngineering.Transport.Handlers;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

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

        public override bool CanConnectTo(IWorldAccessor world, BlockPos pos)
        {            
            IBlockEntityContainer bec = world.BlockAccessor.GetBlock(pos).GetInterface<IBlockEntityContainer>(world, pos);
            if (bec != null)
            {
                // TODO check and load config blacklist of assemblies and types to ignore
                return true;
            }
            return false;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
        }
    }
}
