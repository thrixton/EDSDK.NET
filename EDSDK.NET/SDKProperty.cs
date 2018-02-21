using System;
using System.Collections.Generic;
using System.Text;

namespace EDSDK.NET
{
    public class SDKProperty
    {
        public SDKProperty(string name, uint value, bool matched = true)
        {
            this.Name = name;
            this.Value = value;
            this.Matched = matched;
        }
        public string Name { get; private set; }
        public uint Value { get; private set; }

        /// <summary>
        /// Whether the property matched one of the stored properties
        /// </summary>
        public bool Matched { get; private set; }

        internal object ValueToString()
        {
            return "0x" + Value.ToString("X");
        }
    }
}
