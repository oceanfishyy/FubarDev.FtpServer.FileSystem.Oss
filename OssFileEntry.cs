using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FubarDev.FtpServer.FileSystem.Oss
{
    /// <summary>
    /// A file entry for an S3 object.
    /// </summary>
    internal class OssFileEntry : OssFileSystemEntry, IUnixFileEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="S3FileEntry"/> class.
        /// </summary>
        /// <param name="key">The S3 object key.</param>
        /// <param name="size">The object size.</param>
        public OssFileEntry(string key, long size)
            : base(key, Path.GetFileName(key))
        {
            Size = size;
        }

        /// <inheritdoc />
        public long Size { get; }
    }
}
