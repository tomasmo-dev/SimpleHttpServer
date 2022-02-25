using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace HttpHost
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Configuration config;
            config = ConfigurationReader.GetConfiguration();

            LoggerFactory factory = new LoggerFactory();

            Server.Hoster server = new(config);
            server.Setup();
        }
    }

    class Configuration
    {
        public bool DevEnv { get; set; }

        public char PathSlash { get; set; }

        public int ProdPort { get; set; }
        public int DevPort { get; set; }

        public int ReceiveBufferSize { get; set; }

        public string JsonConfigPath { get; set; }
        public string LogPath { get; set; }
        public string RoutingPath { get; set; }
        public string WWW_Folder { get; set; }
    }
    class ConfigurationReader
    {
        public static Configuration GetConfiguration()
        {
            var JsonPath = File.ReadAllText("JsonPath.conf");
            var JsonString = File.ReadAllText(JsonPath);

            JObject jsonOBject = JObject.Parse(JsonString);

            Configuration configuration = new Configuration()
            {
                DevEnv = bool.Parse(jsonOBject["DevEnabled"].ToString()),
                ReceiveBufferSize = int.Parse(jsonOBject["BufferSize"].ToString()),
                JsonConfigPath = JsonPath,
                DevPort = int.Parse(jsonOBject["DevPort"].ToString()),
                ProdPort = int.Parse(jsonOBject["ProdPort"].ToString()),
                LogPath = jsonOBject["LogFile"].ToString(),
                RoutingPath = jsonOBject["Routing"].ToString(),
                WWW_Folder = jsonOBject["wwwPath"].ToString()

            };

            if (configuration.DevEnv)
            {
                configuration.PathSlash = '\\';
            }
            else
            {
                configuration.PathSlash= '/';
            }

            return configuration;
        }
    }

}

/*
 * 
 * 
   "HTTP/1.1 200 OK" +
   "Date: ---" +
   "Server: " +
   "Last-Modified: ---" +
   "Content-Length: 111" +
   "Content-Type: text/html" +
   "Connection: Closed";
 * 
 * 
    GET / HTTP / 1.1
    Host: 172.30.11.165:7580
    Connection: keep - alive
    Cache - Control: max - age = 0
    Upgrade - Insecure - Requests: 1
    User - Agent: Mozilla / 5.0(Windows NT 10.0; Win64; x64) AppleWebKit / 537.36(KHTML, like Gecko) Chrome / 97.0.4692.99 Safari / 537.36 OPR / 83.0.4254.66
    Accept: text / html,application / xhtml + xml,application / xml; q = 0.9,image / avif,image / webp,image / apng,*\/\*;q=0.8,application/signed-exchange;v=b3;q=0.9
    Accept-Encoding: gzip, deflate
    Accept-Language: en-US,en;q=0.9
 * 
 */



/*"application/epub+zip",
                "application/gzip",
                "application/java-archive",
                "application/json",
                "application/ld+json",
                "application/msword",
                "application/octet-stream",
                "application/ogg",
                "application/pdf",
                "application/rtf",
                "application/vnd.amazon.ebook",
                "application/vnd.apple.installer+xml",
                "application/vnd.mozilla.xul+xml",
                "application/vnd.ms-excel",
                "application/vnd.ms-fontobject",
                "application/vnd.ms-powerpoint",
                "application/vnd.oasis.opendocument.presentation",
                "application/vnd.oasis.opendocument.spreadsheet",
                "application/vnd.oasis.opendocument.text",
                "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "application/vnd.rar",
                "application/vnd.visio",
                "application/x-7z-compressed",
                "application/x-abiword",
                "application/x-bzip",
                "application/x-bzip2",
                "application/x-csh",
                "application/x-freearc",
                "application/x-httpd-php",
                "application/x-sh",
                "application/x-shockwave-flash",
                "application/x-tar",
                "application/xhtml+xml",
                "application/xml",
                "application/zip",
                "audio/3gpp",
                "audio/3gpp2",
                "audio/aac",
                "audio/mpeg",
                "audio/ogg",
                "audio/opus",
                "audio/wav",
                "audio/webm",
                "audio/x-midi",
                "font/otf",
                "font/ttf",
                "font/woff",
                "font/woff2",
                "image/bmp",
                "image/gif",
                "image/jpeg",
                "image/png",
                "image/svg+xml",
                "image/tiff",
                "image/vnd.microsoft.icon",
                "image/webp",
                "text/calendar",
                "text/css",
                "text/csv",
                "text/html",
                "text/javascript",
                "text/plain",
                "text/xml",
                "video/3gpp",
                "video/3gpp2",
                "video/mp2t",
                "video/mpeg",
                "video/ogg",
                "video/webm",
                "video/x-msvideo"
*/
