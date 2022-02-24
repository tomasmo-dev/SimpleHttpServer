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

            Server.Hoster server = new(factory, config);
            server.Setup();
        }
    }

    class Configuration
    {
        public bool DevEnv { get; set; }

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

            return configuration;
        }
    }

}

namespace HttpHost.Server
{
    class Headers
    {
        public string Method { get; set; }
        public string? RequestedFile { get; set; }

        public string? Cookies { get; set; }
    }

    class Hoster
    {
        private readonly Logger.Logger Logger;
        private Configuration conf { get; set; }

        #region TcpConnection

        private TcpListener Socket { get; set; }

        private IPAddress ListenerIPAddress { get; set; }
        private readonly int DevPort;
        private readonly int ProdPort;

        private int ActivePort { get; set; }

        private int BufferLength { get; set; }

        #endregion

        #region Thread Handles

        private ManualResetEvent tcpListenerReset = new ManualResetEvent(false);
        private Thread ListenerTh { get; set; }
        private bool RunServer { get; set; } = true;

        #endregion

        internal class ResponseTemplates
        {
            public static string GetResponse(string code, string signal, string content_length, string FileType)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($"HTTP/1.1 {code} {signal}\r\n");
                sb.Append($"Content-Length: {content_length}\r\n");
                sb.Append($"Content-Type: {FileType}\r\n\r\n");

                return sb.ToString();

            }
        }
        
        public Hoster(LoggerFactory LogConfiguration, Configuration cfg)
        {
            conf = cfg;

            DevPort = cfg.DevPort;
            ProdPort = cfg.ProdPort;

            BufferLength = cfg.ReceiveBufferSize;

            Logger = new(cfg.LogPath);

            Logger.LogInformation("Starting Server!");
        }

        public void Stop()
        {
            ListenerTh.Suspend();
            tcpListenerReset.Set();

        }
        public void Setup()
        {
            if (conf.DevEnv)
            {
                ActivePort = DevPort;
            }
            else
            {
                ActivePort = ProdPort;
            }

            var IpAddress = Dns.GetHostEntry(Dns.GetHostName()).AddressList;

            Logger.LogInformation("Select Address to host on : ");
            ushort Index = 0;
            foreach (var address in IpAddress)
            {
                Logger.LogInformation($"{Index}) {address.ToString()}");
                Index++;
            }

            string addrIndex = Console.ReadLine();

            ListenerIPAddress = IpAddress[int.Parse(addrIndex)];
            var localEndPoint = new IPEndPoint(IpAddress[int.Parse(addrIndex)], ActivePort);

            Socket = new TcpListener(localEndPoint);
            Socket.Start();

            ListenerTh = new Thread(new ThreadStart(ListenerLoop));
            ListenerTh.Start();

            Logger.LogInformation($"Listening on IP   : {IpAddress[int.Parse(addrIndex)]}");
            Logger.LogInformation($"Listening on Port : {ActivePort}");
            Logger.LogInformation($"Page available on : {IpAddress[int.Parse(addrIndex)]}:{ActivePort}");
        }

        private void ListenerLoop()
        {
            while (RunServer)
            {
                tcpListenerReset.Reset();

                Socket.BeginAcceptTcpClient(new AsyncCallback(ProcessConnection), Socket);

                tcpListenerReset.WaitOne();

            }
        }

        #region Client Handling

            private async void ProcessConnection(IAsyncResult ar)
            {
                TcpListener listener = ar.AsyncState as TcpListener;

                var client = listener.EndAcceptTcpClient(ar);

                NetworkStream NetStream = client.GetStream();

                byte[] Buffer = new byte[BufferLength];

                await NetStream.ReadAsync(Buffer, 0, Buffer.Length);

                string conData = Encoding.ASCII.GetString(Buffer);

                Headers h = ParseRequest(conData);

                string LogText = $"Ip address : {((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString()} | Requested data = {h.RequestedFile} | With Method : {h.Method}";

                if (h.Method == "GET")
                {
                    var FileArray = GetFileFromTable(h);

                    byte[] Response;
                    Response = Check404(FileArray);

                    await NetStream.WriteAsync(Response, 0, Response.Length);
                }


                NetStream.Flush();

                NetStream.Close();
                NetStream.Dispose();
                //client.Close();
                //listener.Stop();

                tcpListenerReset.Set();
            }

            private static byte[] Check404(Tuple<byte[], bool> FileArray)
            {
                byte[] Response;
                if (FileArray.Item2)
                {
                    Response = Encoding.ASCII.GetBytes(ResponseTemplates.GetResponse("200", "OK", FileArray.Item1.Length.ToString(), "text/html"))
                        .Concat(FileArray.Item1)
                        .ToArray();
                }
                else
                {
                    Response = Encoding.ASCII.GetBytes(ResponseTemplates.GetResponse("404", "NOT FOUND", FileArray.Item1.Length.ToString(), "text/html"))
                        .Concat(FileArray.Item1)
                        .ToArray();
                }

                return Response;
            }

            // Exists => true | not => false
            private Tuple<byte[], bool> GetFileFromTable(Headers h)
            {
                JObject Routing = JObject.Parse(File.ReadAllText(conf.RoutingPath));

                byte[] ResponseFile = {0};

                if (!Routing.ContainsKey(h.RequestedFile))
                {
                    ResponseFile = Encoding.UTF8.GetBytes(File.ReadAllText(GetPathFromJson(Routing, "/404", FileType.Html)));
                    return new Tuple<byte[], bool>(ResponseFile, false);
                }

                switch (Routing[h.RequestedFile].ToString().Split('.')[^1])
                {
                    case "html":
                        Encoding.UTF8.GetBytes(File.ReadAllText(GetPathFromJson(Routing, h.RequestedFile, FileType.Html)));
                        break;

                    case "css":
                        Encoding.UTF8.GetBytes(File.ReadAllText(GetPathFromJson(Routing, h.RequestedFile, FileType.Css)));
                        break;

                    case "js":
                        Encoding.UTF8.GetBytes(File.ReadAllText(GetPathFromJson(Routing, h.RequestedFile, FileType.Js)));
                        break;

                    default:
                        Encoding.UTF8.GetBytes(File.ReadAllText(GetPathFromJson(Routing, h.RequestedFile, FileType.Html)));
                        break;
                }

                return new Tuple<byte[], bool>(ResponseFile, true);
            }
            private string GetPathFromJson(JObject routing, string v, FileType f)
            {
                string ResultPath;
                string SavedPath = routing[v].ToString();

                switch (f)
                {
                    case FileType.Html:
                        ResultPath = conf.WWW_Folder + @"Html" + SavedPath;
                        break;
                    case FileType.Css:
                        ResultPath = conf.WWW_Folder + @"css" + SavedPath;
                        break;

                    case FileType.Js:
                        ResultPath = conf.WWW_Folder + @"js" + SavedPath;
                        break;

                    case FileType.Plain:
                        ResultPath = conf.WWW_Folder + @"Plain" + SavedPath; // -------------------------------------------------------------- IMPLEMENT PLAIN!!!
                        break;
                    default:
                        ResultPath = conf.WWW_Folder + @"Html" + SavedPath;
                        break;
                }


                return ResultPath;
            }

            private Headers ParseRequest(string conData)
            {
                string[] lines = conData.Split("\n");

                string[] QuestionLine = lines[0].Split(" ");

                string Method = QuestionLine[0];
                string File = QuestionLine[1];
                string Cookies = null;

                foreach (var line in lines)
                {
                    if (line.Split(" ")[0] == "Cookies:")
                    {
                        Cookies = string.Join(" ", line.Split(" ").Skip(0));
                    }
                }

                Headers header = new Headers()
                {
                    Method = Method,
                    RequestedFile = File,
                    Cookies = Cookies
                };

                return header;
            }

            enum FileType
            {
                Html,
                Css,
                Js,
                Plain
            }

        #endregion

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
