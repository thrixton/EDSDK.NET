using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using static EDSDKLib.EDSDK;

namespace EDSDK.NET
{
    public enum CameraFileEntryTypes
    {
        Camera = 5,
        Volume = 10,
        Folder = 20,
        File = 30,
    }

    /// <summary>
    /// A storage for a camera filesystem entry
    /// </summary>
    public class CameraFileEntry
    {
        /// <summary>
        /// Name of this entry
        /// </summary>
        public string Name { get; private set; }

        public CameraFileEntryTypes Type { get; private set; }

        public IntPtr Reference { get; private set; }

        /// <summary>
        /// Thumbnail of this entry (might be null if not available)
        /// </summary>
        public Bitmap Thumbnail { get; private set; }
        /// <summary>
        /// Subentries of this entry (i.e. subfolders)
        /// </summary>
        public CameraFileEntry[] Entries { get; private set; }

        /// <summary>
        /// Creates a new instance of the CameraFileEntry class
        /// </summary>
        /// <param name="Name">Name of this entry</param>
        /// <param name="IsFolder">True if this entry is a folder, false otherwise</param>
        public CameraFileEntry(string Name, CameraFileEntryTypes type, IntPtr reference)
        {
            this.Name = Name;
            this.Type = type;
            this.Reference = reference;
        }

        /// <summary>
        /// Adds subentries (subfolders) to this entry
        /// </summary>
        /// <param name="Entries">the entries to add</param>
        public void AddSubEntries(CameraFileEntry[] Entries)
        {
            this.Entries = Entries;
        }

        /// <summary>
        /// Adds a thumbnail to this entry
        /// </summary>
        /// <param name="Thumbnail">The thumbnail to add</param>
        public void AddThumb(Bitmap Thumbnail)
        {
            this.Thumbnail = Thumbnail;
        }

        public EdsVolumeInfo Volume { get; set; }
    }
}
