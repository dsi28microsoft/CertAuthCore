﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TodoApi.Models;
using Microsoft.Extensions.Options;
using System.Security.Cryptography.X509Certificates;
using TodoApi.Middleware;
using System.IO;

namespace TodoApi.Middleware
{
    // You may need to install the Microsoft.AspNetCore.Http.Abstractions package into your project
    public class ClientCertificateMiddleware
    {

        private readonly RequestDelegate _next;
        private readonly CertificateValidationConfig _config;
        private readonly ILogger _logger;

        public ClientCertificateMiddleware(RequestDelegate next, ILoggerFactory loggerFactory, IOptions<CertificateValidationConfig> options)
        {
            _next = next;
            _config = options.Value;
            _logger = loggerFactory.CreateLogger("ClientCertificateMiddleware");
        }

        public async Task Invoke(HttpContext context)
        {
            //Validate the cert here

            bool isValidCert = false;
            X509Certificate2 certificate = null;

            string certHeader = context.Request.Headers["X-ARR-ClientCert"];

            if (!String.IsNullOrEmpty(certHeader))
            {
                try
                {
                    byte[] clientCertBytes = Convert.FromBase64String(certHeader);
                    certificate = new X509Certificate2(clientCertBytes);

                    isValidCert = IsValidClientCertificate(certificate);
                   
                    if (isValidCert)
                    {
                        //Invoke the next middleware in the pipeline
                        await _next.Invoke(context);
                    }
                    else
                    {
                        //Stop the pipeline here.
                        _logger.LogInformation("Certificate with thumbprint " + certificate.Thumbprint + " is not valid");
                        context.Response.StatusCode = 403;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message, ex);
                    //Assume that an error means unable to parse the
                    //certificate or an invalid cert was provided.
                    context.Response.StatusCode = 403;
                }
            }
            else
            {
                _logger.LogDebug("X-ARR-ClientCert header is missing");
                context.Response.StatusCode = 403;
            }
        }


        private bool IsValidClientCertificate(X509Certificate2 certificate)
        {
            // This example does NOT test that this certificate is chained to a Trusted Root Authority (or revoked) on the server
            // and it allows for self signed certificates
            //
            Console.WriteLine("In Validate Cert");

            if (null == certificate) return false;
            
            // 1. Check time validity of certificate
            if (DateTime.Compare(DateTime.Now, certificate.NotBefore) < 0 || DateTime.Compare(DateTime.Now, certificate.NotAfter) > 0) return false;

            // 2. Check subject name of certificate
            bool foundSubject = false;
            string[] certSubjectData = certificate.Subject.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string s in certSubjectData)
            {
                if (String.Compare(s.Trim(), _config.Subject) == 0)
                {
                    foundSubject = true;
                    break;
                }
            }
            if (!foundSubject) return false;

            // 3. Check issuer name of certificate
            bool foundIssuerCN = false;
            string[] certIssuerData = certificate.Issuer.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string s in certIssuerData)
            {
                if (String.Compare(s.Trim(), _config.IssuerCN) == 0)
                {
                    foundIssuerCN = true;
                    break;
                }

            }

            if (!foundIssuerCN) return false;

            // 4. Check thumprint of certificate using appsettings.json file

            if (String.Compare(certificate.Thumbprint.Trim().ToUpper(), _config.Thumbprint.ToUpper()) != 0) return false;


            // 5. find cert in Azure app service and compare it to the cert the client provided

            X509Store certStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            certStore.Open(OpenFlags.ReadOnly);
            X509Certificate2Collection certCollection = certStore.Certificates.Find(X509FindType.FindByThumbprint,
                _config.Thumbprint,
                //"be07cfbf3184d445a95a3e531dbd0fdd64a9c836", 
                false);
            // Get the first cert with the thumbprint
            if (certCollection.Count > 0)
            {
                X509Certificate2 tempCert = certCollection[0];
                // Use certificate
                if (certificate.Thumbprint.ToUpper() != tempCert.Thumbprint.ToUpper())
                {
                    return false;
                }
            }
            else return false;


            certStore.Close();
            return true;
        }

    }


    // Extension method used to add the middleware to the HTTP request pipeline.
    public static class ClientCertificateMiddlewareExtensions
    {
        public static IApplicationBuilder UseClientCertificateMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ClientCertificateMiddleware>();
        }
        public static IApplicationBuilder UseClientCertMiddleware(this IApplicationBuilder builder, IOptions<CertificateValidationConfig> options)
        {
            return builder.UseMiddleware<ClientCertificateMiddleware>(options);
        }
    }
}
