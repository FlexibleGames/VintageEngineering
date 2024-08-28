using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VintageEngineering.Transport.API
{
    /// <summary>
    /// Pipe Use for pipe blocks is set using the LastCodePart of the pipe block itself. <br/>
    /// Make sure your pipes code ends with one of these values if using VE classes.
    /// </summary>
    public enum EnumPipeUse
    {
        item,
        fluid,
        energy,
        gas,
        heat,
        signal,
        casting,
        universal
    }

    /// <summary>
    /// Specifies how material is moved around via the extract nodes to input nodes.
    /// </summary>
    public enum EnumPipeDistribution
    {
        /// <summary>
        /// Moves to nearest available inventory first until it is full.
        /// </summary>
        Nearest,
        /// <summary>
        /// Moves to farthest available inventory first until it is full.
        /// </summary>
        Farthest,
        /// <summary>
        /// Moves to input for one tick then to the next position, then next, etc. wrapping around when it reaches the end.
        /// </summary>
        RoundRobin,
        /// <summary>
        /// Just randomly input to a position, no guarantee every input will be used over any span of time.
        /// </summary>
        Random
    }
}
