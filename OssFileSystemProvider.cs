using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace FubarDev.FtpServer.FileSystem.Oss
{
    public class OssFileSystemProvider : IFileSystemClassFactory
    {
        private readonly OssFileSystemOptions _options;
        private readonly IAccountDirectoryQuery _accountDirectoryQuery;

        /// <summary>
        /// Initializes a new instance of the <see cref="S3FileSystemProvider"/> class.
        /// </summary>
        /// <param name="options">The provider options.</param>
        /// <param name="accountDirectoryQuery">Interface to query account directories.</param>
        /// <exception cref="ArgumentException">Gets thrown when the S3 credentials weren't set.</exception>
        public OssFileSystemProvider(IOptions<OssFileSystemOptions> options, IAccountDirectoryQuery accountDirectoryQuery)
        {
            _options = options.Value;
            _accountDirectoryQuery = accountDirectoryQuery;

            if (string.IsNullOrEmpty(_options.AccessKeyId)
                || string.IsNullOrEmpty(_options.AccessKeySecret)
                || string.IsNullOrEmpty(_options.Endpoint)
                || string.IsNullOrEmpty(_options.BucketName))
            {
                throw new ArgumentException("Oss Credentials have not been set correctly");
            }
        }

        public Task<IUnixFileSystem> Create(IAccountInformation accountInformation)
        {
            var directories = _accountDirectoryQuery.GetDirectories(accountInformation);
            var rootDictionary = OssPath.Combine(_options.RootPath, directories.RootPath);

            return Task.FromResult<IUnixFileSystem>(new OssFileSystem(_options, rootDictionary));
        }
    }
}
