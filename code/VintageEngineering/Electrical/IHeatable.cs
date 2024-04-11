using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VintageEngineering.Electrical
{
    /// <summary>
    /// Simple interface to define a BlockEntity as needing outside heating in order to
    /// enable some recipes.<br/>
    /// Heating blocks or machines can look for this interface to provide heating to the machine.
    /// </summary>
    public interface IHeatable
    {
        /// <summary>
        /// Returns what temperature the machine requires.
        /// </summary>
        /// <returns>Temperature Desired</returns>
        float GetDesiredTemperature();

        /// <summary>
        /// Sets the temperature of this IHeatable Device
        /// </summary>
        /// <param name="temperature"></param>
        void SetTemperature(float temperature);

        /// <summary>
        /// Returns the temperature of this IHeatableDevice
        /// </summary>
        /// <returns>Temperature</returns>
        float GetTemperature();
    }
}
