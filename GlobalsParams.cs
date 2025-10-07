using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace WiseFlasher
{
    public class GlobalsParams
    {
        public uint code;                // 4 bytes
        public byte gprs;                // 1 byte
        public byte router;              // 1 byte
        public byte lone_coord;          // 1 byte
        public byte identifier;          // 1 byte
        public ushort mod_Config;       // 2 bytes
        public byte Channel;             // 1 byte
        public byte MeshNetworkRetries;  // 1 byte
        public ulong ChannelMask;        // 8 bytes
        public byte NetworkHops;         // 1 byte
        public byte NetworkDelaySlots;   // 1 byte
        public byte UnicastMacRetries;   // 1 byte
        public uint SleepTime;           // 4 bytes
        public uint WakeTime;            // 4 bytes
        public byte SleepOptions;        // 1 byte 
        public byte SleepMode;           // 1 byte
        public byte PowerLevel;          // 1 byte 
        public byte coordinator;         // 1 byte
        public byte SoloGW;              // 1 byte
        public byte PreambleID;          // 1 byte
        public byte SecurityEnable;      // 1 byte
    }
}
