using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ProxyAPISupport
{
    public static class Util
    {
        /// <summary>
        /// Information about the current instance of the application
        /// </summary>
        public static string Info
        {
            get
            {
                if (!Communication.IsInitialized)
                {
                    return "The local proxy was not initialized!" + "\r\n"
                         + "To initialize the proxy the router must be exposed to the internet." + "\r\n"
                         + "Fix the problem and restart the application!" + "\r\n";
                }
                var host = CurrentHost;

                var RealTimeClients = "\r\n" + "REALTIME ClientID -> ConcurentRequests:" + "\r\n";
                lock (Communication.ConcurrentRequestForUser)
                {
                    Communication.ConcurrentRequestForUser.ToList().ForEach(x => RealTimeClients += x.Key + " -> " + x.Value + "\r\n");
                }

                return "Router entry point = " + CommunicationServer.Context.EntryPoint.Host + "\r\n"
                     + "Is connected to router = " + CommunicationServer.Context.IsConnected + "\r\n"
                     + "Host = " + host + "\r\n"
                     + "Is reachable = " + (host == null ? "undefined" : IsReachable().ToString()) + "\r\n"
                     + "ID = " + Communication.Server.Id + "\r\n"
                     + "PubblicKey = " + Communication.Server.PubblicKey + "\r\n"
                     + "Clouds connected = " + CommunicationServer.Context.Contacts.Count + "\r\n"
                     + "Request counter = " + Communication.RequestToDeviceCounter + "\r\n"
                     + "Answer counter = " + Communication.AnswerFromDeviceCounter + "\r\n"
                     + "Current concurent request = " + Communication.RequestConcurent + " (max " + Communication.MaxRequestConcurent + ")\r\n"
                     + "Last request saturaion = " + (Communication.LastRequestSaturation == default ? "never (ok)" : Communication.LastRequestSaturation.ToString("G")) + "\r\n"
                     + "Last client over request = " + (Communication.LastClientOverRequest == default ? "never (ok)" : (DateTime.UtcNow - Communication.LastClientOverRequest).ToString("G") + " ago") + "\r\n"
                     + RealTimeClients
                     ;
            }
        }

        private static Uri _CurrentHost;
        private static bool CurrentHostFirstRead;
        public static Uri CurrentHost
        {
            get
            {
                if (Communication.IsInitialized)
                {
                    lock (AppDomain.CurrentDomain)
                    {
                        if (!CurrentHostFirstRead)
                        {
                            CurrentHostFirstRead = true;
                            if (!SpinWait.SpinUntil(() => _CurrentHost != null, 10000))
                            {
                                // ==================== WARNING: DefaultUrls is not setting at startup !!! ===================
                                //
                                // When the web application that uses this library starts, in the start routine (startup.cs)
                                // it is necessary to initialize the host name with which the proxy is reachable!
                                // 
                                // public Startup(IConfiguration configuration)
                                // {
                                //     ProxyAPISupport.Util.SetCurrentHost(proxyApiEntryPoint);
                                // }
                                // ===========================================================================================
                                Debugger.Break();
                            }
                        }
                    }
                }
                if (_CurrentHost != null)
                    return _CurrentHost;
                var builder = new UriBuilder("http://" + GetPublicIpAddress() + ":5050");
                return builder.Uri;
            }
        }

        /// <summary>
        /// Set the domain and the proxy to make it available to clients (this information will be inserted in the QR code)
        /// </summary>
        /// <param name="uri"></param>
        public static void SetCurrentHost(Uri uri)
        {
            _CurrentHost = uri;
        }
        /// <summary>
        /// Set the domain and the proxy to make it available to clients (this information will be inserted in the QR code)
        /// </summary>
        /// <param name="context"></param>
        public static void SetCurrentHost(HttpContext context)
        {
            if (_CurrentHost == null)
                _CurrentHost = new Uri((context.Request.IsHttps ? "https://" : "http://") + context.Request.Host);
        }

        /// <summary>
        /// Get intranet IP
        /// </summary>
        /// <returns>Intranet IP</returns>
        public static IPAddress GetLocalIPAddress()
        {
            var localIp = GetMyIPs().Find(ip => ip.IsIntranet());
            return localIp;
        }

        private static IPAddress publicIp;
        /// <summary>
        /// Get public IP
        /// </summary>
        /// <returns>Public IP</returns>
        public static IPAddress GetPublicIpAddress()
        {
            if (publicIp != null)
                return publicIp;
            publicIp = GetMyIPs().Find(ip => !IsIntranet(ip) && ip.GetAddressBytes().Length == 4);
            if (publicIp != null)
                return publicIp;
            try
            {
                publicIp = IPAddress.Parse(new WebClient().DownloadString("https://ipinfo.io/ip"));
            }
            catch (Exception ex)
            {
                try
                {
                    publicIp = IPAddress.Parse(new WebClient().DownloadString("http://icanhazip.com").Replace("\\r\\n", "").Replace("\\n", "").Trim());
                }
                catch (Exception ex2)
                {
                }
            }
            return IPAddress.None;
        }
        /// <summary>
        /// Get all my IP
        /// </summary>
        /// <returns>List of IP</returns>
        public static List<IPAddress> GetMyIPs()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            return host.AddressList.ToList();
        }

        /// <summary>
        /// An extension method to determine if an IP address is internal, as specified in RFC1918
        /// </summary>
        /// <param name="toTest">The IP address that will be tested</param>
        /// <returns>Returns true if the IP is internal, false if it is external</returns>
        public static bool IsIntranet(this IPAddress toTest)
        {
            if (IPAddress.IsLoopback(toTest)) return true;
            if (toTest.ToString() == "::1") return false;
            var bytes = toTest.GetAddressBytes();
            if (bytes.Length == 4)
            {
                uint a(byte[] bts) { Array.Reverse(bts); return BitConverter.ToUInt32(bts, 0); }
                bool ir(uint ipReverse, byte[] start, byte[] end) { return (ipReverse >= a(start) && ipReverse <= a(end)); } // Check if is in range
                var ip = a(bytes);
                // IP for special use: https://en.wikipedia.org/wiki/Reserved_IP_addresses             
                if (ir(ip, new byte[] { 0, 0, 0, 0 }, new byte[] { 0, 255, 255, 255 })) return true;
                if (ir(ip, new byte[] { 10, 0, 0, 0 }, new byte[] { 10, 255, 255, 255 })) return true;
                if (ir(ip, new byte[] { 100, 64, 0, 0 }, new byte[] { 100, 127, 255, 255 })) return true;
                if (ir(ip, new byte[] { 127, 0, 0, 0 }, new byte[] { 127, 255, 255, 255 })) return true;
                if (ir(ip, new byte[] { 169, 254, 0, 0 }, new byte[] { 169, 254, 255, 255 })) return true;
                if (ir(ip, new byte[] { 172, 16, 0, 0 }, new byte[] { 172, 31, 255, 255 })) return true;
                if (ir(ip, new byte[] { 192, 0, 0, 0 }, new byte[] { 192, 0, 0, 255 })) return true;
                if (ir(ip, new byte[] { 192, 0, 2, 0 }, new byte[] { 192, 0, 2, 255 })) return true;
                if (ir(ip, new byte[] { 192, 88, 99, 0 }, new byte[] { 192, 88, 99, 255 })) return true;
                if (ir(ip, new byte[] { 192, 168, 0, 0 }, new byte[] { 192, 168, 255, 255 })) return true;
                if (ir(ip, new byte[] { 198, 18, 0, 0 }, new byte[] { 198, 19, 255, 255 })) return true;
                if (ir(ip, new byte[] { 198, 51, 100, 0 }, new byte[] { 198, 51, 100, 255 })) return true;
                if (ir(ip, new byte[] { 203, 0, 113, 0 }, new byte[] { 203, 0, 113, 255 })) return true;
                if (ir(ip, new byte[] { 224, 0, 0, 0 }, new byte[] { 239, 255, 255, 255 })) return true;
                if (ir(ip, new byte[] { 233, 252, 0, 0 }, new byte[] { 233, 252, 0, 255 })) return true;
                if (ir(ip, new byte[] { 240, 0, 0, 0 }, new byte[] { 255, 255, 255, 254 })) return true;
            }
            return false;
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

        public enum ReachableStatus
        {
            Reachable,
            WrongDNS,
            NotSetDNS,
            UnderFirewall,
            UnexpectedResponse,
            ConnectivityError,
            WrongHostName,
            ReachableButWrongEntryPoint,
        }

        public static ReachableStatus IsReachable()
        {
            UriBuilder ub;
            try
            {
                ub = new UriBuilder(CurrentHost) { Path = "proxyinfo", Query = "ping=true" };
            }
            catch (Exception)
            {
                return ReachableStatus.WrongHostName;
            }
            try
            {
                var e = Dns.GetHostEntry(ub.Host);
                if (e.AddressList.Length == 0)
                    return ReachableStatus.NotSetDNS;
                var ip = e.AddressList[0];
                ub.Host = ip.ToString();
                var pinger = new Ping();
                var reply = pinger.Send(ip);
                if (reply.Status != IPStatus.Success)
                    return ReachableStatus.UnderFirewall;
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create(ub.Uri);
                    request.Timeout = 2000;
                    request.ReadWriteTimeout = 2000;
                    var page = new StreamReader(((HttpWebResponse)request.GetResponse()).GetResponseStream()).ReadToEnd();
                    return (page == "ok") ? ReachableStatus.Reachable : ReachableStatus.UnexpectedResponse;
                }
                catch (Exception)
                {
                    var myIp = GetPublicIpAddress();
                    ub.Host = myIp.ToString();
                    var request = (HttpWebRequest)WebRequest.Create(ub.Uri);
                    request.Timeout = 1000;
                    request.ReadWriteTimeout = 1000;
                    var page = new StreamReader(((HttpWebResponse)request.GetResponse()).GetResponseStream()).ReadToEnd();
                    return (page == "ok") ? ReachableStatus.ReachableButWrongEntryPoint : ReachableStatus.UnexpectedResponse;
                }
            }
            catch (Exception)
            {
                return ReachableStatus.ConnectivityError;
            }
        }

        /// <summary>
        /// The latest self-diagnosis test result
        /// </summary>
        public static Exception LastSelfTestDataPostResult { get; private set; }
        /// <summary>
        /// Runs a self-diagnosis test to see if there are any problems using the proxy bees from the internet.
        /// </summary>
        /// <returns>Risultato del test di autodiagnosi</returns>
        public static async Task<Exception> SelfTestDataPostError()
        {
            try
            {
                LastSelfTestDataPostResult = null;
                if (!Communication.IsInitialized)
                    return new Exception("The proxy for the API was not initialized");

                var rnd = new Random();
                var rb = new byte[128];
                rnd.NextBytes(rb);
                var h = rb.ToHex();
                var ub = new UriBuilder(CurrentHost) { Path = "data", Query = "test=" + h };
                var ASPNETCORE_URLS = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
                if (!string.IsNullOrEmpty(ASPNETCORE_URLS))
                {
                    var urls = (ASPNETCORE_URLS.Split(';'));
                    if (urls.Length > 0)
                    {
                        var proxyPort = ub.Uri.Port;
                        var end = ":" + proxyPort;
                        var found = false;
                        foreach (var url in urls)
                        {
                            if (url.EndsWith(end))
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                            return new Exception("The endpoint for the proxy on port " + proxyPort + " has not been mapped");
                    }
                }


                var e = Dns.GetHostEntry(ub.Host);
                var ip = e.AddressList[0];
                ub.Host = ip.ToString();
                var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(3)
                };
                var cts = new CancellationTokenSource();
                cts.CancelAfter(client.Timeout.Milliseconds);
                using var response = await client.PostAsync(ub.Uri, null, cts.Token).ConfigureAwait(false);
                using var content = response.Content;
                var data = await content.ReadAsStringAsync();
                var dataBin = data.Base64ToBytes();
                var h2 = dataBin.ToHex();
                LastSelfTestDataPostResult = h == h2 ? null : new Exception("Response with unexpected data");
            }
            catch (Exception ex)
            {
                LastSelfTestDataPostResult = new Exception(ex.Message); // remove the stack trace
                Debug.WriteLine(ex);
            }
            return LastSelfTestDataPostResult;
        }
    }
}

