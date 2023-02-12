using WebSocketSharp.Server;
using Newtonsoft.Json;
using WebSocketSharp;
using System.Net.Sockets;
using System.Text;

public class OSCWebSocketBehavior : WebSocketBehavior
{
    TcpClient oscTcpClient;
    protected override void OnMessage(MessageEventArgs e)
    {
        base.OnMessage(e);
        var json = e.Data;

        // Convert the JSON string to an OSC packet
        var packet = JsonConvert.DeserializeObject<dynamic>(json);

        // Write the OSC packet to the OSC over TCP client
        var stream = oscTcpClient.GetStream();
        WriteOscPacket(stream, packet);
    }

    private static void WriteOscPacket(NetworkStream stream, dynamic packet)
    {
        // Write the OSC address pattern
        WriteOscString(stream, packet.Address);

        // Write the OSC type tag string
        WriteOscString(stream, packet.TypeTag);

        // Write the OSC arguments based on the type tag string
        byte[] data = new byte[1024];
        int offset = 0;
        WriteOscArguments(ref offset, data, packet.Arguments);
        stream.Write(data, 0, offset);
    }

    private static void WriteOscArguments(ref int offset, byte[] data, List<object> arguments)
    {
        foreach (var argument in arguments)
        {
            if (argument is int)
            {
                data[offset++] = (byte)'i';
                byte[] intBytes = BitConverter.GetBytes((int)argument);
                intBytes.CopyTo(data, offset);
                offset += intBytes.Length;
            }
            else if (argument is float)
            {
                data[offset++] = (byte)'f';
                byte[] floatBytes = BitConverter.GetBytes((float)argument);
                floatBytes.CopyTo(data, offset);
                offset += floatBytes.Length;
            }
            else if (argument is string)
            {
                data[offset++] = (byte)'s';
                byte[] stringBytes = Encoding.UTF8.GetBytes((string)argument);
                data[offset++] = (byte)(stringBytes.Length >> 8);
                data[offset++] = (byte)stringBytes.Length;
                stringBytes.CopyTo(data, offset);
                offset += stringBytes.Length;
            }
        }
    }

    private static void WriteOscString(NetworkStream stream, string s)
    {
        byte[] stringBytes = Encoding.UTF8.GetBytes(s);
        byte[] stringLengthBytes = BitConverter.GetBytes((int)stringBytes.Length);
        stream.Write(stringLengthBytes, 0, stringLengthBytes.Length);
        stream.Write(stringBytes, 0, stringBytes.Length);
    }
    private static string ReadOscString(NetworkStream stream)
    {
        byte[] sizeBytes = new byte[4];
        int bytesRead = 0;
        while (bytesRead < 4)
        {
            bytesRead += stream.Read(sizeBytes, bytesRead, 4 - bytesRead);
        }
        int size = (sizeBytes[0] << 24) | (sizeBytes[1] << 16) | (sizeBytes[2] << 8) | sizeBytes[3];

        byte[] stringBytes = new byte[size];
        bytesRead = 0;
        while (bytesRead < size)
        {
            bytesRead += stream.Read(stringBytes, bytesRead, size - bytesRead);
        }

        return System.Text.Encoding.ASCII.GetString(stringBytes);
    }
}