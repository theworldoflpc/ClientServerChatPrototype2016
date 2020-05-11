/*
* FILE : MainWindow.xaml.cs
* PROJECT : PROG2120 - Windows and Mobile Programming - Assignment #4
* PROGRAMMERS : JMarkic and lpcSetProg
* FIRST VERSION : 2016-11-15
* DESCRIPTION :
* 
* This file contains the source code for a chat system that allows two or more 
* users to communicate with one another on multiple computers. The communication between the two 
* people is managed by a separate program called the 'Server'. Users have the option of private chats,
* broadcast chats, or group chats, depending on how they make their selection of users. All users
* of the chat program may end their session gracefully without disrupting the server. 
* 
* NOTE: Microsoft Message Queuing (MSMQ) technology  must be enabled in Windows to run. 
*/


using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System;
using System.Messaging;
using System.Threading;


namespace WpfApplication1
{
 
    /*
    * Name: MainWindow : Window
    * Purpose: Interaction logic for MainWindow.xaml
    */
    public partial class MainWindow : Window
    {
      
        //Attributes
        static private string mQueueName = @".\private$\messageQueue";
        static private MessageQueue messageQueue;
        static private MessageQueue serverQueue;
        private Packet requestBuffer = new Packet();
        private Packet guiPacketBuffer = new Packet();
        static private string packetBuffer;
        string messageText;
        bool userNameSelected;
        bool connectedStatus;
       
        public object TimeUnit { get; private set; }


        /*
        * Method     : MainWindow()
        * Description: Creates a Message Queue, sets up a thread
        *              to continually listen for incoming requests
        *              and spawns the client GUI for the chat program
        * Parameters : N/A
        * Returns    : N?A
        */
        public MainWindow()
        {
            //if message queue doesn't exist spawn one
            if (!MessageQueue.Exists(mQueueName))
            {
                messageQueue = MessageQueue.Create(mQueueName);
            }
            else
            {
                //connect to queue
                messageQueue = new MessageQueue(mQueueName);
                //purge old request hanging around
                messageQueue.Purge();
            }
            // thread readMessages to continually listen to incoming requests
            Thread incomingMessages = new Thread(readMessages);
            //start thread
            incomingMessages.Start();
      

            //initialize client GUI window
            InitializeComponent();
            this.FontFamily = new FontFamily("Arial");

        } //END OF MAINWINDOW()



      /*
      * METHOD : readMessages()
      *
      * DESCRIPTION : This method unpackages message written to to the message queue
      *               it sits and waits for requests, if a request is made, spawn a thread
      *               to process request and allow readMessages() to process more incoming
      *               requests
      * 
      * PARAMETERS : N/A 
      *
      * RETURNS    : N/A 
      */
        public void readMessages()
        {
            bool finished = false;
            // XML formatter
            messageQueue.Formatter = new XmlMessageFormatter(new Type[] { typeof(string) });

            // continually read and process requests
            while (!finished)
            {
                try
                {

                    string packet = (string)messageQueue.Receive().Body;
                    // if request isn't null
                    if (packet != null)
                    {
                        // deconstruct message
                        DeconstructMessage(packet);
                        Packet serverRequest = requestBuffer;
                        // process request
                        Thread processRequests = new Thread(ProcessRequest);
                        processRequests.Start(serverRequest);
                        if (packet == "Shutdown")
                        {
                            finished = true;
                        }
                    }
                }
                // catch exceptions
                catch (MessageQueueException mqex)
                {
                    Console.WriteLine("MQ Execption: " + mqex.Message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception:" + ex.Message);
                }
                messageQueue.Close();
            }
        }



        /*
       * METHOD : DeconstructMessage()
       *
       * DESCRIPTION : Deconstructs requests made to the server from a string
       *               and repackages into an object for ease of use and
       *               interpretation
       * 
       * PARAMETERS : string : packet 
       *
       * RETURNS    : N/A 
       */
        public void DeconstructMessage(string packet)
        {
            string[] messageComposition = packet.Split(',');

            requestBuffer.Alias = messageComposition[0];
            requestBuffer.MessageType = messageComposition[1];
            requestBuffer.ClientMachine = messageComposition[2];
            requestBuffer.RequestToChat = messageComposition[3];
            requestBuffer.Message = messageComposition[4];
        }



        /*
       * METHOD : ProcessRequest()
       *
       * DESCRIPTION : Determines which type of request a client is making
       *               A client can make a Connect, Disconnect, Private, Group,
       *               Or Broadcast request
       * 
       * PARAMETERS : object : request 
       *
       * RETURNS    : N/A 
       */
        public void ProcessRequest(object request)
        {
            // cast object to a workable Packet object
            Packet serverRequest = (Packet)request;
            // convert request type to a char
            char messageType = Convert.ToChar(serverRequest.MessageType);
            switch (messageType)
            {
                case 'C':
                case 'D':
                    // update client list if request is connect/disconnect
                    UpdateClientList(serverRequest);
                    break;
                case 'P':
                case 'B':
                case 'G':
                    // process message further otherwise
                    ProcessMessage(serverRequest);
                    break;
                default:
                    break;
            }
        }



        /*
        * METHOD : UpdateClientList()
        *
        * DESCRIPTION : This method updates the client list box  (removing and adding)  
        * depending on if there is currently client aliases (usesernames) in the list box. 
        * PARAMETERS : object : request : packet determining if connect or disconnect 
        *        
        *
        * RETURNS    : N/A 
        */
        //Update Client List
        public void UpdateClientList(object request)
        {
            // cast object to a workable Packet object
            Packet serverRequest = (Packet)request;
            //if a clientlistbox exists
            if (ClientListBox != null)
            {
                //check type of server request
                    if (serverRequest.MessageType.Equals("C"))
                    {
                        // if the client doesnt exist
                        if (!(ClientListBox.Items.Contains(serverRequest.Alias)))
                        {
                            //add new client to list
                            this.Dispatcher.Invoke(() =>
                            {
                                ClientListBox.Items.Add(serverRequest.Alias);
                            });
                        }
                    }
                else
                {  //if the the client does exist 
                    this.Dispatcher.Invoke(() =>
                    {   //create an invoke for updating UI elements in the WPF by accessing another processes recourses 
                        Array clientSelected = Array.CreateInstance(typeof(string), ClientListBox.Items.Count);
                        ClientListBox.Items.CopyTo(clientSelected, 0);

                        // removes client lists if client disconnects 
                        for (int i = 0; i < clientSelected.Length; i++)
                        {
                            string updateWho = serverRequest.Alias;
                            if (updateWho.Equals(clientSelected.GetValue(i)))
                            {
                                this.Dispatcher.Invoke(() =>
                                {
                                    ClientListBox.Items.RemoveAt(i);
                                });
                            }
                        }
                    });
                }
            } // if server requests a disconnect
                else if (serverRequest.MessageType.Equals("D"))
                {   //and the client exists in list
                    if ((ClientListBox.Items.Contains(serverRequest.Alias)))
                    {   //find the client and delete them
                        this.Dispatcher.Invoke(() =>
                        {
                            Array clientSelected = Array.CreateInstance(typeof(string), ClientListBox.Items.Count);
                            ClientListBox.Items.CopyTo(clientSelected, 0);

                            // removes client lists if client disconnects 
                            for (int i = 0; i < clientSelected.Length; i++)
                            {
                                string updateWho = serverRequest.Alias;
                                if (updateWho.Equals(clientSelected.GetValue(i)))
                                {
                                    ClientListBox.Items.RemoveAt(i);
                                }
                            }
                        });
                    }
                }
        }



        /*
        * METHOD : WriteToQueue()
        *
        * DESCRIPTION : this method sends a request to the server Queue
        * using the messagequeue class .send and .close properties.
        * PARAMETERS : string : data  : 
        *
        * RETURNS    : string : result : returns 'ok' status 
        */
        static public string WriteToQueue(string data)
        {
            string result = "OK";

            // Send a message with the data object to the queue
            serverQueue.Send(data);

            //Free all resources allocated by the MessageQueue class 
            serverQueue.Close(); 

            return result; //return 'ok' status 
        }



        /*
        * METHOD : UpdateMasterList()
        *
        * DESCRIPTION : this sends a request to the server to update
        *               its master list.
        * 
        * PARAMETERS : string : connectionStatusFlag  : signals the status 'Disconnect' or 'Connect'
        *
        * RETURNS    : N/A
        */
        public void UpdateMasterList(string connectionStatusFlag)
        {

            string serverName = ServerNameTextBox.Text; // server to send too
            serverQueue = new MessageQueue("FormatName:Direct=OS:" + serverName + "\\private$\\messageQueue"); // queue of the server
            requestBuffer.Alias = userNameTextBox.Text; // alias of client
            requestBuffer.MessageType = connectionStatusFlag; //message type -- connect - or disconnect 
            requestBuffer.ClientMachine = Environment.MachineName; // client machine name
            string packetBuffer = requestBuffer.Alias + "," + requestBuffer.MessageType + "," + requestBuffer.ClientMachine + "," + "null" + "," + "null"; //format message
            WriteToQueue(packetBuffer); //write to server queue

        }



        /*
        * METHOD : ProcessMessage()
        *
        * DESCRIPTION : This method processes incoming messages from the server
        * 
        * PARAMETERS : string : request  : 
        *
        * RETURNS    : N/A
        */
        public void ProcessMessage(object request)
        {
            Packet serverRequest = (Packet)request; //convert object to useable packets
            string messageSender = serverRequest.Alias; //get alias
            string message = serverRequest.Message; // get message
            string chatMessage = messageSender + " says: " + message + "\n"; //format message in prep to write to textbox


            //Determine which message is incoming 
            //if its private chat
            if (serverRequest.MessageType.Equals("P"))
            {
                WriteToPrivateChatBox(chatMessage);
            }
            // if it is group chat
            else if (serverRequest.MessageType.Equals("G"))
            {
                WriteToGroupChatBox(chatMessage);
            }
            // if it is broadcast chat 
            else if (serverRequest.MessageType.Equals("B"))
            {
                WriteToBroadChatBox(chatMessage);
            }
        }


        /*
        * METHOD : WriteToPrivateChatBox()
        *
        * DESCRIPTION : Append the incoming user chat to the 
        * Private chat box. 
        * PARAMETERS : string : chatMessage 
        *
        * RETURNS    : N/A 
        */
        public void WriteToPrivateChatBox(string chatMessage)
        {
            this.Dispatcher.Invoke(() =>
            {
                PrivateChatTabTextBox.AppendText(chatMessage);
            });
        }


        /*
        * METHOD : WriteToGroupChatBox()
        *
        * DESCRIPTION : Write incoming group chat message to the textbox in the 
        * groupchatbox tab. 
        * PARAMETERS : string : chatMessage 
        *
        * RETURNS    : N/A 
        */
        public void WriteToGroupChatBox(string chatMessage)
        {
            this.Dispatcher.Invoke(() =>
            {
                GroupChatTabTextBox.AppendText(chatMessage);
            });

        }


        /*
        * METHOD : WriteToBroadChatBox()
        *
        * DESCRIPTION : Write incoming group chat message to the textbox in the 
        * broadchatbox tab. 
        * 
        * PARAMETERS : string : chatMessage 
        *
        * RETURNS    : N/A 
        */
        public void WriteToBroadChatBox(string chatMessage)
        {
            this.Dispatcher.Invoke(() =>
            {
                BroadcastTabTextBox.AppendText(chatMessage);
            });

        }


        /*
        * EVENT : button2_Copy_Click()
        *
        * DESCRIPTION : This button click event will clear the message field text box. 
        * If the user has any text written in the text box it will prompt user before they 
        * clear the content. 
        * PARAMETERS : object : sender
        *            : RoutedEventArgs : e
        *
        * RETURNS    : N/A 
        */
        private void button2_Copy_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(MessageField.Text))
            {
                MessageField.Clear(); //clear the message field. 
            }
            else
            {
                MessageBoxResult result = MessageBox.Show("Would you like to clear your current message?", "Compose Message", MessageBoxButton.YesNo);
                switch (result)
                {
                    //If user selects "Yes" in message box
                    case MessageBoxResult.Yes:
                        MessageField.Clear();
                        break;
                    //If user selects "No" in message box
                    case MessageBoxResult.No:
                        break;
                }
            }
        }


        /* 
        * EVENT : userNameTextBox_TextChanged()  
        *
        * DESCRIPTION : This method is as a safegaurd/validator for the username to not 
        * contain any of the same characters as packet message. As empty space, |, and , all
        * are used as delimiters for packet message.
        * PARAMETERS : object : sender
        *            : RoutedEventArgs : e
        *
        * RETURNS    : N/A 
        */
        private void userNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (userNameTextBox.Text.Contains(" "))
            {
                MessageBox.Show("No ' spaces ' in user name.");
                userNameTextBox.Clear();
                userNameTextBox.Focus();
            }
            else if (userNameTextBox.Text.Contains("|"))
            {
                MessageBox.Show("No ' | ' in user name.");
                userNameTextBox.Clear();
                userNameTextBox.Focus();
            }
            else if (userNameTextBox.Text.Contains(","))
            {
                MessageBox.Show("No ' , ' in user name.");
                userNameTextBox.Clear();
                userNameTextBox.Focus();
            }

            
        }

        /* 
        * EVENT : client_button_Click()  
        *
        * DESCRIPTION : This event is activated when user inputs a 
        * client name. If the user name is not activated it will trigger a series of 
        * formatting events to the GUI. 
        * PARAMETERS : object : sender
        *            : RoutedEventArgs : e
        *
        * RETURNS    : N/A 
        */
        private void client_button_Click(object sender, RoutedEventArgs e)
        {   
            if (!string.IsNullOrWhiteSpace(userNameTextBox.Text))
            {
                userNameConnectionFormatingEvents();
                ServerNameTextBox.Focus();
            }
            else
            {
                MessageBox.Show("Please input a proper user name (Ex: JohnnyUtah30)");
            }
        }


        /* 
        * EVENT : server_button_Click()  
        *
        * DESCRIPTION : This event is activated when user inputs a 
        * server name. It is intended to let the user know they have connected 
        * to the server. It will end by focusing on the message field allowing the 
        * user to write in their intended message to other clients. 
        * PARAMETERS : object : sender
        *            : RoutedEventArgs : e
        *
        * RETURNS    : N/A 
        */
        public void server_button_Click(object sender, RoutedEventArgs e)
        {
            if (userNameSelected == true)
            {
                if (!string.IsNullOrWhiteSpace(ServerNameTextBox.Text))
                {
                    //TAB STUFF
                    connectedStatus = true;

                    //Delay for 1 second 
                    System.Threading.Thread.Sleep(1000);

                    //Insert new name to list box 
                    UpdateMasterList("C");

                    //Trigger series of connection events 
                    connectionServerFieldsFormattingEvents();

                    //Trigger series of tab formatting events
                    connectionServerTabFormattingEvents();

                    //Focus on message field 
                    MessageField.Focus();

                }
                else
                {
                    MessageBox.Show("Please input a proper server name (Ex. 2A213-A05)");
                }
            }
            else 
            {
                MessageBox.Show("You cannot connect to a server without a user name selected.");
            }
         
        }


        /* 
        * EVENT : ServerNameTextBox_TextChanged()  
        *
        * DESCRIPTION : This method is as a safegaurd/validator for the servername (machine name) to not 
        * contain any of the same characters as packet message. As empty space, |, and , all
        * are used as delimiters for packet message.
        * 
        * PARAMETERS : object : sender
        *            : RoutedEventArgs : e
        *
        * RETURNS    : N/A 
        */
        private void ServerNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ServerNameTextBox.Text.Contains(" "))
            {
                MessageBox.Show("No ' spaces ' in user name.");
                ServerNameTextBox.Clear();            
                ServerNameTextBox.Focus();
            }
            else if (ServerNameTextBox.Text.Contains("|"))
            {
                MessageBox.Show("No ' | ' in user name.");
                ServerNameTextBox.Clear();
                ServerNameTextBox.Focus();
            }
            else if (ServerNameTextBox.Text.Contains(","))
            {
                MessageBox.Show("No ' , ' in user name.");
                ServerNameTextBox.Clear();
                ServerNameTextBox.Focus();
            }
        }


        /*
        * EVENT : client_button_Click()  
        *
        * DESCRIPTION : This method is meant as a safegaurd against user choosing to 
        * disconnect from server locally on their client. If they choose 'yes' the GUI
        * will be updated visually.
        * 
        * PARAMETERS : object : sender
        *            : RoutedEventArgs : e
        *
        * RETURNS    : N/A 
        */
        private void button3_Click(object sender, RoutedEventArgs e)
        {
                //Prompt user with message box if they choose to disconnect from server. They might not want to completelty end it. 
                MessageBoxResult result = MessageBox.Show("Would you like to disconnect from Conestoga Local Chat Service?", "Disconnect Warning", MessageBoxButton.YesNo);
                switch (result)
                {
                    //If user selects "Yes" in message box
                    case MessageBoxResult.Yes:
                    //Delay process for 1 second 
                    System.Threading.Thread.Sleep(1000);

                    connectedStatus = false;

                    UpdateMasterList("D"); //disconnected status flag 
                    ClientListBox.Items.Clear();

                    //Series of events are enacted for forms and fields 
                    disconnectionServerFieldsFormattingEvents();


                    //Series of events are enacted for tabs forms and fields 
                    disconnectionServerTabFormattingEvents();

                    //Back to default status, but user is disconnected
                        break;

                //If user selects "No" in message box
                    case MessageBoxResult.No:
                        break; 
                }
        }


        /* 
        * EVENT : client_button_Click()  
        *
        * DESCRIPTION : this method clears the
        *  user name text box.
        * 
        * PARAMETERS : object : sender
        *            : RoutedEventArgs : e
        *
        * RETURNS    : N/A 
        */
        private void button3_Click_1(object sender, RoutedEventArgs e)
        {
            userNameTextBox.Clear();
        }



  


       /* 
       * EVENT : Clear_Click()  
       *
       * DESCRIPTION : this message clears the message field
       * if user clicks clear 
       * PARAMETERS : object : sender
       *            : RoutedEventArgs : e
       *
       * RETURNS    : N/A 
       */
        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            MessageField.Clear();
        }

        

        /* 
        * EVENT : button2_Click()  -- Send button is clicked 
        *
        * DESCRIPTION : Determines which chat tab will recieve the message the local 
        * user types. 
        * PARAMETERS : object : sender
        *            : RoutedEventArgs : e
        *
        * RETURNS    : N/A 
        */
        public void button2_Click(object sender, RoutedEventArgs e)
        {
            if (connectedStatus == true)
            {
                if (!string.IsNullOrEmpty(MessageField.Text))
                {
                    messageText = MessageField.Text;
                    //clear message field so it appears as though text as been sent and user can write again
                    MessageField.Clear();

                    if (ClientListBox.SelectedItems.Count > 0)
                    {
                        if (guiPacketBuffer.MessageType.Equals("P"))
                        {
                            writeToPrivateChatTab(messageText);
                        }
                        else if (guiPacketBuffer.MessageType.Equals("G"))
                        {
                            writeToGroupChatTab(messageText);
                        }
                        else if (guiPacketBuffer.MessageType.Equals("B"))
                        {
                            writeToBroadcastChatTab(messageText);
                        }
                        //focus back on message field for user to write again
                        MessageField.Focus();

                    }
                    else
                    {
                        MessageBox.Show("You cannot chat if no clients are selected.");
                    }
                }
                else
                {
                    MessageBox.Show("Please compose a message.");
                }
            }
            else //there is no connection so you can't write anything 
            {
                MessageBox.Show("You need to be connected to a server to write messages.");

            }    // end of if 
        }


        /* 
        * METHOD : connectionServerFieldsFormattingEvents()  
        *
        * DESCRIPTION : Series of events are enabled and activated when 
        * server is connected. 
        * PARAMETERS : N/A
        *
        * RETURNS    : N/A 
        */
        public void connectionServerFieldsFormattingEvents()
        {
            //Graphics and Buttons Formatting - Signal Connected
            ServerNameTextBox.Foreground = Brushes.Gold;
            ServerNameTextBox.Text = ServerNameTextBox.Text;

            ServerNameTextBox.IsReadOnly = true;
            ConnectedButton.IsEnabled = true;
            DisconnectButton.IsEnabled = true;
            checkServerName.IsEnabled = false;
            DisconnectedButton.IsEnabled = false;

            selectAllButton.IsEnabled = true;
            selectButton.IsEnabled = true;
            Deselect.IsEnabled = true;

            ServerNameTextBox.Background = Brushes.ForestGreen;
            ConnectedButton.Foreground = Brushes.Gold;
            ServerNameTextBox.TextAlignment = TextAlignment.Center;

        }


        /* 
        * METHOD : userNameConnectionFormatingEvents()  
        *
        * DESCRIPTION : A series of formating events are triggerd 
        * which update GUI for user status of connection.
        * 
        * PARAMETERS : N/A
        *
        * RETURNS    : N/A 
        */
        public void userNameConnectionFormatingEvents()
        {
            //Format user name in GUI 
            userNameTextBox.Foreground = Brushes.Gold;
            userNameTextBox.IsReadOnly = true;
            checkUserName.IsEnabled = false;
            ClearButton1.IsEnabled = false;
            userNameSelected = true;
            userNameTextBox.Background = Brushes.ForestGreen;
            System.Threading.Thread.Sleep(1000);
            userNameTextBox.TextAlignment = TextAlignment.Center;

        }


        /* 
        * METHOD : connectionServerTabFormattingEvents()  
        *
        * DESCRIPTION : A series of formating events for the chat tabs are triggerd 
        * which update GUI for user status of connection.
        * 
        * PARAMETERS : 
        *
        * RETURNS    : N/A 
        */
        public void connectionServerTabFormattingEvents()
        {
            ///Clear all chat text in message terminal
            PrivateChatTabTextBox.Clear();
            GroupChatTabTextBox.Clear();
            BroadcastTabTextBox.Clear();

            tabConnectionStatusOnline();
            ///Tab Formatting Colour fonts Events 
            PrivateChatTab.Foreground = Brushes.Black;
            GroupChatTab.Foreground = Brushes.Black;
            BroadcastChatTab.Foreground = Brushes.Black;

        }


        /* 
        * METHOD : disconnectionServerFieldsFormattingEvents()  
        *
        * DESCRIPTION :  A series of formating events are triggerd 
        * which update GUI for user status of disconnection.
        * 
        * PARAMETERS : 
        *
        * RETURNS    : N/A 
        */
        public void disconnectionServerFieldsFormattingEvents()
        {
            SendButton.IsEnabled = false;

            DisconnectedButton.IsEnabled = true;
            checkUserName.IsEnabled = true;
            ClearButton1.IsEnabled = true;
            checkServerName.IsEnabled = true;
            DisconnectButton.IsEnabled = false;
            userNameSelected = false;
            ConnectedButton.IsEnabled = false;
            ServerNameTextBox.IsReadOnly = false;
            userNameTextBox.IsReadOnly = false;

            selectAllButton.IsEnabled = false;
            selectButton.IsEnabled = false;
            Deselect.IsEnabled = false; 


            ServerNameTextBox.Background = SystemColors.WindowBrush;
            userNameTextBox.Background = SystemColors.WindowBrush;
            ServerNameTextBox.Foreground = Brushes.Black;
            userNameTextBox.Foreground = Brushes.Black;
            userNameTextBox.Clear();
            ServerNameTextBox.Clear(); //
            ServerNameTextBox.TextAlignment = TextAlignment.Left;
            userNameTextBox.TextAlignment = TextAlignment.Left;
            userNameTextBox.Focus();

        }


        /* 
        * METHOD : disconnectionServerTabFormattingEvents()  
        *
        * DESCRIPTION : A series of formating events for the chat tabs are triggerd 
        * which update GUI for user status of disconnection.
        * 
        * PARAMETERS : N/A
        *
        * RETURNS    : N/A 
        */
        public void disconnectionServerTabFormattingEvents()
        {
            ///Tab Formatting Colour fonts Events
            PrivateChatTabTextBox.Foreground = Brushes.DarkSlateGray;
            GroupChatTabTextBox.Foreground = Brushes.DarkSlateGray;
            BroadcastTabTextBox.Foreground = Brushes.DarkSlateGray;

            tabConnectionStatusOffline();

            //Focus back on user name input for user to rewrite input 
            userNameTextBox.Focus();
        }


        /* 
         * METHOD : writeToPrivateChatTab()  
         *
         * DESCRIPTION : Display message field text string to private chat text box 
         * and relayed it to queue.
         * PARAMETERS : STRING : messageText
         *
         * RETURNS    : N/A 
         */
        public void writeToPrivateChatTab(string messageText)
        {
            PrivateChatTab.Focus();
            PrivateChatTabTextBox.AppendText("You Say: " + messageText + "\n"); //Allows text to be displayed multiline 
            packetBuffer = userNameTextBox.Text + "," + guiPacketBuffer.MessageType + "," + Environment.MachineName + "," + messageText + "," + guiPacketBuffer.Alias;
            WriteToQueue(packetBuffer); //write to queue 
        }



        /* 
        * METHOD : writeToGroupChatTab()  
        *
        * DESCRIPTION : Display message field text string to group chat text box 
         * and relayed it to queue.
        * 
        * PARAMETERS : STRING : messageText
        *
        * RETURNS    : N/A 
        */
        public void writeToGroupChatTab(string messageText)
        {
            GroupChatTab.Focus();
            GroupChatTabTextBox.AppendText("You Say: " + messageText + "\n");
            packetBuffer = userNameTextBox.Text + "," + guiPacketBuffer.MessageType + "," + Environment.MachineName + "," + messageText + "," + guiPacketBuffer.RequestToChat;
            WriteToQueue(packetBuffer);
        }



        /* 
        * METHOD : writeToBroadcastChatTab()  
        *
        * DESCRIPTION : Display message field text string to broadcast chat text box 
         * and relayed it to queue.
        * 
        * PARAMETERS : STRING : messageText
        *
        * RETURNS    : N/A 
        */
        public void writeToBroadcastChatTab(string messageText)
        {
            BroadcastChatTab.Focus();
            BroadcastTabTextBox.AppendText("You Say: " + messageText + "\n");
            packetBuffer = userNameTextBox.Text + "," + guiPacketBuffer.MessageType + "," + Environment.MachineName + "," + messageText + "," + guiPacketBuffer.RequestToChat;
            WriteToQueue(packetBuffer);
        }



        /* 
        * METHOD : tabConnectionStatusOnline()  
        *
        * DESCRIPTION : Informs the user the their chat tabs are  activated.
        * 
        * PARAMETERS : 
        *
        * RETURNS    : N/A 
        */
        public void tabConnectionStatusOnline()
        {
            PrivateChatTab.Header = "Private Chat";
            GroupChatTab.Header = "Group Chat";
            BroadcastChatTab.Header = "Broadcast Chat";

        }




        /* 
        * METHOD : tabConnectionStatusOffline()  
        *
        * DESCRIPTION : informs the user the their chat tabs are not activated .
        * This is the default status of the tabs when the client is activated.
        * PARAMETERS : N/A
        *
        * RETURNS    : N/A 
        */
        public void tabConnectionStatusOffline()
        {
            PrivateChatTab.Header = "Private Chat - offline";
            GroupChatTab.Header = "Group Chat - offline";
            BroadcastChatTab.Header = "Broadcast Chat - offline";
        }





        /* 
        * EVENT : selectButton_Click()  
        *
        * DESCRIPTION :  Select individual clients in the list box.  
       *  Will update the relevant tabs with graphical notifications depending on which packet is determining 
       *  the message type.
        * PARAMETERS : object : sender
        *            : RoutedEventArgs : e
        *
        * RETURNS    : N/A 
        */
        private void selectButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClientListBox.Items.Count > 0)
            {
                SendButton.IsEnabled = true;
                guiPacketBuffer.RequestToChat = "";
                guiPacketBuffer.MessageType = "";


                if (ClientListBox.SelectedItems.Count > 1)
                {
                    GroupChatTab.Focus();
                    GroupChatTab.Foreground = new SolidColorBrush(Colors.Goldenrod);
                    PrivateChatTab.Foreground = new SolidColorBrush(Colors.Black);
                    BroadcastChatTab.Foreground = new SolidColorBrush(Colors.Black);

                    Array clientSelected = Array.CreateInstance(typeof(string), ClientListBox.SelectedItems.Count);
                    ClientListBox.SelectedItems.CopyTo(clientSelected, 0);

                    //Messages all clients that local user wishes to communicate with. 
                    for (int i = 0; i < clientSelected.Length; i++)
                    {
                        string aliasBuffer = clientSelected.GetValue(i).ToString();
                        string[] removeStatus = aliasBuffer.Split(' ');
                        guiPacketBuffer.RequestToChat = guiPacketBuffer.RequestToChat + clientSelected.GetValue(i);
                        if (i < (clientSelected.Length - 1))
                        {
                            guiPacketBuffer.RequestToChat = guiPacketBuffer.RequestToChat + "|"; //delimites clients with pipe symbol 
                        }
                        guiPacketBuffer.MessageType = "G"; //for group chat type communications 
                    }
                }
                else
                {
                    if (ClientListBox.SelectedItems.Count > 0)
                    {
                        PrivateChatTab.Focus();
                        PrivateChatTab.Foreground = new SolidColorBrush(Colors.Goldenrod);
                        GroupChatTab.Foreground = new SolidColorBrush(Colors.Black);
                        BroadcastChatTab.Foreground = new SolidColorBrush(Colors.Black);

                        string aliasBuffer = ClientListBox.SelectedItem.ToString();
                        string[] removeStatus = aliasBuffer.Split(' '); //delimites with blank space 
                        guiPacketBuffer.Alias = removeStatus[0];
                        guiPacketBuffer.MessageType = "P"; //for private chat type communications 
                    }
                    else
                    {
                        MessageBox.Show("Please select a client before clicking the select button.");
                    }
                }
            }
            else
            {
                MessageBox.Show("You must have online clients in your queue in order to send a message.");
            }

        }


        /* 
       * METHOD : selectAllButton_Click() 
       *
       * DESCRIPTION : Select all clients in the list box.  For a broadcast chat. 
       *  Will update the broad cast chat tab with graphical notifications depending on which packet is determining 
       *  the message type.
       *
       * RETURNS    : N/A 
       */
        private void selectAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClientListBox.Items.Count > 0)
            {
                BroadcastChatTab.Focus();
                BroadcastChatTab.Foreground = new SolidColorBrush(Colors.Goldenrod);
                PrivateChatTab.Foreground = new SolidColorBrush(Colors.Black);
                GroupChatTab.Foreground = new SolidColorBrush(Colors.Black);
                ClientListBox.SelectAll();

                // enable button
                SendButton.IsEnabled = true;

                // Prepares for a broadcast chat - text all users on all computers on the same server. 
                Array clientSelected = Array.CreateInstance(typeof(string), ClientListBox.SelectedItems.Count);
                ClientListBox.SelectedItems.CopyTo(clientSelected, 0);
                for (int i = 0; i < ClientListBox.Items.Count; i++)
                {
                    guiPacketBuffer.RequestToChat = guiPacketBuffer.RequestToChat + clientSelected.GetValue(i);
                    if (i < (clientSelected.Length - 1))
                    {
                        guiPacketBuffer.RequestToChat = guiPacketBuffer.RequestToChat + "|";
                    }
                    guiPacketBuffer.MessageType = "B"; // signals broadcast chat type 
                }
            }
            else
            {
                MessageBox.Show("You must have online clients in your queue in order to send a message.");
            }
        }


        /* 
       * METHOD : Deselect_Click()  
       *
       * DESCRIPTION : Deselects all clients that have been selected in the 
       * list box.
       *
       * RETURNS    : N/A 
       */
        private void Deselect_Click(object sender, RoutedEventArgs e)
        {
         if (ClientListBox.Items.Count > 0)
          {
                ClientListBox.UnselectAll();
          }
          else
          {
                MessageBox.Show("You must have online clients in your queue in order to send a message.");
          }
            
        }


        /* 
       * METHOD : ClearChatMessageField_Click()  
       *
       * DESCRIPTION :  Clear the message field when button is clicked.
       *
       * RETURNS    : N/A 
       */
        private void ClearChatMessageField_Click(object sender, RoutedEventArgs e)
        {
            MessageField.Clear();
        }


        /* 
       * METHOD : Close_Client()  
       *
       * DESCRIPTION : Purge the message queue is component model is closed manually. 
       *
       * RETURNS    : N/A 
       */
        private void Close_Client(object sender, System.ComponentModel.CancelEventArgs e)
        {
            messageQueue.Purge();
        }


    } // End of definitions
}
