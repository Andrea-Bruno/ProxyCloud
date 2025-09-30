using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using EncryptedMessaging;

namespace ProxyAPISupport
{
    /// <summary>
    /// Handles communication with the server, manages context, keys, and connection events.
    /// </summary>
    public class CommunicationServer
    {
        private const bool _multipleChatModes = true;
        internal static Context Context;
        private readonly string PrivateKey;
        public readonly string PublicKey;
        public readonly ulong Id;
        /// <summary>
        /// Gets or sets the current instance of the <see cref="CommunicationServer"/>.
        /// </summary>
        public static CommunicationServer Current { get; set; }

        /// <summary>
        /// Initializes a new instance of the CommunicationServer class.
        /// Optionally blocks the calling thread until the connection to the server is established.
        /// </summary>
        /// <param name="privateKey">The private key for the server context.</param>
        /// <param name="entryPoint">The entry point for the server connection.</param>
        /// <param name="connectivity">Optional connectivity flag.</param>
        /// <param name="networkName">The network name (default: "mainnet").</param>
        /// <param name="WaitConnection">
        /// Optional semaphore. If provided, the constructor will block until the connection is established.
        /// </param>
        public CommunicationServer(string privateKey, string entryPoint, bool? connectivity = null, string networkName = "mainnet", bool? WaitConnection = true)
        {
            PrivateKey = privateKey;
            var platform = (int)Environment.OSVersion.Platform;
            var runtimePlatform = (platform == 4) || (platform == 6) || (platform == 128)
                ? Contact.RuntimePlatform.Unix
                : Contact.RuntimePlatform.Windows;

            Context = new Context(entryPoint, networkName, _multipleChatModes, PrivateKey, Modality.Server | Modality.SaveContacts, connectivity: connectivity);

            // This semaphore will be released when the connection is established
            var semaphore = WaitConnection == true ? new ManualResetEvent(false) : null;
            if (semaphore != null)
            {
                void OnRouterConnectionChange(bool connected)
                {
                    if (connected)
                    {
                        semaphore?.Set();
                        semaphore = null;
                    }
                }
                Context.OnRouterConnectionChange = OnRouterConnectionChange;
            }

            Context.OnContactEvent += OnContactEvent;
            Context.Contacts.OnContactReceived += OnContactReceived;
            PublicKey = Context.My.GetPublicKey();
            Id = Context.My.Id;
            Current = this;
            // If a semaphore is provided, wait until it is released (connection established)
            if (semaphore != null)
            {
                try
                {
                    // Wait until the connection is established
                    semaphore.WaitOne(30000);

                }
                catch (Exception)
                {
                    Debugger.Break(); // Proxy connection timeout to router
                }
            }
        }

        /// <summary>
        /// Event triggered when a contact connects to the server.
        /// </summary>
        /// <param name="contact">The contact that connected.</param>
        private void OnContactReceived(Contact contact)
        {
            if (contact.UserId != null)
            {
                // Communication.UserIdToChatId.AddPair((ulong)contact.UserId, contact.ChatId);
            }
        }

        /// <summary>
        /// Handles incoming messages and routes sub-application commands.
        /// </summary>
        /// <param name="message">The received message.</param>
        private static void OnContactEvent(Message message)
        {
            if (message.Type == MessageFormat.MessageType.SubApplicationCommandWithParameters || message.Type == MessageFormat.MessageType.SubApplicationCommandWithData)
            {
                ushort appId = default;
                ushort command = default;
                List<byte[]> parameters = default;
                byte[] data = default;
                if (message.Type == MessageFormat.MessageType.SubApplicationCommandWithParameters)
                    message.GetSubApplicationCommandWithParameters(out appId, out command, out parameters);
                if (message.Type == MessageFormat.MessageType.SubApplicationCommandWithData)
                    message.GetSubApplicationCommandWithData(out appId, out command, out data);
                if (appId == BitConverter.ToUInt16(Encoding.ASCII.GetBytes("proxy")))
                {
                    var answareToCommand = (Communication.Purpose)command;
                    Communication.OnCommand(message.Contact, answareToCommand, data, parameters);
                }
            }
        }
    }

}
