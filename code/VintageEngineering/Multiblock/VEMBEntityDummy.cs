using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Multiblock
{
    /// <summary>
    /// Dummy BlockEntity for VE Multiblock System
    /// </summary>
    public class VEMBEntityDummy : BlockEntity
    {
        private Vec3i _offset = new Vec3i();

        /// <summary>
        /// Offset of this block from the Core of the Multiblock
        /// </summary>
        public Vec3i Offset { get { return _offset; } }

        public void SetOffset(Vec3i offset) => _offset = offset;
        public void SetOffset(Vec4i offset)
        {
            _offset.X = offset.X;
            _offset.Y = offset.Y;
            _offset.Z = offset.Z;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetVec3i("offset", _offset);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            _offset = tree.GetVec3i("offset");
        }

    }
}
