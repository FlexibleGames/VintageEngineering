using System;
using System.Collections.Generic;
using VintageEngineering.Electrical;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VintageEngineering
{
    public class ElectricBlockWithFluid: ElectricBlock, ILiquidInterface, ILiquidSink, ILiquidSource
    {
        ICoreClientAPI capi;
        ICoreServerAPI sapi;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
            }
            else
            {
                capi = api as ICoreClientAPI;
            }
        }
        public virtual bool AllowHeldLiquidTransfer => throw new NotImplementedException();

        public virtual float CapacityLitres => throw new NotImplementedException();

        public virtual float TransferSizeLitres => throw new NotImplementedException();

        public virtual ItemStack GetContent(ItemStack containerStack)
        {
            throw new NotImplementedException();
        }

        public virtual ItemStack GetContent(BlockPos pos)
        {
            throw new NotImplementedException();
        }

        public virtual WaterTightContainableProps GetContentProps(ItemStack containerStack)
        {
            throw new NotImplementedException();
        }

        public virtual WaterTightContainableProps GetContentProps(BlockPos pos)
        {
            throw new NotImplementedException();
        }

        public virtual float GetCurrentLitres(ItemStack containerStack)
        {
            throw new NotImplementedException();
        }

        public virtual float GetCurrentLitres(BlockPos pos)
        {
            throw new NotImplementedException();
        }

        public virtual bool IsFull(ItemStack containerStack)
        {
            throw new NotImplementedException();
        }

        public virtual bool IsFull(BlockPos pos)
        {
            throw new NotImplementedException();
        }

        public virtual void SetContent(ItemStack containerStack, ItemStack content)
        {
            throw new NotImplementedException();
        }

        public virtual void SetContent(BlockPos pos, ItemStack content)
        {
            throw new NotImplementedException();
        }

        public virtual int TryPutLiquid(BlockPos pos, ItemStack liquidStack, float desiredLitres)
        {
            throw new NotImplementedException();
        }

        public virtual int TryPutLiquid(ItemStack containerStack, ItemStack liquidStack, float desiredLitres)
        {
            throw new NotImplementedException();
        }

        public virtual ItemStack TryTakeContent(ItemStack containerStack, int quantity)
        {
            throw new NotImplementedException();
        }

        public virtual ItemStack TryTakeContent(BlockPos pos, int quantity)
        {
            throw new NotImplementedException();
        }
    }
}
