namespace ProxyCloud
{
    public static class MapEndpoints
    {
        public static string CurrentHost;
        static public CommunicationServer Server;
        public static void Startup(string privateKey,string entryPoint)
        {
            Server = new CommunicationServer(privateKey, entryPoint);
            Console.WriteLine(MapEndpoints.Info);
        }

        /// <summary>
        /// Information about the current instance of the application
        /// </summary>
        public static string Info
        {
            get
            {
                return "Router entry point = " + EncryptedMessaging.Context.EntryPoint.Host + "\r\n"
                     + "Is connected to router= " + CommunicationServer.Context.IsConnected.ToString() + "\r\n"
                     + "Host = " + CurrentHost + "\r\n"
                     + "Host is reachable = " + (CurrentHost == null ? "" : Util.IsReachable(CurrentHost + @"/?ping=true")) + "\r\n"
                     + "ID = " + Server.Id + "\r\n"
                     + "PubblicKey = " + Server.PubblicKey + "\r\n"
                     + "Connected devices = " + CommunicationServer.Context.Contacts.GetContacts().Count;
            }
        }


        public static async Task Status (HttpContext context)
            {

            if (context.Request.Query.TryGetValue("ping", out var ping))
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                await context.Response.WriteAsync("ok");
            }
            else
            {
                var CurrentPort = context.Request.Host.Port;
                CurrentHost = (context.Request.IsHttps ? "https://" : "http://") + Util.GetPublicIpAddress().ToString() + ":" + CurrentPort;
                await context.Response.WriteAsync(Info);
            }
        }

        public static async Task DataGet (HttpContext context)
        {
            if (context.Request.Query.TryGetValue("cid", out var cid))
            {
                var idbytes = cid[0].ToString().HexToBytes();
                var clientId = BitConverter.ToUInt64(idbytes, 0);
                if (Communication.GetPushNotification(clientId, out Communication.CommandForClient pushNotification))
                {
                    await SetResponse(context, pushNotification);
                    return;
                }
            }
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            await context.Response.WriteAsync(context.Response.StatusCode.ToString());
        }

        public static async Task SetResponse(HttpContext context, Communication.CommandForClient response)
        {
            await context.Response.Body.WriteAsync(System.Text.Encoding.ASCII.GetBytes(Convert.ToBase64String(response.EncriptedData)));
        }

        public static async Task DataPost(HttpContext context)
        {
            if (context.Request.Query.TryGetValue("cid", out var cid))
            {
                ulong? chatId = null;
                var purpose = Communication.Purpose.Forwarding;
                if (context.Request.Query.TryGetValue("purpose", out var purposeText))
                    Enum.TryParse(purposeText, out purpose);
                ulong clientId = BitConverter.ToUInt64(cid[0].ToString().HexToBytes(), 0);
                if (purpose == Communication.Purpose.SetClient && context.Request.Query.TryGetValue("sid", out var sid))
                {
                    var serverId = BitConverter.ToUInt64(sid[0].ToString().HexToBytes(), 0);
                    chatId = Communication.UserIdToChatId.GetCorrespondingId(serverId);
                }
                else
                {
                    chatId = Communication.ClientIdToChatId.GetCorrespondingId(clientId);
                }
                if (chatId != null)
                {
                    byte[]? data = null;
                    if (context.Request.Query.TryGetValue("dt", out var dt))
                    {
                        var datab64 = dt[0].ToString();
                        data = Convert.FromBase64String(datab64);
                    }
                    else if (context.Request.ContentLength != null && context.Request.ContentLength > 0)
                    {
                        var dataLength = (int)context.Request.ContentLength;
                        data = new byte[dataLength];
                        int readed = 0;
                        do
                        {
                            IAsyncResult arRead = context.Request.Body.BeginRead(data, readed, dataLength - readed, null, null);
                            if (!arRead.AsyncWaitHandle.WaitOne(120000))
                            {
                                context.Response.StatusCode = StatusCodes.Status408RequestTimeout;
                                return;
                            }
                            readed += context.Request.Body.EndRead(arRead);
                        } while (readed < dataLength);

                        //int readed = 0;
                        //do
                        //{
                        //    readed += await context.Request.Body.ReadAsync(data, readed, dataLength); // don't use Read block, the process!
                        //} while (readed < dataLength);

                    }
                    string ip = null;
                    string userAgent = null;
                    if (purpose == Communication.Purpose.SetClient)
                    {
                        ip = context.Request.Host.Host;
                        userAgent = context.Request.Headers["User-Agent"];
                    }

                    if (Communication.RequestToDevice((ulong)chatId, clientId, purpose, data, out Communication.CommandForClient? response, ip, userAgent))
                    {
                        await SetResponse(context, response);
                        return;
                    }
                }
            }
            if (context.Response.StatusCode == StatusCodes.Status200OK)
                context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync(context.Response.StatusCode.ToString());
        }
    }
}
