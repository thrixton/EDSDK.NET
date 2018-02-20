using System;
using System.Collections.Generic;
using System.Text;

using static EDSDKLib.EDSDK;

namespace EDSDK.NET
{
    /// <summary>
    /// A container for camera related information
    /// </summary>
    public class Camera
    {
        /// <summary>
        /// Pointer to SDK camera object
        /// </summary>
        public IntPtr Ref { get; private set; }
        /// <summary>
        /// Information about this camera
        /// </summary>
        public EdsDeviceInfo Info { get; private set; }
        /// <summary>
        /// Handles errors that happen with the SDK
        /// </summary>
        public uint Error
        {
            get { return EDS_ERR_OK; }
            set { if (value != EDS_ERR_OK) throw new Exception("SDK Error: " + value); }
        }

        /// <summary>
        /// Creates a new instance of the Camera class
        /// </summary>
        /// <param name="Reference">Pointer to the SDK camera object</param>
        public Camera(IntPtr Reference)
        {
            if (Reference == IntPtr.Zero) throw new ArgumentNullException("Camera pointer is zero");
            this.Ref = Reference;
            EdsDeviceInfo dinfo;
            Error = EdsGetDeviceInfo(Reference, out dinfo);
            this.Info = dinfo;
        }
    }
}
