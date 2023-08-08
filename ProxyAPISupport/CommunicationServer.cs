using System;
using System.Collections.Generic;
using System.Text;
using EncryptedMessaging;

namespace ProxyAPISupport
{
    public class CommunicationServer
    {
        private const bool _multipleChatModes = true;
        //private readonly string _networkName;
        //private readonly string _entryPoint; // Used for release
        internal static Context Context;
        private readonly string PrivateKey;
        public readonly string PubblicKey;
        public readonly ulong Id;
        public static CommunicationServer Current;
        public CommunicationServer(string privateKey, string entryPoint, string networkName = "mainnet")
        {
            PrivateKey = privateKey;
            var platform = (int)Environment.OSVersion.Platform;
            var runtimePlatform = (platform == 4) || (platform == 6) || (platform == 128) ? Contact.RuntimePlatform.Unix : Contact.RuntimePlatform.Windows;

            Context = new Context(entryPoint, networkName, _multipleChatModes, PrivateKey, Modality.Server | Modality.SaveContacts);
            Context.OnContactEvent += OnContactEvent;
            Context.Contacts.OnContactReceived += OnContactReceived;
            PubblicKey = Context.My.GetPublicKey();
            Id = Context.My.Id;
            //// Assign incoming push notifications to the appropriate notification project
            //EncryptedMessaging.Cloud.ReceiveCloudCommands.OnPost.Add(EncryptedMessaging.Cloud.Subject.PushNotification, OnClientRequest);
            Current = this;
        }

        /// <summary>
        /// Event that is triggered when the contact connects to the browser
        /// </summary>
        /// <param name="contact">The contact that connected</param>
        private void OnContactReceived(Contact contact)
        {
            if (contact.UserId != null)
            {
                // Communication.UserIdToChatId.AddPair((ulong)contact.UserId, contact.ChatId);
            }
        }

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
