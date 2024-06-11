using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Servidor {
    public class Mensagem {
        public byte[] message;
        public byte[] signature;

        public Mensagem(byte[] message, byte[] signature) {
            this.message = message;
            this.signature = signature;
        }
        public Mensagem() {

        }

        public static Mensagem DeserializeMessage(byte[] mensagemBytes) {
            Mensagem mensagem = new Mensagem();
            int messageLength = BitConverter.ToInt32(mensagemBytes, 0);
            mensagem.message = new byte[messageLength];
            Array.Copy(mensagemBytes, 4, mensagem.message, 0, messageLength);
            int signatureLength = BitConverter.ToInt32(mensagemBytes, 4 + messageLength);
            mensagem.signature = new byte[signatureLength];
            Array.Copy(mensagemBytes, 4 + messageLength + 4, mensagem.signature, 0, signatureLength);
            return mensagem;

        }

       public bool VerifySignature(byte[] rsaPublicKey, byte[] mensagem, byte[] assinatura) {
            ProtoIP.Crypto.RSA rsa = new ProtoIP.Crypto.RSA();
            byte[] messageHash = new ProtoIP.Crypto.SHA256(mensagem)._digest;
            return rsa.Verify(messageHash, assinatura, rsaPublicKey);

        }
    }
}
