using System;
using System.Collections.Generic;
using System.Text;

namespace EDSDK.NET
{
    public class SDKProperty
    {
        public SDKProperty(string name, uint value)
        {
            this.Name = name;
            this.Value = value;
        }
        public string Name { get; private set; }
        public uint Value { get; private set; }

        internal object ValueToString()
        {
            return "0x" + Value.ToString("X");
        }
    }
}
