using System;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;

namespace Spigot.Tests
{
    public class XpubXsubIntermediary : IDisposable
    {
        private Task listenter;
        private Proxy proxy;
        private XPublisherSocket xpubSocket;
        private XSubscriberSocket xsubSocket;

        private XpubXsubIntermediary()
        {
        }

        public void Dispose()
        {
            proxy.Stop();
            xpubSocket.Dispose();
            xsubSocket.Dispose();
        }

        public static XpubXsubIntermediary Start()
        {
            var instance = new XpubXsubIntermediary();
            instance.Init();
            return instance;
        }

        private void Init()
        {
            xpubSocket = new XPublisherSocket("@tcp://127.0.0.1:1234");
            xsubSocket = new XSubscriberSocket("@tcp://127.0.0.1:5677");
            // proxy messages between frontend / backend
            proxy = new Proxy(xsubSocket, xpubSocket);

            // blocks indefinitely
            listenter = Task.Factory.StartNew(() => proxy.Start());
            Thread.Sleep(1000); // time to start up
        }
    }
}