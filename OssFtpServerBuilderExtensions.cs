using Microsoft.Extensions.DependencyInjection;
using System;

namespace FubarDev.FtpServer.FileSystem.Oss
{
    /// <summary>
    /// Extension methods for <see cref="IFtpServerBuilder"/>.
    /// </summary>
    public static class OssFtpServerBuilderExtensions
    {
        /// <summary>
        /// Uses the .NET file system API.
        /// </summary>
        /// <param name="builder">The server builder used to configure the FTP server.</param>
        /// <returns>the server builder used to configure the FTP server.</returns>
        public static IFtpServerBuilder UseAliyunOssFileSystem(this IFtpServerBuilder builder)
        {
            builder.Services.AddSingleton<IFileSystemClassFactory, OssFileSystemProvider>();
            return builder;
        }
    }
}
