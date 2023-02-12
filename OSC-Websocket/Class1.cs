using WebSocketSharp.Server;
using Newtonsoft.Json;
using WebSocketSharp;

public class OSCWebSocketBehavior : WebSocketBehavior
{
    protected override void OnMessage(MessageEventArgs e)
    {
        // Deserialize the JSON string to an object
        var oscPacket = JsonConvert.DeserializeObject<dynamic>(e.Data);

        //TO DO: IMPLIMENT THIS PIECE OF CRAP 
        // THIS IS A PROBLEM FOR FUTURE ME
        // ...
    }
}