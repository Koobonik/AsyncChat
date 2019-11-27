using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace MultiChatClient {

    
    public partial class ChatForm_Client : Form {
        
        

        class DataForm
        {
            public string req;
            public string res;
            public string id;
            public string text;

        }
        

        delegate void AppendTextDelegate(Control ctrl, string s);
        AppendTextDelegate _textAppender;
        Socket mainSock;
        IPAddress thisAddress;
        string broadcastIPAddress;
        string nameID;

        public ChatForm_Client() {
            InitializeComponent();
            mainSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            _textAppender = new AppendTextDelegate(AppendText);
        }

        void AppendText(Control ctrl, string s) {
            if (ctrl.InvokeRequired) ctrl.Invoke(_textAppender, ctrl, s);
            else {
                string source = ctrl.Text;
                ctrl.Text = source + Environment.NewLine + s;
            }
        }

        void OnFormLoaded(object sender, EventArgs e) {

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
                        || ip[1].Equals("168")) || (ip[0].Equals("172") && Convert.ToInt32(ip[1]) >= 16 && Convert.ToInt32(ip[1]) <= 31))
                    {
                        //AppendText(txtHistory, "포함");
                    }
                    else
                    {
                        thisAddress = IPAddress.Parse(addr.ToString());
                        Console.WriteLine("아이피 주소 : "+IPAddress.Parse(addr.ToString()));
                        string[] hi = thisAddress.ToString().Split('.');
                        for (int i = 0; i<3; i++)
                        {
                            // 이후 브로드 캐스트에 쓰일 ip 주소
                            broadcastIPAddress += hi[i] + ".";
                            Console.WriteLine(broadcastIPAddress);
                        }
                        Console.WriteLine(broadcastIPAddress);
                        txtAddress.Text = thisAddress.ToString();
                    }
                }
            }

            if (thisAddress == null)
            {
                // 로컬호스트 주소를 사용한다.
                thisAddress = IPAddress.Loopback;
                // broadcast ping
                txtAddress.Text = thisAddress.ToString();
            }
            else
            {
                thisAddress = IPAddress.Parse(txtAddress.Text);
            }

            // ThreadPool.QueueUserWorkItem(OnConnectToServer);
        }



        void OnConnectToServer(object sender, EventArgs e) {

            if (mainSock.Connected) {
                MsgBoxHelper.Error("이미 연결되어 있습니다!");
                return;
            }

            int port=15000;  //고정

            nameID = txtID.Text; //ID

            AppendText(txtHistory, string.Format("서버: @{0}, port: 15000, ID: @{1}", txtAddress.Text, nameID));
            for (int i = 191; i < 195; i++)
            {
                try
                {
                    // 여기서 브로드 캐스트 한번 해줘야 함
                    // mainSock.Connect(txtAddress.Text, port);
                    IPEndPoint serverEP = new IPEndPoint(IPAddress.Parse(broadcastIPAddress+i), port);
                    Console.WriteLine("serverEP : "+serverEP.ToString());
                    TimeSpan timeSpan = new TimeSpan(50);
                    //SocketExtensions.Connect(mainSock, serverEP, timeSpan);
                    IAsyncResult result = mainSock.BeginConnect(broadcastIPAddress + i, port, null, null);

                    bool success = result.AsyncWaitHandle.WaitOne(50, true);
                    Console.WriteLine(success);
                    if (mainSock.Connected)
                    {
                        mainSock.EndConnect(result);
                    }
                    else
                    {
                        // NOTE, MUST CLOSE THE SOCKET

                        mainSock.Close();
                        throw new ApplicationException("Failed to connect server.");
                    }


                    // 밑에는 원래 코드
                    //mainSock.Connect(broadcastIPAddress + i, port);
                    Console.WriteLine("이거 보이면 연결 잘 된겨 브로드 캐스트 아이피 뽑기 : "+broadcastIPAddress  + i);
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("연결 실패 ");
                    //MsgBoxHelper.Error("연결에 실패했습니다!\n오류 내용: {0}", MessageBoxButtons.OK, ex.Message);
                    // return;
                }
            }
            // 연결 완료되었다는 메세지를 띄워준다.
            AppendText(txtHistory, "서버와 연결되었습니다.");

            // 연결 완료, 서버에서 데이터가 올 수 있으므로 수신 대기한다.
            AsyncObject obj = new AsyncObject(4096);
            //obj.WorkingSocket = mainSock;
            //mainSock.BeginReceive(obj.Buffer, 0, obj.BufferSize, 0, DataReceived, obj);
        }
        
        void DataReceived(IAsyncResult ar) {
            // BeginReceive에서 추가적으로 넘어온 데이터를 AsyncObject 형식으로 변환한다.
            AsyncObject obj = (AsyncObject) ar.AsyncState;

            // 데이터 수신을 끝낸다.
            int received = obj.WorkingSocket.EndReceive(ar);

            // 받은 데이터가 없으면(연결끊어짐) 끝낸다.
            if (received <= 0) {
                obj.WorkingSocket.Close();
                return;
            }

            // 텍스트로 변환한다.
            string text = Encoding.UTF8.GetString(obj.Buffer);
            DataForm data = new DataForm();
            data = JsonConvert.DeserializeObject<DataForm>(text);
            // : 기준으로 짜른다.
            // tokens[0] - 보낸 사람 ID
            // tokens[1] - 보낸 메세지

            // 텍스트박스에 추가해준다.
            // 비동기식으로 작업하기 때문에 폼의 UI 스레드에서 작업을 해줘야 한다.
            // 따라서 대리자를 통해 처리한다.
            if (data.id.Equals("Server"))
            {
                AppendText(txtHistory, string.Format("[공지사항이 등록되었습니다.] : {0}", data.text));
            }
            else
            {
                AppendText(txtHistory, string.Format("[받음]{0} : {1}", data.id, data.text));
            }
            
            
            // 클라이언트에선 데이터를 전달해줄 필요가 없으므로 바로 수신 대기한다.
            // 데이터를 받은 후엔 다시 버퍼를 비워주고 같은 방법으로 수신을 대기한다.
            obj.ClearBuffer();

            // 수신 대기
            obj.WorkingSocket.BeginReceive(obj.Buffer, 0, 4096, 0, DataReceived, obj);
        }

        void OnSendData(object sender, EventArgs e) {
            // 서버가 대기중인지 확인한다.
            if (!mainSock.IsBound) {
                MsgBoxHelper.Warn("서버가 실행되고 있지 않습니다!");
                return;
            }

            // 보낼 텍스트
            string tts = txtTTS.Text.Trim();
            if (string.IsNullOrEmpty(tts)) {
                MsgBoxHelper.Warn("텍스트가 입력되지 않았습니다!");
                txtTTS.Focus();
                return;
            }

            // ID 와 메세지를 담도록 만든다.

            // 문자열을 utf8 형식의 바이트로 변환한다.
            DataForm dataForm = new DataForm();
            dataForm.id = nameID;
            dataForm.text = tts;
            string request = JsonConvert.SerializeObject(dataForm);
            byte[] bDts = Encoding.UTF8.GetBytes(request);
            // Encoding.UTF8.GetBytes(nameID + '`' + tts);


            // 서버에 전송한다.
            mainSock.Send(bDts);

            // 전송 완료 후 텍스트박스에 추가하고, 원래의 내용은 지운다.
            AppendText(txtHistory, string.Format("[나]{0} : {1}", dataForm.id, dataForm.text));
            txtTTS.Clear();
        }

        private void ChatForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (mainSock!=null) {
                try
                {
                    mainSock.Disconnect(false);
                    mainSock.Close();
                }
                catch (Exception)
                {
                    Console.WriteLine("서버 연결 안하고 닫힘");
                }
                
            }

        }

        private void txtTTS_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (Convert.ToInt32(e.KeyChar) == 13)
            {
                // MessageBox.Show(" Enter pressed ");
                OnSendData(sender, e);

            }
        }
    }
}