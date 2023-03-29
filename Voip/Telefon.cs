using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using System.Collections;
using SIPVoipSDK;
using System.Text.RegularExpressions;
using System.Threading;

namespace Voip
{
    using ConnectionInfo = KeyValuePair<int, string>;
    using ConnectionsTbl = Dictionary<int, string>;

    public partial class Telefon : DevExpress.XtraEditors.XtraForm
    {

        private CAbtoPhone AbtoPhone = new CAbtoPhone();

        private string cfgFileName = "phoneCfg.ini";

        public const int LineCount = 6;
        private int m_curLineId = 1;

        ArrayList m_lineConnections = new ArrayList();

        private int m_lineIdWhereRecStarted = 0;

        private bool m_MP3RecordingEnabled = false;
        private bool m_AutoAnswerEnabled = false;

        public Telefon()
        {
            InitializeComponent();
        }

        protected bool ConfigurePhone()
        {
            //Assign event handlers
            this.AbtoPhone.OnInitialized += new _IAbtoPhoneEvents_OnInitializedEventHandler(this.AbtoPhone_OnInitialized);
            this.AbtoPhone.OnLineSwiched += new _IAbtoPhoneEvents_OnLineSwichedEventHandler(this.AbtoPhone_OnLineSwiched);
            this.AbtoPhone.OnEstablishedCall += new _IAbtoPhoneEvents_OnEstablishedCallEventHandler(this.AbtoPhone_OnEstablishedCall);
            this.AbtoPhone.OnIncomingCall += new _IAbtoPhoneEvents_OnIncomingCallEventHandler(this.AbtoPhone_OnIncomingCall);
            this.AbtoPhone.OnClearedCall += new _IAbtoPhoneEvents_OnClearedCallEventHandler(this.AbtoPhone_OnClearedCall);
            this.AbtoPhone.OnVolumeUpdated += new _IAbtoPhoneEvents_OnVolumeUpdatedEventHandler(this.AbtoPhone_OnVolumeUpdated);
            this.AbtoPhone.OnRegistered += new _IAbtoPhoneEvents_OnRegisteredEventHandler(this.AbtoPhone_OnRegistered);
            this.AbtoPhone.OnPlayFinished += new _IAbtoPhoneEvents_OnPlayFinishedEventHandler(this.AbtoPhone_OnPlayFinished);
            this.AbtoPhone.OnEstablishedConnection += new _IAbtoPhoneEvents_OnEstablishedConnectionEventHandler(this.AbtoPhone_OnEstablishedConnection);
            this.AbtoPhone.OnClearedConnection += new _IAbtoPhoneEvents_OnClearedConnectionEventHandler(this.AbtoPhone_OnClearedConnection);
            this.AbtoPhone.OnToneReceived += new _IAbtoPhoneEvents_OnToneReceivedEventHandler(this.AbtoPhone_OnToneReceived);
            this.AbtoPhone.OnTextMessageReceived += new _IAbtoPhoneEvents_OnTextMessageReceivedEventHandler(this.AbtoPhone_OnTextMessageReceived);
            this.AbtoPhone.OnTextMessageSentStatus += new _IAbtoPhoneEvents_OnTextMessageSentStatusEventHandler(AbtoPhone_OnTextMessageSentStatus);
            this.AbtoPhone.OnPhoneNotify += new _IAbtoPhoneEvents_OnPhoneNotifyEventHandler(this.AbtoPhone_OnPhoneNotify);
            this.AbtoPhone.OnRemoteAlerting2 += new _IAbtoPhoneEvents_OnRemoteAlerting2EventHandler(AbtoPhone_OnRemoteAlerting2);
            this.AbtoPhone.OnSubscribeStatus += new _IAbtoPhoneEvents_OnSubscribeStatusEventHandler(AbtoPhone_OnSubscribeStatus);
            this.AbtoPhone.OnSubscriptionNotify += new _IAbtoPhoneEvents_OnSubscriptionNotifyEventHandler(AbtoPhone_OnSubscriptionNotify);
            //Get config
            CConfig phoneCfg = AbtoPhone.Config;

            //Load config values from file
            phoneCfg.Load(cfgFileName);


            try
            {
                //Apply modified config
                AbtoPhone.ApplyConfig();


                AbtoPhone.Initialize();
            }
            catch (Exception e)
            {
                displayNotifyMsg(e.Message);
                return false;
            }

            return true;
        }
        public class ConnListBoxItem
        {
            public int handle;
            public string connection;

            public ConnListBoxItem(int _handle, string _connection)
            {
                handle = _handle;
                connection = _connection;
            }

            public override string ToString()
            {
                return this.connection;
            }
        }
        public class LineInfo
        {
            public LineInfo(int id)
            {
                m_id = id;
                m_conn = new ConnectionsTbl();
                m_bCalling = false;
                m_bCallEstablished = false;
                m_bCallHeld = false;
                m_bCallPlayStarted = false;
                m_usrInputStr = "";
            }

            public ConnectionsTbl m_conn;
            public int m_id;
            public int m_lastConnId;
            public bool m_bCalling;
            public bool m_bCallEstablished;
            public bool m_bCallHeld;
            public bool m_bCallPlayStarted;
            public string m_usrInputStr;
            public System.Windows.Forms.Timer m_callDurationTimer;
            public TimeSpan m_callTime;
            public string m_callTimeStr;
        }

        private Button getLineButton(int lineId)
        {
            return buttonLine1;
        }
        private void ChageControlsState(LineInfo li)
        {
            ChageLineCaption(li);

            buttonStartHangupCall.Text = li.m_bCallEstablished || li.m_bCalling ? "Durdur" : "Arama Başlat";

            buttonHoldRetrieve.Visible = li.m_bCallEstablished;
            buttonHoldRetrieve.Text = li.m_bCallHeld ? "Retrieve" : "Tut";

            buttonTransfer.Visible = li.m_bCallEstablished;
            buttonJoin.Visible = li.m_bCallEstablished;

            callDurationLabel.Visible = li.m_bCallEstablished;
            callDurationLabel.Text = li.m_callTimeStr;

            AddressBox.Enabled = li.m_bCallEstablished || li.m_bCalling ? false : true;

            buttonPlayStartStop.Text = li.m_bCallPlayStarted ? "PlayStop" : "Ses Dosyası Aç";
            buttonRecordStartStop.Text = (m_lineIdWhereRecStarted != 0) ? "RecStop" : "Kayıt";

            UInputLabel.Text = li.m_usrInputStr;
        }
        private void HighlightCurLine(int prevCurLine, int newCurLine)
        {
            Button prevBut = getLineButton(prevCurLine);
            Button newBut = getLineButton(newCurLine);

            prevBut.Font = new Font(prevBut.Font.FontFamily, prevBut.Font.Size, prevBut.Font.Style ^ FontStyle.Bold);
            newBut.Font = new Font(newBut.Font.FontFamily, newBut.Font.Size, newBut.Font.Style | FontStyle.Bold);
        }
        private void DisplayConnectionsAll(LineInfo lnInfo)
        {
            activeConnListbox.Items.Clear();
            foreach (ConnectionInfo it in lnInfo.m_conn) DisplayConnection(it);
        }
        private void AbtoPhone_OnLineSwiched(int lineId)
        {
            //Display line as pressed button
            HighlightCurLine(m_curLineId, lineId);

            //Remember
            m_curLineId = lineId;

            //Display connections of cur line
            LineInfo lnInfo = GetCurLine();
            DisplayConnectionsAll(lnInfo);

            //Show/Hide call controls
            ChageControlsState(lnInfo);
        }

        private void activateSDK_Click(object sender, EventArgs e)
        {
            Activation dlg = new Activation();
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            CConfig phoneCfg = AbtoPhone.Config;
            phoneCfg.LicenseUserId = dlg.m_licUserId;
            phoneCfg.LicenseKey = dlg.m_licKey;
            phoneCfg.Store(cfgFileName);
        }
        delegate void ActivateSDK_Delegate(object sender, EventArgs e);

        private void displayNotifyMsg(string msg)
        {
            Listbox.Items.Add(msg);
            Listbox.TopIndex = Listbox.Items.Count - 1;
        }
        private void AbtoPhone_OnInitialized(string Msg)
        {
            displayNotifyMsg(Msg);

            if (Msg.Contains("Hata"))
                BeginInvoke(new ActivateSDK_Delegate(activateSDK_Click), this, null);
        }

        private void OnCallDurationTimerEvent(object sender, EventArgs e)
        {
            //Get timer
            System.Windows.Forms.Timer timer = (System.Windows.Forms.Timer)sender;
            if (sender == null) return;
            int lineId = (int)timer.Tag;

            //Get line info
            LineInfo lnInfo = GetLine(lineId);
            if (lnInfo == null) return;

            //Increment duration
            lnInfo.m_callTime = lnInfo.m_callTime.Add(new TimeSpan(0, 0, 1));
            lnInfo.m_callTimeStr = "Çagrı süresi: " + lnInfo.m_callTime.ToString();

            //Display current duration
            if (lineId == m_curLineId) callDurationLabel.Text = lnInfo.m_callTimeStr;
        }
        private void startCallDurationTimer(LineInfo lnInfo)
        {
            if (lnInfo.m_callDurationTimer == null)
            {
                lnInfo.m_callDurationTimer = new System.Windows.Forms.Timer();
                lnInfo.m_callDurationTimer.Tick += new EventHandler(OnCallDurationTimerEvent);
                lnInfo.m_callDurationTimer.Tag = lnInfo.m_id;
                lnInfo.m_callDurationTimer.Interval = 1000;
            }

            lnInfo.m_callTime = new TimeSpan(0, 0, 0);
            lnInfo.m_callTimeStr = "Çagrı süresi: 00:00:00";

            lnInfo.m_callDurationTimer.Start();
        }
        private void AbtoPhone_OnEstablishedCall(string adress, int lineId)
        {
            //Update line state
            LineInfo lnInfo = GetLine(lineId);
            lnInfo.m_usrInputStr = "";
            lnInfo.m_bCallEstablished = true;
            lnInfo.m_bCalling = false;

            //Start call duration timer
            startCallDurationTimer(lnInfo);

            //Update controls (only when it's cur line event)
            if (lineId == m_curLineId)
            {
                //Display status
                displayNotifyMsg(adress);

                //Cange controls state
                ChageControlsState(lnInfo);
            }
            else
            {
                ChageLineCaption(lnInfo);
            }
        }


        private void AbtoPhone_OnIncomingCall(string adress, int lineId)
        {
            if (m_AutoAnswerEnabled == true) return;
            Gelen_Arama dlg = new Gelen_Arama();
            dlg.textBoxCaller.Text = adress;
            dlg.textBoxLine.Text = "Line" + lineId.ToString();
            if (dlg.ShowDialog(this) == DialogResult.Yes) AbtoPhone.AnswerCallLine(lineId);
            else AbtoPhone.RejectCallLine(lineId);
        }

        private void AbtoPhone_OnClearedCall(string Msg, int status, int lineId)
        {
            LineInfo lnInfo = GetLine(lineId);
            lnInfo.m_usrInputStr = "";
            lnInfo.m_bCallEstablished = false;
            lnInfo.m_bCalling = false;
            lnInfo.m_callTimeStr = "";
            if (lnInfo.m_callDurationTimer != null) lnInfo.m_callDurationTimer.Stop();

            if (lineId == m_curLineId)
            {
                displayNotifyMsg(Msg + ". Status: " + status.ToString());

                ChageControlsState(lnInfo);
            }
            else
            {
                ChageLineCaption(lnInfo);
            }
        }

        private void AbtoPhone_OnToneReceived(int tone, int connectionId, int lineId)
        {
            LineInfo lnInfo = GetLine(lineId);

            StringBuilder sb = new StringBuilder();
            sb.Append(lnInfo.m_usrInputStr);
            sb.Append((char)tone);
            lnInfo.m_usrInputStr = sb.ToString();

            if (lineId == m_curLineId) UInputLabel.Text = lnInfo.m_usrInputStr;
        }

        private void AbtoPhone_OnVolumeUpdated(int IsMicrophone, int level)
        {
            if (IsMicrophone == 0) spkVolumeBar.Value = level;
            else micVolumeBar.Value = level;
        }

        private void AbtoPhone_OnRegistered(string Msg)
        {
            displayNotifyMsg(Msg);
        }

        private void stopStartPlaying(bool bCalledByPlayFinishedEvent, int lineId)
        {
            LineInfo lnInfo = GetLine(lineId);
            if (bCalledByPlayFinishedEvent && !lnInfo.m_bCallPlayStarted) return;

            if (lnInfo.m_bCallPlayStarted)
            {
                AbtoPhone.StopPlayback();
                if (lineId == m_curLineId) buttonPlayStartStop.Text = "Play";
            }
            else
            {
                OpenFileDialog fileDlg = new OpenFileDialog();
                fileDlg.Multiselect = false;
                fileDlg.Filter = "Sound Files (*.wav)|*.wav|Sound Files (*.mp3)|*.mp3";
                if (fileDlg.ShowDialog(this) != DialogResult.OK) return;

                int succeded = AbtoPhone.PlayFile(fileDlg.FileName);
                if (succeded == 0) return;

                displayNotifyMsg("Oynatılıyor: " + fileDlg.FileName);
                buttonPlayStartStop.Text = "Durdur";
            }

            lnInfo.m_bCallPlayStarted = !lnInfo.m_bCallPlayStarted;

        }
        private void AbtoPhone_OnPlayFinished(string Msg)
        {
            string playStr = "Play Finished on Line: ";

            int idx = Msg.IndexOf(playStr);
            if (idx == 0)
            {
                string lineStr = Msg.Substring(playStr.Length);
                stopStartPlaying(true, int.Parse(lineStr));
            }

            displayNotifyMsg(Msg);
        }
        private void DisplayConnection(ConnectionInfo ci)
        {
            int itemIndex = activeConnListbox.Items.Add(new ConnListBoxItem(ci.Key, ci.Value));
            activeConnListbox.SelectedIndex = itemIndex;
        }

        private void AbtoPhone_OnEstablishedConnection(string addrFrom, string addrTo, int connectionId, int lineId)
        {
            LineInfo lnInfo = GetLine(lineId);
            string addr = lnInfo.m_bCalling ? addrTo : addrFrom;

            lnInfo.m_conn.Add(connectionId, addr);
            lnInfo.m_lastConnId = connectionId;

            if (lineId == m_curLineId) DisplayConnection(new ConnectionInfo(connectionId, addr));
        }

        private void RemoveConnection(int connectionId)
        {
            foreach (ConnListBoxItem t in activeConnListbox.Items)
            {
                if (t.handle == connectionId) { activeConnListbox.Items.Remove(t); break; }
            }

            int count = activeConnListbox.Items.Count;
            if (count >= 1) activeConnListbox.SelectedIndex = count - 1;
        }

        private bool GetSelectedConnection(out int connectionId)
        {
            connectionId = 0;
            int count = activeConnListbox.Items.Count;
            if (count == 0) return false;

            int selectedIndex = activeConnListbox.SelectedIndex;
            if (selectedIndex == -1) selectedIndex = count - 1;

            connectionId = ((ConnListBoxItem)activeConnListbox.Items[selectedIndex]).handle;
            return true;
        }
        private void ChageLineCaption(LineInfo li)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Line");
            sb.Append(li.m_id);
            if (li.m_bCallEstablished) sb.Insert(0, "[x]");

            getLineButton(li.m_id).Text = sb.ToString();

        }
        private void AbtoPhone_OnClearedConnection(int connectionId, int lineId)
        {
            LineInfo lnInfo = GetLine(lineId);
            lnInfo.m_conn.Remove(connectionId);
            lnInfo.m_lastConnId = 0;

            if (lineId == m_curLineId) RemoveConnection(connectionId);
        }


        private void AbtoPhone_OnTextMessageReceived(string from, string message)
        {
            displayNotifyMsg("'" + message + "' received from: " + from);
        }

        private void AbtoPhone_OnTextMessageSentStatus(string address, string reason, int bSuccess)
        {
            if (bSuccess != 0) displayNotifyMsg("Message gönderimi başarılı " + address + " Reason: " + reason);
            else displayNotifyMsg("Message gönderimi başarısız " + address + " Reason: " + reason);
        }

        private void AbtoPhone_OnPhoneNotify(string message)
        {
            displayNotifyMsg(message);
            Match match = Regex.Match(message, @"Redirect.*Connection: \d+");
            if (match.Success)
            {
                string connIdStr = Regex.Match(match.Value, @"\d+").Value;
                AbtoPhone.HangUp(int.Parse(connIdStr));
            }
        }

        private void AbtoPhone_OnRemoteAlerting2(int ConnectionId, int lineid, int responseCode, string reasonMsg)
        {
            string str = "Remote alerting: " + responseCode.ToString() + " " + reasonMsg;
            displayNotifyMsg(str);
        }

        void AbtoPhone_OnSubscribeStatus(int subscriptionId, int statusCode, string statusMsg)
        {
            string str = string.Format("OnVoiceMail: Not supported. StatusCode: {0}", statusCode);
            displayNotifyMsg(str);
        }

        void AbtoPhone_OnSubscriptionNotify(int subscriptionId, string StateStr, string NotifyStr)
        {
            string str = string.Format("OnVoiceMail: {0}", StateStr);
            displayNotifyMsg(str);
        }
        private void frmPhone_Load(object sender, EventArgs e)
        {
            displayNotifyMsg("Initializing...");
            bool bRes = ConfigurePhone();
            for (int i = 1; i <= LineCount; ++i) m_lineConnections.Add(new LineInfo(i));
            buttonLine1.Tag = 1;

            spkVolume.SetRange(0, 100);
            spkVolume.Value = AbtoPhone.PlaybackVolume;
            spkVolume.TickFrequency = 10;

            spkVolumeBar.Minimum = 0;
            spkVolumeBar.Maximum = Int16.MaxValue;

            micVolume.SetRange(0, 100);
            micVolume.Value = AbtoPhone.RecordVolume;
            micVolume.TickFrequency = 10;

            micVolumeBar.Minimum = 0;
            micVolumeBar.Maximum = Int16.MaxValue;

            muteSpeakerFlag.Checked = true;
            muteMicrophoneFlag.Checked = true;

            m_MP3RecordingEnabled = AbtoPhone.Config.MP3RecordingEnabled != 0;
            m_AutoAnswerEnabled = AbtoPhone.Config.AutoAnswerEnabled != 0;

            AcceptButton = buttonStartHangupCall;
        }
        private LineInfo GetCurLine()
        {
            return (LineInfo)m_lineConnections[m_curLineId - 1];
        }

        private LineInfo GetLine(int lineId)
        {
            return (LineInfo)m_lineConnections[lineId - 1];
        }        
        private void buttonStartHangupCall_Click(object sender, EventArgs e)
        {
            LineInfo lnInfo = GetCurLine();

            if (lnInfo.m_bCallEstablished || lnInfo.m_bCalling)
            {
                int connectionId;
                if (GetSelectedConnection(out connectionId) == true) AbtoPhone.HangUp(connectionId);
                else AbtoPhone.HangUpLastCall();
            }
            else
            {
                string address = AddressBox.Text;
                if (address.Length == 0) return;

                int idx = AddressBox.FindString(address, -1);
                if (idx == -1) AddressBox.Items.Add(address);

                displayNotifyMsg("Çalıyor...");

                lnInfo.m_bCalling = true;
                ChageControlsState(lnInfo);
                int connId = AbtoPhone.StartCall2(address);
            }
        }

        private void DTFM1_Click(object sender, EventArgs e)
        {
            Button b = (Button)sender;
            bool bDtmfSent = false;

            while (!bDtmfSent)
            {
                try
                {
                    AbtoPhone.SendToneEx(Convert.ToInt32(b.Tag), 200, 1, 1, 0);
                    bDtmfSent = true;
                }
                catch (Exception)
                {
                    Thread.Sleep(100);
                }
            }
        }

        private void spkVolume_Scroll(object sender, EventArgs e)
        {
            AbtoPhone.RecordVolume = micVolume.Value;
        }

        private void micVolume_Scroll(object sender, EventArgs e)
        {
            AbtoPhone.PlaybackVolume = spkVolume.Value;
        }

        private void muteSpeakerFlag_CheckedChanged(object sender, EventArgs e)
        {
            AbtoPhone.PlaybackMuted = muteSpeakerFlag.Checked ? 0 : 1;
        }

        private void muteMicrophoneFlag_CheckedChanged(object sender, EventArgs e)
        {
            AbtoPhone.RecordMuted = muteMicrophoneFlag.Checked ? 0 : 1;
        }

        private void buttonSendText_Click(object sender, EventArgs e)
        {

        }

        private void buttonTransfer_Click(object sender, EventArgs e)
        {

        }

        private void buttonJoin_Click(object sender, EventArgs e)
        {

        }

        private void buttonPlayStartStop_Click(object sender, EventArgs e)
        {
            stopStartPlaying(false, m_curLineId);
        }

        private void buttonRecordStartStop_Click(object sender, EventArgs e)
        {
            LineInfo lnInfo = GetCurLine();
            if (m_lineIdWhereRecStarted != 0)
            {
                AbtoPhone.StopRecording();
                buttonRecordStartStop.Text = "Kayıt";
                displayNotifyMsg("Kayıt durduruldu");
                m_lineIdWhereRecStarted = 0;
            }
            else
            {
                SaveFileDialog fileDlg = new SaveFileDialog();
                fileDlg.Filter = (m_MP3RecordingEnabled == true) ? "Sound Files (*.mp3)|*.mp3" : "Sound Files (*.wav)|*.wav";
                fileDlg.OverwritePrompt = true;
                if (fileDlg.ShowDialog(this) != DialogResult.OK) return;

                AbtoPhone.StartRecording(fileDlg.FileName);
                buttonRecordStartStop.Text = "Durdur";
                displayNotifyMsg("Kayıt dosyası: " + fileDlg.FileName);

                m_lineIdWhereRecStarted = m_curLineId;
            }
        }

        private void buttonHoldRetrieve_Click(object sender, EventArgs e)
        {
            LineInfo lnInfo = GetCurLine();

            AbtoPhone.HoldRetrieveCall(lnInfo.m_id);

            buttonHoldRetrieve.Text = lnInfo.m_bCallHeld ? "Tut" : "Sürdür";

            lnInfo.m_bCallHeld = !lnInfo.m_bCallHeld;
        }
    }
}

 