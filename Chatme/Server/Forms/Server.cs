﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using Server.Objects;

namespace Server.Forms
{
    public partial class Server : DevExpress.XtraEditors.XtraForm
    {
        #region Instance variables
        public static readonly IPAddress ip = IPAddress.Parse("127.0.0.1");         // IP
        public static readonly int port = 2019;                                     // Port
        private TcpListener listener;                                               // TCP/IP protocol for Server
        private List<Client> clients;                                               // List connected Clients
        private Socket socket;                                                      // Socket
        private Thread service;                                                     // Thread Service run when Client has connected
        private Thread listen;                                                      // Thread Listen run when Server has started
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor Server
        /// </summary>
        /// <param name="administrator">Administrator manage Server</param>
        public Server(string administrator)
        {
            InitializeComponent();
            labelAdministrator.Text += administrator;

            // Create List connected Clients
            clients = new List<Client>();

            // Create and run Thread Lister Clients
            listen = new Thread(new ThreadStart(Listen));
            listen.Start();
        }
        #endregion

        #region Methods
        /// <summary>
        /// Lister Clients
        /// </summary>
        private void Listen()
        {
            // Create and start listening Clients by TCP/IP protocol
            listener = new TcpListener(ip, port);
            listener.Start();

            while (true)
            {
                try
                {
                    // Accept and allocate socket for Client when requesting
                    socket = listener.AcceptSocket();

                    // Create and run Thread Service for connected Client
                    service = new Thread(new ThreadStart(Service));
                    service.Start();
                }
                catch { }
            }
        }

        /// <summary>
        /// Service for connected Client (Recievice message form Client)
        /// </summary>
        private void Service()
        {
            Socket clientSocket = socket;
            bool keepConnect = true;

            while (keepConnect)
            {
                // Receive message
                Byte[] buffer = new Byte[1024];
                clientSocket.Receive(buffer);
                string command = Encoding.ASCII.GetString(buffer);

                // Analyze message
                string[] tokens = command.Trim('\0').Split(new Char[] { '|' });
                if (tokens[0] == "connect")
                {
                    // Send message "join" to Clients in "List connted Clients"
                    clients.ForEach(client => Send(client, "join|" + tokens[1]));

                    // Create and add new Client in "List connted Clients"
                    Client newConnectedClient = new Client(tokens[1], clientSocket, service, clientSocket.RemoteEndPoint);
                    clients.Add(newConnectedClient);

                    // Send message "list" to that Client about "List connted Clients"
                    Send(newConnectedClient, "list|" + ConnectedClients() + "\r\n");

                    // Show in listboxConnectedClient
                    listboxConnectedClients.Items.Add(newConnectedClient);
                }
                if (tokens[0] == "logout")
                {
                    clients.ForEach(client =>
                    {
                        if (client.Name == tokens[1])
                        {
                            listboxConnectedClients.Items.Remove(client);
                            clients.Remove(client);
                        }
                        else Send(client, command);
                    });
                    keepConnect = false;
                    clientSocket.Close();
                }
            }
        }

        /// <summary>
        /// Send message to connected Client
        /// </summary>
        /// <param name="client">Client</param>
        /// <param name="message">Message</param>
        private void Send(Client client, string message)
        {
            try
            {
                byte[] buffer = Encoding.ASCII.GetBytes(message.ToCharArray());
                client.Socket.Send(buffer, buffer.Length, 0);
            }
            catch
            {
                client.Socket.Close();
                client.Thread.Abort();
                clients.Remove(client);
                listboxConnectedClients.Items.Remove(client);
            }
        }

        /// <summary>
        /// Get string of Connected Clients List: "client 1|client 2|...|client n"
        /// </summary>
        /// <returns></returns>
        private string ConnectedClients()
        {
            string result = "";
            clients.ForEach(client => result += (client.Name + "|"));
            return result.Trim(new char[] { '|' });
        }

        /// <summary>
        /// Override OnClosed method
        /// </summary>
        /// <param name="e"></param>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                clients.ForEach(client =>
                {
                    Send(client, "close|");
                    client.Socket.Close();
                    client.Thread.Abort();
                });
                socket.Close();
                service.Abort();
                listener.Stop();
                listen.Abort();

            }
            catch { }
            base.OnClosed(e);
        }
        #endregion
    }
}