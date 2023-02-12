using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace ConsoleApp
{
    class Program
    {
        static WebSocketServer wssv;
        static void Main(string[] args)
        {
            // Start a WebSocket server
            wssv = new WebSocketServer("ws://localhost:8080");
            wssv.AddWebSocketService<OSCWebSocketBehavior>("/osc");
            wssv.Start();
            Console.WriteLine("WebSocket server started on ws://localhost:8080/osc");

            // Start an OSC over TCP server
            var oscTcpListener = new TcpListener(IPAddress.Any, 8000);
            oscTcpListener.Start();
            Console.WriteLine("OSC over TCP listener started on TCP 0.0.0.0:8000");
            var oscTcpClient = new TcpClient();

            // Start listening for incoming connections
            var listenerTask = new Task(() =>
            {
                while (true)
                {
                    // Accept incoming connections
                    var client = oscTcpListener.AcceptTcpClient();
                    Console.WriteLine("Accepted incoming OSC over TCP connection");

                    // Start processing the incoming OSC packets
                    var clientTask = new Task(() =>
                    {
                        using (var stream = client.GetStream())
                        {
                            while (client.Connected)
                            {
                                // Read the OSC packet from the stream
                                var packet = ReadOscPacket(stream);
                                if (packet == null)
                                {
                                    continue;
                                }

                                // Convert the OSC packet to a JSON string
                                var json = JsonConvert.SerializeObject(packet);

                                // Broadcast the JSON string to all WebSocket clients
                                wssv.WebSocketServices["/osc"].Sessions.Broadcast(json);
                            }
                        }
                    });
                    clientTask.Start();
                }
            });
            listenerTask.Start();

            Console.WriteLine("Press Enter to stop the servers.");
            Console.ReadLine();

            // Stop the servers
            oscTcpListener.Stop();
            wssv.Stop();
        }

        private static object ReadOscPacket(NetworkStream stream)
        {
            // Read the OSC address pattern
            var address = ReadOscString(stream);
            if (address == null)
            {
                return null;
            }

            // Read the OSC type tag string
            var typeTag = ReadOscString(stream);
            if (typeTag == null)
            {
                return null;
            }

            // Read the OSC arguments based on the type tag string
            byte[] data = GetDataFromStream(stream);
            int offset = 0;
            var arguments = ReadOscArguments(ref offset, data);
            if (arguments == null)
            {
                return null;
            }

            // Return the OSC message as an object
            return new
            {
                Address = address,
                TypeTag = typeTag,
                Arguments = arguments
            };
        }
        private static byte[] GetDataFromStream(NetworkStream stream)
        {
            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }
        private static List<object> ReadOscArguments(ref int offset, byte[] data)
        {
            List<object> arguments = new List<object>();
            while (offset < data.Length)
            {
                switch (data[offset])
                {
                    case (byte)'i':
                        int intValue = BitConverter.ToInt32(data, offset + 1);
                        arguments.Add(intValue);
                        offset += sizeof(int) + 1;
                        break;
                    case (byte)'f':
                        float floatValue = BitConverter.ToSingle(data, offset + 1);
                        arguments.Add(floatValue);
                        offset += sizeof(float) + 1;
                        break;
                    case (byte)'s':
                        int stringLength = (data[offset + 1] << 8) + data[offset + 2];
                        string stringValue = Encoding.UTF8.GetString(data, offset + 3, stringLength);
                        arguments.Add(stringValue);
                        offset += stringLength + 3;
                        break;
                    default:
                        throw new Exception("Invalid argument type");
                }
            }
            return arguments;
        }

        private static string ReadOscString(NetworkStream stream)
        {
            var lengthBytes = new byte[4];
            stream.Read(lengthBytes, 0, 4);

            int length = BitConverter.ToInt32(lengthBytes, 0);
            var stringBytes = new byte[length];
            stream.Read(stringBytes, 0, length);

            return Encoding.UTF8.GetString(stringBytes);
        }
        private static void ProcessOscPacket(NetworkStream stream)
        {
            // Read the OSC address pattern
            var address = ReadOscString(stream);

            // Read the OSC type tag string
            var typeTag = ReadOscString(stream);

            // Read the OSC arguments
            var arguments = new List<object>();
            for (int i = 1; i < typeTag.Length; i++)
            {
                switch (typeTag[i])
                {
                    case 'i':
                        var intBytes = new byte[4];
                        stream.Read(intBytes, 0, 4);
                        arguments.Add(BitConverter.ToInt32(intBytes, 0));
                        break;
                    case 'f':
                        var floatBytes = new byte[4];
                        stream.Read(floatBytes, 0, 4);
                        arguments.Add(BitConverter.ToSingle(floatBytes, 0));
                        break;
                    case 's':
                        arguments.Add(ReadOscString(stream));
                        break;
                }
            }

            // Do something with the OSC packet
            Console.WriteLine("Received OSC packet: " + address + " " + string.Join(" ", arguments));
        }
        private static void ListenForOscPackets()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 8000);
            listener.Start();
            Console.WriteLine("Listening for OSC packets on TCP port 8000");

            while (true)
            {
                var client = listener.AcceptTcpClient();
                Console.WriteLine("Accepted connection from " + client.Client.RemoteEndPoint);

                var stream = client.GetStream();
                ProcessOscPacket(stream);

                client.Close();
            }
        }
    }
}

class OSCWebSocketBehavior : WebSocketBehavior
{
    protected override void OnOpen()
    {
        Console.WriteLine("A WebSocket client connected.");
    }

    protected override void OnClose(CloseEventArgs e)
    {
        Console.WriteLine("A WebSocket client disconnected.");
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        Console.WriteLine("A WebSocket client sent a message: " + e.Data);
        Send(e.Data);
    }
}