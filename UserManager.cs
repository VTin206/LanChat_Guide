using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace LanChat
{
    public class UserData
    {
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public List<string> Friends { get; set; } = new List<string>();
        public string IPAddress { get; set; }
        public int Port { get; set; }

        public string Serialize()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Username={Username}");
            sb.AppendLine($"PasswordHash={PasswordHash}");
            sb.AppendLine($"IPAddress={IPAddress ?? ""}");
            sb.AppendLine($"Port={Port}");
            sb.Append("Friends=");
            if (Friends != null && Friends.Count > 0)
            {
                sb.Append(string.Join(",", Friends));
            }
            return sb.ToString();
        }

        public static UserData Deserialize(string data)
        {
            UserData user = new UserData();
            string[] lines = data.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (string line in lines)
            {
                if (line.Contains("="))
                {
                    string[] parts = line.Split(new[] { '=' }, 2);
                    string key = parts[0].Trim();
                    string value = parts.Length > 1 ? parts[1].Trim() : "";

                    switch (key)
                    {
                        case "Username":
                            user.Username = value;
                            break;
                        case "PasswordHash":
                            user.PasswordHash = value;
                            break;
                        case "IPAddress":
                            user.IPAddress = value;
                            break;
                        case "Port":
                            int.TryParse(value, out int port);
                            user.Port = port;
                            break;
                        case "Friends":
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                user.Friends = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(f => f.Trim()).ToList();
                            }
                            break;
                    }
                }
            }
            return user;
        }
    }

    public static class UserManager
    {
        private static string dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LanChat");
        private static string usersFolder = Path.Combine(dataFolder, "Users");
        private static Dictionary<string, UserData> users = new Dictionary<string, UserData>();

        static UserManager()
        {
            if (!Directory.Exists(usersFolder))
            {
                Directory.CreateDirectory(usersFolder);
            }
            LoadUsers();
        }

        private static void LoadUsers()
        {
            try
            {
                users.Clear();
                if (Directory.Exists(usersFolder))
                {
                    string[] userFiles = Directory.GetFiles(usersFolder, "*.user");
                    foreach (string file in userFiles)
                    {
                        try
                        {
                            string data = File.ReadAllText(file);
                            UserData user = UserData.Deserialize(data);
                            if (!string.IsNullOrEmpty(user.Username))
                            {
                                users[user.Username] = user;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("Lỗi khi tải dữ liệu người dùng: " + ex.Message);
            }
        }

        private static void SaveUser(UserData user)
        {
            try
            {
                string userFile = Path.Combine(usersFolder, user.Username + ".user");
                string data = user.Serialize();
                File.WriteAllText(userFile, data);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("Lỗi khi lưu dữ liệu người dùng: " + ex.Message);
            }
        }

        private static string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(password);
                byte[] hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        public static bool RegisterUser(string username, string password)
        {
            if (users.ContainsKey(username))
            {
                return false;
            }

            UserData user = new UserData
            {
                Username = username,
                PasswordHash = HashPassword(password),
                Friends = new List<string>()
            };

            users[username] = user;
            SaveUser(user);
            return true;
        }

        public static bool ValidateUser(string username, string password)
        {
            if (!users.ContainsKey(username))
            {
                return false;
            }

            string passwordHash = HashPassword(password);
            return users[username].PasswordHash == passwordHash;
        }

        public static bool UserExists(string username)
        {
            return users.ContainsKey(username);
        }

        public static UserData GetUser(string username)
        {
            if (users.ContainsKey(username))
            {
                return users[username];
            }
            return null;
        }

        public static void UpdateUser(UserData user)
        {
            if (users.ContainsKey(user.Username))
            {
                users[user.Username] = user;
                SaveUser(user);
            }
        }

        public static List<string> GetFriends(string username)
        {
            if (users.ContainsKey(username))
            {
                return users[username].Friends ?? new List<string>();
            }
            return new List<string>();
        }

        public static bool AddFriend(string username, string friendUsername)
        {
            if (!users.ContainsKey(username) || !users.ContainsKey(friendUsername))
            {
                return false;
            }

            if (username == friendUsername)
            {
                return false; // Không thể kết bạn với chính mình
            }

            if (!users[username].Friends.Contains(friendUsername))
            {
                users[username].Friends.Add(friendUsername);
                SaveUser(users[username]);
            }

            if (!users[friendUsername].Friends.Contains(username))
            {
                users[friendUsername].Friends.Add(username);
                SaveUser(users[friendUsername]);
            }

            return true;
        }

        public static bool RemoveFriend(string username, string friendUsername)
        {
            if (!users.ContainsKey(username) || !users.ContainsKey(friendUsername))
            {
                return false;
            }

            users[username].Friends.Remove(friendUsername);
            SaveUser(users[username]);
            
            users[friendUsername].Friends.Remove(username);
            SaveUser(users[friendUsername]);
            
            return true;
        }

        public static void UpdateUserIP(string username, string ip, int port)
        {
            if (users.ContainsKey(username))
            {
                users[username].IPAddress = ip;
                users[username].Port = port;
                SaveUser(users[username]);
            }
        }

        public static List<UserData> GetAllUsers()
        {
            return users.Values.ToList();
        }

        public static List<string> SearchUsers(string searchTerm)
        {
            return users.Keys.Where(u => u.ToLower().Contains(searchTerm.ToLower())).ToList();
        }
    }
}
