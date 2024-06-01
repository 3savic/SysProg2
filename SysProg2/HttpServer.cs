using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;

namespace SysProg2
{
    internal class HttpServer
    {
        public static readonly byte[] notFoundRequestBody = Encoding.ASCII.GetBytes("<h1>Not found.</h1>");

        private string baseUrl;
        private int port;
        private string rootDir;
        private LRUCache<string, byte[]> cache;

        public HttpServer(string baseUrl, int port, string rootDir, int cacheCapacity)
        {
            this.baseUrl = baseUrl;
            this.port = port;
            this.rootDir = rootDir;
            cache = new LRUCache<string, byte[]>(cacheCapacity);
        }

        public async Task Launch()
        {
            string address = $"{baseUrl}:{port}/";
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(address);
                listener.Start();
                Console.WriteLine($"Listening on {address}...");
                while (listener.IsListening)
                {
                    var context = await listener.GetContextAsync();
                    Task.Run(async () =>
                    {
                        try
                        {

                            string fileName = Path.GetFileName(context.Request.RawUrl);

                            if (fileName == string.Empty)
                            {
                                await BuildPage(context);
                                return;
                            }
                            else
                            {
                                await DownloadFile(fileName, context);
                            }

                        }
                        catch (Exception ex)
                        {
                            if (context.Response.OutputStream.CanWrite)
                            {
                                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                                context.Response.OutputStream.Close();
                            }
                            Console.WriteLine(ex.Message);
                        }
                    });
                }
            }
        }
        private async Task DownloadFile(string fileName, HttpListenerContext context)
        {
            string filePath = Path.GetFullPath(rootDir) + fileName;
            byte[] responseBody;
            if (!cache.TryGetValue(fileName, out responseBody))
            {
                if (!File.Exists(filePath))
                {
                    await SendResponse(context, notFoundRequestBody, "text/html", HttpStatusCode.NotFound);
                    Console.WriteLine("File does not exist.");
                }
                else
                {
                    Console.WriteLine($"Requested download for {filePath}");
                    using (FileStream fs = new FileStream(filePath, FileMode.Open))
                    {
                        responseBody = new byte[fs.Length];
                        fs.Read(responseBody, 0, responseBody.Length);
                        cache.Add(fileName, responseBody);
                        await SendResponse(context, responseBody, "attachment", HttpStatusCode.OK, true);
                        Console.WriteLine($"\"{filePath}\" has been downloaded.");
                    }
                }
            }
            else
            {
                await SendResponse(context, responseBody, "attachment", HttpStatusCode.OK, true);
                Console.WriteLine($"\"{filePath}\" has been downloaded from the cache.");
            }
        }
        private async Task BuildPage(HttpListenerContext context)
        {
            FileInfo[] files = new DirectoryInfo(rootDir).GetFiles();
            string elements = string.Empty;
            foreach (FileInfo file in files)
            {
                elements += $"<li><a href=\"http://localhost:8080/{file.Name}\" target=\"_blank\">{file.Name}</a></li>";
            }
            string responseBody = "<HTML>" +
                                  "<BODY>" +
                                  "<ul>" +
                                  elements +
                                  "</ul>" +
                                  "</BODY>" +
                                  "</HTML>";

            await SendResponse(context, Encoding.ASCII.GetBytes(responseBody), "text/html", HttpStatusCode.OK);
        }
        private async Task SendResponse(HttpListenerContext context, byte[] responseBody, string contentType, HttpStatusCode statusCode = HttpStatusCode.OK, Boolean att = false)
        {
            string logString = string.Format(
                "REQUEST:\n{0} {1} HTTP/{2}\nHost: {3}\nUser-agent: {4}\n-------------------\nRESPONSE:\nStatus: {5}\nDate: {6}\nContent-Type: {7}\nContent-Length: {8}\n",
                context.Request.HttpMethod,
                context.Request.RawUrl,
                context.Request.ProtocolVersion,
                context.Request.UserHostName,
                context.Request.UserAgent,
                statusCode,
                DateTime.Now,
                contentType,
                responseBody.Length
            );
            if (att == true)
            {
                context.Response.AppendHeader("Content-Disposition", "attachment");
            }
            context.Response.ContentType = contentType;
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentLength64 = responseBody.Length;

            using (Stream outputStream = context.Response.OutputStream)
            {
                await outputStream.WriteAsync(responseBody, 0, responseBody.Length);
            }
            Console.WriteLine(logString);
        }
    }
}
