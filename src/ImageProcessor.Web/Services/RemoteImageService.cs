﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RemoteImageService.cs" company="James Jackson-South">
//   Copyright (c) James Jackson-South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The remote image service.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using System.Web;

    using ImageProcessor.Web.Caching;
    using ImageProcessor.Web.Helpers;

    using Microsoft.IO;

    /// <summary>
    /// The remote image service.
    /// </summary>
    public class RemoteImageService : IImageService
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteImageService"/> class.
        /// </summary>
        public RemoteImageService()
        {
            this.Settings = new Dictionary<string, string>
            {
                { "MaxBytes", "4194304" },
                { "Timeout", "30000" },
                { "Protocol", "http" },
                { "UserAgent", string.Empty }
            };

            this.WhiteList = new Uri[] { };
        }

        /// <summary>
        /// Gets or sets the prefix for the given implementation.
        /// <remarks>
        /// This value is used as a prefix for any image requests that should use this service.
        /// </remarks>
        /// </summary>
        public string Prefix { get; set; } = "remote.axd";

        /// <summary>
        /// Gets a value indicating whether the image service requests files from
        /// the locally based file system.
        /// </summary>
        public bool IsFileLocalService => false;

        /// <summary>
        /// Gets or sets any additional settings required by the service.
        /// </summary>
        public Dictionary<string, string> Settings { get; set; }

        /// <summary>
        /// Gets or sets the white list of <see cref="System.Uri"/>.
        /// </summary>
        public Uri[] WhiteList { get; set; }

        /// <summary>
        /// Gets a value indicating whether the current request passes sanitizing rules.
        /// </summary>
        /// <param name="path">
        /// The image path.
        /// </param>
        /// <returns>
        /// <c>True</c> if the request is valid; otherwise, <c>False</c>.
        /// </returns>
        public virtual bool IsValidRequest(string path)
        {
            // Check the url is from a whitelisted location.
            Uri url = new Uri(path);
            string upper = url.Host.ToUpperInvariant();

            // Check for root or sub domain.
            bool validUrl = false;
            foreach (Uri uri in this.WhiteList)
            {
                if (!uri.IsAbsoluteUri)
                {
                    Uri rebaseUri = new Uri("http://" + uri.ToString().TrimStart('.', '/'));
                    validUrl = upper.StartsWith(rebaseUri.Host.ToUpperInvariant()) || upper.EndsWith(rebaseUri.Host.ToUpperInvariant());
                }
                else
                {
                    validUrl = upper.StartsWith(uri.Host.ToUpperInvariant()) || upper.EndsWith(uri.Host.ToUpperInvariant());
                }

                if (validUrl)
                {
                    break;
                }
            }

            return validUrl;
        }

        /// <summary>
        /// Gets the image using the given identifier.
        /// </summary>
        /// <param name="id">
        /// The value identifying the image to fetch.
        /// </param>
        /// <returns>
        /// The <see cref="System.Byte"/> array containing the image data.
        /// </returns>
        public virtual async Task<byte[]> GetImage(object id)
        {
            Uri uri = new Uri(id.ToString());
            RemoteFile remoteFile = new RemoteFile(uri)
            {
                MaxDownloadSize = int.Parse(this.Settings["MaxBytes"]),
                TimeoutLength = int.Parse(this.Settings["Timeout"])
            };

            // Check for optional user agesnt.
            if (this.Settings.ContainsKey("Useragent"))
            {
                if (!string.IsNullOrWhiteSpace(this.Settings["Useragent"]))
                {
                    remoteFile.UserAgent = this.Settings["Useragent"];
                }
            }

            byte[] buffer;

            // Prevent response blocking.
            WebResponse webResponse = await remoteFile.GetWebResponseAsync().ConfigureAwait(false);

            using (RecyclableMemoryStream memoryStream = new RecyclableMemoryStream(MemoryStreamPool.Shared))
            {
                using (WebResponse response = webResponse)
                {
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        if (responseStream != null)
                        {
                            responseStream.CopyTo(memoryStream);

                            // Reset the position of the stream to ensure we're reading the correct part.
                            memoryStream.Position = 0;

                            buffer = memoryStream.GetBuffer();
                        }
                        else
                        {
                            throw new HttpException((int)HttpStatusCode.NotFound, $"No image exists at {uri}");
                        }
                    }
                }
            }

            return buffer;
        }
    }
}