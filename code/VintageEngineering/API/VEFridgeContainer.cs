using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace VintageEngineering.API
{
    public class VEFridgeContainer : InWorldContainer
    {
        BELVFridge _entity;
        public VEFridgeContainer(InventorySupplierDelegate invSupplier, string treeAttrKey, BELVFridge l_entity) : base(invSupplier, treeAttrKey) 
        {
            _entity = l_entity;
        }

        public override float GetPerishRate()
        {
            if (_entity == null) return base.GetPerishRate();
            float rate = 0f;
            if (_entity.Electric.MachineState == EnumBEState.On)
            {
                rate = (Inventory as InventoryGeneric).PerishableFactorByFoodCategory[EnumFoodCategory.Vegetable];
                rate = _entity.Block.Attributes["spoilrate"]["powered"].AsFloat(0.25f);
            }
            else
            {
                rate = _entity.Block.Attributes["spoilrate"]["unpowered"].AsFloat(2.10f);
            }
            return rate;
        }
    }
}
