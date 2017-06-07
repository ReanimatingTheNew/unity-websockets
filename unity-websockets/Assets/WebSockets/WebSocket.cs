using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;

#if WINDOWS_UWP
    using Windows.Foundation;
    using Windows.Networking.Sockets;
    using Windows.Security.Cryptography.Certificates;
    using Windows.Storage.Streams;
    using System.Threading.Tasks;
#endif

namespace WebServices
{
    public interface IWebSocketProcessor
    {
        void OnConnected(WebSocket ws);
        void OnMessage(WebSocket ws, string msg);
        void OnDebugMessage(WebSocket ws, string msg, bool isError);
    }

    public class WebSocket : MonoBehaviour
    {
        private static string TAG = "WebServices.WebSocket";
        public bool IsConnected { get; internal set; }

        protected IWebSocketProcessor processor;
        protected string webSocketIP;

#if WINDOWS_UWP
        private MessageWebSocket messageWebSocket;
        private DataWriter messageWriter;
        private bool busy;

        void Start()
        {
            IsConnected = false;
            ConnectAsync();
        }

        public void WriteString(string message)
        {
            if (messageWebSocket == null) return;
            Task.Run(async () =>
            {
                await EmitEvent(messageWebSocket, message);
            });
        }

        private async Task EmitEvent(MessageWebSocket webSocket, string message)
        {
            var dic = Json.Deserialize("{\"time\":\"\",\"entity\":\"\",\"message\":\"\"}") as Dictionary<string, object>;
            dic["time"]    = System.DateTime.Now.ToString();
            dic["entity"]  = this.gameObject.name;
            dic["message"] = message;

            var json = Json.Serialize(dic);

            messageWriter.WriteString("42[\"hololens\"," + json + "]");
            try
            {
                await messageWriter.StoreAsync();
            }
            catch (Exception ex)
            {
                Debug.Log(ex.Message);
            }
        }

        private void SetBusy(bool value)
        {
            busy = value;
        }
   
        private async Task ConnectAsync()
        {
            SetBusy(true);

            messageWebSocket = new MessageWebSocket();
            messageWebSocket.Control.MessageType = SocketMessageType.Utf8;
 
            // Add the MessageReceived event handler.
            messageWebSocket.MessageReceived += OnMessageReceived;

            // Add the Closed event handler.
            messageWebSocket.Closed += OnClosed;

            Uri server = new Uri(webSocketIP);
            try
            {
                await messageWebSocket.ConnectAsync(server);
            }
            catch (Exception ex)
            {
                // Error happened during connect operation.
                messageWebSocket.Dispose();
                messageWebSocket = null;
  
                // show error dialog!
                processor.OnDebugMessage(this, ex.Message, true);

                SetBusy(false);
                return;
            }

            // The default DataWriter encoding is Utf8.
            messageWriter = new DataWriter(messageWebSocket.OutputStream);

            processor.OnDebugMessage(this, "Connection established", false);

            connected = true;
            SetBusy(false);
        }
  
        private void OnMessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            try
            {
                DataReader reader = args.GetDataReader();
                reader.UnicodeEncoding = UnicodeEncoding.Utf8;
                string msg = reader.ReadString(reader.UnconsumedBufferLength);
                processor.OnMessage(this, msg);
            }
            catch (Exception ex)
            {
                Debug.Log(ex.Message);
            }
        }
 
        private void OnClosed(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            if (messageWebSocket == sender)
            {
                CloseSocket();
            }
            connected = false;
        } 

        private void CloseSocket()
        {
            if (messageWebSocket != null)
            {
                try
                {
                    messageWebSocket.Close(1000, "Closed due to user request.");
                }
                catch (Exception ex)
                {
                    Debug.Log(ex.Message);
                }
                messageWebSocket = null;
            }
        }
#else
        // List of messages queue for a sending and processing
        private Queue<string> mOutQueue = new Queue<string>();
        private Queue<byte[]> mInQueue = new Queue<byte[]>();

        private WebSocketSharp.WebSocket ws;
        private string wsError = null;

        private IEnumerator Connect()
        {
            ws = new WebSocketSharp.WebSocket(new Uri(webSocketIP).ToString());

            ws.OnClose += (sender, e) => 
            {
                Debug.Log("client disconnected");
            };

            ws.OnMessage += (sender, e) =>
            {
                lock (mInQueue) { mInQueue.Enqueue(e.RawData); }
            };

            ws.OnOpen += (sender, e) =>
            {
                IsConnected = true;
                processor.OnConnected(this);
            };
            
            ws.OnError += (sender, e) => wsError = e.Message;
            ws.ConnectAsync();

            while (!IsConnected && wsError == null)
                yield return 0;
        }

        private byte[] Recv()
        {
            if (mInQueue.Count == 0)
                return null;
            return mInQueue.Dequeue();
        }

        private string RecvString()
        {
            byte[] retval = Recv();
            if (retval == null)
                return null;
            return Encoding.UTF8.GetString(retval);
        }

        private IEnumerator StartWebSocket()
        {
            yield return StartCoroutine(Connect());

            processor.OnDebugMessage(this, "Connection established", false);

            while (true)
            {
                lock (mOutQueue)
                {
                    while (mOutQueue.Count > 0)
                    {
                        Debug.Log("asd");
                        ws.Send(Encoding.UTF8.GetBytes(mOutQueue.Dequeue()));
                    }
                }

                string reply = RecvString();
                if (reply != null)
                {
                    processor.OnMessage(this, reply);
                }

                if (wsError != null)
                {
                    processor.OnDebugMessage(this, wsError, true);
                    break;
                }
                yield return 0;
            }

            ws.Close();
        }

        private void Start()
        {
            StartCoroutine(StartWebSocket());
        }

        public void WriteString(string message)
        {
            lock (mOutQueue)
            {
                mOutQueue.Enqueue(message);
            }
        }
#endif
    }
}