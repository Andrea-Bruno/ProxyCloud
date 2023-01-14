using EncryptedMessaging;
using System.Text;

namespace ProxyCloud
{
    public static class Communication
    {
        /// <summary>
        /// Purpose of the message (sending data via proxy, or other)
        /// </summary>
        public enum Purpose : ushort // 2 byte - the names must start with Get or Set
        {
            Forwarding,
            ForwardingWithEncryptRsa256,
            GetProxyAddress,
            /// <summary>
            /// Parameter used only for communication between browser and proxy to read any push messages sent by the device
            /// </summary>
            GetPushNotifications,
            /// <summary>
            /// Used to send the public key and setting from the browser/client to the device (server). Normally the public key must be scanned by the QR code in the client.
            /// The public key is not encrypted because the exchange of cryptographic keys has not yet taken place.
            /// </summary>
            SetClient,
        }

        internal static PairedTable UserIdToChatId = new PairedTable("UserIdToChatId");
        internal static PairedTable ClientIdToChatId = new PairedTable("ClientIdToChatId");

        /// <summary>
        /// Send commands from the web server to the device (SmartPhone, tablet, etc.)
        /// </summary>
        /// <param name="purpose">Purpose of the message (sending data via proxy, or other)</param>
        /// <param name="directlyWithoutSpooler"></param>
        /// <param name="encrypted"></param>
        /// <param name="values"></param>
        public static void SendCommand(Contact toContact, ulong fromClientId, Purpose purpose, bool directlyWithoutSpooler = false, bool encrypted = true, params byte[][] values)
        {
            byte[][] newValues = new byte[values.Length + 1][];
            Array.Copy(values, 0, newValues, 1, values.Length);
            newValues[0] = BitConverter.GetBytes(fromClientId);
            CommunicationServer.Context.Messaging.SendCommandToSubApplication(toContact, BitConverter.ToUInt16(Encoding.ASCII.GetBytes("proxy"), 0), (ushort)purpose, directlyWithoutSpooler, encrypted, newValues);
        }

        /// <summary>
        /// Send commands from the web server to the device (SmartPhone, tablet, etc.)
        /// </summary>
        /// <param name="purpose">Purpose of the message (sending data via proxy, or other)</param>
        /// <param name="directlyWithoutSpooler"></param>
        /// <param name="encrypted"></param>
        /// <param name="data"></param>
        public static void SendCommand(Contact toContact, ulong fromClientId, Purpose purpose, bool directlyWithoutSpooler = false, bool encrypted = true, byte[] data = null)
        {
            data ??= Array.Empty<byte>();
            CommunicationServer.Context.Messaging.SendCommandToSubApplication(toContact, BitConverter.ToUInt16(Encoding.ASCII.GetBytes("proxy"), 0), (ushort)purpose, directlyWithoutSpooler, encrypted, BitConverter.GetBytes(fromClientId), data);
        }

        public static void OnCommand(Contact contact, Purpose ResponseToPurpose, byte[] data, List<byte[]> parameters)
        {
            // All messages destined for the web application are in here
            switch (ResponseToPurpose)
            {
                case Purpose.GetProxyAddress:
                    SendCommand(contact, 0, ResponseToPurpose, true, true, Encoding.ASCII.GetBytes(MapEndpoints.CurrentHost ?? ""));
                    break;
                case Purpose.ForwardingWithEncryptRsa256: // Paired!
                    {
                        // we do the Pair with the hash of the public key to not let the server know anything about the keys used (amuento security and privacy)
                        if (contact.UserId != null)
                        {
                            var clientId = BitConverter.ToUInt64(parameters[0]);
                            data = parameters[1];
                            ClientIdToChatId.AddPair(clientId, contact.ChatId);
                        }
                    }
                    break;
                default:
                    break;
            }
            var response = new CommandForClient()
            {
                Purpose = ResponseToPurpose,
                EncriptedData = data,
            };
            if (contact.Session.TryGetValue("semaphore", out object semaphoreObject))
            {
                contact.Session.Remove("semaphore");
                contact.Session["response"] = response;
                var semaphore = semaphoreObject as SemaphoreSlim;
                if (semaphore.CurrentCount == 0)
                    semaphore.Release();
            }
            else if (contact.Session.TryGetValue("pushNotifications", out object pushNotificationsObject))
            {
                var pushNotifications = pushNotificationsObject as List<CommandForClient>;
                pushNotifications.Add(response);
            }
            else
            {
                var pushNotifications = new List<CommandForClient>();
                contact.Session["pushNotifications"] = pushNotifications;
                pushNotifications.Add(response);
            }
        }

        /// <summary>
        /// Send a command to the device 
        /// </summary>
        /// <param name="purpose">Purpose of the message (sending data via proxy, or other)</param>
        /// <param name="response">The response that the device (smartphone, tablet, etc.) gives to the command sent to it</param>
        /// <returns>True if the response arrives within the timeout, otherwise false</returns>
        public static bool RequestToDevice(ulong chatId, ulong fromClientId, Purpose purpose, byte[]? data, out CommandForClient? response, string? ip = null, string? userAgent = null)
        {
            Contact contact = CommunicationServer.Context.Contacts.GetContact(chatId);
            if (contact != null)
            {
                lock (contact)
                {
                    if (contact.Session.ContainsKey("response"))
                        contact.Session.Remove("response");
                    if (ip != null && userAgent != null)
                        SendCommand(contact, fromClientId, purpose, true, true, data, Encoding.ASCII.GetBytes(ip), Encoding.ASCII.GetBytes(userAgent));
                    else
                        SendCommand(contact, fromClientId, purpose, true, true, data);
                    if (contact.Session.ContainsKey("semaphore"))
                        contact.Session.Remove("semaphore");
                    var semaphore = new SemaphoreSlim(0, 1);
                    contact.Session.Add("semaphore", semaphore);
                    var limitMbps = 0.01; // minimun neetwork speed
                    var mb = (data == null ? 0 : data.Length) / (double)1000000;
                    var sec = mb / limitMbps;
                    semaphore.Wait(10000 + Convert.ToInt32(sec * 1000)); // Wait for a response with a timeout of 10000 ms + time the time it takes to send data at the limitMbps
                    if (contact.Session.TryGetValue("response", out object respondeObject))
                    {
                        response = (CommandForClient)respondeObject;
                        if (response != null)
                        {
                            contact.Session.Remove("response");
                            return true;
                        }
                    }
                }
            }
            response = null;
            return false;
        }

        static public bool GetPushNotification(ulong clientId, out CommandForClient? pushNotification)
        {
            var chatId = ClientIdToChatId.GetCorrespondingId(clientId);
            if (chatId != null)
            {
                Contact contact = CommunicationServer.Context.Contacts.GetContact((ulong)chatId);
                if (contact != null && contact.Session.TryGetValue("pushNotifications", out object pushNotificationsObject))
                {
                    var pushNotifications = pushNotificationsObject as List<CommandForClient>;
                    lock (pushNotifications)
                    {
                        if (pushNotifications.Count > 0)
                        {
                            pushNotification = pushNotifications[0];
                            pushNotifications.Remove(pushNotification);
                            return true;
                        }
                    }
                }
            }
            pushNotification = null;
            return false;
        }
        /// <summary>
        /// It is a command intended for the client (the browser). The command could be either received as a response to a client request, or sent by the device as an action to be performed in the browser with a push notification.
        /// </summary>
        public class CommandForClient
        {
            /// <summary>
            /// Purpose of the message (sending data via proxy, or other)
            /// </summary>
            public Purpose Purpose;
            /// <summary>
            /// Encripted data that is part of the command. Once decrypted the data contains a command for the clinet (browser) and parameters that vary according to the command
            /// </summary>
            public byte[] EncriptedData;
        }

    }

}
