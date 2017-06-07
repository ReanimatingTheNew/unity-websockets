using UnityEngine;
using System.Collections.Generic;
using MiniJSON;

namespace WebServices
{
    public class DefaultWebSocketProcessor : IWebSocketProcessor
    {
        public void OnConnected(WebSocket ws)
        {
            var dic = new Dictionary<string, object>();
            dic.Add("time", System.DateTime.Now.ToString());
            dic.Add("message", "hello");
            var json = Json.Serialize(dic);
            ws.WriteString(json);
        }

        public void OnMessage(WebSocket ws, string msg)
        {
            Debug.Log(msg);
        }

        public void OnDebugMessage(WebSocket ws, string msg, bool isError)
        {
            Debug.Log(msg);
        }
    }

    public class EchoWebSocket : WebSocket
    {
        public EchoWebSocket() : base()
        {
            // Update this with your IP:Port
            base.webSocketIP = "ws://localhost:3001";
            base.processor = new DefaultWebSocketProcessor();
        }
    }

}