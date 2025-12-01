using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace LanChat
{
    public partial class AddFriendForm : Form
    {
        private string currentUsername;

        public AddFriendForm(string username)
        {
            InitializeComponent();
            currentUsername = username;
            LoadAllUsers();
        }

        private void LoadAllUsers()
        {
            lstAllUsers.Items.Clear();
            List<UserData> allUsers = UserManager.GetAllUsers();
            List<string> friends = UserManager.GetFriends(currentUsername);

            foreach (UserData user in allUsers)
            {
                if (user.Username != currentUsername && !friends.Contains(user.Username))
                {
                    lstAllUsers.Items.Add(user.Username);
                }
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            if (lstAllUsers.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn người dùng để thêm bạn!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string friendUsername = lstAllUsers.SelectedItem.ToString();
            
            if (UserManager.AddFriend(currentUsername, friendUsername))
            {
                MessageBox.Show($"Đã thêm {friendUsername} vào danh sách bạn bè!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("Không thể thêm bạn!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            string searchTerm = txtSearch.Text.Trim().ToLower();
            lstAllUsers.Items.Clear();

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                LoadAllUsers();
                return;
            }

            List<string> friends = UserManager.GetFriends(currentUsername);
            List<string> searchResults = UserManager.SearchUsers(searchTerm);

            foreach (string username in searchResults)
            {
                if (username != currentUsername && !friends.Contains(username))
                {
                    lstAllUsers.Items.Add(username);
                }
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}

