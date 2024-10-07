using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace VintageEngineering.RecipeSystem.Recipes
{

    /// <summary>
    /// A class to allow for variable recipe output based on the games World Rand functions.<br/>
    /// [Optional] Use "litres" instead of quantity to show this output should be considered a fluid.
    /// </summary>
    public class VERecipeVariableOutput : JsonItemStack
    {
        public override void FromBytes(BinaryReader reader, IClassRegistryAPI instancer)
        {
            base.FromBytes(reader, instancer);
            if (reader.ReadBoolean())
            {
                Variable = reader.ReadInt32();
            }            
            if (reader.ReadBoolean())
            {
                Litres = reader.ReadInt32();
            }
        }

        public override void ToBytes(BinaryWriter writer)
        {
            base.ToBytes(writer);
            writer.Write(Variable != null);
            if (Variable != null)
            {
                writer.Write(Variable.Value);
            }
            writer.Write(Litres != null);
            if (Litres != null)
            {
                writer.Write(Litres.Value);
            }
        }

        /// <summary>
        /// Make full copy of this ItemStack
        /// </summary>
        /// <returns></returns>
        public new VERecipeVariableOutput Clone()
        {
            VERecipeVariableOutput output = new VERecipeVariableOutput();
            output.Code = this.Code.Clone();
            ItemStack resolved = this.ResolvedItemstack;
            output.ResolvedItemstack = ((resolved != null) ? resolved.Clone() : null);
            output.StackSize = this.StackSize;
            output.Type = this.Type;
            if (this.Attributes != null)
            {
                output.Attributes = this.Attributes.Clone();
            }
            output.Variable = this.Variable;
            output.Litres = this.Litres;
            return output;
        }

        /// <summary>
        /// Returns a random stacksize value between (inclusive) StackSize-Variable and Stacksize+Variable<br/>
        /// CAN return 0 if Output Stacksize = Variable<br/>
        /// Scales if output is a fluid (Litres != null)<br/>
        /// Returns -1 if output stack has not been resolved. Call Resolve(..) on Output recipe entries!<br/>
        /// If Output is NOT variable, this returns the normal set stacksize.
        /// </summary>
        /// <param name="resolver">WorldAccessor</param>
        /// <param name="sourceForErrorLogging">String to use if printing errors in the log file.</param>
        /// <param name="printWarningOnError">True to print any errors in the log.</param>
        /// <returns>(Variable optional) Stacksize for output, -1 if output isn't resolved</returns>
        public int VariableResolve(IWorldAccessor resolver, string sourceForErrorLogging, bool printWarningOnError = true)
        {
            int newstack = -1;

            WaterTightContainableProps props = null;
            int perliter = 1;
            if (this.Litres != null && Litres.Value > 0)
            {
                // output is a fluid, scale output according to items per litre
                props = BlockLiquidContainerBase.GetContainableProps(this.ResolvedItemstack);
                perliter = props != null ? (int)props.ItemsPerLitre : 1;
                this.ResolvedItemstack.StackSize = (int)Litres.Value * perliter; // stacksize is now number of portions, not total litres
            }

            if (this.ResolvedItemstack != null && Variable != null && Variable.Value != 0)
            {
                newstack = resolver.Rand.Next(Math.Max(0, this.ResolvedItemstack.StackSize - Variable.Value*perliter), this.ResolvedItemstack.StackSize + Variable.Value*perliter + 1);

                if (newstack < 0) newstack = 0;
            }
            else if (this.ResolvedItemstack != null && Variable == null)
            {
                return this.ResolvedItemstack.StackSize;
            }
            else
            {
                if (this.ResolvedItemstack == null && printWarningOnError)
                {
                    resolver.Logger.Error($"VintEng: Cannot resolve variable {sourceForErrorLogging} Recipe Ouput, must resolve Output ItemStack first!");
                }
                newstack = -1;
            }

            return newstack;
        }

        /// <summary>
        /// Determines what +/- limit the base stacksize can be altered, can be null!<br/>
        /// <u>Careful:</u> If Stacksize = 2 and Variable = 2 then there is a chance that <u>nothing</u> could be crafted.
        /// </summary>
        public int? Variable;

        /// <summary>
        /// Optional, instead of quantity or stacksize, output should be considered a fluid.
        /// </summary>
        public float? Litres;
    }
}
