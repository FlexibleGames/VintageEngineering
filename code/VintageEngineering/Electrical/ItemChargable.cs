using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;

namespace VintageEngineering.Electrical
{
    /// <summary>
    /// Base Class to use when creating items that you want to be chargable.
    /// </summary>
    public class ItemChargable : Item, IChargeableItem
    {        
        public ulong MaxPower
        {
            get
            {
                return ((ulong)this.Attributes["maxpower"].AsDouble(2));
            }
        }

        public ulong CurrentPower
        {
            get { return ((ulong)this.Attributes["currentpower"].AsDouble(0)); } 
        }

        public void SetPower(ItemStack stack, ulong power)
        {
            stack.Attributes.SetLong("currentpower", (long)power);
        }

        public ulong MaxPPS
        {
            get
            {
                return ((ulong)this.Attributes["maxpps"].AsDouble(5));
            }
        }

        public bool CanExtractPower
        {
            get
            {
                return (this.Attributes["canextractpower"].AsBool(false));
            }
        }

        public bool CanReceivePower
        {
            get
            {
                return (this.Attributes["canreceivepower"].AsBool(false));
            }
        }

        public void CheatPower(ItemStack stack, bool drain = false)
        {
            if (drain)
            {
                SetPower(stack, 0);
            }
            else
            {
                SetPower(stack, MaxPower);
            }
        }

        public ulong ExtractPower(ItemStack stack, ulong powerWanted, float dt, bool simulate = false)
        {
            ulong currentpow = CurrentPower;
            if (currentpow == 0) return powerWanted; // we have no power to give

            if (simulate) return RatedPower(stack, dt, false);

            // what is the max power transfer of this machine for this DeltaTime update tick?
            ulong pps = (ulong)Math.Round((MaxPPS * 1.05) * dt); // rounding issues abound
            // pps at this point is the PPS from JSON multiplied by DeltaTime (fractional second timing).
            // NOT going to deal with fractinal amounts of power. So Rounding errors are expected.

            if (pps == 0) pps = ulong.MaxValue; // PPS of 0 means NO LIMIT ***This will break recipes***

            // PPS can't exceed the amount of power we have in this generator
            // i.e. we can't provide power we don't have
            pps = (pps > currentpow) ? currentpow : pps;

            if (pps >= powerWanted) // this will probably rarely fire.
            {
                // PPS meets or exceeds power wanted, this machine can cover all power needs.
                if (!simulate) 
                { 
                    currentpow -= powerWanted;
                    SetPower(stack, currentpow);
                }                
                return 0; // all power wanted was supplied
            }
            else
            {
                // powerWanted exceeds how much we can supply
                if (!simulate)
                {
                    currentpow -= pps; // simulation mode doesn't change machines power total.
                    SetPower(stack, currentpow);
                }
                return powerWanted - pps; // return powerWanted reduced by our PPS.
            }
        }

        public ulong RatedPower(ItemStack stack, float dt, bool isInsert = false)
        {
            ulong rate = ((ulong)Math.Round(MaxPPS * dt));
            if (isInsert)
            {
                if (!CanReceivePower) return 0;
                ulong emptycap = MaxPower - CurrentPower;
                return emptycap < rate ? emptycap : rate;
            }
            else
            {
                // extracting
                if (!CanExtractPower) return 0;
                if (CurrentPower == 0) return 0;
                if (CurrentPower < rate) return CurrentPower;
                return rate; // CurrentPower > rate ? rate : CurrentPower;
            }
        }

        public ulong ReceivePower(ItemStack stack, ulong powerOffered, float dt, bool simulate = false)
        {            
            // The == was changed to >= to ensure rebalancing machine power values don't break the system.
            if (CurrentPower >= MaxPower) return powerOffered; // we're full, bounce fast

            if (simulate) return RatedPower(stack, dt, true);

            // what is the max power transfer of this machine for this DeltaTime update tick?
            ulong pps = (ulong)Math.Round((MaxPPS * 1.05) * dt); // rounding issues abound

            if (pps == 0) pps = ulong.MaxValue; // PPS of 0 means NO LIMIT ***This would break recipes, aka InstaCraft ***
            else pps += 2;

            ulong capacityempty = MaxPower - CurrentPower;

            // simular to ExtractPower, we can't receive more power than we can store.
            pps = (pps > capacityempty) ? capacityempty : pps;
            // pps now holds the actual amount of power we can receive that won't exceed empty capacity

            if (pps >= powerOffered)  // if amount we can take exceeds amount offered
            {
                // meaning we can take it all.
                if (!simulate) 
                {
                    ulong newpower = CurrentPower + powerOffered; 
                    SetPower(stack, newpower);
                }
                return 0;
            }
            else
            {
                // far more common, powerOffered exceeds PPS
                if (!simulate)
                {
                    ulong newpower = CurrentPower + pps;
                    SetPower(stack, newpower);
                }
                return powerOffered - pps;
            }
        }
    }
}
