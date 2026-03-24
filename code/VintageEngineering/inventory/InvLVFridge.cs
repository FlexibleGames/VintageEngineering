using System.Collections.Generic;
using VintageEngineering.blockentity;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

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

        private float InvLVFridge_OnAcquireTransitionSpeed(EnumTransitionType transType, ItemStack stack, float mulByConfig)
        {
            // why are there 5 places these values can appear/be set/be retrieved? 
            return GetTransitionSpeedMul(transType, stack) * mulByConfig;
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
            this.OnAcquireTransitionSpeed += InvLVFridge_OnAcquireTransitionSpeed;
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
                { EnumTransitionType.Ripen, rate }
            };
            PerishableFactorByFoodCategory = new Dictionary<EnumFoodCategory, float>
            {
                { EnumFoodCategory.Protein, rate },
                { EnumFoodCategory.Vegetable, rate },
                { EnumFoodCategory.Grain, rate },
                { EnumFoodCategory.Fruit, rate }
            };
        }

        /// <summary>
        /// The entire purpose of this inventory is to override this function...<br/>
        /// When powered, slows down Perish, Ripen, Cure, and Melt transition types. Values loaded from JSON Attributes.
        /// </summary>
        /// <param name="transType">Transition Type</param>
        /// <param name="stack">The Stack</param>
        /// <returns>Float 0 <=> 1</returns>
        public override float GetTransitionSpeedMul(EnumTransitionType transType, ItemStack stack)
        {
            
            if (!_fridgeBE.Block.Attributes.KeyExists("spoilrate"))
            {
                return GlobalConstants.PerishSpeedModifier;
            }
            float _poweredrate = _fridgeBE.Block.Attributes["spoilrate"]["powered"].AsFloat(0.25f);
            float _unpoweredrate = _fridgeBE.Block.Attributes["spoilrate"]["unpowered"].AsFloat(2.0f);

            if (transType == EnumTransitionType.Perish || 
                transType == EnumTransitionType.Ripen || 
                transType == EnumTransitionType.Cure ||
                transType == EnumTransitionType.Melt)
            {
                // "spoilrate": { "powered": 0.2, "unpowered": 0.9 }

                if (_fridgeBE.Electric.MachineState == EnumBEState.On)
                {
                    return _poweredrate;
                }
                else
                {
                    return _unpoweredrate;
                }
            }
            else
            {
                return GlobalConstants.PerishSpeedModifier;
            }
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
