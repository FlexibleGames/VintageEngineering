using System.Collections.Generic;
using VintageEngineering.blockentity;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using static System.TimeZoneInfo;

namespace VintageEngineering.inventory
{
    public class InvLVFridge : InventoryGeneric
    {
        public BELVFridge _fridgeBE;

        public InvLVFridge() : base(null)
        {
            this.baseWeight = 3f;
            // Empty, MUST initialize!
        }
        public InvLVFridge(string inventoryID, int numSlots, ICoreAPI api, BELVFridge fridge) : base(numSlots, inventoryID, api)
        {
            slots = GenEmptySlots(numSlots);
            _fridgeBE = fridge;            
        }

        public override float GetSuitability(ItemSlot sourceSlot, ItemSlot targetSlot, bool isMerge)
        {
            if (sourceSlot != null && !sourceSlot.Empty && (sourceSlot.Itemstack.Attributes.HasAttribute("transitionstate") || sourceSlot.Itemstack.Collectible.CanSpoil(sourceSlot.Itemstack)))
            {
                return this.baseWeight + 4f;
            }
            else return this.baseWeight - 2f;
        }

        public void Initialize(int numSlots, BELVFridge fridge)
        {
            slots = GenEmptySlots(numSlots);
            _fridgeBE = fridge;            
        }

        public void UpdateSpoilRates(EnumBEState tostate)
        {
            float rate = 0f;
            if (tostate == EnumBEState.On)
            {
                rate = _fridgeBE.Block.Attributes["spoilrate"]["powered"].AsFloat(0.25f);
            }
            else
            {
                rate = _fridgeBE.Block.Attributes["spoilrate"]["unpowered"].AsFloat(2.1f);
            }
            TransitionableSpeedMulByType = new Dictionary<EnumTransitionType, float>
            {
                { EnumTransitionType.Cure, rate },
                { EnumTransitionType.Melt, rate },
                { EnumTransitionType.Perish, rate },
                { EnumTransitionType.Ripen, rate },
                { EnumTransitionType.Convert, rate }
            };
            PerishableFactorByFoodCategory = new Dictionary<EnumFoodCategory, float>
            {
                { EnumFoodCategory.Protein, rate },
                { EnumFoodCategory.Vegetable, rate },
                { EnumFoodCategory.Grain, rate },
                { EnumFoodCategory.Fruit, rate },
                { EnumFoodCategory.Dairy, rate }
            };
        }

        protected override float GetDefaultTransitionSpeedMul(EnumTransitionType transitionType)
        {
            if (TransitionableSpeedMulByType.ContainsKey(transitionType))
            {
                return TransitionableSpeedMulByType[transitionType];
            }
            else return 1f;
        }

        public override void FromTreeAttributes(ITreeAttribute tree)
        {
            this.slots = this.SlotsFromTreeAttributes(tree, this.slots, null);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.SlotsToTreeAttributes(slots, tree);
            this.ResolveBlocksOrItems();
        }
    }
}
