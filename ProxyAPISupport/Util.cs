using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.AspNetCore.Http;
using NBitcoin;

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
                    return "The local proxy has not started!" + "\r\n";
                }
                var host = CurrentHost;

                var RealTimeClients = "\r\n" + "REALTIME ClientID -> ConcurentRequests:" + "\r\n";
                lock (Communication.ConcurrentRequestForUser)
                {
                    Communication.ConcurrentRequestForUser.ToList().ForEach(x => RealTimeClients += x.Key + " -> " + x.Value + "\r\n");
                }
                var isReachable = IsReachable(out IPAddress myIP, out string isRearchableNote);
                return "Is reachable = " + (host == null ? "undefined" : "[" + isReachable.ToString()) + "] " + isRearchableNote + "\r\n"
                     + "IP address = " + myIP + "\r\n"
                     + "Router entry point = " + CommunicationServer.Context?.EntryPoint.Host + "\r\n"
                     + "Is connected to router = " + CommunicationServer.Context?.IsConnected + "\r\n"
                     + "Host = " + host + "\r\n"
                     + "ID = " + Communication.Server?.Id + "\r\n"
                     + "PubblicKey = " + Communication.Server?.PublicKey + "\r\n"
                     + "Clouds connected = " + CommunicationServer.Context?.Contacts.Count + "\r\n"
                     + "Clients paired = " + Communication.ClientIdToChatId?.Paired + "\r\n"
                     + "Request counter = " + Communication.RequestToDeviceCounter + "\r\n"
                     + "Answer counter = " + Communication.AnswerFromDeviceCounter + "\r\n"
                     + "Current concurent request = " + Communication.RequestConcurent + " (max " + Communication.MaxRequestConcurent + ")\r\n"
                     + "Last request saturaion = " + (Communication.LastRequestSaturation == default ? "never (ok)" : Communication.LastRequestSaturation.ToString("G")) + "\r\n"
                     + "Last client over request = " + (Communication.LastClientOverRequest == default ? "never (ok)" : Communication.LastClientOverRequest.ToString("G")) + "\r\n"
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
                    lock (_lock)
                    {
                        if (!CurrentHostFirstRead)
                        {
                            CurrentHostFirstRead = true;
#if DEBUG                            
                            if (!SpinWait.SpinUntil(() => _CurrentHost != null, 2000))
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
#endif
                        }
                    }
                }
                if (_CurrentHost != null)
                    return _CurrentHost;
                var ip = GetPublicIpAddress();
                if (ip == null || ip == IPAddress.None)
                    return null;
                var builder = new UriBuilder("http://" + ip + ":" + Communication.Port);
                return builder.Uri;
            }
        }

        private static object _lock = new object();

        /// <summary>
        /// Set the domain and the proxy to make it available to clients (this information will be inserted in the QR code)
        /// </summary>
        /// <param name="uri"></param>
        public static void SetCurrentHost(Uri uri)
        {
            SetHostAndReplaceIp(uri);
        }
        /// <summary>
        /// Set the domain and the proxy to make it available to clients (this information will be inserted in the QR code)
        /// </summary>
        /// <param name="context"></param>
        public static void SetCurrentHost(HttpContext context)
        {
            if (_CurrentHost == null)
                SetHostAndReplaceIp(new Uri((context.Request.IsHttps ? "https://" : "http://") + context.Request.Host));
        }

        /// <summary>
        /// If the Uri in IP format is a known domain name (the default one), replace it with the domain in order to have a dynamic entry point (that can change ip)
        /// </summary>
        /// <param name="uri"></param>
        private static void SetHostAndReplaceIp(Uri uri)
        {
            // Check if is IP
            if (IPAddress.TryParse(uri.Host, out var ip))
            {
                // Check if the IP corresponds to a known domain by default, in this case replace the Ip with the domain name in order to have a dynamic entry point (which can change ip)
                var newUri = new UriBuilder(uri)
                {
                    Host = IpToDomain("proxy.tc0.it", ip)
                };
                _CurrentHost = newUri.Uri;
            }
            else
            {
                _CurrentHost = uri;
            }
        }

        static private string IpToDomain(string domain, IPAddress ip)
        {
            try
            {
                var hostIp = Dns.GetHostAddresses(domain);
                if (hostIp.FirstOrDefault(x => x == ip) != null)
                {
                    return domain;
                }
            }
            catch (Exception)
            {
            }
            return ip.ToString();
        }

        /// <summary>
        /// Get Intranet IP
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
            catch (Exception)
            {
                try
                {
                    publicIp = IPAddress.Parse(new WebClient().DownloadString("http://icanhazip.com").Replace("\\r\\n", "").Replace("\\n", "").Trim());
                }
                catch (Exception)
                {
                }
            }
            return publicIp ?? IPAddress.None;
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
                if (ir(ip, [0, 0, 0, 0], [0, 255, 255, 255])) return true;
                if (ir(ip, [10, 0, 0, 0], [10, 255, 255, 255])) return true;
                if (ir(ip, [100, 64, 0, 0], [100, 127, 255, 255])) return true;
                if (ir(ip, [127, 0, 0, 0], [127, 255, 255, 255])) return true;
                if (ir(ip, [169, 254, 0, 0], [169, 254, 255, 255])) return true;
                if (ir(ip, [172, 16, 0, 0], [172, 31, 255, 255])) return true;
                if (ir(ip, [192, 0, 0, 0], [192, 0, 0, 255])) return true;
                if (ir(ip, [192, 0, 2, 0], [192, 0, 2, 255])) return true;
                if (ir(ip, [192, 88, 99, 0], [192, 88, 99, 255])) return true;
                if (ir(ip, [192, 168, 0, 0], [192, 168, 255, 255])) return true;
                if (ir(ip, [198, 18, 0, 0], [198, 19, 255, 255])) return true;
                if (ir(ip, [198, 51, 100, 0], [198, 51, 100, 255])) return true;
                if (ir(ip, [203, 0, 113, 0], [203, 0, 113, 255])) return true;
                if (ir(ip, [224, 0, 0, 0], [239, 255, 255, 255])) return true;
                if (ir(ip, [233, 252, 0, 0], [233, 252, 0, 255])) return true;
                if (ir(ip, [240, 0, 0, 0], [255, 255, 255, 254])) return true;
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
            WSLDoesNotAcceptRequestsFromInternet
        }

        private static ReachableStatus LastIsReachable;
        private static IPAddress LastMyIp;
        private static DateTime LastIsReachableTime;

        public static ReachableStatus IsReachable(out IPAddress myIp, out string description)
        {
            description = null;
            myIp = LastMyIp; // Default assignment
            ReachableStatus status = LastIsReachable; // Variable for the final return value
            if ((DateTime.UtcNow - LastIsReachableTime).TotalMinutes > 60)
            {
                LastIsReachableTime = DateTime.UtcNow;
                try
                {
                    myIp = GetPublicIpAddress();
                    LastMyIp = myIp;
                    var ub = new UriBuilder(CurrentHost) { Path = "proxyinfo", Query = "ping=true", Port = Communication.Port };
                    var hostIp = Dns.GetHostAddresses(ub.Host);
                    if (!hostIp.Contains(myIp))
                    {
                        status = ReachableStatus.WrongHostName;
                    }
                    else
                    {
                        var e = Dns.GetHostEntry(ub.Host);
                        if (e.AddressList.Length == 0)
                        {
                            status = ReachableStatus.NotSetDNS;
                        }
                        else
                        {
                            var ip = e.AddressList[0];
                            var pinger = new Ping();
                            var reply = pinger.Send(ip);

                            if (reply.Status != IPStatus.Success)
                            {
                                status = ReachableStatus.UnderFirewall;
                            }
                            else
                            {
                                try
                                {
                                    status = CheckUri(ub.Uri) ? ReachableStatus.Reachable : ReachableStatus.UnexpectedResponse;
                                }
                                catch (Exception)
                                {
                                    var isRunningOnWSL = File.Exists("/proc/sys/fs/binfmt_misc/WSLInterop");
                                    if (isRunningOnWSL)
                                    {
                                        status = ReachableStatus.WSLDoesNotAcceptRequestsFromInternet;
                                    }
                                    else
                                    {
                                        try
                                        {
                                            if (ub.Host != myIp.ToString())
                                            {
                                                ub.Host = myIp.ToString();
                                                status = CheckUri(ub.Uri) ? ReachableStatus.ReachableButWrongEntryPoint : ReachableStatus.UnexpectedResponse;
                                            }
                                            else
                                            {
                                                ub.Host = IPAddress.Loopback.ToString();
                                                status = CheckUri(ub.Uri) ? ReachableStatus.UnderFirewall : ReachableStatus.UnexpectedResponse;
                                            }
                                        }
                                        catch
                                        {
                                            status = ReachableStatus.UnexpectedResponse;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    description = ex.Message;
                    status = ReachableStatus.ConnectivityError;
                }
            }
            LastIsReachable = status; // Update the global status
            return status;
        }

        static private bool CheckUri(Uri uri)
        {
            // Create an HttpClient with a timeout of 1 second
            HttpClientHandler handler = new HttpClientHandler();
            using (HttpClient client = new HttpClient(handler))
            {
                client.Timeout = TimeSpan.FromSeconds(1);
                // Execute the HTTP GET request synchronously
                HttpResponseMessage response = client.GetAsync(uri, HttpCompletionOption.ResponseContentRead).GetAwaiter().GetResult();
                // Read the response content
                string content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                // Return true if the content equals "ok"
                return content == "ok";
            }
        }

        /// <summary>
        /// The latest self-diagnosis test result
        /// </summary>
        public static Exception LastSelfTestDataPostResult { get; private set; }
        /// <summary>
        /// Runs a self-diagnosis test to see if there are any problems using the proxy bees from the internet.
        /// It can throw an error indicating the problem you are having.
        /// </summary>
        public static void SelfTestDataPost()
        {
            try
            {
                LastSelfTestDataPostResult = null;
                if (!Communication.IsInitialized)
                    throw new Exception("The proxy for the API was not initialized");

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
                            throw new Exception("The endpoint for the proxy on port " + proxyPort + " has not been mapped");
                    }
                }
                if (!IPAddress.TryParse(ub.Host, out _))
                {
                    var e = Dns.GetHostEntry(ub.Host);
                    var ip = e.AddressList[0];
                    ub.Host = ip.ToString();
                }
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(ub.Uri.ToString());
                request.Timeout = 4000;
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                byte[] bytes = Array.Empty<byte>();
                request.ContentLength = bytes.Length;
                Stream requestStream = request.GetRequestStream();
                requestStream.Write(bytes, 0, bytes.Length);
                WebResponse response = request.GetResponse();
                Stream stream = response.GetResponseStream();
                StreamReader reader = new StreamReader(stream);
                var result = reader.ReadToEnd();
                var dataBin = result.Base64ToBytes();
                var h2 = dataBin.ToHex();
                LastSelfTestDataPostResult = string.Compare(h, h2, false) == 0 ? null : new Exception("Response with unexpected data");

                //var client = new HttpClient
                //{
                //    Timeout = TimeSpan.FromSeconds(3)
                //};
                //var cts = new CancellationTokenSource();
                //cts.CancelAfter(2000);
                //using var response = client.PostAsync(ub.Uri, null, cts.Token).Result;
                //using var content = response.Content;
                //var data = content.ReadAsStringAsync().Result;

                //var dataBin = data.Base64ToBytes();
                //var h2 = dataBin.ToHex();
                //LastSelfTestDataPostResult = h == h2 ? null : new Exception("Response with unexpected data");
            }
            catch (Exception ex)
            {
                LastSelfTestDataPostResult = new Exception(ex.Message); // remove the stack trace
                Debug.WriteLine(ex);
            }
            if (LastSelfTestDataPostResult != null)
                throw LastSelfTestDataPostResult;
        }
    }
}

