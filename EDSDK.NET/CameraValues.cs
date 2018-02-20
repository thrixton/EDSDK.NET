using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace EDSDK.NET
{
    /// <summary>
    /// Helper to convert between ID and string camera values
    /// </summary>
    public static class CameraValues
    {
        private static CultureInfo cInfo = new CultureInfo("en-US");

        /// <summary>
        /// Gets the Av string value from an Av ID
        /// </summary>
        /// <param name="v">The Av ID</param>
        /// <returns>the Av string</returns>
        public static string AV(uint v)
        {
            switch (v)
            {
                case 0x00:
                    return "Auto";
                case 0x08:
                    return "1";
                case 0x40:
                    return "11";
                case 0x0B:
                    return "1.1";
                case 0x43:
                    return "13 (1/3)";
                case 0x0C:
                    return "1.2";
                case 0x44:
                    return "13";
                case 0x0D:
                    return "1.2 (1/3)";
                case 0x45:
                    return "14";
                case 0x10:
                    return "1.4";
                case 0x48:
                    return "16";
                case 0x13:
                    return "1.6";
                case 0x4B:
                    return "18";
                case 0x14:
                    return "1.8";
                case 0x4C:
                    return "19";
                case 0x15:
                    return "1.8 (1/3)";
                case 0x4D:
                    return "20";
                case 0x18:
                    return "2";
                case 0x50:
                    return "22";
                case 0x1B:
                    return "2.2";
                case 0x53:
                    return "25";
                case 0x1C:
                    return "2.5";
                case 0x54:
                    return "27";
                case 0x1D:
                    return "2.5 (1/3)";
                case 0x55:
                    return "29";
                case 0x20:
                    return "2.8";
                case 0x58:
                    return "32";
                case 0x23:
                    return "3.2";
                case 0x5B:
                    return "36";
                case 0x24:
                    return "3.5";
                case 0x5C:
                    return "38";
                case 0x25:
                    return "3.5 (1/3)";
                case 0x5D:
                    return "40";
                case 0x28:
                    return "4";
                case 0x60:
                    return "45";
                case 0x2B:
                    return "4.5";
                case 0x63:
                    return "51";
                case 0x2C:
                    return "4.5 (1/3)";
                case 0x64:
                    return "54";
                case 0x2D:
                    return "5.0";
                case 0x65:
                    return "57";
                case 0x30:
                    return "5.6";
                case 0x68:
                    return "64";
                case 0x33:
                    return "6.3";
                case 0x6B:
                    return "72";
                case 0x34:
                    return "6.7";
                case 0x6C:
                    return "76";
                case 0x35:
                    return "7.1";
                case 0x6D:
                    return "80";
                case 0x38:
                    return " 8";
                case 0x70:
                    return "91";
                case 0x3B:
                    return "9";
                case 0x3C:
                    return "9.5";
                case 0x3D:
                    return "10";

                case 0xffffffff:
                default:
                    return "N/A";
            }
        }

        /// <summary>
        /// Gets the ISO string value from an ISO ID
        /// </summary>
        /// <param name="v">The ISO ID</param>
        /// <returns>the ISO string</returns>
        public static string ISO(uint v)
        {
            switch (v)
            {
                case 0x00000000:
                    return "Auto ISO";
                case 0x00000028:
                    return "ISO 6";
                case 0x00000030:
                    return "ISO 12";
                case 0x00000038:
                    return "ISO 25";
                case 0x00000040:
                    return "ISO 50";
                case 0x00000048:
                    return "ISO 100";
                case 0x0000004b:
                    return "ISO 125";
                case 0x0000004d:
                    return "ISO 160";
                case 0x00000050:
                    return "ISO 200";
                case 0x00000053:
                    return "ISO 250";
                case 0x00000055:
                    return "ISO 320";
                case 0x00000058:
                    return "ISO 400";
                case 0x0000005b:
                    return "ISO 500";
                case 0x0000005d:
                    return "ISO 640";
                case 0x00000060:
                    return "ISO 800";
                case 0x00000063:
                    return "ISO 1000";
                case 0x00000065:
                    return "ISO 1250";
                case 0x00000068:
                    return "ISO 1600";
                case 0x00000070:
                    return "ISO 3200";
                case 0x00000078:
                    return "ISO 6400";
                case 0x00000080:
                    return "ISO 12800";
                case 0x00000088:
                    return "ISO 25600";
                case 0x00000090:
                    return "ISO 51200";
                case 0x00000098:
                    return "ISO 102400";
                case 0xffffffff:
                default:
                    return "N/A";
            }
        }

        /// <summary>
        /// Gets the Tv string value from an Tv ID
        /// </summary>
        /// <param name="v">The Tv ID</param>
        /// <returns>the Tv string</returns>
        public static string TV(uint v)
        {
            switch (v)
            {
                case 0x00:
                    return "Auto";
                case 0x0C:
                    return "Bulb";
                case 0x5D:
                    return "1/25";
                case 0x10:
                    return "30\"";
                case 0x60:
                    return "1/30";
                case 0x13:
                    return "25\"";
                case 0x63:
                    return "1/40";
                case 0x14:
                    return "20\"";
                case 0x64:
                    return "1/45";
                case 0x15:
                    return "20\" (1/3)";
                case 0x65:
                    return "1/50";
                case 0x18:
                    return "15\"";
                case 0x68:
                    return "1/60";
                case 0x1B:
                    return "13\"";
                case 0x6B:
                    return "1/80";
                case 0x1C:
                    return "10\"";
                case 0x6C:
                    return "1/90";
                case 0x1D:
                    return "10\" (1/3)";
                case 0x6D:
                    return "1/100";
                case 0x20:
                    return "8\"";
                case 0x70:
                    return "1/125";
                case 0x23:
                    return "6\" (1/3)";
                case 0x73:
                    return "1/160";
                case 0x24:
                    return "6\"";
                case 0x74:
                    return "1/180";
                case 0x25:
                    return "5\"";
                case 0x75:
                    return "1/200";
                case 0x28:
                    return "4\"";
                case 0x78:
                    return "1/250";
                case 0x2B:
                    return "3\"2";
                case 0x7B:
                    return "1/320";
                case 0x2C:
                    return "3\"";
                case 0x7C:
                    return "1/350";
                case 0x2D:
                    return "2\"5";
                case 0x7D:
                    return "1/400";
                case 0x30:
                    return "2\"";
                case 0x80:
                    return "1/500";
                case 0x33:
                    return "1\"6";
                case 0x83:
                    return "1/640";
                case 0x34:
                    return "1\"5";
                case 0x84:
                    return "1/750";
                case 0x35:
                    return "1\"3";
                case 0x85:
                    return "1/800";
                case 0x38:
                    return "1\"";
                case 0x88:
                    return "1/1000";
                case 0x3B:
                    return "0\"8";
                case 0x8B:
                    return "1/1250";
                case 0x3C:
                    return "0\"7";
                case 0x8C:
                    return "1/1500";
                case 0x3D:
                    return "0\"6";
                case 0x8D:
                    return "1/1600";
                case 0x40:
                    return "0\"5";
                case 0x90:
                    return "1/2000";
                case 0x43:
                    return "0\"4";
                case 0x93:
                    return "1/2500";
                case 0x44:
                    return "0\"3";
                case 0x94:
                    return "1/3000";
                case 0x45:
                    return "0\"3 (1/3)";
                case 0x95:
                    return "1/3200";
                case 0x48:
                    return "1/4";
                case 0x98:
                    return "1/4000";
                case 0x4B:
                    return "1/5";
                case 0x9B:
                    return "1/5000";
                case 0x4C:
                    return "1/6";
                case 0x9C:
                    return "1/6000";
                case 0x4D:
                    return "1/6 (1/3)";
                case 0x9D:
                    return "1/6400";
                case 0x50:
                    return "1/8";
                case 0xA0:
                    return "1/8000";
                case 0x53:
                    return "1/10 (1/3)";
                case 0x54:
                    return "1/10";
                case 0x55:
                    return "1/13";
                case 0x58:
                    return "1/15";
                case 0x5B:
                    return "1/20 (1/3)";
                case 0x5C:
                    return "1/20";

                case 0xffffffff:
                default:
                    return "N/A";
            }
        }


        /// <summary>
        /// Gets the Av ID from an Av string value
        /// </summary>
        /// <param name="v">The Av string</param>
        /// <returns>the Av ID</returns>
        public static uint AV(string v)
        {
            switch (v)
            {
                case "Auto":
                    return 0x00;
                case "1":
                    return 0x08;
                case "11":
                    return 0x40;
                case "1.1":
                    return 0x0B;
                case "13 (1/3)":
                    return 0x43;
                case "1.2":
                    return 0x0C;
                case "13":
                    return 0x44;
                case "1.2 (1/3)":
                    return 0x0D;
                case "14":
                    return 0x45;
                case "1.4":
                    return 0x10;
                case "16":
                    return 0x48;
                case "1.6":
                    return 0x13;
                case "18":
                    return 0x4B;
                case "1.8":
                    return 0x14;
                case "19":
                    return 0x4C;
                case "1.8 (1/3)":
                    return 0x15;
                case "20":
                    return 0x4D;
                case "2":
                    return 0x18;
                case "22":
                    return 0x50;
                case "2.2":
                    return 0x1B;
                case "25":
                    return 0x53;
                case "2.5":
                    return 0x1C;
                case "27":
                    return 0x54;
                case "2.5 (1/3)":
                    return 0x1D;
                case "29":
                    return 0x55;
                case "2.8":
                    return 0x20;
                case "32":
                    return 0x58;
                case "3.2":
                    return 0x23;
                case "36":
                    return 0x5B;
                case "3.5":
                    return 0x24;
                case "38":
                    return 0x5C;
                case "3.5 (1/3)":
                    return 0x25;
                case "40":
                    return 0x5D;
                case "4":
                    return 0x28;
                case "45":
                    return 0x60;
                case "4.5":
                    return 0x2B;
                case "51":
                    return 0x63;
                case "4.5 (1/3)":
                    return 0x2C;
                case "54":
                    return 0x64;
                case "5.0":
                    return 0x2D;
                case "57":
                    return 0x65;
                case "5.6":
                    return 0x30;
                case "64":
                    return 0x68;
                case "6.3":
                    return 0x33;
                case "72":
                    return 0x6B;
                case "6.7":
                    return 0x34;
                case "76":
                    return 0x6C;
                case "7.1":
                    return 0x35;
                case "80":
                    return 0x6D;
                case " 8":
                    return 0x38;
                case "91":
                    return 0x70;
                case "9":
                    return 0x3B;
                case "9.5":
                    return 0x3C;
                case "10":
                    return 0x3D;

                case "N/A":
                default:
                    return 0xffffffff;
            }
        }

        /// <summary>
        /// Gets the ISO ID from an ISO string value
        /// </summary>
        /// <param name="v">The ISO string</param>
        /// <returns>the ISO ID</returns>
        public static uint ISO(string v)
        {
            switch (v)
            {
                case "Auto ISO":
                    return 0x00000000;
                case "ISO 6":
                    return 0x00000028;
                case "ISO 12":
                    return 0x00000030;
                case "ISO 25":
                    return 0x00000038;
                case "ISO 50":
                    return 0x00000040;
                case "ISO 100":
                    return 0x00000048;
                case "ISO 125":
                    return 0x0000004b;
                case "ISO 160":
                    return 0x0000004d;
                case "ISO 200":
                    return 0x00000050;
                case "ISO 250":
                    return 0x00000053;
                case "ISO 320":
                    return 0x00000055;
                case "ISO 400":
                    return 0x00000058;
                case "ISO 500":
                    return 0x0000005b;
                case "ISO 640":
                    return 0x0000005d;
                case "ISO 800":
                    return 0x00000060;
                case "ISO 1000":
                    return 0x00000063;
                case "ISO 1250":
                    return 0x00000065;
                case "ISO 1600":
                    return 0x00000068;
                case "ISO 3200":
                    return 0x00000070;
                case "ISO 6400":
                    return 0x00000078;
                case "ISO 12800":
                    return 0x00000080;
                case "ISO 25600":
                    return 0x00000088;
                case "ISO 51200":
                    return 0x00000090;
                case "ISO 102400":
                    return 0x00000098;

                case "N/A":
                default:
                    return 0xffffffff;
            }
        }

        /// <summary>
        /// Gets the Tv ID from an Tv string value
        /// </summary>
        /// <param name="v">The Tv string</param>
        /// <returns>the Tv ID</returns>
        public static uint TV(string v)
        {
            switch (v)
            {
                case "Auto":
                    return 0x00;
                case "Bulb":
                    return 0x0C;
                case "1/25":
                    return 0x5D;
                case "30\"":
                    return 0x10;
                case "1/30":
                    return 0x60;
                case "25\"":
                    return 0x13;
                case "1/40":
                    return 0x63;
                case "20\"":
                    return 0x14;
                case "1/45":
                    return 0x64;
                case "20\" (1/3)":
                    return 0x15;
                case "1/50":
                    return 0x65;
                case "15\"":
                    return 0x18;
                case "1/60":
                    return 0x68;
                case "13\"":
                    return 0x1B;
                case "1/80":
                    return 0x6B;
                case "10\"":
                    return 0x1C;
                case "1/90":
                    return 0x6C;
                case "10\" (1/3)":
                    return 0x1D;
                case "1/100":
                    return 0x6D;
                case "8\"":
                    return 0x20;
                case "1/125":
                    return 0x70;
                case "6\" (1/3)":
                    return 0x23;
                case "1/160":
                    return 0x73;
                case "6\"":
                    return 0x24;
                case "1/180":
                    return 0x74;
                case "5\"":
                    return 0x25;
                case "1/200":
                    return 0x75;
                case "4\"":
                    return 0x28;
                case "1/250":
                    return 0x78;
                case "3\"2":
                    return 0x2B;
                case "1/320":
                    return 0x7B;
                case "3\"":
                    return 0x2C;
                case "1/350":
                    return 0x7C;
                case "2\"5":
                    return 0x2D;
                case "1/400":
                    return 0x7D;
                case "2\"":
                    return 0x30;
                case "1/500":
                    return 0x80;
                case "1\"6":
                    return 0x33;
                case "1/640":
                    return 0x83;
                case "1\"5":
                    return 0x34;
                case "1/750":
                    return 0x84;
                case "1\"3":
                    return 0x35;
                case "1/800":
                    return 0x85;
                case "1\"":
                    return 0x38;
                case "1/1000":
                    return 0x88;
                case "0\"8":
                    return 0x3B;
                case "1/1250":
                    return 0x8B;
                case "0\"7":
                    return 0x3C;
                case "1/1500":
                    return 0x8C;
                case "0\"6":
                    return 0x3D;
                case "1/1600":
                    return 0x8D;
                case "0\"5":
                    return 0x40;
                case "1/2000":
                    return 0x90;
                case "0\"4":
                    return 0x43;
                case "1/2500":
                    return 0x93;
                case "0\"3":
                    return 0x44;
                case "1/3000":
                    return 0x94;
                case "0\"3 (1/3)":
                    return 0x45;
                case "1/3200":
                    return 0x95;
                case "1/4":
                    return 0x48;
                case "1/4000":
                    return 0x98;
                case "1/5":
                    return 0x4B;
                case "1/5000":
                    return 0x9B;
                case "1/6":
                    return 0x4C;
                case "1/6000":
                    return 0x9C;
                case "1/6 (1/3)":
                    return 0x4D;
                case "1/6400":
                    return 0x9D;
                case "1/8":
                    return 0x50;
                case "1/8000":
                    return 0xA0;
                case "1/10 (1/3)":
                    return 0x53;
                case "1/10":
                    return 0x54;
                case "1/13":
                    return 0x55;
                case "1/15":
                    return 0x58;
                case "1/20 (1/3)":
                    return 0x5B;
                case "1/20":
                    return 0x5C;

                case "N/A":
                default:
                    return 0xffffffff;
            }
        }
    }
}
