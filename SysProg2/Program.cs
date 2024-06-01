using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysProg2
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            string localhost = "http://127.0.0.1";
            int port = 8080;
            string rootDirectoryPath = @"../../root/";
            HttpServer server = new HttpServer(localhost, port, rootDirectoryPath, 2);
            await server.Launch();
        }
    }
}
