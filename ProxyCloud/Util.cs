using System.Net;
using System.Net.Sockets;

namespace ProxyCloud
{
    public class Util
    {
        public static IPAddress GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            return host.AddressList.ToList().Find(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        }

        public static IPAddress GetPublicIpAddress()
        {
            return Array.Find(Dns.GetHostEntry(Dns.GetHostName()).AddressList.ToArray(), x => x.AddressFamily == AddressFamily.InterNetwork);
        }

        public static bool CheckConnection(Uri uri)
        {
            try
            {
                var client = new TcpClient(uri.Host, uri.Port)
                {
                    LingerState = new LingerOption(true, 0)
                };
                client.Close();
                client.Dispose();
                return true;
            }
            catch (Exception)
            {
            }
            return false;
        }

        public static bool IsReachable(string url)
        {
            if (url != null)
                try
                {
                    WebClient webClient = new WebClient();
                    var page = webClient.DownloadString(url);
                    return page == "ok";
                }
                catch ( Exception ex)
                {
                }
            return false;
        }
        //if (url != null)
        //{
        //    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        //    request.Timeout = 3000;
        //    try
        //    {
        //        WebResponse resp = request.GetResponse();
        //        return true;
        //    }
        //    catch { }
        //}
        //return false;
    }
}

