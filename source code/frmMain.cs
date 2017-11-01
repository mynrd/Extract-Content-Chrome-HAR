using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace ExtractContentHAR
{
    public partial class frmMain : Form
    {
        private List<ItemMessage> Messages;
        private string Pattern;
        private System.Windows.Forms.Timer Timer;
        private bool TimerDispose = false;

        public frmMain()
        {
            InitializeComponent();
            lstUrls.SelectedIndexChanged += new EventHandler(lstUrls_SelectedIndexChanged);
            lstUrls.MouseDoubleClick += new MouseEventHandler(lstUrls_MouseDoubleClick);
            txtFile.KeyDown += new KeyEventHandler(txtFile_KeyDown);

            Messages = new List<ItemMessage>();
            this.InstanceTimer();
        }

        private delegate void ProcessDownload(string path);

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Har files (*.har)|*.har|All files (*.*)|*.*";
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtFile.Text = ofd.FileName;
                string content = "";
                using (StreamReader sr = new StreamReader(ofd.FileName)) { content = sr.ReadToEnd(); }
                LoadHARContent(content);
            }
        }

        private void btnBrowseFolder_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtPath.Text = fbd.SelectedPath;
            }
        }

        private void btnExtract_Click(object sender, EventArgs e)
        {
            this.TimerDispose = false;
            this.InstanceTimer();
            this.Pattern = txtPattern.Text.ToLower().Trim();
            //this.Messages = new List<ItemMessage>();
            this.Timer.Enabled = true;
            //txtFile_KeyDown(null, new KeyEventArgs(Keys.Enter));
            txtFile.Enabled = false;
            btnBrowse.Enabled = false;
            btnParseData.Enabled = false;
            btnParseData_Click(null, null);
            btnExtract.Enabled = false;
            txtPath.Enabled = false;
            btnBrowseFolder.Enabled = false;

            Thread th = new Thread(new ParameterizedThreadStart(DownloadFiles));
            th.Start(txtPath.Text);
        }

        private void btnParseData_Click(object sender, EventArgs e)
        {
            btnParseData.Enabled = false;
            txtPattern.ReadOnly = true;
            List<ItemMessage> forRemove = new List<ItemMessage>();
            foreach (var item in this.Messages)
            {
                if (!item.URL.ToLower().StartsWith(txtPattern.Text.ToLower()))
                {
                    forRemove.Add(item);
                }
            }
            forRemove.ForEach(x =>
            {
                lstUrls.Items.Remove(x.ListViewItem);
                this.Messages.Remove(x);
            });
        }

        private void DownloadFiles(object o_path)
        {
            ProcessDownload pd = path =>
            {
                if (!path.EndsWith(@"\")) path += @"\";

                foreach (var lv in Messages)
                {
                    if (lv.URL.ToLower().StartsWith(this.Pattern))
                    {
                        Uri uri = new Uri(lv.URL);
                        string filename = System.IO.Path.GetFileName(uri.LocalPath);
                        if (string.IsNullOrEmpty(filename)) continue;
                        string filePath = lv.URL.Replace(txtPattern.Text, "").Replace(@"/", @"\");

                        if (filePath.IndexOf("?") != -1)
                        {
                            filePath = filePath.Substring(0, filePath.IndexOf("?"));
                        }

                        string folder = (path + filePath).Replace(filename, "");
                        if (!Directory.Exists(folder))
                        {
                            try
                            {
                                Directory.CreateDirectory(folder);
                            }
                            catch (Exception)
                            {
                                continue;
                            }
                        }

                        if (File.Exists(path + filePath))
                        {
                            lv.Status = "File Exist";
                            continue;
                        }

                        try
                        {
                            lv.Status = "Downloading";
                            using (WebClient Client = new WebClient())
                            {
                                string fileDownloadPath = path + filePath;
                                fileDownloadPath = fileDownloadPath.Replace(@"\\", @"\");
                                string url = lv.URL;

                                if (url.IndexOf("?") != -1)
                                {
                                    url = url.Substring(0, url.IndexOf("?"));
                                }

                                if (url.ToLower().Contains(".png") || url.ToLower().Contains(".bmp") || url.ToLower().Contains(".jpg") || url.ToLower().Contains(".jpeg"))
                                {
                                    url += "?v=" + DateTime.Now.ToString("MMddyyyyhhmmss");
                                }

                                Client.DownloadFile(url, fileDownloadPath);
                            }
                            lv.Status = "Done";
                        }
                        catch (Exception ex)
                        {
                            lv.Status = "Error: " + ex.Message;
                        }
                    }
                    else
                    {
                        lv.Status = "Excluded: not in pattern.";
                    }
                }

                MessageBox.Show("Done", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TimerDispose = true;
            };

            pd(o_path.ToString());
        }

        private void InstanceTimer()
        {
            this.Timer = new System.Windows.Forms.Timer();
            this.Timer.Interval = 1;
            this.Timer.Tick += new EventHandler(Timer_Tick);
            this.Timer.Disposed += new EventHandler(Timer_Disposed);
        }

        private void LoadHARContent(string content, bool clearFirst = true)
        {
            try
            {
                btnParseData.Enabled = true;
                txtPattern.ReadOnly = false;

                if (clearFirst) lstUrls.Items.Clear();
                if (clearFirst) this.Messages.Clear();

                JObject json = JsonConvert.DeserializeObject(content) as JObject;
                JToken entries = json.First.First["entries"];
                foreach (var entry in entries)
                {
                    try
                    {
                        var im = new ItemMessage() { URL = entry["request"]["url"].ToString(), Status = "--" };

                        if (this.Messages.Any(x => x.URL == im.URL))
                        {
                            continue;
                        }
                        this.Messages.Add(im);
                    }
                    catch { }
                }
                if (clearFirst) ReorderLists();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void ReorderLists()
        {
            foreach (var im in this.Messages.OrderBy(x => x.URL))
            {
                ListViewItem lv = lstUrls.Items.Add(im.Status);
                lv.SubItems.Add(im.URL);
                im.ListViewItem = lv;
            }
        }

        private void lstUrls_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                if (lstUrls.FocusedItem != null)
                {
                    MessageBox.Show(lstUrls.FocusedItem.Text, "Status Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void lstUrls_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!txtPattern.ReadOnly)
                txtPattern.Text = lstUrls.FocusedItem.SubItems[1].Text;
        }

        private void Timer_Disposed(object sender, EventArgs e)
        {
            txtFile.Enabled = true;
            btnBrowse.Enabled = true;
            btnParseData.Enabled = true;
            btnExtract.Enabled = true;
            txtPath.Enabled = true;
            btnBrowseFolder.Enabled = true;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            foreach (var item in this.Messages)
            {
                if (item.ListViewItem.Text != item.Status)
                {
                    item.ListViewItem.Text = item.Status;
                }
            }
            if (TimerDispose)
            {
                this.Timer.Enabled = false;
                this.Timer.Dispose();
                this.lstUrls.Items.Clear();

                var items = this.Messages.OrderBy(x => x.Status);
                foreach (var im in items)
                {
                    var lv = lstUrls.Items.Add(im.Status);
                    lv.SubItems.Add(im.URL);
                }
            }
        }

        private void txtFile_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.KeyCode == Keys.Enter)
                {
                    string content = "";
                    using (StreamReader sr = new StreamReader(txtFile.Text)) { content = sr.ReadToEnd(); }
                    LoadHARContent(content);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, this.Text, MessageBoxButtons.OK);
            }
        }

        public class ItemMessage
        {
            public ListViewItem ListViewItem { get; set; }

            public string Status { get; set; }

            public string URL { get; set; }
        }

        private void btnAppend_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog()
            {
                Multiselect = true
            };
            ofd.Filter = "All files (*.*)|*.*";
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                foreach (var item in ofd.FileNames)
                {
                    string content = "";
                    using (StreamReader sr = new StreamReader(item)) { content = sr.ReadToEnd(); }
                    LoadHARContent(content, false);
                }
                ReorderLists();
            }
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            this.Messages = new List<ItemMessage>();
            lstUrls.Items.Clear();
            txtPattern.Enabled = true;
            btnParseData.Enabled = true;
        }
    }
}