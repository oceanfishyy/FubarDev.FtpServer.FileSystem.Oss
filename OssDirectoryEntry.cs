using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FubarDev.FtpServer.FileSystem.Oss
{
    internal class OssDirectoryEntry : OssFileSystemEntry, IUnixDirectoryEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="S3DirectoryEntry"/> class.
        /// </summary>
        /// <param name="key">The path/prefix of the child entries.</param>
        /// <param name="isRoot">Determines if this is the root directory.</param>
        public OssDirectoryEntry(string key, bool isRoot = false)
            : base(key.EndsWith("/") || isRoot ? key : key + "/", Path.GetFileName(key.TrimEnd('/')))
        {
            IsRoot = isRoot;
        }

        /// <inheritdoc />
        public bool IsRoot { get; }

        /// <inheritdoc />
        public bool IsDeletable => !IsRoot;
    }
}
