using System;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using System.Threading;
using System.Security.Cryptography;
using System.IO;

namespace MultiChatServer
{
    public partial class ChatForm_Server : Form
    {



        class DataForm
        {
            public string req;
            public string res;
            public string id;
            public string text;
        }
        delegate void AppendTextDelegate(Control ctrl, string s);
        AppendTextDelegate _textAppender;
        Socket mainSock; // 메시지 주고 받는 소켓
        Socket udpSock; // udp 응답을 받아줄 소켓
        IPAddress thisAddress; // ip 서버 주소
        List<Socket> connectedClients;
        string notice = "";
        string key = "01234567891234560123456789123456";
        int serverPort = 15952;
        void asyncConnectClient()
        {


            int recv = 0;
            byte[] data = new byte[1024];

            IPEndPoint ep = new IPEndPoint(IPAddress.Any, 15001);
            Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            server.Bind(ep);

            Console.WriteLine("클라이언트 기다리는 중");

            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            EndPoint remoteEP = (EndPoint)sender;

            recv = server.ReceiveFrom(data, ref remoteEP);

            Console.WriteLine("[first] Message received from {0}", remoteEP.ToString());
            Console.WriteLine("[first] received data : {0}", Encoding.UTF8.GetString(data, 0, recv));

            DataForm dataForm = new DataForm();
            dataForm.id = "Server";
            dataForm.text = notice;
            dataForm.req = "notice";
            string request = JsonConvert.SerializeObject(dataForm);
            string encryptedData = AESEncrypt256(request, key);

            byte[] bDts = Encoding.UTF8.GetBytes(encryptedData);

            string text = Encoding.UTF8.GetString(bDts);

            
            Console.WriteLine(text);

            server.SendTo(bDts, remoteEP);

            server.Close();
            Thread th = new Thread(asyncConnectClient);
            th.IsBackground = true;
            th.Start();
        }

        public ChatForm_Server()
        {
            InitializeComponent();
            mainSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            udpSock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _textAppender = new AppendTextDelegate(AppendText);
            connectedClients = new List<Socket>();
            BeginStartServer(null, null);
            Thread th = new Thread(asyncConnectClient);
            th.IsBackground = true;
            th.Start();
        }

        void AppendText(Control ctrl, string s)
        {
            if (ctrl.InvokeRequired) ctrl.Invoke(_textAppender, ctrl, s);
            else
            {
                string source = ctrl.Text;
                ctrl.Text = source + Environment.NewLine + s;
            }
        }

        void OnFormLoaded(object sender, EventArgs e)
        {
            IPHostEntry he = Dns.GetHostEntry(Dns.GetHostName());
            // 처음으로 발견되는 ipv4 주소를 사용한다.
            foreach (IPAddress addr in he.AddressList)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                {
                    //AppendText(txtHistory, addr.ToString());
                    string[] ip = addr.ToString().Split('.');
                    // ! A, B, C 클래스 제외
                    if (ip[0].Equals("172") || ip[0].Equals("192") || ip[0].Equals("10") || ip[0].Equals("169") || ip[0].Equals("127")
                        || ip[0].Equals("0") || ip[0].Equals("224") || ip[0].Equals("240") || ip[0].Equals("239") || (ip[0].Equals("192")
                        && ip[1].Equals("168")) || (ip[0].Equals("172") && Convert.ToInt32(ip[1]) >= 16 && Convert.ToInt32(ip[1]) <= 31))
                    {
                        //AppendText(txtHistory, "포함");
                    }
                    else
                    {
                        thisAddress = IPAddress.Parse(addr.ToString());
                    }
                }
            }
        }
        void BeginStartServer(object sender, EventArgs e)
        {
            int port;
            if (!int.TryParse(txtPort.Text, out port))
            {
                MsgBoxHelper.Error("포트 번호가 잘못 입력되었거나 입력되지 않았습니다.");
                txtPort.Focus();
                txtPort.SelectAll();
                return;
            }

            IPHostEntry he = Dns.GetHostEntry(Dns.GetHostName());
            // 처음으로 발견되는 ipv4 주소를 사용한다.
            foreach (IPAddress addr in he.AddressList)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                {
                    //AppendText(txtHistory, addr.ToString());
                    string[] ip = addr.ToString().Split('.');
                    // ! A, B, C 클래스 제외
                    if (ip[0].Equals("172") || ip[0].Equals("192") || ip[0].Equals("10") || ip[0].Equals("169") || ip[0].Equals("127")
                        || ip[0].Equals("0") || ip[0].Equals("224") || ip[0].Equals("240") || ip[0].Equals("239") || (ip[0].Equals("192")
                        && ip[1].Equals("168")) || (ip[0].Equals("172") && Convert.ToInt32(ip[1]) >= 16 && Convert.ToInt32(ip[1]) <= 31))
                    {
                        //AppendText(txtHistory, "포함");
                    }
                    else
                    {
                        thisAddress = IPAddress.Parse(addr.ToString());
                    }
                }
            }
            // 서버에서 클라이언트의 연결 요청을 대기하기 위해
            // 소켓을 열어둔다.

            IPEndPoint serverEP = new IPEndPoint(thisAddress, serverPort);

            udpSock.Bind(serverEP);
            mainSock.Bind(serverEP);
            mainSock.Listen(10);

            AppendText(txtHistory, string.Format("서버 시작: @{0}", serverEP));
            AppendText(txtAddress, thisAddress.ToString());
            // 비동기적으로 클라이언트의 연결 요청을 받는다.
            //udpSock.BeginAccept(AcceptCallback, null);
            mainSock.BeginAccept(AcceptCallback, null);
        }


        void AcceptCallback(IAsyncResult ar)
        {
            // 클라이언트의 연결 요청을 수락한다.
            Socket client = mainSock.EndAccept(ar);

            // 또 다른 클라이언트의 연결을 대기한다.
            mainSock.BeginAccept(AcceptCallback, null);

            AsyncObject obj = new AsyncObject(4096);
            obj.WorkingSocket = client;

            // 연결된 클라이언트 리스트에 추가해준다.
            connectedClients.Add(client);

            // 텍스트박스에 클라이언트가 연결되었다고 써준다.
            AppendText(txtHistory, string.Format("클라이언트 (@ {0})가 연결되었습니다.", client.RemoteEndPoint));

            // 클라이언트의 데이터를 받는다.
            client.BeginReceive(obj.Buffer, 0, 4096, 0, DataReceived, obj);
        }

        void DataReceived(IAsyncResult ar)
        {
            // BeginReceive에서 추가적으로 넘어온 데이터를 AsyncObject 형식으로 변환한다.
            AsyncObject obj = (AsyncObject)ar.AsyncState;

            try
            {
                // 데이터 수신을 끝낸다.
                int received = obj.WorkingSocket.EndReceive(ar);

                // 받은 데이터가 없으면(연결끊어짐) 끝낸다.
                if (received <= 0)
                {
                    obj.WorkingSocket.Disconnect(false);
                    obj.WorkingSocket.Close();
                    return;
                }

                Console.WriteLine(obj.Buffer);
                // 텍스트로 변환한다.
                string text = Encoding.UTF8.GetString(obj.Buffer);
                Console.WriteLine("데이터 브로드 캐스트"+text);
                string[] arrayText;
                arrayText = text.ToString().Split('?');
                DataForm data = new DataForm();
                data = JsonConvert.DeserializeObject<DataForm>(arrayText[0]);
                if (data.req == null)
                {
                    MsgBoxHelper.Error("null");
                    data.req = "";
                }
                if (data.req.Equals("close"))
                {
                    MsgBoxHelper.Error("close 들어옴");
                    Console.WriteLine("close 들어옴");
                    obj.WorkingSocket.Disconnect(false);
                    obj.WorkingSocket.Close();
                }

                // 텍스트박스에 추가해준다.
                // 비동기식으로 작업하기 때문에 폼의 UI 스레드에서 작업을 해줘야 한다.
                // 따라서 대리자를 통해 처리한다.


                AppendText(txtHistory, string.Format("[받음]{0}: {1}", data.id, data.text));

                // for을 통해 "역순"으로 클라이언트에게 데이터를 보낸다.
                for (int i = connectedClients.Count - 1; i >= 0; i--)
                {
                    Socket socket = connectedClients[i];
                    if (socket != obj.WorkingSocket)
                    {
                        try
                        {
                            string request = JsonConvert.SerializeObject(data);
                            AESEncrypt256(request, key);
                            Encoding.UTF8.GetBytes(AESEncrypt256(request, key));
                            Console.WriteLine("이 암호화된 내용으로 브로드 캐스팅 : "+ AESEncrypt256(request, key));
                            socket.Send(Encoding.UTF8.GetBytes(AESEncrypt256(request, key)));
                        }
                        catch
                        {
                            // 오류 발생하면 전송 취소하고 리스트에서 삭제한다.
                            try { socket.Dispose(); } catch { }
                            // connectedClients.RemoveAt(i);
                        }
                    }
                }

                // 데이터를 받은 후엔 다시 버퍼를 비워주고 같은 방법으로 수신을 대기한다.
                obj.ClearBuffer();

                // 수신 대기
                obj.WorkingSocket.BeginReceive(obj.Buffer, 0, 4096, 0, DataReceived, obj);
            }
            catch
            {

            }

        }

        void OnSendData(object sender, EventArgs e)
        {
            sendData();
        }

        private void ChatForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                mainSock.Close();
            }
            catch { }

        }

        private void txtPort_TextChanged(object sender, EventArgs e)
        {

        }

        private void txtTTS_TextChanged(object sender, EventArgs e, KeyPressEventArgs f)
        {
            if (Convert.ToInt32(f.KeyChar) == 13)
            {
                //sendData();
                //MessageBox.Show(" Enter pressed ");
            }
        }

        private void txtTTS_Click(object sender, EventArgs e)
        {

        }

        private void txtTTS_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (Convert.ToInt32(e.KeyChar) == 13)
            {
                sendData();
                // MessageBox.Show(" Enter pressed ");
            }
        }
        void sendData()
        {
            // 서버가 대기중인지 확인한다.
            if (!mainSock.IsBound)
            {
                MsgBoxHelper.Warn("서버가 실행되고 있지 않습니다!");
                return;
            }

            // 보낼 텍스트
            string tts = txtTTS.Text.Trim();
            if (string.IsNullOrEmpty(tts))
            {
                MsgBoxHelper.Warn("공지사항이 입력되지 않았습니다!");
                txtTTS.Focus();
                return;
            }

            DataForm dataForm = new DataForm();
            dataForm.id = "Server";
            dataForm.text = tts;
            notice = tts;
            dataForm.req = "notice";
            
            string request = JsonConvert.SerializeObject(dataForm);
            string encryptedRequest = AESEncrypt256(request, key);
            Console.WriteLine(encryptedRequest);
            byte[] bDts = Encoding.UTF8.GetBytes(encryptedRequest);

            string text = Encoding.UTF8.GetString(bDts);

            string dectypedstr = Decrypt256(text, key);
            Console.WriteLine(dectypedstr);



            // 문자열을 utf8 형식의 바이트로 변환한다.
            // byte[] bDts = Encoding.UTF8.GetBytes("Server" + '`' + tts);

            // 연결된 모든 클라이언트에게 전송한다.
            for (int i = connectedClients.Count - 1; i >= 0; i--)
            {
                Socket socket = connectedClients[i];
                try { socket.Send(bDts); }
                catch
                {
                    // 오류 발생하면 전송 취소하고 리스트에서 삭제한다.
                    try { socket.Dispose(); } catch { }
                    connectedClients.RemoveAt(i);
                }
            }
            AppendText(txtHistory, string.Format("[서버 공지]Server : {0}", tts));
            txtTTS.Clear();

        }


        private string Decrypt256(String Input, String key)

        {

            RijndaelManaged aes = new RijndaelManaged();

            aes.KeySize = 256;

            aes.BlockSize = 128;

            aes.Mode = CipherMode.CBC;

            aes.Padding = PaddingMode.PKCS7;

            aes.Key = Encoding.UTF8.GetBytes(key);

            aes.IV = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };



            var decrypt = aes.CreateDecryptor();

            byte[] xBuff = null;

            using (var ms = new MemoryStream())

            {

                using (var cs = new CryptoStream(ms, decrypt, CryptoStreamMode.Write))

                {

                    byte[] xXml = Convert.FromBase64String(Input);

                    cs.Write(xXml, 0, xXml.Length);

                }



                xBuff = ms.ToArray();

            }



            String Output = Encoding.UTF8.GetString(xBuff);

            return Output;

        }



        private String AESEncrypt256(String Input, String key)

        {

            RijndaelManaged aes = new RijndaelManaged();

            aes.KeySize = 256;

            aes.BlockSize = 128;

            aes.Mode = CipherMode.CBC;

            aes.Padding = PaddingMode.PKCS7;

            aes.Key = Encoding.UTF8.GetBytes(key);

            aes.IV = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };



            var encrypt = aes.CreateEncryptor(aes.Key, aes.IV);

            byte[] xBuff = null;

            using (var ms = new MemoryStream())

            {

                using (var cs = new CryptoStream(ms, encrypt, CryptoStreamMode.Write))

                {

                    byte[] xXml = Encoding.UTF8.GetBytes(Input);

                    cs.Write(xXml, 0, xXml.Length);

                }



                xBuff = ms.ToArray();

            }



            String Output = Convert.ToBase64String(xBuff);

            return Output;

        }
    }
}