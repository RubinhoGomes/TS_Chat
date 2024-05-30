using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using EI.SI;

namespace Client
{
    public partial class Chat : Form
    {
        private const int PORT = 10000;
        NetworkStream networkStream;
        TcpClient client;
        ProtocolSI protocolSI;

        public Chat()
        {
            InitializeComponent();
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, PORT);
            client = new TcpClient();
            client.Connect(endPoint);
            networkStream = client.GetStream();
            protocolSI = new ProtocolSI();
        }

        private void buttonSend_Click(object sender, EventArgs e)
        {
            string msg = textBoxMessage.Text;
            textBoxMessage.Clear();
            byte[] packet = protocolSI.Make(ProtocolSICmdType.DATA, msg);
            networkStream.Write(packet, 0, packet.Length);

            while (protocolSI.GetCmdType() != ProtocolSICmdType.ACK)
            {
                networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);            
            }
        }

        private void CloseClient()
        {
            byte[] eot = protocolSI.Make(ProtocolSICmdType.EOT);
            networkStream.Write(eot, 0, eot.Length);
            networkStream.Read(protocolSI.Buffer,0,protocolSI.Buffer.Length);  
            networkStream.Close();
            client.Close(); 
        }


        private void buttonQuit_Click(object sender, EventArgs e)
        {
            CloseClient();
            this.Close();

        }
    }
}
