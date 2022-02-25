using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace HttpHost.Server
{
    class Headers
    {
        public string Method { get; set; }
        public string RequestedFile { get; set; }

        public string Cookies { get; set; }
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

        public Hoster(Configuration cfg)
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
#pragma warning disable CS0618 // Type or member is obsolete
            ListenerTh.Suspend();
#pragma warning restore CS0618 // Type or member is obsolete
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
            //SslStream sslStream=new SslStream(NetStream);
            //sslStream.AuthenticateAsServer();

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

            Logger.LogInformation(LogText);

            NetStream.Flush();

            NetStream.Close();
            NetStream.Dispose();
            //client.Close();
            //listener.Stop();

            tcpListenerReset.Set();
        }

        private static byte[] Check404(Tuple<byte[], bool, FileType> FileArray)
        {
            var FileTP = "";

            switch (FileArray.Item3)
            {
                case FileType.Html:
                    FileTP = "text/html";
                    break;

                case FileType.Css:
                    FileTP = "text/css";
                    break;

                case FileType.Js:
                    FileTP = "text/javascript";
                    break;

                case FileType.Plain:
                    FileTP = "text/plain";
                    break;

                case FileType.Ico:
                    FileTP = "image/png";
                    break;
            }

            byte[] Response;
            if (FileArray.Item2)
            {
                Response = Encoding.ASCII.GetBytes(ResponseTemplates.GetResponse("200", "OK", FileArray.Item1.Length.ToString(), FileTP))
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
        private Tuple<byte[], bool, FileType> GetFileFromTable(Headers h)
        {
            JObject Routing = JObject.Parse(File.ReadAllText(conf.RoutingPath));

            byte[] ResponseFile = null;
            string[] extension = h.RequestedFile.Split('.');

            FileType ft;

            switch (extension[extension.Length - 1])
            {
                case "html":
                    ResponseFile = Encoding.UTF8.GetBytes(File.ReadAllText(GetPathFromJson(Routing, h.RequestedFile, FileType.Html)));
                    ft = FileType.Html;
                    break;

                case "css":
                    ResponseFile = Encoding.UTF8.GetBytes(File.ReadAllText(GetPathFromJson(Routing, h.RequestedFile, FileType.Css)));
                    ft = FileType.Css;
                    break;

                case "js":
                    ResponseFile = Encoding.UTF8.GetBytes(File.ReadAllText(GetPathFromJson(Routing, h.RequestedFile, FileType.Js)));
                    ft = FileType.Js;
                    break;

                case "ico":
                    ResponseFile = new byte[] { 1, 2, 0, 1, 100 };
                    ft = FileType.Ico;
                    break;

                default:
                    ResponseFile = Encoding.UTF8.GetBytes(File.ReadAllText(GetPathFromJson(Routing, h.RequestedFile, FileType.Html)));
                    ft = FileType.Html;
                    break;
            }

            return new Tuple<byte[], bool, FileType>(ResponseFile, true, ft);
        }
        private string GetPathFromJson(JObject routing, string v, FileType f)
        {
            string ResultPath;
            string SavedPath;

            string _v = v.Replace("/", "");

            switch (f)
            {
                case FileType.Html:
                    SavedPath = routing[v].ToString();
                    ResultPath = conf.WWW_Folder + @$"Html{conf.PathSlash}" + SavedPath;
                    break;
                case FileType.Css:
                    ResultPath = conf.WWW_Folder + @$"css{conf.PathSlash}" + _v;
                    break;

                case FileType.Js:
                    ResultPath = conf.WWW_Folder + @$"js{conf.PathSlash}" + _v;
                    break;

                case FileType.Plain:
                    SavedPath = routing[v].ToString();
                    ResultPath = conf.WWW_Folder + @$"Plain{conf.PathSlash}" + SavedPath; // -------------------------------------------------------------- IMPLEMENT PLAIN!!!
                    break;
                default:
                    SavedPath = routing[v].ToString();
                    ResultPath = conf.WWW_Folder + @$"Html{conf.PathSlash}" + SavedPath;
                    break;
            }

            if (!File.Exists(ResultPath))
            {
                ResultPath = conf.WWW_Folder + @$"Html{conf.PathSlash}404.html";
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
            Plain,
            Ico
        }

        #endregion

    }
}