using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Threading;
using System.Globalization;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using static EDSDKLib.EDSDK;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Reflection;
using Microsoft.Extensions.Logging.Summarized;
using System.Diagnostics;

namespace EDSDK.NET
{
    /// <summary>
    /// Handles the Canon SDK
    /// </summary>
    public class SDKHandler : IDisposable
    {
        #region Events

        public event EventHandler<SdkErrorEventArgs> SdkError;

        #endregion Events

        #region Variables

        ILogger logger;

        /// <summary>
        /// The used camera
        /// </summary>
        public Camera MainCamera { get; private set; }
        /// <summary>
        /// States if a session with the MainCamera is opened
        /// </summary>
        public bool CameraSessionOpen { get; private set; }
        /// <summary>
        /// States if the live view is on or not
        /// </summary>
        public bool IsLiveViewOn { get; private set; }
        /// <summary>
        /// States if camera is recording or not
        /// </summary>
        public bool IsFilming { get; private set; }
        /// <summary>
        /// Directory to where photos will be saved
        /// </summary>
        public string ImageSaveDirectory { get; set; }

        /// <summary>
        /// File name of next photo
        /// </summary>
        public string ImageSaveFilename
        {
            get => _imageSaveFilename;
            set
            {
                var t = LogInfoAsync("Setting ImageSaveFilename. ImageSaveFilename: {ImageSaveFilename}", _imageSaveFilename);
                _imageSaveFilename = value;
            }
        }

        /// <summary>
        /// The focus and zoom border rectangle for live view (set after first use of live view)
        /// </summary>
        public EdsRect Evf_ZoomRect { get; private set; }
        /// <summary>
        /// The focus and zoom border position of the live view (set after first use of live view)
        /// </summary>
        public EdsPoint Evf_ZoomPosition { get; private set; }
        /// <summary>
        /// The cropping position of the enlarged live view image (set after first use of live view)
        /// </summary>
        public EdsPoint Evf_ImagePosition { get; private set; }
        /// <summary>
        /// The live view coordinate system (set after first use of live view)
        /// </summary>
        public EdsSize Evf_CoordinateSystem { get; private set; }
        /// <summary>
        /// States if the Evf_CoordinateSystem is already set
        /// </summary>
        public bool IsCoordSystemSet = false;
        /// <summary>
        /// Handles errors that happen with the SDK
        /// </summary>
        public uint Error
        {
            get { return EDS_ERR_OK; }
            set
            {
                if (value != EDS_ERR_OK)
                {
                    var errorProperty = SDKErrorToProperty(value);
                    LogError("SDK Error. Name: {0}, Value: {1}", errorProperty.Name, errorProperty.ValueToString());


                    switch (value)
                    {
                        case EDS_ERR_COMM_DISCONNECTED:
                        case EDS_ERR_DEVICE_INVALID:
                        case EDS_ERR_DEVICE_NOT_FOUND:
                            string name = FindProperty(SDKErrors, value).Name;
                            OnSdkError(new SdkErrorEventArgs() { Error = name, ErrorLevel = LogLevel.Critical });
                            break;
                    }

                }
            }
        }


        /// <summary>
        /// States if a finished video should be downloaded from the camera
        /// </summary>
        private bool DownloadVideo;
        /// <summary>
        /// For video recording, SaveTo has to be set to Camera. This is to store the previous setting until after the filming.
        /// </summary>
        private uint PrevSaveTo;
        EdsCapacity PrevCapacity;
        private uint PrevEVFSetting;

        public void SetUintSetting(string propertyName, string propertyValue)
        {
            uint value;
            bool error = false;
            if (!string.IsNullOrEmpty(propertyValue))
            {
                propertyValue = propertyValue.Replace("0x", "");
            }

            if (!uint.TryParse(propertyValue, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out value))
            {
                LogError("Could not convert value {0} to uint", propertyValue);
                error = true;
            }

            if (!string.IsNullOrEmpty(propertyName) && propertyName.StartsWith("kEds"))
            {
                propertyName = propertyName.Substring(4);
            }


            var prop = GetSDKProperty(propertyName);
            if (!prop.Matched)
            {
                LogWarning("Could not find property named {0}");
            }

            if (!error)
            {
                SetSetting(prop.Value, value);
            }


        }

        /// <summary>
        /// The thread on which the live view images will get downloaded continuously
        /// </summary>
        private Thread LVThread;
        /// <summary>
        /// If true, the live view will be shut off completely. If false, live view will go back to the camera.
        /// </summary>
        private bool LVoff;

        public void DumpAllProperties()
        {
            var t = LogInfoAsync("=========Dumping properties=========");

            foreach (var prop in SDKProperties)
            {
                uint value = GetSetting(prop.Value);

                t = LogInfoAsync("Property: {SDKProperty}, Value: {SDKPropertyValue}", prop.Name, "0x" + value.ToString("X"));
            }
        }

        #endregion

        #region Events

        #region SDK Events

        public event EdsCameraAddedHandler SDKCameraAddedEvent;
        public event EdsObjectEventHandler SDKObjectEvent;
        public event EdsProgressCallback SDKProgressCallbackEvent;
        public event EdsPropertyEventHandler SDKPropertyEvent;
        public event EdsStateEventHandler SDKStateEvent;

        #endregion

        #region Custom Events

        public delegate void CameraAddedHandler();
        public delegate void ProgressHandler(int Progress);
        public delegate void StreamUpdate(Stream img);
        public delegate void BitmapUpdate(Bitmap bmp);

        /// <summary>
        /// Fires if a camera is added
        /// </summary>
        public event CameraAddedHandler CameraAdded;
        /// <summary>
        /// Fires if any process reports progress
        /// </summary>
        public event ProgressHandler ProgressChanged;
        /// <summary>
        /// Fires if the live view image has been updated
        /// </summary>
        public event StreamUpdate LiveViewUpdated;
        /// <summary>
        /// If the camera is disconnected or shuts down, this event is fired
        /// </summary>
        public event EventHandler CameraHasShutdown;
        /// <summary>
        /// If an image is downloaded, this event fires with the downloaded image.
        /// </summary>
        public event BitmapUpdate ImageDownloaded;

        #endregion

        #endregion

        #region Basic SDK and Session handling

        public SDKProperty SDKObjectEventToProperty(uint objectEvent)
        {
            return FindProperty(SDKObjectEvents, objectEvent);
        }

        SDKProperty[] _sDKObjectEvents;
        public SDKProperty[] SDKObjectEvents
        {
            get
            {
                return _sDKObjectEvents;
            }
        }

        public SDKProperty SDKErrorToProperty(uint error)
        {
            return FindProperty(SDKErrors, error);
        }

        SDKProperty[] _sDKErrors;
        public SDKProperty[] SDKErrors
        {
            get
            {
                return _sDKErrors;
            }
        }

        private SDKProperty FindProperty(SDKProperty[] properties, string property)
        {
            var search = properties.FirstOrDefault(p => p.Name == property);
            if (search == null)
            {
                search = new SDKProperty(property, 0, false);
            }
            return search;
        }


        private SDKProperty FindProperty(SDKProperty[] properties, uint property)
        {
            var search = properties.FirstOrDefault(p => p.Value == property);
            if (search == null)
            {
                search = new SDKProperty("UNKNOWN", property, false);
            }
            return search;
        }

        public void SetSaveToHost()
        {
            SetSetting(EDSDKLib.EDSDK.PropID_SaveTo, (uint)EDSDKLib.EDSDK.EdsSaveTo.Host);
        }

        public SDKProperty GetStateEvent(uint stateEvent)
        {
            return FindProperty(SDKStateEvents, stateEvent);
        }

        public SDKProperty GetSDKProperty(string property)
        {
            return FindProperty(SDKProperties, property);
        }


        public SDKProperty GetSDKProperty(uint property)
        {
            return FindProperty(SDKProperties, property);
        }

        SDKProperty[] _sDKProperties;
        public SDKProperty[] SDKProperties
        {
            get
            {
                return _sDKProperties;
            }
        }

        SDKProperty[] _sDKStateEvents;
        public SDKProperty[] SDKStateEvents
        {
            get
            {
                return _sDKStateEvents;
            }
        }

        public object Value { get; private set; }
        public bool KeepAlive { get; set; }

        SummarizedLogger summaryLogger;

        /// <summary>
        /// Initializes the SDK and adds events
        /// </summary>
        public SDKHandler(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger<SDKHandler>();

            STAThread.SetLogAction(loggerFactory.CreateLogger(nameof(STAThread)));

            STAThread.FatalError += STAThread_FatalError;

            summaryLogger = new SummarizedLogger(logger, LogLevel.Debug, nameof(SDKHandler))
                .SetFrequency(TimeSpan.FromSeconds(10));

            PopulateSDKConstantStructures();

            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                LogWarning("SDKHandler created on a non-STA thread");
            }

            //initialize SDK

            try
            {
                Error = EdsInitializeSDK();

            }
            catch (Exception x)
            {
                logger.LogError(x, "Error initialising SDK");
                throw new Exception("Error initialising SDK", x);
                //TODO: Move to Initialise pattern instead of constructor
            }
            STAThread.Init();

            //subscribe to camera added event (the C# event and the SDK event)
            SDKCameraAddedEvent += new EdsCameraAddedHandler(SDKHandler_CameraAddedEvent);
            AddCameraHandler(() => { return EdsSetCameraAddedHandler(SDKCameraAddedEvent, IntPtr.Zero); }, nameof(EdsSetCameraAddedHandler));

            //subscribe to the camera events (for the C# events)
            SDKStateEvent += new EdsStateEventHandler(Camera_SDKStateEvent);
            SDKPropertyEvent += new EdsPropertyEventHandler(Camera_SDKPropertyEvent);
            SDKProgressCallbackEvent += new EdsProgressCallback(Camera_SDKProgressCallbackEvent);
            SDKObjectEvent += new EdsObjectEventHandler(Camera_SDKObjectEvent);

        }

        private void STAThread_FatalError(object sender, EventArgs e)
        {
            var args = new SdkErrorEventArgs() { Error = "Execution thread error", ErrorLevel = LogLevel.Critical };
            OnSdkError(args);
        }

        /// <summary>
        /// Call this once to initialize event listeners and thread management
        /// NOTE: Should be called from an STA thread
        /// </summary>
        private void PopulateSDKConstantStructures()
        {
            FieldInfo[] fields = typeof(EDSDKLib.EDSDK).GetFields(BindingFlags.Public | BindingFlags.Static);

            _sDKStateEvents = FilterFields(fields, "StateEvent_");
            _sDKObjectEvents = FilterFields(fields, "ObjectEvent_");
            _sDKErrors = FilterFields(fields, "EDS_ERR_");
            _sDKProperties = FilterFields(fields, "kEdsPropID_", "PropID_");
        }


        static SDKProperty[] FilterFields(FieldInfo[] fields, string prefix, string prefix2 = null)
        {
            var filteredFields = from f in fields
                                 where (f.Name.StartsWith(prefix) || (prefix2 != null && f.Name.StartsWith(prefix2))) && f.IsLiteral
                                 select new SDKProperty(f.Name, (uint)f.GetValue(null));
            return filteredFields.ToArray();
        }

        /// <summary>
        /// Get a list of all connected cameras
        /// </summary>
        /// <returns>The camera list</returns>
        public List<Camera> GetCameraList()
        {
            IntPtr camlist;
            //get list of cameras
            Error = EdsGetCameraList(out camlist);

            //get each camera from camlist
            int c;
            //get amount of connected cameras
            Error = EdsGetChildCount(camlist, out c);
            List<Camera> camList = new List<Camera>();
            for (int i = 0; i < c; i++)
            {
                IntPtr cptr;
                //get pointer to camera at index i
                Error = EdsGetChildAtIndex(camlist, i, out cptr);
                camList.Add(new Camera(cptr));
            }

            var t = LogInfoAsync("Found {CameraCount} cameras", camList.Count);

            return camList;
        }

        private async Task LogInfoAsync(string message, params object[] args)
        {
            await Log(LogLevel.Information, message, args);
        }

        /// <summary>
        /// Opens a session with given camera
        /// </summary>
        /// <param name="newCamera">The camera which will be used</param>
        public void OpenSession(Camera newCamera)
        {
            logger?.LogDebug("Opening session");

            if (CameraSessionOpen) CloseSession();
            if (newCamera != null)
            {
                MainCamera = newCamera;
                //open a session
                SendSDKCommand(delegate { Error = EdsOpenSession(MainCamera.Ref); }, sdkAction: nameof(EdsOpenSession));
                //subscribe to the camera events (for the SDK)
                AddCameraHandler(() => { return EdsSetCameraStateEventHandler(MainCamera.Ref, StateEvent_All, SDKStateEvent, MainCamera.Ref); }, nameof(EdsSetCameraStateEventHandler));
                AddCameraHandler(() => { return EdsSetObjectEventHandler(MainCamera.Ref, ObjectEvent_All, SDKObjectEvent, MainCamera.Ref); }, nameof(EdsSetObjectEventHandler));
                AddCameraHandler(() => { return EdsSetPropertyEventHandler(MainCamera.Ref, PropertyEvent_All, SDKPropertyEvent, MainCamera.Ref); }, nameof(EdsSetPropertyEventHandler));
                CameraSessionOpen = true;

                var t = LogInfoAsync("Connected to Camera: {CameraName}", newCamera.Info.szDeviceDescription);

            }
        }

        void AddCameraHandler(Func<uint> action, string handlerName)
        {
            var t = LogInfoAsync("Adding handler: {SDKHandlerName}", handlerName);

            Error = action();
        }


        /// <summary>
        /// Closes the session with the current camera
        /// </summary>
        public void CloseSession()
        {
            logger?.LogDebug("Closing session");
            if (CameraSessionOpen)
            {
                //if live view is still on, stop it and wait till the thread has stopped
                if (IsLiveViewOn)
                {
                    StopLiveView();
                    LVThread.Join(1000);
                }

                //Remove the event handler
                EdsSetCameraStateEventHandler(MainCamera.Ref, StateEvent_All, null, MainCamera.Ref);
                EdsSetObjectEventHandler(MainCamera.Ref, ObjectEvent_All, null, MainCamera.Ref);
                EdsSetPropertyEventHandler(MainCamera.Ref, PropertyEvent_All, null, MainCamera.Ref);

                //close session and release camera
                SendSDKCommand(delegate { Error = EdsCloseSession(MainCamera.Ref); }, sdkAction: nameof(EdsCloseSession));
                uint c = EdsRelease(MainCamera.Ref);
                CameraSessionOpen = false;
            }
        }

        /// <summary>
        /// Closes open session and terminates the SDK
        /// </summary>
        public void Dispose()
        {
            //close session
            CloseSession();
            //terminate SDK
            Error = EdsTerminateSDK();
            //stop command execution thread
            STAThread.Shutdown();
        }

        #endregion

        #region Eventhandling

        /// <summary>
        /// A new camera was plugged into the computer
        /// </summary>
        /// <param name="inContext">The pointer to the added camera</param>
        /// <returns>An EDSDK errorcode</returns>
        private uint SDKHandler_CameraAddedEvent(IntPtr inContext)
        {
            //Handle new camera here
            OnCameraAdded();
            return EDS_ERR_OK;
        }

        protected void OnCameraAdded()
        {
            if (CameraAdded != null)
            {
                CameraAdded();
            }
        }


        /// <summary>
        /// An Objectevent fired
        /// </summary>
        /// <param name="inEvent">The ObjectEvent id</param>
        /// <param name="inRef">Pointer to the object</param>
        /// <param name="inContext"></param>
        /// <returns>An EDSDK errorcode</returns>
        private uint Camera_SDKObjectEvent(uint inEvent, IntPtr inRef, IntPtr inContext)
        {
            var eventProperty = SDKObjectEventToProperty(inEvent);
            //LogInfo("SDK Object Event. Name: {SDKEventName}, Value: {SDKEventHex}", eventProperty.Name, eventProperty.ValueToString());
            var t = summaryLogger.LogEventAsync($"Camera_SDKObjectEvent. EventName: {eventProperty.Name}");
            //handle object event here
            switch (inEvent)
            {
                case ObjectEvent_All:
                    break;
                case ObjectEvent_DirItemCancelTransferDT:
                    break;
                case ObjectEvent_DirItemContentChanged:
                    break;
                case ObjectEvent_DirItemCreated:
                    if (DownloadVideo)
                    {
                        DownloadImage(inRef, ImageSaveDirectory, ImageSaveFilename, isVideo: true);
                        DownloadVideo = false;
                    }
                    break;
                case ObjectEvent_DirItemInfoChanged:
                    break;
                case ObjectEvent_DirItemRemoved:
                    break;
                case ObjectEvent_DirItemRequestTransfer:
                    DownloadImage(inRef, ImageSaveDirectory, ImageSaveFilename);
                    break;
                case ObjectEvent_DirItemRequestTransferDT:
                    break;
                case ObjectEvent_FolderUpdateItems:
                    break;
                case ObjectEvent_VolumeAdded:
                    break;
                case ObjectEvent_VolumeInfoChanged:
                    break;
                case ObjectEvent_VolumeRemoved:
                    break;
                case ObjectEvent_VolumeUpdateItems:
                    break;
            }

            return EDS_ERR_OK;
        }

        /// <summary>
        /// A progress was made
        /// </summary>
        /// <param name="inPercent">Percent of progress</param>
        /// <param name="inContext">...</param>
        /// <param name="outCancel">Set true to cancel event</param>
        /// <returns>An EDSDK errorcode</returns>
        private uint Camera_SDKProgressCallbackEvent(uint inPercent, IntPtr inContext, ref bool outCancel)
        {
            //Handle progress here
            OnProgressChanged((int)inPercent);
            return EDS_ERR_OK;
        }

        protected void OnProgressChanged(int percent)
        {
            if (ProgressChanged != null)
            {
                ProgressChanged(percent);
            }
        }

        /// <summary>
        /// A property changed
        /// </summary>
        /// <param name="inEvent">The PropertyEvent ID</param>
        /// <param name="inPropertyID">The Property ID</param>
        /// <param name="inParameter">Event Parameter</param>
        /// <param name="inContext">...</param>
        /// <returns>An EDSDK errorcode</returns>
        private uint Camera_SDKPropertyEvent(uint inEvent, uint inPropertyID, uint inParameter, IntPtr inContext)
        {
            //Handle property event here
            switch (inEvent)
            {
                case PropertyEvent_All:
                    break;
                case PropertyEvent_PropertyChanged:
                    LogPropertyValue(inPropertyID, GetSetting(inPropertyID));
                    break;

                case PropertyEvent_PropertyDescChanged:
                    break;
            }

            switch (inPropertyID)
            {
                case PropID_AEBracket:
                    break;
                case PropID_AEMode:
                    break;
                case PropID_AEModeSelect:
                    break;
                case PropID_AFMode:
                    break;
                case PropID_Artist:
                    break;
                case PropID_AtCapture_Flag:
                    break;
                case PropID_Av:
                    break;
                case PropID_AvailableShots:
                    break;
                case PropID_BatteryLevel:
                    break;
                case PropID_BatteryQuality:
                    break;
                case PropID_BodyIDEx:
                    break;
                case PropID_Bracket:
                    break;
                case PropID_CFn:
                    break;
                case PropID_ClickWBPoint:
                    break;
                case PropID_ColorMatrix:
                    break;
                case PropID_ColorSaturation:
                    break;
                case PropID_ColorSpace:
                    break;
                case PropID_ColorTemperature:
                    break;
                case PropID_ColorTone:
                    break;
                case PropID_Contrast:
                    break;
                case PropID_Copyright:
                    break;
                case PropID_DateTime:
                    break;
                case PropID_DepthOfField:
                    break;
                case PropID_DigitalExposure:
                    break;
                case PropID_DriveMode:
                    break;
                case PropID_EFCompensation:
                    break;
                case PropID_Evf_AFMode:
                    break;
                case PropID_Evf_ColorTemperature:
                    break;
                case PropID_Evf_DepthOfFieldPreview:
                    break;
                case PropID_Evf_FocusAid:
                    break;
                case PropID_Evf_Histogram:
                    break;
                case PropID_Evf_HistogramStatus:
                    break;
                case PropID_Evf_ImagePosition:
                    break;
                case PropID_Evf_Mode:
                    break;
                case PropID_Evf_OutputDevice:
                    if (IsLiveViewOn == true) DownloadEvf();
                    break;
                case PropID_Evf_WhiteBalance:
                    break;
                case PropID_Evf_Zoom:
                    break;
                case PropID_Evf_ZoomPosition:
                    break;
                case PropID_ExposureCompensation:
                    break;
                case PropID_FEBracket:
                    break;
                case PropID_FilterEffect:
                    break;
                case PropID_FirmwareVersion:
                    break;
                case PropID_FlashCompensation:
                    break;
                case PropID_FlashMode:
                    break;
                case PropID_FlashOn:
                    break;
                case PropID_FocalLength:
                    break;
                case PropID_FocusInfo:
                    break;
                case PropID_GPSAltitude:
                    break;
                case PropID_GPSAltitudeRef:
                    break;
                case PropID_GPSDateStamp:
                    break;
                case PropID_GPSLatitude:
                    break;
                case PropID_GPSLatitudeRef:
                    break;
                case PropID_GPSLongitude:
                    break;
                case PropID_GPSLongitudeRef:
                    break;
                case PropID_GPSMapDatum:
                    break;
                case PropID_GPSSatellites:
                    break;
                case PropID_GPSStatus:
                    break;
                case PropID_GPSTimeStamp:
                    break;
                case PropID_GPSVersionID:
                    break;
                case PropID_HDDirectoryStructure:
                    break;
                case PropID_ICCProfile:
                    break;
                case PropID_ImageQuality:
                    break;
                case PropID_ISOBracket:
                    break;
                case PropID_ISOSpeed:
                    break;
                case PropID_JpegQuality:
                    break;
                case PropID_LensName:
                    break;
                case PropID_LensStatus:
                    break;
                case PropID_Linear:
                    break;
                case PropID_MakerName:
                    break;
                case PropID_MeteringMode:
                    break;
                case PropID_NoiseReduction:
                    break;
                case PropID_Orientation:
                    break;
                case PropID_OwnerName:
                    break;
                case PropID_ParameterSet:
                    break;
                case PropID_PhotoEffect:
                    break;
                case PropID_PictureStyle:
                    break;
                case PropID_PictureStyleCaption:
                    break;
                case PropID_PictureStyleDesc:
                    break;
                case PropID_ProductName:
                    break;
                case PropID_Record:
                    break;
                case PropID_RedEye:
                    break;
                case PropID_SaveTo:
                    break;
                case PropID_Sharpness:
                    break;
                case PropID_ToneCurve:
                    break;
                case PropID_ToningEffect:
                    break;
                case PropID_Tv:
                    break;
                case PropID_Unknown:
                    break;
                case PropID_WBCoeffs:
                    break;
                case PropID_WhiteBalance:
                    break;
                case PropID_WhiteBalanceBracket:
                    break;
                case PropID_WhiteBalanceShift:
                    break;
            }
            return EDS_ERR_OK;
        }

        public void LogPropertyValue(string propertyName, uint propertyValue)
        {
            var task = LogInfoAsync($"Camera_SDKPropertyEvent. Property {propertyName} changed to {"0x" + propertyValue.ToString("X")}");
        }


        public void LogPropertyValue(uint propertyID, uint propertyValue)
        {
            var prop = GetSDKProperty(propertyID);
            LogPropertyValue(prop.Name, propertyValue);
            //do nothing with task, continue
        }

        /// <summary>
        /// The camera state changed
        /// </summary>
        /// <param name="inEvent">The StateEvent ID</param>
        /// <param name="inParameter">Parameter from this event</param>
        /// <param name="inContext">...</param>
        /// <returns>An EDSDK errorcode</returns>
        private uint Camera_SDKStateEvent(uint inEvent, uint inParameter, IntPtr inContext)
        {

            var stateProperty = GetStateEvent(inEvent);

            var t = LogInfoAsync("SDK State Event. Name: {SDKStateEventName}, Hex {SDKStateEventHex}", stateProperty.Name, stateProperty.ValueToString());

            //Handle state event here
            switch (inEvent)
            {
                case StateEvent_All:
                    break;
                case StateEvent_AfResult:
                    break;
                case StateEvent_BulbExposureTime:
                    break;
                case StateEvent_CaptureError:
                    break;
                case StateEvent_InternalError:
                    break;
                case StateEvent_JobStatusChanged:
                    break;
                case StateEvent_Shutdown:
                    CameraSessionOpen = false;
                    if (IsLiveViewOn)
                    {
                        StopLiveView();
                        // Not supported in .NET Core. Transition to cancellation token LVThread.Abort();
                    }
                    OnCameraHasShutdown();
                    break;
                case StateEvent_ShutDownTimerUpdate:
                    break;
                case StateEvent_WillSoonShutDown:
                    if (KeepAlive)
                    {
                        SendSDKCommand(() =>
                        {
                            logger.LogDebug("Extending camera shutdown timer");
                            EdsSendCommand(MainCamera.Ref, CameraCommand_ExtendShutDownTimer, 0);
                        }, sdkAction: nameof(CameraCommand_ExtendShutDownTimer));
                    }
                    break;
            }
            return EDS_ERR_OK;
        }

        protected void OnCameraHasShutdown()
        {
            CameraHasShutdown?.Invoke(this, new EventArgs());
        }

        protected void OnSdkError(SdkErrorEventArgs e)
        {
            SdkError?.Invoke(this, e);
        }

        #endregion Eventhandling

        #region Camera commands

        #region Download data

        /// <summary>
        /// Downloads an image to given directory
        /// </summary>
        /// <param name="ObjectPointer">Pointer to the object. Get it from the SDKObjectEvent.</param>
        /// <param name="directory">Path to where the image will be saved to</param>
        public void DownloadImage(IntPtr ObjectPointer, string directory, string fileName, bool isVideo = false)
        {
            try
            {
                EdsDirectoryItemInfo dirInfo;
                IntPtr streamRef;
                //get information about object
                Error = EdsGetDirectoryItemInfo(ObjectPointer, out dirInfo);
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = dirInfo.szFileName;
                }
                else
                {
                    FileInfo targetInfo = new FileInfo(fileName);
                    FileInfo cameraInfo = new FileInfo(dirInfo.szFileName);

                    if (!string.Equals(targetInfo.Extension, cameraInfo.Extension, StringComparison.CurrentCultureIgnoreCase))
                    {
                        fileName = targetInfo.Name.Substring(0, targetInfo.Name.Length - targetInfo.Extension.Length) + cameraInfo.Extension;
                    }
                }

                LogInfoAsync("Downloading data. Filename: {Filename}", fileName);

                string targetImage = Path.Combine(directory, fileName);
                if (File.Exists(targetImage))
                {
                    throw new NotImplementedException("Renaming files not permitted");
                }

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }


                var t = LogInfoAsync("Downloading data {Filename} to {SaveDirectory}", fileName, directory);

                SendSDKCommand(delegate
                {

                    var stopWatch = Stopwatch.StartNew();

                    //create filestream to data
                    Error = EdsCreateFileStream(targetImage, EdsFileCreateDisposition.CreateAlways, EdsAccess.ReadWrite, out streamRef);
                    //download file
                    STAThread.TryLockAndExecute(STAThread.ExecLock, nameof(STAThread.ExecLock), TimeSpan.FromSeconds(30), () =>
                        {
                            DownloadData(ObjectPointer, streamRef);
                        });
                    //release stream
                    Error = EdsRelease(streamRef);

                    stopWatch.Stop();

                    var downloadFile = new FileInfo(targetImage);
                    var mB = downloadFile.Length / 1000.0 / 1000;

                    t = LogInfoAsync("Downloaded data. Filename: {Filename}, FileLengthMB: {FileLengthMB}, DurationSeconds: {DurationSeconds}, MBPerSecond: {MBPerSecond}", targetImage, mB.ToString("0.0"), stopWatch.Elapsed.TotalSeconds.ToString("0.0"), (mB / stopWatch.Elapsed.TotalSeconds).ToString("0.0"));

                    if (isVideo)
                    {
                        videoDownloadDone?.TrySetResult(targetImage);
                    }
                    else
                    {
                        takePhotoCompletionSource?.TrySetResult(new FileInfo(fileName));
                    }

                }, true);
            }
            catch (Exception x)
            {
                logger.LogError(x, "Error downloading data");
                takePhotoCompletionSource.TrySetException(x);
                videoDownloadDone?.TrySetException(x);
            }

        }
        

        /// <summary>
        /// Downloads a jpg image from the camera into a Bitmap. Fires the ImageDownloaded event when done.
        /// </summary>
        /// <param name="ObjectPointer">Pointer to the object. Get it from the SDKObjectEvent.</param>
        public void DownloadImage(IntPtr ObjectPointer)
        {
            //get information about image
            EdsDirectoryItemInfo dirInfo = new EdsDirectoryItemInfo();
            Error = EdsGetDirectoryItemInfo(ObjectPointer, out dirInfo);

            //check the extension. Raw data cannot be read by the bitmap class
            string ext = Path.GetExtension(dirInfo.szFileName).ToLower();

            var t = LogInfoAsync("Downloading image {ImageFileName}", dirInfo.szFileName);


            if (ext == ".jpg" || ext == ".jpeg")
            {
                SendSDKCommand(delegate
                {
                    Bitmap bmp = null;
                    IntPtr streamRef, jpgPointer = IntPtr.Zero;
                    ulong length = 0;

                    //create memory stream
                    Error = EdsCreateMemoryStream(dirInfo.Size, out streamRef);

                    //download data to the stream
                    lock (STAThread.ExecLock) { DownloadData(ObjectPointer, streamRef); }
                    Error = EdsGetPointer(streamRef, out jpgPointer);
                    Error = EdsGetLength(streamRef, out length);

                    unsafe
                    {
                        //create a System.IO.Stream from the pointer
                        using (UnmanagedMemoryStream ums = new UnmanagedMemoryStream((byte*)jpgPointer.ToPointer(), (long)length, (long)length, FileAccess.Read))
                        {
                            //create bitmap from stream (it's a normal jpeg image)
                            bmp = new Bitmap(ums);
                        }
                    }

                    //release data
                    Error = EdsRelease(streamRef);

                    //Fire the event with the image
                    OnImageDownloaded(bmp);
                }, true);
            }
            else
            {
                //if it's a RAW image, cancel the download and release the image
                SendSDKCommand(delegate { Error = EdsDownloadCancel(ObjectPointer); });
                Error = EdsRelease(ObjectPointer);
            }
        }

        protected void OnImageDownloaded(Bitmap bitmap)
        {
            if (ImageDownloaded != null)
            {
                ImageDownloaded(bitmap);
            }
        }

        /// <summary>
        /// Gets the thumbnail of an image (can be raw or jpg)
        /// </summary>
        /// <param name="filepath">The filename of the image</param>
        /// <returns>The thumbnail of the image</returns>
        public Bitmap GetFileThumb(string filepath)
        {
            IntPtr stream;
            //create a filestream to given file
            Error = EdsCreateFileStream(filepath, EdsFileCreateDisposition.OpenExisting, EdsAccess.Read, out stream);
            return GetImage(stream, EdsImageSource.Thumbnail);
        }

        /// <summary>
        /// Downloads data from the camera
        /// </summary>
        /// <param name="ObjectPointer">Pointer to the object</param>
        /// <param name="stream">Pointer to the stream created in advance</param>
        private void DownloadData(IntPtr ObjectPointer, IntPtr stream)
        {
            //get information about the object
            EdsDirectoryItemInfo dirInfo;
            Error = EdsGetDirectoryItemInfo(ObjectPointer, out dirInfo);

            try
            {
                //set progress event
                Error = EdsSetProgressCallback(stream, SDKProgressCallbackEvent, EdsProgressOption.Periodically, ObjectPointer);
                //download the data
                Error = EdsDownload(ObjectPointer, dirInfo.Size, stream);
            }
            finally
            {
                //set the download as complete
                Error = EdsDownloadComplete(ObjectPointer);
                //release object
                Error = EdsRelease(ObjectPointer);
            }
        }

        /// <summary>
        /// Creates a Bitmap out of a stream
        /// </summary>
        /// <param name="img_stream">Image stream</param>
        /// <param name="imageSource">Type of image</param>
        /// <returns>The bitmap from the stream</returns>
        private Bitmap GetImage(IntPtr img_stream, EdsImageSource imageSource)
        {
            IntPtr stream = IntPtr.Zero;
            IntPtr img_ref = IntPtr.Zero;
            IntPtr streamPointer = IntPtr.Zero;
            EdsImageInfo imageInfo;

            try
            {
                //create reference and get image info
                Error = EdsCreateImageRef(img_stream, out img_ref);
                Error = EdsGetImageInfo(img_ref, imageSource, out imageInfo);

                EdsSize outputSize = new EdsSize();
                outputSize.width = imageInfo.EffectiveRect.width;
                outputSize.height = imageInfo.EffectiveRect.height;
                //calculate amount of data
                int datalength = outputSize.height * outputSize.width * 3;
                //create buffer that stores the image
                byte[] buffer = new byte[datalength];
                //create a stream to the buffer

                IntPtr ptr = new IntPtr();
                Marshal.StructureToPtr<byte[]>(buffer, ptr, false);


                Error = EdsCreateMemoryStreamFromPointer(ptr, (uint)datalength, out stream);
                //load image into the buffer
                Error = EdsGetImage(img_ref, imageSource, EdsTargetImageType.RGB, imageInfo.EffectiveRect, outputSize, stream);

                //create output bitmap
                Bitmap bmp = new Bitmap(outputSize.width, outputSize.height, PixelFormat.Format24bppRgb);

                //assign values to bitmap and make BGR from RGB (System.Drawing (i.e. GDI+) uses BGR)
                unsafe
                {
                    BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, bmp.PixelFormat);

                    byte* outPix = (byte*)data.Scan0;
                    fixed (byte* inPix = buffer)
                    {
                        for (int i = 0; i < datalength; i += 3)
                        {
                            outPix[i] = inPix[i + 2];//Set B value with R value
                            outPix[i + 1] = inPix[i + 1];//Set G value
                            outPix[i + 2] = inPix[i];//Set R value with B value
                        }
                    }
                    bmp.UnlockBits(data);
                }

                return bmp;
            }
            finally
            {
                //Release all data
                if (img_stream != IntPtr.Zero) EdsRelease(img_stream);
                if (img_ref != IntPtr.Zero) EdsRelease(img_ref);
                if (stream != IntPtr.Zero) EdsRelease(stream);
            }
        }

        #endregion

        #region Get Settings

        /// <summary>
        /// Gets the list of possible values for the current camera to set.
        /// Only the PropertyIDs "AEModeSelect", "ISO", "Av", "Tv", "MeteringMode" and "ExposureCompensation" are allowed.
        /// </summary>
        /// <param name="PropID">The property ID</param>
        /// <returns>A list of available values for the given property ID</returns>
        public List<int> GetSettingsList(uint PropID)
        {
            if (MainCamera.Ref != IntPtr.Zero)
            {
                //a list of settings can only be retrieved for following properties
                if (PropID == PropID_AEModeSelect || PropID == PropID_ISOSpeed || PropID == PropID_Av
                    || PropID == PropID_Tv || PropID == PropID_MeteringMode || PropID == PropID_ExposureCompensation)
                {
                    //get the list of possible values
                    EdsPropertyDesc des = new EdsPropertyDesc();
                    Error = EdsGetPropertyDesc(MainCamera.Ref, PropID, out des);
                    return des.PropDesc.Take(des.NumElements).ToList();
                }
                else throw new ArgumentException("Method cannot be used with this Property ID");
            }
            else { throw new ArgumentNullException("Camera or camera reference is null/zero"); }
        }

        /// <summary>
        /// Gets the current setting of given property ID as an uint
        /// </summary>
        /// <param name="PropID">The property ID</param>
        /// <returns>The current setting of the camera</returns>
        public uint GetSetting(uint PropID)
        {
            if (MainCamera.Ref != IntPtr.Zero)
            {
                uint property = 0;
                Error = EdsGetPropertyData(MainCamera.Ref, PropID, 0, out property);
                return property;
            }
            else { throw new ArgumentNullException("Camera or camera reference is null/zero"); }
        }

        /// <summary>
        /// Gets the current setting of given property ID as a string
        /// </summary>
        /// <param name="PropID">The property ID</param>
        /// <returns>The current setting of the camera</returns>
        public string GetStringSetting(uint PropID)
        {
            if (MainCamera.Ref != IntPtr.Zero)
            {
                string data = String.Empty;
                EdsGetPropertyData(MainCamera.Ref, PropID, 0, out data);
                return data;
            }
            else { throw new ArgumentNullException("Camera or camera reference is null/zero"); }
        }

        /// <summary>
        /// Gets the current setting of given property ID as a struct
        /// </summary>
        /// <param name="PropID">The property ID</param>
        /// <typeparam name="T">One of the EDSDK structs</typeparam>
        /// <returns>The current setting of the camera</returns>
        public T GetStructSetting<T>(uint PropID) where T : struct
        {
            if (MainCamera.Ref != IntPtr.Zero)
            {
                //get type and size of struct
                Type structureType = typeof(T);
                int bufferSize = Marshal.SizeOf(structureType);

                //allocate memory
                IntPtr ptr = Marshal.AllocHGlobal(bufferSize);
                //retrieve value
                Error = EdsGetPropertyData(MainCamera.Ref, PropID, 0, bufferSize, ptr);

                try
                {
                    //convert pointer to managed structure
                    T data = (T)Marshal.PtrToStructure(ptr, structureType);
                    return data;
                }
                finally
                {
                    if (ptr != IntPtr.Zero)
                    {
                        //free the allocated memory
                        Marshal.FreeHGlobal(ptr);
                        ptr = IntPtr.Zero;
                    }
                }
            }
            else { throw new ArgumentNullException("Camera or camera reference is null/zero"); }
        }

        #endregion

        #region Set Settings



        /// <summary>
        /// Sets an uint value for the given property ID
        /// </summary>
        /// <param name="propertyId">The property ID</param>
        /// <param name="value">The value which will be set</param>
        public void SetSetting(uint propertyId, uint value)
        {
            LogSetProperty(propertyId, "0x" + value.ToString("X"));
            if (MainCamera.Ref != IntPtr.Zero)
            {
                SendSDKCommand(delegate
                {
                    var cThread = Thread.CurrentThread;
                    var t = LogInfoAsync("Executing SDK command. ThreadName: {ThreadName}, ApartmentState: {ApartmentState}", cThread.Name, cThread.GetApartmentState());
                    int propsize;
                    EdsDataType proptype;
                    //get size of property
                    Error = EdsGetPropertySize(MainCamera.Ref, propertyId, 0, out proptype, out propsize);
                    //set given property
                    Error = EdsSetPropertyData(MainCamera.Ref, propertyId, 0, propsize, value);
                }, sdkAction: nameof(EdsSetPropertyData));
            }
            else { throw new ArgumentNullException("Camera or camera reference is null/zero"); }
        }

        /// <summary>
        /// Sets a DateTime value for the given property ID
        /// </summary>
        /// <param name="PropID">The property ID</param>
        /// <param name="Value">The value which will be set</param>
        public void SetDateTimeSetting(uint propertyId, DateTime value)
        {
            EDSDKLib.EDSDK.EdsTime dateTime = new EDSDKLib.EDSDK.EdsTime()
            {
                Year = value.Year,
                Month = value.Month,
                Day = value.Day,
                Hour = value.Hour,
                Minute = value.Minute,
                Second = value.Second
            };
            SetStructSetting(propertyId, dateTime);
        }

        void LogSetProperty(uint propertyId, string value)
        {
            var prop = GetSDKProperty(propertyId);
            var t = LogInfoAsync("Setting property. Name: {SDKPropertyName}, Id: {SDKPropertyHex}, Value: {SDKPropertyValue}", prop.Name, prop.Value, value);
        }

        /// <summary>
        /// Sets a string value for the given property ID
        /// </summary>
        /// <param name="propertyId">The property ID</param>
        /// <param name="value">The value which will be set</param>
        public void SetStringSetting(uint propertyId, string value)
        {
            LogSetProperty(propertyId, value);
            //TODO: Refactor to remove duplicate code in Set_XXX_Setting methods
            if (MainCamera.Ref != IntPtr.Zero)
            {
                if (value == null) throw new ArgumentNullException("String must not be null");

                //convert string to byte array
                byte[] propertyValueBytes = System.Text.Encoding.ASCII.GetBytes(value + '\0');
                int propertySize = propertyValueBytes.Length;

                //check size of string
                if (propertySize > 32) throw new ArgumentOutOfRangeException("Value must be smaller than 32 bytes");

                //set value
                SendSDKCommand(delegate { Error = EdsSetPropertyData(MainCamera.Ref, propertyId, 0, 32, propertyValueBytes); }, sdkAction: nameof(EdsSetPropertyData));
            }
            else { throw new ArgumentNullException("Camera or camera reference is null/zero"); }
        }

        /// <summary>
        /// Sets a struct value for the given property ID
        /// </summary>
        /// <param name="propertyId">The property ID</param>
        /// <param name="value">The value which will be set</param>
        public void SetStructSetting<T>(uint propertyId, T value) where T : struct
        {
            LogSetProperty(propertyId, value.ToString());
            if (MainCamera.Ref != IntPtr.Zero)
            {
                SendSDKCommand(delegate { Error = EdsSetPropertyData(MainCamera.Ref, propertyId, 0, Marshal.SizeOf(typeof(T)), value); }, sdkAction: nameof(EdsSetPropertyData));
            }
            else { throw new ArgumentNullException("Camera or camera reference is null/zero"); }
        }

        #endregion

        #region Live view

        /// <summary>
        /// Starts the live view
        /// </summary>
        public void StartLiveView()
        {
            if (!IsLiveViewOn)
            {
                var t = LogInfoAsync("Starting Liveview");

                if (LiveViewUpdated != null)
                {
                    t = LogInfoAsync("{LiveViewUpdatedEventListeners} LiveViewUpdated listeners found", LiveViewUpdated.GetInvocationList().Length);
                }
                else
                {
                    t = LogInfoAsync("{LiveViewUpdatedEventListeners} LiveViewUpdated listeners found", 0);

                }


                LogPropertyValue(nameof(PropID_Evf_OutputDevice), GetSetting(PropID_Evf_OutputDevice));



                SetSetting(PropID_Evf_OutputDevice, EvfOutputDevice_PC);
                IsLiveViewOn = true;

                LogPropertyValue(nameof(PropID_Evf_OutputDevice), GetSetting(PropID_Evf_OutputDevice));

            }
        }

        /// <summary>
        /// Stops the live view
        /// </summary>
        public void StopLiveView()
        {
            if (IsLiveViewOn)
            {
                var t = LogInfoAsync("Stopping liveview");
                this.LVoff = true;
                IsLiveViewOn = false;

                //Wait 5 seconds for evf thread to finish, otherwise manually stop
                if (!cancelLiveViewWait.WaitOne(TimeSpan.FromSeconds(5)))
                {
                    KillLiveView();
                }
            }

        }

        AutoResetEvent cancelLiveViewWait = new AutoResetEvent(false);

        /// <summary>
        /// Downloads the live view image
        /// </summary>
        private void DownloadEvf()
        {
            LVThread = STAThread.Create(delegate
            {
                try
                {
                    IntPtr jpgPointer;
                    IntPtr stream = IntPtr.Zero;
                    IntPtr EvfImageRef = IntPtr.Zero;
                    UnmanagedMemoryStream ums;

                    uint err;
                    ulong length;
                    //create stream
                    Error = EdsCreateMemoryStream(0, out stream);

                    //run live view
                    while (IsLiveViewOn)
                    {
                        lock (STAThread.ExecLock)
                        {
                            //download current live view image
                            err = EdsCreateEvfImageRef(stream, out EvfImageRef);
                            if (err == EDS_ERR_OK) err = EdsDownloadEvfImage(MainCamera.Ref, EvfImageRef);
                            if (err == EDS_ERR_OBJECT_NOTREADY) { Thread.Sleep(4); continue; }
                            else Error = err;
                        }

                        //get pointer
                        Error = EdsGetPointer(stream, out jpgPointer);
                        Error = EdsGetLength(stream, out length);

                        //get some live view image metadata
                        if (!IsCoordSystemSet) { Evf_CoordinateSystem = GetEvfCoord(EvfImageRef); IsCoordSystemSet = true; }
                        Evf_ZoomRect = GetEvfZoomRect(EvfImageRef);
                        Evf_ZoomPosition = GetEvfPoints(PropID_Evf_ZoomPosition, EvfImageRef);
                        Evf_ImagePosition = GetEvfPoints(PropID_Evf_ImagePosition, EvfImageRef);

                        //release current evf image
                        if (EvfImageRef != IntPtr.Zero) { Error = EdsRelease(EvfImageRef); }

                        //create stream to image
                        unsafe { ums = new UnmanagedMemoryStream((byte*)jpgPointer.ToPointer(), (long)length, (long)length, FileAccess.Read); }

                        //fire the LiveViewUpdated event with the live view image stream
                        OnLiveViewUpdated(ums);
                        ums.Close();
                    }

                    //release and finish
                    if (stream != IntPtr.Zero)
                    {
                        Error = EdsRelease(stream);
                    }
                    KillLiveView();
                    cancelLiveViewWait.Set();
                }
                catch
                {
                    IsLiveViewOn = false;
                }
            });
            LVThread.Start();
        }

        private void KillLiveView()
        {
            var t = LogInfoAsync("Killing LiveView");
            //stop the live view
            SetSetting(PropID_Evf_OutputDevice, LVoff ? 0 : EvfOutputDevice_TFT);
        }


        /// <summary>
        /// Fires the LiveViewUpdated event
        /// </summary>
        /// <param name="stream"></param>
        protected void OnLiveViewUpdated(UnmanagedMemoryStream stream)
        {
            var t = summaryLogger.LogEventAsync(nameof(OnLiveViewUpdated));
            if (LiveViewUpdated != null)
            {
                LiveViewUpdated(stream);
            }
        }


        /// <summary>
        /// Get the live view ZoomRect value
        /// </summary>
        /// <param name="imgRef">The live view reference</param>
        /// <returns>ZoomRect value</returns>
        private EdsRect GetEvfZoomRect(IntPtr imgRef)
        {
            int size = Marshal.SizeOf(typeof(EdsRect));
            IntPtr ptr = Marshal.AllocHGlobal(size);

            uint err = EdsGetPropertyData(imgRef, PropID_Evf_ZoomPosition, 0, size, ptr);
            EdsRect rect = (EdsRect)Marshal.PtrToStructure(ptr, typeof(EdsRect));
            Marshal.FreeHGlobal(ptr);
            if (err == EDS_ERR_OK) return rect;
            else return new EdsRect();
        }

        /// <summary>
        /// Get the live view coordinate system
        /// </summary>
        /// <param name="imgRef">The live view reference</param>
        /// <returns>the live view coordinate system</returns>
        private EdsSize GetEvfCoord(IntPtr imgRef)
        {
            int size = Marshal.SizeOf(typeof(EdsSize));
            IntPtr ptr = Marshal.AllocHGlobal(size);
            uint err = EdsGetPropertyData(imgRef, PropID_Evf_CoordinateSystem, 0, size, ptr);
            EdsSize coord = (EdsSize)Marshal.PtrToStructure(ptr, typeof(EdsSize));
            Marshal.FreeHGlobal(ptr);
            if (err == EDS_ERR_OK) return coord;
            else return new EdsSize();
        }

        /// <summary>
        /// Get a live view EdsPoint value
        /// </summary>
        /// <param name="imgRef">The live view reference</param>
        /// <returns>a live view EdsPoint value</returns>
        private EdsPoint GetEvfPoints(uint PropID, IntPtr imgRef)
        {
            int size = Marshal.SizeOf(typeof(EdsPoint));
            IntPtr ptr = Marshal.AllocHGlobal(size);
            uint err = EdsGetPropertyData(imgRef, PropID, 0, size, ptr);
            EdsPoint data = (EdsPoint)Marshal.PtrToStructure(ptr, typeof(EdsPoint));
            Marshal.FreeHGlobal(ptr);
            if (err == EDS_ERR_OK) return data;
            else return new EdsPoint();
        }

        #endregion

        #region Filming

        /// <summary>
        /// Starts recording a video and downloads it when finished
        /// </summary>
        /// <param name="FilePath">Directory to where the final video will be saved to</param>
        public void StartFilming(string FilePath)
        {
            if (!IsFilming)
            {
                StartFilming();
                this.DownloadVideo = true;
                ImageSaveDirectory = FilePath;
            }
        }

        public void SetTFTEvf()
        {
            SetSetting(PropID_Evf_OutputDevice, EvfOutputDevice_TFT);
        }

        /// <summary>
        /// Starts recording a video
        /// NOTE: Will throw an ArgumentException if the camera is not in the correct mode
        /// </summary>
        public void StartFilming()
        {
            if (!IsFilming)
            {
                //Snapshot setting for restoration after filming completes
                PrevEVFSetting = GetSetting(PropID_Evf_OutputDevice);


                //Set EVF output to TFT to enable film, otherwise
                //NOTE: Not working to set it and start video in the same action, disabling
                //SetSetting(PropID_Evf_OutputDevice, EvfOutputDevice_TFT);
                //SetTFTEvf();

                //LogPropertyValue(nameof(PropID_Record), GetSetting(PropID_Record));

                SetSetting(PropID_Evf_OutputDevice, 3);

                //Check if the camera is ready to film
                if (GetSetting(PropID_Record) != (uint)PropID_Record_Status.Movie_shooting_ready)
                {
                    //DOES NOT WORK, readonly setting?
                    //DOES NOT THROW AN ERROR
                    //SetSetting(PropID_Record, (uint)EdsDriveMode.Video);
                    //SetSetting(PropID_Record, (uint)PropID_Record_Status.Movie_shooting_ready);


                    LogPropertyValue(PropID_Record, GetSetting(PropID_Record));
                    var tx = Log(LogLevel.Critical, "Camera physical switch must be in movie record mode. Leave in this mode permanently!");
                    //throw new ArgumentException("Camera in invalid mode", nameof(PropID_Record));
                }
                IsFilming = true;


                //to restore the current setting after recording
                PrevSaveTo = GetSetting(PropID_SaveTo);


                //when recording videos, it has to be saved on the camera internal memory
                SetSetting(PropID_SaveTo, (uint)EdsSaveTo.Camera);
                this.DownloadVideo = false;
                //start the video recording

                var t = LogInfoAsync("Start filming");

                SendSDKCommand(delegate { Error = EdsSetPropertyData(MainCamera.Ref, PropID_Record, 0, sizeof(PropID_Record_Status), (uint)PropID_Record_Status.Begin_movie_shooting); });
            }
        }

        TaskCompletionSource<string> videoDownloadDone;

        /// <summary>
        /// Stops recording a video
        /// </summary>
        public TaskCompletionSource<string> StopFilming(out long unixTimeMs)
        {
            if (IsFilming)
            {
                videoDownloadDone = new TaskCompletionSource<string>();
                long stopMs = 0;
                SendSDKCommand(delegate
                {
                    //Shut off live view (it will hang otherwise)
                    //StopLiveView(false);
                    //stop video recording
                    Error = EdsSetPropertyData(MainCamera.Ref, PropID_Record, 0, sizeof(PropID_Record_Status), (uint)PropID_Record_Status.End_movie_shooting);
                    stopMs = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    //set back to previous state
                });
                SetSetting(PropID_SaveTo, PrevSaveTo);
                SetSetting(PropID_Evf_OutputDevice, PrevEVFSetting);
                if (PrevCapacity.NumberOfFreeClusters != 0)
                {
                    SetCapacity(PrevCapacity);
                }
                IsFilming = false;
                unixTimeMs = stopMs;
            }
            else
            {
                videoDownloadDone.SetResult(null);
                unixTimeMs = 0;
            }
            return videoDownloadDone;
        }

        #endregion

        #region Taking photos

        /// <summary>
        /// Press the shutter button
        /// </summary>
        /// <param name="state">State of the shutter button</param>
        public void PressShutterButton(EdsShutterButton state)
        {
            //start thread to not block everything
            SendSDKCommand(delegate
            {
                //send command to camera
                lock (STAThread.ExecLock) { Error = EdsSendCommand(MainCamera.Ref, CameraCommand_PressShutterButton, (int)state); };
            }, true);
        }

        private TaskCompletionSource<FileInfo> takePhotoCompletionSource;
        private string _imageSaveFilename;

        /// <summary>
        /// Takes a photo and returns the file info
        /// </summary>
        /// <returns></returns>
        public async Task<FileInfo> TakePhotoAsync(FileInfo saveFile)
        {
            return await Task.Run<FileInfo>(async () =>
            {
                if(IsFilming || IsLiveViewOn)
                {
                    logger.LogWarning("Ignoring attempt to take photo whilst filming or in live-view mode. Filming: {Filming}, LiveView: {LiveView}", IsFilming, IsLiveViewOn);
                    return null;
                }

                takePhotoCompletionSource = new TaskCompletionSource<FileInfo>();
                SetSaveToLocation(saveFile.Directory);
                this.ImageSaveFilename = saveFile.Name;

                this.TakePhoto();

                await takePhotoCompletionSource.Task;
                if (takePhotoCompletionSource.Task.Status == TaskStatus.RanToCompletion)
                {
                    return takePhotoCompletionSource.Task.Result;
                }
                else
                {
                    LogError("Error taking photo, check previous messages");
                    return null;
                }
            });

        }

        private async Task Log(LogLevel level, string message, params object[] args)
        {
            await Task.Run(() =>
            {

                if (logger != null)
                {
                    switch (level)
                    {
                        case LogLevel.Trace:
                            logger.LogTrace(message, args);
                            break;

                        case LogLevel.Debug:
                            logger.LogDebug(message, args);
                            break;

                        case LogLevel.Information:
                            logger.LogInformation(message, args);
                            break;

                        case LogLevel.Warning:
                            logger.LogWarning(message, args);
                            break;

                        case LogLevel.Critical:
                            logger.LogCritical(message, args);
                            break;

                        case LogLevel.None:
                            // breakpoint only
                            break;

                        case LogLevel.Error:
                            logger.LogError(message, args);
                            break;

                        default:
                            logger.LogError("Unknown level: {0}{1}Message: {2}", level, Environment.NewLine, string.Format(message, args));
                            break;
                    }
                }

                if (level >= LogLevel.Error)
                {
                    // throw new Exception(string.Format(message, args));
                }
            });
        }

        private void HandleException(Exception ex, string message, params object[] args)
        {
            if (logger != null)
            {
                logger.LogError(ex, message, args);
            }
        }

        private void LogWarning(string message, params object[] args)
        {
            var t = Log(LogLevel.Warning, message, args);
        }

        private void LogError(string message, params object[] args)
        {
            var t = Log(LogLevel.Error, message, args);
        }


        public void SetSaveToLocation(DirectoryInfo directory)
        {
            SetSaveToHost();
            this.ImageSaveDirectory = directory.FullName;
        }

        /// <summary>
        /// Takes a photo with the current camera settings
        /// </summary>
        public void TakePhoto()
        {
            //start thread to not block everything
            SendSDKCommand(delegate
            {
                //send command to camera
                lock (STAThread.ExecLock) { Error = EdsSendCommand(MainCamera.Ref, CameraCommand_TakePicture, 0); };
            }, true);
        }

        /// <summary>
        /// Takes a photo in bulb mode with the current camera settings
        /// </summary>
        /// <param name="bulbTime">The time in milliseconds for how long the shutter will be open</param>
        public void TakePhoto(uint bulbTime)
        {
            //bulbtime has to be at least a second
            if (bulbTime < 1000) { throw new ArgumentException("Bulbtime has to be bigger than 1000ms"); }

            //start thread to not block everything
            SendSDKCommand(delegate
            {
                //open the shutter
                lock (STAThread.ExecLock) { Error = EdsSendCommand(MainCamera.Ref, CameraCommand_BulbStart, 0); }
                //wait for the specified time
                Thread.Sleep((int)bulbTime);
                //close shutter
                lock (STAThread.ExecLock) { Error = EdsSendCommand(MainCamera.Ref, CameraCommand_BulbEnd, 0); }
            }, true);
        }


        public void FormatAllVolumes()
        {
            RunForEachVolume((childReference, volumeInfo) =>
            {
                logger.LogInformation("Formatting volume. Volume: {Volume}", volumeInfo.szVolumeLabel);
                SendSDKCommand(() =>
                {
                    Error = EdsFormatVolume(childReference);
                });
            });
        }

        public float GetMinVolumeSpacePercent()
        {
            var minPercent = 1f;
            RunForEachVolume((childReference, volumeInfo) =>
            {
                var freePc = volumeInfo.FreeSpaceInBytes / (float)volumeInfo.MaxCapacity;
                logger.LogDebug("Camera volume free space. volume: {volume}, freeSpaceBytes: {freeSpaceBytes}, maxCapacity: {maxCapacity}, freePercent: {freePercent}", volumeInfo.szVolumeLabel, volumeInfo.FreeSpaceInBytes, volumeInfo.MaxCapacity, freePc);
                if (freePc < minPercent)
                {
                    minPercent = freePc;
                }
            });
            return minPercent;
        }

        private void RunForEachVolume(Action<IntPtr, EdsVolumeInfo> action)
        {
            //get the number of volumes currently installed in the camera
            int VolumeCount;
            Error = EdsGetChildCount(GetCamera().Reference, out VolumeCount);

            for (int i = 0; i < VolumeCount; i++)
            {
                //get information about volume
                IntPtr childReference;
                Error = EdsGetChildAtIndex(MainCamera.Ref, i, out childReference);
                EdsVolumeInfo volumeInfo = new EdsVolumeInfo();
                SendSDKCommand(delegate { Error = EdsGetVolumeInfo(childReference, out volumeInfo); });

                if (volumeInfo.StorageType != (uint)EdsStorageType.Non && volumeInfo.Access == (uint)EdsAccess.ReadWrite)
                {
                    action(childReference, volumeInfo);
                }
                Error = EdsRelease(childReference);
            }
        }


        public void FormatVolume(CameraFileEntry volume)
        {
            throw new NotImplementedException();
            // NOTE: Need to marry up obj ref to camera entry then delete based on camera entry / ref
            logger.LogDebug("Formatting volume. Volume: {Volume}", volume.Name);
            SendSDKCommand(() =>
            {
                var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(volume.Volume));
                try
                {
                    Marshal.StructureToPtr(volume.Volume, ptr, false);
                    Error = EdsFormatVolume(ptr);

                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }, sdkAction: nameof(EdsFormatVolume)
            );
        }


        #endregion

        #region Other

        /// <summary>
        /// Sends a command to the camera safely
        /// </summary>
        private void SendSDKCommand(Action command, bool longTask = false, string sdkAction = null)
        {
            if (sdkAction != null)
            {
                var t = LogInfoAsync("Sending SDK command: {SDKCommand}", sdkAction);
            }


            try
            {
                if (longTask)
                {
                    STAThread.Create(command).Start();
                }
                else
                {
                    STAThread.ExecuteSafely(command);
                }

            }
            catch (Exception ex)
            {
                HandleException(ex, "Error sending command: {0}", sdkAction);
            }
        }

        /// <summary>
        /// Tells the camera that there is enough space on the HDD if SaveTo is set to Host
        /// This method does not use the actual free space!
        /// </summary>
        public void SetCapacity()
        {
            SetCapacity(0x1000, 0x7FFFFFFF);
        }

        /// <summary>
        /// Tells the camera how much space is available on the host PC
        /// </summary>
        /// <param name="bytesPerSector">Bytes per sector on HD</param>
        /// <param name="numberOfFreeClusters">Number of free clusters on HD</param>
        public void SetCapacity(int bytesPerSector, int numberOfFreeClusters)
        {
            //create new capacity struct
            EdsCapacity capacity = new EdsCapacity();

            //set given values
            capacity.Reset = 1;
            capacity.BytesPerSector = bytesPerSector;
            capacity.NumberOfFreeClusters = numberOfFreeClusters;

            SetCapacity(capacity);
        }

        private void SetCapacity(EdsCapacity capacity)
        {
            PrevCapacity = capacity;
            SendSDKCommand(delegate { Error = EdsSetCapacity(MainCamera.Ref, capacity); });
        }




        /// <summary>
        /// Moves the focus (only works while in live view)
        /// </summary>
        /// <param name="speed">Speed and direction of focus movement</param>
        public void SetFocus(uint speed)
        {
            if (IsLiveViewOn) SendSDKCommand(delegate { Error = EdsSendCommand(MainCamera.Ref, CameraCommand_DriveLensEvf, (int)speed); });
        }

        /// <summary>
        /// Sets the WB of the live view while in live view
        /// </summary>
        /// <param name="x">X Coordinate</param>
        /// <param name="y">Y Coordinate</param>
        public void SetManualWBEvf(ushort x, ushort y)
        {
            if (IsLiveViewOn)
            {
                //converts the coordinates to a form the camera accepts
                byte[] xa = BitConverter.GetBytes(x);
                byte[] ya = BitConverter.GetBytes(y);
                uint coord = BitConverter.ToUInt32(new byte[] { xa[0], xa[1], ya[0], ya[1] }, 0);
                //send command to camera
                SendSDKCommand(delegate { Error = EdsSendCommand(MainCamera.Ref, CameraCommand_DoClickWBEvf, (int)coord); });
            }
        }

        public List<CameraFileEntry> GetVolumes()
        {
            return GetVolumes(GetCamera());
        }



        public List<CameraFileEntry> GetVolumes(CameraFileEntry camera)
        {
            //get the number of volumes currently installed in the camera
            int VolumeCount;
            Error = EdsGetChildCount(camera.Reference, out VolumeCount);
            List<CameraFileEntry> volumes = new List<CameraFileEntry>();

            //iterate through all of them
            for (int i = 0; i < VolumeCount; i++)
            {
                //get information about volume
                IntPtr childReference;
                Error = EdsGetChildAtIndex(MainCamera.Ref, i, out childReference);
                EdsVolumeInfo volumeInfo = new EdsVolumeInfo();
                SendSDKCommand(delegate { Error = EdsGetVolumeInfo(childReference, out volumeInfo); });
                //ignore the HDD
                if (volumeInfo.szVolumeLabel != "HDD")
                {
                    //add volume to the list
                    volumes.Add(new CameraFileEntry("Volume" + i + "(" + volumeInfo.szVolumeLabel + ")", CameraFileEntryTypes.Volume, childReference) { Volume = volumeInfo });
                }
                //release the volume
                Error = EdsRelease(childReference);
            }
            return volumes;
        }

        public CameraFileEntry GetCamera()
        {
            return new CameraFileEntry("Camera", CameraFileEntryTypes.Camera, MainCamera.Ref);
        }

        public void DeleteFileItem(CameraFileEntry fileItem)
        {
            throw new NotImplementedException();
            // NOTE: Get original structure from camera to delete
            SendSDKCommand(() =>
            {
                var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(fileItem));
                try
                {
                    Marshal.StructureToPtr(fileItem, ptr, false);
                    Error = EdsDeleteDirectoryItem(ptr);

                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
            );
        }

        /// <summary>
        /// Gets all volumes, folders and files existing on the camera
        /// </summary>
        /// <returns>A CameraFileEntry with all informations</returns>
        public CameraFileEntry GetAllEntries()
        {
            //create the main entry which contains all subentries
            CameraFileEntry camera = GetCamera();

            List<CameraFileEntry> volumes = GetVolumes(camera);

            volumes.ForEach(v => v.AddSubEntries(GetChildren(v.Reference)));

            //add all volumes to the main entry and return it
            camera.AddSubEntries(volumes.ToArray());
            return camera;
        }

        /// <summary>
        /// Locks or unlocks the cameras UI
        /// </summary>
        /// <param name="lockState">True for locked, false to unlock</param>
        public void UILock(bool lockState)
        {
            SendSDKCommand(delegate
            {
                if (lockState == true) Error = EdsSendStatusCommand(MainCamera.Ref, CameraState_UILock, 0);
                else Error = EdsSendStatusCommand(MainCamera.Ref, CameraState_UIUnLock, 0);
            });
        }

        /// <summary>
        /// Gets the children of a camera folder/volume. Recursive method.
        /// </summary>
        /// <param name="ptr">Pointer to volume or folder</param>
        /// <returns></returns>
        private CameraFileEntry[] GetChildren(IntPtr ptr)
        {
            int childCount;
            //get children of first pointer
            Error = EdsGetChildCount(ptr, out childCount);
            if (childCount > 0)
            {
                //if it has children, create an array of entries
                CameraFileEntry[] children = new CameraFileEntry[childCount];
                for (int i = 0; i < childCount; i++)
                {
                    IntPtr childReference;
                    //get children of children
                    Error = EdsGetChildAtIndex(ptr, i, out childReference);
                    //get the information about this children
                    EdsDirectoryItemInfo child = new EdsDirectoryItemInfo();
                    SendSDKCommand(delegate { Error = EdsGetDirectoryItemInfo(childReference, out child); });
                    //create entry from information
                    children[i] = new CameraFileEntry(child.szFileName, GetBool(child.isFolder) ? CameraFileEntryTypes.Folder : CameraFileEntryTypes.File, childReference);
                    if (children[i].Type == CameraFileEntryTypes.File)
                    {
                        if (false)
                        {
                            //if it's not a folder, create thumbnail and save it to the entry                       
                            IntPtr stream;
                            Error = EdsCreateMemoryStream(0, out stream);
                            SendSDKCommand(delegate { Error = EdsDownloadThumbnail(childReference, stream); });
                            children[i].AddThumb(GetImage(stream, EdsImageSource.Thumbnail));
                        }
                    }
                    else
                    {
                        //if it's a folder, check for children with recursion
                        CameraFileEntry[] retval = GetChildren(childReference);
                        if (retval != null) children[i].AddSubEntries(retval);
                    }
                    //release current children
                    EdsRelease(childReference);
                }
                return children;
            }
            else return new CameraFileEntry[0];
        }

        /// <summary>
        /// Converts an int to a bool
        /// </summary>
        /// <param name="val">Value</param>
        /// <returns>A bool created from the value</returns>
        private bool GetBool(int val)
        {
            if (val == 0) return false;
            else return true;
        }

        #endregion

        #endregion
    }
}
