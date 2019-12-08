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
        AppendTextDelegate _notiAppender;
        Socket mainSock;
        Socket udpSock;
        IPAddress thisAddress;
        IPAddress serverIPAddress;
        string broadcastIPAddress;
        Socket[] socket = new Socket[254];
        IPAddress[] broadcastIPAddresses = new IPAddress[254];
        string nameID;
        int port = 15000;  //고정

        public ChatForm_Client() {

            

            InitializeComponent();
            mainSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            _textAppender = new AppendTextDelegate(AppendText);
            udpSock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _textAppender = new AppendTextDelegate(AppendText);
            _notiAppender = new AppendTextDelegate(AppendText);

        }

        void AppendText(Control ctrl, string s) {
            if (ctrl.InvokeRequired) ctrl.Invoke(_textAppender, ctrl, s);
            else {
                string source = ctrl.Text;
                ctrl.Text = source + Environment.NewLine + s;
            }
        }

        void AppendNoti(Control ctrl, string s)
        {
            if (ctrl.InvokeRequired)
            {
                Console.WriteLine("공지 바꾸기");
                //ctrl.Invoke(_notiAppender, ctrl, s);
                //ctrl.ResetText();
                ctrl.Invoke(_notiAppender, ctrl, s);
            }
            else
            {
                Console.WriteLine("음");
                //string source = ctrl.Text;
                ctrl.Text = s;
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
                        && ip[1].Equals("168")) || (ip[0].Equals("172") && Convert.ToInt32(ip[1]) >= 16 && Convert.ToInt32(ip[1]) <= 31))
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
                        for(int i = 0; i<254; i++)
                        {

                            broadcastIPAddresses[i] = IPAddress.Parse( broadcastIPAddress + (i + 1));
                            Thread th = new Thread(broadcastPing);
                            th.IsBackground = true;
                            th.Start(broadcastIPAddresses[i]);
                        }
                        

                        Console.WriteLine(broadcastIPAddresses[0]);
                        Console.WriteLine(broadcastIPAddress);
                        //txtAddress.Text = thisAddress.ToString();
                        txtAddress.Text = "";

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
                //thisAddress = IPAddress.Parse(txtAddress.Text);
            }

            // ThreadPool.QueueUserWorkItem(OnConnectToServer);
        }
        
        
        void OnConnectToServer(object sender, EventArgs e) {
            // 연결 버튼

            if (btnConnect.Text.Equals("연결끊기")){

                // 문자열을 utf8 형식의 바이트로 변환한다.
                DataForm dataForm = new DataForm();
                dataForm.id = nameID;
                dataForm.req = "close";
                dataForm.text = "연결을 종료합니다.";
                string request = JsonConvert.SerializeObject(dataForm);
                byte[] bDts = Encoding.UTF8.GetBytes(request);
                // Encoding.UTF8.GetBytes(nameID + '`' + tts);


                // 서버에 전송한다.
                mainSock.Send(bDts);

                // 전송 완료 후 텍스트박스에 추가하고, 원래의 내용은 지운다.
                AppendText(txtHistory, string.Format("[연결을 종료합니다.]"));

                mainSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                btnConnect.Text = "연결";
                return;
            }
            if (mainSock.Connected) {
                MsgBoxHelper.Error("이미 연결되어 있습니다!");
                return;
            }


            
            if(txtID.Text.Length <= 0)
            {
                MsgBoxHelper.Error("아이디를 입력해주세요");
            }
            nameID = txtID.Text; //ID

            AppendText(txtHistory, string.Format("서버: @{0}, port: 15000, ID: @{1}", txtAddress.Text, nameID));
            try
            {
                // 여기서 브로드 캐스트 한번 해줘야 함
                mainSock.Connect(serverIPAddress, port);
                // 밑에는 원래 코드
                // mainSock.Connect(broadcastIPAddresses[192], port);
                //Console.WriteLine("이거 보이면 연결 잘 된겨 브로드 캐스트 아이피 뽑기 : "+broadcastIPAddress  + i);
            }
            catch (Exception ex)
            {
                Console.WriteLine("연결 실패 ");
                MsgBoxHelper.Error("연결에 실패했습니다!\n오류 내용: {0}", MessageBoxButtons.OK, ex.Message);
            }
            // 연결 완료되었다는 메세지를 띄워준다.
            AppendText(txtHistory, "서버와 연결되었습니다.");

            // 연결 완료, 서버에서 데이터가 올 수 있으므로 수신 대기한다.
            AsyncObject obj = new AsyncObject(4096);
            obj.WorkingSocket = mainSock;
            mainSock.BeginReceive(obj.Buffer, 0, obj.BufferSize, 0, DataReceived, obj);
        }

        void broadcastPing(object ip)
        {
            IPAddress ipArray = (IPAddress)ip;
            Console.WriteLine("파라미터 아이피 보여줘 : "+ipArray);

            int recv = 0;
            byte[] data = new byte[1024];
            string input, stringData;

            IPEndPoint serverEP = new IPEndPoint(ipArray, 15001);

            Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            EndPoint remoteEP = (EndPoint)sender;

            string welcome = "hello, udp server?";
            data = Encoding.UTF8.GetBytes(welcome);
            client.SendTo(data, data.Length, SocketFlags.None, serverEP);

            data = new byte[1024];
            recv = client.ReceiveFrom(data, ref remoteEP);
            
            
            Console.WriteLine("[first] Message received from {0}", remoteEP.ToString());
            if (remoteEP != null)
            {
                //txtAddress.Text = ipArray.ToString();
                AppendText(txtAddress, ipArray.ToString());
                nameID = ipArray.ToString();
                serverIPAddress = ipArray;
                mainSock.Connect(ipArray, port);
                // 연결 완료되었다는 메세지를 띄워준다.
                AppendText(txtHistory, "서버와 연결되었습니다.");

                // 연결 완료, 서버에서 데이터가 올 수 있으므로 수신 대기한다.
                AsyncObject obj = new AsyncObject(4096);
                obj.WorkingSocket = mainSock;
                //obj.WorkingSocket = udpSock;
                mainSock.BeginReceive(obj.Buffer, 0, obj.BufferSize, 0, DataReceived, obj);
                stringData = Encoding.UTF8.GetString(data, 0, recv);
                Console.WriteLine(stringData);


                // 텍스트로 변환한다.
                
                DataForm data3 = new DataForm();
                data3 = JsonConvert.DeserializeObject<DataForm>(stringData);
                Console.WriteLine(data3.id + " " + data3.text);
                if(data3.text.Length > 0)
                {
                    try
                    {
                        this.Invoke(new Action(delegate ()
                        {
                            notificationBox.Text = data3.text;
                        }));
                    }
                    catch
                    {
                    }
                    
                }
                return;
            }
            stringData = Encoding.UTF8.GetString(data, 0, recv);



            Console.WriteLine(stringData);

            Console.WriteLine("Stopping client");
            client.Close();

            Thread th = new Thread(broadcastPing);
            th.IsBackground = true;
            th.Start(broadcastIPAddresses);
        }
        
        void DataReceived(IAsyncResult ar) {
            // BeginReceive에서 추가적으로 넘어온 데이터를 AsyncObject 형식으로 변환한다.
            AsyncObject obj = (AsyncObject) ar.AsyncState;
            try
            {
                // 데이터 수신을 끝낸다.
                int received = obj.WorkingSocket.EndReceive(ar);

                // 받은 데이터가 없으면(연결끊어짐) 끝낸다.
                if (received <= 0)
                {
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
                    this.Invoke(new Action(delegate ()
                    {
                        notificationBox.Text = data.text;
                    }));
                    // AppendNoti(notificationBox, data.text);
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
            } catch
            {

            }
            
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
            dataForm.req = "msg";
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

        private void notificationBox_Click(object sender, EventArgs e)
        {

        }
    }
}