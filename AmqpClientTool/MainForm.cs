﻿using Amqp;
using Amqp.Framing;
using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace AmqpClientTool
{
    public partial class MainForm : Form
    {
        class SenderLinkWrapper
        {
            public ISenderLink SenderLink { get; set; }
            public string Name
            {
                get
                {
                    return SenderLink.Name;
                }
                set
                {
                }
            }
            public SenderLinkWrapper(ISenderLink sender)
            {
                SenderLink = sender;
            }
        }

        class ReceiverLinkWrapper
        {
            public IReceiverLink ReceiverLink { get; set; }
            public string Name
            {
                get
                {
                    return ReceiverLink.Name;
                }
                set
                {
                }
            }
            public ReceiverLinkWrapper(IReceiverLink receiver)
            {
                ReceiverLink = receiver;
            }
        }

        private Connection connection;
        private Session session;
        private BindingList<SenderLinkWrapper> senders = new BindingList<SenderLinkWrapper>();
        private BindingList<ReceiverLinkWrapper> receivers = new BindingList<ReceiverLinkWrapper>();
        ISenderLink senderLink;

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.Save();
        }

        private void Open_Click(object sender, EventArgs e)
        {
            string scheme = "AMQP";
            if (useTls.Checked)
            {
                scheme = "AMQPS";
            }
            Address amqpAddress = new Address(host.Text, int.Parse(port.Text),
                username.Text, password.Text, scheme: scheme);
            connection = new Connection(amqpAddress);
            connection.Closed += Connection_Closed;
            session = new Session(connection);
            sendersListBox.DataSource = senders;
            sendersListBox.DisplayMember = "Name";
            sendersListBox.ValueMember = "Name";
            receiversListBox.DataSource = receivers;
            receiversListBox.DisplayMember = "Name";
            receiversListBox.ValueMember = "Name";
            status.Text = "Opened";
        }

        private void Connection_Closed(IAmqpObject sender, Amqp.Framing.Error error)
        {
            status.Text = "Closed";
            senderLink = null;
            session = null;
            connection.Closed -= Connection_Closed;
            connection = null;
            for(int i = senders.Count - 1; i >= 0; i--)
            {
                senders.RemoveAt(i);
            }
            for (int i = receivers.Count - 1; i >= 0; i--)
            {
                receivers.RemoveAt(i);
            }
            BeginInvoke(new MethodInvoker(() =>
            {
                sendersListBox.DataSource = null;
                receiversListBox.DataSource = null;
            }));
        }

        private void Close_Click(object sender, EventArgs e)
        {
            if (senderLink != null)
            {
                senderLink.Close();
                senderLink = null;
            }
            if (session != null)
            {
                session.Close();
                session = null;
            }
            if (connection != null)
            {
                connection.Close();
            }
        }

        private void Send_Click(object sender, EventArgs e)
        {
            if (senderLink == null)
            {
                AddSender();
            }
            object body;
            if (input.BinaryChecked)
            {
                body = input.BinaryValue;
            }
            else
            {
                body = input.TextValue;
            }
            Amqp.Message message = new Amqp.Message(body);
            message.Header = new Header()
            {
                Durable = true
            };
            message.Properties = new Amqp.Framing.Properties();
            if (!string.IsNullOrWhiteSpace(subject.Text))
            {
                message.Properties.Subject = subject.Text;
            }
            try
            {
                senderLink.Send(message, (s, m, outcome, state) =>
                {

                }, null);
            }
            catch (AmqpException ex)
            {
                MessageBox.Show(this, ex.Message);
                return;
            }
        }

        private async void AddReceiver_Click(object sender, EventArgs e)
        {
            if (session == null)
            {
                return;
            }
            IReceiverLink receiverLink;
            Source receiveSource = new Source()
            {
                Address = receiverLinkAddress.Text,
                //Durable = 1,
            };
            try
            {
                receiverLink = new ReceiverLink(session, receiverLinkName.Text, receiveSource, (link, attach) =>
                {
                });
                receivers.Add(new ReceiverLinkWrapper(receiverLink));
            }
            catch(AmqpException ex)
            {
                MessageBox.Show(this, ex.Message);
                return;
            }
            while (receiverLink != null && !receiverLink.IsClosed)
            {
                Amqp.Message message;
                try
                {
                    message = await receiverLink.ReceiveAsync(TimeSpan.FromSeconds(5));
                    if (message == null)
                    {
                        continue;
                    }
                    receiverLink.Accept(message);
                }
                catch (AmqpException)
                {
                    return;
                }
                string subject = string.Empty;
                if (!string.IsNullOrEmpty(message.Properties.Subject))
                {
                    subject = $"received message with subject { message.Properties.Subject} ";
                }
                output.AppendText($"Receiver {receiverLink.Name} {subject}on {DateTime.Now}:{Environment.NewLine}");
                if (message.Body is byte[])
                {
                    byte[] data = (byte[])message.Body;
                    output.AppendBinary(data, data.Length);
                }
                else
                {
                    output.AppendText(message.Body.ToString());
                }
                output.AppendText($"{Environment.NewLine}");
                output.AppendText($"{Environment.NewLine}");
            }
        }

        private void RemoveReceiver_Click(object sender, EventArgs e)
        {
        }

        private void AddSender_Click(object sender, EventArgs e)
        {
            AddSender();
        }

        private void AddSender()
        {
            if (session == null)
            {
                return;
            }
            Target sendTarget = new Target()
            {
                Address = senderLinkAddress.Text,
                //Durable = 1,
            };
            try
            {
                senderLink = new SenderLink(session, senderLinkName.Text, sendTarget, (link, attach) =>
                {
                });
                senders.Add(new SenderLinkWrapper(senderLink));
            }
            catch (AmqpException ex)
            {
                MessageBox.Show(this, ex.Message);
                return;
            }
        }

        private void RemoveSender_Click(object sender, EventArgs e)
        {

        }

        private void ReceiversListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
        }

        private void SendersListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (sendersListBox.SelectedItem == null)
            {
                return;
            }
            senderLink = ((SenderLinkWrapper)sendersListBox.SelectedItem).SenderLink;
        }
    }
}
