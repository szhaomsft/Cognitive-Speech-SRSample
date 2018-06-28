using System;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Text;
using System.Threading;

namespace SpeechBot.WebPortal
{
    public class WebSocketBehavior
    {
        internal WebSocket webSocket;
        internal HttpContext context;

        public WebSocketBehavior()
        {
        }

        public virtual void OnClose()
        {
        }

        protected async Task Send(string s)
        {
            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(s)), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        protected async Task Send(byte[] s)
        {
            await webSocket.SendAsync(new ArraySegment<byte>(s), WebSocketMessageType.Binary, true, CancellationToken.None);
        }

        public virtual void OnOpen()
        {
        }

        public virtual void OnMessage(WebSocketReceiveResult result, ArraySegment<byte> arraySegment)
        {
        }

        public virtual void OnError(Exception e)
        {
        }
    }
}