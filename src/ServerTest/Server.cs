using SuperSimpleTcp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ServerTest
{
    class Program
    {
        static string _ListenerIp;
        static int _ListenerPort;
        static bool _Ssl;
        static string _PfxFilename = null;
        static string _PfxPassword = null;
        static string _LastClientIpPort = null;
        static int _IdleClientTimeoutMs = 0;

        static SimpleTcpServer _Server;
        static bool _RunForever = true;

        static void Main(string[] args)
        { 
            _ListenerIp =    InputString("Listener IP   :", "127.0.0.1", false);
            _ListenerPort = InputInteger("Listener Port :", 9000, true, false); 
            _Ssl =          InputBoolean("Use SSL       :", false);
            if (_Ssl)
            {
                _PfxFilename = InputString("PFX Certificate File:", "simpletcp.pfx", false);
                _PfxPassword = InputString("PFX File Password:", "simpletcp", false);
                _Server = new SimpleTcpServer(_ListenerIp, _ListenerPort, _Ssl, _PfxFilename, _PfxPassword);
            }
            else
            {
                _Server = new SimpleTcpServer(_ListenerIp, _ListenerPort);
            }

            _Server.Events.ClientConnected += ClientConnected;
            _Server.Events.ClientDisconnected += ClientDisconnected;
            _Server.Events.DataReceived += DataReceived;
            _Server.Events.DataSent += DataSent;
            _Server.Keepalive.EnableTcpKeepAlives = true;
            _Server.Settings.IdleClientTimeoutMs = _IdleClientTimeoutMs; 
            _Server.Settings.MutuallyAuthenticate = false;
            _Server.Settings.AcceptInvalidCertificates = true;
            _Server.Logger = Logger;
            _Server.Start();

            Console.WriteLine("Server started");

            while (_RunForever)
            {
                string userInput = InputString("Command [? for help]:", null, false);
                switch (userInput)
                {
                    case "?":
                        Menu();
                        break;
                    case "q":
                    case "Q":
                        _RunForever = false;
                        break;
                    case "c":
                    case "C":
                    case "cls":
                        Console.Clear();
                        break;
                    case "list":
                        ListClients();
                        break;
                    case "send":
                        Send();
                        break;
                    case "sendasync":
                        SendAsync();
                        break;
                    case "remove":
                        RemoveClient();
                        break; 
                    case "dispose":
                        _Server.Dispose();
                        break;
                    case "stats":
                        Console.WriteLine(_Server.Statistics.ToString());
                        break;
                    case "stats reset":
                        _Server.Statistics.Reset();
                        break;
                    case "conn":
                        Console.WriteLine("Connections: " + _Server.Connections);
                        break;
                    case "max":
                        _Server.Settings.MaxConnections = InputInteger("Max connections:", 4096, true, false);
                        break;
                    case "start":
                        _Server.Start();
                        break;
                    case "stop":
                        _Server.Stop();
                        break;
                }
            }
        }

        static void ClientConnected(object sender, ConnectionEventArgs e)
        {
            _LastClientIpPort = e.IpPort;
            Console.WriteLine("[" + e.IpPort + "] client connected");
        }

        static void ClientDisconnected(object sender, ConnectionEventArgs e)
        {
            Console.WriteLine("[" + e.IpPort + "] client disconnected: " + e.Reason.ToString());
        }

        static void DataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine("[" + e.IpPort + "]: " + Encoding.UTF8.GetString(e.Data));
        }

        private static void DataSent(object sender, DataSentEventArgs e)
        {
            Console.WriteLine("[" + e.IpPort + "] sent " + e.BytesSent + " bytes");
        }

        static void Menu()
        {
            Console.WriteLine("Available commands:");
            Console.WriteLine(" ?             Help, this menu");
            Console.WriteLine(" q             Quit");
            Console.WriteLine(" cls           Clear the screen");
            Console.WriteLine(" start         Start listening for connections (listening: " + (_Server != null ? _Server.IsListening.ToString() : "false") + ")");
            Console.WriteLine(" stop          Stop listening for connections  (listening: " + (_Server != null ? _Server.IsListening.ToString() : "false") + ")");
            Console.WriteLine(" list          List connected clients");
            Console.WriteLine(" send          Send a message to a client");
            Console.WriteLine(" sendasync     Send a message to a client asynchronously");
            Console.WriteLine(" remove        Disconnect client");
            Console.WriteLine(" dispose       Dispose of the server");
            Console.WriteLine(" stats         Display server statistics");
            Console.WriteLine(" stats reset   Reset server statistics");
            Console.WriteLine(" conn          Show connection count");
            Console.WriteLine(" max           Set max connections (currently " + _Server.Settings.MaxConnections + ")");
            Console.WriteLine("");
        }

        static void ListClients()
        {
            List<string> clients = _Server.GetClients().ToList();
            if (clients != null && clients.Count > 0)
            {
                foreach (string curr in clients) Console.WriteLine(curr);
            }
            else
            {
                Console.WriteLine("None");
            }
        }

        static void Send()
        {
            string ipPort = InputString("Client IP:port:", _LastClientIpPort, true);
            if (!String.IsNullOrEmpty(ipPort))
            {
                string data = InputString("Data:", "Hello!", true);
                if (!String.IsNullOrEmpty(data))
                {
                    _Server.Send(ipPort, Encoding.UTF8.GetBytes(data));
                }
            }
        }

        static void SendAsync()
        {
            string ipPort = InputString("Client IP:port:", _LastClientIpPort, true);
            if (!String.IsNullOrEmpty(ipPort))
            {
                string data = InputString("Data:", "Hello!", true);
                if (!String.IsNullOrEmpty(data))
                {
                    _Server.SendAsync(ipPort, Encoding.UTF8.GetBytes(data)).Wait();
                }
            }
        }

        static void RemoveClient()
        {
            string ipPort = InputString("Client IP:port:", _LastClientIpPort, true);
            if (!String.IsNullOrEmpty(ipPort))
            {
                _Server.DisconnectClient(ipPort);
            }
        }

        static void Logger(string msg)
        {
            Console.WriteLine(msg);
        }

        static bool InputBoolean(string question, bool yesDefault)
        {
            Console.Write(question);

            if (yesDefault) Console.Write(" [Y/n]? ");
            else Console.Write(" [y/N]? ");

            string userInput = Console.ReadLine();

            if (String.IsNullOrEmpty(userInput))
            {
                if (yesDefault) return true;
                return false;
            }

            userInput = userInput.ToLower();

            if (yesDefault)
            {
                if (
                    (String.Compare(userInput, "n") == 0)
                    || (String.Compare(userInput, "no") == 0)
                   )
                {
                    return false;
                }

                return true;
            }
            else
            {
                if (
                    (String.Compare(userInput, "y") == 0)
                    || (String.Compare(userInput, "yes") == 0)
                   )
                {
                    return true;
                }

                return false;
            }
        }

        static string InputString(string question, string defaultAnswer, bool allowNull)
        {
            while (true)
            {
                Console.Write(question);

                if (!String.IsNullOrEmpty(defaultAnswer))
                {
                    Console.Write(" [" + defaultAnswer + "]");
                }

                Console.Write(" ");

                string userInput = Console.ReadLine();

                if (String.IsNullOrEmpty(userInput))
                {
                    if (!String.IsNullOrEmpty(defaultAnswer)) return defaultAnswer;
                    if (allowNull) return null;
                    else continue;
                }

                return userInput;
            }
        }

        static List<string> InputStringList(string question, bool allowEmpty)
        {
            List<string> ret = new List<string>();

            while (true)
            {
                Console.Write(question);

                Console.Write(" ");

                string userInput = Console.ReadLine();

                if (String.IsNullOrEmpty(userInput))
                {
                    if (ret.Count < 1 && !allowEmpty) continue;
                    return ret;
                }

                ret.Add(userInput);
            }
        }

        static int InputInteger(string question, int defaultAnswer, bool positiveOnly, bool allowZero)
        {
            while (true)
            {
                Console.Write(question);
                Console.Write(" [" + defaultAnswer + "] ");

                string userInput = Console.ReadLine();

                if (String.IsNullOrEmpty(userInput))
                {
                    return defaultAnswer;
                }

                int ret = 0;
                if (!Int32.TryParse(userInput, out ret))
                {
                    Console.WriteLine("Please enter a valid integer.");
                    continue;
                }

                if (ret == 0)
                {
                    if (allowZero)
                    {
                        return 0;
                    }
                }

                if (ret < 0)
                {
                    if (positiveOnly)
                    {
                        Console.WriteLine("Please enter a value greater than zero.");
                        continue;
                    }
                }

                return ret;
            }
        }
    }
}
