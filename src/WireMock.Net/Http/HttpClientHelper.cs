﻿using JetBrains.Annotations;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using WireMock.HttpsCertificate;
using WireMock.Settings;
using WireMock.Validation;

namespace WireMock.Http
{
    internal static class HttpClientHelper
    {
        public static HttpClient CreateHttpClient(IProxyAndRecordSettings settings)
        {
#if NETSTANDARD || NETCOREAPP3_1
            var handler = new HttpClientHandler
            {
                CheckCertificateRevocationList = false,
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls11 | System.Security.Authentication.SslProtocols.Tls,
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
#elif NET46
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
#else
            var handler = new WebRequestHandler
            {
                ServerCertificateValidationCallback = (sender, certificate, chain, errors) => true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
#endif

            if (!string.IsNullOrEmpty(settings.ClientX509Certificate2ThumbprintOrSubjectName))
            {
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;

                var x509Certificate2 = CertificateLoader.LoadCertificate(settings.ClientX509Certificate2ThumbprintOrSubjectName);
                handler.ClientCertificates.Add(x509Certificate2);
            }

            handler.AllowAutoRedirect = settings.AllowAutoRedirect == true;

            // If UseCookies enabled, httpClient ignores Cookie header
            handler.UseCookies = false;

            if (settings.WebProxySettings != null)
            {
                handler.UseProxy = true;

                handler.Proxy = new WebProxy(settings.WebProxySettings.Address);
                if (settings.WebProxySettings.UserName != null && settings.WebProxySettings.Password != null)
                {
                    handler.Proxy.Credentials = new NetworkCredential(settings.WebProxySettings.UserName, settings.WebProxySettings.Password);
                }
            }

            var client = new HttpClient(handler);
#if NET452 || NET46
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
#endif
            return client;
        }

        public static async Task<ResponseMessage> SendAsync([NotNull] HttpClient client, [NotNull] RequestMessage requestMessage, string url, bool deserializeJson, bool decompressGzipAndDeflate)
        {
            Check.NotNull(client, nameof(client));
            Check.NotNull(requestMessage, nameof(requestMessage));

            var originalUri = new Uri(requestMessage.Url);
            var requiredUri = new Uri(url);

            // Create HttpRequestMessage
            var httpRequestMessage = HttpRequestMessageHelper.Create(requestMessage, url);

            // Call the URL
            var httpResponseMessage = await client.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseContentRead);

            // Create ResponseMessage
            return await HttpResponseMessageHelper.CreateAsync(httpResponseMessage, requiredUri, originalUri, deserializeJson, decompressGzipAndDeflate);
        }
    }
}