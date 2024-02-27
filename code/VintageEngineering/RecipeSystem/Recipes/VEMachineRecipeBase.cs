using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace VintageEngineering.RecipeSystem.Recipes
{
    /// <summary>
    /// This Base class holds common elements for all machine recipes.
    /// </summary>
    public class VEMachineRecipeBase : IByteSerializable
    {
        /// <summary>
        /// How much power does it take to craft one iteration of this recipe?
        /// <br>Time (seconds) to craft is PowerPerCraft/Machine PPS</br>
        /// </summary>
        public long PowerPerCraft { get; set; } = 0L;

        /// <summary>
        /// Reads base class data from the stream, call base at the end of subclass calls.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="resolver"></param>
        public virtual void FromBytes(BinaryReader reader, IWorldAccessor resolver)
        {
            PowerPerCraft = reader.ReadInt64();
        }

        /// <summary>
        /// Writes base class data to the stream, call base at the end of subclass calls.
        /// </summary>
        /// <param name="writer"></param>
        public virtual void ToBytes(BinaryWriter writer)
        {
            writer.Write(PowerPerCraft);
        }
    }
}
