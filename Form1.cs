using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LanChat
{
    public partial class Form1 : Form
    {
        private string currentUsername;
        private TcpListener server;
        private TcpClient client;
        private NetworkStream clientStream;
        private Dictionary<string, TcpClient> friendConnections = new Dictionary<string, TcpClient>();
        private Dictionary<string, NetworkStream> friendStreams = new Dictionary<string, NetworkStream>();
        private Dictionary<string, string> friendIPs = new Dictionary<string, string>();
        private Dictionary<string, int> friendPorts = new Dictionary<string, int>();
        private string currentChatFriend = null;
        private bool isServerRunning = false;
        private Thread serverThread;
        private Thread clientListenThread;
        private int serverPort = 8888;

        public Form1(string username)
        {
            InitializeComponent();
            currentUsername = username;
            this.Text = $"LAN Chat - {username}";
            InitializeApp();
        }

        private void InitializeApp()
        {
            GetLocalIP();
            StartServer();
            LoadFriends();
            UpdateUI();
        }

        private void GetLocalIP()
        {
            try
            {
                string hostName = Dns.GetHostName();
                IPAddress[] addresses = Dns.GetHostAddresses(hostName);
                foreach (IPAddress address in addresses)
                {
                    if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                    {
                        string ip = address.ToString();
                        UserManager.UpdateUserIP(currentUsername, ip, serverPort);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                AppendChat("Lỗi khi lấy địa chỉ IP: " + ex.Message);
            }
        }

        private void StartServer()
        {
            try
            {
                server = new TcpListener(IPAddress.Any, serverPort);
                server.Start();
                isServerRunning = true;

                serverThread = new Thread(ServerListen);
                serverThread.IsBackground = true;
                serverThread.Start();

                AppendChat($"[{DateTime.Now:HH:mm:ss}] Server đã khởi động trên port {serverPort}");
                lblStatus.Text = "Sẵn sàng";
                lblStatus.ForeColor = Color.Green;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi khởi động server: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ServerListen()
        {
            while (isServerRunning)
            {
                try
                {
                    TcpClient newClient = server.AcceptTcpClient();
                    Thread clientHandler = new Thread(() => HandleIncomingConnection(newClient));
                    clientHandler.IsBackground = true;
                    clientHandler.Start();
                }
                catch (Exception ex)
                {
                    if (isServerRunning)
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            AppendChat($"[{DateTime.Now:HH:mm:ss}] Lỗi server: {ex.Message}");
                        });
                    }
                }
            }
        }

        private void HandleIncomingConnection(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[4096];
            string friendUsername = null;

            try
            {
                // Đọc username của người kết nối
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0) return;

                friendUsername = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                
                // Kiểm tra xem có phải bạn bè không
                if (!UserManager.GetFriends(currentUsername).Contains(friendUsername))
                {
                    client.Close();
                    return;
                }

                this.Invoke((MethodInvoker)delegate
                {
                    if (!friendConnections.ContainsKey(friendUsername))
                    {
                        friendConnections[friendUsername] = client;
                        friendStreams[friendUsername] = stream;
                        AppendChat($"[{DateTime.Now:HH:mm:ss}] {friendUsername} đã kết nối");
                        
                        if (currentChatFriend == friendUsername)
                        {
                            lblCurrentChat.Text = $"Đang chat với: {friendUsername}";
                        }
                    }
                });

                // Tiếp tục đọc tin nhắn
                while (client.Connected)
                {
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    string currentFriend = friendUsername; // Capture for closure
                    this.Invoke((MethodInvoker)delegate
                    {
                        if (currentChatFriend == currentFriend)
                        {
                            AppendChat(message);
                        }
                        else
                        {
                            // Hiển thị thông báo có tin nhắn mới
                            int index = lstFriends.Items.IndexOf(currentFriend);
                            if (index >= 0)
                            {
                                lstFriends.Items[index] = $"{currentFriend} (có tin nhắn mới)";
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    AppendChat($"[{DateTime.Now:HH:mm:ss}] Lỗi kết nối: {ex.Message}");
                });
            }
            finally
            {
                if (friendUsername != null)
                {
                    string finalFriend = friendUsername; // Capture for closure
                    this.Invoke((MethodInvoker)delegate
                    {
                        if (friendConnections.ContainsKey(finalFriend))
                        {
                            friendConnections.Remove(finalFriend);
                            friendStreams.Remove(finalFriend);
                            if (currentChatFriend == finalFriend)
                            {
                                currentChatFriend = null;
                                lblCurrentChat.Text = "";
                                lblStatus.Text = "Đã ngắt kết nối";
                                lblStatus.ForeColor = Color.Red;
                            }
                        }
                    });
                }
                client.Close();
            }
        }

        private void LoadFriends()
        {
            lstFriends.Items.Clear();
            List<string> friends = UserManager.GetFriends(currentUsername);
            foreach (string friend in friends)
            {
                lstFriends.Items.Add(friend);
            }
            lblFriends.Text = $"Bạn bè ({friends.Count})";
        }

        private void lstFriends_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstFriends.SelectedItem != null)
            {
                string selectedFriend = lstFriends.SelectedItem.ToString().Split(' ')[0]; // Lấy tên, bỏ phần "(có tin nhắn mới)"
                if (selectedFriend != currentChatFriend)
                {
                    ConnectToFriend(selectedFriend);
                }
            }
        }

        private void lstFriends_DoubleClick(object sender, EventArgs e)
        {
            lstFriends_SelectedIndexChanged(sender, e);
        }

        private void ConnectToFriend(string friendUsername)
        {
            if (currentChatFriend == friendUsername && friendConnections.ContainsKey(friendUsername))
            {
                return; // Đã kết nối rồi
            }

            DisconnectFromCurrentFriend();

            currentChatFriend = friendUsername;
            lblCurrentChat.Text = $"Đang chat với: {friendUsername}";
            txtChat.Clear();

            UserData friendData = UserManager.GetUser(friendUsername);
            if (friendData == null || string.IsNullOrEmpty(friendData.IPAddress))
            {
                MessageBox.Show($"Không tìm thấy thông tin của {friendUsername}. Người này có thể chưa đăng nhập.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                currentChatFriend = null;
                lblCurrentChat.Text = "";
                return;
            }

            try
            {
                client = new TcpClient();
                client.Connect(friendData.IPAddress, friendData.Port > 0 ? friendData.Port : serverPort);
                clientStream = client.GetStream();

                // Gửi username của mình
                byte[] usernameData = Encoding.UTF8.GetBytes(currentUsername);
                clientStream.Write(usernameData, 0, usernameData.Length);
                clientStream.Flush();

                friendConnections[friendUsername] = client;
                friendStreams[friendUsername] = clientStream;

                clientListenThread = new Thread(() => ListenToFriend(friendUsername));
                clientListenThread.IsBackground = true;
                clientListenThread.Start();

                AppendChat($"[{DateTime.Now:HH:mm:ss}] Đã kết nối đến {friendUsername}");
                lblStatus.Text = $"Đã kết nối với {friendUsername}";
                lblStatus.ForeColor = Color.Green;
                UpdateUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể kết nối đến {friendUsername}: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                currentChatFriend = null;
                lblCurrentChat.Text = "";
                lblStatus.Text = "Kết nối thất bại";
                lblStatus.ForeColor = Color.Red;
            }
        }

        private void ListenToFriend(string friendUsername)
        {
            NetworkStream stream = friendStreams[friendUsername];
            byte[] buffer = new byte[4096];

            try
            {
                while (friendConnections.ContainsKey(friendUsername) && 
                       friendConnections[friendUsername].Connected)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    this.Invoke((MethodInvoker)delegate
                    {
                        if (currentChatFriend == friendUsername)
                        {
                            AppendChat(message);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    AppendChat($"[{DateTime.Now:HH:mm:ss}] Mất kết nối với {friendUsername}: {ex.Message}");
                    if (currentChatFriend == friendUsername)
                    {
                        DisconnectFromCurrentFriend();
                    }
                });
            }
        }

        private void DisconnectFromCurrentFriend()
        {
            if (currentChatFriend != null && friendConnections.ContainsKey(currentChatFriend))
            {
                try
                {
                    friendConnections[currentChatFriend].Close();
                }
                catch { }
                friendConnections.Remove(currentChatFriend);
                friendStreams.Remove(currentChatFriend);
            }
            currentChatFriend = null;
            lblCurrentChat.Text = "";
            UpdateUI();
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            SendMessage();
        }

        private void txtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                SendMessage();
            }
        }

        private void SendMessage()
        {
            if (currentChatFriend == null || !friendConnections.ContainsKey(currentChatFriend))
            {
                MessageBox.Show("Chưa chọn bạn để chat!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string message = txtMessage.Text.Trim();
            if (string.IsNullOrWhiteSpace(message))
                return;

            string fullMessage = $"[{DateTime.Now:HH:mm:ss}] {currentUsername}: {message}";

            try
            {
                byte[] data = Encoding.UTF8.GetBytes(fullMessage);
                NetworkStream stream = friendStreams[currentChatFriend];
                stream.Write(data, 0, data.Length);
                stream.Flush();

                AppendChat(fullMessage);
                txtMessage.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi gửi tin nhắn: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                DisconnectFromCurrentFriend();
            }
        }

        private void AppendChat(string message)
        {
            if (txtChat.InvokeRequired)
            {
                txtChat.Invoke((MethodInvoker)delegate { AppendChat(message); });
                return;
            }

            txtChat.AppendText(message + Environment.NewLine);
            txtChat.SelectionStart = txtChat.Text.Length;
            txtChat.ScrollToCaret();
        }

        private void btnAddFriend_Click(object sender, EventArgs e)
        {
            using (AddFriendForm addFriendForm = new AddFriendForm(currentUsername))
            {
                if (addFriendForm.ShowDialog() == DialogResult.OK)
                {
                    LoadFriends();
                }
            }
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            DisconnectFromCurrentFriend();
            lblStatus.Text = "Sẵn sàng";
            lblStatus.ForeColor = Color.Green;
        }

        private void UpdateUI()
        {
            btnSend.Enabled = currentChatFriend != null && friendConnections.ContainsKey(currentChatFriend);
            txtMessage.Enabled = currentChatFriend != null && friendConnections.ContainsKey(currentChatFriend);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            isServerRunning = false;
            if (server != null)
            {
                server.Stop();
            }

            foreach (var connection in friendConnections.Values)
            {
                try
                {
                    connection.Close();
                }
                catch { }
            }

            if (client != null)
            {
                try
                {
                    client.Close();
                }
                catch { }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
