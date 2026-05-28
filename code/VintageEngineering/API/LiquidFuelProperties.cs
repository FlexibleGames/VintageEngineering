using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Datastructures;

namespace VintageEngineering.API
{
    public class LiquidFuelProperties
    {
        /// <summary>
        /// Create a Deep Copy
        /// </summary>
        /// <returns></returns>
        public LiquidFuelProperties Clone()
        {
            LiquidFuelProperties cloned = new LiquidFuelProperties
            {
                EnergyPPS = this.EnergyPPS,
                Duration = this.Duration,
                BurnTemp = this.BurnTemp,
                Soot = this.Soot
            };
            return cloned;
        }

        /// <summary>
        /// Set values from a JsonObject, should be Attributes["liquidfuel"]
        /// </summary>
        /// <param name="tree"></param>
        public static LiquidFuelProperties FromJSON(JsonObject tree)
        {
            LiquidFuelProperties output = new LiquidFuelProperties();
            if (tree["pps"].Exists) output.EnergyPPS = tree["pps"].AsFloat();
            if (tree["duration"].Exists) output.Duration = tree["duration"].AsFloat();
            if (tree["burntemp"].Exists) output.BurnTemp = tree["burntemp"].AsInt();
            if (tree["soot"].Exists) output.Soot = tree["soot"].AsFloat();
            return output;
        }
        /// <summary>
        /// Energy value of a single portion of this fuel. A portion is 10mL.<br/>
        /// Value is in PPS (Power Per Second).
        /// </summary>
        public float EnergyPPS;
        /// <summary>
        /// Duration in seconds a single portion burns until it is fully consumed.<br/>
        /// Total power produced per portion = EnergyPPS * Duration
        /// </summary>
        public float Duration;
        /// <summary>
        /// Burn Temperature of this fuel. In °C
        /// </summary>
        public int BurnTemp;
        /// <summary>
        /// Pollution factor for the fuel. How much pollution does it produce when burned?<br/>
        /// Used in possible expansion/add-on making pollution a real consideration.
        /// </summary>
        public float Soot;
    }
}
