using System;
using System.Collections.Generic;
using System.Text;

namespace FubarDev.FtpServer.FileSystem.Oss
{
    public class OssFileSystemOptions
    {
        /// <summary>
        /// 
        /// </summary>
        public string AccessKeyId { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string AccessKeySecret { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string Endpoint { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string BucketName { get; set; }

        /// <summary>
        /// Gets or sets the root path.
        /// </summary>
        public string RootPath { get; set; }
    }
}
