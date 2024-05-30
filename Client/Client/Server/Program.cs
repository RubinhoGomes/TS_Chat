using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using EI.SI;
using System.Net.Sockets;
using System.Net;


namespace Server
{
    internal class Program
    {
        private const int PORT = 10000;
      //  private static int clientcounter = 0;

        static void Main(string[] args)
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, PORT);
            TcpListener listener = new TcpListener(endPoint);
            listener.Start();
            Console.WriteLine("Server is Ready");
            int clientCounter = 0;

            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                clientCounter++;
                Console.WriteLine("Cliente {0} connected", clientCounter);
                //criar o objeto ClientHandler para tratar do cliente, criando uma Classe    
                ClientHandler clientHandler = new ClientHandler(client, clientCounter);
                clientHandler.Handle();
            }
         }
    }

    //classe que vai "tratar" do cliente
    class ClientHandler
    {
        private TcpClient client;
        private int clientID;
        //construtor da classe
        public ClientHandler(TcpClient client, int clientID)
        {

            this.client = client;
            this.clientID = clientID;
        }
        //função para construir e iniciar a Thread
        public void Handle()
        {
            Thread thread = new Thread(threadHandler);
            thread.Start();
        }

        //código para criar a funcionalidade 
        private void threadHandler()
        { 
            NetworkStream networkStream = this.client.GetStream();
            ProtocolSI protocolSI = new ProtocolSI();
            //enquanto não for recebida o EOT, fica a comunicar 
            while (protocolSI.GetCmdType() != ProtocolSICmdType.EOT)
            { 
                int bytesRead = networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                byte[] ack;
                switch (protocolSI.GetCmdType())
                {
                    case ProtocolSICmdType.DATA:
                        Console.WriteLine("Cliente " + clientID + ": " + protocolSI.GetStringFromData());
                        ack = protocolSI.Make(ProtocolSICmdType.ACK);
                        networkStream.Write(ack, 0, ack.Length);
                        break;
                    
                    case ProtocolSICmdType.EOT:
                        Console.WriteLine("Terminar a thread do cliente {0}", clientID);
                        ack = protocolSI.Make(ProtocolSICmdType.ACK);
                        networkStream.Write(ack, 0, ack.Length);
                        break;


                }     
            }
        
        networkStream.Close();
        client.Close();
        
        }
    }
}
