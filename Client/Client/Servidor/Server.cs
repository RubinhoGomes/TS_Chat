using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Security.Policy;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using ProtoIP;
using ProtoIP.Crypto;



namespace Servidor {
    class Server : ProtoServer {
        private byte[] publicKey; // Chave publica do cliente
        public byte[] aesKey; // Chave AES do cliente
        private const string _serverIP = "127.0.0.1";

        private List<User> listaUsers = new List<User>(); //Lista de users logados

        public NotificationPusher _notificationPusher = new NotificationPusher();

        //Os pacotes utilizados:
        // 1 - PUBLIC_KEY 
        // 2 - REGISTER 
        // 3 - LOGIN
        // 4 - MESSAGE
        // 5 - NOTIFICATION
        // 6 - INFORM_COMMUNICATION
     

        //Função chamada quando um cliente faz um request (Função do ProtoIP)
        public override void OnRequest(int userID) {


            Packet receivedPacket = AssembleReceivedDataIntoPacket(userID);
          
            if (receivedPacket._GetType() == Pacote.PUBLIC_KEY) {
                Logger.WriteLog(Logger.LogType.INFO, "Chave publica recebida.");
                this.publicKey = receivedPacket.GetDataAs<byte[]>();
                AES aes = new AES();
                aes.GenerateKey();
                this.aesKey = aes._key;
                byte[] encrypedKey = RSA.Encrypt(aesKey, publicKey);
                Packet ecryptedKeyPacket = new Packet(Pacote.AES_ENCRYPTED_KEY);
                Logger.WriteLog(Logger.LogType.INFO, "Chave AES encriptada enviada para o cliente.");
                ecryptedKeyPacket.SetPayload(encrypedKey);
                Send(Packet.Serialize(ecryptedKeyPacket), userID);

            } else if (receivedPacket._GetType() == Pacote.REGISTER) {
                Logger.WriteLog(Logger.LogType.INFO, "Pedido para Registo recebido.");

                Packet packet = new Packet(Pacote.REGISTER);
                AES aes = new AES(aesKey);
                byte[] dados = receivedPacket.GetDataAs<byte[]>();
                dados = aes.Decrypt(dados);
                string name = Encoding.UTF8.GetString(dados, 0, dados.Length);
                string[] dadosUser = name.Split(';');
                string userName = dadosUser[0];
                string password = dadosUser[1];
                byte[] passwordBytes = Encoding.ASCII.GetBytes(password);
                byte[] salt = ProtoIP.Crypto.HASH.GenerateRandomBytes(32);

                passwordBytes = SHA256.Hash(passwordBytes, salt);

                DateTime localDate = DateTime.Now;
                using (var db = new AuthContext()) {

                    //validar se o user ja existe na base de dados 
                    var user = db.Auths.FirstOrDefault(u => u.Username == userName);
                    if (user != null) {
                        packet.SetPayload(aes.Encrypt(Encoding.ASCII.GetBytes("false")));
                        Send(Packet.Serialize(packet), userID); 
                        Logger.WriteLog(Logger.LogType.ERROR, "Cliente ja registado.");
                        return;
                    }

                    var auth = new Auth { Username = userName, Password = passwordBytes, Salt = salt, AccoutCreation = localDate, LastAuthentication = localDate };
                    User cliente = new User(userID, userName, publicKey, aesKey);
                    listaUsers.Add(cliente);
                    db.Auths.Add(auth);
                    db.SaveChanges();
                 

                    packet.SetPayload(aes.Encrypt(Encoding.ASCII.GetBytes("true")));
                    Logger.WriteLog(Logger.LogType.INFO, "Cliente " + userName + "registado com sucesso.");

                }


                Send(Packet.Serialize(packet), userID);

            } else if (receivedPacket._GetType() == Pacote.LOGIN) {
                Logger.WriteLog(Logger.LogType.INFO, "Pedido para Login recebido.");

                Packet packet = new Packet(Pacote.LOGIN);
                AES aes = new AES(aesKey);
                byte[] dados = receivedPacket.GetDataAs<byte[]>();
                dados = aes.Decrypt(dados);
                string name = Encoding.UTF8.GetString(dados, 0, dados.Length);
                string[] dadosUser = name.Split(';');
                string userName = dadosUser[0];
                string password = dadosUser[1];
                // Console.WriteLine("Passowd campo: (" + password+")");
                if (ClientIsOnline(listaUsers, userName)) {
                    packet.SetPayload(aes.Encrypt(Encoding.ASCII.GetBytes("userOnline")));
                    Logger.WriteLog(Logger.LogType.ERROR, "Login falhado cliente já online.");
                    Send(Packet.Serialize(packet), userID);
                    return;
                } else {
                    using (var db = new AuthContext()) {

                      
                        var user = db.Auths.FirstOrDefault(u => u.Username == userName);
                        if (user == null) {
                            packet.SetPayload(aes.Encrypt(Encoding.ASCII.GetBytes("false")));
                            Logger.WriteLog(Logger.LogType.ERROR, "Login falhado nome não existe.");
                            Send(Packet.Serialize(packet), userID);
                            return;
                        }
                        byte[] passwordBd = user.Password;
                        byte[] salt = user.Salt;

                        byte[] pass = SHA256.Hash(Encoding.ASCII.GetBytes(password), salt);
                        if (ComparaArrayBytes(pass, passwordBd)) {
                            Console.WriteLine("Cliente :"+user.Username+" conectado");
                            User cliente = new User(userID, userName, publicKey, aesKey);
                            listaUsers.Add(cliente);
                            user.LastAuthentication = DateTime.Now;
                            db.SaveChanges();
                            Logger.WriteLog(Logger.LogType.INFO, "Cliente " + userName + " logado com sucesso.");
                            packet.SetPayload(aes.Encrypt(Encoding.ASCII.GetBytes("true")));
                        } else {
                            Logger.WriteLog(Logger.LogType.ERROR, "Login falhado password/username errados.");
                            packet.SetPayload(aes.Encrypt(Encoding.ASCII.GetBytes("false")));
                        }

                    }
                }
                Send(Packet.Serialize(packet), userID);

            } else if (receivedPacket._GetType() == Pacote.MESSAGE) {
                byte[] mensagem = receivedPacket.GetDataAs<byte[]>();
                Packet packet = new Packet(Pacote.MESSAGE);
               

                Mensagem msg =Mensagem.DeserializeMessage(mensagem);

                if (msg.VerifySignature(listaUsers[userID]._publicKey, msg.message,msg.signature )) {
                    packet.SetPayload(msg.message);
                    this._notificationPusher.PushNotification(_serverIP, GetUserNotificationHandlerPort(listaUsers, listaUsers[userID].communicationUsername), Packet.Serialize(packet));
                } else {
                    this._notificationPusher.PushNotification(_serverIP, GetUserNotificationHandlerPort(listaUsers, listaUsers[userID].communicationUsername), Packet.Serialize(packet));
                    Logger.WriteLog(Logger.LogType.ERROR, "Mensagem enviada para o cliente assinatura inválida.");
                }
                
                Send(Packet.Serialize(packet), userID);

            } else if (receivedPacket._GetType() == Pacote.NOTIFICATION) {
                byte[] portaRecebida = receivedPacket.GetDataAs<byte[]>();
                int portaNotificacao = Int32.Parse(Encoding.UTF8.GetString(portaRecebida, 0, portaRecebida.Length));
                string users = "";
                Packet notificationPacket = new Packet((int)Pacote.NOTIFICATION);
                Packet pacote1 = new Packet(10);
                
                listaUsers[userID].portaNotificationHandler = portaNotificacao;
                foreach (User user in listaUsers) {
                    users += user.username + ";";
                }
                users = users.TrimEnd(';');

                byte[] result = Encoding.UTF8.GetBytes(users);
                notificationPacket.SetPayload(result);

                foreach (User user in listaUsers) {
                    this._notificationPusher.PushNotification(_serverIP, user.portaNotificationHandler, Packet.Serialize(notificationPacket));
                }
                
                Logger.WriteLog(Logger.LogType.INFO, "Notificação para todos os utilizadores enviada.");
                Send(Packet.Serialize(pacote1), userID);



            } else if (receivedPacket._GetType() == Pacote.INFORM_COMUNICATION) {
                byte[] userAESKey = GetUserAESKey(listaUsers, userID);

                Packet packet = new Packet(Pacote.INFORM_COMUNICATION);
                Packet packet2 = new Packet(10);

                byte[] dados = receivedPacket.GetDataAs<byte[]>();
                string name = Encoding.UTF8.GetString(dados, 0, dados.Length);
                listaUsers[userID].communicationUsername = name;
                listaUsers[GetUserIndex(listaUsers, name)].communicationUsername = listaUsers[userID].username;

                int porta = GetUserNotificationHandlerPort(listaUsers, name);

                packet.SetPayload(GetUserPublicKey(listaUsers, name));

                Console.WriteLine("Communication Request");
                this._notificationPusher.PushNotification(_serverIP, porta, Packet.Serialize(packet));
                SendComunicationUsername(listaUsers, name, listaUsers[userID].username);
                Logger.WriteLog(Logger.LogType.INFO, "Pacote para informar uma comunicação entre dois clientes enviada.");
                Send(Packet.Serialize(packet), userID);

            } else if (receivedPacket._GetType() == Pacote.COMMUNICATION_AES_ENCRYPTED_KEY) {
                byte[] encryptedAESKey = receivedPacket.GetDataAs<byte[]>();
                Packet packet = new Packet(Pacote.COMMUNICATION_AES_ENCRYPTED_KEY);
                packet.SetPayload(encryptedAESKey);
                this._notificationPusher.PushNotification(_serverIP, GetUserNotificationHandlerPort(listaUsers, listaUsers[userID].communicationUsername), Packet.Serialize(packet));
                Logger.WriteLog(Logger.LogType.INFO, "Chave AES para a comunicação dos clientes enviada.");
                Send(Packet.Serialize(packet), userID);
            }

        }

        private void SendComunicationUsername(List<User> listaUsers, string username, string comunicationUsername) {
            Packet packet = new Packet(Pacote.INFORM_COMUNICATION_USERNAME);
            int userIndex = GetUserIndex(listaUsers, username);
            byte[] userAESKey = GetUserAESKey(listaUsers, userIndex);
            AES aes = new AES(userAESKey);
            byte[] nomeEnc = aes.Encrypt(Encoding.ASCII.GetBytes(comunicationUsername));
            Logger.WriteLog(Logger.LogType.INFO, "Nome do cliente comunicador enviado para o cliente comunicado.");
            packet.SetPayload(nomeEnc);
            this._notificationPusher.PushNotification(_serverIP, GetUserNotificationHandlerPort(listaUsers, username), Packet.Serialize(packet));

        }

        

        private static bool ComparaArrayBytes(byte[] a, byte[] b) {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; ++i) {

                if (a[i] != b[i]) return false;
            }
            return true;
        }
        private int GetUserNotificationHandlerPort(List<User> listaUsers, string username) {
            int porta = 0;
            foreach (User user in listaUsers) {
                if (user.username == username) {
                    porta = user.portaNotificationHandler;
                }

            }
            return porta;
        }
        private byte[] GetUserPublicKey(List<User> listaUsers, string username) {
            byte[] publicKey = new byte[0];
            foreach (User user in listaUsers) {
                if (user.username == username) {
                    publicKey = user._publicKey;
                }

            }
            return publicKey;
        }
        private byte[] GetUserAESKey(List<User> listaUsers, int userID) {
            byte[] userAesKey = new byte[0];
            foreach (User user in listaUsers) {
                if (user.userID == userID) {
                    userAesKey = user._aesKey;
                }

            }
            return userAesKey;
        }

        private int GetUserIndex(List<User> listaUsers, string username) {
            int index = 0;
            foreach (User user in listaUsers) {
                if (user.username == username) {
                    index = listaUsers.IndexOf(user);
                }

            }
            return index;
        }
        private bool ClientIsOnline(List<User> listaUsers, string username) {
            bool existe = false;
            foreach (User user in listaUsers) {
                if (user.username == username) {
                    existe = true;
                }

            }
            return existe;
        }
        public override void OnUserConnect(int usedID) {
            Logger.WriteLog(Logger.LogType.INFO, "Cliente Conectado.");
        }
        public override void OnUserDisconnect(int usedID) {
            Logger.WriteLog(Logger.LogType.INFO, "Cliente" + listaUsers[usedID].username + " Desconectado.");

            Console.WriteLine("User Disconnected");
            listaUsers.RemoveAt(usedID);
        }

    }
}

